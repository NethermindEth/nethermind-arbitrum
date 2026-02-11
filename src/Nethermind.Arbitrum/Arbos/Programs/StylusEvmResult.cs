// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Arbos.Programs;

public struct StylusEvmResult(byte[] returnData, ulong cost, EvmExceptionType err, Address? createdAddress = null)
{
    public readonly byte[] ReturnData = returnData;
    public readonly ulong GasCost = cost;
    public readonly EvmExceptionType EvmException = err;
    public readonly Address? CreatedAddress = createdAddress;
}
