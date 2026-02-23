// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// Represents an empty RPC response. Serializes to <c>{}</c> in JSON.
/// Used for methods where Nitro expects an empty struct response.
/// </summary>
public readonly struct EmptyResponse;
