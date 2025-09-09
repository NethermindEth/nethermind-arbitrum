// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos;

public enum OperationExceptionType
{
    StylusExecutionSuccess = 0,
    StylusExecutionRevert = 1,
    StylusExecutionFailure = 2,
    StylusExecutionOutOfInk = 3,
    StylusExecutionOutOfStack = 4,
    InvalidStylusByteCode = 5,
    UnsupportedStylusDictForCompression = 6,
    ExecutionOutOfGas = 7,
    ModuleHashMismatch = 8,
    ActivationFailed = 9,
    ProgramNotWasm = 10,
    ProgramNotActivated = 11,
    Other = 12,
    ProgramNeedsUpgrade = 13,
    ProgramExpired = 14
}
