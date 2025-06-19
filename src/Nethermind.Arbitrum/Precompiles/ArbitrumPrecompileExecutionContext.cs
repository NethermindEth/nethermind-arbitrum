using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.State;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbitrumPrecompileExecutionContext(
    Address? caller,
    ulong gasSupplied,
    ulong gasLeft,
    ITxTracer txTracer,
    bool readOnly,
    IWorldState worldState,
    BlockExecutionContext blockExecutionContext,
    ulong chainId
) : IBurner
{
    private readonly Address? _caller = caller;

    private readonly ulong _gasSupplied = gasSupplied;

    private ulong _gasLeft = gasLeft;

    private readonly ITxTracer _tracingInfo = txTracer;

    private readonly bool _readOnly = readOnly;

    public ulong ChainId { get; } = chainId;

    public BlockExecutionContext BlockExecutionContext { get; } = blockExecutionContext;

    public IWorldState WorldState { get; } = worldState;

    public List<LogEntry> EventLogs { get; } = new();

    public ArbitrumTransactionProcessor TxProcessor { get; }

    public ArbosState ArbosState { get; set; }

    public ulong Burned => _gasSupplied - _gasLeft;

    public bool ReadOnly => _readOnly;

    public ulong GasLeft => _gasLeft;

    public ITxTracer TracingInfo => _tracingInfo;

    public Address? Caller => _caller;


    public void Burn(ulong amount)
    {
        if (_gasLeft < amount)
        {
            BurnOut();
        }

        _gasLeft -= amount;
    }

    public void BurnOut()
    {
        _gasLeft = 0;
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

