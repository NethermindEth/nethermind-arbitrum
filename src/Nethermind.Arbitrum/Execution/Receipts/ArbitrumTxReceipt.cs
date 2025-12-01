using Nethermind.Arbitrum.Evm;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Execution.Receipts;

public class ArbitrumTxReceipt : TxReceipt
{
    public ulong GasUsedForL1 { get; set; }

    /// <summary>
    /// Multidimensional gas used by this transaction.
    /// Reads from and writes to PolicyData field.
    /// </summary>
    public MultiGas MultiGasUsed
    {
        get => PolicyData is MultiGas multiGas ? multiGas : MultiGas.Zero;
        set => PolicyData = value;
    }
}
