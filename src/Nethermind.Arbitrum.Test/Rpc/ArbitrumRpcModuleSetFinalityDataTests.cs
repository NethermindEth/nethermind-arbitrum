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
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public sealed class ArbitrumRpcModuleSetFinalityDataTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private IBlockTree _blockTree = null!;
    private IArbitrumRpcModule _rpcModule = null!;
    private IArbitrumSpecHelper _specHelper = null!;

    [Test]
    public void SetFinalityData_WithChainSpecGenesisBlock_RespectsGenesisOffset()
    {
        ulong genesisBlockNum = _specHelper.GenesisBlockNum;
        long blockNumber = (long)(genesisBlockNum + 3);

        Block genesisBlock = Build.A.Block.WithNumber((long)genesisBlockNum).TestObject;
        Block block = Build.A.Block.WithNumber(blockNumber).WithParentHash(genesisBlock.Hash!).TestObject;

        _blockTree.SuggestBlock(genesisBlock);
        _blockTree.SuggestBlock(block);

        SetFinalityDataParams parameters = new SetFinalityDataParams
        {
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3, BlockHash = block.Hash! }
        };

        ResultWrapper<string> result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public void SetFinalityData_WithEmptyParameters_ReturnsSuccess()
    {
        SetFinalityDataParams parameters = new SetFinalityDataParams();

        ResultWrapper<string> result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithMissingBlock_HandlesGracefullyOnValidation()
    {
        SetFinalityDataParams parameters = new SetFinalityDataParams
        {
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3, BlockHash = TestItem.KeccakA }
        };

        ResultWrapper<string> result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithNonExistentBlock_HandlesGracefully()
    {
        SetFinalityDataParams parameters = new SetFinalityDataParams
        {
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = 3, BlockHash = TestItem.KeccakA }
        };

        ResultWrapper<string> result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void SetFinalityData_WithNullParameters_ReturnsFailure()
    {
        ResultWrapper<string> result = _rpcModule.SetFinalityData(null!);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain(ArbitrumRpcErrors.FormatNullParameters());
    }

    [Test]
    public void SetFinalityData_WithValidParameters_ReturnsSuccess()
    {
        ulong genesisBlockNum = _specHelper.GenesisBlockNum;
        const ulong finalizedMessageIndex = 3ul;
        const ulong safeMessageIndex = 5ul;
        long finalizedBlockNumber = (long)(genesisBlockNum + finalizedMessageIndex);
        long safeBlockNumber = (long)(genesisBlockNum + safeMessageIndex);

        Block genesisBlock = Build.A.Block.WithNumber((long)genesisBlockNum).TestObject;
        Block block1 = Build.A.Block.WithNumber(finalizedBlockNumber).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(safeBlockNumber).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(genesisBlock);
        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        SetFinalityDataParams parameters = new SetFinalityDataParams
        {
            SafeFinalityData = new RpcFinalityData { MsgIdx = safeMessageIndex, BlockHash = block2.Hash! },
            FinalizedFinalityData = new RpcFinalityData { MsgIdx = finalizedMessageIndex, BlockHash = block1.Hash! }
        };

        ResultWrapper<string> result = _rpcModule.SetFinalityData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

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
}
