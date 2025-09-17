// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
