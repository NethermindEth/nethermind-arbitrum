// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class SubStorageVectorTests
{
    [Test]
    public void At_WithValidIndex_ReturnsSameStorageAsPush()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));
        ArbosStorage pushedStorage = vector.Push();

        // Write a value to the pushed storage
        pushedStorage.Set(0, 12345UL);

        // Access via At and verify the value
        ArbosStorage atStorage = vector.At(0);
        ulong value = atStorage.GetULong(0);

        value.Should().Be(12345UL);
    }
    [Test]
    public void Length_InitialState_ReturnsZero()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));

        ulong length = vector.Length();

        length.Should().Be(0);
    }

    [Test]
    public void Pop_AfterPush_ReturnsSameStorageReference()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));
        ArbosStorage pushedStorage = vector.Push();
        pushedStorage.Set(0, 99999UL);

        ArbosStorage poppedStorage = vector.Pop();
        ulong value = poppedStorage.GetULong(0);

        value.Should().Be(99999UL);
    }

    [Test]
    public void Pop_EmptyVector_ThrowsInvalidOperationException()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));

        Action act = () => vector.Pop();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("sub-storage vector: can't pop empty");
    }

    [Test]
    public void Pop_MultipleElements_DecrementsLengthCorrectly()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));
        vector.Push();
        vector.Push();
        vector.Push();

        vector.Pop();
        vector.Pop();

        vector.Length().Should().Be(1);
    }

    [Test]
    public void Pop_SingleElement_DecrementsLengthToZero()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));
        vector.Push();

        vector.Pop();

        vector.Length().Should().Be(0);
    }

    [Test]
    public void Push_AfterPop_ReusesStorageSlot()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));

        // Push the first element
        ArbosStorage first = vector.Push();
        first.Set(0, 111UL);

        // Pop it (data should remain accessible via the same index)
        vector.Pop();

        // Push again - should get the same index 0
        ArbosStorage second = vector.Push();

        // The new storage at index 0 should have the old data (until cleared)
        ulong value = second.GetULong(0);

        value.Should().Be(111UL);
    }

    [Test]
    public void Push_EmptyVector_IncrementsLengthToOne()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));

        vector.Push();

        vector.Length().Should().Be(1);
    }

    [Test]
    public void Push_MultipleTimes_IncrementsLengthCorrectly()
    {
        using IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        SubStorageVector vector = new(storage.OpenSubStorage([1]));

        vector.Push();
        vector.Push();
        vector.Push();

        vector.Length().Should().Be(3);
    }
}
