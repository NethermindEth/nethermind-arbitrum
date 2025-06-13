

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Precompiles.Events;

public class EventsEncoder
{
    private static readonly EventsEncoder Instance = new();

    public LogEntry EncodeEvent(AbiEventDescription eventDescription, Address address, params object[] arguments)
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
        byte[] data = Array.Empty<byte>();
        if (nonIndexedParams.Count > 0)
        {
            data = AbiEncoder.Instance.Encode(
                AbiEncodingStyle.None,
                new AbiSignature(string.Empty, eventDescription.Inputs.Where(p => !p.Indexed).Select(p => p.Type).ToArray()),
                nonIndexedParams.ToArray());
        }

        return new LogEntry(address, data, topics.ToArray());
    }

    public static LogEntry BuildLogEntryFromEvent(AbiEventDescription eventDescription, Address address, params object[] arguments)
    {
        return Instance.EncodeEvent(eventDescription, address, arguments);
    }

    public static LogEntry EmitEvent(Context context, ArbVirtualMachine vm, LogEntry eventLog)
    {
        ulong arbosVersion = ArbosState.ArbOSVersion(vm.WorldState);
        if (context.ReadOnly && arbosVersion >= ArbosVersion.Eleven) {
            throw new Exception(EvmExceptionExtensions.GetEvmExceptionDescription(EvmExceptionType.StaticCallViolation));
        }

        ulong emitCost = EventCost(eventLog);
        context.Burn(emitCost);

        return eventLog;
    }

    public static ulong EventCost(LogEntry eventLog)
    {
        ulong eventCost = GasCostOf.Log;

        eventCost += GasCostOf.LogTopic * (ulong)eventLog.Topics.Length;

        eventCost += GasCostOf.LogData * (ulong)eventLog.Data.Length;

        return eventCost;
    }
}
