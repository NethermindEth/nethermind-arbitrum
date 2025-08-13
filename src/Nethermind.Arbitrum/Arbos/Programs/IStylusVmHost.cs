// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

internal interface IStylusVmHost
{
    (byte[] ret, ulong cost, EvmExceptionType? err) DoCall(
        Address acting, ExecutionType kind, Address to, ReadOnlySpan<byte> input,
        ulong gasLeftReportedByRust, ulong gasRequestedByRust, in UInt256 value);

    (Address created, byte[] returnData, ulong cost, EvmExceptionType? err) DoCreate(
        ReadOnlySpan<byte> initCode, in UInt256 endowment, UInt256? salt, ulong gas);
}
