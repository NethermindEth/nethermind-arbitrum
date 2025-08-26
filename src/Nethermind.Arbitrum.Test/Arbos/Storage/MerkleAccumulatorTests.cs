using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class MerkleAccumulatorTests
{
    [Test]
    public void CalculateRoot_EmptyAccumulator_ReturnsZeroHash()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        MerkleAccumulator accumulator = new(storage);

        accumulator.CalculateRoot().Should().Be(default);
    }

    [Test]
    public void CalculateRoot_SingleNode_ReturnsCorrectRootAndSize()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        MerkleAccumulator accumulator = new(storage);

        ValueHash256 node = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ValueHash256 expected = Keccak.Compute(node.Bytes);

        accumulator.Append(node).Should().BeEmpty();

        accumulator.GetSize().Should().Be(1);
        accumulator.CalculateRoot().Should().Be(expected);
    }

    [Test]
    public void CalculateRoot_3Nodes_ReturnsCorrectRootAndSize()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        MerkleAccumulator accumulator = new(storage);

        ValueHash256 node1 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ValueHash256 node2 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ValueHash256 node3 = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        ValueHash256 expectedLevel1 = Keccak.Compute(Bytes.Concat(
            Keccak.Compute(node1.Bytes).Bytes,
            Keccak.Compute(node2.Bytes).Bytes));
        ValueHash256 expectedRoot = Keccak.Compute(Bytes.Concat(
            expectedLevel1.Bytes,
            Keccak.Compute(Bytes.Concat(
                Keccak.Compute(node3.Bytes).Bytes,
                new byte[32])).Bytes));

        accumulator.Append(node1).Should().BeEmpty(); // Add level 0 node
        accumulator.Append(node2).Should().BeEquivalentTo([
            new MerkleTreeNodeEvent(1ul, 1ul, expectedLevel1) // Level 1 calculated from 2 nodes at level 0
        ]);
        accumulator.Append(node3).Should().BeEmpty(); // Add level 0 node, no new event as no new level is created

        accumulator.GetSize().Should().Be(3);
        accumulator.CalculateRoot().Should().Be(expectedRoot);
    }

    [Test]
    public void CalculateRoot_4Nodes_ReturnsCorrectRootAndSize()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        MerkleAccumulator accumulator = new(storage);

        ValueHash256 node1 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ValueHash256 node2 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ValueHash256 node3 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ValueHash256 node4 = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        ValueHash256 expectedLevel1First = Keccak.Compute(Bytes.Concat(
            Keccak.Compute(node1.Bytes).Bytes,
            Keccak.Compute(node2.Bytes).Bytes));
        ValueHash256 expectedLevel1Second = Keccak.Compute(Bytes.Concat(
            Keccak.Compute(node3.Bytes).Bytes,
            Keccak.Compute(node4.Bytes).Bytes));
        ValueHash256 expectedTreeRoot = Keccak.Compute(
            Bytes.Concat(
                expectedLevel1First.Bytes,
                expectedLevel1Second.Bytes));

        accumulator.Append(node1).Should().BeEmpty(); // Add level 0 node
        accumulator.Append(node2).Should().BeEquivalentTo([
            new MerkleTreeNodeEvent(1ul, 1ul, expectedLevel1First) // Level 1 calculated from 2 nodes at level 0
        ]);
        accumulator.Append(node3).Should().BeEmpty(); // Add level 0 node
        accumulator.Append(node4).Should().BeEquivalentTo([
            new MerkleTreeNodeEvent(1ul, 3ul, expectedLevel1Second), // Level 1 calculated from 2 nodes at level 0 (second pair)
            new MerkleTreeNodeEvent(2ul, 3ul, expectedTreeRoot)  // Level 2 calculated from 2 nodes at level 1
        ]);

        accumulator.GetSize().Should().Be(4);
        accumulator.CalculateRoot().Should().Be(expectedTreeRoot);
    }

    [Test]
    public void GetExportState_3Nodes_ReturnsCorrectState()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        MerkleAccumulator accumulator = new(storage);

        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000001"));
        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000002"));
        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000003"));

        // Captured from Nitro
        MerkleAccumulatorExportState expected = new(3,
            new ValueHash256("0x68d4880e8b3b97fc57678c4dce33b2e0d84a06024476eceba62bdc2e07a1e279"),
            [
                new ValueHash256("0xc2575a0e9e593c00f959f8c92f12db2869c3395a3b0502d05e2516446f71f85b"),
                new ValueHash256("0x50387073e2d4f7060a3c02c3c5268d8a72700a28b5cbd7e23314ae0e1ebda895")
            ]);

        accumulator.GetExportState().Should().BeEquivalentTo(expected);
    }

    [Test]
    public void GetExportState_4Nodes_ReturnsCorrectState()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        MerkleAccumulator accumulator = new(storage);

        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000001"));
        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000002"));
        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000003"));
        accumulator.Append(new("0x0000000000000000000000000000000000000000000000000000000000000004"));

        // Captured from Nitro
        MerkleAccumulatorExportState expected = new(4,
            new ValueHash256("0x1e8cc8511a4954df48a80e5f5b8da3419a99ba3e7697574234e10893022167fc"),
            [
                new ValueHash256("0x0000000000000000000000000000000000000000000000000000000000000000"), // Level 0 is calculated and reset
                new ValueHash256("0x0000000000000000000000000000000000000000000000000000000000000000"), // Level 1 is calculated and reset
                new ValueHash256("0x1e8cc8511a4954df48a80e5f5b8da3419a99ba3e7697574234e10893022167fc")
            ]);

        accumulator.GetExportState().Should().BeEquivalentTo(expected);
    }
}
