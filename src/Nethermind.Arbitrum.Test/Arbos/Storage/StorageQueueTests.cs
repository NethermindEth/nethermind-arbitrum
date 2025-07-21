using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class StorageQueueTests
{
    [Test]
    public void Initialize_NewQueue_CreatesNextPushAndNextPopOffsets()
    {
        (ArbosStorage storage, TrackingWorldState state) = TestArbosStorage.Create();

        StorageQueue.Initialize(storage);

        storage.GetULong(0).Should().Be(2);
        storage.GetULong(1).Should().Be(2);
        state.SetRecords.Should().HaveCount(2);
    }

    [Test]
    public void IsEmpty_EmptyQueue_ReturnsTrue()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        bool isEmpty = queue.IsEmpty();

        isEmpty.Should().BeTrue();
    }

    [Test]
    public void Peek_EmptyQueue_ReturnsDefault()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        queue.Peek().Should().Be(default);
    }

    [Test]
    public void Peek_HasValueInQueue_ReturnsValue()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        ValueHash256 expected = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        queue.Push(expected);

        queue.IsEmpty().Should().BeFalse();
        queue.Peek().Should().Be(expected);
        queue.Peek().Should().Be(expected);
    }

    [Test]
    public void Pop_EmptyQueue_ReturnsDefault()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        queue.Pop().Should().Be(default);
    }

    [Test]
    public void Pop_HasValueInQueue_ReturnsValueAndRemovesIt()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        ValueHash256 expected = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        queue.Push(expected);

        queue.IsEmpty().Should().BeFalse();
        queue.Pop().Should().Be(expected);
        queue.IsEmpty().Should().BeTrue();
    }

    [Test]
    public void Size_EmptyQueue_ReturnsZero()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        queue.Size().Should().Be(0);
    }

    [TestCase(1u)]
    [TestCase(2u)]
    [TestCase(3u)]
    public void Size_HasValuesInQueue_ReturnsCorrectSize(ulong count)
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        for (ulong i = 0; i < count; i++)
        {
            queue.Push(new(RandomNumberGenerator.GetBytes(Hash256.Size)));
        }

        queue.Size().Should().Be(count);
    }

    [Test]
    public void ForEach_EmptyQueue_DoesNotInvokeHandler()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        bool handlerInvoked = false;
        queue.ForEach((_, _) => { handlerInvoked = true; return true; });

        handlerInvoked.Should().BeFalse();
    }

    [Test]
    public void ForEach_HasValuesInQueue_InvokesHandlerForEachValue()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        ulong count = 5;
        List<(ulong, ValueHash256)> hashes = [];
        for (ulong i = 0; i < count; i++)
        {
            hashes.Add((i + 2, new(RandomNumberGenerator.GetBytes(Hash256.Size)))); // Start from 2 to match Nitro's offset implementation
            queue.Push(hashes[^1].Item2);
        }

        List<(ulong, ValueHash256)> processed = new();
        queue.ForEach((index, hash) => { processed.Add((index, hash)); return false; });

        processed.Should().BeEquivalentTo(hashes);
        queue.Size().Should().Be(count); // Does not remove items
    }

    [Test]
    public void ForEach_HandlerReturnsTrue_StopsProcessing()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        StorageQueue.Initialize(storage);
        StorageQueue queue = new(storage);

        ulong count = 5;
        ulong stopAt = 3;
        for (ulong i = 0; i < count; i++)
        {
            queue.Push(new(RandomNumberGenerator.GetBytes(Hash256.Size)));
        }

        ulong processedCount = 0;
        queue.ForEach((_, _) => { processedCount++; return processedCount == stopAt; });

        processedCount.Should().Be(stopAt);
    }
}
