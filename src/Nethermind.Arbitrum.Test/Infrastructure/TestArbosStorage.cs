using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Db;
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

        WasmStore.Initialize(new WasmStore(new WasmDb(new MemDb()), new StylusTargetConfig(), cacheTag: 1));

        ArbosStorage storage = new(worldState, currentBurner, currentTestAccount);

        return (storage, worldState);
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
