
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles
{
    public interface IArbitrumPrecompile
    {
        static virtual Address Address => Address.Zero;

        (byte[], bool) RunAdvanced(ArbitrumPrecompileExecutionContext context, ArbVirtualMachine evm, ReadOnlyMemory<byte> inputData);
    }


    public interface IArbitrumPrecompile<TPrecompileTypeInstance> : IArbitrumPrecompile
    {
        static TPrecompileTypeInstance Instance { get; }
    }
}
