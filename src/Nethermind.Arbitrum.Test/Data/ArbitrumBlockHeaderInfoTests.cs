// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Logging;

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
            _logger = LimboLogs.Instance.GetClassLogger<ArbitrumBlockHeaderInfoTests>();
            (_validHeader, _expectedSendRoot) = CreateValidBlockHeader();
        }

        [Test]
        public void Empty_WhenAccessed_ReturnsObjectWithZeroValues()
        {
            ArbitrumBlockHeaderInfo empty = ArbitrumBlockHeaderInfo.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(empty.SendRoot, Is.EqualTo(Keccak.Zero));
                Assert.That(empty.ArbOSFormatVersion, Is.EqualTo(0UL));
                Assert.That(empty.L1BlockNumber, Is.EqualTo(0UL));
                Assert.That(empty.SendCount, Is.EqualTo(0UL));
                Assert.That(empty.CollectTips, Is.False);
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
        public void Deserialize_WhenHeaderIsValid_ReturnsDeserializedInfo()
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
        public void Deserialize_WhenMixHashIsBigEndian_ReadsValuesCorrectly()
        {
            ulong sendCount = 0x0102030405060708;
            ulong l1BlockNumber = 0x090A0B0C0D0E0F10;
            ulong arbosVersion = 0x1112131415161718;

            byte[] mixHashBytes = new byte[32];

            using MemoryStream stream = new(mixHashBytes);
            using BinaryWriter writer = new(stream);

            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, sendCount);
            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, l1BlockNumber);
            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, arbosVersion);

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
        public void Deserialize_WhenValuesAreMaximum_ReadsCorrectly()
        {
            ulong sendCount = ulong.MaxValue;
            ulong l1BlockNumber = ulong.MaxValue - 1;
            ulong arbosVersion = ulong.MaxValue - 2;

            byte[] mixHashBytes = new byte[32];

            using MemoryStream stream = new(mixHashBytes);
            using BinaryWriter writer = new(stream);

            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, sendCount);
            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, l1BlockNumber);
            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, arbosVersion);

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
        public void Deserialize_WhenUsingBigEndianEncoding_MaintainsCorrectByteOrder()
        {
            ulong testValue = 0x0102030405060708;
            byte[] mixHashBytes = new byte[32];

            using MemoryStream stream = new(mixHashBytes);
            using BinaryWriter writer = new(stream);
            ArbitrumBinaryTestWriter.WriteULongBigEndian(writer, testValue);

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

        [Test]
        public void UpdateHeader_CollectTipsTrueOnV60_SetsBit25()
        {
            BlockHeader header = CreateBareHeader();
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                SendCount = 0,
                L1BlockNumber = 0,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = true,
            };

            ArbitrumBlockHeaderInfo.UpdateHeader(header, info);

            byte[] bytes = header.MixHash!.Bytes.ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(bytes[25], Is.EqualTo((byte)1));
                Assert.That(bytes[24], Is.EqualTo((byte)0));
                for (int i = 26; i < 32; i++)
                    Assert.That(bytes[i], Is.EqualTo((byte)0), $"byte {i} must remain zero");
            });
        }

        [Test]
        public void UpdateHeader_CollectTipsTrueOnV9_KeepsBit25Zero()
        {
            BlockHeader header = CreateBareHeader();
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                SendCount = 0,
                L1BlockNumber = 0,
                ArbOSFormatVersion = ArbosVersion.Nine,
                CollectTips = true,
            };

            ArbitrumBlockHeaderInfo.UpdateHeader(header, info);

            Assert.That(header.MixHash!.Bytes[25], Is.EqualTo((byte)0),
                "v9 carve-out: bit 25 must never be written on v9 blocks");
        }

        [Test]
        public void UpdateHeader_CollectTipsFalse_KeepsBit25Zero()
        {
            BlockHeader header = CreateBareHeader();
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                SendCount = 0,
                L1BlockNumber = 0,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = false,
            };

            ArbitrumBlockHeaderInfo.UpdateHeader(header, info);

            Assert.That(header.MixHash!.Bytes[25], Is.EqualTo((byte)0));
        }

        [Test]
        public void Deserialize_V9_AlwaysReadsCollectTipsTrue()
        {
            BlockHeader header = BuildHeaderWithVersionAndBit25(ArbosVersion.Nine, bit25Value: 0);
            ArbitrumBlockHeaderInfo info = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            Assert.That(info.CollectTips, Is.True, "v9 carve-out: deserialize always returns true regardless of bit 25");
        }

        [TestCase(ArbosVersion.Sixty, (byte)1, true)]
        [TestCase(ArbosVersion.Sixty, (byte)0, false)]
        [TestCase(ArbosVersion.FiftyOne, (byte)0, false)]
        [TestCase(ArbosVersion.FiftyOne, (byte)1, true, Description = "Nitro honors bit 25 for any version != 9")]
        public void Deserialize_FromBit25_ReadsCollectTips(ulong arbosVersion, byte bit25, bool expected)
        {
            BlockHeader header = BuildHeaderWithVersionAndBit25(arbosVersion, bit25);
            ArbitrumBlockHeaderInfo info = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            Assert.That(info.CollectTips, Is.EqualTo(expected));
        }

        [Test]
        public void Deserialize_OnlyLowBitOfByte25_Matters()
        {
            // Nitro uses (byte & 0x1) == 1; bit value 0x2 must NOT be read as CollectTips=true.
            BlockHeader header = BuildHeaderWithVersionAndBit25(ArbosVersion.Sixty, bit25Value: 0x2);
            ArbitrumBlockHeaderInfo info = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);
            Assert.That(info.CollectTips, Is.False);
        }

        [TestCase(ArbosVersion.Sixty, true)]
        [TestCase(ArbosVersion.Sixty, false)]
        [TestCase(ArbosVersion.FiftyOne, false)]
        [TestCase(ArbosVersion.FiftyOne, true)]
        public void RoundTrip_AnyVersion_PreservesCollectTipsBit(ulong arbosVersion, bool collectTips)
        {
            BlockHeader header = CreateBareHeader();
            ArbitrumBlockHeaderInfo original = new()
            {
                SendRoot = Hash256.Zero,
                SendCount = 42,
                L1BlockNumber = 7,
                ArbOSFormatVersion = arbosVersion,
                CollectTips = collectTips,
            };

            ArbitrumBlockHeaderInfo.UpdateHeader(header, original);
            ArbitrumBlockHeaderInfo decoded = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);

            Assert.That(decoded.CollectTips, Is.EqualTo(collectTips));
        }

        [Test]
        public void RoundTrip_V9_AlwaysDecodesAsCollect()
        {
            // v9 carve-out: encode writes 0, decode forces true regardless.
            BlockHeader header = CreateBareHeader();
            ArbitrumBlockHeaderInfo original = new()
            {
                SendRoot = Hash256.Zero,
                SendCount = 0,
                L1BlockNumber = 0,
                ArbOSFormatVersion = ArbosVersion.Nine,
                CollectTips = false,
            };

            ArbitrumBlockHeaderInfo.UpdateHeader(header, original);
            ArbitrumBlockHeaderInfo decoded = ArbitrumBlockHeaderInfo.Deserialize(header, _logger);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.CollectTips, Is.True, "v9 always decodes as collect=true");
                Assert.That(header.MixHash!.Bytes[25], Is.EqualTo((byte)0), "v9 never writes bit 25");
            });
        }

        [Test]
        public void ResolveEffectiveGasPrice_CollectTipsTrue_ReturnsBaseFeePlusTip()
        {
            BlockHeader header = CreateBareHeader(baseFee: 1.GWei);
            Transaction tx = CreateEip1559Transaction(maxPriorityFeePerGas: 100.Wei, maxFeePerGas: 1.GWei + 100.Wei);
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = true,
            };

            UInt256 effectiveGasPrice = info.ResolveEffectiveGasPrice(header, tx);

            effectiveGasPrice.Should().Be(1.GWei + 100.Wei);
        }

        [Test]
        public void ResolveEffectiveGasPrice_CollectTipsFalse_ReturnsBaseFee()
        {
            BlockHeader header = CreateBareHeader(baseFee: 1.GWei);
            Transaction tx = CreateEip1559Transaction(maxPriorityFeePerGas: 100.Wei, maxFeePerGas: 1.GWei + 100.Wei);
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = false,
            };

            UInt256 effectiveGasPrice = info.ResolveEffectiveGasPrice(header, tx);

            effectiveGasPrice.Should().Be(1.GWei);
        }

        [Test]
        public void ResolveEffectiveGasPrice_V9HistoricalBlock_ReturnsFullPrice()
        {
            BlockHeader header = CreateBareHeader(baseFee: 1.GWei);
            Transaction tx = CreateEip1559Transaction(maxPriorityFeePerGas: 100.Wei, maxFeePerGas: 1.GWei + 100.Wei);
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = ArbosVersion.Nine,
                CollectTips = true,
            };

            UInt256 effectiveGasPrice = info.ResolveEffectiveGasPrice(header, tx);

            effectiveGasPrice.Should().Be(1.GWei + 100.Wei);
        }

        [Test]
        public void ResolveEffectiveGasPrice_CollectTipsTrueWithZeroTip_ReturnsBaseFee()
        {
            BlockHeader header = CreateBareHeader(baseFee: 1.GWei);
            Transaction tx = CreateEip1559Transaction(maxPriorityFeePerGas: UInt256.Zero, maxFeePerGas: 1.GWei);
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = true,
            };

            UInt256 effectiveGasPrice = info.ResolveEffectiveGasPrice(header, tx);

            effectiveGasPrice.Should().Be(1.GWei);
        }

        [Test]
        public void ResolveEffectiveGasPrice_CollectTipsTrueWithLegacyTx_ReturnsTxGasPrice()
        {
            BlockHeader header = CreateBareHeader(baseFee: 1.GWei);
            UInt256 legacyGasPrice = 1.GWei + 50.Wei;
            Transaction tx = Build.A.Transaction
                .WithType(TxType.Legacy)
                .WithGasPrice(legacyGasPrice)
                .TestObject;
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = true,
            };

            UInt256 effectiveGasPrice = info.ResolveEffectiveGasPrice(header, tx);

            effectiveGasPrice.Should().Be(legacyGasPrice);
        }

        [Test]
        public void ResolveEffectiveGasPrice_CollectTipsTrueWithTipCappedByMaxFee_ReturnsMaxFee()
        {
            BlockHeader header = CreateBareHeader(baseFee: 1.GWei);
            UInt256 maxFee = 1.GWei + 30.Wei;
            Transaction tx = CreateEip1559Transaction(maxPriorityFeePerGas: 100.Wei, maxFeePerGas: maxFee);
            ArbitrumBlockHeaderInfo info = new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = ArbosVersion.Sixty,
                CollectTips = true,
            };

            UInt256 effectiveGasPrice = info.ResolveEffectiveGasPrice(header, tx);

            effectiveGasPrice.Should().Be(maxFee);
        }

        private static Transaction CreateEip1559Transaction(UInt256 maxPriorityFeePerGas, UInt256 maxFeePerGas) =>
            Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithMaxPriorityFeePerGas(maxPriorityFeePerGas)
                .WithMaxFeePerGas(maxFeePerGas)
                .TestObject;

        private static BlockHeader CreateBareHeader(UInt256? baseFee = null)
        {
            return new BlockHeader(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 1,
                gasLimit: 1_000_000,
                timestamp: 1_000,
                extraData: new byte[32])
            {
                BaseFeePerGas = baseFee ?? UInt256.One,
                MixHash = new Hash256(new byte[32]),
            };
        }

        private static BlockHeader BuildHeaderWithVersionAndBit25(ulong arbosVersion, byte bit25Value)
        {
            byte[] mixHashBytes = new byte[32];
            arbosVersion.ToBigEndianByteArray().CopyTo(mixHashBytes, 16);
            mixHashBytes[25] = bit25Value;

            return new BlockHeader(
                parentHash: Keccak.Zero,
                unclesHash: Keccak.Zero,
                beneficiary: Address.Zero,
                difficulty: UInt256.One,
                number: 1,
                gasLimit: 1_000_000,
                timestamp: 1_000,
                extraData: new byte[32])
            {
                BaseFeePerGas = UInt256.One,
                MixHash = new Hash256(mixHashBytes),
            };
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
