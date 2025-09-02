using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo(IArbitrumPrecompile precompile) : ICodeInfo
{
    public ReadOnlyMemory<byte> Code => Array.Empty<byte>();
    ReadOnlySpan<byte> ICodeInfo.CodeSpan => Code.Span;
    public IArbitrumPrecompile Precompile { get; } = precompile;

    public bool IsPrecompile => true;
    public bool IsEmpty => false;
}

public class PrecompileSolidityError(byte[] errorData) : Exception
{
    public readonly byte[] ErrorData = errorData;
}
