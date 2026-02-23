// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Precompiles;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class PrecompileInfo : CodeInfo
{
    private static readonly byte[] PrecompileCode = [(byte)Instruction.INVALID];
    private static readonly IPrecompile PrecompileStub = new ArbitrumPrecompileStub();

    public PrecompileInfo(IArbitrumPrecompile precompile)
        : base(PrecompileStub, version: 0, new ReadOnlyMemory<byte>(PrecompileCode))
    {
        ArbitrumPrecompile = precompile;
    }

    public IArbitrumPrecompile ArbitrumPrecompile { get; }

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
