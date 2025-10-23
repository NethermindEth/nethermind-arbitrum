// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Container;

namespace Nethermind.Arbitrum.Execution.Validators;

public class ArbitrumBlockValidator: IBlockValidator
{
    public bool Validate(BlockHeader header, BlockHeader parent, bool isUncle, [NotNullWhen(false)] out string? error)
    {
        error = null;
        return true;
    }

    public bool ValidateOrphaned(BlockHeader header, [NotNullWhen(false)] out string? error)
    {
        error = null;
        return true;
    }

    public bool ValidateWithdrawals(Block block, out string? error)
    {
        error = null;
        return true;
    }

    public bool ValidateOrphanedBlock(Block block, [NotNullWhen(false)] out string? error)
    {
        error = null;
        return true;
    }

    public bool ValidateSuggestedBlock(Block block, BlockHeader parent, [NotNullWhen(false)] out string? error, bool validateHashes = true)
    {
        error = null;
        return true;
    }

    public bool ValidateProcessedBlock(Block processedBlock, TxReceipt[] receipts, Block suggestedBlock, [NotNullWhen(false)] out string? error)
    {
        error = null;
        return true;
    }

    public bool ValidateBodyAgainstHeader(BlockHeader header, BlockBody toBeValidated, [NotNullWhen(false)] out string? error)
    {
        error = null;
        return true;
    }
}
