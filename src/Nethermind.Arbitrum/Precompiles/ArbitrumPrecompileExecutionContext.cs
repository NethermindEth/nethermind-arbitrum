using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
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
    public bool ReadOnly { get; set; }

    public bool IsCallStatic { get; init; }

    public TracingInfo? TracingInfo { get; protected set; } = TracingInfo;

    public Address Caller { get; protected set; } = Caller;

    public ref ulong GasLeft => ref _gasLeft;

    public BlockExecutionContext BlockExecutionContext { get; protected set; } = BlockExecutionContext;

    public IReleaseSpec ReleaseSpec { get; protected set; } = ReleaseSpec;

    public ArbosState ArbosState { get; set; } = null!;

    public List<LogEntry> EventLogs { get; } = [];

    public IBlockhashProvider BlockHashProvider { get; init; } = null!;

    public int CallDepth { get; init; }

    public Address? GrandCaller { get; init; }

    public ValueHash256 Origin { get; init; }

    public UInt256 Value { get; init; } = Value;

    public ArbitrumTxType TopLevelTxType { get; init; }

    public ArbosState FreeArbosState { get; set; } = null!;

    public Hash256? CurrentRetryable { get; init; }

    public Address? CurrentRefundTo { get; init; }

    public UInt256 PosterFee { get; init; }

    public Address ExecutingAccount { get; init; } = null!;

    public bool IsMethodCalledPure { get; set; }

    public ulong Burned => GasSupplied - GasLeft;

    public IArbitrumSpecHelper? SpecHelper { get; init; }

    private ulong _gasLeft = GasSupplied;

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
        Nethermind.Evm.Metrics.EvmExceptions++;
        throw ArbitrumPrecompileException.CreateOutOfGasException();
    }

    public void AddEventLog(LogEntry log)
    {
        EventLogs.Add(log);
    }
}
