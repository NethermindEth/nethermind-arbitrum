// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Evm;

namespace Nethermind.Arbitrum.Arbos;

public static class StylusOperationResultTypeExtensions
{
    public static EvmExceptionType ToEvmExceptionType(this StylusOperationResultType operationResult)
    {
        return operationResult switch
        {
            StylusOperationResultType.Success => EvmExceptionType.None,
            StylusOperationResultType.ExecutionRevert => EvmExceptionType.Revert,
            StylusOperationResultType.ExecutionFailure => EvmExceptionType.Other,
            StylusOperationResultType.ExecutionOutOfInk => EvmExceptionType.OutOfGas,
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
