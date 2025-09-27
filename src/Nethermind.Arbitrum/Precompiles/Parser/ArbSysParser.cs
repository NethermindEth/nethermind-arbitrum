using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbSysParser : IArbitrumPrecompile<ArbSysParser>
{
    public static readonly ArbSysParser Instance = new();

    public static Address Address { get; } = ArbSys.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbSys.Abi);

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

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _arbBlockNumberId)
        {
            return ArbBlockNumber(context, inputDataSpan);
        }

        if (methodId == _arbBlockHashId)
        {
            return ArbBlockHash(context, inputDataSpan);
        }

        if (methodId == _arbChainIdId)
        {
            return ArbChainID(context, inputDataSpan);
        }

        if (methodId == _arbOSVersionId)
        {
            return ArbOSVersion(context, inputDataSpan);
        }

        if (methodId == _getStorageGasAvailableId)
        {
            return GetStorageGasAvailable(context, inputDataSpan);
        }

        if (methodId == _isTopLevelCallId)
        {
            return IsTopLevelCall(context, inputDataSpan);
        }

        if (methodId == _mapL1SenderContractAddressToL2AliasId)
        {
            return MapL1SenderContractAddressToL2Alias(context, inputDataSpan);
        }

        if (methodId == _wasMyCallersAddressAliasedId)
        {
            return WasMyCallersAddressAliased(context, inputDataSpan);
        }

        if (methodId == _myCallersAddressWithoutAliasingId)
        {
            return MyCallersAddressWithoutAliasing(context, inputDataSpan);
        }

        if (methodId == _sendTxToL1Id)
        {
            return SendTxToL1(context, inputDataSpan);
        }

        if (methodId == _sendMerkleTreeStateId)
        {
            return SendMerkleTreeState(context, inputDataSpan);
        }

        if (methodId == _withdrawEthId)
        {
            return WithdrawEth(context, inputDataSpan);
        }

        throw new ArgumentException($"Invalid precompile method ID: {methodId}");
    }

    private static byte[] ArbBlockNumber(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbSys.ArbBlockNumber(context).ToBigEndian();

    private static byte[] ArbBlockHash(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[_arbBlockHashId].AbiFunctionDescription;

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
            PrecompileFunctions[_isTopLevelCallId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] MapL1SenderContractAddressToL2Alias(ArbitrumPrecompileExecutionContext _, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[_mapL1SenderContractAddressToL2AliasId].AbiFunctionDescription;

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
            PrecompileFunctions[_wasMyCallersAddressAliasedId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] MyCallersAddressWithoutAliasing(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address address = ArbSys.MyCallersAddressWithoutAliasing(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_myCallersAddressWithoutAliasingId].AbiFunctionDescription.GetReturnInfo().Signature,
            address
        );
    }

    private static byte[] SendTxToL1(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_sendTxToL1Id].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctions[_sendMerkleTreeStateId].AbiFunctionDescription.GetReturnInfo().Signature,
            [size, root, partials]
        );
    }

    private static byte[] WithdrawEth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_withdrawEthId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address destination = (Address)decoded[0];
        return ArbSys.WithdrawEth(context, destination).ToBigEndian();
    }
}
