// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Test;

namespace Nethermind.Arbitrum.Test;

/// <summary>
/// Test utilities for Arbitrum precompile checkers.
/// </summary>
public static class TestArbitrumPrecompiles
{
    /// <summary>
    /// Shared ArbitrumPrecompileChecker instance for use in tests.
    /// </summary>
    public static readonly IPrecompileChecker Arbitrum = new ArbitrumPrecompileChecker();

    /// <summary>
    /// Shared CompositePrecompileChecker instance that combines Ethereum and Arbitrum precompiles.
    /// </summary>
    public static readonly IPrecompileChecker EthereumAndArbitrum = new CompositePrecompileChecker(
        TestPrecompiles.Ethereum,
        Arbitrum
    );
}
