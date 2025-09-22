// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Arbitrum-specific execution requests processor that doesn't process execution requests.
/// Arbitrum uses its own L2→L1 withdrawal system instead of Ethereum's execution request system.
/// </summary>
public class ArbitrumExecutionRequestsProcessor : IExecutionRequestsProcessor
{
    /// <summary>
    /// Arbitrum doesn't use Ethereum's execution request system (EIP-6110/7002/7251) for withdrawals.
    /// Instead, it uses its own L2→L1 messaging system through ArbSys precompile.
    /// This method is a no-op to prevent attempts to read from non-existent withdrawal contracts.
    /// </summary>
    public void ProcessExecutionRequests(Block block, IWorldState state, TxReceipt[] receipts, IReleaseSpec spec)
    {
        // Arbitrum doesn't process execution requests - it uses its own withdrawal system
        // Setting ExecutionRequests to null indicates no execution requests are present
        block.ExecutionRequests = null;
        block.Header.RequestsHash = null;
    }
}
