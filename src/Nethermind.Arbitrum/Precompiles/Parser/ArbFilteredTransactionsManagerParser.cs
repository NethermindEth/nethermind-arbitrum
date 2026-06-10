// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbFilteredTransactionsManagerParser : IArbitrumPrecompile<ArbFilteredTransactionsManagerParser>
{
    public static readonly ArbFilteredTransactionsManagerParser Instance = new();

    public static Address Address { get; } = ArbFilteredTransactionsManager.Address;

    /// <summary>
    /// Implements Nitro's FreeAccessPrecompile wrapper semantics: transaction filterers pay
    /// nothing; non-filterers pay only the cost of the filterer membership check.
    /// </summary>
    public GasConsumptionPolicy ShouldConsumeGas(IWorldState worldState, Address caller)
    {
        IBurner checkBurner = new SystemBurner();
        ArbosState checkState = ArbosState.OpenArbosState(worldState, checkBurner, NullLogger.Instance);

        bool isFilterer = checkState.TransactionFilterers.IsMember(caller);
        
        return isFilterer
            ? new GasConsumptionPolicy { IsFree = true, CheckCost = new MultiGas() }
            : new GasConsumptionPolicy { IsFree = false, CheckCost = checkBurner.BurnedMultiGas };
    }

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbFilteredTransactionsManager.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint AddFilteredTransactionId = Solgen.ArbFilteredTransactionsManager.Methods.AddFilteredTransaction;
    private const uint DeleteFilteredTransactionId = Solgen.ArbFilteredTransactionsManager.Methods.DeleteFilteredTransaction;
    private const uint IsTransactionFilteredId = Solgen.ArbFilteredTransactionsManager.Methods.IsTransactionFiltered;

    static ArbFilteredTransactionsManagerParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { AddFilteredTransactionId, AddFilteredTransaction },
            { DeleteFilteredTransactionId, DeleteFilteredTransaction },
            { IsTransactionFilteredId, IsTransactionFiltered }
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[AddFilteredTransactionId].ArbOSVersion = ArbosVersion.TransactionFiltering;
        PrecompileFunctionDescription[DeleteFilteredTransactionId].ArbOSVersion = ArbosVersion.TransactionFiltering;
        PrecompileFunctionDescription[IsTransactionFilteredId].ArbOSVersion = ArbosVersion.TransactionFiltering;
    }

    private static byte[] AddFilteredTransaction(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[AddFilteredTransactionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 txHash = new((byte[])decoded[0]);
        ArbFilteredTransactionsManager.AddFilteredTransaction(context, txHash);
        return [];
    }

    private static byte[] DeleteFilteredTransaction(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[DeleteFilteredTransactionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 txHash = new((byte[])decoded[0]);
        ArbFilteredTransactionsManager.DeleteFilteredTransaction(context, txHash);
        return [];
    }

    private static byte[] IsTransactionFiltered(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[IsTransactionFilteredId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 txHash = new((byte[])decoded[0]);
        bool isFiltered = ArbFilteredTransactionsManager.IsTransactionFiltered(context, txHash);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            isFiltered
        );
    }
}
