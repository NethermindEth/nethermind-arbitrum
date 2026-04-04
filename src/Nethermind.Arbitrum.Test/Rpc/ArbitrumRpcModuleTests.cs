// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ArbitrumRpcModuleTests
{
    [Test]
    public async Task ResultAtMessageIndex_BlockNumberOverflow_ReturnsFailResult()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ulong messageIndex = ulong.MaxValue - 50UL;

        ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(messageIndex);

        result.Should().RequestFail(ArbitrumRpcErrors.Overflow);
    }

    [Test]
    public async Task ResultAtMessageIndex_BlockNumberExceedsMaxValue_ReturnsFailResult()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ulong messageIndex = ulong.MaxValue - 5000UL;

        ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(messageIndex);

        result.Should().RequestFail(ArbitrumRpcErrors.Overflow);
    }

    [Test]
    public async Task ResultAtMessageIndex_BlockNotFound_ReturnsFailResult()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 32)
            .Build();

        ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(99999);

        long expectedBlockNumber = (long)(chain.SpecHelper.GenesisBlockNum + 99999);
        result.Should().RequestFail(ArbitrumRpcErrors.BlockNotFound(expectedBlockNumber));
    }

    [Test]
    public async Task ResultAtMessageIndex_HasBlock_ReturnsCorrectResult()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 32)
            .Build();

        await chain.Digest(new TestL2Transactions(92, TestItem.AddressA, Build.A.Transaction.SignedAndResolved().TestObject));

        ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(1);

        result.Should().RequestSucceed();
        result.Data.BlockHash.Should().Be(chain.BlockTree.Head!.Hash!);
        result.Data.SendRoot.Should().NotBeNull();
    }

    [Test]
    public async Task ResultAtMessageIndex_BlockFinalized_ReturnsCorrectResult()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), 8)
            .Build();

        ResultWrapper<ulong> headResult = await chain.ArbitrumRpcModule.HeadMessageIndex();
        headResult.Should().RequestSucceed();
        headResult.Data.Should().Be(8UL);

        Hash256 lastBlockHash = chain.BlockTree.Head!.Hash!;

        SetFinalityDataParams finalityParams = new()
        {
            SafeFinalityData = new RpcFinalityData { MsgIdx = 8UL, BlockHash = lastBlockHash },
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 8UL, BlockHash = lastBlockHash }
        };

        chain.ArbitrumRpcModule.SetFinalityData(finalityParams).Should().RequestSucceed();
        chain.ArbitrumRpcModule.MarkFeedStart(8UL).Should().RequestSucceed();

        ResultWrapper<MessageResult> resultAtIndex = await chain.ArbitrumRpcModule.ResultAtMessageIndex(7UL);
        resultAtIndex.Should().RequestSucceed();
        resultAtIndex.Data.BlockHash.Should().NotBeNull();
        resultAtIndex.Data.SendRoot.Should().NotBeNull();
    }

    [Test]
    public async Task MessageIndexToBlockNumber_Always_ReturnsCorrectBlockNumber()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<long> result = await chain.ArbitrumRpcModule.MessageIndexToBlockNumber(0);

        result.Should().RequestSucceed();
        result.Data.Should().Be((long)chain.SpecHelper.GenesisBlockNum);
    }

    [Test]
    public async Task BlockNumberToMessageIndex_Always_ReturnsCorrectMessageIndex()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ulong genesisBlockNum = chain.SpecHelper.GenesisBlockNum;

        ResultWrapper<ulong> result = await chain.ArbitrumRpcModule.BlockNumberToMessageIndex(genesisBlockNum);

        result.Should().RequestSucceed();
        result.Data.Should().Be(0UL);
    }

    [Test]
    public async Task BlockNumberToMessageIndex_BlockNumberIsLowerThanGenesis_Fails()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
        chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>()
            .GenesisBlockNum = 10;
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(chainSpec: chainSpec);

        ResultWrapper<ulong> result = await chain.ArbitrumRpcModule.BlockNumberToMessageIndex(0);

        result.Should().RequestFail("blockNumber 0 < genesis 10");
    }

    [Test]
    public async Task HeadMessageIndex_AfterDigestingBlocks_ReturnsCorrectIndex()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 32)
            .Build();

        await chain.Digest(new TestL2Transactions(92, TestItem.AddressA, Build.A.Transaction.SignedAndResolved().TestObject));

        ResultWrapper<ulong> result = await chain.ArbitrumRpcModule.HeadMessageIndex();

        result.Should().RequestSucceed();
        result.Data.Should().Be(1UL);
    }

    [Test]
    public async Task HeadMessageIndex_BeforeArbitrumGenesisDigested_Fails()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
        chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>()
            .GenesisBlockNum = 10;
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithChainSpec(chainSpec)
            .Build();

        ResultWrapper<ulong> result = await chain.ArbitrumRpcModule.HeadMessageIndex();

        result.Should().RequestFail("Failed to get latest header");
    }

    [Test]
    public void Synced_WithSyncedState_ReturnsTrue()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<bool> result = chain.ArbitrumRpcModule.Synced();

        result.Should().RequestSucceed();
    }

    [Test]
    public void FullSyncProgressMap_EmptyChain_ReturnsZeroes()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 32)
            .Build();

        ResultWrapper<Dictionary<string, object>> result = chain.ArbitrumRpcModule.FullSyncProgressMap();

        result.Should().RequestSucceed();
        result.Data.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["consensusMaxMessageCount"] = 0UL,
            ["executionSyncTarget"] = 0UL,
            ["blockNum"] = 0UL,
            ["messageOfLastBlock"] = 0UL,
        });
    }

    [Test]
    public void FullSyncProgressMap_AfterSetConsensusSyncData_ReflectsConsensusState()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 32)
            .Build();

        chain.ArbitrumRpcModule.SetConsensusSyncData(new SetConsensusSyncDataParams
        {
            Synced = true,
            MaxMessageCount = 42,
            SyncProgressMap = new Dictionary<string, object> { ["someKey"] = "someValue" },
            UpdatedAt = DateTime.UtcNow,
        }).Should().RequestSucceed();

        ResultWrapper<Dictionary<string, object>> result = chain.ArbitrumRpcModule.FullSyncProgressMap();

        result.Should().RequestSucceed();
        result.Data.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["someKey"] = "someValue",
            ["consensusMaxMessageCount"] = 42UL,
            ["executionSyncTarget"] = 42UL,
            ["blockNum"] = 0UL,
            ["messageOfLastBlock"] = 0UL,
        });
    }

    [Test]
    public void FullSyncProgressMap_AfterSetFinalityData_ReflectsBlockState()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), 3)
            .Build();

        Hash256 lastBlockHash = chain.BlockTree.Head!.Hash!;

        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams
        {
            SafeFinalityData = new RpcFinalityData { MsgIdx = 3UL, BlockHash = lastBlockHash },
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3UL, BlockHash = lastBlockHash },
        }).Should().RequestSucceed();

        ResultWrapper<Dictionary<string, object>> result = chain.ArbitrumRpcModule.FullSyncProgressMap();

        result.Should().RequestSucceed();
        result.Data.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["consensusMaxMessageCount"] = 0UL,
            ["executionSyncTarget"] = 0UL,
            ["blockNum"] = 3UL,
            ["messageOfLastBlock"] = 3UL,
        });
    }

    [Test]
    public async Task ArbOSVersionForMessageIndex_WhenMessageIndexCausesOverflow_ReturnsFailResult()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<ulong> result = await chain.ArbitrumRpcModule.ArbOSVersionForMessageIndex(ulong.MaxValue);

        result.Should().RequestFail(ArbitrumRpcErrors.Overflow);
    }

    [Test]
    public async Task ArbOSVersionForMessageIndex_WhenBlockNotFound_ReturnsFailResult()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 32)
            .Build();

        ResultWrapper<ulong> result = await chain.ArbitrumRpcModule.ArbOSVersionForMessageIndex(99999);

        long expectedBlockNumber = (long)(chain.SpecHelper.GenesisBlockNum + 99999);
        result.Should().RequestFail(ArbitrumRpcErrors.BlockNotFound(expectedBlockNumber));
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

        versionResult.Should().RequestSucceed();

        ResultWrapper<MessageResult> blockResult = await chain.ArbitrumRpcModule.ResultAtMessageIndex(messageIndex);
        BlockHeader? header = chain.BlockTree.FindHeader(blockResult.Data.BlockHash);
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header!, LimboLogs.Instance.GetClassLogger<ArbitrumRpcModuleTests>());

        versionResult.Data.Should().Be(headerInfo.ArbOSFormatVersion);
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
