// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
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
    private ulong _initialGas;

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
        return underflow ? _accumulated.SaturatingSub(_retained) : result;
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

    private const ulong LogTopicHistoryGas = 256;     // 32 bytes * 8 gas/byte
    private const ulong LogTopicComputationGas = 119; // 375 - 256

    /// <summary>
    /// Creates a new ArbitrumGasPolicy instance from a long value.
    /// Stores the initial gas for retained gas tracking in nested calls.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy FromLong(long value) => new()
    {
        _ethereum = EthereumGasPolicy.FromLong(value),
        _initialGas = (ulong)value
    };

    /// <summary>
    /// Creates a new ArbitrumGasPolicy with specified available gas while preserving
    /// an existing MultiGas breakdown. Used by GasChargingHook to preserve intrinsic
    /// gas breakdown when creating available gas for EVM execution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy FromLongWithAccumulated(long value, in MultiGas accumulated) => new()
    {
        _ethereum = EthereumGasPolicy.FromLong(value),
        _initialGas = (ulong)value,
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
    /// </summary>
    public static void ConsumeSelfDestructGas(ref ArbitrumGasPolicy gas)
    {
        // Note from Nitro:
        // SELFDESTRUCT is a special case because it charges for storage access, but it isn't
        // dependent on any input data. We charge a small computational cost for warm access like
        // other multidimensional gas opcodes, and the rest is storage access to delete the
        // contract from the database.
        // Note we only need to cover EIP150 because it is the current cost, and SELFDESTRUCT cost was
        // zero previously.
        EthereumGasPolicy.ConsumeSelfDestructGas(ref gas._ethereum);
        gas._accumulated.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
        gas._accumulated.Increment(ResourceKind.StorageAccess, GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
    }

    /// <summary>
    /// Refund gas from a child call frame.
    /// Merges the child's NET gas usage (accumulated - retained) into the parent.
    /// Tracks the child's initial gas allocation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Refund(ref ArbitrumGasPolicy gas, in ArbitrumGasPolicy childGas)
    {
        EthereumGasPolicy.Refund(ref gas._ethereum, in childGas._ethereum);
        // Add child's NET usage (already excludes child's retained from nested calls)
        MultiGas childNet = childGas.GetTotalAccumulated();
        gas._accumulated.Add(in childNet);
        // Track gas allocated to this child. UpdateGas already added initialGas to
        // _accumulated, so we track it as retained to prevent double-counting.
        gas._retained.Increment(ResourceKind.Computation, childGas._initialGas);
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
            return UpdateGasWithResource(ref gas, GasCostOf.ColdAccountAccess, ResourceKind.StorageAccess);
        return !chargeForWarm || UpdateGasWithResource(ref gas, GasCostOf.WarmStateRead, ResourceKind.Computation);
    }

    /// <summary>
    /// Charges gas for accessing a storage cell based on a cold / warm state (interface implementation).
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
            return UpdateGasWithResource(ref gas, GasCostOf.ColdSLoad, ResourceKind.StorageAccess);
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
        long cost = isSlotCreation ? GasCostOf.SSet : spec.GetSStoreResetCost();
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
        gas._accumulated.Increment(ResourceKind.HistoryGrowth, (ulong)topicCount * LogTopicHistoryGas);
        gas._accumulated.Increment(ResourceKind.Computation, (ulong)topicCount * LogTopicComputationGas);
        // Data payload -> HistoryGrowth
        gas._accumulated.Increment(ResourceKind.HistoryGrowth, (ulong)dataSize * GasCostOf.LogData);
        return true;
    }

    /// <summary>
    /// Consumes gas for code copy operations with multi-gas categorization.
    /// EXTCODECOPY word cost -> StorageAccess (reading from state trie)
    /// Other copy ops word cost -> Computation
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
    /// Returns the maximum of two gas values.
    /// Used for MinimalGas calculation in IntrinsicGas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy Max(in ArbitrumGasPolicy a, in ArbitrumGasPolicy b)
        => EthereumGasPolicy.GetRemainingGas(in a._ethereum) >= EthereumGasPolicy.GetRemainingGas(in b._ethereum) ? a : b;

    /// <summary>
    /// Calculates intrinsic gas for a transaction with MultiGas breakdown.
    /// </summary>
    public static ArbitrumGasPolicy CalculateIntrinsicGas(Transaction tx, IReleaseSpec spec)
    {
        // Get base intrinsic gas from EthereumGasPolicy
        EthereumGasPolicy ethGas = EthereumGasPolicy.CalculateIntrinsicGas(tx, spec);
        ArbitrumGasPolicy gas = new() { _ethereum = ethGas };

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

        // 3. L2Calldata: Transaction data bytes
        if (tx.Data.Length > 0)
        {
            long txDataNonZeroMultiplier = spec.IsEip2028Enabled
                ? GasCostOf.TxDataNonZeroMultiplierEip2028
                : GasCostOf.TxDataNonZeroMultiplier;
            ReadOnlySpan<byte> data = tx.Data.Span;
            ulong totalZeros = (ulong)data.CountZeros();
            ulong tokensInCallData = totalZeros + ((ulong)data.Length - totalZeros) * (ulong)txDataNonZeroMultiplier;
            ulong dataCost = tokensInCallData * GasCostOf.TxDataZero;
            gas._accumulated.Increment(ResourceKind.L2Calldata, dataCost);
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
        if (tx.AuthorizationList is null)
            return gas;
        long authCost = tx.AuthorizationList.Length * GasCostOf.NewAccount;
        gas._accumulated.Increment(ResourceKind.StorageGrowth, (ulong)authCost);

        return gas;
    }

    /// <summary>
    /// Creates available gas from gas limit minus intrinsic gas, preserving the multi-gas breakdown.
    /// The accumulated breakdown from intrinsic gas is preserved for tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGasPolicy CreateAvailableFromIntrinsic(long gasLimit, in ArbitrumGasPolicy intrinsicGas)
        => intrinsicGas with { _ethereum = EthereumGasPolicy.CreateAvailableFromIntrinsic(gasLimit, in intrinsicGas._ethereum) };
}
