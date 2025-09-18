// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Precompiles.Exceptions;
public class UnauthorizedCallerException : Exception
{
    public UnauthorizedCallerException()
        : base("Unauthorized caller to access-controlled method")
    {
    }
}

