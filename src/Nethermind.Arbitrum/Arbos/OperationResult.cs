// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;

namespace Nethermind.Arbitrum.Arbos;

public readonly ref struct OperationResult<T>(T? value, string? error)
    where T : allows ref struct
{
    public T? Value { get; } = value;
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
        return new(value, null);
    }

    public static OperationResult<T> Failure(string error)
    {
        return new(default, error);
    }

    public OperationResult<T> WithErrorContext(string additionalContext)
    {
        return IsSuccess ? this : new(Value, $"{Error} [{additionalContext}]");
    }

    public OperationResult<TR> CastFailure<TR>()
    {
        return IsSuccess
            ? throw new InvalidOperationException($"Cannot cast {typeof(T).Name} to {typeof(TR).Name}")
            : OperationResult<TR>.Failure(Error!);
    }
}
