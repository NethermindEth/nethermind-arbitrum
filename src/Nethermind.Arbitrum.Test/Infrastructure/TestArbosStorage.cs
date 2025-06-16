using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestArbosStorage
{
    public static readonly Address DefaultTestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    public static (ArbosStorage, TrackingWorldState) Create(Address? testAccount = null, IBurner? burner = null)
    {
        Address currentTestAccount = testAccount ?? DefaultTestAccount;
        IBurner currentBurner = burner ?? new SystemBurner();

        TrackingWorldState worldState = TrackingWorldState.CreateNewInMemory();
        worldState.CreateAccountIfNotExists(currentTestAccount, UInt256.Zero, UInt256.One);

        ArbosStorage storage = new(worldState, currentBurner, currentTestAccount);

        return (storage, worldState);
    }
}
