// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;

namespace Nethermind.Arbitrum.Arbos;

public readonly ref struct StylusOperationResult<T>(T? value, StylusOperationResultType operationResultType, string? error)
    where T : allows ref struct
{
    public T? Value { get; } = value;
    public StylusOperationResultType OperationResultType { get; } = operationResultType;
    public string? Error { get; } = error;
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public void Deconstruct(out T? value, out string? error)
    {
        value = Value;
        error = Error;
    }

    public static StylusOperationResult<T> Success(T value)
    {
        return new(value, StylusOperationResultType.Success, null);
    }

    public static StylusOperationResult<T> Failure(StylusOperationResultType operationResultType, string error)
    {
        return new(default, operationResultType, error);
    }

    public static StylusOperationResult<T> Failure(StylusOperationResultType operationResultType, string error, T? value)
    {
        return new(value, operationResultType, error);
    }

    public StylusOperationResult<T> WithErrorContext(string additionalContext)
    {
        return IsSuccess ? this : new(Value, OperationResultType, $"{Error} [{additionalContext}]");
    }

    public StylusOperationResult<TR> CastFailure<TR>()
    {
        return IsSuccess
            ? throw new InvalidOperationException($"Cannot cast {typeof(T).Name} to {typeof(TR).Name}")
            : StylusOperationResult<TR>.Failure(OperationResultType, Error!);
    }
}
