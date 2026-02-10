// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
