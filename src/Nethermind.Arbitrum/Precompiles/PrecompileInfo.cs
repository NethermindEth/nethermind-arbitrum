

using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo(IArbitrumPrecompile precompile) : ICodeInfo
{
    public ReadOnlyMemory<byte> MachineCode => Array.Empty<byte>();
    public IArbitrumPrecompile? Precompile { get; } = precompile;

    public bool IsPrecompile => true;
    public bool IsEmpty => false;
}
