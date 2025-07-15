using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.State;

namespace Nethermind.Arbitrum.Precompiles;

public record ArbitrumPrecompileExecutionContext(
    Address Caller,
    ulong GasSupplied,
    ITxTracer TracingInfo,
    bool ReadOnly,
    IWorldState WorldState,
    BlockExecutionContext BlockExecutionContext,
    ulong ChainId,
    IReleaseSpec ReleaseSpec = null!
) : IBurner
{
    public Address Caller { get; protected set; } = Caller;

    public ulong GasLeft { get; protected set; } = GasSupplied;

    public BlockExecutionContext BlockExecutionContext { get; protected set; } = BlockExecutionContext;

    public IReleaseSpec ReleaseSpec { get; protected set; } = ReleaseSpec;

    public ArbosState ArbosState { get; set; }

    public List<LogEntry> EventLogs { get; } = [];

    public Hash256? CurrentRetryable { get; init; }

    public Address? CurrentRefundTo { get; init; }

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

    private void BurnOut()
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
