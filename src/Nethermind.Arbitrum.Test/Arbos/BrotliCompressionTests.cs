// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Test.Arbos;

public class BrotliCompressionTests
{
    [Test]
    public void Compress_UnknownDictionary_Throws()
    {
        BrotliCompression.Dictionary unknownDictionary = (BrotliCompression.Dictionary)999;
        byte[] input = RandomNumberGenerator.GetBytes(128);

        Assert.Throws<ArgumentException>(() => BrotliCompression.Compress(input, 11, unknownDictionary));
    }

    [Test]
    public void Decompress_UnknownDictionary_Throws()
    {
        BrotliCompression.Dictionary unknownDictionary = (BrotliCompression.Dictionary)999;

        byte[] input = RandomNumberGenerator.GetBytes(128);
        uint maxSize = (uint)StylusNative.GetCompressedBufferSize(input.Length);
        byte[] compressed = BrotliCompression.Compress(input, BrotliCompression.LevelWell);

        Assert.Throws<ArgumentException>(() => BrotliCompression.Decompress(compressed, maxSize, unknownDictionary));
    }

    [Test]
    public void Decompress_RandomInput_Throws()
    {
        byte[] input = RandomNumberGenerator.GetBytes(128);
        uint maxSize = (uint)StylusNative.GetCompressedBufferSize(input.Length);

        Assert.Throws<InvalidOperationException>(() => BrotliCompression.Decompress(input, maxSize));
    }

    [TestCase(BrotliCompression.Dictionary.EmptyDictionary)]
    [TestCase(BrotliCompression.Dictionary.StylusProgramDictionary)]
    public void CompressDecompress_Always_ReturnsOriginalData(BrotliCompression.Dictionary dictionary)
    {
        byte[] input = RandomNumberGenerator.GetBytes(128);
        uint maxSize = (uint)StylusNative.GetCompressedBufferSize(input.Length);

        byte[] compressed = BrotliCompression.Compress(input, 11, dictionary);
        byte[] decompressed = BrotliCompression.Decompress(compressed, maxSize, dictionary);

        decompressed.Should().BeEquivalentTo(input, o => o.WithStrictOrdering());
    }
}
