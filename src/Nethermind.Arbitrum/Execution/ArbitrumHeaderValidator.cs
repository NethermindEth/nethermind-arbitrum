// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Consensus;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Messages;
using Nethermind.Core.Specs;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumHeaderValidator(IBlockTree? blockTree, ISealValidator? sealValidator, ISpecProvider? specProvider, ILogManager? logManager) : HeaderValidator(blockTree, sealValidator, specProvider, logManager)
{
    protected override bool Validate1559(BlockHeader header, BlockHeader parent, IReleaseSpec spec, ref string? error)
    {
        return true;
    }

    protected override bool ValidateTimestamp(BlockHeader header, BlockHeader parent, ref string? error)
    {
        bool timestampMoreThanAtParent = header.Timestamp >= parent.Timestamp;
        if (!timestampMoreThanAtParent)
        {
            error = BlockErrorMessages.InvalidTimestamp;
            if (_logger.IsWarn) _logger.Warn($"Invalid block header ({header.Hash}) - timestamp before parent");
        }
        return timestampMoreThanAtParent;
    }
}
