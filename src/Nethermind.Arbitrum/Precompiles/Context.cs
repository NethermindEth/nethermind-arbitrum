using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.TransactionProcessing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;

namespace Nethermind.Arbitrum.Precompiles;

public class Context(
    Address? caller,
    ulong gasSupplied,
    ulong gasLeft,
    ITxTracer txTracer,
    bool readOnly
): IBurner {
    private readonly Address? _caller = caller;

    private readonly ulong _gasSupplied = gasSupplied;

    private ulong _gasLeft = gasLeft;

    public ArbitrumTransactionProcessor TxProcessor { get; set; }

    public ArbosState ArbosState { get; set; }

    private readonly ITxTracer _tracingInfo = txTracer;

    private readonly bool _readOnly = readOnly;


    public ulong Burned => _gasSupplied - _gasLeft;

    public bool ReadOnly => _readOnly;

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

    public ulong GasLeft => _gasLeft;

    public ITxTracer TracingInfo => _tracingInfo;

    public ValueHash256 GetCodeHash(Address address)
    {
        return ArbosState.BackingStorage.GetCodeHash(address);
    }
}

