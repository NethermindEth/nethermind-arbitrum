// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm;

namespace Nethermind.Arbitrum.Arbos;

public static class StylusOperationResultTypeExtensions
{
    public static EvmExceptionType ToEvmExceptionType(this StylusOperationResultType operationResult)
    {
        return operationResult switch
        {
            StylusOperationResultType.Success => EvmExceptionType.None,
            StylusOperationResultType.ExecutionRevert => EvmExceptionType.Other,
            StylusOperationResultType.ExecutionFailure => EvmExceptionType.OutOfGas,
            StylusOperationResultType.ExecutionOutOfInk => EvmExceptionType.Other,
            StylusOperationResultType.ExecutionOutOfStack => EvmExceptionType.Other,
            StylusOperationResultType.InvalidByteCode => EvmExceptionType.Other,
            StylusOperationResultType.UnsupportedCompressionDict => EvmExceptionType.Other,
            StylusOperationResultType.ExecutionOutOfGas => EvmExceptionType.OutOfGas,
            StylusOperationResultType.ModuleHashMismatch => EvmExceptionType.Other,
            StylusOperationResultType.ActivationFailed => EvmExceptionType.Other,
            StylusOperationResultType.ProgramNotWasm => EvmExceptionType.Other,
            StylusOperationResultType.ProgramNotActivated => EvmExceptionType.Other,
            StylusOperationResultType.UnknownError => EvmExceptionType.Other,
            StylusOperationResultType.ProgramNeedsUpgrade => EvmExceptionType.Other,
            StylusOperationResultType.ProgramExpired => EvmExceptionType.Other,
            _ => EvmExceptionType.Other,
        };
    }
}
