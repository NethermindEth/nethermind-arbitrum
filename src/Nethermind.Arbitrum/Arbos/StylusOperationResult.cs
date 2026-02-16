// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using static Nethermind.Arbitrum.Arbos.Programs.StylusPrograms;

namespace Nethermind.Arbitrum.Arbos;

public readonly ref struct StylusOperationResult<T>(T? value, StylusOperationError? error)
    where T : allows ref struct
{
    public T? Value { get; } = value;
    public StylusOperationError? Error { get; } = error;
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public void Deconstruct(out T? value, out StylusOperationError? error)
    {
        value = Value;
        error = Error;
    }

    public static StylusOperationResult<T> Success(T value)
    {
        return new(value, null);
    }

    public static StylusOperationResult<T> Failure(StylusOperationError error)
    {
        return new(default, error);
    }

    public static StylusOperationResult<T> Failure(StylusOperationError error, T value)
    {
        return new(value, error);
    }

    public StylusOperationResult<T> WithErrorContext(string additionalContext)
    {
        if (IsSuccess)
            return this;

        StylusOperationError newError = new(Error.Value.OperationResultType, $"{Error.Value.Message} [{additionalContext}]", Error.Value.Arguments);
        return new(Value, newError);
    }

    public StylusOperationResult<TR> CastFailure<TR>()
    {
        return IsSuccess
            ? throw new InvalidOperationException($"Cannot cast {typeof(T).Name} to {typeof(TR).Name}")
            : StylusOperationResult<TR>.Failure(Error.Value);
    }
}
