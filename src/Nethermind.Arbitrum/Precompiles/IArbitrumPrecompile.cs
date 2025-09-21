using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles
{
    public interface IArbitrumPrecompile
    {
        static abstract Address Address { get; }

        /// <summary>
        /// Gets a value indicating whether this precompile has owner privileges
        /// </summary>
        bool IsOwner => false;

        static abstract string Abi { get; }

        static virtual ulong AvailableFromArbosVersion => ArbosVersion.Zero;

        static abstract IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }

        static virtual void CustomizeFunctionDescriptionsWithArbosVersion(IReadOnlyDictionary<uint, ArbitrumFunctionDescription> _)
        {}

        byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData);
    }

    public interface IArbitrumPrecompile<TPrecompileTypeInstance> : IArbitrumPrecompile
    {
        static TPrecompileTypeInstance Instance { get; }
    }
}
