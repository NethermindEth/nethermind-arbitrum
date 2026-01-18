using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo(IArbitrumPrecompile precompile) : ICodeInfo
{
    private static readonly byte[] PrecompileCode = [(byte)Instruction.INVALID];
    ReadOnlySpan<byte> ICodeInfo.CodeSpan => Code.Span;
    public ReadOnlyMemory<byte> Code => PrecompileCode;
    public bool IsEmpty => false;

    public bool IsPrecompile => true;
    public IArbitrumPrecompile Precompile { get; } = precompile;
}
