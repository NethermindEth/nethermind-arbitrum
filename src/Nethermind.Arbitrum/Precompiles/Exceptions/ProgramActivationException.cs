// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only


namespace Nethermind.Arbitrum.Precompiles.Exceptions;

public class ProgramActivationException : Exception
{
    public string ErrorCode { get; }

    public ProgramActivationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

