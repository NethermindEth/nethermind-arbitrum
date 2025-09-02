using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestArbosStorage
{
    public static readonly Address DefaultTestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    public static ArbosStorage Create(IWorldState worldState, Address? testAccount = null, IBurner? burner = null)
    {
        Address currentTestAccount = testAccount ?? DefaultTestAccount;
        IBurner currentBurner = burner ?? new SystemBurner();

        worldState.CreateAccountIfNotExists(currentTestAccount, UInt256.Zero, UInt256.One);

        ArbosStorage storage = new(worldState, currentBurner, currentTestAccount);

        return storage;
    }

    public static IDisposable Create(out TrackingWorldState worldState, out ArbosStorage arbosStorage, Address? testAccount = null, IBurner? burner = null)
    {
        Address currentTestAccount = testAccount ?? DefaultTestAccount;
        IBurner currentBurner = burner ?? new SystemBurner();

        worldState = TrackingWorldState.CreateNewInMemory();
        var dispose = worldState.BeginScope(IWorldState.PreGenesis);
        worldState.CreateAccountIfNotExists(currentTestAccount, UInt256.Zero, UInt256.One);

        arbosStorage = new(worldState, currentBurner, currentTestAccount);

        return dispose;
    }

    public class TestBurner(ulong availableGas, TracingInfo? tracingInfo = null) : IBurner
    {
        private ulong _availableGas = availableGas;

        public bool ReadOnly => false;
        public TracingInfo? TracingInfo { get; } = tracingInfo;
        public ulong Burned => _availableGas;
        public ref ulong GasLeft => ref _availableGas;

        public void Burn(ulong amount)
        {
            checked
            {
                _availableGas -= amount;
            }
        }
    }
}
