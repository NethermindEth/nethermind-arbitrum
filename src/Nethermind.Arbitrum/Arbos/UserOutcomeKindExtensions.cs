// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Arbos;


public static class UserOutcomeKindExtensions
{
    public static OperationResultType ToOperationResultType(this UserOutcomeKind outcomeKind)
    {
        return outcomeKind switch
        {
            UserOutcomeKind.Success => OperationResultType.Success,
            UserOutcomeKind.Revert => OperationResultType.StylusExecutionRevert,
            UserOutcomeKind.Failure => OperationResultType.StylusExecutionFailure,
            UserOutcomeKind.OutOfInk => OperationResultType.StylusExecutionOutOfInk,
            UserOutcomeKind.OutOfStack => OperationResultType.StylusExecutionOutOfStack,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
