// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core.Crypto;
using System.Buffers.Binary;

namespace Nethermind.Arbitrum.Precompiles.Abi;

/// <summary>
/// ABI Metadata is a fake precompile - not to be called, just supplying data for internal transaction processing
/// </summary>
public class AbiMetadata
{
    public static readonly string Metadata =
        "[{\"inputs\":[],\"name\":\"CallerNotArbOS\",\"type\":\"error\"}," +
        "{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchDataGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReport\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}," +
        "{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchCallDataLength\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchCallDataNonZeros\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchExtraGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReportV2\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}," +
        "{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"l1BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"l2BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"timePassed\",\"type\":\"uint64\"}],\"name\":\"startBlock\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

    public static readonly string StartBlockMethod = "startBlock";
    public static readonly string BatchPostingReport = "batchPostingReport";
    public static readonly string BatchPostingReportV2 = "batchPostingReportV2";


    private static byte[]? _startBlockMethodId;
    private static byte[]? _batchPostingReportMethodId;
    private static byte[]? _batchPostingReportV2MethodId;

    public static byte[] StartBlockMethodId => _startBlockMethodId ??= GetMethodSignature(StartBlockMethod);
    public static byte[] BatchPostingReportMethodId => _batchPostingReportMethodId ??= GetMethodSignature(BatchPostingReport);
    public static byte[] BatchPostingReportV2MethodId => _batchPostingReportV2MethodId ??= GetMethodSignature(BatchPostingReportV2);


    private static readonly JsonSerializerOptions? _jso = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Dictionary<string, object> UnpackInput(string methodName, byte[] rawData)
    {
        if (rawData.Length <= 4)
            throw new ArgumentException("Input data too short");

        AbiParam[] inputs = GetArbAbiParams(Metadata, methodName);
        AbiSignature signature = new(methodName, inputs.Select(i => i.Type).ToArray());

        var arguments = AbiEncoder.Instance.Decode(AbiEncodingStyle.None, signature, rawData[4..]);

        Dictionary<string, object> result = [];
        for (int i = 0; i < inputs.Length; i++)
            result[inputs[i].Name] = arguments[i];

        return result;
    }

    public static byte[] PackInput(string methodName, params object[] arguments)
    {
        AbiSignature signature = GetAbiSignature(Metadata, methodName);
        return AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, signature, arguments);
    }

    public static AbiSignature GetAbiSignature(string abiJson, string methodName)
    {
        AbiParam[] inputs = GetArbAbiParams(abiJson, methodName);
        return new AbiSignature(methodName, inputs.Select(i => i.Type).ToArray());
    }

    private static AbiParam[] GetArbAbiParams(string abiJson, string methodName)
    {
        List<AbiItem>? functions = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso);
        AbiItem target = functions?.FirstOrDefault(f => f.Type == "function" && f.Name == methodName)
            ?? throw new ArgumentException($"Function '{methodName}' not found in ABI");

        return target.Inputs ?? [];
    }

    private static byte[] GetMethodSignature(string methodName)
    {
        AbiParam[] inputs = GetArbAbiParams(Metadata, methodName);
        string signature = $"{methodName}({string.Join(",", inputs.Select(i => i.Type))})";
        return ValueKeccak.Compute(signature).Bytes[..4].ToArray();
    }

    public static Dictionary<string, AbiErrorDescription> GetAllErrorDescriptions(string abiJson)
    {
        if (string.IsNullOrWhiteSpace(abiJson))
            return [];

        List<AbiItem>? abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso);

        return abiItems!
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

        List<AbiItem>? abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso);

        return abiItems!
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

        List<AbiItem>? abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso);

        return abiItems!
            .Where(item => item.Type == "function")
            .Select(item =>
            {
                var funcDesc = new ArbitrumFunctionDescription(
                    new AbiFunctionDescription
                    {
                        Name = item.Name,
                        StateMutability = item.StateMutability ?? throw new ArgumentException($"StateMutability not found in abi for function {item.Name}"),
                        Inputs = item.Inputs?.Select(input => new AbiParameter
                        {
                            Name = input.Name,
                            Type = input.GetResolvedAbiType(),
                        }).ToArray() ?? [],
                        Outputs = item.Outputs?.Select(output => new AbiParameter
                        {
                            Name = output.Name,
                            Type = output.GetResolvedAbiType(),
                        }).ToArray() ?? []
                    });

                // Compute method ID using canonical signature (handles nested tuples correctly)
                string canonicalInputs = string.Join(",", item.Inputs?.Select(i => i.GetCanonicalType()) ?? []);
                string canonicalSignature = $"{item.Name}({canonicalInputs})";
                uint methodId = BinaryPrimitives.ReadUInt32BigEndian(ValueKeccak.Compute(canonicalSignature).Bytes[..4]);

                return (methodId, funcDesc);
            })
            .ToDictionary(item => item.methodId, item => item.funcDesc);
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

        [JsonPropertyName("components")]
        public AbiParam[]? Components { get; set; } // for tuple types (nested structs)

        /// <summary>
        /// Gets the canonical type string for this parameter.
        /// For tuple types, recursively resolves components to produce signatures like "(uint8,uint64)[]".
        /// </summary>
        public string GetCanonicalType()
        {
            string typeStr = Type.ToString();

            // Handle tuple types by recursively resolving components
            // When JSON deserializes "tuple" or "tuple[]", AbiTypeConverter creates an empty AbiTuple
            // which produces "()" or "()[]" - we need to use Components to build the real signature
            if (Components is { Length: > 0 } && (typeStr == "()" || typeStr.StartsWith("()")))
            {
                // Build the tuple signature from components
                string componentTypes = string.Join(",", Components.Select(c => c.GetCanonicalType()));
                string tupleSignature = $"({componentTypes})";

                // Preserve array suffix if present (e.g., "()[]" -> "(...)[]")
                if (typeStr.Length > 2) // "()" is 2 chars
                    return tupleSignature + typeStr[2..];
                return tupleSignature;
            }

            return typeStr;
        }

        /// <summary>
        /// Gets the resolved AbiType, properly building tuple types from components.
        /// For non-tuple types or tuples without components, returns the original Type.
        /// For tuple types with components, recursively builds the correct AbiTuple structure.
        /// </summary>
        public AbiType GetResolvedAbiType()
        {
            string typeStr = Type.ToString();

            // Handle tuple types by recursively resolving components
            if (Components is { Length: > 0 } && (typeStr == "()" || typeStr.StartsWith("()")))
            {
                // Build AbiTuple from components
                AbiType[] componentTypes = Components.Select(c => c.GetResolvedAbiType()).ToArray();
                string[] componentNames = Components.Select(c => c.Name).ToArray();
                AbiType tupleType = new AbiTuple(componentTypes, componentNames);

                // Handle array suffix
                if (typeStr.Length > 2) // "()" is 2 chars, so we have array suffix
                {
                    string suffix = typeStr[2..];
                    if (suffix == "[]")
                        return new AbiArray(tupleType);
                    if (suffix.StartsWith('[') && suffix.EndsWith(']'))
                    {
                        string sizeStr = suffix[1..^1];
                        if (int.TryParse(sizeStr, out int size))
                            return new AbiFixedLengthArray(tupleType, size);
                    }
                }
                return tupleType;
            }

            return Type;
        }
    }
}
