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
            UserOutcomeKind.Revert => OperationResultType.ExecutionRevert,
            UserOutcomeKind.Failure => OperationResultType.ExecutionFailure,
            UserOutcomeKind.OutOfInk => OperationResultType.ExecutionOutOfInk,
            UserOutcomeKind.OutOfStack => OperationResultType.ExecutionOutOfStack,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
