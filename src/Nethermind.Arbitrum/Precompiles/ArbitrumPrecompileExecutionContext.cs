using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.State;

namespace Nethermind.Arbitrum.Precompiles;

public record ArbitrumPrecompileExecutionContext(
    Address? Caller,
    ulong GasSupplied,
    ITxTracer TracingInfo,
    bool ReadOnly,
    IWorldState WorldState,
    BlockExecutionContext BlockExecutionContext
) : IBurner
{
    public ulong GasLeft { get; private set; } = GasSupplied;
    public ArbosState ArbosState { get; set; }

    public ulong Burned => GasSupplied - GasLeft;

    public void Burn(ulong amount)
    {
        if (GasLeft < amount)
        {
            BurnOut();
        }
        else
        {
            GasLeft -= amount;
        }
    }

    public void BurnOut()
    {
        GasLeft = 0;
        EvmPooledMemory.ThrowOutOfGasException();
    }

    public ValueHash256 GetCodeHash(Address address)
    {
        return ArbosState.BackingStorage.GetCodeHash(address);
    }
}