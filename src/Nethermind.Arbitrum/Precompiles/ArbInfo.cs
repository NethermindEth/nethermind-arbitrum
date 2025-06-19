using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbInfo
{
    public static Address Address => ArbosAddresses.ArbInfoAddress;

    public Int256.UInt256 GetBalance(ArbitrumPrecompileExecutionContext context, Address account)
    {
        context.Burn(GasCostOf.BalanceEip1884);
        return context.WorldState.GetBalance(account);
    }

    public byte[] GetCode(ArbitrumPrecompileExecutionContext context, Address account)
    {
        context.Burn(GasCostOf.ColdSLoad);
        byte[] code = context.WorldState.GetCode(account)!;
        context.Burn(GasCostOf.DataCopy * (ulong)EvmPooledMemory.Div32Ceiling((Int256.UInt256)code.Length));
        return code;
    }
}
