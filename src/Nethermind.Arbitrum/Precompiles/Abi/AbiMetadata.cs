using Nethermind.Abi;
using System.Text.Json;
using Nethermind.Core.Crypto;
using System.Buffers.Binary;

namespace Nethermind.Arbitrum.Precompiles.Abi;

/// <summary>
/// ABI Metadata is a fake precompile - not to be called, just supplying data for internal transaction processing
/// </summary>
public class AbiMetadata
{
    public static readonly string Metadata =
        "[{\"inputs\":[],\"name\":\"CallerNotArbOS\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchDataGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReport\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"l1BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"l2BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"timePassed\",\"type\":\"uint64\"}],\"name\":\"startBlock\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

    public static readonly string StartBlockMethod = "startBlock";
    public static readonly string BatchPostingReport = "batchPostingReport";

    private static byte[]? _startBlockMethodId;
    private static byte[]? _batchPostingReportMethodId;
    public static byte[] StartBlockMethodId => _startBlockMethodId ??= GetMethodSignature(StartBlockMethod);
    public static byte[] BatchPostingReportMethodId => _batchPostingReportMethodId ??= GetMethodSignature(BatchPostingReport);

    private static readonly JsonSerializerOptions _jso = new()
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
        List<AbiItem> functions = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, _jso);
        AbiItem target = (functions?.FirstOrDefault(f => f.Type == "function" && f.Name == methodName))
            ?? throw new ArgumentException($"Function '{methodName}' not found in ABI");

        return target.Inputs;
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
        public string Name { get; set; } // for errors, events, functions
        public string Type { get; set; } // for errors, events, functions
        public bool? Anonymous { get; set; } // for events
        public AbiParam[] Inputs { get; set; } // for errors, events, functions
        public AbiParam[] Outputs { get; set; } // for functions
        public StateMutability? StateMutability { get; set; } // for functions
    }

    private class AbiParam
    {
        public string Name { get; set; }
        public AbiType Type { get; set; }
        public bool? Indexed { get; set; } // for event parameters
    }
}
