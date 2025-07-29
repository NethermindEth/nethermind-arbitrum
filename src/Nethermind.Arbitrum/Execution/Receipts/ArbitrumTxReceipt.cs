using Nethermind.Core;

namespace Nethermind.Arbitrum.Execution.Receipts;

public class ArbitrumTxReceipt : TxReceipt
{
    public ArbitrumTxReceipt()
    { }

    public ArbitrumTxReceipt(TxReceipt receipt) : base(receipt)
    { }

    public ulong GasUsedForL1 { get; set; }
}
