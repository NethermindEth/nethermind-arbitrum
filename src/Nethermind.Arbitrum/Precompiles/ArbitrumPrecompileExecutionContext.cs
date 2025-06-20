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
    BlockExecutionContext BlockExecutionContext,
    ulong ChainId
) : IBurner
{
    public ulong GasLeft { get; private set; } = GasSupplied;

    public ArbosState ArbosState { get; set; }

    public List<LogEntry> EventLogs { get; } = [];

    //TODO: let's for now put this here (as in nitro) but let's probably remove it
    // which means we'd have to put some tx processor fields elsewhere
    public ArbitrumTransactionProcessor TxProcessor { get; set; }

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

    public void AddEventLog(LogEntry log)
    {
        EventLogs.Add(log);
    }
}
