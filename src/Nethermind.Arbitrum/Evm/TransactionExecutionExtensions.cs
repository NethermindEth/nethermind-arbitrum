using Nethermind.Arbitrum.Core;
using Nethermind.Evm;
using Nethermind.Int256;

public static class TransactionExecutionExtensions
{
    /// <summary>
    /// Gets the effective base fee for gas calculations.
    /// When NoBaseFee is active:
    /// - EVM execution sees BaseFee = 0 (from header)
    /// - Gas calculations use the original base fee (from VM's stored value)
    /// </summary>
    public static UInt256 GetEffectiveBaseFeeForGasCalculations(this in BlockExecutionContext context)
    {
        // Check if we're using an ArbitrumBlockHeader with the original base fee
        if (context.Header is ArbitrumBlockHeader arbitrumHeader)
        {
            return arbitrumHeader.OriginalBaseFee;
        }

        // Fallback to header's base fee
        return context.Header.BaseFeePerGas;
    }
}
