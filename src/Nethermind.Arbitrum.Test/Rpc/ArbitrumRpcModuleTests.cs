// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Moq;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.JsonRpc;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Rpc
{
    [TestFixture]
    public class ArbitrumRpcModuleTests
    {
        private const ulong GenesisBlockNum = 1000UL;

        private ArbitrumBlockTreeInitializer _initializer = null!;
        private Mock<IBlocksConfig> _blockConfigMock = null!;
        private Mock<IBlockTree> _blockTreeMock = null!;
        private Mock<IManualBlockProductionTrigger> _triggerMock = null!;
        private ArbitrumRpcTxSource _txSource = null!;
        private LimboLogs _logManager = null!;
        private ChainSpec _chainSpec = null!;
        private Mock<IArbitrumSpecHelper> _specHelper = null!;
        private ArbitrumRpcModule _rpcModule = null!;

        [SetUp]
        public void Setup()
        {
            Mock<IWorldStateManager> worldStateManagerMock = new();

            _blockConfigMock = new Mock<IBlocksConfig>();
            _blockTreeMock = new Mock<IBlockTree>();
            _triggerMock = new Mock<IManualBlockProductionTrigger>();
            _logManager = LimboLogs.Instance;
            _chainSpec = new ChainSpec();
            _specHelper = new Mock<IArbitrumSpecHelper>();
            _initializer = new ArbitrumBlockTreeInitializer(
                _chainSpec,
                FullChainSimulationSpecProvider.Instance,
                _specHelper.Object,
                worldStateManagerMock.Object,
                _blockTreeMock.Object,
                _blockConfigMock.Object,
                _logManager);

            _specHelper.SetupGet(x => x.GenesisBlockNum).Returns(GenesisBlockNum);
            _txSource = new ArbitrumRpcTxSource(_logManager);

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                _blockTreeMock.Object,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager);
        }

        [Test]
        public async Task ResultAtPos_BlockNumberOverflow_ReturnsFailResult()
        {
            ulong genesis = 100UL;
            ulong messageIndex = ulong.MaxValue - 50UL;

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesis);

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
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.FormatExceedsLongMax(messageIndex + GenesisBlockNum)));
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
        public async Task ResultAtPos_HasBlock_ReturnsCorrectResult()
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
        public async Task MessageIndexToBlockNumber_Always_ReturnsCorrectBlockNumber()
        {
            ulong messageIndex = 500UL;
            ulong genesisBlockNum = 1000UL;

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.MessageIndexToBlockNumber(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(genesisBlockNum + messageIndex));
            });
        }

        [Test]
        public async Task BlockNumberToMessageIndex_Always_ReturnsCorrectMessageIndex()
        {
            ulong blockNumber = 50UL;
            ulong genesisBlockNum = 10UL;

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.BlockNumberToMessageIndex(blockNumber);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(blockNumber - genesisBlockNum));
            });
        }

        [Test]
        public async Task BlockNumberToMessageIndex_BlockNumberIsLowerThanGenesis_Fails()
        {
            ulong blockNumber = 9UL;
            ulong genesisBlockNum = 10UL;

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.BlockNumberToMessageIndex(blockNumber);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo($"blockNumber {blockNumber} < genesis {genesisBlockNum}"));
            });
        }

        [Test]
        public async Task HeadMessageNumber_Always_ReturnsHeadMessageIndex()
        {
            ulong blockNumber = 1UL;

            Block genesis = Build.A.Block.Genesis.TestObject;
            BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
            Block newBlock = Build.A.Block.WithParent(genesis).WithNumber((long)blockNumber).TestObject;
            blockTree.SuggestBlock(newBlock);
            blockTree.UpdateMainChain(newBlock);

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager);

            _specHelper.Setup(c => c.GenesisBlockNum).Returns((ulong)genesis.Number);

            var result = await _rpcModule.HeadMessageNumber();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(blockNumber - (ulong)genesis.Number));
            });
        }

        [Test]
        public async Task HeadMessageNumber_HasNoBlocks_NoLatestHeaderFound()
        {
            var blockTree = Build.A.BlockTree().TestObject;

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager);

            var result = await _rpcModule.HeadMessageNumber();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo("Failed to get latest header"));
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.InternalError));
            });
        }

        [Test]
        public async Task HeadMessageNumber_BlockNumberIsLowerThanGenesis_Fails()
        {
            ulong blockNumber = 1UL;
            ulong genesisBlockNum = 10UL;

            Block genesis = Build.A.Block.Genesis.TestObject;
            BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
            Block newBlock = Build.A.Block.WithParent(genesis).WithNumber((long)blockNumber).TestObject;
            blockTree.SuggestBlock(newBlock);
            blockTree.UpdateMainChain(newBlock);

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager);

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.HeadMessageNumber();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo($"blockNumber {blockNumber} < genesis {genesisBlockNum}"));
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.InternalError));
            });
        }

        [Test]
        public void DigestInitMessage_IsNotInitialized_ProducesGenesisBlock()
        {
            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);

            ResultWrapper<MessageResult> result = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            result.Data.Should().BeEquivalentTo(new MessageResult
            {
                BlockHash = new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"),
                SendRoot = Hash256.Zero
            });
        }

        [Test]
        public void DigestInitMessage_AlreadyInitialized_Throws()
        {
            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);

            // Produce genesis block
            _ = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            // Call again to ensure it throws
            chain.Invoking(c => c.ArbitrumRpcModule.DigestInitMessage(initMessage))
                .Should()
                .Throw<InvalidOperationException>();
        }

        [Test]
        public void DigestInitMessage_InvalidInitialL1BaseFee_ReturnsInvalidParamsError()
        {
            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            DigestInitMessage initMessage = new(UInt256.Zero, FullChainSimulationInitMessage.GetSerializedChainConfigBase64Bytes());

            ResultWrapper<MessageResult> result = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            result.Result.ResultType.Should().Be(ResultType.Failure);
            result.ErrorCode.Should().Be(ErrorCodes.InvalidParams);
        }

        public static IEnumerable<TestCaseData> InvalidSerializedChainConfigCases()
        {
            yield return new TestCaseData<byte[]>(null!);
            yield return new TestCaseData<byte[]>([]);
            yield return new TestCaseData<byte[]>("?"u8.ToArray());
        }

        [TestCaseSource(nameof(InvalidSerializedChainConfigCases))]
        public void DigestInitMessage_InvalidSerializedChainConfig_ReturnsInvalidParamsError(byte[]? invalidSerializedChainConfig)
        {
            DigestInitMessage initMessage = new(UInt256.One, invalidSerializedChainConfig);
            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();

            ResultWrapper<MessageResult> result = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            result.Result.ResultType.Should().Be(ResultType.Failure);
            result.ErrorCode.Should().Be(ErrorCodes.InvalidParams);
        }
    }
}
