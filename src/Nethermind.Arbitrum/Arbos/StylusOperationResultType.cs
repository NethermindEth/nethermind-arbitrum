// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Arbos;

public enum StylusOperationResultType : byte
{
    Success = 0,
    InvalidByteCode,
    ExecutionRevert,
    ExecutionFailure,
    ExecutionOutOfInk,
    ExecutionOutOfStack,
    ExecutionOutOfGas,
    UnsupportedCompressionDict,
    ModuleHashMismatch,
    ActivationFailed,
    ProgramNotWasm,
    ProgramNotActivated,
    ProgramNeedsUpgrade,
    ProgramExpired,
    ProgramUpToDate,
    ProgramKeepaliveTooSoon,
    ProgramInsufficientValue,
    UnknownError
}
