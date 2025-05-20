// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading.Tasks;
using Moq;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Modules
{
    [TestFixture]
    public class ArbitrumRpcModuleTests
    {
        private Mock<IArbitrumConfig> _configMock = null!;
        private Mock<IBlockTree> _blockTreeMock = null!;
        private Mock<IManualBlockProductionTrigger> _triggerMock = null!;
        private ArbitrumRpcTxSource _txSource = null!;
        private LimboLogs _logManager = null!;
        private ChainSpec _chainSpec = null!;
        private ArbitrumRpcModule _rpcModule = null!;
        private const ulong genesisBlockNum = 1000UL;

        [SetUp]
        public void Setup()
        {
            _configMock = new Mock<IArbitrumConfig>();
            _blockTreeMock = new Mock<IBlockTree>();
            _triggerMock = new Mock<IManualBlockProductionTrigger>();
            _logManager = LimboLogs.Instance;
            _chainSpec = new ChainSpec();

            _configMock.SetupGet(x => x.GenesisBlockNum).Returns(genesisBlockNum);
            _txSource = new ArbitrumRpcTxSource(_logManager.GetClassLogger());

            _rpcModule = new ArbitrumRpcModule(
                _blockTreeMock.Object,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _configMock.Object,
                _logManager.GetClassLogger());
        }

        [Test]
        public async Task ResultAtPos_BlockNumberOverflow_ReturnsFailResult()
        {
            ulong genesis = 100UL;
            ulong messageIndex = ulong.MaxValue - 50UL;
            _configMock.Setup(c => c.GenesisBlockNum).Returns(genesis);

            var result = await _rpcModule.ResultAtPos(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.Overflow));
            });
        }

        [Test]
        public async Task ResultAtPos_BlockNumberExceedsMaxValue_ReturnsFailResult()
        {
            ulong messageIndex = ulong.MaxValue - 5000UL;

            var result = await _rpcModule.ResultAtPos(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.FormatExceedsLongMax(messageIndex + genesisBlockNum)));
            });
        }

        [Test]
        public async Task ResultAtPos_BlockNotFound_ReturnsFailResult()
        {
            ulong messageIndex = 10000UL;
            ulong blockNumber = 11000UL;

            _blockTreeMock.Setup(b => b.FindHeader((long)blockNumber, BlockTreeLookupOptions.None))
                .Returns((BlockHeader?)null);

            var result = await _rpcModule.ResultAtPos(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.BlockNotFound));
            });
        }

        [Test]
        public async Task ResultAtPos_Success_ReturnsCorrectResult()
        {
            ulong messageIndex = 10000UL;
            ulong blockNumber = messageIndex + 1000UL;
            var expectedBlockHash = TestItem.KeccakA;
            var expectedSendRoot = TestItem.KeccakB;

            // Create MixHash with correct values
            var mixHashBytes = new byte[32];
            BitConverter.GetBytes(789UL).CopyTo(mixHashBytes, 0); // SendCount
            BitConverter.GetBytes(456UL).CopyTo(mixHashBytes, 8); // L1BlockNumber
            BitConverter.GetBytes(123UL).CopyTo(mixHashBytes, 16); // ArbOSFormatVersion
            var mixHash = new Hash256(mixHashBytes);

            var header = Build.A.BlockHeader
                .WithNumber((long)blockNumber)
                .WithHash(expectedBlockHash)
                .WithExtraData(expectedSendRoot.Bytes.ToArray())
                .WithDifficulty(UInt256.One)
                .TestObject;
            header.BaseFeePerGas = UInt256.One;

            _blockTreeMock.Setup(x => x.FindHeader((long)blockNumber, BlockTreeLookupOptions.None))
                .Returns(header);

            var result = await _rpcModule.ResultAtPos(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data.BlockHash, Is.EqualTo(expectedBlockHash));
                Assert.That(result.Data.SendRoot, Is.EqualTo(expectedSendRoot));
            });
        }

        [Test]
        public async Task MessageIndexToBlockNumber_ReturnsCorrectBlockNumber()
        {
            ulong messageIndex = 500UL;
            ulong genesisBlockNum = 1000UL;

            _configMock.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.MessageIndexToBlockNumber(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(genesisBlockNum + messageIndex));
            });
        }
    }
}
