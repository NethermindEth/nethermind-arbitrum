using Nethermind.Core;
namespace Nethermind.Arbitrum.Precompiles
{
    public interface IArbitrumPrecompile
    {
        static virtual Address Address => Address.Zero;

        /// <summary>
        /// Gets a value indicating whether this precompile has owner privileges
        /// </summary>
        bool IsOwner => false;

        byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData);
    }


    public interface IArbitrumPrecompile<TPrecompileTypeInstance> : IArbitrumPrecompile
    {
        static TPrecompileTypeInstance Instance { get; }
    }
}
