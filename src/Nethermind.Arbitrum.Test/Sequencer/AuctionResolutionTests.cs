// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class AuctionResolutionTests
{
    [Test]
    public void TxQueueItem_NewInstance_HasRetryCountZero()
    {
        TxQueueItem item = new(Build.A.Transaction.TestObject, CancellationToken.None);
        item.RetryCount.Should().Be(0);
    }

    [Test]
    public void TxQueueItem_IncrementRetryCount_TracksRetries()
    {
        TxQueueItem item = new(Build.A.Transaction.TestObject, CancellationToken.None);
        item.RetryCount++;
        item.RetryCount.Should().Be(1);

        item.RetryCount++;
        item.RetryCount.Should().Be(2);
    }
}
