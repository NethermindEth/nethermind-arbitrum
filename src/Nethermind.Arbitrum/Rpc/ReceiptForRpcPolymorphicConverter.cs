// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Rpc;

/// <summary>
/// Adds polymorphic serialization for ReceiptForRpc so that derived types like
/// ArbitrumReceiptForRpc serialize their own properties (GasUsedForL1, MultiGasUsed).
/// </summary>
/// <remarks>
/// Required because ReceiptForRpc is in upstream Nethermind and cannot be decorated with
/// [JsonDerivedType] attributes for Arbitrum-specific types. Uses the standard
/// <see cref="IJsonTypeInfoResolver"/> modifier approach instead of a custom converter.
/// </remarks>
public static class ReceiptForRpcPolymorphism
{
    public static IJsonTypeInfoResolver CreateResolver() =>
        new DefaultJsonTypeInfoResolver().WithAddedModifier(static typeInfo =>
        {
            if (typeInfo.Type == typeof(ReceiptForRpc))
            {
                typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
                    DerivedTypes = { new JsonDerivedType(typeof(ArbitrumReceiptForRpc)) }
                };
            }
        });
}
