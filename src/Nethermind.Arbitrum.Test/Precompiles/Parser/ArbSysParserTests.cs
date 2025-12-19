using System.Text;
using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using System.Buffers.Binary;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbSysParserTests
{
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

    [Test]
    public void Instance_WhenAccessed_ReturnsSameSingleton()
    {
        ArbSysParser instance1 = ArbSysParser.Instance;
        ArbSysParser instance2 = ArbSysParser.Instance;
        instance1.Should().BeSameAs(instance2);
    }

    [Test]
    public void Address_WhenQueried_ReturnsArbSysAddress()
    {
        Address parserAddress = ArbSysParser.Address;
        parserAddress.Should().Be(ArbSys.Address);
    }

    [Test]
    public void InvokeSomeMethod_WhenInvalidMethodId_ThrowsArgumentException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] invalidMethodId = [0xFF, 0xFF, 0xFF, 0xFF];

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(BinaryPrimitives.ReadUInt32BigEndian(invalidMethodId), out PrecompileHandler? implementation);
        exists.Should().BeFalse();
    }

    [TestCase(1L)]
    [TestCase(100L)]
    [TestCase(1000L)]
    [TestCase(10000L)]
    [TestCase(12345L)]
    [TestCase(100000L)]
    public void ArbBlockNumber_WhenDifferentBlockNumbers_ReturnsCorrectSerialization(long blockNumber)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        genesisBlock.Header.Number = blockNumber;

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_arbBlockNumberId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = ((UInt256)blockNumber).ToBigEndian();
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void ArbBlockHash_WhenMissingParameter_ThrowsRevertException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_arbBlockHashId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ArbChainID_WhenCalled_ReturnsSerializedChainId()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_arbChainIdId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = new byte[32];
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void ArbOSVersion_WhenCalled_ReturnsSerializedVersionPlus55()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        ulong currentVersion = context.ArbosState.CurrentArbosVersion;

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_arbOSVersionId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = ((UInt256)currentVersion + 55).ToBigEndian();
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void GetStorageGasAvailable_WhenCalled_ReturnsSerializedZero()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_getStorageGasAvailableId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = new byte[32];
        result.Should().BeEquivalentTo(expected);
    }

    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(10, false)]
    public void IsTopLevelCall_WhenDifferentCallDepths_ReturnsCorrectSerialization(int callDepth, bool expectedResult)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue)
        {
            CallDepth = callDepth
        };
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_isTopLevelCallId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = new byte[32];
        expected[31] = (byte)(expectedResult ? 1 : 0);
        result.Should().BeEquivalentTo(expected);
    }

    [TestCase("0x0000000000000000000000000000000000000000", "0x1111000000000000000000000000000000001111")]
    [TestCase("0x0000000000000000000000000000000000000001", "0x1111000000000000000000000000000000001112")]
    [TestCase("0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb7", "0x853e35cc6634c0532925a3b844bc9e7595f0cfc8")]
    [TestCase("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE", "0x111100000000000000000000000000000000110f")]
    [TestCase("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "0x1111000000000000000000000000000000001110")]
    public void MapL1SenderContractAddressToL2Alias_WhenValidAddress_ReturnsSerializedAlias(string senderHex, string expectedAliasHex)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] inputData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbSysParser.PrecompileFunctionDescription[_mapL1SenderContractAddressToL2AliasId].AbiFunctionDescription.GetCallInfo().Signature,
            [new Address(senderHex), Address.Zero]  // 2nd address is needed by ABI even if unused in precompile
        );

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_mapL1SenderContractAddressToL2AliasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        byte[] result = implementation!(context, inputData);

        result.Should().HaveCount(32);
        result[..12].Should().BeEquivalentTo(new byte[12], o => o.WithStrictOrdering());
        Address resultAddress = new(result[12..32]);
        resultAddress.Should().Be(new Address(expectedAliasHex));
    }

    [Test]
    public void WasMyCallersAddressAliased_TxTypeNotAliasable_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_wasMyCallersAddressAliasedId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = new byte[32];
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void WasMyCallersAddressAliased_WasAliased_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue)
        {
            TopLevelTxType = ArbitrumTxType.ArbitrumUnsigned,
            CallDepth = 0
        };
        context.WithArbosState();
        // Ensure we're at top level (CallDepth should be 0 for IsTopLevel to return true in ArbOS >= 6)

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_wasMyCallersAddressAliasedId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = new byte[32];
        expected[31] = 1;  // Should return true when aliased
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void MyCallersAddressWithoutAliasing_CallDepthIsZero_ReturnsZeroAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue)
        {
            CallDepth = 0
        };
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_myCallersAddressWithoutAliasingId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expected = new byte[32];
        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void SendTxToL1_WhenMissingParameters_ThrowsRevertException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_sendTxToL1Id, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void WithdrawEth_WhenMissingParameter_ThrowsRevertException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_withdrawEthId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void WithdrawEth_WhenCallDataIsLargerThanExpected_DoesNotFail()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_withdrawEthId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        //according to ABI, withdrawEth takes one address parameter (32 bytes), but we provide more data to test that it is ignored and method still executed
        byte[] callData = new byte[60];
        TestItem.AddressA.Bytes.CopyTo(callData);

        //no exception
        _ = implementation!(context, callData);
    }

    [Test]
    public void SendMerkleTreeState_InvalidInputData_ReturnsSerializedState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbSysParser.PrecompileImplementation.TryGetValue(_sendMerkleTreeStateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    private static class ArbSysMethodIds
    {
        private static readonly Dictionary<string, uint> _methodIds = new();

        static ArbSysMethodIds()
        {
            _methodIds["arbBlockNumber"] = GetMethodId("arbBlockNumber()");
            _methodIds["arbBlockHash"] = GetMethodId("arbBlockHash(uint256)");
            _methodIds["arbChainID"] = GetMethodId("arbChainID()");
            _methodIds["arbOSVersion"] = GetMethodId("arbOSVersion()");
            _methodIds["getStorageGasAvailable"] = GetMethodId("getStorageGasAvailable()");
            _methodIds["isTopLevelCall"] = GetMethodId("isTopLevelCall()");
            _methodIds["mapL1SenderContractAddressToL2Alias"] = GetMethodId("mapL1SenderContractAddressToL2Alias(address,address)");
            _methodIds["wasMyCallersAddressAliased"] = GetMethodId("wasMyCallersAddressAliased()");
            _methodIds["myCallersAddressWithoutAliasing"] = GetMethodId("myCallersAddressWithoutAliasing()");
            _methodIds["sendTxToL1"] = GetMethodId("sendTxToL1(address,bytes)");
            _methodIds["sendMerkleTreeState"] = GetMethodId("sendMerkleTreeState()");
            _methodIds["withdrawEth"] = GetMethodId("withdrawEth(address)");
        }

        private static uint GetMethodId(string methodSignature)
        {
            Hash256 hash = Keccak.Compute(Encoding.UTF8.GetBytes(methodSignature));
            byte[] hashBytes = hash.Bytes.ToArray();
            byte[] first4Bytes = hashBytes[0..4];
            return (uint)((first4Bytes[0] << 24) | (first4Bytes[1] << 16) | (first4Bytes[2] << 8) | first4Bytes[3]);
        }
    }
}
