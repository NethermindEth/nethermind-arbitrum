using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Precompiles.Events;

public static class EventsEncoder
{
    private static LogEntry EncodeEvent(AbiEventDescription eventDescription, Address address, params object[] arguments)
    {
        if (arguments.Length != eventDescription.Inputs.Length)
        {
            throw new AbiException($"Insufficient parameters for {eventDescription.Name}. Expected {eventDescription.Inputs.Length} arguments but got {arguments.Length}");
        }

        // Collect indexed and non-indexed parameters
        List<object> nonIndexedParams = new();
        List<Hash256> topics = new();

        // Add event signature as first topic (unless anonymous)
        if (!eventDescription.Anonymous)
        {
            topics.Add(eventDescription.GetHash());
        }

        for (int i = 0; i < eventDescription.Inputs.Length; i++)
        {
            var parameter = eventDescription.Inputs[i];
            if (parameter.Indexed)
            {
                byte[] encoded = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, new AbiSignature(string.Empty, new[] { parameter.Type }), arguments[i]);
                // Complex types' values are hashed before being added as topics
                topics.Add(parameter.Type.IsDynamic ? Keccak.Compute(encoded) : new Hash256(encoded));
            }
            else
            {
                nonIndexedParams.Add(arguments[i]);
            }
        }

        // Encode non-indexed parameters as data
        byte[] data = [];
        if (nonIndexedParams.Count > 0)
        {
            data = AbiEncoder.Instance.Encode(
                AbiEncodingStyle.None,
                new AbiSignature(string.Empty, eventDescription.Inputs.Where(p => !p.Indexed).Select(p => p.Type).ToArray()),
                nonIndexedParams.ToArray());
        }

        return new LogEntry(address, data, topics.ToArray());
    }

    public static Dictionary<string, object> DecodeEvent(AbiEventDescription eventDescription, LogEntry logEntry)
    {
        var result = new Dictionary<string, object>();

        var objs = AbiEncoder.Instance.Decode(AbiEncodingStyle.None,
            new AbiSignature(string.Empty, eventDescription.Inputs.Where(p => !p.Indexed).Select(p => p.Type).ToArray()), logEntry.Data);

        int shift = objs.Length - eventDescription.Inputs.Length;
        //assume non-indexed params last
        for (int i = 0; i < eventDescription.Inputs.Length; i++)
        {
            var parameter = eventDescription.Inputs[i];
            if (parameter.Indexed)
            {
                var topicShift = eventDescription.Anonymous ? 0 : 1;
                result[parameter.Name] = AbiEncoder.Instance.Decode(AbiEncodingStyle.None,
                    new AbiSignature(string.Empty, new[] { parameter.Type }), logEntry.Topics[i + topicShift].BytesToArray())[0];
            }
            else
            {
                result[parameter.Name] = objs[shift + i];
            }
        }
        return result;
    }

    public static LogEntry BuildLogEntryFromEvent(AbiEventDescription eventDescription, Address address, params object[] arguments)
    {
        return EncodeEvent(eventDescription, address, arguments);
    }

    public static void EmitEvent(ArbitrumPrecompileExecutionContext context, LogEntry eventLog)
    {
        if (context.ReadOnly && context.ArbosState.CurrentArbosVersion >= ArbosVersion.Eleven)
        {
            throw new Exception(EvmExceptionExtensions.GetEvmExceptionDescription(EvmExceptionType.StaticCallViolation));
        }

        ulong emitCost = EventCost(eventLog);
        context.Burn(emitCost);

        context.AddEventLog(eventLog);
    }

    public static ulong EventCost(LogEntry eventLog)
    {
        ulong eventCost = GasCostOf.Log;

        eventCost += GasCostOf.LogTopic * (ulong)eventLog.Topics.Length;

        eventCost += GasCostOf.LogData * (ulong)eventLog.Data.Length;

        return eventCost;
    }
}
