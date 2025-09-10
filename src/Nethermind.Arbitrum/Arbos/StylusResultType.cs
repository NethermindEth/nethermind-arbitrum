// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos;

public enum StylusResultType : byte
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
    Other
}
