// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Text.Json;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;

namespace Nethermind.Arbitrum.Test.Infrastructure;

/// <summary>
/// Test-only ABI JSON parsing helpers used by precompile golden tests to assert the
/// function/event/error surface of package-sourced ABI strings. Production code consumes
/// the package's pre-parsed descriptors directly via SolgenExtensions; this helper exists
/// purely to let golden tests verify the package's JSON representation against expected shape.
/// </summary>
public static class PrecompileTestAbiHelpers
{
    private static readonly JsonSerializerOptions _jso = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Dictionary<string, AbiErrorDescription> GetAllErrorDescriptions(string abiJson)
    {
        if (string.IsNullOrWhiteSpace(abiJson))
            return [];

        List<AbiItem> abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso) ?? [];

        return abiItems
            .Where(item => item.Type == "error")
            .Select(item => new AbiErrorDescription
            {
                Name = item.Name,
                Inputs = item.Inputs?.Select(input => new AbiParameter
                {
                    Name = input.Name,
                    Type = input.Type,
                }).ToArray() ?? []
            })
            .ToDictionary(item => item.Name);
    }

    public static Dictionary<string, AbiEventDescription> GetAllEventDescriptions(string abiJson)
    {
        if (string.IsNullOrWhiteSpace(abiJson))
            return [];

        List<AbiItem> abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso) ?? [];

        return abiItems
            .Where(item => item.Type == "event")
            .Select(item => new AbiEventDescription
            {
                Name = item.Name,
                Anonymous = item.Anonymous ?? false,
                Inputs = item.Inputs?.Select(input => new AbiEventParameter
                {
                    Name = input.Name,
                    Indexed = input.Indexed ?? false,
                    Type = input.Type,
                }).ToArray() ?? []
            })
            .ToDictionary(item => item.Name);
    }

    public static Dictionary<uint, ArbitrumFunctionDescription> GetAllFunctionDescriptions(string abiJson)
    {
        if (string.IsNullOrWhiteSpace(abiJson))
            return [];

        List<AbiItem> abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso) ?? [];

        return abiItems
            .Where(item => item.Type == "function")
            .Select(item => new ArbitrumFunctionDescription(
                new AbiFunctionDescription
                {
                    Name = item.Name,
                    StateMutability = item.StateMutability ?? throw new ArgumentException($"StateMutability not found in abi for function {item.Name}"),
                    Inputs = item.Inputs?.Select(input => new AbiParameter
                    {
                        Name = input.Name,
                        Type = input.Type,
                    }).ToArray() ?? [],
                    Outputs = item.Outputs?.Select(output => new AbiParameter
                    {
                        Name = output.Name,
                        Type = output.Type,
                    }).ToArray() ?? []
                }))
            .ToDictionary(item => BinaryPrimitives.ReadUInt32BigEndian(item.AbiFunctionDescription.GetHash().Bytes[0..4]));
    }

    private class AbiItem
    {
        public required string Name { get; set; } // for errors, events, functions
        public required string Type { get; set; } // for errors, events, functions
        public bool? Anonymous { get; set; } // for events
        public AbiParam[]? Inputs { get; set; } // for errors, events, functions
        public AbiParam[]? Outputs { get; set; } // for functions only
        public StateMutability? StateMutability { get; set; } // for functions only
    }

    private class AbiParam
    {
        public required string Name { get; set; }
        public required AbiType Type { get; set; }
        public bool? Indexed { get; set; } // for event parameters
    }
}
