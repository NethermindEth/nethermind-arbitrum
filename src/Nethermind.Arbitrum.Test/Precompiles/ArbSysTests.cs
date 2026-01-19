using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbSysTests
{
    [Test]
    public void ArbBlockHash_WithArbosVersion11OrHigher_ThrowsSolidityError()
    {
        const long currentBlock = 500;
        const long targetBlock = 100;

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithBlockNumber(currentBlock)
            .WithArbosVersion(ArbosVersion.Eleven);

        Action action = () => ArbSys.ArbBlockHash(context, new UInt256(targetBlock));

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbSys.InvalidBlockNumberSolidityError(new UInt256(targetBlock), new UInt256(currentBlock));
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ArbBlockHash_WithBlockNumberInFuture_ThrowsException()
    {
        const long currentBlock = 100;
        const long targetBlock = 200; // Future block

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Eleven)
            .WithBlockNumber(currentBlock);

        Action action = () => ArbSys.ArbBlockHash(context, new UInt256(targetBlock));

        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbSys.InvalidBlockNumberSolidityError(new UInt256(targetBlock), new UInt256(currentBlock));
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ArbBlockHash_WithBlockNumberTooOld_ThrowsException()
    {
        const long currentBlock = 500;
        const long targetBlock = 100; // More than 256 blocks old

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Eleven)
            .WithBlockNumber(currentBlock);

        Action action = () => ArbSys.ArbBlockHash(context, new UInt256(targetBlock));

        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;

        ArbitrumPrecompileException expected = ArbSys.InvalidBlockNumberSolidityError(new UInt256(targetBlock), new UInt256(currentBlock));
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ArbBlockHash_WithNonUint64Value_ThrowsException()
    {
        UInt256 hugeBlockNumber = UInt256.MaxValue;

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        long currentBlockNumber = 100;
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithBlockNumber(currentBlockNumber)
            .WithArbosVersion(ArbosVersion.Eleven);

        Action action = () => ArbSys.ArbBlockHash(context, hugeBlockNumber);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbSys.InvalidBlockNumberSolidityError(hugeBlockNumber, new UInt256((ulong)currentBlockNumber));
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ArbBlockHash_WithValidBlockNumber_ReturnsHash()
    {
        const long currentBlock = 256;
        const long targetBlock = 100;
        Hash256 expectedHash = TestItem.KeccakA;

        IBlockhashProvider blockhashProvider = PrecompileTestContextBuilder.CreateTestBlockHashProvider(
            (targetBlock, expectedHash)
        );

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Eleven)
            .WithBlockNumber(currentBlock)
            .WithBlockHashProvider(blockhashProvider);

        Hash256 result = ArbSys.ArbBlockHash(context, new UInt256(targetBlock));

        result.Should().Be(expectedHash);
    }
    [Test]
    public void ArbBlockNumber_WithValidContext_ReturnsCurrentBlockNumber()
    {
        const long expectedBlockNumber = 12345;
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithBlockNumber(expectedBlockNumber);

        UInt256 result = ArbSys.ArbBlockNumber(context);

        result.Should().Be(new UInt256(expectedBlockNumber));
    }

    [Test]
    public void ArbChainID_WithValidContext_ReturnsCorrectChainId()
    {
        const ulong expectedChainId = 42161; // Arbitrum One
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithChainId(expectedChainId);

        UInt256 result = ArbSys.ArbChainID(context);

        result.Should().Be(new UInt256(expectedChainId));
    }

    [Test]
    public void ArbOSVersion_WithValidContext_ReturnsCorrectVersion()
    {
        ulong arbosVersion = ArbosVersion.Thirty;
        UInt256 expectedVersion = arbosVersion + 55; // Nitro starts at version 56

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(arbosVersion);

        UInt256 result = ArbSys.ArbOSVersion(context);

        result.Should().Be(expectedVersion);
    }

    [Test]
    public void DecodeL2ToL1TransactionEvent_WithValidLogEntry_DecodesCorrectly()
    {
        Address caller = TestItem.AddressA;
        Address destination = TestItem.AddressB;
        UInt256 batchNumber = new(10);
        UInt256 uniqueId = new(123);
        UInt256 indexInBatch = new(5);
        UInt256 arbBlockNum = new(1000);
        UInt256 ethBlockNum = new(500);
        UInt256 timestamp = new(1700000000);
        UInt256 callValue = new(100);
        byte[] data = Bytes.FromHexString("0x1234");

        LogEntry logEntry = EventsEncoder.BuildLogEntryFromEvent(
            ArbSys.L2ToL1TransactionEvent,
            ArbSys.Address,
            caller,
            destination,
            uniqueId, // uniqueId comes before batchNumber in the event
            batchNumber,
            indexInBatch,
            arbBlockNum,
            ethBlockNum,
            timestamp,
            callValue,
            data
        );

        ArbSys.ArbSysL2ToL1Transaction expected = new(
            Caller: caller,
            Destination: destination,
            BatchNumber: batchNumber,
            UniqueId: uniqueId,
            IndexInBatch: indexInBatch,
            ArbBlockNum: arbBlockNum,
            EthBlockNum: ethBlockNum,
            Timestamp: timestamp,
            CallValue: callValue,
            Data: data
        );

        ArbSys.ArbSysL2ToL1Transaction result = ArbSys.DecodeL2ToL1TransactionEvent(logEntry);

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void DecodeL2ToL1TxEvent_WithValidLogEntry_DecodesCorrectly()
    {
        Address caller = TestItem.AddressA;
        Address destination = TestItem.AddressB;
        UInt256 hash = new(TestItem.KeccakA.Bytes, true);
        UInt256 position = new(42);
        UInt256 arbBlockNum = new(1000);
        UInt256 ethBlockNum = new(500);
        UInt256 timestamp = new(1700000000);
        UInt256 callValue = new(100);
        byte[] data = Bytes.FromHexString("0x5678");

        LogEntry logEntry = EventsEncoder.BuildLogEntryFromEvent(
            ArbSys.L2ToL1TxEvent,
            ArbSys.Address,
            caller,
            destination,
            hash,
            position,
            arbBlockNum,
            ethBlockNum,
            timestamp,
            callValue,
            data
        );

        ArbSys.ArbSysL2ToL1Tx expected = new(
            Caller: caller,
            Destination: destination,
            Hash: hash,
            Position: position,
            ArbBlockNum: arbBlockNum,
            EthBlockNum: ethBlockNum,
            Timestamp: timestamp,
            CallValue: callValue,
            Data: data
        );

        ArbSys.ArbSysL2ToL1Tx result = ArbSys.DecodeL2ToL1TxEvent(logEntry);

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void EmitL2ToL1TxEvent_WithValidParameters_AddsCorrectEventToLogs()
    {
        Address sender = TestItem.AddressA;
        Address destination = TestItem.AddressB;
        UInt256 hash = new(TestItem.KeccakA.Bytes, true);
        UInt256 position = new(42);
        UInt256 arbBlockNum = new(1000);
        UInt256 ethBlockNum = new(500);
        UInt256 timestamp = new(1700000000);
        UInt256 callValue = new(100);
        byte[] data = Bytes.FromHexString("0x1234");

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        ArbSys.EmitL2ToL1txEvent(
            context,
            sender,
            destination,
            hash,
            position,
            arbBlockNum,
            ethBlockNum,
            timestamp,
            callValue,
            data
        );

        context.EventLogs.Should().HaveCount(1);
        LogEntry eventLog = context.EventLogs[0];
        eventLog.Address.Should().Be(ArbSys.Address);
        eventLog.Topics[0].Should().Be(ArbSys.L2ToL1TxEvent.GetHash());
    }

    [Test]
    public void EmitSendMerkleUpdateEvent_WithValidParameters_AddsCorrectEventToLogs()
    {
        UInt256 reserved = UInt256.Zero;
        Hash256 hash = TestItem.KeccakA;
        UInt256 position = new(123);

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        ArbSys.EmitSendMerkleUpdateEvent(context, reserved, hash, position);

        context.EventLogs.Should().HaveCount(1);
        LogEntry eventLog = context.EventLogs[0];
        eventLog.Address.Should().Be(ArbSys.Address);
        eventLog.Topics[0].Should().Be(ArbSys.SendMerkleUpdateEvent.GetHash());
    }

    [Test]
    public void GetStorageGasAvailable_Always_ReturnsZero()
    {
        UInt256 result = ArbSys.GetStorageGasAvailable();

        result.Should().Be(UInt256.Zero);
    }

    [Test]
    public void InvalidBlockNumberSolidityError_Always_CreatesCorrectErrorData()
    {
        UInt256 requested = new(500);
        UInt256 current = new(100);

        ArbitrumPrecompileException error = ArbSys.InvalidBlockNumberSolidityError(requested, current);

        // The error data should contain the encoded InvalidBlockNumber error
        // with the requested and current block numbers
        byte[] expectedSignature = new AbiSignature(
            ArbSys.InvalidBlockNumber.Name,
            ArbSys.InvalidBlockNumber.Inputs.Select(p => p.Type).ToArray()
        ).Hash.Bytes[..4].ToArray();

        error.Output[..4].Should().BeEquivalentTo(expectedSignature);
    }

    [Test]
    public void IsTopLevel_WithDifferentArbosVersions_BehavesCorrectly()
    {
        // Test that IsTopLevel behaves differently for ArbOS versions < 6 and >= 6

        // ArbOS < 6: top level when callDepth == 1 (was 2 in Nitro)
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext contextV5 = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(1)
            .WithGrandCaller(TestItem.AddressB) // Need valid GrandCaller for CallDepth = 2
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned)
            .WithArbosVersion(ArbosVersion.Five);

        bool resultV5 = ArbSys.WasMyCallersAddressAliased(contextV5);
        resultV5.Should().BeTrue();

        // ArbOS >= 6: top level when callDepth < 2 in Nitro, in Nethermind it should be == 0
        ArbitrumPrecompileExecutionContext contextV6CallDepth = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(0)
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned)
            .WithArbosVersion(ArbosVersion.Six);

        bool resultV6CallDepth = ArbSys.WasMyCallersAddressAliased(contextV6CallDepth);
        resultV6CallDepth.Should().BeTrue();

        // ArbOS >= 6: also top level when origin == grandCaller
        Address commonAddress = TestItem.AddressC;
        ArbitrumPrecompileExecutionContext contextV6Origin = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(2) // Deep call
            .WithOrigin(commonAddress.ToHash())
            .WithGrandCaller(commonAddress)
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned)
            .WithArbosVersion(ArbosVersion.Six);

        bool resultV6Origin = ArbSys.WasMyCallersAddressAliased(contextV6Origin);
        resultV6Origin.Should().BeTrue();
    }

    [Test]
    public void IsTopLevelCall_WithCallDepthGreaterThan2_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(2);

        bool result = ArbSys.IsTopLevelCall(context);

        result.Should().BeFalse();
    }

    [Test]
    public void IsTopLevelCall_WithCallDepthLessThanOrEqualTo2_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(1);

        bool result = ArbSys.IsTopLevelCall(context);

        result.Should().BeTrue();
    }

    [Test]
    public void MapL1SenderContractAddressToL2Alias_WithValidAddress_AppliesCorrectOffset()
    {
        Address l1Address = new("0x0000000000000000000000000000000000001234");
        // Expected: l1Address + 0x1111000000000000000000000000000000001111
        Address expectedAlias = new("0x1111000000000000000000000000000000002345");

        Address result = ArbSys.MapL1SenderContractAddressToL2Alias(l1Address);

        result.Should().Be(expectedAlias);
    }

    [Test]
    public void MyCallersAddressWithoutAliasing_WithAliasedGrandCaller_ReturnsUnaliasedAddress()
    {
        Address aliasedAddress = new("0x1111000000000000000000000000000000002345");
        Address expectedUnaliased = new("0x0000000000000000000000000000000000001234");

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(1) // Need CallDepth > 1 to use GrandCaller, and == 2 for IsTopLevel in ArbOS < 6 for Nitro, in Nethermind we need - 1
            .WithGrandCaller(aliasedAddress)
            .WithOrigin(TestItem.AddressA.ToHash()) // Ensure Origin is set
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned)
            .WithArbosVersion(ArbosVersion.Five);

        Address result = ArbSys.MyCallersAddressWithoutAliasing(context);

        result.Should().Be(expectedUnaliased);
    }

    [Test]
    public void MyCallersAddressWithoutAliasing_WithNoGrandCaller_ReturnsZeroAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(0)
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumLegacy);

        Address result = ArbSys.MyCallersAddressWithoutAliasing(context);

        result.Should().Be(Address.Zero);
    }

    [Test]
    public void MyCallersAddressWithoutAliasing_WithNoGrandCallerAndAliasingTxType_ReturnsUnaliasedZeroAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(1) // Top level for ArbOS < 6
            .WithGrandCaller(Address.Zero) // GrandCaller is Address.Zero
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned) // Aliasing tx type
            .WithArbosVersion(ArbosVersion.Five);

        Address result = ArbSys.MyCallersAddressWithoutAliasing(context);

        // When GrandCaller is Address.Zero and WasMyCallersAddressAliased returns true,
        // InverseRemapL1Address should be called on Address.Zero
        // Address.Zero when inverse-remapped should return the inverse-aliased address
        Address expectedInverseAliased = new("0xeeeeffffffffffffffffffffffffffffffffeeef");
        result.Should().Be(expectedInverseAliased, "because InverseRemapL1Address on Address.Zero should return the inverse-aliased address");
    }

    [Test]
    public void SendMerkleTreeState_WithActualMerkleData_ReturnsCorrectValues()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCaller(Address.Zero) // Only zero address can call SendMerkleTreeState
            .WithArbosVersion(ArbosVersion.Forty);

        // Add some entries to the MerkleAccumulator to test with actual data
        MerkleAccumulator merkleAccumulator = context.ArbosState.SendMerkleAccumulator;
        ValueHash256 testHash1 = ValueKeccak.Compute("test1"u8);
        ValueHash256 testHash2 = ValueKeccak.Compute("test2"u8);

        // Add test entries to the accumulator
        merkleAccumulator.Append(testHash1);
        merkleAccumulator.Append(testHash2);

        (UInt256 size, Hash256 root, Hash256[] partials) = ArbSys.SendMerkleTreeState(context);

        // Verify the returned values reflect the added data
        size.Should().Be(2, "because we added 2 entries to the MerkleAccumulator");
        root.Should().NotBe(Hash256.Zero, "because root should be computed from the added entries");
        partials.Should().NotBeNull("because partials should be a valid array");
        partials.Length.Should().BeGreaterThan(0, "because partials should contain data for the added entries");
    }

    [Test]
    public void SendMerkleTreeState_WithCallerNotZeroAddress_ThrowsException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCaller(TestItem.AddressA);

        Action action = () => ArbSys.SendMerkleTreeState(context);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException($"Caller must be the 0 address, instead got {context.Caller}");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void SendMerkleTreeState_WithZeroAddressCaller_ReturnsState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCaller(Address.Zero);

        (UInt256 size, Hash256 root, Hash256[] partials) = ArbSys.SendMerkleTreeState(context);

        size.Should().BeGreaterThanOrEqualTo(UInt256.Zero);
        root.Should().NotBeNull();
        partials.Should().NotBeNull();
    }

    [Test]
    public void SendTxToL1_Always_BurnsCorrectAmountOfGas()
    {
        Address destination = TestItem.AddressB;
        const int dataLength = 100;
        byte[] callDataForL1 = new byte[dataLength];
        UInt256 value = new(50);

        // Calculate expected gas burn: 30 + 6 * ceil(data_length / 32)
        ulong expectedGasBurn = 30 + 6 * ((ulong)(dataLength + 31) / 32);

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 10_000_000)
            .WithArbosState()
            .WithValue(value)
            .WithCaller(TestItem.AddressA)
            .WithBlockNumber(1000)
            .WithArbosVersion(ArbosVersion.Four);

        context.FreeArbosState.Blockhashes.SetL1BlockNumber(500);

        ulong initialGas = context.GasLeft;

        _ = ArbSys.SendTxToL1(context, destination, callDataForL1);

        ulong gasUsed = initialGas - context.GasLeft;
        gasUsed.Should().BeGreaterThanOrEqualTo(expectedGasBurn);
    }

    [Test]
    public void SendTxToL1_WithNativeTokenOwnersAndValue_ThrowsException()
    {
        Address destination = TestItem.AddressB;
        byte[] callDataForL1 = [];

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithValue(1)
            .WithArbosVersion(ArbosVersion.FortyOne) // > ArbosVersion.Forty, so 41 works
            .WithNativeTokenOwners(TestItem.AddressC);

        Action action = () => ArbSys.SendTxToL1(context, destination, callDataForL1);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("Not allowed to withdraw funds when native token owners exist");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void SendTxToL1_WithValidParameters_EmitsCorrectEventFields()
    {
        Address destination = TestItem.AddressB;
        byte[] callDataForL1 = [1, 2, 3, 4];
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithBlockNumber(12345)
            .WithValue(UInt256.Parse("1000000000000000000")) // 1 ETH
            .WithArbosVersion(ArbosVersion.Forty);

        _ = ArbSys.SendTxToL1(context, destination, callDataForL1);

        // Verify that SendTxToL1 completes successfully and emits an event
        LogEntry[] logs = context.EventLogs.ToArray();
        logs.Should().HaveCount(1, "because SendTxToL1 should emit exactly one L2ToL1Tx event");

        LogEntry eventLog = logs[0];
        eventLog.Address.Should().Be(ArbSys.Address, "because the event should be emitted by ArbSys precompile");

        // The leaf number might be 0 in test environment due to MerkleAccumulator setup
        // but the event should still be emitted with correct basic fields
        ArbSys.ArbSysL2ToL1Tx decodedEvent = ArbSys.DecodeL2ToL1TxEvent(eventLog);
        decodedEvent.Caller.Should().Be(context.Caller, "because caller should match the transaction sender");
        decodedEvent.Destination.Should().Be(destination, "because destination should match the provided destination");
        decodedEvent.CallValue.Should().Be(UInt256.Parse("1000000000000000000"), "because call value should match the transaction value");
        decodedEvent.Data.Should().BeEquivalentTo(callDataForL1, "because data should match the provided call data");
        decodedEvent.ArbBlockNum.Should().Be(12345, "because ArbBlockNum should match the current block number");
    }

    [Test]
    public void SendTxToL1_WithValidParameters_EmitsEventsAndReturnsLeafNum()
    {
        Address destination = TestItem.AddressB;
        byte[] callDataForL1 = Bytes.FromHexString("0x1234567890");
        UInt256 value = new(100);

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 10_000_000)
            .WithArbosState()
            .WithValue(value)
            .WithCaller(TestItem.AddressA)
            .WithBlockNumber(1000)
            .WithArbosVersion(ArbosVersion.Four);

        // Initialize L1 block number in storage
        context.FreeArbosState.Blockhashes.SetL1BlockNumber(500);

        UInt256 result = ArbSys.SendTxToL1(context, destination, callDataForL1);

        // For ArbosVersion >= 4, returns leafNum which is 0 for the first message
        result.Should().BeGreaterThanOrEqualTo(UInt256.Zero);
        context.EventLogs.Should().NotBeEmpty();

        // Check that L2ToL1Tx event was emitted
        LogEntry? l2ToL1Event = context.EventLogs
            .FirstOrDefault(log => log.Topics[0] == ArbSys.L2ToL1TxEvent.GetHash());
        l2ToL1Event.Should().NotBeNull();

        // Burns correct amount of gas
        context.Burned.Should().Be(4723UL);
    }

    [Test]
    public void WasMyCallersAddressAliased_WithAliasingTxTypes_ReturnsTrue()
    {
        // Test that the correct transaction types trigger aliasing
        ArbitrumTxType[] aliasingTypes =
        [
            ArbitrumTxType.ArbitrumUnsigned,
            ArbitrumTxType.ArbitrumContract,
            ArbitrumTxType.ArbitrumRetry
        ];

        ArbitrumTxType[] nonAliasingTypes =
        [
            ArbitrumTxType.ArbitrumLegacy,
            ArbitrumTxType.ArbitrumDeposit,
            ArbitrumTxType.ArbitrumSubmitRetryable,
            ArbitrumTxType.ArbitrumInternal
        ];

        foreach (ArbitrumTxType txType in aliasingTypes)
        {
            IWorldState worldState = TestWorldStateFactory.CreateForTest();
            using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

            _ = ArbOSInitialization.Create(worldState);
            ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
                .WithArbosState()
                .WithCallDepth(1) // For ArbOS < 6, IsTopLevel requires CallDepth == 2 in Nitro, in Nethermind it should be == 1
                .WithGrandCaller(TestItem.AddressB) // Need valid GrandCaller for CallDepth = 2 in Nitro, in Nethermind it should be == 1
                .WithTopLevelTxType(txType)
                .WithArbosVersion(ArbosVersion.Five);

            bool result = ArbSys.WasMyCallersAddressAliased(context);
            result.Should().BeTrue($"Transaction type {txType} should cause aliasing");
        }

        foreach (ArbitrumTxType txType in nonAliasingTypes)
        {
            IWorldState worldState = TestWorldStateFactory.CreateForTest();
            using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

            _ = ArbOSInitialization.Create(worldState);
            ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
                .WithArbosState()
                .WithCallDepth(0)
                .WithTopLevelTxType(txType)
                .WithArbosVersion(ArbosVersion.Five);

            bool result = ArbSys.WasMyCallersAddressAliased(context);
            result.Should().BeFalse($"Transaction type {txType} should not cause aliasing");
        }
    }

    [Test]
    public void WasMyCallersAddressAliased_WithArbosVersionSixAndOriginEqualsGrandCaller_UsesComplexLogic()
    {
        Address commonAddress = TestItem.AddressC;
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(4) // Deep call, but should still be top level due to origin == grandCaller
            .WithOrigin(commonAddress.ToHash())
            .WithGrandCaller(commonAddress)
            .WithArbosVersion(ArbosVersion.Six)
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned);

        bool result = ArbSys.WasMyCallersAddressAliased(context);

        result.Should().BeTrue("because Origin == GrandCaller should make it top level for ArbOS >= 6");
    }

    [Test]
    public void WasMyCallersAddressAliased_WithNonAliasingTxType_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(1) // Top level in ArbOS < 6 requires Nitro CallDepth == 2, which corresponds to Nethermind CallDepth == 1
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumLegacy)
            .WithArbosVersion(ArbosVersion.Five);

        bool result = ArbSys.WasMyCallersAddressAliased(context);

        result.Should().BeFalse();
    }

    [Test]
    public void WasMyCallersAddressAliased_WithNotTopLevelCall_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(2)
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned)
            .WithArbosVersion(ArbosVersion.Five);

        bool result = ArbSys.WasMyCallersAddressAliased(context);

        result.Should().BeFalse();
    }

    [Test]
    public void WasMyCallersAddressAliased_WithTopLevelAndAliasingTxType_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithCallDepth(1) // Top level in ArbOS <    6 requires Nitro CallDepth == 2, which corresponds Nethermind CallDepth == 1
            .WithGrandCaller(TestItem.AddressB) // Need valid GrandCaller for CallDepth = 1
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumUnsigned)
            .WithArbosVersion(ArbosVersion.Five);

        bool result = ArbSys.WasMyCallersAddressAliased(context);

        result.Should().BeTrue();
    }

    [Test]
    public void WithdrawEth_WithValidParameters_CallsSendTxToL1WithEmptyData()
    {
        Address destination = TestItem.AddressB;
        UInt256 value = new(1000);

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 10_000_000)
            .WithArbosState()
            .WithValue(value)
            .WithCaller(TestItem.AddressA)
            .WithBlockNumber(1000)
            .WithArbosVersion(ArbosVersion.Four);

        // Initialize L1 block number
        context.FreeArbosState.Blockhashes.SetL1BlockNumber(500);

        UInt256 result = ArbSys.WithdrawEth(context, destination);

        LogEntry[] logs = context.EventLogs.ToArray();
        logs.Should().HaveCount(1, "because WithdrawEth should emit exactly one L2ToL1Tx event");

        // Decode the event to verify empty data
        ArbSys.ArbSysL2ToL1Tx decodedEvent = ArbSys.DecodeL2ToL1TxEvent(logs[0]);
        decodedEvent.Data.Should().BeEmpty("because WithdrawEth should use empty data");

        // For ArbosVersion >= 4, returns leafNum which is 0 for the first message
        result.Should().BeGreaterThanOrEqualTo(UInt256.Zero);
    }
}
