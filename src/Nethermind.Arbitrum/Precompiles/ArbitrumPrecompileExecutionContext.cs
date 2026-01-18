using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Stylus;
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
    UInt256 Value,
    ulong GasSupplied,
    IWorldState WorldState,
    IWasmStore WasmStore,
    BlockExecutionContext BlockExecutionContext,
    ulong ChainId,
    TracingInfo? TracingInfo,
    IReleaseSpec ReleaseSpec = null!
) : IBurner
{
    private ulong _gasLeft = GasSupplied;

    public ArbosState ArbosState { get; set; } = null!;

    public BlockExecutionContext BlockExecutionContext { get; protected set; } = BlockExecutionContext;

    public IBlockhashProvider BlockHashProvider { get; init; } = null!;

    public ulong Burned => GasSupplied - GasLeft;

    public int CallDepth { get; init; }

    public Address Caller { get; protected set; } = Caller;

    public Address? CurrentRefundTo { get; init; }

    public Hash256? CurrentRetryable { get; init; }

    public List<LogEntry> EventLogs { get; } = [];

    public Address ExecutingAccount { get; init; } = null!;

    public ArbosState FreeArbosState { get; set; } = null!;

    public ref ulong GasLeft => ref _gasLeft;

    public Address? GrandCaller { get; init; }

    public bool IsCallStatic { get; init; }

    public bool IsMethodCalledPure { get; set; }

    public ValueHash256 Origin { get; init; }

    public UInt256 PosterFee { get; init; }
    public bool ReadOnly { get; set; }

    public IReleaseSpec ReleaseSpec { get; protected set; } = ReleaseSpec;

    public ArbitrumTxType TopLevelTxType { get; init; }

    public TracingInfo? TracingInfo { get; protected set; } = TracingInfo;

    public UInt256 Value { get; init; } = Value;

    public void AddEventLog(LogEntry log)
    {
        EventLogs.Add(log);
    }

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
        Metrics.EvmExceptions++;
        throw ArbitrumPrecompileException.CreateOutOfGasException();
    }
}
