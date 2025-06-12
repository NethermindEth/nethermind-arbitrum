using Nethermind.Abi;
using System.Text.Json;

namespace Nethermind.Arbitrum.Precompiles
{
    /// <summary>
    /// ABI Metadata is a fake precompile - not to be called, just supplying data for internal transaction processing
    /// </summary>
    public class AbiMetadata
    {
        public static AbiEventDescription? GetEventDescription(string abiJson, string eventName)
        {
            if (string.IsNullOrWhiteSpace(abiJson) || string.IsNullOrWhiteSpace(eventName))
            {
                return null;
            }

            var allEvents = GetAllEventDescriptions(abiJson);
            return allEvents.FirstOrDefault(e => e.Name == eventName);
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
