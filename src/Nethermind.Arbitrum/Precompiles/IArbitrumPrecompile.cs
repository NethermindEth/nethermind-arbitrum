// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Evm.State;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Precompiles
{
    // Input data passed to precompile is the calldata excluding method ID
    public delegate byte[] PrecompileHandler(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> input);

    /// <summary>
    /// Describes how a precompile call should be gas-accounted.
    /// Mirrors the logic of Nitro's FreeAccessPrecompile wrapper (wrapper.go).
    /// </summary>
    public readonly struct GasConsumptionPolicy
    {
        /// <summary>
        /// When true the caller pays nothing - context.Free is set before execution
        /// </summary>
        public bool IsFree { get; init; }

        /// <summary>
        /// For callers that are not free: the gas cost of the membership check itself.
        /// Zero means normal gas accounting applies.
        /// </summary>
        public MultiGas CheckCost { get; init; }

        public static readonly GasConsumptionPolicy Default = default;
    }

    public interface IArbitrumPrecompile
    {
        /// <summary>
        /// Address of precompile
        /// </summary>
        static abstract Address Address { get; }

        /// <summary>
        /// Gets a value indicating whether this precompile has owner privileges (default to false)
        /// </summary>
        bool IsOwner => false;

        /// <summary>
        /// Gets a value indicating whether this precompile has debug privileges (default to false)
        /// </summary>
        bool IsDebug => false;

        /// <summary>
        /// Determines the gas consumption policy for this call. Called in RunPrecompile before
        /// execution. The default returns <see cref="GasConsumptionPolicy.Default"/> (normal
        /// charging). Precompiles that grant free access to certain callers (e.g.
        /// ArbFilteredTransactionsManager for transaction filterers) override this to return an
        /// appropriate policy, matching Nitro's FreeAccessPrecompile wrapper semantics.
        /// </summary>
        GasConsumptionPolicy ShouldConsumeGas(IWorldState worldState, Address caller)
            => GasConsumptionPolicy.Default;

        /// <summary>
        /// The version of ArbOS from which this precompile is enabled (default to 0)
        /// </summary>
        static virtual ulong AvailableFromArbosVersion => ArbosVersion.Zero;

        /// <summary>
        /// Abi characteristics for all precompile functions
        /// </summary>
        static abstract IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }

        /// <summary>
        /// Mapping of method id to implementation of all precompile functions
        /// </summary>
        static abstract FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }
    }

    public interface IArbitrumPrecompile<TPrecompileTypeInstance> : IArbitrumPrecompile;
}
