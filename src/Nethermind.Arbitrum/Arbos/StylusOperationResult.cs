// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using static Nethermind.Arbitrum.Arbos.Programs.StylusPrograms;

namespace Nethermind.Arbitrum.Arbos;

public readonly ref struct StylusOperationResult<T>(T? value, StylusOperationError? error)
    where T : allows ref struct
{
    public StylusOperationError? Error { get; } = error;
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;
    public T? Value { get; } = value;

    public static StylusOperationResult<T> Failure(StylusOperationError error)
    {
        return new(default, error);
    }

    public static StylusOperationResult<T> Failure(StylusOperationError error, T value)
    {
        return new(value, error);
    }

    public static StylusOperationResult<T> Success(T value)
    {
        return new(value, null);
    }

    public StylusOperationResult<TR> CastFailure<TR>()
    {
        return IsSuccess
            ? throw new InvalidOperationException($"Cannot cast {typeof(T).Name} to {typeof(TR).Name}")
            : StylusOperationResult<TR>.Failure(Error.Value);
    }

    public void Deconstruct(out T? value, out StylusOperationError? error)
    {
        value = Value;
        error = Error;
    }

    public StylusOperationResult<T> WithErrorContext(string additionalContext)
    {
        if (IsSuccess)
            return this;

        StylusOperationError newError = new(Error.Value.OperationResultType, $"{Error.Value.Message} [{additionalContext}]", Error.Value.Arguments);
        return new(Value, newError);
    }
}
