// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Moq;
using Nethermind.Arbitrum.Config;
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
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Rpc
{
    [TestFixture]
    public class ArbitrumRpcModuleTests
    {
        private Mock<IArbitrumSpecHelper> _specHelperMock = null!;
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
            _specHelperMock = new Mock<IArbitrumSpecHelper>();
            _blockTreeMock = new Mock<IBlockTree>();
            _triggerMock = new Mock<IManualBlockProductionTrigger>();
            _logManager = LimboLogs.Instance;
            _chainSpec = new ChainSpec();

            _specHelperMock.SetupGet(x => x.GenesisBlockNum).Returns(genesisBlockNum);
            _txSource = new ArbitrumRpcTxSource(_logManager.GetClassLogger());

            _rpcModule = new ArbitrumRpcModule(
                _blockTreeMock.Object,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelperMock.Object,
                _logManager.GetClassLogger());
        }

        [Test]
        public async Task ResultAtPos_BlockNumberOverflow_ReturnsFailResult()
        {
            ulong genesis = 100UL;
            ulong messageIndex = ulong.MaxValue - 50UL;
            _specHelperMock.Setup(c => c.GenesisBlockNum).Returns(genesis);

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

            _specHelperMock.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.MessageIndexToBlockNumber(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(genesisBlockNum + messageIndex));
            });
        }

        [Test]
        public async Task BlockNumberToMessageIndex_Success_ReturnsMessageIndex()
        {
            ulong blockNumber = 50UL;
            ulong genesisBlockNum = 10UL;

            _specHelperMock.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.BlockNumberToMessageIndex(blockNumber);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(blockNumber - genesisBlockNum));
            });
        }

        [Test]
        public async Task BlockNumberToMessageIndex_Failure_BlockNumberIsLowerThanGenesis()
        {
            ulong blockNumber = 9UL;
            ulong genesisBlockNum = 10UL;

            _specHelperMock.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.BlockNumberToMessageIndex(blockNumber);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo($"blockNumber {blockNumber} < genesis {genesisBlockNum}"));
            });
        }

        [Test]
        public async Task HeadMessageNumber_Success_ReturnsHeadMessageIndex()
        {
            ulong blockNumber = 1UL;

            Block genesis = Build.A.Block.Genesis.TestObject;
            BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
            Block newBlock = Build.A.Block.WithParent(genesis).WithNumber((long)blockNumber).TestObject;
            blockTree.SuggestBlock(newBlock);
            blockTree.UpdateMainChain(newBlock);

            _rpcModule = new ArbitrumRpcModule(
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _configMock.Object,
                _logManager.GetClassLogger());

            _configMock.Setup(c => c.GenesisBlockNum).Returns((ulong)genesis.Number);

            var result = await _rpcModule.HeadMessageNumber();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(blockNumber - (ulong)genesis.Number));
            });
        }

        [Test]
        public async Task HeadMessageNumber_Failure_NoLatestHeaderFound()
        {
            var blockTree = Build.A.BlockTree().TestObject;

            _rpcModule = new ArbitrumRpcModule(
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _configMock.Object,
                _logManager.GetClassLogger());

            var result = await _rpcModule.HeadMessageNumber();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo("Failed to get latest header"));
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.InternalError));
            });
        }

        [Test]
        public async Task HeadMessageNumber_Failure_BlockNumberIsLowerThanGenesis()
        {
            ulong blockNumber = 1UL;
            ulong genesisBlockNum = 10UL;

            Block genesis = Build.A.Block.Genesis.TestObject;
            BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
            Block newBlock = Build.A.Block.WithParent(genesis).WithNumber((long)blockNumber).TestObject;
            blockTree.SuggestBlock(newBlock);
            blockTree.UpdateMainChain(newBlock);

            _rpcModule = new ArbitrumRpcModule(
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _configMock.Object,
                _logManager.GetClassLogger());

            _configMock.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.HeadMessageNumber();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo($"blockNumber {blockNumber} < genesis {genesisBlockNum}"));
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.InternalError));
            });
        }
    }
}
