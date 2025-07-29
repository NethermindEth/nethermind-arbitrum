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
public sealed class ArbitrumDigestMessageTracker : IDisposable
{
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private readonly IBlockTree _blockTree;
    private readonly IArbitrumSpecHelper _specHelper;

    // Track the latest DigestMessage response
    private Hash256? _latestDigestMessageResponse;
    private long _latestMessageNumber;

    private Block? _currentTip;
    private readonly object _tipLock = new();

    public ArbitrumDigestMessageTracker(IBlockTree blockTree, IArbitrumSpecHelper specHelper, ILogManager logManager)
    {
        _blockTree = blockTree;
        _specHelper = specHelper;
        _logger = logManager.GetClassLogger<ArbitrumDigestMessageTracker>();

        // Subscribe to tip updates once at startup
        _currentTip = blockTree.Head;
        blockTree.NewHeadBlock += OnNewHeadBlock;

        if (_logger.IsDebug)
            _logger.Debug($"ArbitrumDigestMessageTracker initialized with tip: {_currentTip?.Hash} (block {_currentTip?.Number})");
    }

    private void OnNewHeadBlock(object? sender, BlockEventArgs e)
    {
        lock (_tipLock)
        {
            _currentTip = e.Block;
            if (_logger.IsTrace)
                _logger.Trace($"Tip updated to: {_currentTip.Hash} (block {_currentTip.Number})");
        }
    }

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
        Block? currentTip = GetCurrentTip();
        if (IsTipMatching(currentTip, expectedBlockNumber, expectedResponseHash))
        {
            if (_logger.IsTrace)
                _logger.Trace($"Tip already matches recorded response for message {expectedBlockNumber}");
            return true;
        }

        // Wait for tip to match the recorded response by polling
        if (_logger.IsDebug)
            _logger.Debug($"Waiting for tip to match recorded response: expected={expectedResponseHash}, current={currentTip?.Hash}");

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        while (DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(1); // Poll every 1ms for optimal responsiveness

            currentTip = GetCurrentTip();
            if (IsTipMatching(currentTip, expectedBlockNumber, expectedResponseHash))
            {
                if (_logger.IsDebug)
                    _logger.Debug($"Tip now matches recorded response for message {expectedBlockNumber}");
                return true;
            }
        }

        _logger.Warn($"Consistency check timeout after {timeoutMs}ms for message {messageNumber}");
        return false;
    }

    /// <summary>
    /// Validates that the tip hasn't advanced beyond the expected message number.
    /// </summary>
    /// <param name="messageNumber">The message number being processed</param>
    public void ValidateTipAdvancement(long messageNumber)
    {
        var expectedBlockNumber = MessageBlockConverter.MessageIndexToBlockNumber((ulong)messageNumber, _specHelper);
        var currentTip = GetCurrentTip();

        if (currentTip?.Number > expectedBlockNumber)
        {
            var errorMessage = $"Tip has advanced beyond expected message number. Tip block: {currentTip.Number}, Expected block: {expectedBlockNumber}, Message number: {messageNumber}";
            if (_logger.IsError)
                _logger.Error(errorMessage);
            throw new InvalidOperationException(errorMessage);
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

    private Block? GetCurrentTip()
    {
        lock (_tipLock)
        {
            return _currentTip;
        }
    }

    private static bool IsTipMatching(Block? currentTip, long expectedBlockNumber, Hash256 expectedResponseHash) =>
        currentTip?.Number == expectedBlockNumber && currentTip.Hash == expectedResponseHash;

    public void Dispose()
    {
        // Unsubscribe from tip updates
        _blockTree.NewHeadBlock -= OnNewHeadBlock;
    }
}
