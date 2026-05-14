// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Precompiles;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo(IArbitrumPrecompile precompile) : CodeInfo(PrecompileStub, PrecompileCode)
{
    private static readonly byte[] PrecompileCode = [(byte)Instruction.INVALID];
    private static readonly IPrecompile PrecompileStub = new ArbitrumPrecompileStub();

    public IArbitrumPrecompile ArbitrumPrecompile { get; } = precompile;

    /// <summary>
    /// Stateless stub that satisfies <see cref="CodeInfo"/>'s <see cref="IPrecompile"/> requirement
    /// so that <see cref="CodeInfo.IsPrecompile"/> returns <c>true</c>.
    /// Actual execution is handled by <see cref="ArbitrumVirtualMachine"/> which dispatches
    /// to <see cref="ArbitrumPrecompile"/> directly.
    /// </summary>
    private sealed class ArbitrumPrecompileStub : IPrecompile
    {
        public long BaseGasCost(IReleaseSpec releaseSpec) => 0;
        public long DataGasCost(ReadOnlyMemory<byte> inputData, IReleaseSpec releaseSpec) => 0;
        public Result<byte[]> Run(ReadOnlyMemory<byte> inputData, IReleaseSpec releaseSpec) =>
            throw new InvalidOperationException("Arbitrum precompiles are executed through ArbitrumVirtualMachine, not through IPrecompile.Run");
    }
}
