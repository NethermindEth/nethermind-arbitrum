// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Core;
using Nethermind.Arbitrum.Modules;
using Nethermind.Core.Caching;
using Nethermind.Core;
using Nethermind.Db;
using Nethermind.JsonRpc;
using Nethermind.History;
using Nethermind.Logging;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Modules;

/// <summary>
/// Tests for ArbitrumDebugRpcModule - debug_reinitialize RPC endpoint.
/// </summary>
public class ArbitrumDebugRpcModuleTests
{
    private IDbProvider _dbProvider = null!;
    private IArbitrumResettableBlockTree _blockTree = null!;
    private List<IClearableCache> _cacheAwareServices = null!;
    private IClearableCache _mockCache1 = null!;
    private IClearableCache _mockCache2 = null!;
    private IArbOSVersionOverride _arbosVersionOverride = null!;
    private ArbitrumDebugRpcModule _module = null!;

    [SetUp]
    public void Setup()
    {
        _dbProvider = CreateMockDbProvider();
        _blockTree = Substitute.For<IArbitrumResettableBlockTree>();
        _mockCache1 = Substitute.For<IClearableCache>();
        _mockCache2 = Substitute.For<IClearableCache>();
        _cacheAwareServices = [_mockCache1, _mockCache2];
        _arbosVersionOverride = Substitute.For<IArbOSVersionOverride>();

        _module = new ArbitrumDebugRpcModule(
            _dbProvider,
            _blockTree,
            _cacheAwareServices,
            LimboLogs.Instance,
            _arbosVersionOverride);
    }

    [TearDown]
    public void TearDown()
    {
        _dbProvider.Dispose();
    }

    private static IDbProvider CreateMockDbProvider()
    {
        IDbProvider dbProvider = Substitute.For<IDbProvider>();

        // Create mock databases - capture in variables first to avoid NSubstitute nested call issues
        IDb stateDb = CreateMockDb();
        IDb codeDb = CreateMockDb();
        IDb blocksDb = CreateMockDb();
        IDb headersDb = CreateMockDb();
        IDb blockInfosDb = CreateMockDb();
        IDb blockNumbersDb = CreateMockDb();
        IDb metadataDb = CreateMockDb();
        IDb badBlocksDb = CreateMockDb();
        IColumnsDb<ReceiptsColumns> receiptsDb = CreateMockColumnsDb<ReceiptsColumns>();
        IColumnsDb<BlobTxsColumns> blobTransactionsDb = CreateMockColumnsDb<BlobTxsColumns>();

        // Configure returns
        dbProvider.StateDb.Returns(stateDb);
        dbProvider.CodeDb.Returns(codeDb);
        dbProvider.BlocksDb.Returns(blocksDb);
        dbProvider.HeadersDb.Returns(headersDb);
        dbProvider.BlockInfosDb.Returns(blockInfosDb);
        dbProvider.BlockNumbersDb.Returns(blockNumbersDb);
        dbProvider.MetadataDb.Returns(metadataDb);
        dbProvider.BadBlocksDb.Returns(badBlocksDb);
        dbProvider.ReceiptsDb.Returns(receiptsDb);
        dbProvider.BlobTransactionsDb.Returns(blobTransactionsDb);

        return dbProvider;
    }

    private static IDb CreateMockDb()
    {
        IDb db = Substitute.For<IDb>();
        return db;
    }

    private static IColumnsDb<T> CreateMockColumnsDb<T>() where T : struct, Enum
    {
        IColumnsDb<T> columnsDb = Substitute.For<IColumnsDb<T>>();
        foreach (T column in Enum.GetValues<T>())
        {
            IDb columnDb = CreateMockDb();
            columnsDb.GetColumnDb(column).Returns(columnDb);
        }
        return columnsDb;
    }


    [Test]
    public void Constructor_NullDbProvider_ThrowsArgumentNullException()
    {
        Action act = () => new ArbitrumDebugRpcModule(
            null!,
            _blockTree,
            _cacheAwareServices,
            LimboLogs.Instance,
            _arbosVersionOverride);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dbProvider");
    }

    [Test]
    public void Constructor_NullBlockTree_ThrowsArgumentNullException()
    {
        Action act = () => new ArbitrumDebugRpcModule(
            _dbProvider,
            null!,
            _cacheAwareServices,
            LimboLogs.Instance,
            _arbosVersionOverride);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("blockTree");
    }

    [Test]
    public void Constructor_NullCacheAwareServices_ThrowsArgumentNullException()
    {
        Action act = () => new ArbitrumDebugRpcModule(
            _dbProvider,
            _blockTree,
            null!,
            LimboLogs.Instance,
            _arbosVersionOverride);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheAwareServices");
    }



    [Test]
    public async Task DebugReinitialize_ValidParams_ReturnsSuccess()
    {
        ResultWrapper<bool> result = await _module.debug_reinitialize(51, "{}", null);

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public async Task DebugReinitialize_WithArbosVersion_ReturnsSuccess()
    {
        ResultWrapper<bool> result = await _module.debug_reinitialize(20, "{}", null);

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public async Task DebugReinitialize_WithMaxCodeSize_ReturnsSuccess()
    {
        ResultWrapper<bool> result = await _module.debug_reinitialize(51, "{}", "0x8000");

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public async Task DebugReinitialize_EmptyAccountsJson_ReturnsSuccess()
    {
        ResultWrapper<bool> result = await _module.debug_reinitialize(51, "", null);

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public async Task DebugReinitialize_NullAccountsJson_ReturnsSuccess()
    {
        ResultWrapper<bool> result = await _module.debug_reinitialize(51, "null", null);

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }



    [Test]
    public async Task AllDatabases_AfterReinitialize_AreCleared()
    {
        await _module.debug_reinitialize(51, "{}", null);

        _dbProvider.StateDb.Received(1).Clear();
        // CodeDb is intentionally NOT cleared — entries are content-addressed (see ClearAllDatabases comment)
        _dbProvider.CodeDb.DidNotReceive().Clear();
        _dbProvider.BlocksDb.Received(1).Clear();
        _dbProvider.HeadersDb.Received(1).Clear();
        _dbProvider.BlockInfosDb.Received(1).Clear();
        _dbProvider.BlockNumbersDb.Received(1).Clear();
        _dbProvider.MetadataDb.Received(1).Clear();
        _dbProvider.BadBlocksDb.Received(1).Clear();
    }

    [Test]
    public async Task ColumnsDb_AfterReinitialize_IsCleared()
    {
        await _module.debug_reinitialize(51, "{}", null);

        // ReceiptsDb columns should be cleared
        foreach (ReceiptsColumns column in Enum.GetValues<ReceiptsColumns>())
        {
            _dbProvider.ReceiptsDb.GetColumnDb(column).Received(1).Clear();
        }

        // BlobTransactionsDb columns should be cleared
        foreach (BlobTxsColumns column in Enum.GetValues<BlobTxsColumns>())
        {
            _dbProvider.BlobTransactionsDb.GetColumnDb(column).Received(1).Clear();
        }
    }



    [Test]
    public async Task BlockTree_AfterReinitialize_IsReset()
    {
        await _module.debug_reinitialize(51, "{}", null);

        _blockTree.Received(1).ResetForTesting();
    }



    [Test]
    public async Task AllCacheAwareServices_AfterReinitialize_AreCleared()
    {
        await _module.debug_reinitialize(51, "{}", null);

        _mockCache1.Received(1).ClearCache();
        _mockCache2.Received(1).ClearCache();
    }



    [Test]
    public async Task DebugReinitialize_DbClearThrows_ReturnsFail()
    {
        _dbProvider.StateDb.When(x => x.Clear()).Do(_ => throw new InvalidOperationException("DB error"));

        ResultWrapper<bool> result = await _module.debug_reinitialize(51, "{}", null);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("Reinitialization failed");
    }

    [Test]
    public async Task DebugReinitialize_BlockTreeResetThrows_ReturnsFail()
    {
        _blockTree.When(x => x.ResetForTesting()).Do(_ => throw new InvalidOperationException("Reset error"));

        ResultWrapper<bool> result = await _module.debug_reinitialize(51, "{}", null);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("Reinitialization failed");
    }



    [Test]
    public async Task DebugReinitialize_WithNullOptionalCaches_Succeeds()
    {
        ArbitrumDebugRpcModule moduleWithNullCaches = new(
            _dbProvider,
            _blockTree,
            _cacheAwareServices,
            LimboLogs.Instance,
            _arbosVersionOverride,
            null,  // blockhashCache
            null); // preBlockCaches

        ResultWrapper<bool> result = await moduleWithNullCaches.debug_reinitialize(51, "{}", null);

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public async Task DebugReinitialize_WithEmptyCacheAwareServices_Succeeds()
    {
        ArbitrumDebugRpcModule moduleWithEmptyCaches = new(
            _dbProvider,
            _blockTree,
            [],  // empty list
            LimboLogs.Instance,
            _arbosVersionOverride);

        ResultWrapper<bool> result = await moduleWithEmptyCaches.debug_reinitialize(51, "{}", null);

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }



    [Test]
    public async Task DebugSchedulePruneHistory_WhenHistoryPrunerIsNull_ReturnsFail()
    {
        ResultWrapper<bool> result = await _module.debug_schedulePruneHistory();

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("IHistoryPruner service not available");
    }

    [Test]
    public async Task DebugSchedulePruneHistory_WhenHistoryPrunerAvailable_SchedulesAndReturnsSuccess()
    {
        IHistoryPruner historyPruner = Substitute.For<IHistoryPruner>();
        ArbitrumDebugRpcModule module = new(
            _dbProvider,
            _blockTree,
            _cacheAwareServices,
            LimboLogs.Instance,
            _arbosVersionOverride = Substitute.For<IArbOSVersionOverride>(),
            historyPruner);

        ResultWrapper<bool> result = await module.debug_schedulePruneHistory();

        result.Data.Should().BeTrue();
        result.Result.ResultType.Should().Be(ResultType.Success);
        historyPruner.Received(1).SchedulePruneHistory();
    }



    [Test]
    public async Task DebugReinitialize_MultipleCalls_AllSucceed()
    {
        for (int i = 0; i < 5; i++)
        {
            ResultWrapper<bool> result = await _module.debug_reinitialize(51, "{}", null);
            result.Data.Should().BeTrue();
            result.Result.ResultType.Should().Be(ResultType.Success);
        }

        // Each call should clear databases and caches
        _dbProvider.StateDb.Received(5).Clear();
        _blockTree.Received(5).ResetForTesting();
        _mockCache1.Received(5).ClearCache();
        _mockCache2.Received(5).ClearCache();
    }

}
