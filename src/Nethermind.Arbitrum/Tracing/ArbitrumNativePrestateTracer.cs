using Nethermind.Arbitrum.Arbos;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native.Prestate;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumNativePrestateTracer(
    IWorldState worldState,
    GethTraceOptions options,
    Hash256? txHash,
    Address? from,
    Address? to = null,
    Address? beneficiary = null)
    : NativePrestateTracer(worldState, options, txHash, from, to, beneficiary), IArbitrumTxTracer
{
    public void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before)
    {
        LookupAccount(ArbosAddresses.ArbosSystemAccount);
        LookupStorage(ArbosAddresses.ArbosSystemAccount, index);
    }

    public void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before)
    {
        LookupAccount(ArbosAddresses.ArbosSystemAccount);
        LookupStorage(ArbosAddresses.ArbosSystemAccount, index);
    }

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before,
        BalanceChangeReason reason)
    {
    }

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk)
    {
    }
}
