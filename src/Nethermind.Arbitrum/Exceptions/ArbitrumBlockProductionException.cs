// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Exceptions;

public class ArbitrumBlockProductionException(string error, int errorCode) : Exception(error)
{
    public readonly int ErrorCode = errorCode;
}
