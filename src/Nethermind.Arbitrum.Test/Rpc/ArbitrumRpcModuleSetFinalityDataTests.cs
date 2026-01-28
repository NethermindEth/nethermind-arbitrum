// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public sealed class ArbitrumRpcModuleSetFinalityDataTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private IArbitrumRpcModule _rpcModule = null!;
    private IBlockTree _blockTree = null!;
    private IArbitrumSpecHelper _specHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _blockchain = ArbitrumRpcTestBlockchain.CreateDefault();
        _rpcModule = _blockchain.ArbitrumRpcModule;
        _blockTree = _blockchain.BlockTree;
        _specHelper = _blockchain.Container.Resolve<IArbitrumSpecHelper>();
    }

    [TearDown]
    public void TearDown()
    {
        _blockchain?.Dispose();
    }

    [Test]
    public void SetFinalityData_WithValidParameters_ReturnsSuccess()
    {
        var genesisBlockNum = _specHelper.GenesisBlockNum;
        const ulong finalizedMessageIndex = 3ul;
        const ulong safeMessageIndex = 5ul;
        var finalizedBlockNumber = (long)(genesisBlockNum + finalizedMessageIndex);
        var safeBlockNumber = (long)(genesisBlockNum + safeMessageIndex);

        var genesisBlock = Build.A.Block.WithNumber((long)genesisBlockNum).TestObject;
        var block1 = Build.A.Block.WithNumber(finalizedBlockNumber).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(safeBlockNumber).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(genesisBlock);
        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        var parameters = new SetFinalityDataParams
        {
            SafeFinalityData = new RpcFinalityData { MsgIdx = safeMessageIndex, BlockHash = block2.Hash! },
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = finalizedMessageIndex, BlockHash = block1.Hash! }
        };

        var result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithMissingBlock_HandlesGracefullyOnValidation()
    {
        var parameters = new SetFinalityDataParams
        {
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3, BlockHash = TestItem.KeccakA }
        };

        var result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithEmptyParameters_ReturnsSuccess()
    {
        var parameters = new SetFinalityDataParams();

        var result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithNonExistentBlock_HandlesGracefully()
    {
        var parameters = new SetFinalityDataParams
        {
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3, BlockHash = TestItem.KeccakA }
        };

        var result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithChainSpecGenesisBlock_RespectsGenesisOffset()
    {
        var genesisBlockNum = _specHelper.GenesisBlockNum;
        var blockNumber = (long)(genesisBlockNum + 3);

        var genesisBlock = Build.A.Block.WithNumber((long)genesisBlockNum).TestObject;
        var block = Build.A.Block.WithNumber(blockNumber).WithParentHash(genesisBlock.Hash!).TestObject;

        _blockTree.SuggestBlock(genesisBlock);
        _blockTree.SuggestBlock(block);

        var parameters = new SetFinalityDataParams
        {
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3, BlockHash = block.Hash! }
        };

        var result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }
}
