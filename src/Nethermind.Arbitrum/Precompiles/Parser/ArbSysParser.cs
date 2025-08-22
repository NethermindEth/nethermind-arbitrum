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

    private static readonly Dictionary<string, AbiFunctionDescription> precompileFunctions;

    private static readonly uint _arbBlockNumberId;
    private static readonly uint _arbBlockHashId;
    private static readonly uint _arbChainIdId;
    private static readonly uint _arbOSVersionId;
    private static readonly uint _getStorageGasAvailableId;
    private static readonly uint _isTopLevelCallId;
    private static readonly uint _mapL1SenderContractAddressToL2AliasId;
    private static readonly uint _wasMyCallersAddressAliasedId;
    private static readonly uint _myCallersAddressWithoutAliasingId;
    private static readonly uint _sendTxToL1Id;
    private static readonly uint _sendMerkleTreeStateId;
    private static readonly uint _withdrawEthId;


    static ArbSysParser()
    {
        precompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbSys.Abi);

        _arbBlockNumberId = MethodIdHelper.GetMethodId("arbBlockNumber()");
        _arbBlockHashId = MethodIdHelper.GetMethodId("arbBlockHash(uint256)");
        _arbChainIdId = MethodIdHelper.GetMethodId("arbChainID()");
        _arbOSVersionId = MethodIdHelper.GetMethodId("arbOSVersion()");
        _getStorageGasAvailableId = MethodIdHelper.GetMethodId("getStorageGasAvailable()");
        _isTopLevelCallId = MethodIdHelper.GetMethodId("isTopLevelCall()");
        _mapL1SenderContractAddressToL2AliasId = MethodIdHelper.GetMethodId("mapL1SenderContractAddressToL2Alias(address,address)");
        _wasMyCallersAddressAliasedId = MethodIdHelper.GetMethodId("wasMyCallersAddressAliased()");
        _myCallersAddressWithoutAliasingId = MethodIdHelper.GetMethodId("myCallersAddressWithoutAliasing()");
        _sendTxToL1Id = MethodIdHelper.GetMethodId("sendTxToL1(uint256,address,bytes)"); // TODO: check that
        _sendMerkleTreeStateId = MethodIdHelper.GetMethodId("sendMerkleTreeState()");
        _withdrawEthId = MethodIdHelper.GetMethodId("withdrawEth(address)"); // TODO: check that
    }

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
        if (result) resultBytes[^1] = 1;

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
        if (result) resultBytes[^1] = 1;

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
        // TODO: check that parameter (not in abi somehow??)
        // UInt256 value = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        // TODO: not sure it's the balance, to check
        UInt256 value = context.WorldState.GetBalance(context.Caller);

        ReadOnlySpan<byte> destinationBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address destination = new(destinationBytes[(Hash256.Size - Address.Size)..]);

        byte[] calldataForL1 = inputData.ToArray();

        UInt256 result = ArbSys.SendTxToL1(context, value, destination, calldataForL1);

        return result.ToBigEndian();
    }

    private static byte[] SendMerkleTreeState(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (UInt256 size, ValueHash256 root, ValueHash256[] partials) = ArbSys.SendMerkleTreeState(context);

        AbiFunctionDescription function = precompileFunctions["sendMerkleTreeState"];

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [size, root, partials]
        );

        return abiEncodedResult;
    }

    private static byte[] WithdrawEth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        // TODO: check that parameter (not in abi somehow??)
        // UInt256 value = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        // TODO: not sure it's the balance, to check
        UInt256 value = context.WorldState.GetBalance(context.Caller);

        ReadOnlySpan<byte> destinationBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address destination = new(destinationBytes[(Hash256.Size - Address.Size)..]);

        UInt256 result = ArbSys.WithdrawEth(context, value, destination);

        return result.ToBigEndian();
    }
}
