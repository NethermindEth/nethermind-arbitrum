using System.Reflection;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class ArbosStateTestExtensions
{
    public static void SetCurrentArbosVersion(this ArbosState arbosState, ulong version)
    {
        // Set the backing storage
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, version);

        // Update the property using reflection since it has a private setter
        var property = typeof(ArbosState).GetProperty("CurrentArbosVersion");
        property?.SetValue(arbosState, version);
    }

    public static void SetL1BlockNumber(this Blockhashes blockHashes, ulong blockNumber)
    {
        blockHashes.RecordNewL1Block(blockNumber, ValueKeccak.Zero, ArbosVersion.Forty);
    }
}
