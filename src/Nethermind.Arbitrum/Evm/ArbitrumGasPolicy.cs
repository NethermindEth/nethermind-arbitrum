// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.GasPolicy;
using Nethermind.Int256;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Test")]

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Arbitrum multidimensional gas policy with inline MultiGas tracking.
/// Embeds EthereumGasPolicy for single-dimensional gas tracking and adds MultiGas breakdown.
/// </summary>
public struct ArbitrumGasPolicy : IGasPolicy<ArbitrumGasPolicy>
{
    private EthereumGasPolicy _ethereum;
    private MultiGas _accumulated;
    private MultiGas _retained;
    private ulong _allocatedByParent; // Total gas allocated by parent (includes stipend if value transfer)
    private IArbitrumTxTracer? _tracer;

    /// <summary>
    /// Returns a readonly copy of the accumulated multi-gas breakdown.
    /// </summary>
    public readonly MultiGas GetAccumulated() => _accumulated;

    /// <summary>
    /// Returns net accumulated gas (accumulated - retained).
    /// </summary>
    public readonly MultiGas GetTotalAccumulated()
    {
        (MultiGas result, bool underflow) = _accumulated.SafeSub(_retained);
        Debug.Assert(!underflow, "MultiGas underflow: retained > accumulated");
        return underflow ? _accumulated.SaturatingSub(_retained) : result;
    }

    /// <summary>
    /// Sets the tracer for gas dimension capture.
    /// The tracer stores before-state and computes gas dimension logs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTracer(ref ArbitrumGasPolicy gas, IArbitrumTxTracer? tracer)
    {
        gas._tracer = tracer;
    }

    /// <summary>
    /// Applies the final transaction refund to the accumulated MultiGas.
    /// Called at the transaction end after calculating the capped refund.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyRefund(ref ArbitrumGasPolicy gas, ulong refund)
    {
        gas._accumulated = gas._accumulated.WithRefund(refund);
    }

    /// <summary>
    /// Creates a new ArbitrumGasPolicy instance from a long value.
    /// Stores the allocated gas for retained gas tracking in nested calls.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy FromLong(long value) => new()
    {
        _ethereum = EthereumGasPolicy.FromLong(value),
        _allocatedByParent = (ulong)value // Default: assume all gas was allocated
    };

    /// <summary>
    /// Consume gas for code deposit during CREATE/CREATE2.
    /// Tracks as StorageGrowth.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConsumeCodeDeposit(ref ArbitrumGasPolicy gas, long cost)
    {
        EthereumGasPolicy.ConsumeCodeDeposit(ref gas._ethereum, cost);
        gas._accumulated.Increment(ResourceKind.StorageGrowth, (ulong)cost);
    }

    /// <summary>
    /// Creates a new ArbitrumGasPolicy with specified available gas while preserving
    /// an existing MultiGas breakdown. Used by GasChargingHook to preserve intrinsic
    /// gas breakdown when creating available gas for EVM execution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy FromLongWithAccumulated(long value, in MultiGas accumulated) => new()
    {
        _ethereum = EthereumGasPolicy.FromLong(value),
        _allocatedByParent = (ulong)value,
        _accumulated = accumulated
    };

    /// <summary>
    /// Get remaining gas for OOG checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetRemainingGas(in ArbitrumGasPolicy gas)
        => EthereumGasPolicy.GetRemainingGas(in gas._ethereum);

    /// <summary>
    /// Consume gas for an operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Consume(ref ArbitrumGasPolicy gas, long cost)
    {
        EthereumGasPolicy.Consume(ref gas._ethereum, cost);
        gas._accumulated.Increment(ResourceKind.Computation, (ulong)cost);
    }

    /// <summary>
    /// Consume gas for SelfDestruct operation.
    /// Based on observed Nitro behavior: the EIP150 cost (5000) is split as
    /// 100 Computation (warm read) + 4900 StorageAccess.
    /// </summary>
    public static bool ConsumeSelfDestructGas(ref ArbitrumGasPolicy gas)
    {
        try { System.IO.File.AppendAllText("/tmp/arb-selfdestruct-debug.txt", $"[{DateTime.UtcNow:HH:mm:ss.fff}] ConsumeSelfDestructGas CALLED\n"); } catch { }
        if (!EthereumGasPolicy.ConsumeSelfDestructGas(ref gas._ethereum))
            return false;
        // Split EIP150 cost: 100 Computation + 4900 StorageAccess (matching Nitro behavior)
        gas._accumulated.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
        gas._accumulated.Increment(ResourceKind.StorageAccess, GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
        try { System.IO.File.AppendAllText("/tmp/arb-selfdestruct-debug.txt", $"[{DateTime.UtcNow:HH:mm:ss.fff}] Added 100 Comp + 4900 SA\n"); } catch { }
        return true;
    }

    /// <summary>
    /// Refund gas from a child call frame.
    /// Merges the child's NET gas usage (accumulated - retained) into the parent.
    /// Tracks the TOTAL gas allocated by the parent (includes stipend for value transfers).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Refund(ref ArbitrumGasPolicy gas, in ArbitrumGasPolicy childGas)
    {
        EthereumGasPolicy.Refund(ref gas._ethereum, in childGas._ethereum);
        // Add child's NET usage (already excludes child's retained from nested calls)
        MultiGas childNet = childGas.GetTotalAccumulated();
        gas._accumulated.Add(in childNet);
        // Track TOTAL gas allocated to the child (including stipend for value transfers).
        // The stipend accounts for the difference between single-dim refund and multigas accounting.
        gas._retained.Increment(ResourceKind.Computation, childGas._allocatedByParent);
    }

    /// <summary>
    /// Add external MultiGas (e.g., from ArbOS storage operations via IBurner) to accumulated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddToAccumulated(ref ArbitrumGasPolicy gas, in MultiGas toAdd)
    {
        gas._accumulated.Add(in toAdd);
    }

    /// <summary>
    /// Mark the gas state as out of gas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetOutOfGas(ref ArbitrumGasPolicy gas)
        => EthereumGasPolicy.SetOutOfGas(ref gas._ethereum);

    /// <summary>
    /// Charges gas for accessing an account, including potential delegation lookups (interface implementation).
    /// </summary>
    public static bool ConsumeAccountAccessGasWithDelegation(ref ArbitrumGasPolicy gas,
        IReleaseSpec spec,
        ref readonly StackAccessTracker accessTracker,
        bool isTracingAccess,
        Address address,
        Address? delegated,
        bool chargeForWarm = true)
    {
        if (!spec.UseHotAndColdStorage)
            return true;

        return ConsumeAccountAccessGas(ref gas, spec, in accessTracker, isTracingAccess, address, chargeForWarm)
               && (delegated is null
                   || ConsumeAccountAccessGas(ref gas, spec, in accessTracker, isTracingAccess, delegated, chargeForWarm));
    }

    /// <summary>
    /// Charges gas for accessing an account based on a cold / warm state (interface implementation).
    /// Cold access splits cost: (ColdAccountAccess - WarmStateRead) as StorageAccess, WarmStateRead as Computation.
    /// Warm access charges WarmStateRead as Computation.
    /// See rationale: https://github.com/OffchainLabs/nitro/blob/master/docs/decisions/0002-multi-dimensional-gas-metering.md
    /// </summary>
    public static bool ConsumeAccountAccessGas(ref ArbitrumGasPolicy gas,
        IReleaseSpec spec,
        ref readonly StackAccessTracker accessTracker,
        bool isTracingAccess,
        Address address,
        bool chargeForWarm = true)
    {
        if (!spec.UseHotAndColdStorage)
            return true;
        if (isTracingAccess)
            accessTracker.WarmUp(address);

        if (!spec.IsPrecompile(address) && accessTracker.WarmUp(address))
        {
            // Cold account access: split into StorageAccess + Computation (matching Nitro gasEip2929AccountCheck)
            long coldDelta = GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead;
            if (!EthereumGasPolicy.UpdateGas(ref gas._ethereum, GasCostOf.ColdAccountAccess))
                return false;
            gas._accumulated.Increment(ResourceKind.StorageAccess, (ulong)coldDelta);
            gas._accumulated.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
            return true;
        }
        return !chargeForWarm || UpdateGasWithResource(ref gas, GasCostOf.WarmStateRead, ResourceKind.Computation);
    }

    /// <summary>
    /// Charges gas for accessing the SELFDESTRUCT beneficiary account.
    /// Unlike regular ConsumeAccountAccessGas, cold access is charged as FULL StorageAccess (no Computation split).
    /// This matches Nitro's makeSelfdestructGasFn in operations_acl.go which charges ColdAccountAccessCostEIP2929
    /// entirely to ResourceKindStorageAccess.
    /// </summary>
    public static bool ConsumeSelfDestructBeneficiaryAccessGas(ref ArbitrumGasPolicy gas,
        IReleaseSpec spec,
        ref readonly StackAccessTracker accessTracker,
        bool isTracingAccess,
        Address address)
    {
        if (!spec.UseHotAndColdStorage)
            return true;
        if (isTracingAccess)
            accessTracker.WarmUp(address);

        if (!spec.IsPrecompile(address) && accessTracker.WarmUp(address))
        {
            // SELFDESTRUCT beneficiary cold access: FULL cost to StorageAccess (no Computation split)
            // This matches Nitro's makeSelfdestructGasFn:
            //   multiGas = multiGas.SaturatingIncrement(multigas.ResourceKindStorageAccess, params.ColdAccountAccessCostEIP2929)
            if (!EthereumGasPolicy.UpdateGas(ref gas._ethereum, GasCostOf.ColdAccountAccess))
                return false;
            gas._accumulated.Increment(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess);
            return true;
        }
        // Warm access: no gas charged (matches Nitro - only cold access adds to multiGas in makeSelfdestructGasFn)
        return true;
    }

    /// <summary>
    /// Charges gas for accessing a storage cell based on a cold / warm state (interface implementation).
    /// For SLOAD: Cold access splits cost into StorageAccess + Computation (matching Nitro gasSLoadEIP2929).
    /// For SSTORE: Cold access charges full cost to StorageAccess only (matching Nitro gasSStoreEIP2929).
    /// Warm access charges WarmStateRead as Computation (for SLOAD only).
    /// See rationale: https://github.com/OffchainLabs/nitro/blob/master/docs/decisions/0002-multi-dimensional-gas-metering.md
    /// </summary>
    public static bool ConsumeStorageAccessGas(ref ArbitrumGasPolicy gas,
        ref readonly StackAccessTracker accessTracker,
        bool isTracingAccess,
        in StorageCell storageCell,
        StorageAccessType storageAccessType,
        IReleaseSpec spec)
    {
        if (!spec.UseHotAndColdStorage)
            return true;
        if (isTracingAccess)
            accessTracker.WarmUp(in storageCell);

        if (accessTracker.WarmUp(in storageCell))
        {
            // Cold slot access handling differs by operation type:
            // - SLOAD: split into StorageAccess + Computation (matching Nitro gasSLoadEIP2929)
            // - SSTORE: full cost to StorageAccess only (matching Nitro gasSStoreEIP2929)
            if (!EthereumGasPolicy.UpdateGas(ref gas._ethereum, GasCostOf.ColdSLoad))
                return false;

            if (storageAccessType == StorageAccessType.SLOAD)
            {
                // SLOAD splits cold access: (ColdSLoad - WarmStateRead) to StorageAccess, WarmStateRead to Computation
                long coldDelta = GasCostOf.ColdSLoad - GasCostOf.WarmStateRead;
                gas._accumulated.Increment(ResourceKind.StorageAccess, (ulong)coldDelta);
                gas._accumulated.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
            }
            else
            {
                // SSTORE: full cold cost to StorageAccess (no split)
                gas._accumulated.Increment(ResourceKind.StorageAccess, GasCostOf.ColdSLoad);
            }
            return true;
        }
        return storageAccessType != StorageAccessType.SLOAD ||
               UpdateGasWithResource(ref gas, GasCostOf.WarmStateRead, ResourceKind.Computation);
    }

    /// <summary>
    /// Updates gas for memory expansion.
    /// Tracks as Computation resource.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool UpdateMemoryCost(ref ArbitrumGasPolicy gas,
        in UInt256 position,
        in UInt256 length, VmState<ArbitrumGasPolicy> vmState)
    {
        long memoryCost = vmState.Memory.CalculateMemoryCost(in position, length, out bool outOfGas);
        if (outOfGas)
            return false;
        return memoryCost == 0L || UpdateGasWithResource(ref gas, memoryCost, ResourceKind.Computation);
    }

    /// <summary>
    /// Deducts a specified gas cost.
    /// Tracks as a Computation resource by default.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool UpdateGas(ref ArbitrumGasPolicy gas, long gasCost)
    {
        if (!EthereumGasPolicy.UpdateGas(ref gas._ethereum, gasCost))
            return false;
        gas._accumulated.Increment(ResourceKind.Computation, (ulong)gasCost);
        return true;
    }

    /// <summary>
    /// Refunds gas by adding the specified amount back to the available gas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateGasUp(ref ArbitrumGasPolicy gas, long refund)
        => EthereumGasPolicy.UpdateGasUp(ref gas._ethereum, refund);

    /// <summary>
    /// Internal helper to deduct gas with resource tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UpdateGasWithResource(
        ref ArbitrumGasPolicy gas,
        long gasCost,
        ResourceKind resourceKind)
    {
        if (!EthereumGasPolicy.UpdateGas(ref gas._ethereum, gasCost))
            return false;
        gas._accumulated.Increment(resourceKind, (ulong)gasCost);
        return true;
    }

    /// <summary>
    /// Charges gas for SSTORE write operation (after cold/warm access cost).
    /// Tracks as StorageGrowth for slot creation, StorageAccess for modification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeStorageWrite(ref ArbitrumGasPolicy gas, bool isSlotCreation, IReleaseSpec spec)
    {
        if (!EthereumGasPolicy.ConsumeStorageWrite(ref gas._ethereum, isSlotCreation, spec))
            return false;
        long cost = isSlotCreation ? GasCostOf.SSet : spec.GasCosts.SStoreResetCost;
        gas._accumulated.Increment(isSlotCreation ? ResourceKind.StorageGrowth : ResourceKind.StorageAccess, (ulong)cost);
        return true;
    }

    /// <summary>
    /// Charges gas for CALL value transfer.
    /// Tracks as Computation resource.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeCallValueTransfer(ref ArbitrumGasPolicy gas)
    {
        if (!EthereumGasPolicy.ConsumeCallValueTransfer(ref gas._ethereum))
            return false;
        gas._accumulated.Increment(ResourceKind.Computation, GasCostOf.CallValue);
        return true;
    }

    /// <summary>
    /// Charges gas for new account creation.
    /// Tracks as StorageGrowth resource.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeNewAccountCreation(ref ArbitrumGasPolicy gas)
    {
        if (!EthereumGasPolicy.ConsumeNewAccountCreation(ref gas._ethereum))
            return false;
        gas._accumulated.Increment(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
        return true;
    }

    /// <summary>
    /// Charges gas for LOG emission with topic and data costs.
    /// Splits gas between Computation and HistoryGrowth.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeLogEmission(ref ArbitrumGasPolicy gas, long topicCount, long dataSize)
    {
        if (!EthereumGasPolicy.ConsumeLogEmission(ref gas._ethereum, topicCount, dataSize))
            return false;
        // Base cost -> Computation
        gas._accumulated.Increment(ResourceKind.Computation, GasCostOf.Log);
        // Per-topic split: HistoryGrowth for storage, Computation for bloom filter work
        gas._accumulated.Increment(ResourceKind.HistoryGrowth, (ulong)topicCount * (ulong)ArbitrumGasCostOf.LogTopicHistoryGas);
        gas._accumulated.Increment(ResourceKind.Computation, (ulong)topicCount * (ulong)ArbitrumGasCostOf.LogTopicComputationGas);
        // Data payload -> HistoryGrowth
        gas._accumulated.Increment(ResourceKind.HistoryGrowth, (ulong)dataSize * GasCostOf.LogData);
        return true;
    }

    /// <summary>
    /// Consumes gas for data copy operations with multi-gas categorization.
    /// EXTCODECOPY data cost -> StorageAccess (reading from state trie)
    /// Other copy ops data cost -> Computation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConsumeDataCopyGas(ref ArbitrumGasPolicy gas, bool isExternalCode, long baseCost, long dataCost)
    {
        EthereumGasPolicy.Consume(ref gas._ethereum, baseCost + dataCost);

        // Base cost always computation
        gas._accumulated.Increment(ResourceKind.Computation, (ulong)baseCost);

        // Word cost: StorageAccess for EXTCODECOPY, Computation for others
        ResourceKind wordResource = isExternalCode ? ResourceKind.StorageAccess : ResourceKind.Computation;
        gas._accumulated.Increment(wordResource, (ulong)dataCost);
    }

    /// <summary>
    /// Hook called before instruction execution for gas dimension tracing.
    /// Delegates to the tracer to capture pre-execution gas state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnBeforeInstructionTrace(in ArbitrumGasPolicy gas, int pc, Instruction instruction, int depth)
    {
        IArbitrumTxTracer? tracer = gas._tracer;
        // Depth is 0-based from VmState.Env.CallDepth, convert to 1-based for Nitro compatibility
        tracer?.BeginGasDimensionCapture(pc, instruction, depth + 1, gas.GetAccumulated());
    }

    /// <summary>
    /// Hook called after instruction execution for gas dimension tracing.
    /// Delegates to the tracer to capture post-execution gas state and emit dimension log.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAfterInstructionTrace(in ArbitrumGasPolicy gas)
    {
        IArbitrumTxTracer? tracer = gas._tracer;
        tracer?.EndGasDimensionCapture(gas.GetAccumulated());
    }

    /// <summary>
    /// Returns the maximum of two gas values.
    /// Used for MinimalGas calculation in IntrinsicGas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy Max(in ArbitrumGasPolicy a, in ArbitrumGasPolicy b)
        => EthereumGasPolicy.GetRemainingGas(in a._ethereum) >= EthereumGasPolicy.GetRemainingGas(in b._ethereum) ? a : b;

    /// <summary>
    /// Calculates intrinsic gas for a transaction with MultiGas breakdown.
    /// </summary>
    public static IntrinsicGas<ArbitrumGasPolicy> CalculateIntrinsicGas(Transaction tx, IReleaseSpec spec)
    {
        long tokensInCallData = IGasPolicy<ArbitrumGasPolicy>.CalculateTokensInCallData(tx, spec);

        // Get base intrinsic gas from EthereumGasPolicy
        IntrinsicGas<EthereumGasPolicy> ethIntrinsic = EthereumGasPolicy.CalculateIntrinsicGas(tx, spec);
        ArbitrumGasPolicy gas = new() { _ethereum = ethIntrinsic.Standard };

        // Now build the MultiGas breakdown (Arbitrum-specific categorization)
        // 1. Computation: Base transaction cost
        long baseTxGas = tx.IsContractCreation && spec.IsEip2Enabled
            ? GasCostOf.TxCreate + GasCostOf.Transaction
            : GasCostOf.Transaction;
        gas._accumulated.Increment(ResourceKind.Computation, (ulong)baseTxGas);

        // 2. Computation: Init code cost (EIP-3860)
        if (tx.IsContractCreation && spec.IsEip3860Enabled && tx.Data.Length > 0)
        {
            long initCodeCost = EvmCalculations.Div32Ceiling((UInt256)tx.Data.Length) * GasCostOf.InitCodeWord;
            gas._accumulated.Increment(ResourceKind.Computation, (ulong)initCodeCost);
        }

        // 3. L2Calldata: Transaction data bytes (matching Nitro state_transition.go:127,132)
        if (tx.Data.Length > 0)
        {
            ReadOnlySpan<byte> data = tx.Data.Span;
            int zeroCount = data.CountZeros();
            int nonZeroCount = data.Length - zeroCount;

            // Charge separately for zero and non-zero bytes as L2Calldata
            ulong nonZeroGas = (ulong)(spec.IsEip2028Enabled ? GasCostOf.TxDataNonZeroEip2028 : GasCostOf.TxDataNonZero);
            gas._accumulated.Increment(ResourceKind.L2Calldata, (ulong)nonZeroCount * nonZeroGas);
            gas._accumulated.Increment(ResourceKind.L2Calldata, (ulong)zeroCount * (ulong)GasCostOf.TxDataZero);
        }

        // 4. StorageAccess: Access list costs (EIP-2930)
        if (tx.AccessList is not null)
        {
            (int addressesCount, int storageKeysCount) = tx.AccessList.Count;
            long accessListCost = addressesCount * GasCostOf.AccessAccountListEntry
                + storageKeysCount * GasCostOf.AccessStorageListEntry;
            gas._accumulated.Increment(ResourceKind.StorageAccess, (ulong)accessListCost);
        }

        // 5. StorageGrowth: Authorization list (EIP-7702)
        if (tx.AuthorizationList is not null)
        {
            long authCost = tx.AuthorizationList.Length * GasCostOf.NewAccount;
            gas._accumulated.Increment(ResourceKind.StorageGrowth, (ulong)authCost);
        }

        long floorCost = IGasPolicy<ArbitrumGasPolicy>.CalculateFloorCost(tokensInCallData, spec);
        ArbitrumGasPolicy floorGas = FromLong(floorCost);

        return new IntrinsicGas<ArbitrumGasPolicy>(gas, floorGas);
    }

    /// <summary>
    /// Creates available gas from gas limit minus intrinsic gas, preserving the multi-gas breakdown.
    /// The accumulated breakdown from intrinsic gas is preserved for tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy CreateAvailableFromIntrinsic(long gasLimit, in ArbitrumGasPolicy intrinsicGas)
        => intrinsicGas with { _ethereum = EthereumGasPolicy.CreateAvailableFromIntrinsic(gasLimit, in intrinsicGas._ethereum) };
}
