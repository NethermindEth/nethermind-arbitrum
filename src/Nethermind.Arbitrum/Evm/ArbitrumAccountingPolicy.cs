// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Gas;
using Nethermind.Evm.GasPolicy;
using Nethermind.Int256;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Test")]

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Arbitrum multidimensional gas policy.
/// Operates on ArbitrumGas type, delegating scalar gas operations to EthereumAccountingPolicy
/// while adding MultiGas breakdown tracking.
/// </summary>
public struct ArbitrumAccountingPolicy : IAccountingPolicy<ArbitrumGas, ArbitrumAccountingPolicy>
{
    /// <summary>
    /// Applies the final transaction refund to the accumulated MultiGas.
    /// Called at the transaction end after calculating the capped refund.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyRefund(ref ArbitrumGas gas, ulong refund)
    {
        gas.Accumulated = gas.Accumulated.WithRefund(refund);
    }

    private const ulong LogTopicHistoryGas = 256;     // 32 bytes * 8 gas/byte
    private const ulong LogTopicComputationGas = 119; // 375 - 256

    /// <summary>
    /// Get remaining gas for OOG checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetRemainingGas(in ArbitrumGas gas)
        => EthereumGas.ToLong(in gas.Ethereum);

    /// <summary>
    /// Consume gas for an operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Consume(ref ArbitrumGas gas, long cost)
    {
        EthereumAccountingPolicy.Consume(ref gas.Ethereum, cost);
        gas.Accumulated.Increment(ResourceKind.Computation, (ulong)cost);
    }

    /// <summary>
    /// Consume gas for SelfDestruct operation.
    /// </summary>
    public static void ConsumeSelfDestructGas(ref ArbitrumGas gas)
    {
        // Note from Nitro:
        // SELFDESTRUCT is a special case because it charges for storage access, but it isn't
        // dependent on any input data. We charge a small computational cost for warm access like
        // other multidimensional gas opcodes, and the rest is storage access to delete the
        // contract from the database.
        // Note we only need to cover EIP150 because it is the current cost, and SELFDESTRUCT cost was
        // zero previously.
        EthereumAccountingPolicy.Consume(ref gas.Ethereum, GasCostOf.SelfDestructEip150);
        gas.Accumulated.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
        gas.Accumulated.Increment(ResourceKind.StorageAccess, GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
    }

    /// <summary>
    /// Refund gas from a child call frame.
    /// Merges the child's NET gas usage (accumulated - retained) into the parent.
    /// Tracks the child's initial gas allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Refund(ref ArbitrumGas gas, in ArbitrumGas childGas)
    {
        EthereumAccountingPolicy.Refund(ref gas.Ethereum, in childGas.Ethereum);
        // Add child's NET usage (already excludes child's retained from nested calls)
        MultiGas childNet = childGas.GetTotalAccumulated();
        gas.Accumulated.Add(in childNet);
        // Track gas allocated to this child. UpdateGas already added initialGas to
        // _accumulated, so we track it as retained to prevent double-counting.
        gas.Retained.Increment(ResourceKind.Computation, childGas.InitialGas);
    }

    /// <summary>
    /// Mark the gas state as out of gas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetOutOfGas(ref ArbitrumGas gas)
        => EthereumAccountingPolicy.SetOutOfGas(ref gas.Ethereum);

    /// <summary>
    /// Charges gas for accessing an account, including potential delegation lookups (interface implementation).
    /// </summary>
    public static bool ConsumeAccountAccessGasWithDelegation(ref ArbitrumGas gas,
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
    public static bool ConsumeAccountAccessGas(ref ArbitrumGas gas,
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
    public static bool ConsumeStorageAccessGas(ref ArbitrumGas gas,
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
    public static bool UpdateMemoryCost(ref ArbitrumGas gas,
        in UInt256 position,
        in UInt256 length, VmState<ArbitrumGas> vmState)
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
    public static bool UpdateGas(ref ArbitrumGas gas, long gasCost)
    {
        if (!EthereumAccountingPolicy.UpdateGas(ref gas.Ethereum, gasCost))
            return false;
        gas.Accumulated.Increment(ResourceKind.Computation, (ulong)gasCost);
        return true;
    }

    /// <summary>
    /// Refunds gas by adding the specified amount back to the available gas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateGasUp(ref ArbitrumGas gas, long refund)
        => EthereumAccountingPolicy.UpdateGasUp(ref gas.Ethereum, refund);

    /// <summary>
    /// Internal helper to deduct gas with resource tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UpdateGasWithResource(
        ref ArbitrumGas gas,
        long gasCost,
        ResourceKind resourceKind)
    {
        if (!EthereumAccountingPolicy.UpdateGas(ref gas.Ethereum, gasCost))
            return false;
        gas.Accumulated.Increment(resourceKind, (ulong)gasCost);
        return true;
    }

    /// <summary>
    /// Charges gas for SSTORE write operation (after cold/warm access cost).
    /// Tracks as StorageGrowth for slot creation, StorageAccess for modification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeStorageWrite(ref ArbitrumGas gas, bool isSlotCreation, IReleaseSpec spec)
    {
        if (!EthereumAccountingPolicy.ConsumeStorageWrite(ref gas.Ethereum, isSlotCreation, spec))
            return false;
        long cost = isSlotCreation ? GasCostOf.SSet : spec.GetSStoreResetCost();
        gas.Accumulated.Increment(isSlotCreation ? ResourceKind.StorageGrowth : ResourceKind.StorageAccess, (ulong)cost);
        return true;
    }

    /// <summary>
    /// Charges gas for CALL value transfer.
    /// Tracks as Computation resource.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeCallValueTransfer(ref ArbitrumGas gas)
    {
        if (!EthereumAccountingPolicy.ConsumeCallValueTransfer(ref gas.Ethereum))
            return false;
        gas.Accumulated.Increment(ResourceKind.Computation, GasCostOf.CallValue);
        return true;
    }

    /// <summary>
    /// Charges gas for new account creation.
    /// Tracks as StorageGrowth resource.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeNewAccountCreation(ref ArbitrumGas gas)
    {
        if (!EthereumAccountingPolicy.ConsumeNewAccountCreation(ref gas.Ethereum))
            return false;
        gas.Accumulated.Increment(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
        return true;
    }

    /// <summary>
    /// Charges gas for LOG emission with topic and data costs.
    /// Splits gas between Computation and HistoryGrowth.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConsumeLogEmission(ref ArbitrumGas gas, long topicCount, long dataSize)
    {
        if (!EthereumAccountingPolicy.ConsumeLogEmission(ref gas.Ethereum, topicCount, dataSize))
            return false;
        // Base cost -> Computation
        gas.Accumulated.Increment(ResourceKind.Computation, GasCostOf.Log);
        // Per-topic split: HistoryGrowth for storage, Computation for bloom filter work
        gas.Accumulated.Increment(ResourceKind.HistoryGrowth, (ulong)topicCount * LogTopicHistoryGas);
        gas.Accumulated.Increment(ResourceKind.Computation, (ulong)topicCount * LogTopicComputationGas);
        // Data payload -> HistoryGrowth
        gas.Accumulated.Increment(ResourceKind.HistoryGrowth, (ulong)dataSize * GasCostOf.LogData);
        return true;
    }

    /// <summary>
    /// Consumes gas for code copy operations with multi-gas categorization.
    /// EXTCODECOPY word cost -> StorageAccess (reading from state trie)
    /// Other copy ops word cost -> Computation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConsumeDataCopyGas(ref ArbitrumGas gas, bool isExternalCode, long baseCost, long dataCost)
    {
        EthereumAccountingPolicy.Consume(ref gas.Ethereum, baseCost + dataCost);

        // Base cost always computation
        gas.Accumulated.Increment(ResourceKind.Computation, (ulong)baseCost);

        // Word cost: StorageAccess for EXTCODECOPY, Computation for others
        ResourceKind wordResource = isExternalCode ? ResourceKind.StorageAccess : ResourceKind.Computation;
        gas.Accumulated.Increment(wordResource, (ulong)dataCost);
    }

    /// <summary>
    /// Calculates intrinsic gas for a transaction with MultiGas breakdown.
    /// </summary>
    public static ArbitrumGas CalculateIntrinsicGas(Transaction tx, IReleaseSpec spec)
    {
        // Get base intrinsic gas from EthereumAccountingPolicy
        EthereumGas ethGas = EthereumAccountingPolicy.CalculateIntrinsicGas(tx, spec);
        ArbitrumGas gas = new() { Ethereum = ethGas };

        // Now build the MultiGas breakdown (Arbitrum-specific categorization)
        // 1. Computation: Base transaction cost
        long baseTxGas = tx.IsContractCreation && spec.IsEip2Enabled
            ? GasCostOf.TxCreate + GasCostOf.Transaction
            : GasCostOf.Transaction;
        gas.Accumulated.Increment(ResourceKind.Computation, (ulong)baseTxGas);

        // 2. Computation: Init code cost (EIP-3860)
        if (tx.IsContractCreation && spec.IsEip3860Enabled && tx.Data.Length > 0)
        {
            long initCodeCost = EvmCalculations.Div32Ceiling((UInt256)tx.Data.Length) * GasCostOf.InitCodeWord;
            gas.Accumulated.Increment(ResourceKind.Computation, (ulong)initCodeCost);
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
            gas.Accumulated.Increment(ResourceKind.L2Calldata, dataCost);
        }

        // 4. StorageAccess: Access list costs (EIP-2930)
        if (tx.AccessList is not null)
        {
            (int addressesCount, int storageKeysCount) = tx.AccessList.Count;
            long accessListCost = addressesCount * GasCostOf.AccessAccountListEntry
                + storageKeysCount * GasCostOf.AccessStorageListEntry;
            gas.Accumulated.Increment(ResourceKind.StorageAccess, (ulong)accessListCost);
        }

        // 5. StorageGrowth: Authorization list (EIP-7702)
        if (tx.AuthorizationList is null)
            return gas;
        long authCost = tx.AuthorizationList.Length * GasCostOf.NewAccount;
        gas.Accumulated.Increment(ResourceKind.StorageGrowth, (ulong)authCost);

        return gas;
    }

    /// <summary>
    /// Creates available gas from gas limit minus intrinsic gas, preserving the multi-gas breakdown.
    /// The accumulated breakdown from intrinsic gas is preserved for tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGas CreateAvailableFromIntrinsic(long gasLimit, in ArbitrumGas intrinsicGas)
        => intrinsicGas with { Ethereum = EthereumAccountingPolicy.CreateAvailableFromIntrinsic(gasLimit, in intrinsicGas.Ethereum) };

    /// <summary>
    /// Returns the maximum of two gas values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGas Max(in ArbitrumGas a, in ArbitrumGas b)
        => ArbitrumGas.Max(in a, in b);
}
