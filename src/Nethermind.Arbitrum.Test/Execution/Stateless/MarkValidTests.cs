// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Reflection;
using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Blockchain.FullPruning;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Db.FullPruning;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules.Admin;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Test.Execution;

public class MarkValidTests
{
    private const string RecordingPath = "./Recordings/1__arbos32_basefee92.jsonl";

    /// <summary>
    /// PrepareForRecord(start, end) reconstructs state for blocks [start-1, end) and sets
    /// _validHeaderCandidate to the oldest block in the range (start-1).
    /// MarkValid then promotes that candidate to _validHeader.
    /// </summary>
    [Test]
    public void MarkValid_AfterPrepareForRecord_PromotesCandidateToValidHeader()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        ulong start = 3;
        ulong end = 5;

        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);

        // _validHeaderCandidate is block start-1=2 (the oldest block PrepareForRecord touches).
        // MarkValid at pos=end promotes it because candidate.Number (2) <= blockNumber(end) (5).
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        SetFinalityDataParams finalityData = new() { ValidatedFinalityData = new RpcFinalityData() { MsgIdx = end, BlockHash = endHeader.Hash! } };
        chain.ArbitrumRpcModule.SetFinalityData(finalityData).Should().RequestSucceed();

        BlockHeader? validHeader = ReadValidHeader(chain.Container.Resolve<StateReconstructor>());
        validHeader.Should().NotBeNull();
        validHeader!.Number.Should().Be((long)start - 1);
    }

    /// <summary>
    /// RecordBlockCreation(index) sets _validHeaderCandidate to the parent block (index-1).
    /// MarkValid then promotes it to _validHeader.
    /// </summary>
    [Test]
    public async Task MarkValid_AfterRecordBlockCreation_UpdatesValidHeader()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        DigestMessageParameters lastMessage = GetLastDigestedMessage();

        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastMessage.Index, lastMessage.Message, WasmTargets: []));
        recordResult.Result.Should().Be(Result.Success);

        // RecordBlockCreation sets _validHeaderCandidate to the parent (lastMessage.Index - 1).
        // MarkValid at lastMessage.Index promotes it.
        BlockHeader lastHeader = chain.BlockTree.FindHeader((long)lastMessage.Index, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = lastMessage.Index, BlockHash = lastHeader.Hash! } }).Should().RequestSucceed();

        BlockHeader? validHeader = ReadValidHeader(chain.Container.Resolve<StateReconstructor>());
        validHeader.Should().NotBeNull();
        validHeader!.Number.Should().Be((long)lastMessage.Index - 1);
    }

    /// <summary>
    /// After PrepareForRecord + MarkValid advances _validHeader, a subsequent RecordBlockCreation
    /// followed by MarkValid advances _validHeader again to the recorded block's parent.
    /// </summary>
    [Test]
    public async Task MarkValid_CalledTwice_AdvancesValidHeaderEachTime()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        // First promotion: PrepareForRecord(3, 5) → MarkValid(5) → _validHeader = block 2
        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = end, BlockHash = endHeader.Hash! } }).Should().RequestSucceed();

        BlockHeader? firstValidHeader = ReadValidHeader(chain.Container.Resolve<StateReconstructor>());
        firstValidHeader!.Number.Should().Be((long)start - 1, "first SetFinalityData should promote block start-1");

        // Second promotion: RecordBlockCreation → SetFinalityData → _validHeader = parent of last block
        DigestMessageParameters lastMessage = GetLastDigestedMessage();
        await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastMessage.Index, lastMessage.Message, WasmTargets: []));

        BlockHeader lastHeader = chain.BlockTree.FindHeader((long)lastMessage.Index, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = lastMessage.Index, BlockHash = lastHeader.Hash! } }).Should().RequestSucceed();

        BlockHeader? secondValidHeader = ReadValidHeader(chain.Container.Resolve<StateReconstructor>());
        secondValidHeader!.Number.Should().Be((long)lastMessage.Index - 1,
            "second MarkValid should advance _validHeader to the recorded block's parent");
        secondValidHeader.Number.Should().BeGreaterThan(firstValidHeader.Number,
            "_validHeader should only advance forward");
    }

    /// <summary>
    /// MarkValid with a wrong ResultHash does not promote the candidate.
    /// </summary>
    [Test]
    public void MarkValid_WrongResultHash_DoesNotPromoteCandidate()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);

        SetFinalityDataParams finalityData = new() { ValidatedFinalityData = new RpcFinalityData { MsgIdx = end, BlockHash = Keccak.Zero } };
        // Fails when calling ValidateAndGetBlockHash, even before reaching MarkValid
        chain.ArbitrumRpcModule.SetFinalityData(finalityData).Should().RequestFail(ArbitrumRpcErrors.InternalError);

        ReadValidHeader(chain.Container.Resolve<StateReconstructor>()).Should().BeNull(
            "wrong ResultHash should not promote the candidate");
    }

    /// <summary>
    /// Runs a full pruning cycle.
    /// FullPruner.RunFullPruning calls _stateReader.RunTreeVisitor, where _stateReader
    /// is set through FullPrunerFactory (registered by the Arbitrum plugin).
    /// This means RunTreeVisitor goes through ValidatorStatePreservingStateReader, which calls
    /// CopyStatesForFullPruning to copy the validator state into the pruning context alongside
    /// the main state.  After Commit + Dispose fire PruningFinished, OnPruningFinished clears
    /// the MemDb overlay and restores _validHeader.
    ///
    /// PruningBoundary is set to 0 so FullPruner only needs Head.Number > stateToCopy (not +64).
    /// Three extra blocks from the recording are digested one-by-one to satisfy FullPruner's
    /// three sequential WaitForMainChainChange conditions.  BestPersistedState is set manually
    /// because TrieStoreBoundaryWatcher is not wired in tests.
    /// </summary>
    [Test]
    public async Task FullPruning_AfterCommit_ClearsMemDbAndValidStateRemainsAccessible()
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        DigestMessageParameters[] allMessages = recording.GetDigestMessages().ToArray();

        // Digest only the first 5 messages so blocks 6-8 are available for driving FullPruner.
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording, numberToDigest: 5)
            .WithArbitrumConfig(config => config.ValidationEnabled = true)
            .Build(c =>
            {
                c.WorldStateManager.FlushCache(CancellationToken.None);
                // PruningBoundary = 0: Condition 3 requires Head > stateToCopy, not stateToCopy+64.
                ((PruningConfig)c.Container.Resolve<IPruningConfig>()).PruningBoundary = 0;
            });

        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = end, BlockHash = endHeader.Hash! } }).Should().RequestSucceed();

        StateReconstructor stateReconstructor = chain.Container.Resolve<StateReconstructor>();
        ReconstructedStateTrieStore reconStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        BlockHeader? validHeader = ReadValidHeader(stateReconstructor);
        validHeader!.Number.Should().Be((long)start - 1);

        // PrepareForRecord reconstructs blocks [start-1, end) into the overlay, so block `start`
        // (= block 3) is present in the MemDb overlay before pruning.
        BlockHeader? intermediateHeader = chain.BlockTree.FindHeader((long)start, BlockTreeLookupOptions.RequireCanonical)!;
        reconStore.HasRoot(intermediateHeader.StateRoot!).Should().BeTrue(
            "block 3 state should be in the MemDb overlay after PrepareForRecord");

        // Subscribe before triggering so we don't miss the PruningFinished event.
        IFullPruningDb fullPruningDb = (IFullPruningDb)chain.Container.Resolve<IDbProvider>().StateDb;
        TaskCompletionSource<bool> pruningTcs = new();
        fullPruningDb.PruningFinished += (_, e) => pruningTcs.TrySetResult(e.Success);

        // admin_prune() → FullPruner.OnPrune → RunFullPruning (fire-and-forget async).
        // RunFullPruning synchronously registers the first WaitForMainChainChange handler before
        // its first await, so blocks can be digested immediately without yielding.
        IPruningTrieStateAdminRpcModule adminModule = chain.Container.Resolve<IPruningTrieStateAdminRpcModule>();
        adminModule.admin_prune().Data.Should().Be(PruningStatus.Starting);

        // Drive FullPruner through its 3 sequential WaitForMainChainChange conditions.
        // Each DigestMessage fires OnUpdateMainChain.  Task.Delay(10) yields the thread pool so
        // RunFullPruning (RunContinuationsAsynchronously) can register the next handler before
        // the subsequent OnUpdateMainChain event fires.
        // BestPersistedState is updated manually between blocks because TrieStoreBoundaryWatcher
        // is not active in tests.
        int nextMsg = 5; // messages[0..4] already digested at build time → blocks 1-5

        // Condition 1: captures blockToWaitFor from the first post-trigger block (block 6).
        chain.BlockTree.BestPersistedState = chain.BlockTree.BestKnownNumber;
        (await chain.ArbitrumRpcModule.DigestMessage(allMessages[nextMsg++])).Result.Should().Be(Result.Success);
        await Task.Delay(10);

        // Condition 2: BestPersistedState (6) >= blockToWaitFor (6) → captures stateToCopy = 6.
        chain.BlockTree.BestPersistedState = chain.BlockTree.BestKnownNumber;
        (await chain.ArbitrumRpcModule.DigestMessage(allMessages[nextMsg++])).Result.Should().Be(Result.Success);
        await Task.Delay(10);

        // Condition 3: Head (8) > stateToCopy + PruningBoundary(0) = 6 → CopyTrie executes.
        (await chain.ArbitrumRpcModule.DigestMessage(allMessages[nextMsg++])).Result.Should().Be(Result.Success);

        bool success = await pruningTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        success.Should().BeTrue("full pruning should complete successfully");

        ReadValidHeader(stateReconstructor)!.Number.Should().Be(validHeader!.Number,
            "_validHeader should be restored to the header that was copied to the new DB");

        // Only the last-valid state was copied to the new DB by CopyStatesForFullPruning.
        // Other states reconstructed into the overlay (e.g. block 3) were not copied, so after
        // ClearOverlay() they are gone entirely — this proves the overlay was cleared.
        reconStore.HasRoot(validHeader.StateRoot!).Should().BeTrue(
            "state root of last valid block must still be accessible via the new pruned DB");
        reconStore.HasRoot(intermediateHeader.StateRoot!).Should().BeFalse(
            "block 3 state was in the overlay before pruning but not copied → overlay must be cleared");

    }

    /// <summary>
    /// End-to-end regression test for the DuplicateReads pollution bug.
    ///
    /// Scenario where bug occurred:
    ///   1. A state root (block 3) is in the MemDb overlay after PrepareForRecord.
    ///   2. Full pruning starts and enters dual-write mode.
    ///   3. During dual-write, block 3 is dereferenced from the overlay (simulating it being evicted
    ///      or never present), and its root is then read from disk via TryLoadRlp.  Without the fix,
    ///      DuplicateReads writes just that root node — not the full trie — into the new DB.
    ///   4. Pruning completes; MemDb is cleared.
    ///   5. RecordBlockCreation(4) calls EnsureStateAvailable → FindLastAvailableState walks back from
    ///      block 3.  Without the fix TryReference(block3) succeeds (root is in the new DB) and
    ///      reconstruction proceeds from block 3, failing with MissingTrieNodeException when it tries
    ///      to read block 3's subtree (which was never fully copied).
    ///      With the fix TryReference(block3) returns false → falls back to the valid header (block 2,
    ///      which was properly copied by CopyStatesForFullPruning) → re-executes block 3 from block 2
    ///      → RecordBlockCreation(4) succeeds.
    /// </summary>
    [Test]
    public async Task ReconstructedStateTrieStore_DuringFullPruning_TryLoadRlpUsesSkipDuplicateReadFlagNotPollutingNewDbWithPartialReconstructedState()
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        DigestMessageParameters[] allMessages = recording.GetDigestMessages().ToArray();

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording, numberToDigest: 5)
            .WithArbitrumConfig(config => config.ValidationEnabled = true)
            .Build(c =>
            {
                c.WorldStateManager.FlushCache(CancellationToken.None);
                // PruningBoundary = 0: Condition 3 in FullPruner requires Head > stateToCopy, not stateToCopy+64.
                // Override after building chain otherwise PruningBoundary is enforced to be at least 64, complicating the test setup.
                ((PruningConfig)c.Container.Resolve<IPruningConfig>()).PruningBoundary = 0;
            });

        // PrepareForRecord(3,5) reconstructs blocks 3-4 into the overlay; validHeader = block 2.
        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = end, BlockHash = endHeader.Hash! } }).Should().RequestSucceed();

        ReconstructedStateTrieStore reconStore = chain.Container.Resolve<ReconstructedStateTrieStore>();

        // PrepareForRecord does not reconstruct any state as states for the blocks in the PrepareForRecord range can be found in base store's DB.
        // Just assert block 3 is disk-only (not found in memDb overlay) when dual-write is active.
        reconStore.DirtySize.Should().Be(0, "State exists in base store's underlying DB, no reconstruction necessary");
        BlockHeader block3Header = chain.BlockTree.FindHeader((long)start, BlockTreeLookupOptions.RequireCanonical)!;
        reconStore.HasRoot(block3Header.StateRoot!).Should().BeTrue("block 3 must be on disk");

        IFullPruningDb fullPruningDb = (IFullPruningDb)chain.Container.Resolve<IDbProvider>().StateDb;
        TaskCompletionSource<bool> pruningTcs = new();
        fullPruningDb.PruningFinished += (_, e) => pruningTcs.TrySetResult(e.Success);

        IPruningTrieStateAdminRpcModule adminModule = chain.Container.Resolve<IPruningTrieStateAdminRpcModule>();
        adminModule.admin_prune().Data.Should().Be(PruningStatus.Starting);
        await Task.Delay(10);

        // Simulate the reconStore disk-fallback read that triggers DuplicateReads.
        // Without the SkipDuplicateRead flag, this would write block 3's root node, not its full subtree, to the new DB.
        reconStore.HasRoot(block3Header.StateRoot!).Should().BeTrue(
            "block 3 state root must be readable from disk during dual-write phase");

        // Drive FullPruner through its 3 WaitForMainChainChange conditions.
        int nextMsg = 5;

        chain.BlockTree.BestPersistedState = chain.BlockTree.BestKnownNumber;
        (await chain.ArbitrumRpcModule.DigestMessage(allMessages[nextMsg++])).Result.Should().Be(Result.Success);
        await Task.Delay(10);

        // blockToPruneAfter = 6, stateToCopy = 6 in FullPruner
        chain.BlockTree.BestPersistedState = chain.BlockTree.BestKnownNumber;
        (await chain.ArbitrumRpcModule.DigestMessage(allMessages[nextMsg++])).Result.Should().Be(Result.Success);
        await Task.Delay(10);

        (await chain.ArbitrumRpcModule.DigestMessage(allMessages[nextMsg++])).Result.Should().Be(Result.Success);

        bool success = await pruningTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        success.Should().BeTrue("full pruning should complete successfully");

        // After pruning, block 3's root should not be found anywhere
        reconStore.HasRoot(block3Header.StateRoot!).Should().BeFalse("After pruning, block 3's state should not be found in the new DB (or the overlay)");

        // RecordBlockCreation(4) needs state at block 3.
        // Without fix: TryReference(block3) succeeds (root was DuplicateRead'd into new DB),
        //   reconstruction proceeds from block 3, then fails with MissingTrieNodeException because
        //   the rest of block 3's trie was never copied.
        // With fix: TryReference(block3) returns false → falls back to validHeader (block 2, fully
        //   available) → re-executes block 3 from block 2 → RecordBlockCreation(4) succeeds.
        DigestMessageParameters block4Message = allMessages.First(m => m.Index == 4);
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(block4Message.Index, block4Message.Message, WasmTargets: []));
        recordResult.Result.Should().Be(Result.Success,
            "RecordBlockCreation must fall back to the valid header, not attempt reconstruction from an incomplete intermediate state");
    }

    /// <summary>
    /// On shutdown (Dispose / PersistOnShutdown) a binary marker file is written that encodes
    /// the last valid block number (8 bytes big-endian) followed by the block hash (32 bytes).
    /// </summary>
    [Test]
    public void StateReconstructor_GetsDisposed_PersistsValidHeaderMarkerToDisk()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = end, BlockHash = endHeader.Hash! } }).Should().RequestSucceed();

        StateReconstructor stateReconstructor = chain.Container.Resolve<StateReconstructor>();
        BlockHeader? validHeader = ReadValidHeader(stateReconstructor);
        validHeader!.Number.Should().Be(2);

        string markerPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        SetMarkerPath(stateReconstructor, markerPath);
        try
        {
            File.Exists(markerPath).Should().BeFalse("marker file should not exist before");
            InvokePersistOnShutdown(stateReconstructor);

            File.Exists(markerPath).Should().BeTrue("marker file should be written on shutdown");

            byte[] bytes = File.ReadAllBytes(markerPath);
            long storedBlockNumber = BinaryPrimitives.ReadInt64BigEndian(bytes);
            Hash256 storedHash = new Hash256(bytes.AsSpan(sizeof(long)));

            storedBlockNumber.Should().Be(validHeader!.Number);
            storedHash.Should().Be(validHeader.Hash!);
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    /// <summary>
    /// RestoreValidHeader reads the marker file written by PersistOnShutdown and restores _validHeader
    /// to the block it encodes, provided the block is still canonical and its state is accessible.
    /// </summary>
    [Test]
    public void StateReconstructor_OnRestart_ReadsMarkerFileAndRestoresValidHeader()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.SetFinalityData(new SetFinalityDataParams { ValidatedFinalityData = new RpcFinalityData { MsgIdx = end, BlockHash = endHeader.Hash! } }).Should().RequestSucceed();

        StateReconstructor stateReconstructor = chain.Container.Resolve<StateReconstructor>();
        BlockHeader? validHeader = ReadValidHeader(stateReconstructor);
        validHeader.Should().NotBeNull();

        string markerPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        SetMarkerPath(stateReconstructor, markerPath);
        try
        {
            // Simulate shutdown
            InvokePersistOnShutdown(stateReconstructor);

            // Simulate restart
            SetValidHeader(stateReconstructor, null);
            ReadValidHeader(stateReconstructor).Should().BeNull("sanity: _validHeader cleared");

            InvokeRestoreValidHeader(stateReconstructor);

            BlockHeader? restored = ReadValidHeader(stateReconstructor);
            restored.Should().NotBeNull("RestoreValidHeader should restore _validHeader from the marker file");
            restored!.Number.Should().Be(validHeader!.Number);
            restored.Hash.Should().Be(validHeader.Hash!);
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    private static ArbitrumRpcTestBlockchain BuildChain() =>
        new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath))
            .WithArbitrumConfig(config => config.ValidationEnabled = true)
            .Build(chain => chain.WorldStateManager.FlushCache(CancellationToken.None));

    private static DigestMessageParameters GetLastDigestedMessage()
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        return recording.GetDigestMessages().Last();
    }

    private static BlockHeader? ReadValidHeader(StateReconstructor stateReconstructor) =>
        (BlockHeader?)typeof(StateReconstructor)
            .GetField("_validHeader", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(stateReconstructor);

    private static void SetValidHeader(StateReconstructor stateReconstructor, BlockHeader? value) =>
        typeof(StateReconstructor)
            .GetField("_validHeader", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(stateReconstructor, value);

    private static void SetMarkerPath(StateReconstructor stateReconstructor, string path) =>
        typeof(StateReconstructor)
            .GetField("_validMarkerPath", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(stateReconstructor, path);

    private static void InvokePersistOnShutdown(StateReconstructor reconstructor) =>
        typeof(StateReconstructor)
            .GetMethod("PersistOnShutdown", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(reconstructor, null);

    private static void InvokeRestoreValidHeader(StateReconstructor reconstructor) =>
        typeof(StateReconstructor)
            .GetMethod("RestoreValidHeader", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(reconstructor, null);
}
