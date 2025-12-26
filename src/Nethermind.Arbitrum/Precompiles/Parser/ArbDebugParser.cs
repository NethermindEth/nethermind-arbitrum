using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbDebugParser : IArbitrumPrecompile<ArbDebugParser>
{
    public static readonly ArbDebugParser Instance = new();

    public bool IsDebug => true;

    public static Address Address { get; } = ArbDebug.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbDebug.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _becomeChainOwnerId = PrecompileHelper.GetMethodId("becomeChainOwner()");
    private static readonly uint _eventsId = PrecompileHelper.GetMethodId("events(bool,bytes32)");
    private static readonly uint _eventsViewId = PrecompileHelper.GetMethodId("eventsView()");
    private static readonly uint _customRevertId = PrecompileHelper.GetMethodId("customRevert(uint64)");
    private static readonly uint _panicId = PrecompileHelper.GetMethodId("panic()");
    private static readonly uint _legacyErrorId = PrecompileHelper.GetMethodId("legacyError()");
    private static readonly uint _overwriteContractCodeId = PrecompileHelper.GetMethodId("overwriteContractCode(address,bytes)");

    static ArbDebugParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _becomeChainOwnerId, BecomeChainOwner },
            { _eventsId, Events },
            { _eventsViewId, EventsView },
            { _customRevertId, CustomRevert },
            { _panicId, Panic },
            { _legacyErrorId, LegacyError },
            { _overwriteContractCodeId, OverwriteContractCode },
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[_panicId].ArbOSVersion = ArbosVersion.Stylus;
    }

    private static byte[] BecomeChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbDebug.BecomeChainOwner(context);
        return [];
    }

    private static byte[] OverwriteContractCode(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_overwriteContractCodeId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address addr = (Address)decoded[0];
        byte[] code = (byte[])decoded[1];

        byte[] oldCode = ArbDebug.OverwriteContractCode(context, addr, code);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            oldCode
        );
    }

    private static byte[] Events(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_eventsId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 paid = context.Value;
        bool flag = (bool)decoded[0];
        Hash256 value = new((byte[])decoded[1]);

        (Address caller, UInt256 paid) result = ArbDebug.Events(context, paid, flag, value);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result.caller, result.paid
        );
    }

    private static byte[] EventsView(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbDebug.EventsView(context);
        return [];
    }

    private static byte[] CustomRevert(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_customRevertId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ArbDebug.CustomRevert(context, (ulong)decoded[0]);
        return [];
    }

    private static byte[] Panic(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbDebug.Panic(context);
        return [];
    }

    private static byte[] LegacyError(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbDebug.LegacyError(context);
        return [];
    }
}
