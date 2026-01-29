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
using Nethermind.JsonRpc;
using Nethermind.Arbitrum.Execution;
using Nethermind.Consensus.Processing;
using Nethermind.Core.Specs;
using Nethermind.Arbitrum.Execution.Stateless;

namespace Nethermind.Arbitrum.Test.Rpc
{
    [TestFixture]
    public abstract class ArbitrumRpcModuleTests
    {
        private const ulong GenesisBlockNum = 1000UL;

        private ArbitrumBlockTreeInitializer _initializer = null!;
        private IBlocksConfig _blockConfig = null!;
        private Mock<IBlockTree> _blockTreeMock = null!;
        private Mock<IManualBlockProductionTrigger> _triggerMock = null!;
        private ArbitrumRpcTxSource _txSource = null!;
        private LimboLogs _logManager = null!;
        private ChainSpec _chainSpec = null!;
        private Mock<IArbitrumSpecHelper> _specHelper = null!;
        private ArbitrumRpcModule _rpcModule = null!;
        private Mock<IBlockProcessingQueue> _blockProcessingQueue = null!;
        private IArbitrumConfig _arbitrumConfig = null!;
        private Mock<IMainProcessingContext> _mainProcessingContextMock = null!;
        private ISpecProvider _specProvider = null!;
        private Mock<IArbitrumWitnessGeneratingBlockProcessingEnvFactory> _witnessGeneratingBlockProcessingEnvFactory = null!;
        [SetUp]
        public void Setup()
        {
            _mainProcessingContextMock = new Mock<IMainProcessingContext>();
            _blockConfig = new BlocksConfig();
            _blockConfig.BuildBlocksOnMainState = true;
            _blockTreeMock = new Mock<IBlockTree>();
            _triggerMock = new Mock<IManualBlockProductionTrigger>();
            _logManager = LimboLogs.Instance;
            _chainSpec = FullChainSimulationChainSpecProvider.Create();
            _specHelper = new Mock<IArbitrumSpecHelper>();
            _blockProcessingQueue = new Mock<IBlockProcessingQueue>();
            _specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(_chainSpec);
            _witnessGeneratingBlockProcessingEnvFactory = new Mock<IArbitrumWitnessGeneratingBlockProcessingEnvFactory>();

            _initializer = new ArbitrumBlockTreeInitializer(
                _chainSpec,
                _specProvider,
                _specHelper.Object,
                _mainProcessingContextMock.Object,
                _blockTreeMock.Object,
                _blockConfig,
                _logManager);

            _specHelper.SetupGet(x => x.GenesisBlockNum).Returns(GenesisBlockNum);
            _txSource = new ArbitrumRpcTxSource(_logManager);

            CachedL1PriceData cachedL1PriceData = new(_logManager);

            _arbitrumConfig = new ArbitrumConfig();

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                _blockTreeMock.Object,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager,
                cachedL1PriceData,
                _blockProcessingQueue.Object,
                _arbitrumConfig,
                _witnessGeneratingBlockProcessingEnvFactory.Object,
                _blockConfig);
        }

        [Test]
        public async Task ResultAtMessageIndex_BlockNumberOverflow_ReturnsFailResult()
        {
            ulong genesis = 100UL;
            ulong messageIndex = ulong.MaxValue - 50UL;

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesis);

            var result = await _rpcModule.ResultAtMessageIndex(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.Overflow));
            });
        }

        [Test]
        public async Task ResultAtMessageIndex_BlockNumberExceedsMaxValue_ReturnsFailResult()
        {
            ulong messageIndex = ulong.MaxValue - 5000UL;

            var result = await _rpcModule.ResultAtMessageIndex(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.Overflow));
            });
        }

        [Test]
        public async Task ResultAtMessageIndex_BlockNotFound_ReturnsFailResult()
        {
            ulong messageIndex = 10000UL;
            ulong blockNumber = 11000UL;

            _blockTreeMock.Setup(b => b.FindHeader((long)blockNumber, BlockTreeLookupOptions.None))
                .Returns((BlockHeader?)null);

            var result = await _rpcModule.ResultAtMessageIndex(messageIndex);

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.BlockNotFound((long)blockNumber)));
            });
        }

        [Test]
        public async Task ResultAtMessageIndex_HasBlock_ReturnsCorrectResult()
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

            _blockTreeMock.Setup(x => x.FindHeader((long)blockNumber, BlockTreeLookupOptions.RequireCanonical))
                .Returns(header);

            var result = await _rpcModule.ResultAtMessageIndex(messageIndex);

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
        public async Task HeadMessageIndex_Always_ReturnsHeadMessageIndex()
        {
            ulong blockNumber = 1UL;

            Block genesis = Build.A.Block.Genesis.TestObject;
            BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
            Block newBlock = Build.A.Block.WithParent(genesis).WithNumber((long)blockNumber).TestObject;
            blockTree.SuggestBlock(newBlock);
            blockTree.UpdateMainChain(newBlock);

            CachedL1PriceData cachedL1PriceData = new(_logManager);

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager,
                cachedL1PriceData,
                _blockProcessingQueue.Object,
                _arbitrumConfig,
                _witnessGeneratingBlockProcessingEnvFactory.Object,
                _blockConfig);

            _specHelper.Setup(c => c.GenesisBlockNum).Returns((ulong)genesis.Number);

            var result = await _rpcModule.HeadMessageIndex();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Success));
                Assert.That(result.Data, Is.EqualTo(blockNumber - (ulong)genesis.Number));
            });
        }

        [Test]
        public async Task HeadMessageIndex_HasNoBlocks_NoLatestHeaderFound()
        {
            var blockTree = Build.A.BlockTree().TestObject;

            CachedL1PriceData cachedL1PriceData = new(_logManager);

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager,
                cachedL1PriceData,
                _blockProcessingQueue.Object,
                _arbitrumConfig,
                _witnessGeneratingBlockProcessingEnvFactory.Object,
                _blockConfig);

            var result = await _rpcModule.HeadMessageIndex();

            Assert.Multiple(() =>
            {
                Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
                Assert.That(result.Result.Error, Is.EqualTo("Failed to get latest header"));
                Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.InternalError));
            });
        }

        [Test]
        public async Task HeadMessageIndex_BlockNumberIsLowerThanGenesis_Fails()
        {
            ulong blockNumber = 1UL;
            ulong genesisBlockNum = 10UL;

            Block genesis = Build.A.Block.Genesis.TestObject;
            BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
            Block newBlock = Build.A.Block.WithParent(genesis).WithNumber((long)blockNumber).TestObject;
            blockTree.SuggestBlock(newBlock);
            blockTree.UpdateMainChain(newBlock);

            CachedL1PriceData cachedL1PriceData = new(_logManager);

            _rpcModule = new ArbitrumRpcModule(
                _initializer,
                blockTree,
                _triggerMock.Object,
                _txSource,
                _chainSpec,
                _specHelper.Object,
                _logManager,
                cachedL1PriceData,
                _blockProcessingQueue.Object,
                _arbitrumConfig,
                _witnessGeneratingBlockProcessingEnvFactory.Object,
                _blockConfig);

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(genesisBlockNum);

            var result = await _rpcModule.HeadMessageIndex();

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
        public void DigestInitMessage_AlreadyInitialized_ReturnsGenesisResult()
        {
            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);

            // Produce genesis block
            ResultWrapper<MessageResult> firstCallResult = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            // Call again with the same init message
            ResultWrapper<MessageResult> secondCallResult = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            secondCallResult.Data.Should().BeEquivalentTo(new MessageResult
            {
                BlockHash = new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"),
                SendRoot = Hash256.Zero
            });
            firstCallResult.Should().BeEquivalentTo(secondCallResult);
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

        [Test]
        public void Synced_WithSyncedState_ReturnsTrue()
        {
            ResultWrapper<bool> result = _rpcModule.Synced();

            result.Should().NotBeNull();
            result.Result.ResultType.Should().Be(ResultType.Success);
        }

        [Test]
        public void FullSyncProgressMap_Always_ReturnsProgressMap()
        {
            ResultWrapper<Dictionary<string, object>> result = _rpcModule.FullSyncProgressMap();

            result.Should().NotBeNull();
            result.Result.ResultType.Should().Be(ResultType.Success);
            result.Data.Should().NotBeNull();
            result.Data.Should().ContainKey("consensusMaxMessageCount");
            result.Data.Should().ContainKey("executionSyncTarget");
        }

        [Test]
        public async Task ArbOSVersionForMessageIndex_WhenMessageIndexCausesOverflow_ReturnsFailResult()
        {
            ulong messageIndex = ulong.MaxValue;

            _specHelper.Setup(c => c.GenesisBlockNum).Returns(GenesisBlockNum);

            ResultWrapper<ulong> result = await _rpcModule.ArbOSVersionForMessageIndex(messageIndex);

            Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
            Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.Overflow));
        }

        [Test]
        public async Task ArbOSVersionForMessageIndex_WhenBlockNotFound_ReturnsFailResult()
        {
            ulong messageIndex = 100UL;
            long expectedBlockNumber = (long)(GenesisBlockNum + messageIndex);

            _blockTreeMock.Setup(b => b.FindHeader(expectedBlockNumber, BlockTreeLookupOptions.RequireCanonical))
                .Returns((BlockHeader?)null);

            ResultWrapper<ulong> result = await _rpcModule.ArbOSVersionForMessageIndex(messageIndex);

            Assert.That(result.Result.ResultType, Is.EqualTo(ResultType.Failure));
            Assert.That(result.Result.Error, Does.Contain(ArbitrumRpcErrors.BlockNotFound(expectedBlockNumber)));
        }

        [Test]
        public async Task ArbOSVersionForMessageIndex_WhenBlockExists_ReturnsCorrectArbOSVersion()
        {
            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);

            chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

            TestL2Transactions message = new(
                new UInt256(1000000000),
                TestItem.AddressA,
                Build.A.Transaction.SignedAndResolved().TestObject
            );

            await chain.Digest(message);

            ulong messageIndex = 1;
            ResultWrapper<ulong> versionResult = await chain.ArbitrumRpcModule.ArbOSVersionForMessageIndex(messageIndex);

            Assert.That(versionResult.Result.ResultType, Is.EqualTo(ResultType.Success));

            ResultWrapper<MessageResult> blockResult = await chain.ArbitrumRpcModule.ResultAtMessageIndex(messageIndex);
            BlockHeader? header = chain.BlockTree.FindHeader(blockResult.Data.BlockHash);
            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header!, LimboLogs.Instance.GetClassLogger());

            Assert.That(versionResult.Data, Is.EqualTo(headerInfo.ArbOSFormatVersion));
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
