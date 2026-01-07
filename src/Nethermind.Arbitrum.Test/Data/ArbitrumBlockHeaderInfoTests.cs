// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
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
            ArbitrumBlockHeaderInfo empty = ArbitrumBlockHeaderInfo.Empty;

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
            ArbitrumBlockHeaderInfo result = ArbitrumBlockHeaderInfo.Deserialize(null!, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [TestCase(0UL, "Zero difficulty")]
        [TestCase(2UL, "Non-one difficulty")]
        public void Deserialize_WithInvalidDifficulty_ReturnsEmpty(ulong difficulty, string description)
        {
            BlockHeader header = CreateBlockHeader(new UInt256(difficulty));
            ArbitrumBlockHeaderInfo result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty, description);
        }

        [Test]
        public void Deserialize_WithZeroBaseFee_ReturnsEmpty()
        {
            BlockHeader header = CreateBlockHeader(UInt256.One, baseFee: UInt256.Zero);
            ArbitrumBlockHeaderInfo result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [Test]
        public void Deserialize_WithInvalidExtraDataLength_ReturnsEmpty()
        {
            BlockHeader header = CreateBlockHeader(UInt256.One, extraDataLength: 31);
            ArbitrumBlockHeaderInfo result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [Test]
        public void Deserialize_WithValidHeader_DeserializesCorrectly()
        {
            ArbitrumBlockHeaderInfo result = ArbitrumBlockHeaderInfo.Deserialize(_validHeader, _logger);

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
            BlockHeader header = CreateBlockHeader(UInt256.One);
            header.MixHash = null;

            ArbitrumBlockHeaderInfo result = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            result.Should().BeEquivalentTo(ArbitrumBlockHeaderInfo.Empty);
        }

        [Test]
        public void Deserialize_WithBigEndianMixHash_ReadsValuesCorrectly()
        {
            ulong sendCount = 0x0102030405060708;
            ulong l1BlockNumber = 0x090A0B0C0D0E0F10;
            ulong arbosVersion = 0x1112131415161718;

            byte[] mixHashBytes = new byte[32];

            using MemoryStream stream = new(mixHashBytes);
            using BinaryWriter writer = new(stream);

            ArbitrumBinaryWriter.WriteULongBigEndian(writer, sendCount);
            ArbitrumBinaryWriter.WriteULongBigEndian(writer, l1BlockNumber);
            ArbitrumBinaryWriter.WriteULongBigEndian(writer, arbosVersion);

            byte[] sendRootBytes = new byte[32];
            sendRootBytes[0] = 0xAB;
            sendRootBytes[31] = 0xCD;
            Hash256 sendRoot = new(sendRootBytes);

            BlockHeader header = new(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 1,
                gasLimit: 1000000,
                timestamp: 1000,
                extraData: sendRoot.Bytes.ToArray()
            )
            {
                BaseFeePerGas = UInt256.One,
                MixHash = new Hash256(mixHashBytes)
            };

            ArbitrumBlockHeaderInfo info = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);

            Assert.Multiple(() =>
            {
                Assert.That(info.SendCount, Is.EqualTo(sendCount), "SendCount should be read correctly in big-endian");
                Assert.That(info.L1BlockNumber, Is.EqualTo(l1BlockNumber), "L1BlockNumber should be read correctly in big-endian");
                Assert.That(info.ArbOSFormatVersion, Is.EqualTo(arbosVersion), "ArbOSFormatVersion should be read correctly in big-endian");
                Assert.That(info.SendRoot, Is.EqualTo(sendRoot), "SendRoot should match");
            });
        }

        [Test]
        public void Deserialize_WithMaxValues_ReadsCorrectly()
        {
            ulong sendCount = ulong.MaxValue;
            ulong l1BlockNumber = ulong.MaxValue - 1;
            ulong arbosVersion = ulong.MaxValue - 2;

            byte[] mixHashBytes = new byte[32];

            using MemoryStream stream = new(mixHashBytes);
            using BinaryWriter writer = new(stream);

            ArbitrumBinaryWriter.WriteULongBigEndian(writer, sendCount);
            ArbitrumBinaryWriter.WriteULongBigEndian(writer, l1BlockNumber);
            ArbitrumBinaryWriter.WriteULongBigEndian(writer, arbosVersion);

            Span<byte> sendRootBytes = Keccak.Compute("test").Bytes;

            BlockHeader header = new(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 1,
                gasLimit: 1000000,
                timestamp: 1000,
                extraData: sendRootBytes.ToArray()
            )
            {
                BaseFeePerGas = UInt256.One,
                MixHash = new Hash256(mixHashBytes)
            };

            ArbitrumBlockHeaderInfo info = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);

            Assert.Multiple(() =>
            {
                Assert.That(info.SendCount, Is.EqualTo(sendCount));
                Assert.That(info.L1BlockNumber, Is.EqualTo(l1BlockNumber));
                Assert.That(info.ArbOSFormatVersion, Is.EqualTo(arbosVersion));
            });
        }

        [Test]
        public void Deserialize_VerifyBigEndianByteOrder()
        {
            ulong testValue = 0x0102030405060708;
            byte[] mixHashBytes = new byte[32];

            using MemoryStream stream = new(mixHashBytes);
            using BinaryWriter writer = new(stream);
            ArbitrumBinaryWriter.WriteULongBigEndian(writer, testValue);

            Assert.Multiple(() =>
            {
                Assert.That(mixHashBytes[0], Is.EqualTo(0x01), "First byte should be most significant");
                Assert.That(mixHashBytes[1], Is.EqualTo(0x02));
                Assert.That(mixHashBytes[2], Is.EqualTo(0x03));
                Assert.That(mixHashBytes[3], Is.EqualTo(0x04));
                Assert.That(mixHashBytes[4], Is.EqualTo(0x05));
                Assert.That(mixHashBytes[5], Is.EqualTo(0x06));
                Assert.That(mixHashBytes[6], Is.EqualTo(0x07));
                Assert.That(mixHashBytes[7], Is.EqualTo(0x08), "Last byte should be least significant");
            });

            BlockHeader header = new(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 1,
                gasLimit: 1000000,
                timestamp: 1000,
                extraData: new byte[32]
            )
            {
                BaseFeePerGas = UInt256.One,
                MixHash = new Hash256(mixHashBytes)
            };

            ArbitrumBlockHeaderInfo info = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            Assert.That(info.SendCount, Is.EqualTo(testValue));
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

            BlockHeader header = new(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 0,
                gasLimit: 0,
                timestamp: 0,
                extraData: sendRoot)
            {
                BaseFeePerGas = UInt256.One,
                MixHash = new Hash256(mixHash)
            };

            return (header, new Hash256(sendRoot));
        }

        private static BlockHeader CreateBlockHeader(
            UInt256 difficulty,
            UInt256? baseFee = null,
            int extraDataLength = 32)
        {
            BlockHeader header = new(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: difficulty,
                number: 0,
                gasLimit: 0,
                timestamp: 0,
                extraData: new byte[extraDataLength])
            {
                BaseFeePerGas = baseFee ?? UInt256.One,
                MixHash = new Hash256(new byte[32])
            };
            return header;
        }
    }
}
