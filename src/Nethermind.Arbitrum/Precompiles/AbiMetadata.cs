using Nethermind.Abi;
using System.Text.Json;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Precompiles
{
    /// <summary>
    /// ABI Metadata is a fake precompile - not to be called, just supplying data for internal transaction processing
    /// </summary>
    public class AbiMetadata
    {
        public static readonly string Metadata =
            "[{\"inputs\":[],\"name\":\"CallerNotArbOS\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchDataGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReport\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"l1BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"l2BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"timePassed\",\"type\":\"uint64\"}],\"name\":\"startBlock\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

        public static readonly string StartBlockMethod = "startBlock";
        private static byte[]? _startBlockMethodId;
        public static byte[] StartBlockMethodId => _startBlockMethodId ??= GetMethodSignature(StartBlockMethod);

        public static Dictionary<string, object> UnpackInput(string methodName, byte[] rawData)
        {
            if (rawData.Length <= 4)
                throw new ArgumentException("Input data too short");

            var inputs = GetArbAbiParams(Metadata, methodName);
            AbiSignature signature = new(methodName, inputs.Select(i => i.Type).ToArray());

            var arguments = new AbiEncoder().Decode(AbiEncodingStyle.None, signature, rawData[4..]);

            var result = new Dictionary<string, object>();
            for (int i = 0; i < inputs.Length; i++)
            {
                result[inputs[i].Name] = arguments[i];
            }

            return result;
        }

        public static byte[] PackInput(string methodName, params object[] arguments)
        {
            AbiSignature signature = GetAbiSignature(Metadata, methodName);
            return new AbiEncoder().Encode(AbiEncodingStyle.IncludeSignature, signature, arguments);
        }

        public static AbiSignature GetAbiSignature(string abiJson, string methodName)
        {
            var inputs = GetArbAbiParams(abiJson, methodName);
            return new AbiSignature(methodName, inputs.Select(i => i.Type).ToArray());
        }

        private static ArbAbiParameter[] GetArbAbiParams(string abiJson, string methodName)
        {
            var jso = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var functions = JsonSerializer.Deserialize<List<ArbAbiFunction>>(abiJson, jso);
            var target = functions?.FirstOrDefault(f => f.Type == "function" && f.Name == methodName);
            if (target == null)
                throw new Exception($"Function '{methodName}' not found in ABI");

            return target.Inputs;
        }

        private static byte[] GetMethodSignature(string methodName)
        {
            var inputs = GetArbAbiParams(Metadata, methodName);
            var signature = string.Format($"{methodName}({string.Join(",", inputs.Select(i => i.Type))})");
            return ValueKeccak.Compute(signature).Bytes[..4].ToArray();
        }

        public static List<AbiErrorDescription> GetAllErrorDescriptions(string abiJson)
        {
            if (string.IsNullOrWhiteSpace(abiJson))
            {
                return new List<AbiErrorDescription>();
            }

            var jso = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, jso);

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
                .ToList();
        }

        public static List<AbiEventDescription> GetAllEventDescriptions(string abiJson)
        {
            if (string.IsNullOrWhiteSpace(abiJson))
            {
                return new List<AbiEventDescription>();
            }

            var jso = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var abiItems = JsonSerializer.Deserialize<List<AbiItem>>(abiJson, jso);

            return abiItems!
                .Where(item => item.Type == "event")
                .Select(item => new AbiEventDescription
                {
                    Name = item.Name,
                    Anonymous = item.Anonymous,
                    Inputs = item.Inputs?.Select(input => new AbiEventParameter
                    {
                        Name = input.Name,
                        Indexed = input.Indexed,
                        Type = input.Type,
                    }).ToArray() ?? []
                })
                .ToList();
        }

        private class ArbAbiFunction
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public ArbAbiParameter[] Inputs { get; set; }
        }

        private class ArbAbiParameter
        {
            public string Name { get; set; }
            public AbiType Type { get; set; }
        }

        private class AbiItem
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool Anonymous { get; set; }
            public AbiInput[] Inputs { get; set; }
        }

        private class AbiInput
        {
            public string Name { get; set; }
            public AbiType Type { get; set; }
            public bool Indexed { get; set; }
        }
    }
}
