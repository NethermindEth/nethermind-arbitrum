// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Arbos;

public static class UserOutcomeKindExtensions
{
    public static StylusResultType ToOperationResultType(this UserOutcomeKind outcomeKind)
    {
        return outcomeKind switch
        {
            UserOutcomeKind.Success => StylusResultType.Success,
            UserOutcomeKind.Revert => StylusResultType.ExecutionRevert,
            UserOutcomeKind.Failure => StylusResultType.ExecutionFailure,
            UserOutcomeKind.OutOfInk => StylusResultType.ExecutionOutOfInk,
            UserOutcomeKind.OutOfStack => StylusResultType.ExecutionOutOfStack,
            _ => throw new ArgumentOutOfRangeException(nameof(outcomeKind), "Unknown UserOutcomeKind value")
        };
    }
}
