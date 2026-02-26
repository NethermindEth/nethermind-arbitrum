// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading.Channels;

namespace Nethermind.Arbitrum.Sequencer;

public sealed class AuctionResolutionQueue
{
    private readonly Channel<TxQueueItem> _channel =
        Channel.CreateBounded<TxQueueItem>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public ChannelReader<TxQueueItem> Reader => _channel.Reader;
    public ChannelWriter<TxQueueItem> Writer => _channel.Writer;
}
