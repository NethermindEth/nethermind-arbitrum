// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json.Serialization;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// Represents an Arbitrum message index. Serialized as a raw number
/// </summary>
[JsonConverter(typeof(MessageIndexJsonConverter))]
public readonly struct MessageIndex(ulong value)
{
    public ulong Value { get; } = value;

    public static implicit operator ulong(MessageIndex index) => index.Value;
    public static implicit operator MessageIndex(ulong value) => new(value);

    public override string ToString() => Value.ToString();
}
