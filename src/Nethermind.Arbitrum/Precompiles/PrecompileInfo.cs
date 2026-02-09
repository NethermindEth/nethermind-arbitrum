// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo(IArbitrumPrecompile precompile) : ICodeInfo
{
    private static readonly byte[] PrecompileCode = [(byte)Instruction.INVALID];
    public ReadOnlyMemory<byte> Code => PrecompileCode;
    ReadOnlySpan<byte> ICodeInfo.CodeSpan => Code.Span;
    public IArbitrumPrecompile Precompile { get; } = precompile;

    public bool IsPrecompile => true;
    public bool IsEmpty => false;
}
