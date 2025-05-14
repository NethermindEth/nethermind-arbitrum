using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Tests.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Tests.Arbos;

public class ArbosStorageTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [TestCase(0, "0x15fed0451499512d95f3ec5a41c878b9de55f21878b5b4e190d4667ec709b400")]
    [TestCase(1, "0x15fed0451499512d95f3ec5a41c878b9de55f21878b5b4e190d4667ec709b401")]
    [TestCase(10, "0x15fed0451499512d95f3ec5a41c878b9de55f21878b5b4e190d4667ec709b40a")]
    [TestCase(100, "0x15fed0451499512d95f3ec5a41c878b9de55f21878b5b4e190d4667ec709b464")]
    public void ArbosStorage_Always_DoesCorrectMapping(byte value, string cellHash)
    {
        TrackingWorldState worldState = new(new WorldState(new TrieStore(new MemDb(), Logger), new MemDb(), Logger));
        ArbosStorage storage = new(worldState, new SystemBurner(Logger));

        storage.Set(Hash256.FromBytesWithPadding([value]), Hash256.Zero);

        worldState.SetRecords[0].Should().BeEquivalentTo(new WorldStateSetRecord(
            ArbosAddresses.ArbosSystemAccount,
            new ValueHash256(cellHash),
            new byte[32]));
    }

    [Test]
    public void ArbosStorage_InitializeL1Pricing_StoresDataCorrectly()
    {
        TrackingWorldState worldState = new(new WorldState(new TrieStore(new MemDb(), Logger), new MemDb(), Logger));
        ArbosStorage storage = new(worldState, new SystemBurner(Logger));

        Console.WriteLine("Storage set");
        storage.Set(Hash256.FromBytesWithPadding([1]), Hash256.Zero);

        var l1PricingStorage = storage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L1PricingSubspace);

        Console.WriteLine("l1PricingStorage set");
        l1PricingStorage.Set(Hash256.FromBytesWithPadding([1]), Hash256.Zero);

        L1PricingState.Initialize(l1PricingStorage, ArbosAddresses.BatchPosterAddress, 154, Logger.GetClassLogger<L1PricingState>());
    }
}
