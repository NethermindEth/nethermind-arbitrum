// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
