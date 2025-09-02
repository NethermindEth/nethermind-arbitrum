using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public record ArbitrumPrecompileExecutionContext(
    Address Caller,
    ulong GasSupplied,
    bool ReadOnly,
    IWorldState WorldState,
    BlockExecutionContext BlockExecutionContext,
    ulong ChainId,
    TracingInfo? TracingInfo,
    IReleaseSpec ReleaseSpec = null!
) : IBurner
{
    private ulong _gasLeft = GasSupplied;

    public TracingInfo? TracingInfo { get; protected set; } = TracingInfo;

    public Address Caller { get; protected set; } = Caller;

    public ref ulong GasLeft => ref _gasLeft;

    public BlockExecutionContext BlockExecutionContext { get; protected set; } = BlockExecutionContext;

    public IReleaseSpec ReleaseSpec { get; protected set; } = ReleaseSpec;

    public ArbosState ArbosState { get; set; }

    public List<LogEntry> EventLogs { get; } = [];

    public IBlockhashProvider BlockHashProvider { get; init; }

    public int CallDepth { get; init; }

    public Address? GrandCaller { get; init; }

    public ValueHash256 Origin { get; init; }

    public UInt256 Value { get; init; }

    public ArbitrumTxType TopLevelTxType { get; init; }

    public ArbosState FreeArbosState { get; set; }

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
