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
        = AbiMetadata.GetAllFunctionDescriptions(ArbSys.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _arbBlockNumberId = PrecompileHelper.GetMethodId("arbBlockNumber()");
    private static readonly uint _arbBlockHashId = PrecompileHelper.GetMethodId("arbBlockHash(uint256)");
    private static readonly uint _arbChainIdId = PrecompileHelper.GetMethodId("arbChainID()");
    private static readonly uint _arbOSVersionId = PrecompileHelper.GetMethodId("arbOSVersion()");
    private static readonly uint _getStorageGasAvailableId = PrecompileHelper.GetMethodId("getStorageGasAvailable()");
    private static readonly uint _isTopLevelCallId = PrecompileHelper.GetMethodId("isTopLevelCall()");
    private static readonly uint _mapL1SenderContractAddressToL2AliasId = PrecompileHelper.GetMethodId("mapL1SenderContractAddressToL2Alias(address,address)");
    private static readonly uint _wasMyCallersAddressAliasedId = PrecompileHelper.GetMethodId("wasMyCallersAddressAliased()");
    private static readonly uint _myCallersAddressWithoutAliasingId = PrecompileHelper.GetMethodId("myCallersAddressWithoutAliasing()");
    private static readonly uint _sendTxToL1Id = PrecompileHelper.GetMethodId("sendTxToL1(address,bytes)");
    private static readonly uint _sendMerkleTreeStateId = PrecompileHelper.GetMethodId("sendMerkleTreeState()");
    private static readonly uint _withdrawEthId = PrecompileHelper.GetMethodId("withdrawEth(address)");

    static ArbSysParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _arbBlockNumberId, ArbBlockNumber },
            { _arbBlockHashId, ArbBlockHash },
            { _arbChainIdId, ArbChainID },
            { _arbOSVersionId, ArbOSVersion },
            { _getStorageGasAvailableId, GetStorageGasAvailable },
            { _isTopLevelCallId, IsTopLevelCall },
            { _mapL1SenderContractAddressToL2AliasId, MapL1SenderContractAddressToL2Alias },
            { _wasMyCallersAddressAliasedId, WasMyCallersAddressAliased },
            { _myCallersAddressWithoutAliasingId, MyCallersAddressWithoutAliasing },
            { _sendTxToL1Id, SendTxToL1 },
            { _sendMerkleTreeStateId, SendMerkleTreeState },
            { _withdrawEthId, WithdrawEth },
        }.ToFrozenDictionary();
    }

    private static byte[] ArbBlockNumber(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbSys.ArbBlockNumber(context).ToBigEndian();

    private static byte[] ArbBlockHash(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_arbBlockHashId].AbiFunctionDescription;

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

    private static byte[] ArbChainID(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
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
            PrecompileFunctionDescription[_isTopLevelCallId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] MapL1SenderContractAddressToL2Alias(ArbitrumPrecompileExecutionContext _, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_mapL1SenderContractAddressToL2AliasId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[_wasMyCallersAddressAliasedId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] MyCallersAddressWithoutAliasing(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address address = ArbSys.MyCallersAddressWithoutAliasing(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_myCallersAddressWithoutAliasingId].AbiFunctionDescription.GetReturnInfo().Signature,
            address
        );
    }

    private static byte[] SendTxToL1(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_sendTxToL1Id].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_sendMerkleTreeStateId].AbiFunctionDescription.GetReturnInfo().Signature,
            [size, root, partials]
        );
    }

    private static byte[] WithdrawEth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_withdrawEthId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address destination = (Address)decoded[0];
        return ArbSys.WithdrawEth(context, destination).ToBigEndian();
    }
}
