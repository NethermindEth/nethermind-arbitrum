// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Abi;
using System.Collections.Concurrent;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public static class ArbitrumPrecompileAbiDecoder
{
    // Cache signatures to avoid recreating them
    private static readonly ConcurrentDictionary<string, AbiSignature> SignatureCache = new();

    public static object[] Decode(string methodName, ReadOnlySpan<byte> inputData, params AbiType[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return Array.Empty<object>();
        }

        AbiSignature signature = GetOrCreateSignature(methodName, parameterTypes);

        return AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            signature,
            inputData.ToArray()
        );
    }

    private static AbiSignature GetOrCreateSignature(string methodName, AbiType[] parameterTypes)
    {
        // Create a unique key for caching
        string key = $"{methodName}({string.Join(",", parameterTypes.Select(t => t.Name))})";

        return SignatureCache.GetOrAdd(key, _ => new AbiSignature(methodName, parameterTypes));
    }
}
