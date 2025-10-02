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

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbSysParserTests
{
    [Test]
    public void Instance_WhenAccessed_ReturnsSameSingleton()
    {
        var instance1 = ArbSysParser.Instance;
        var instance2 = ArbSysParser.Instance;
        instance1.Should().BeSameAs(instance2);
    }

    [Test]
    public void Address_WhenQueried_ReturnsArbSysAddress()
    {
        var parserAddress = ArbSysParser.Address;
        parserAddress.Should().Be(ArbSys.Address);
    }

    [Test]
    public void RunAdvanced_WhenInvalidMethodId_ThrowsArgumentException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] invalidMethodId = [0xFF, 0xFF, 0xFF, 0xFF];
        ArbSysParser arbSysParser = new();

        Action act = () => arbSysParser.RunAdvanced(context, invalidMethodId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid precompile method ID: 4294967295 for ArbSys precompile");
    }

    [Test]
    public void RunAdvanced_WhenInsufficientInput_ThrowsEndOfStreamException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] insufficientData = new byte[] { 0x01, 0x02, 0x03 };
        ArbSysParser arbSysParser = new();

        Action act = () => arbSysParser.RunAdvanced(context, insufficientData);
        act.Should().Throw<EndOfStreamException>()
            .WithMessage("Attempted to read past the end of the stream.");
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

        byte[] inputData = ArbSysMethodIds.GetInputData("arbBlockNumber");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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

        byte[] inputData = ArbSysMethodIds.GetInputData("arbBlockHash");

        ArbSysParser arbSysParser = new();
        Action act = () => arbSysParser.RunAdvanced(context, inputData);

        act.Should().Throw<RevertException>();
    }

    [Test]
    public void ArbChainID_WhenCalled_ReturnsSerializedChainId()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] inputData = ArbSysMethodIds.GetInputData("arbChainID");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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
        byte[] inputData = ArbSysMethodIds.GetInputData("arbOSVersion");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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

        byte[] inputData = ArbSysMethodIds.GetInputData("getStorageGasAvailable");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

        byte[] expected = new byte[32];
        result.Should().BeEquivalentTo(expected);
    }

    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
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

        byte[] inputData = ArbSysMethodIds.GetInputData("isTopLevelCall");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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

        uint mapL1SenderContractAddressToL2AliasMethodId = PrecompileHelper.GetMethodId("mapL1SenderContractAddressToL2Alias(address,address)");

        byte[] inputData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbSysParser.PrecompileFunctions[mapL1SenderContractAddressToL2AliasMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            [new Address(senderHex), Address.Zero]  // 2nd address is needed by ABI even if unused in precompile
        );

        Address expectedAlias = new(expectedAliasHex);

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

        result.Should().HaveCount(32);
        result[..12].Should().BeEquivalentTo(new byte[12], o => o.WithStrictOrdering());
        Address resultAddress = new(result[12..32]);
        resultAddress.Should().Be(expectedAlias);
    }

    [Test]
    public void WasMyCallersAddressAliased_TxTypeNotAliasable_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] inputData = ArbSysMethodIds.GetInputData("wasMyCallersAddressAliased");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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
            CallDepth = 1
        };
        context.WithArbosState();
        // Ensure we're at top level (CallDepth should be 0 or 1 for IsTopLevel to return true)

        byte[] inputData = ArbSysMethodIds.GetInputData("wasMyCallersAddressAliased");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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

        byte[] inputData = ArbSysMethodIds.GetInputData("myCallersAddressWithoutAliasing");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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

        byte[] inputData = ArbSysMethodIds.GetInputData("sendTxToL1");

        ArbSysParser arbSysParser = new();
        Action act = () => arbSysParser.RunAdvanced(context, inputData);
        act.Should().Throw<RevertException>();
    }

    [Test]
    public void WithdrawEth_WhenMissingParameter_ThrowsRevertException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] inputData = ArbSysMethodIds.GetInputData("withdrawEth");

        ArbSysParser arbSysParser = new();
        Action act = () => arbSysParser.RunAdvanced(context, inputData);

        act.Should().Throw<RevertException>();
    }

    [Test]
    public void SendMerkleTreeState_InvalidInputData_ReturnsSerializedState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();

        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        byte[] inputData = ArbSysMethodIds.GetInputData("sendMerkleTreeState");

        ArbSysParser arbSysParser = new();
        byte[] result = arbSysParser.RunAdvanced(context, inputData);

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

        private static byte[] GetMethodIdBytes(string methodName)
        {
            uint methodId = _methodIds[methodName];
            byte[] methodIdBytes = BitConverter.GetBytes(methodId);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(methodIdBytes);
            }
            return methodIdBytes;
        }

        public static byte[] GetInputData(string methodName, byte[] parameters = null)
        {
            byte[] methodIdBytes = GetMethodIdBytes(methodName);

            if (parameters == null || parameters.Length == 0)
                return methodIdBytes;

            byte[] inputData = new byte[methodIdBytes.Length + parameters.Length];
            methodIdBytes.CopyTo(inputData, 0);
            parameters.CopyTo(inputData, methodIdBytes.Length);
            return inputData;
        }
    }
}
