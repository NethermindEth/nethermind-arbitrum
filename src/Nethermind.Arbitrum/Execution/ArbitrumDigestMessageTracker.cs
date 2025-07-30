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
    private readonly Lock _lock = new();
    private readonly IBlockTree _blockTree;
    private readonly ILogger _logger;
    private readonly IArbitrumSpecHelper _specHelper;

    private ValueHash256 _latestResponseHash;
    private long _latestResponseNumber = -1; // -1 indicates no response recorded yet

    private ValueHash256 _currentTipHash;

    public ArbitrumDigestMessageTracker(IBlockTree blockTree, IArbitrumSpecHelper specHelper, ILogManager logManager)
    {
        _blockTree = blockTree;
        _specHelper = specHelper;
        _logger = logManager.GetClassLogger<ArbitrumDigestMessageTracker>();

        // Subscribe to tip updates once at startup
        _currentTipHash = blockTree.Head?.Hash ?? Hash256.Zero;
        blockTree.NewHeadBlock += OnNewHeadBlock;

        if (_logger.IsDebug)
            _logger.Debug($"ArbitrumDigestMessageTracker initialized with tip: {_currentTipHash}");
    }

    private void OnNewHeadBlock(object? sender, BlockEventArgs e)
    {
        lock (_lock)
        {
            _currentTipHash = e.Block.Hash;
            if (_logger.IsTrace)
                _logger.Trace($"Tip updated to: {_currentTipHash}");
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
            _latestResponseHash = blockHash;
            _latestResponseNumber = messageNumber;

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
        if (messageNumber == 0)
            return true;

        // If no previous response has been recorded yet, allow this message to proceed
        // This handles the case where the node was just started and this is the first message after initialization
        if (_latestResponseNumber == -1)
        {
            if (_logger.IsDebug)
                _logger.Debug($"No previous response recorded, allowing message {messageNumber} to proceed");
            return true;
        }

        // Ensure order is not broken
        if (_latestResponseNumber + 1 != messageNumber)
        {
            if (_logger.IsDebug)
                _logger.Debug($"Message order broken: expected {_latestResponseNumber + 1}, got {messageNumber}");
            return false;
        }

        // If get here, we should already have a response recorded
        if (_latestResponseHash == Hash256.Zero)
        {
            if (_logger.IsDebug)
                _logger.Debug($"No response recorded for previous message {_latestResponseNumber}");
            return false;
        }

        // Check if tip already matches
        lock (_lock)
        {
            if (_currentTipHash == _latestResponseHash)
            {
                if (_logger.IsTrace)
                    _logger.Trace($"Tip already matches recorded response for message {_latestResponseNumber}");
                return true;
            }
        }

        if (_logger.IsDebug)
            _logger.Debug($"Waiting for tip to match recorded response: expected={_latestResponseHash}");

        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMs);

        while (DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(10);

            lock (_lock)
            {
                if (_currentTipHash == _latestResponseHash)
                {
                    if (_logger.IsDebug)
                        _logger.Debug($"Tip now matches recorded response for message {_latestResponseNumber}");
                    return true;
                }
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

        lock (_lock)
        {
            var currentTip = _blockTree.Head;
            if (currentTip?.Number > expectedBlockNumber)
            {
                var errorMessage = $"Tip has advanced beyond expected message number. Tip block: {currentTip.Number}, Expected block: {expectedBlockNumber}, Message number: {messageNumber}";
                if (_logger.IsError)
                    _logger.Error(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }
    }

    public void Dispose()
    {
        // Unsubscribe from tip updates
        _blockTree.NewHeadBlock -= OnNewHeadBlock;
    }
}
