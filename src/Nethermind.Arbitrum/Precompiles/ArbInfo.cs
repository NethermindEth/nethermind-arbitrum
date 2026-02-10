// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbInfo
{
    public static Address Address => ArbosAddresses.ArbInfoAddress;

    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"address\",\"name\":\"account\",\"type\":\"address\"}],\"name\":\"getBalance\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"account\",\"type\":\"address\"}],\"name\":\"getCode\",\"outputs\":[{\"internalType\":\"bytes\",\"name\":\"\",\"type\":\"bytes\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";

    public static Int256.UInt256 GetBalance(ArbitrumPrecompileExecutionContext context, Address account)
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
}
