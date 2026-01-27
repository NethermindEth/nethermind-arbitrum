// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
