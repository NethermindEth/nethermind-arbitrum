// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
