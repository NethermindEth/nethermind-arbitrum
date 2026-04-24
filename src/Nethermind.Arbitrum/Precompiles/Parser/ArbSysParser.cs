// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbSysParser : IArbitrumPrecompile<ArbSysParser>
{
    public static readonly ArbSysParser Instance = new();

    public static Address Address { get; } = ArbSys.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbSys.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint ArbBlockNumberId = Solgen.ArbSys.Methods.ArbBlockNumber;
    private const uint ArbBlockHashId = Solgen.ArbSys.Methods.ArbBlockHash;
    private const uint ArbChainIdId = Solgen.ArbSys.Methods.ArbChainID;
    private const uint ArbOSVersionId = Solgen.ArbSys.Methods.ArbOSVersion;
    private const uint GetStorageGasAvailableId = Solgen.ArbSys.Methods.GetStorageGasAvailable;
    private const uint IsTopLevelCallId = Solgen.ArbSys.Methods.IsTopLevelCall;
    private const uint MapL1SenderContractAddressToL2AliasId = Solgen.ArbSys.Methods.MapL1SenderContractAddressToL2Alias;
    private const uint WasMyCallersAddressAliasedId = Solgen.ArbSys.Methods.WasMyCallersAddressAliased;
    private const uint MyCallersAddressWithoutAliasingId = Solgen.ArbSys.Methods.MyCallersAddressWithoutAliasing;
    private const uint SendTxToL1Id = Solgen.ArbSys.Methods.SendTxToL1;
    private const uint SendMerkleTreeStateId = Solgen.ArbSys.Methods.SendMerkleTreeState;
    private const uint WithdrawEthId = Solgen.ArbSys.Methods.WithdrawEth;

    static ArbSysParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { ArbBlockNumberId, ArbBlockNumber },
            { ArbBlockHashId, ArbBlockHash },
            { ArbChainIdId, ArbChainId },
            { ArbOSVersionId, ArbOSVersion },
            { GetStorageGasAvailableId, GetStorageGasAvailable },
            { IsTopLevelCallId, IsTopLevelCall },
            { MapL1SenderContractAddressToL2AliasId, MapL1SenderContractAddressToL2Alias },
            { WasMyCallersAddressAliasedId, WasMyCallersAddressAliased },
            { MyCallersAddressWithoutAliasingId, MyCallersAddressWithoutAliasing },
            { SendTxToL1Id, SendTxToL1 },
            { SendMerkleTreeStateId, SendMerkleTreeState },
            { WithdrawEthId, WithdrawEth },
        }.ToFrozenDictionary();
    }

    private static byte[] ArbBlockNumber(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbSys.ArbBlockNumber(context).ToBigEndian();

    private static byte[] ArbBlockHash(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[ArbBlockHashId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 arbBlockNum = (UInt256)decoded[0];
        Hash256 l2BlockHash = ArbSys.ArbBlockHash(context, arbBlockNum);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            l2BlockHash
        );
    }

    private static byte[] ArbChainId(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
     => ArbSys.ArbChainID(context).ToBigEndian();

    private static byte[] ArbOSVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
     => ArbSys.ArbOSVersion(context).ToBigEndian();

    private static byte[] GetStorageGasAvailable(ArbitrumPrecompileExecutionContext _, ReadOnlySpan<byte> __)
     => ArbSys.GetStorageGasAvailable().ToBigEndian();

    private static byte[] IsTopLevelCall(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        bool result = ArbSys.IsTopLevelCall(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[IsTopLevelCallId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] MapL1SenderContractAddressToL2Alias(ArbitrumPrecompileExecutionContext _, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[MapL1SenderContractAddressToL2AliasId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address sender = (Address)decoded[0];
        Address alias = ArbSys.MapL1SenderContractAddressToL2Alias(sender);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            alias
        );
    }

    private static byte[] WasMyCallersAddressAliased(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        bool result = ArbSys.WasMyCallersAddressAliased(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[WasMyCallersAddressAliasedId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] MyCallersAddressWithoutAliasing(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address address = ArbSys.MyCallersAddressWithoutAliasing(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[MyCallersAddressWithoutAliasingId].AbiFunctionDescription.GetReturnInfo().Signature,
            address
        );
    }

    private static byte[] SendTxToL1(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SendTxToL1Id].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address destination = (Address)decoded[0];
        byte[] calldataForL1 = (byte[])decoded[1];

        UInt256 result = ArbSys.SendTxToL1(context, destination, calldataForL1);
        return result.ToBigEndian();
    }

    private static byte[] SendMerkleTreeState(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (UInt256 size, Hash256 root, Hash256[] partials) = ArbSys.SendMerkleTreeState(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SendMerkleTreeStateId].AbiFunctionDescription.GetReturnInfo().Signature,
            [size, root, partials]
        );
    }

    private static byte[] WithdrawEth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[WithdrawEthId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address destination = (Address)decoded[0];
        return ArbSys.WithdrawEth(context, destination).ToBigEndian();
    }
}
