// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Provides the initial message for Arbitrum genesis initialization.
/// This can come from L1 parent chain or be constructed from chain config.
/// </summary>
public interface IInitMessageProvider
{
    ParsedInitMessage GetInitMessage();
}
