// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// Represents an empty RPC response. Serializes to <c>{}</c> in JSON.
/// Used for methods where Nitro expects an empty struct response.
/// </summary>
public readonly struct EmptyResponse;
