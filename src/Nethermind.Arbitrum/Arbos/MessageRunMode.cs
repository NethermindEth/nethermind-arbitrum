// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos;

public enum MessageRunMode : byte
{
    MessageCommitMode,
    MessageGasEstimationMode,
    MessageEthCallMode,
    MessageReplayMode
}
