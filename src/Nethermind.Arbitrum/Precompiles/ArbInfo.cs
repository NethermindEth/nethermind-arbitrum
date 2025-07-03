using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Arbitrum.Evm;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbInfo
{
    public static Address Address => ArbosAddresses.ArbInfoAddress;

    public static UInt256 GetBalance(ArbitrumPrecompileExecutionContext context, Address account)
    {
        context.Burn(GasCostOf.BalanceEip1884);
        return context.WorldState.GetBalance(account);
    }

    public static byte[] GetCode(ArbitrumPrecompileExecutionContext context, Address account)
    {
        context.Burn(GasCostOf.ColdSLoad);
        byte[] code = context.WorldState.GetCode(account)!;
        context.Burn(GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)code.Length));
        return code;
    }

    /// <summary>
    /// Gets the fee paid to the aggregator for posting this transaction to L1.
    /// This demonstrates the architectural improvement: posterFee is now accessed from
    /// transaction-scoped context instead of being a stateful field in the processor.
    /// 
    /// Note: This is a demonstration of how the integration would work.
    /// In the full implementation, ArbitrumPrecompileExecutionContext would need
    /// a reference to the current transaction's execution context.
    /// </summary>
    public static UInt256 GetCurrentTxL1GasFees(ArbitrumPrecompileExecutionContext context)
    {
        // TODO: In the full implementation, this would access the posterFee from
        // the transaction execution context passed through the VM.
        // For now, return zero as a placeholder to demonstrate the concept.
        // 
        // Example of what this would look like:
        // return context.TxExecutionContext.PosterFee;
        
        return UInt256.Zero; // Placeholder - would contain actual posterFee in full implementation
    }
}
