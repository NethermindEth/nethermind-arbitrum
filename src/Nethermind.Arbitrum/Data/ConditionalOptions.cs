// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json.Serialization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Data;

/// EIP-7796 conditional transaction options.
/// Matches Nitro's <c>ConditionalOptions</c> in <c>go-ethereum/arbitrum_types/txoptions.go</c>.
public sealed class ConditionalOptions
{
    [JsonPropertyName("knownAccounts")]
    public Dictionary<Address, AccountStateCondition>? KnownAccounts { get; init; }

    [JsonPropertyName("blockNumberMin")]
    public ulong? BlockNumberMin { get; init; }

    [JsonPropertyName("blockNumberMax")]
    public ulong? BlockNumberMax { get; init; }

    [JsonPropertyName("timestampMin")]
    public ulong? TimestampMin { get; init; }

    [JsonPropertyName("timestampMax")]
    public ulong? TimestampMax { get; init; }

    public Result Check(ulong l1BlockNumber, ulong l2Timestamp, IStateReader stateReader, BlockHeader header)
    {
        if (BlockNumberMin is not null && l1BlockNumber < BlockNumberMin.Value)
            return Result.Fail("BlockNumberMin condition not met");

        if (BlockNumberMax is not null && l1BlockNumber > BlockNumberMax.Value)
            return Result.Fail("BlockNumberMax condition not met");

        if (TimestampMin is not null && l2Timestamp < TimestampMin.Value)
            return Result.Fail("TimestampMin condition not met");

        if (TimestampMax is not null && l2Timestamp > TimestampMax.Value)
            return Result.Fail("TimestampMax condition not met");

        if (KnownAccounts is not null)
        {
            // Allocated outside inner loop to satisfy CA2014 (no stackalloc in loops)
            Span<byte> padded = stackalloc byte[Hash256.Size];

            foreach ((Address address, AccountStateCondition condition) in KnownAccounts)
            {
                if (condition.RootHash is not null)
                {
                    // Nitro returns common.Hash{} (all zeros) for nonexistent accounts.
                    // Nethermind's default AccountStruct has StorageRoot = EmptyTreeHash,
                    // so we must use zero when TryGetAccount returns false.
                    ValueHash256 storageRoot = stateReader.TryGetAccount(header, address, out AccountStruct account)
                        ? account.StorageRoot
                        : default;

                    if (storageRoot != condition.RootHash.ValueHash256)
                        return Result.Fail("Storage root hash condition not met");
                }
                else if (condition.SlotValues is { Count: > 0 })
                {
                    foreach ((UInt256 slot, Hash256 expectedValue) in condition.SlotValues)
                    {
                        ReadOnlySpan<byte> stored = stateReader.GetStorage(header, address, in slot);

                        padded.Clear();
                        stored.CopyTo(padded[(Hash256.Size - stored.Length)..]);

                        if (!Bytes.AreEqual(padded, expectedValue.Bytes))
                            return Result.Fail("Storage slot value condition not met");
                    }
                }
            }
        }

        return Result.Success;
    }
}
