// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Nethermind.Arbitrum.Sequencer;

public interface IAuctionResolutionQueue
{
    public bool TryRead([MaybeNullWhen(false)] out TxQueueItem item);
    public ValueTask WriteAsync(TxQueueItem item);
}

public sealed class DisabledAuctionResolutionQueue : IAuctionResolutionQueue
{
    public bool TryRead([MaybeNullWhen(false)] out TxQueueItem item)
    {
        item = null;
        return false;
    }

    public ValueTask WriteAsync(TxQueueItem item)
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class AuctionResolutionQueue : IAuctionResolutionQueue
{
    private readonly Channel<TxQueueItem> _channel =
        Channel.CreateBounded<TxQueueItem>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public ChannelReader<TxQueueItem> Reader => _channel.Reader;
    public ChannelWriter<TxQueueItem> Writer => _channel.Writer;

    public bool TryRead([MaybeNullWhen(false)] out TxQueueItem item)
    {
        return _channel.Reader.TryRead(out item);
    }

    public ValueTask WriteAsync(TxQueueItem item)
    {
        return _channel.Writer.WriteAsync(item);
    }
}
