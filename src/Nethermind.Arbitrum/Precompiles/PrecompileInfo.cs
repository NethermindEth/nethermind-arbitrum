using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo(IArbitrumPrecompile precompile) : ICodeInfo
{
    public ReadOnlyMemory<byte> Code => ReadOnlyMemory<byte>.Empty;
    ReadOnlySpan<byte> ICodeInfo.CodeSpan => Code.Span;
    public IArbitrumPrecompile Precompile { get; } = precompile;

    public bool IsPrecompile => true;
    public bool IsEmpty => false;
}
