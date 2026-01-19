using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Arbos;

public partial class ArbosStorageTests
{
    private static readonly Hash256 MappedCellHashOne = new Hash256("0x0bd839f4461b871f3a9c86a40a5fdd92fd303f2683640e55dfb3105603a46223");
    private static readonly Address TestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    [TestCase(31, 36ul)] // burns 30 + 6 * 1
    [TestCase(33, 42ul)] // burns 30 + 6 * 2
    [TestCase(65, 48ul)] // burns 30 + 6 * 3
    public void CalculateHash_Always_BurnsProperCostAndReturnsHash(int bytesLength, ulong burnedCost)
    {
        SystemBurner systemBurner = new();
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        ReadOnlySpan<byte> data = RandomNumberGenerator.GetBytes(bytesLength);
        ValueHash256 expected = Keccak.Compute(data);

        ValueHash256 actual = storage.ComputeKeccakHash(data);

        systemBurner.Burned.Should().Be(burnedCost);
        actual.Should().Be(expected);
    }

    [Test]
    public void Clear_Always_ClearsStorage()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ValueHash256 key = Hash256.FromBytesWithPadding([1]);

        storage.Set(key, new ValueHash256(Bytes32(1, 2, 3)));
        storage.Clear(key);

        ValueHash256 actual = storage.Get(key);
        actual.Should().Be(Hash256.Zero.ValueHash256);
    }

    [TestCase(32)]
    [TestCase(100)]
    public void ClearBytes_Always_ClearsStorage(int length)
    {
        using IDisposable disposable = TestArbosStorage.Create(out TrackingWorldState worldState, out ArbosStorage storage, TestAccount);

        worldState.Commit(GetSpecProvider().GenesisSpec);
        worldState.CommitTree(0);
        Hash256 emptyStorageStateRoot = worldState.StateRoot;

        storage.Set(RandomNumberGenerator.GetBytes(length));
        worldState.Commit(GetSpecProvider().GenesisSpec);
        worldState.CommitTree(1);
        Hash256 filledStorageStateRoot = worldState.StateRoot;

        storage.ClearBytes();
        worldState.Commit(GetSpecProvider().GenesisSpec);
        worldState.CommitTree(2);
        Hash256 clearBytesStateRoot = worldState.StateRoot;

        emptyStorageStateRoot.Should().NotBe(filledStorageStateRoot);
        emptyStorageStateRoot.Should().Be(clearBytesStateRoot);
    }

    [Test]
    public void ClearByULong_Always_ClearsStorage()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ulong key = 999;

        storage.Set(key, new ValueHash256(Bytes32(1, 2, 3)));
        storage.Clear(key);

        ValueHash256 actual = storage.Get(key);
        actual.Should().Be(Hash256.Zero.ValueHash256);
    }

    [Test]
    public void Get_Always_BurnsStorageReadCost()
    {
        SystemBurner systemBurner = new();
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        storage.Get(Hash256.FromBytesWithPadding([1]));

        systemBurner.Burned.Should().Be(ArbosStorage.StorageReadCost);
    }

    [TestCase(4)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(100)]
    public void GetBytesLength_Always_ReturnsCorrectLength(int length)
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);

        byte[] value = RandomNumberGenerator.GetBytes(length);

        storage.Set(value);

        ulong actual = storage.GetBytesSize();
        actual.Should().Be((ulong)length);
    }

    [Test]
    public void GetCodeHash_Always_BurnsStorageReadCostAndGetsHash()
    {
        SystemBurner systemBurner = new();
        using IDisposable disposable = TestArbosStorage.Create(out TrackingWorldState worldState, out ArbosStorage storage, TestAccount, systemBurner);

        // Insert random code to ensure the code hash is set.
        byte[] code = RandomNumberGenerator.GetBytes(Hash256.Size);
        ValueHash256 codeHash = Keccak.Compute(code);
        worldState.InsertCode(TestAccount, in codeHash, code, GetSpecProvider().GenesisSpec);
        worldState.Commit(GetSpecProvider().GenesisSpec);
        worldState.CommitTree(0);

        ValueHash256 expected = worldState.GetCodeHash(TestAccount);
        ValueHash256 actual = storage.GetCodeHash(TestAccount);

        systemBurner.Burned.Should().Be(ArbosStorage.StorageCodeHashCost);
        actual.Should().Be(expected);
    }

    [Test]
    public void GetFree_Always_BurnsNothing()
    {
        SystemBurner systemBurner = new();
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        storage.GetFree(Hash256.FromBytesWithPadding([1]));

        systemBurner.Burned.Should().Be(0);
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(9u)]
    [TestCase(ulong.MaxValue)]
    public void GetSetByULong_Always_SetsAndGetsTheSameValue(ulong key)
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ValueHash256 value = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        storage.Set(key, value);

        ValueHash256 actual = storage.Get(key);
        actual.Should().Be(value);
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(9u)]
    [TestCase(ulong.MaxValue)]
    public void GetSetULong_Always_SetsAndGetsProperValue(ulong value)
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([1]), value);

        ulong actual = storage.GetULong(Hash256.FromBytesWithPadding([1]));
        actual.Should().Be(value);
    }

    [TestCase(0u, 0u)]
    [TestCase(1u, 2u)]
    [TestCase(9u, 10u)]
    [TestCase(ulong.MaxValue, ulong.MaxValue)]
    public void GetSetULongByULong_Always_SetsAndGetsTheSameValue(ulong key, ulong value)
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);

        storage.Set(key, value);

        ulong actual = storage.GetULong(key);
        actual.Should().Be(value);
    }

    [TestCase(0, 0, "0xba12bdd82e221f7a7dfbaeb06816308a7d8c7004ee06ebe8efbcd89176bb6a66")]
    [TestCase(0, 1, "0x0bd839f4461b871f3a9c86a40a5fdd92fd303f2683640e55dfb3105603a46223")]
    [TestCase(0, 10, "0xb72e675bcf22090999b034b3b0f020896b9818a95425ff7ae87118c6a1a8e38e")]
    [TestCase(0, 100, "0x47ee75c7bd2b4cc759f12816dbe07d79e17f59e5d1cd238ab0c2202ba4ce34c0")]
    [TestCase(0, 255, "0x50cb541fe6b0f9b567dcdda9d45065ed5ce46acb542706cedf7d95af9167350e")]
    [TestCase(1, 1, "0xd74b26b06a5dd654e737d00781cb44e3060cd87ac1e00ab8752c1ba2be05ffb5")]
    [TestCase(1, 255, "0xdbdac6271f6e6f0b61b2d13ce15962a39f49ff9593aa09e53e4a9ce085ccd03b")]
    public void Set_Always_MapsAddressTheSameWayAsNitro(byte byte1, byte byte2, string cellHash)
    {
        using IDisposable disposable = TestArbosStorage.Create(out TrackingWorldState worldState, out ArbosStorage storage, TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([byte1, byte2]), Hash256.Zero);

        worldState.SetRecords[0].Should().BeEquivalentTo(new WorldStateSetRecord(TestAccount, new ValueHash256(cellHash), [0]));
    }

    [TestCase(0, 0, 1)]
    [TestCase(0, 1, 255)]
    [TestCase(1, 255, 255)]
    [TestCase(255, 255, 255)]
    public void Set_Always_TrimsLeadingZeroes(byte byte1, byte byte2, byte byte3)
    {
        byte[] expectedValue = new[] { byte1, byte2, byte3 }.WithoutLeadingZeros().ToArray();

        using IDisposable disposable = TestArbosStorage.Create(out TrackingWorldState worldState, out ArbosStorage storage, TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([1]), new ValueHash256(Bytes32(byte1, byte2, byte3)));

        worldState.SetRecords[0].Should().BeEquivalentTo(new WorldStateSetRecord(TestAccount, MappedCellHashOne, expectedValue));
    }

    [Test]
    public void Set_ValueIsEmpty_BurnsStorageWriteZeroCost()
    {
        SystemBurner systemBurner = new();
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        storage.Set(Hash256.FromBytesWithPadding([1]), Hash256.Zero);

        systemBurner.Burned.Should().Be(ArbosStorage.StorageWriteZeroCost);
    }

    [Test]
    public void Set_ValueIsNonEmpty_BurnsStorageWriteCost()
    {
        SystemBurner systemBurner = new();
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        storage.Set(Hash256.FromBytesWithPadding([1]), new ValueHash256(Bytes32(1, 2, 3)));

        systemBurner.Burned.Should().Be(ArbosStorage.StorageWriteCost);
    }

    [TestCase(4)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(100)]
    [TestCase(200)]
    [TestCase(400)]
    public void SetBytesGetBytes_Always_SetsAndGetsTheSameValue(int length)
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);

        byte[] value = RandomNumberGenerator.GetBytes(length);

        storage.Set(value);

        byte[] actual = storage.GetBytes();
        actual.Should().BeEquivalentTo(value);
    }

    [TestCase(0, 0, 1)]
    [TestCase(0, 1, 255)]
    [TestCase(1, 255, 255)]
    [TestCase(255, 255, 255)]
    public void SetGet_Always_GetReturnsWhatsSet(byte byte1, byte byte2, byte byte3)
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);

        ValueHash256 key = Hash256.FromBytesWithPadding([1]);
        ValueHash256 value = new Hash256(Bytes32(byte1, byte2, byte3));

        storage.Set(key, value);

        ValueHash256 actual = storage.Get(key);
        actual.Should().BeEquivalentTo(value);
    }

    [TestCase(TracingScenario.TracingBeforeEvm)]
    [TestCase(TracingScenario.TracingDuringEvm)]
    [TestCase(TracingScenario.TracingAfterEvm)]
    public void Trace_OnlyDuringEvm_RecordStorageGet(TracingScenario scenario)
    {
        ArbitrumGethLikeTxTracer tracer = new(GethTraceOptions.Default);
        using ExecutionEnvironment executionEnv = ExecutionEnvironment.Rent(
            CodeInfo.Empty,
            TestAccount,
            TestAccount,
            null,
            0,
            0,
            0,
            Array.Empty<byte>());
        TracingInfo tracingInfo = new(tracer, scenario, executionEnv);

        SystemBurner systemBurner = new(tracingInfo);
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        storage.Get(Hash256.FromBytesWithPadding([1]));

        GethLikeTxTrace entry = tracer.BuildResult();
        entry.Should().NotBeNull();
        if (scenario == TracingScenario.TracingDuringEvm)
        {
            entry.Entries.Count.Should().Be(1);
            entry.Entries[0].Opcode.Should().Be("SLOAD");
        }
        else
        {
            entry.Entries.Count.Should().Be(0);
        }
    }

    [TestCase(TracingScenario.TracingBeforeEvm)]
    [TestCase(TracingScenario.TracingDuringEvm)]
    [TestCase(TracingScenario.TracingAfterEvm)]
    public void Trace_OnlyDuringEvm_RecordStorageSet(TracingScenario scenario)
    {
        ArbitrumGethLikeTxTracer tracer = new(GethTraceOptions.Default);
        using ExecutionEnvironment executionEnv = ExecutionEnvironment.Rent(
            CodeInfo.Empty,
            TestAccount,
            TestAccount,
            null,
            0,
            0,
            0,
            Array.Empty<byte>());
        TracingInfo tracingInfo = new(tracer, scenario, executionEnv);

        SystemBurner systemBurner = new(tracingInfo);
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount, systemBurner);

        storage.Set(Hash256.FromBytesWithPadding([1]), Hash256.FromBytesWithPadding([2]));

        GethLikeTxTrace entry = tracer.BuildResult();
        entry.Should().NotBeNull();
        if (scenario == TracingScenario.TracingDuringEvm)
        {
            entry.Entries.Count.Should().Be(1);
            entry.Entries[0].Opcode.Should().Be("SSTORE");
        }
        else
        {
            entry.Entries.Count.Should().Be(0);
        }
    }

    private static byte[] Bytes32(params byte[] bytes)
    {
        byte[] result = new byte[32];
        Array.Copy(bytes, 0, result, System.Math.Max(0, 32 - bytes.Length), System.Math.Min(bytes.Length, 32));
        return result;
    }

    private static ISpecProvider GetSpecProvider()
        => FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();
}
