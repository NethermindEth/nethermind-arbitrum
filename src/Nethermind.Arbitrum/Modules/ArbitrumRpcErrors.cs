// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Modules
{
    public static class ArbitrumRpcErrors
    {
        public const string Overflow = "Overflow occurred while calculating block number";
        public const string InternalError = "Internal error processing request";

        public static string BlockNotFound(long blockNumber) =>
            $"result not found";

        public static string FormatNullParameters() =>
            "Parameters cannot be null";
    }
}
