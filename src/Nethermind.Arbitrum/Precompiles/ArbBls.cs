// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbBLS precompile provides a registry of BLS public keys for accounts.
/// This precompile is disabled in Arbitrum and serves as a placeholder.
/// </summary>
public static class ArbBls
{
    public const string Abi = Solgen.ArbBLS.Abi;

    public static Address Address => ArbosAddresses.ArbBLSAddress;
}
