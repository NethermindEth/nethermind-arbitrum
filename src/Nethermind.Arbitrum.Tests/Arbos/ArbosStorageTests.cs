using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Tests.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Tests.Arbos;

public partial class ArbosStorageTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;
    private static readonly Address TestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
    private static readonly Hash256 MappedCellHashOne = new Hash256("0x0bd839f4461b871f3a9c86a40a5fdd92fd303f2683640e55dfb3105603a46223");

    [TestCase(0, 0, "0xba12bdd82e221f7a7dfbaeb06816308a7d8c7004ee06ebe8efbcd89176bb6a66")]
    [TestCase(0,1, "0x0bd839f4461b871f3a9c86a40a5fdd92fd303f2683640e55dfb3105603a46223")]
    [TestCase(0,10, "0xb72e675bcf22090999b034b3b0f020896b9818a95425ff7ae87118c6a1a8e38e")]
    [TestCase(0,100, "0x47ee75c7bd2b4cc759f12816dbe07d79e17f59e5d1cd238ab0c2202ba4ce34c0")]
    [TestCase(0,255, "0x50cb541fe6b0f9b567dcdda9d45065ed5ce46acb542706cedf7d95af9167350e")]
    [TestCase(1,1, "0xd74b26b06a5dd654e737d00781cb44e3060cd87ac1e00ab8752c1ba2be05ffb5")]
    [TestCase(1,255, "0xdbdac6271f6e6f0b61b2d13ce15962a39f49ff9593aa09e53e4a9ce085ccd03b")]
    public void Set_Always_MapsAddressTheSameWayAsNitro(byte byte1, byte byte2, string cellHash)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([byte1, byte2]), Hash256.Zero);

        worldState.SetRecords[0].Should().BeEquivalentTo(new WorldStateSetRecord(TestAccount, new ValueHash256(cellHash), [0]));
    }

    [TestCase(0, 0, 1)]
    [TestCase(0, 1, 255)]
    [TestCase(1, 255, 255)]
    [TestCase(255, 255, 255)]
    public void Set_Always_TrimsLeadingZeroes(byte byte1, byte byte2, byte byte3)
    {
        byte[] expectedValue = new[] {byte1, byte2, byte3}.WithoutLeadingZeros().ToArray();

        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([1]), new ValueHash256(Bytes32(byte1, byte2, byte3)));

        worldState.SetRecords[0].Should().BeEquivalentTo(new WorldStateSetRecord(TestAccount, MappedCellHashOne, expectedValue));
    }

    [TestCase(0, 0, 1)]
    [TestCase(0, 1, 255)]
    [TestCase(1, 255, 255)]
    [TestCase(255, 255, 255)]
    public void SetGet_Always_GetReturnsWhatsSet(byte byte1, byte byte2, byte byte3)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        ValueHash256 key = Hash256.FromBytesWithPadding([1]);
        ValueHash256 value = new Hash256(Bytes32(byte1, byte2, byte3));

        storage.Set(key, value);

        var actual = storage.Get(key);
        actual.Should().BeEquivalentTo(value);
    }

    [Test]
    public void Get_Always_BurnsStorageReadCost()
    {
        TrackingWorldState worldState = CreateWorldState();
        SystemBurner systemBurner = new SystemBurner(Logger);
        ArbosStorage storage = new(worldState, systemBurner, TestAccount);

        storage.Get(Hash256.FromBytesWithPadding([1]));

        systemBurner.Burned.Should().Be(ArbosStorage.StorageReadCost);
    }

    [Test]
    public void GetFree_Always_BurnsNothing()
    {
        TrackingWorldState worldState = CreateWorldState();
        SystemBurner systemBurner = new SystemBurner(Logger);
        ArbosStorage storage = new(worldState, systemBurner, TestAccount);

        storage.GetFree(Hash256.FromBytesWithPadding([1]));

        systemBurner.Burned.Should().Be(0);
    }

    [Test]
    public void Set_ValueIsEmpty_BurnsStorageWriteZeroCost()
    {
        TrackingWorldState worldState = CreateWorldState();
        SystemBurner systemBurner = new SystemBurner(Logger);
        ArbosStorage storage = new(worldState, systemBurner, TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([1]), Hash256.Zero);

        systemBurner.Burned.Should().Be(ArbosStorage.StorageWriteZeroCost);
    }

    [Test]
    public void Set_ValueIsNonEmpty_BurnsStorageWriteCost()
    {
        TrackingWorldState worldState = CreateWorldState();
        SystemBurner systemBurner = new SystemBurner(Logger);
        ArbosStorage storage = new(worldState, systemBurner, TestAccount);

        storage.Set(Hash256.FromBytesWithPadding([1]), new ValueHash256(Bytes32(1, 2, 3)));

        systemBurner.Burned.Should().Be(ArbosStorage.StorageWriteCost);
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(9u)]
    [TestCase(ulong.MaxValue)]
    public void GetSetULong_Always_SetsAndGetsProperValue(ulong value)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        storage.SetULong(Hash256.FromBytesWithPadding([1]), value);

        ulong actual = storage.GetULong(Hash256.FromBytesWithPadding([1]));
        actual.Should().Be(value);
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(9u)]
    [TestCase(ulong.MaxValue)]
    public void GetSetByULong_Always_SetsAndGetsTheSameValue(ulong key)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ValueHash256 value = new ValueHash256(RandomNumberGenerator.GetBytes(32));

        storage.SetByULong(key, value);

        ValueHash256 actual = storage.GetByULong(key);
        actual.Should().Be(value);
    }

    [TestCase(0u, 0u)]
    [TestCase(1u, 2u)]
    [TestCase(9u, 10u)]
    [TestCase(ulong.MaxValue, ulong.MaxValue)]
    public void GetSetULongByULong_Always_SetsAndGetsTheSameValue(ulong key, ulong value)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        storage.SetULongByULong(key, value);

        ulong actual = storage.GetULongByULong(key);
        actual.Should().Be(value);
    }

    [Test]
    public void Clear_Always_ClearsStorage()
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ValueHash256 key = Hash256.FromBytesWithPadding([1]);

        storage.Set(key, new ValueHash256(Bytes32(1, 2, 3)));
        storage.Clear(key);

        var actual = storage.Get(key);
        actual.Should().Be(Hash256.Zero.ValueHash256);
    }

    [Test]
    public void ClearByULong_Always_ClearsStorage()
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ulong key = 999;

        storage.SetByULong(key, new ValueHash256(Bytes32(1, 2, 3)));
        storage.ClearByULong(key);

        var actual = storage.GetByULong(key);
        actual.Should().Be(Hash256.Zero.ValueHash256);
    }

    [TestCase(4)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(100)]
    public void GetBytesLength_Always_ReturnsCorrectLength(int length)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        byte[] value = RandomNumberGenerator.GetBytes(length);

        storage.SetBytes(value);

        ulong actual = storage.GetBytesSize();
        actual.Should().Be((ulong)length);
    }

    [TestCase(4)]
    [TestCase(16)]
    [TestCase(32)]
    [TestCase(100)]
    [TestCase(200)]
    [TestCase(400)]
    public void SetBytesGetBytes_Always_SetsAndGetsTheSameValue(int length)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);

        byte[] value = RandomNumberGenerator.GetBytes(length);

        storage.SetBytes(value);

        byte[] actual = storage.GetBytes();
        actual.Should().BeEquivalentTo(value);
    }

    [TestCase(32)]
    [TestCase(100)]
    public void ClearBytes_Always_ClearsStorage(int length)
    {
        TrackingWorldState worldState = CreateWorldState();
        worldState.Commit(FullChainSimulationReleaseSpec.Instance);
        worldState.CommitTree(0);
        var emptyStorageStateRoot = worldState.StateRoot;

        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        byte[] value = RandomNumberGenerator.GetBytes(length);

        storage.SetBytes(value);
        worldState.Commit(FullChainSimulationReleaseSpec.Instance);
        worldState.CommitTree(1);
        var filledStorageStateRoot = worldState.StateRoot;

        storage.ClearBytes();
        worldState.Commit(FullChainSimulationReleaseSpec.Instance);
        worldState.CommitTree(2);
        var clearBytesStateRoot = worldState.StateRoot;

        emptyStorageStateRoot.Should().NotBe(filledStorageStateRoot);
        emptyStorageStateRoot.Should().Be(clearBytesStateRoot);
    }

    private static byte[] Bytes32(params byte[] bytes)
    {
        byte[] result = new byte[32];
        Array.Copy(bytes, 0, result, Math.Max(0, 32 - bytes.Length), Math.Min(bytes.Length, 32));
        return result;
    }

    private static TrackingWorldState CreateWorldState()
    {
        TrackingWorldState worldState = TrackingWorldState.CreateNewInMemory();
        worldState.CreateAccountIfNotExists(TestAccount, UInt256.Zero, UInt256.One);
        return worldState;
    }
}
