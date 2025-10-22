using System.Collections.Frozen;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles
{
    // Input data passed to precompile is the calldata excluding method ID
    public delegate byte[] PrecompileHandler(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> input);

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

    public interface IArbitrumPrecompile<TPrecompileTypeInstance> : IArbitrumPrecompile
    {
        static TPrecompileTypeInstance Instance { get; }
    }
}
