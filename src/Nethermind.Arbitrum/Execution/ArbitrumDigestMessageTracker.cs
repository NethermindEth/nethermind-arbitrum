// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Math;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Events;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Tracks DigestMessage responses and ensures consistency between responses and blockchain tip.
///
/// Assumes synchronous, single-threaded communication with Nitro where:
/// - Nitro waits for previous DigestMessage to be processed before sending the next one
/// - There cannot be two parallel DigestMessage sent to Nethermind
/// - Once Nethermind returns BlockHash and SendRoot to Nitro, it guarantees those won't change
/// </summary>
public sealed class ArbitrumDigestMessageTracker(
    IBlockTree blockTree,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumDigestMessageTracker>();
    private readonly Lock _lock = new();

    // Track the latest DigestMessage response
    private Hash256? _latestDigestMessageResponse;
    private long _latestTipNumber = blockTree.Head?.Number ?? 0;
    private long _latestMessageNumber;

    /// <summary>
    /// Records a DigestMessage response for consistency checking.
    /// </summary>
    /// <param name="messageNumber">The message number that was processed</param>
    /// <param name="blockHash">The block hash returned in the response</param>
    public void RecordDigestMessageResponse(long messageNumber, Hash256 blockHash)
    {
        lock (_lock)
        {
            _latestDigestMessageResponse = blockHash;
            _latestMessageNumber = messageNumber;
            _latestTipNumber = messageNumber;

            if (_logger.IsTrace)
                _logger.Trace($"Recorded DigestMessage response: messageNumber={messageNumber}, blockHash={blockHash}");
        }
    }

    /// <summary>
    /// Ensures consistency between the latest DigestMessage response and the current blockchain tip.
    /// Returns true if consistent, false if timeout occurs.
    /// </summary>
    public async ValueTask<bool> EnsureConsistencyAsync(long messageNumber, int timeoutMs = 5000)
    {
        // If this is the first message (number 0), no consistency check needed
        if (messageNumber <= 0)
            return true;

        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            var expectedBlockNumber = messageNumber - 1; // Previous message should have created this block

            // Check if we have a recorded response for the previous message
            Hash256? expectedResponseHash = GetExpectedResponseHash(expectedBlockNumber);

            if (expectedResponseHash is null)
            {
                if (_logger.IsDebug)
                    _logger.Debug($"No recorded response for message {expectedBlockNumber}, skipping consistency check");
                return true;
            }

            // Check if tip already matches
            Block? currentTip = blockTree.Head;
            if (IsTipMatching(currentTip, expectedBlockNumber, expectedResponseHash))
            {
                if (_logger.IsTrace)
                    _logger.Trace($"Tip already matches recorded response for message {expectedBlockNumber}");
                return true;
            }

            // Wait for tip to match the recorded response
            if (_logger.IsDebug)
                _logger.Debug($"Waiting for tip to match recorded response: expected={expectedResponseHash}, current={currentTip?.Hash}");

            await Wait.ForEventCondition<BlockEventArgs>(
                cts.Token,
                register: handler => blockTree.NewHeadBlock += handler,
                unregister: handler => blockTree.NewHeadBlock -= handler,
                condition: args => args.Block.Number == expectedBlockNumber && args.Block.Hash == expectedResponseHash
            );

            if (_logger.IsDebug)
                _logger.Debug($"Tip now matches recorded response for message {expectedBlockNumber}");

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.Warn($"Consistency check timeout after {timeoutMs}ms for message {messageNumber}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during consistency check for message {messageNumber}", ex);
            return false;
        }
    }

    /// <summary>
    /// Validates that the tip hasn't advanced beyond the expected message number.
    /// </summary>
    /// <param name="messageNumber">The message number being processed</param>
    public void ValidateTipAdvancement(long messageNumber)
    {
        lock (_lock)
        {
            var expectedBlockNumber = MessageBlockConverter.MessageIndexToBlockNumber((ulong)messageNumber, specHelper);

            if (_latestTipNumber > expectedBlockNumber)
            {
                var errorMessage = $"Tip has advanced beyond expected message number. Tip block: {_latestTipNumber}, Expected block: {expectedBlockNumber}, Message number: {messageNumber}";
                if (_logger.IsError)
                    _logger.Error(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }
    }

    private Hash256? GetExpectedResponseHash(long expectedBlockNumber)
    {
        lock (_lock)
        {
            return _latestMessageNumber == expectedBlockNumber && _latestDigestMessageResponse is not null
                ? _latestDigestMessageResponse
                : null;
        }
    }

    private static bool IsTipMatching(Block? currentTip, long expectedBlockNumber, Hash256 expectedResponseHash) =>
        currentTip?.Number == expectedBlockNumber && currentTip.Hash == expectedResponseHash;
}
