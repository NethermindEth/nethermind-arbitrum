// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Data.Converters;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Data;

/// Matches Nitro's <c>RootHashOrSlots</c> in <c>go-ethereum/arbitrum_types/txoptions.go</c>.
[JsonConverter(typeof(AccountStateConditionConverter))]
public sealed class AccountStateCondition
{
    public Hash256? RootHash { get; init; }
    public Dictionary<UInt256, Hash256>? SlotValues { get; init; }
}
