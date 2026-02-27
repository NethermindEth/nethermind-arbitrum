// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// Debug RPC module for Arbitrum system testing with MemDb mode.
/// Provides state reinitialization for test isolation.
/// </summary>
[RpcModule(ModuleType.Debug)]
public interface IArbitrumDebugRpcModule : IRpcModule
{
    /// <summary>
    /// Reinitializes the execution engine for a new test with different configuration.
    /// Clears all state, rebuilds chainspec, and re-initializes genesis.
    /// Only available in MemDb mode (DiagnosticMode.MemDb).
    /// </summary>
    /// <param name="arbosVersion">ArbOS version number (e.g., 51 for latest)</param>
    /// <param name="accountsJson">JSON object with account addresses and balances</param>
    /// <param name="maxCodeSize">Optional max code size in hex (default: 0x6000)</param>
    /// <returns>True if reinitialization succeeded</returns>
    [JsonRpcMethod(
        Description = "Reinitializes execution state for a new test. Only available in MemDb mode.",
        IsImplemented = true,
        IsSharable = false)]
    Task<ResultWrapper<bool>> debug_reinitialize(
        int arbosVersion,
        string accountsJson,
        string? maxCodeSize = null);
}
