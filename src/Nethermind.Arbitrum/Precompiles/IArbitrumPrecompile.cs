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
        public abstract static Address Address { get; }

        /// <summary>
        /// The version of ArbOS from which this precompile is enabled (default to 0)
        /// </summary>
        public virtual static ulong AvailableFromArbosVersion => ArbosVersion.Zero;

        /// <summary>
        /// Abi characteristics for all precompile functions
        /// </summary>
        public abstract static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }

        /// <summary>
        /// Mapping of method id to implementation of all precompile functions
        /// </summary>
        public abstract static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }
        /// <summary>
        /// Gets a value indicating whether this precompile has debug privileges (default to false)
        /// </summary>
        public bool IsDebug => false;

        /// <summary>
        /// Gets a value indicating whether this precompile has owner privileges (default to false)
        /// </summary>
        public bool IsOwner => false;
    }

    public interface IArbitrumPrecompile<TPrecompileTypeInstance> : IArbitrumPrecompile;
}
