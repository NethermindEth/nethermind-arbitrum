using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

// ArbSys provides system-level functionality for interacting with L1 and understanding the call stack.
public static class ArbSys
{
    public static Address Address => ArbosAddresses.ArbSysAddress;

    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"requested\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"current\",\"type\":\"uint256\"}],\"name\":\"InvalidBlockNumber\",\"type\":\"error\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"internalType\":\"address\",\"name\":\"caller\",\"type\":\"address\"},{\"indexed\":true,\"internalType\":\"address\",\"name\":\"destination\",\"type\":\"address\"},{\"indexed\":true,\"internalType\":\"uint256\",\"name\":\"uniqueId\",\"type\":\"uint256\"},{\"indexed\":true,\"internalType\":\"uint256\",\"name\":\"batchNumber\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"indexInBatch\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"arbBlockNum\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"ethBlockNum\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"timestamp\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"callvalue\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"bytes\",\"name\":\"data\",\"type\":\"bytes\"}],\"name\":\"L2ToL1Transaction\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"internalType\":\"address\",\"name\":\"caller\",\"type\":\"address\"},{\"indexed\":true,\"internalType\":\"address\",\"name\":\"destination\",\"type\":\"address\"},{\"indexed\":true,\"internalType\":\"uint256\",\"name\":\"hash\",\"type\":\"uint256\"},{\"indexed\":true,\"internalType\":\"uint256\",\"name\":\"position\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"arbBlockNum\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"ethBlockNum\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"timestamp\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"callvalue\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"bytes\",\"name\":\"data\",\"type\":\"bytes\"}],\"name\":\"L2ToL1Tx\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"uint256\",\"name\":\"reserved\",\"type\":\"uint256\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"hash\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"uint256\",\"name\":\"position\",\"type\":\"uint256\"}],\"name\":\"SendMerkleUpdate\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"arbBlockNum\",\"type\":\"uint256\"}],\"name\":\"arbBlockHash\",\"outputs\":[{\"internalType\":\"bytes32\",\"name\":\"\",\"type\":\"bytes32\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"arbBlockNumber\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"arbChainID\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"arbOSVersion\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getStorageGasAvailable\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"isTopLevelCall\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"sender\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"unused\",\"type\":\"address\"}],\"name\":\"mapL1SenderContractAddressToL2Alias\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"pure\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"myCallersAddressWithoutAliasing\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"sendMerkleTreeState\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"size\",\"type\":\"uint256\"},{\"internalType\":\"bytes32\",\"name\":\"root\",\"type\":\"bytes32\"},{\"internalType\":\"bytes32[]\",\"name\":\"partials\",\"type\":\"bytes32[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"destination\",\"type\":\"address\"},{\"internalType\":\"bytes\",\"name\":\"data\",\"type\":\"bytes\"}],\"name\":\"sendTxToL1\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"payable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"wasMyCallersAddressAliased\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"destination\",\"type\":\"address\"}],\"name\":\"withdrawEth\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"payable\",\"type\":\"function\"}]";

    // Events
    public static readonly AbiEventDescription SendMerkleUpdateEvent;
    public static readonly AbiEventDescription L2ToL1TxEvent;
    // Deprecated in favour of the new L2ToL1Tx event above after the nitro upgrade
    public static readonly AbiEventDescription L2ToL1TransactionEvent;

    // Solidity errors
    public static readonly AbiErrorDescription InvalidBlockNumber;

    private static readonly UInt256 AddressAliasOffset;
    private static readonly UInt256 InverseAddressAliasOffset;

    static ArbSys()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi)!;
        SendMerkleUpdateEvent = allEvents["SendMerkleUpdate"];
        L2ToL1TxEvent = allEvents["L2ToL1Tx"];
        L2ToL1TransactionEvent = allEvents["L2ToL1Transaction"];

        Dictionary<string, AbiErrorDescription> allErrors = AbiMetadata.GetAllErrorDescriptions(Abi)!;
        InvalidBlockNumber = allErrors["InvalidBlockNumber"];

        Address offset = new("0x1111000000000000000000000000000000001111");
        AddressAliasOffset = new(offset.Bytes, isBigEndian: true);

        InverseAddressAliasOffset = (UInt256.One << 160) - AddressAliasOffset;
    }

    public static void EmitSendMerkleUpdateEvent(
        ArbitrumPrecompileExecutionContext context, UInt256 reserved, Hash256 hash, UInt256 position
    )
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(SendMerkleUpdateEvent, Address, reserved, hash, position);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static void EmitL2ToL1txEvent(
        ArbitrumPrecompileExecutionContext context, Address sender, Address destination, UInt256 hash,
        UInt256 position, UInt256 arbBlockNum, UInt256 ethBlockNum,
        UInt256 timestamp, UInt256 callvalue, byte[] data
    )
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(L2ToL1TxEvent, Address, sender, destination, hash, position, arbBlockNum, ethBlockNum, timestamp, callvalue, data);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static PrecompileSolidityError InvalidBlockNumberSolidityError(UInt256 requested, UInt256 current)
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(InvalidBlockNumber.Name, InvalidBlockNumber.Inputs.Select(p => p.Type).ToArray()),
            [requested, current]
        );
        return new PrecompileSolidityError(errorData);
    }

    // ArbBlockNumber gets the current L2 block number
    public static UInt256 ArbBlockNumber(ArbitrumPrecompileExecutionContext context)
        => context.BlockExecutionContext.Number;

    // ArbBlockHash gets the L2 block hash, if sufficiently recent
    public static Hash256 ArbBlockHash(ArbitrumPrecompileExecutionContext context, UInt256 arbBlockNum)
    {
        if (!arbBlockNum.IsUint64)
        {
            if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Eleven)
                throw InvalidBlockNumberSolidityError(arbBlockNum, context.BlockExecutionContext.Number);

            throw new InvalidOperationException($"Invalid block number {arbBlockNum}: not a uint64");
        }

        if (arbBlockNum >= context.BlockExecutionContext.Number ||
            arbBlockNum + 256 < context.BlockExecutionContext.Number)
        {
            if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Eleven)
                throw InvalidBlockNumberSolidityError(arbBlockNum, context.BlockExecutionContext.Number);

            throw new InvalidOperationException($"Invalid block number {arbBlockNum}: not in valid range");
        }

        return context.BlockHashProvider.GetBlockhash(context.BlockExecutionContext.Header, (long)arbBlockNum)
            ?? throw new InvalidOperationException($"Block number {arbBlockNum} not found");
    }

    // ArbChainID gets the rollup's unique chain identifier
    public static UInt256 ArbChainID(ArbitrumPrecompileExecutionContext context) => context.ChainId;

    // ArbOSVersion gets the current ArbOS version
    public static UInt256 ArbOSVersion(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.CurrentArbosVersion + 55; // Nitro starts at version 56

    // GetStorageGasAvailable returns 0 since Nitro has no concept of storage gas
    public static UInt256 GetStorageGasAvailable() => 0;

    // IsTopLevelCall checks if the call is top-level (deprecated)
    public static bool IsTopLevelCall(ArbitrumPrecompileExecutionContext context) => context.CallDepth <= 1;

    // MapL1SenderContractAddressToL2Alias gets the contract's L2 alias
    public static Address MapL1SenderContractAddressToL2Alias(Address sender) => RemapL1Address(sender);

    // WasMyCallersAddressAliased checks if the caller's caller was aliased
    public static bool WasMyCallersAddressAliased(ArbitrumPrecompileExecutionContext context)
    {
        bool topLevel = context.ArbosState.CurrentArbosVersion < ArbosVersion.Six
            ? context.CallDepth == 1 : IsTopLevel(context);

        return topLevel && DoesTxAlias(context.TopLevelTxType);
    }

    // MyCallersAddressWithoutAliasing gets the caller's caller without any potential aliasing
    public static Address MyCallersAddressWithoutAliasing(ArbitrumPrecompileExecutionContext context)
    {
        Address address = context.GrandCaller ?? Address.Zero;

        if (WasMyCallersAddressAliased(context))
            address = InverseRemapL1Address(address);

        return address;
    }

    // SendTxToL1 sends a transaction to L1, adding it to the outbox
    public static UInt256 SendTxToL1(
        ArbitrumPrecompileExecutionContext context,
        Address destination,
        byte[] calldataForL1)
    {
        UInt256 l1BlockNumber = context.FreeArbosState.Blockhashes.GetL1BlockNumber();

        // As of ArbOS 41, the concept of "native token owners" was introduced.
        // Native token owners are accounts that are allowed to mint and burn
        // the chain's native token to and from their own address.
        //
        // Without the "mint" and "burn" functionality, a "bridge" contract on
        // the parent chain (L1) locks up funds equivalent to all the funds on
        // the child chain, so it is always safe to withdraw funds from the
        // child chain to the parent chain.
        //
        // With the "mint" and "burn" functionality, a "bridge" contract on
        // the parent chain can become under collateralized because the native
        // token owners can mint funds on the child chain without putting
        // funds into the bridge contract. So, it is not safe to withdraw funds
        // from the child chain to the parent chain in the normal way.
        if (context.ArbosState.CurrentArbosVersion > ArbosVersion.Forty &&
            context.ArbosState.NativeTokenOwners.Size() > 0)
        {
            throw new InvalidOperationException("Not allowed to withdraw funds when native token owners exist");
        }

        UInt256 blockNumber = new(context.BlockExecutionContext.Number);
        UInt256 timestamp = new(context.BlockExecutionContext.Header.Timestamp);

        Hash256 sendHash = ComputeSendTxHash(
            context,
            destination,
            blockNumber,
            l1BlockNumber,
            timestamp,
            calldataForL1
        );

        IReadOnlyCollection<MerkleTreeNodeEvent> merkleUpdateEvents =
            context.ArbosState.SendMerkleAccumulator.Append((ValueHash256)sendHash);

        ulong size = context.ArbosState.SendMerkleAccumulator.GetSize();

        // burn the callvalue, which was previously deposited to this precompile's account
        ArbitrumTransactionProcessor.BurnBalance(Address, context.Value, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo!);

        foreach (MerkleTreeNodeEvent merkleTreeNodeEvent in merkleUpdateEvents)
        {
            UInt256 position = (new UInt256(merkleTreeNodeEvent.Level) << 192) + merkleTreeNodeEvent.NumLeaves;

            EmitSendMerkleUpdateEvent(
                context,
                0,
                merkleTreeNodeEvent.Hash.ToCommitment(),
                position
            );
        }

        UInt256 leafNum = new(size - 1);
        UInt256 sendHashNumber = new(sendHash.Bytes, isBigEndian: true);

        EmitL2ToL1txEvent(
            context, context.Caller, destination, sendHashNumber, leafNum,
            blockNumber, l1BlockNumber,
            timestamp, context.Value, calldataForL1
        );

        return context.ArbosState.CurrentArbosVersion >= ArbosVersion.Four ? leafNum : sendHashNumber;
    }

    // SendMerkleTreeState gets the root, size, and partials of the outbox Merkle tree state (caller must be the 0 address)
    public static (UInt256, Hash256, Hash256[]) SendMerkleTreeState(ArbitrumPrecompileExecutionContext context)
    {
        if (context.Caller != Address.Zero)
            throw new InvalidOperationException($"Caller must be the 0 address, instead got {context.Caller}");

        // OK to not charge gas, because method is only callable by address zero

        MerkleAccumulatorExportState state = context.ArbosState.SendMerkleAccumulator.GetExportState();

        Hash256[] partials = new Hash256[state.Partials.Count];
        for (int i = 0; i < state.Partials.Count; i++)
        {
            partials[i] = new Hash256(state.Partials[i]);
        }

        return (new UInt256(state.Size), new Hash256(state.Root), partials);
    }

    // WithdrawEth send paid eth to the destination on L1
    public static UInt256 WithdrawEth(ArbitrumPrecompileExecutionContext context, Address destination)
        => SendTxToL1(context, destination, []);

    public record class ArbSysL2ToL1Transaction(
        Address Caller,
        Address Destination,
        UInt256 BatchNumber,
        UInt256 UniqueId,
        UInt256 IndexInBatch,
        UInt256 ArbBlockNum,
        UInt256 EthBlockNum,
        UInt256 Timestamp,
        UInt256 CallValue,
        byte[] Data
    );

    public record class ArbSysL2ToL1Tx(
        Address Caller,
        Address Destination,
        UInt256 Hash,
        UInt256 Position,
        UInt256 ArbBlockNum,
        UInt256 EthBlockNum,
        UInt256 Timestamp,
        UInt256 CallValue,
        byte[] Data
    );

    public static ArbSysL2ToL1Transaction DecodeL2ToL1TransactionEvent(LogEntry logEntry)
    {
        var data = EventsEncoder.DecodeEvent(L2ToL1TransactionEvent, logEntry);

        return new ArbSysL2ToL1Transaction(
            Caller: (Address)data["caller"],
            Destination: (Address)data["destination"],
            BatchNumber: (UInt256)data["batchNumber"],
            UniqueId: (UInt256)data["uniqueId"],
            IndexInBatch: (UInt256)data["indexInBatch"],
            ArbBlockNum: (UInt256)data["arbBlockNum"],
            EthBlockNum: (UInt256)data["ethBlockNum"],
            Timestamp: (UInt256)data["timestamp"],
            CallValue: (UInt256)data["callvalue"],
            Data: (byte[])data["data"]
        );
    }

    public static ArbSysL2ToL1Tx DecodeL2ToL1TxEvent(LogEntry logEntry)
    {
        var data = EventsEncoder.DecodeEvent(L2ToL1TxEvent, logEntry);

        return new ArbSysL2ToL1Tx(
            Caller: (Address)data["caller"],
            Destination: (Address)data["destination"],
            Hash: (UInt256)data["hash"],
            Position: (UInt256)data["position"],
            ArbBlockNum: (UInt256)data["arbBlockNum"],
            EthBlockNum: (UInt256)data["ethBlockNum"],
            Timestamp: (UInt256)data["timestamp"],
            CallValue: (UInt256)data["callvalue"],
            Data: (byte[])data["data"]
        );
    }

    private static Hash256 ComputeSendTxHash(
        ArbitrumPrecompileExecutionContext context,
        Address destination,
        in UInt256 blockNumber,
        in UInt256 l1BlockNumber,
        in UInt256 timestamp,
        byte[] calldataForL1)
    {
        int totalLength = Address.Size * 2 + Hash256.Size * 4 + calldataForL1.Length;

        const int StackAllocThreshold = 512;

        Span<byte> buffer = totalLength <= StackAllocThreshold
            ? stackalloc byte[totalLength]
            : new byte[totalLength];

        int offset = 0;
        context.Caller.Bytes.CopyTo(buffer.Slice(offset, 20));
        offset += 20;
        destination.Bytes.CopyTo(buffer.Slice(offset, 20));
        offset += 20;

        Span<byte> blockNumberBytes = buffer.Slice(offset, 32);
        offset += 32;
        Span<byte> l1BlockNumberBytes = buffer.Slice(offset, 32);
        offset += 32;
        Span<byte> timestampBytes = buffer.Slice(offset, 32);
        offset += 32;
        Span<byte> valueBytes = buffer.Slice(offset, 32);
        offset += 32;

        blockNumber.ToBigEndian(blockNumberBytes);
        l1BlockNumber.ToBigEndian(l1BlockNumberBytes);
        timestamp.ToBigEndian(timestampBytes);
        context.Value.ToBigEndian(valueBytes);

        calldataForL1.CopyTo(buffer.Slice(offset));

        return context.ArbosState.BackingStorage.ComputeKeccakHash(buffer).ToCommitment();
    }

    private static Address RemapL1Address(Address l1Address)
    {
        UInt256 l1AddressAsNumber = new(l1Address.Bytes, isBigEndian: true);
        UInt256 sumBytes = l1AddressAsNumber + AddressAliasOffset;

        return new(sumBytes.ToBigEndian()[12..]);
    }

    private static Address InverseRemapL1Address(Address l2Address)
    {
        UInt256 l2AddressAsNumber = new(l2Address.Bytes, isBigEndian: true);
        UInt256 sumBytes = l2AddressAsNumber + InverseAddressAliasOffset;

        return new(sumBytes.ToBigEndian()[12..]);
    }

    private static bool DoesTxAlias(ArbitrumTxType txType)
        => txType is ArbitrumTxType.ArbitrumUnsigned
            or ArbitrumTxType.ArbitrumContract
            or ArbitrumTxType.ArbitrumRetry;

    private static bool IsTopLevel(ArbitrumPrecompileExecutionContext context)
        => context.CallDepth == 0 || context.Origin == context.GrandCaller?.ToHash();
}
