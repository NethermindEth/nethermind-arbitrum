// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;

namespace Nethermind.Arbitrum.Arbos;

public readonly ref struct StylusResult<T>(T? value, StylusResultType resultType, string? error)
    where T : allows ref struct
{
    public T? Value { get; } = value;
    public StylusResultType ResultType { get; } = resultType;
    public string? Error { get; } = error;
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public void Deconstruct(out T? value, out string? error)
    {
        value = Value;
        error = Error;
    }

    public static StylusResult<T> Success(T value)
    {
        return new(value, StylusResultType.Success, null);
    }

    public static StylusResult<T> Failure(StylusResultType resultType, string error)
    {
        return new(default, resultType, error);
    }

    public static StylusResult<T> Failure(StylusResultType resultType, string error, T? value)
    {
        return new(value, resultType, error);
    }

    public StylusResult<T> WithErrorContext(string additionalContext)
    {
        return IsSuccess ? this : new(Value, ResultType, $"{Error} [{additionalContext}]");
    }

    public StylusResult<TR> CastFailure<TR>()
    {
        return IsSuccess
            ? throw new InvalidOperationException($"Cannot cast {typeof(T).Name} to {typeof(TR).Name}")
            : StylusResult<TR>.Failure(ResultType, Error!);
    }
}
