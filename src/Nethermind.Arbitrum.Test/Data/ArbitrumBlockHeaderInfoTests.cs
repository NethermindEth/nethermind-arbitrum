// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Logging;

using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Data
{
    [TestFixture]
    public class ArbitrumBlockHeaderInfoTests
    {
        private const ulong TEST_SEND_COUNT = 789UL;
        private const ulong TEST_L1_BLOCK_NUMBER = 456UL;
        private const ulong TEST_ARBOS_VERSION = 123UL;

        private ILogger _logger;
        private BlockHeader _validHeader = null!;
        private Hash256 _expectedSendRoot = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            _logger = LimboLogs.Instance.GetClassLogger();
            (_validHeader, _expectedSendRoot) = CreateValidBlockHeader();
        }

        [Test]
        public void Empty_ReturnsObjectWithZeroValues()
        {
            var empty = ArbitrumBlockHeaderInfo.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(empty.SendRoot, Is.EqualTo(Keccak.Zero));
                Assert.That(empty.ArbOSFormatVersion, Is.EqualTo(0UL));
                Assert.That(empty.L1BlockNumber, Is.EqualTo(0UL));
                Assert.That(empty.SendCount, Is.EqualTo(0UL));
            });
        }

        [Test]
        public void Deserialize_WithNullHeader_ReturnsEmpty()
        {
            var result = ArbitrumBlockHeaderInfo.Deserialize(null!, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [TestCase(0UL, "Zero difficulty")]
        [TestCase(2UL, "Non-one difficulty")]
        public void Deserialize_WithInvalidDifficulty_ReturnsEmpty(ulong difficulty, string description)
        {
            var header = CreateBlockHeader(new UInt256(difficulty));
            var result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty, description);
        }

        [Test]
        public void Deserialize_WithZeroBaseFee_ReturnsEmpty()
        {
            var header = CreateBlockHeader(UInt256.One, baseFee: UInt256.Zero);
            var result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [Test]
        public void Deserialize_WithInvalidExtraDataLength_ReturnsEmpty()
        {
            var header = CreateBlockHeader(UInt256.One, extraDataLength: 31);
            var result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [Test]
        public void Deserialize_WithValidHeader_DeserializesCorrectly()
        {
            var result = ArbitrumBlockHeaderInfo.Deserialize(_validHeader, _logger);

            Assert.Multiple(() =>
            {
                Assert.That(result.SendRoot, Is.EqualTo(_expectedSendRoot));
                Assert.That(result.ArbOSFormatVersion, Is.EqualTo(TEST_ARBOS_VERSION));
                Assert.That(result.L1BlockNumber, Is.EqualTo(TEST_L1_BLOCK_NUMBER));
                Assert.That(result.SendCount, Is.EqualTo(TEST_SEND_COUNT));
            });
        }

        [Test]
        public void Deserialize_WithNullMixHash_ReturnsEmpty()
        {
            var header = CreateBlockHeader(UInt256.One);
            header.MixHash = null;

            var result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        private static (BlockHeader header, Hash256 sendRoot) CreateValidBlockHeader()
        {
            var sendRoot = new byte[32];
            sendRoot[0] = 0x01;
            sendRoot[31] = 0xFF;

            var mixHash = new byte[32];
            TEST_SEND_COUNT.ToBigEndianByteArray().CopyTo(mixHash, 0);
            TEST_L1_BLOCK_NUMBER.ToBigEndianByteArray().CopyTo(mixHash, 8);
            TEST_ARBOS_VERSION.ToBigEndianByteArray().CopyTo(mixHash, 16);

            var header = new BlockHeader(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 0,
                gasLimit: 0,
                timestamp: 0,
                extraData: sendRoot);
            header.BaseFeePerGas = UInt256.One;
            header.MixHash = new Hash256(mixHash);

            return (header, new Hash256(sendRoot));
        }

        private static BlockHeader CreateBlockHeader(
            UInt256 difficulty,
            UInt256? baseFee = null,
            int extraDataLength = 32)
        {
            var header = new BlockHeader(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: difficulty,
                number: 0,
                gasLimit: 0,
                timestamp: 0,
                extraData: new byte[extraDataLength]);
            header.BaseFeePerGas = baseFee ?? UInt256.One;
            header.MixHash = new Hash256(new byte[32]);
            return header;
        }
    }
}
