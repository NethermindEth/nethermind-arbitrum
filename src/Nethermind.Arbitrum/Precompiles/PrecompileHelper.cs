using System.Buffers.Binary;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Precompiles;

public static class PrecompileHelper
{
    public static uint GetMethodId(string methodSignature)
    {
        Hash256 hash = Keccak.Compute(methodSignature);
        ReadOnlySpan<byte> hashBytes = hash.Bytes;
        return BinaryPrimitives.ReadUInt32BigEndian(hashBytes[..4]);
    }

    public static bool TryCheckMethodVisibility(IArbitrumPrecompile precompile, uint methodId, ArbitrumPrecompileExecutionContext context, ILogger logger, out bool shouldRevert)
        => precompile switch
        {
            _ when precompile is ArbInfoParser _ => CheckMethodVisibility<ArbInfoParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbRetryableTxParser _ => CheckMethodVisibility<ArbRetryableTxParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is OwnerWrapper<ArbOwnerParser> _ => CheckMethodVisibility<OwnerWrapper<ArbOwnerParser>>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbSysParser _ => CheckMethodVisibility<ArbSysParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbAddressTableParser _ => CheckMethodVisibility<ArbAddressTableParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbWasmParser _ => CheckMethodVisibility<ArbWasmParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbGasInfoParser _ => CheckMethodVisibility<ArbGasInfoParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbAggregatorParser _ => CheckMethodVisibility<ArbAggregatorParser>(methodId, context, logger, out shouldRevert),
            _ when precompile is ArbOwnerParser _ => throw new ArgumentException("ArbOwnerParser should only be called through OwnerWrapper<T>"),
            _ => throw new ArgumentException($"CheckMethodVisibility is not registered for precompile: {precompile.GetType()}")
        };

    private static bool CheckMethodVisibility<T>(uint methodId, ArbitrumPrecompileExecutionContext context, ILogger logger, out bool shouldRevert)
        where T : IArbitrumPrecompile<T>
    {
        ulong currentVersion = context.FreeArbosState.CurrentArbosVersion;

        // The precompile isn't active yet, so treat this call as if it were to a contract that doesn't exist
        if (currentVersion < T.AvailableFromArbosVersion)
            return shouldRevert = false;

        // If any of the following checks fail, we should revert
        shouldRevert = true;

        // Method does not exist
        if (!T.PrecompileFunctions.TryGetValue(methodId, out ArbitrumFunctionDescription? abiFunction))
            return false;

        // Method hasn't been activated yet or has been deactivated
        if (currentVersion < abiFunction.ArbOSVersion || (abiFunction.MaxArbOSVersion.HasValue && currentVersion > abiFunction.MaxArbOSVersion.Value))
            return false;

        // Should not access precompile superpowers when not acting as the precompile
        if (abiFunction.AbiFunctionDescription.StateMutability >= StateMutability.View && T.Address != context.ExecutingAccount)
            return false;

        // Tried to write to global state in read-only mode
        if (abiFunction.AbiFunctionDescription.StateMutability >= StateMutability.NonPayable && context.ReadOnly)
            return false;

        // Tried to pay something that's non-payable
        if (!abiFunction.AbiFunctionDescription.Payable && context.Value != 0)
            return false;

        // Impure methods may need the ArbOS state, so open & update the call context now
        if (abiFunction.AbiFunctionDescription.StateMutability != StateMutability.Pure)
        {
            // Arbos opening could throw if there is not enough gas
            context.ArbosState = ArbosState.OpenArbosState(context.WorldState, context, logger);
        }

        return true;
    }
}
