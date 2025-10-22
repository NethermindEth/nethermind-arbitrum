// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Arbos;

public static class UserOutcomeKindExtensions
{
    public static StylusOperationResultType ToOperationResultType(this UserOutcomeKind outcomeKind, bool isStylusActivation)
    {
        return outcomeKind switch
        {
            UserOutcomeKind.Success => StylusOperationResultType.Success,
            UserOutcomeKind.Revert => isStylusActivation ? StylusOperationResultType.ActivationFailed : StylusOperationResultType.ExecutionRevert,
            UserOutcomeKind.Failure => isStylusActivation ? StylusOperationResultType.ActivationFailed : StylusOperationResultType.ExecutionRevert,
            UserOutcomeKind.OutOfInk => StylusOperationResultType.ExecutionOutOfInk,
            UserOutcomeKind.OutOfStack => StylusOperationResultType.ExecutionOutOfStack,
            _ => throw new ArgumentOutOfRangeException(nameof(outcomeKind), "Unknown UserOutcomeKind value")
        };
    }
}
