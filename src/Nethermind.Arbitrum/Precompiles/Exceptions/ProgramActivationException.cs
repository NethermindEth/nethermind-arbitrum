// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Precompiles.Exceptions;

public class ProgramActivationException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
