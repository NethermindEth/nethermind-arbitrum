// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Arbos;

public enum MessageRunMode : byte
{
    MessageCommitMode,
    MessageGasEstimationMode,
    MessageEthCallMode,
    MessageReplayMode
}
