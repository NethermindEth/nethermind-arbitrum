using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbSysParser : IArbitrumPrecompile<ArbSysParser>
{
    public static readonly ArbSysParser Instance = new();

    public static Address Address { get; } = ArbSys.Address;

    public static string Abi => ArbSys.Abi;

    public static IReadOnlyDictionary<uint, AbiFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(Abi);

    private static readonly uint _arbBlockNumberId = MethodIdHelper.GetMethodId("arbBlockNumber()");
    private static readonly uint _arbBlockHashId = MethodIdHelper.GetMethodId("arbBlockHash(uint256)");
    private static readonly uint _arbChainIdId = MethodIdHelper.GetMethodId("arbChainID()");
    private static readonly uint _arbOSVersionId = MethodIdHelper.GetMethodId("arbOSVersion()");
    private static readonly uint _getStorageGasAvailableId = MethodIdHelper.GetMethodId("getStorageGasAvailable()");
    private static readonly uint _isTopLevelCallId = MethodIdHelper.GetMethodId("isTopLevelCall()");
    private static readonly uint _mapL1SenderContractAddressToL2AliasId = MethodIdHelper.GetMethodId("mapL1SenderContractAddressToL2Alias(address,address)");
    private static readonly uint _wasMyCallersAddressAliasedId = MethodIdHelper.GetMethodId("wasMyCallersAddressAliased()");
    private static readonly uint _myCallersAddressWithoutAliasingId = MethodIdHelper.GetMethodId("myCallersAddressWithoutAliasing()");
    private static readonly uint _sendTxToL1Id = MethodIdHelper.GetMethodId("sendTxToL1(address,bytes)");
    private static readonly uint _sendMerkleTreeStateId = MethodIdHelper.GetMethodId("sendMerkleTreeState()");
    private static readonly uint _withdrawEthId = MethodIdHelper.GetMethodId("withdrawEth(address)");

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
        UInt256 arbBlockNum = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);

        return ArbSys.ArbBlockHash(context, arbBlockNum).BytesToArray();
    }

    private static byte[] ArbChainID(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
     => ArbSys.ArbChainID(context).ToBigEndian();

    private static byte[] ArbOSVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
     => ArbSys.ArbOSVersion(context).ToBigEndian();

    private static byte[] GetStorageGasAvailable(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
     => ArbSys.GetStorageGasAvailable().ToBigEndian();

    private static byte[] IsTopLevelCall(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        bool result = ArbSys.IsTopLevelCall(context);

        byte[] resultBytes = new byte[Hash256.Size];
        if (result)
            resultBytes[^1] = 1;

        return resultBytes;
    }

    private static byte[] MapL1SenderContractAddressToL2Alias(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address sender = new(accountBytes[(Hash256.Size - Address.Size)..]);

        Address alias = ArbSys.MapL1SenderContractAddressToL2Alias(sender);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        alias.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);

        return abiEncodedResult;
    }

    private static byte[] WasMyCallersAddressAliased(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        bool result = ArbSys.WasMyCallersAddressAliased(context);

        byte[] resultBytes = new byte[Hash256.Size];
        if (result)
            resultBytes[^1] = 1;

        return resultBytes;
    }

    private static byte[] MyCallersAddressWithoutAliasing(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address address = ArbSys.MyCallersAddressWithoutAliasing(context);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        address.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);

        return abiEncodedResult;
    }

    private static byte[] SendTxToL1(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> destinationBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address destination = new(destinationBytes[(Hash256.Size - Address.Size)..]);

        byte[] calldataForL1 = inputData.ToArray();

        UInt256 result = ArbSys.SendTxToL1(context, destination, calldataForL1);

        return result.ToBigEndian();
    }

    private static byte[] SendMerkleTreeState(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (UInt256 size, Hash256 root, Hash256[] partials) = ArbSys.SendMerkleTreeState(context);

        AbiFunctionDescription function = PrecompileFunctions[_sendMerkleTreeStateId];

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [size, root, partials]
        );

        return abiEncodedResult;
    }

    private static byte[] WithdrawEth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> destinationBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address destination = new(destinationBytes[(Hash256.Size - Address.Size)..]);

        UInt256 result = ArbSys.WithdrawEth(context, destination);

        return result.ToBigEndian();
    }
}
