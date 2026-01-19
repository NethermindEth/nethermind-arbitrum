// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Math;

/// <summary>
/// Utility class for converting between Arbitrum message indices and block numbers.
/// </summary>
public static class MessageBlockConverter
{
    /// <summary>
    /// Converts a block number to the corresponding Arbitrum message index.
    /// </summary>
    /// <param name="blockNumber">The block number to convert</param>
    /// <param name="specHelper">The Arbitrum spec helper containing genesis block number</param>
    /// <returns>The corresponding message index</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when block number is before genesis</exception>
    public static ulong BlockNumberToMessageIndex(ulong blockNumber, IArbitrumSpecHelper specHelper)
    {
        ulong genesisBlockNum = specHelper.GenesisBlockNum;

        if (blockNumber < genesisBlockNum)
        {
            throw new ArgumentOutOfRangeException(nameof(blockNumber),
                $"Block number {blockNumber} is before genesis block {genesisBlockNum}");
        }

        return blockNumber - genesisBlockNum;
    }

    /// <summary>
    /// Converts an Arbitrum message index to a corresponding block number.
    /// </summary>
    /// <param name="messageIndex">The message index to convert</param>
    /// <param name="specHelper">The Arbitrum spec helper containing genesis block number</param>
    /// <returns>The corresponding block number</returns>
    /// <exception cref="OverflowException">Thrown when the result would overflow long.MaxValue</exception>
    public static long MessageIndexToBlockNumber(ulong messageIndex, IArbitrumSpecHelper specHelper)
    {
        ulong genesisBlockNum = specHelper.GenesisBlockNum;

        // Check for overflow before performing addition
        if (messageIndex > long.MaxValue - genesisBlockNum)
        {
            throw new OverflowException(
                $"Message index {messageIndex} would cause overflow when added to genesis block {genesisBlockNum}");
        }

        ulong blockNumber = genesisBlockNum + messageIndex;
        return (long)blockNumber;
    }
}
