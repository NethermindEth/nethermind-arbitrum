// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;

namespace Nethermind.Arbitrum.Arbos;

public readonly ref struct OperationResult<T>(T? value, OperationResultType resultType, string? error)
    where T : allows ref struct
{
    public T? Value { get; } = value;
    public OperationResultType ResultType { get; } = resultType;
    public string? Error { get; } = error;
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public void Deconstruct(out T? value, out string? error)
    {
        value = Value;
        error = Error;
    }

    public static OperationResult<T> Success(T value)
    {
        return new(value, OperationResultType.Success, null);
    }

    public static OperationResult<T> Failure(OperationResultType resultType, string error)
    {
        return new(default, resultType, error);
    }

    public static OperationResult<T> Failure(OperationResultType resultType, string error, T? value)
    {
        return new(value, resultType, error);
    }

    public OperationResult<T> WithErrorContext(string additionalContext)
    {
        return IsSuccess ? this : new(Value, ResultType, $"{Error} [{additionalContext}]");
    }

    public OperationResult<TR> CastFailure<TR>()
    {
        return IsSuccess
            ? throw new InvalidOperationException($"Cannot cast {typeof(T).Name} to {typeof(TR).Name}")
            : OperationResult<TR>.Failure(ResultType, Error!);
    }
}
