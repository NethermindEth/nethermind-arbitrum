using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
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

    public static bool TryCheckMethodVisibility(IArbitrumPrecompile precompile, ArbitrumPrecompileExecutionContext context, ILogger logger, ref ReadOnlySpan<byte> calldata, out bool shouldRevert, [NotNullWhen(true)] out PrecompileHandler? methodToExecute)
        => precompile switch
        {
            _ when precompile is ArbInfoParser _ => CheckMethodVisibility<ArbInfoParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbRetryableTxParser _ => CheckMethodVisibility<ArbRetryableTxParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbOwnerParser _ => CheckMethodVisibility<ArbOwnerParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbOwnerPublicParser _ => CheckMethodVisibility<ArbOwnerPublicParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbSysParser _ => CheckMethodVisibility<ArbSysParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbAddressTableParser _ => CheckMethodVisibility<ArbAddressTableParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbWasmParser _ => CheckMethodVisibility<ArbWasmParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbGasInfoParser _ => CheckMethodVisibility<ArbGasInfoParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbAggregatorParser _ => CheckMethodVisibility<ArbAggregatorParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbActsParser _ => CheckMethodVisibility<ArbActsParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbFunctionTableParser _ => CheckMethodVisibility<ArbFunctionTableParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbTestParser _ => CheckMethodVisibility<ArbTestParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ when precompile is ArbStatisticsParser _ => CheckMethodVisibility<ArbStatisticsParser>(context, logger, ref calldata, out shouldRevert, out methodToExecute),
            _ => throw new ArgumentException($"CheckMethodVisibility is not registered for precompile: {precompile.GetType()}")
        };

    private static bool CheckMethodVisibility<T>(ArbitrumPrecompileExecutionContext context, ILogger logger, ref ReadOnlySpan<byte> calldata, out bool shouldRevert, [NotNullWhen(true)] out PrecompileHandler? methodToExecute)
        where T : IArbitrumPrecompile<T>
    {
        methodToExecute = null!; // Safe, will not be called if has not been found

        ulong currentVersion = context.FreeArbosState.CurrentArbosVersion;

        // The precompile isn't active yet, so treat this call as if it were to a contract that doesn't exist
        if (currentVersion < T.AvailableFromArbosVersion)
            return shouldRevert = false;

        // If any of the following checks fail, we should revert
        shouldRevert = true;

        // Should never fail as we already checked that calldata is at least 4 bytes long
        if (!ArbitrumBinaryReader.TryReadUInt32BigEndian(ref calldata, out uint methodId))
        {
            logger.Error($"Should never fail: calldata is not at least 4 bytes long: {calldata.ToHexString()}");
            return false;
        }

        // Method does not exist
        if (!T.PrecompileFunctionDescription.TryGetValue(methodId, out ArbitrumFunctionDescription? abiFunction))
            return false;

        // Should never fail as we checked just above the method exists in the ABI
        if (!T.PrecompileImplementation.TryGetValue(methodId, out methodToExecute))
        {
            logger.Error($"Should never fail: missing implementation for method {methodId} in precompile {typeof(T).Name}");
            return false;
        }

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

        context.IsMethodCalledPure = abiFunction.AbiFunctionDescription.StateMutability == StateMutability.Pure;

        return true;
    }
}
