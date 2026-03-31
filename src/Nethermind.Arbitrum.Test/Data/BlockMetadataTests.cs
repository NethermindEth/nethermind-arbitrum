// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;

namespace Nethermind.Arbitrum.Test.Data;

[TestFixture]
public class BlockMetadataTests
{
    [Test]
    public void IsTxTimeboosted_NullMetadata_ReturnsNull()
    {
        BlockMetadata.IsTxTimeboosted(null, 0).Should().BeNull();
    }

    [Test]
    public void IsTxTimeboosted_EmptyMetadata_ReturnsFalse()
    {
        BlockMetadata.IsTxTimeboosted([], 0).Should().BeFalse();
    }

    [Test]
    public void IsTxTimeboosted_NegativeTxIndex_ReturnsFalse()
    {
        byte[] metadata = [0x00, 0x01];
        BlockMetadata.IsTxTimeboosted(metadata, -1).Should().BeFalse();
    }

    [Test]
    public void IsTxTimeboosted_VersionByteOnly_ReturnsFalse()
    {
        byte[] metadata = [0x00];
        BlockMetadata.IsTxTimeboosted(metadata, 0).Should().BeFalse();
    }

    [Test]
    public void IsTxTimeboosted_TxIndex0_Timeboosted()
    {
        byte[] metadata = [0x00, 0x01];
        BlockMetadata.IsTxTimeboosted(metadata, 0).Should().BeTrue();
    }

    [Test]
    public void IsTxTimeboosted_TxIndex1_Timeboosted()
    {
        byte[] metadata = [0x00, 0x02];
        BlockMetadata.IsTxTimeboosted(metadata, 1).Should().BeTrue();
    }

    [Test]
    public void IsTxTimeboosted_TxIndex0_NotTimeboosted()
    {
        byte[] metadata = [0x00, 0x02];
        BlockMetadata.IsTxTimeboosted(metadata, 0).Should().BeFalse();
    }

    [Test]
    public void IsTxTimeboosted_ByteBoundary_TxIndex8()
    {
        byte[] metadata = [0x00, 0x00, 0x01];
        BlockMetadata.IsTxTimeboosted(metadata, 8).Should().BeTrue();
    }

    [Test]
    public void IsTxTimeboosted_BeyondBitmap_ReturnsFalse()
    {
        byte[] metadata = [0x00, 0x01];
        BlockMetadata.IsTxTimeboosted(metadata, 8).Should().BeFalse();
    }

    [Test]
    public void IsTxTimeboosted_MultipleTxs_CorrectBits()
    {
        // 0x05 = 0b00000101 → bits 0 and 2 are set
        byte[] metadata = [0x00, 0x05];
        BlockMetadata.IsTxTimeboosted(metadata, 0).Should().BeTrue();
        BlockMetadata.IsTxTimeboosted(metadata, 1).Should().BeFalse();
        BlockMetadata.IsTxTimeboosted(metadata, 2).Should().BeTrue();
    }

    [Test]
    public void IsTxTimeboosted_SequencerBuiltBitmap_MatchesExpectedBits()
    {
        // Build metadata the same way ArbitrumSequencerEngine.BuildSequencedMsg does
        int txCount = 12;
        int[] timeboostedIndices = [1, 4, 8, 11];
        byte[] metadata = new byte[1 + (txCount + 7) / 8];

        foreach (int i in timeboostedIndices)
        {
            metadata[1 + i / 8] |= (byte)(1 << (i % 8));
        }

        for (int i = 0; i < txCount; i++)
        {
            bool expected = timeboostedIndices.Contains(i);
            BlockMetadata.IsTxTimeboosted(metadata, i).Should().Be(expected,
                because: $"tx {i} should{(expected ? "" : " not")} be timeboosted");
        }
    }
}
