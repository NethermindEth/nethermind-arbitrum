using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;

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

    public static UInt256 GetCurrentTxL1GasFees(ArbitrumPrecompileExecutionContext context)
    {
        // TODO: In the full implementation, this would access the posterFee from
        // the transaction execution context passed through the VM.
        return UInt256.Zero; // Placeholder - would contain actual posterFee in full implementation
    }
}
