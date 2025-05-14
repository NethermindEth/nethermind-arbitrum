// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only


// File: ArbitrumRpcErrors.cs
namespace Nethermind.Arbitrum.Modules
{
    public static class ArbitrumRpcErrors
    {
        public const string Overflow = "Overflow occurred while calculating block number";
        public const string BlockNotFound = "Block not found or not synced";
        public const string InternalError = "Internal error processing request";

        public static string FormatIndexBeforeGenesis(ulong index, ulong genesis) =>
            $"Message index {index} is before genesis block {genesis}";

        public static string FormatExceedsLongMax(ulong number) =>
            $"Block number {number} exceeds maximum value of {long.MaxValue}";
    }
}

