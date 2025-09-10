// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Arbos;

public enum OperationResultType
{
    Success = 0,
    StylusExecutionRevert = 1,
    StylusExecutionFailure = 2,
    StylusExecutionOutOfInk = 3,
    StylusExecutionOutOfStack = 4,
    StylusInvalidByteCode = 5,
    UnsupportedStylusCompressionDict = 6,
    ExecutionOutOfGas = 7,
    ModuleHashMismatch = 8,
    ActivationFailed = 9,
    ProgramNotWasm = 10,
    ProgramNotActivated = 11,
    Other = 12,
    ProgramNeedsUpgrade = 13,
    ProgramExpired = 14
}

