// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Data;

public record DelayedMessage(L1IncomingMessage Message, ulong MessageIndex);
