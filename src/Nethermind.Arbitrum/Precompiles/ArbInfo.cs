// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbInfo
{
    public static Address Address => ArbosAddresses.ArbInfoAddress;

    public static string Abi => Solgen.ArbInfo.Abi;

    public static Int256.UInt256 GetBalance(ArbitrumPrecompileExecutionContext context, Address account)
    {
        context.Burn(GasCostOf.BalanceEip1884);
        return context.WorldState.GetBalance(account);
    }

    public static byte[] GetCode(ArbitrumPrecompileExecutionContext context, Address account)
    {
        context.Burn(GasCostOf.ColdSLoad);
        byte[] code = context.WorldState.GetCode(account)!;
        context.Burn(GasCostOf.Memory * Math.Utils.Div32Ceiling((ulong)code.Length));
        return code;
    }
}
