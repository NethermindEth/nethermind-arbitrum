// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbBLS precompile provides a registry of BLS public keys for accounts.
/// This precompile is disabled in Arbitrum and serves as a placeholder.
/// Available from ArbOS version 41.
/// </summary>
public static class ArbBls
{
    public static Address Address => ArbosAddresses.ArbBLSAddress;

    // ABI definition for the disabled precompile
    // Empty because no functions are implemented
    public const string Abi = "[]";
}
