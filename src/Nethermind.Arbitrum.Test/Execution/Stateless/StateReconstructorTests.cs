// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.JsonRpc;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Test.Execution.Stateless;

public class StateReconstructorTests
{
    private const string RecordingPath = "./Recordings/1__arbos32_basefee92.jsonl";

    [Test]
    public async Task RecordBlockCreation_WithFullyPrunedState_ReconstructsStateFromGenesis()
    {
        // Keep genesis state root accessible, prune the rest
        HashSet<long> blockNumbersToKeep = [0];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep);

        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();
        long headNumber = (long)lastDigestMessage.Index;

        // Verify ALL non-genesis state roots are NOT available before reconstruction
        // Verified in SimulatePruning()

        // RecordBlockCreation triggers state reconstruction from the nearest available state (genesis, in this case)
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // All state roots got reconstructed from genesis to head - 1 (RecordBlockCreation is read only!),
        // but due to reconstructed state pruning, all these intermediate state root reconstructions got pruned,
        // except for the recorded block's parent's state which got pinned as the validHeaderCandidate.
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNumbersToKeep.Contains(blockNum) || blockNum == (long)lastDigestMessage.Index - 1)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"state root for block {blockNum} should be available after reconstruction");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available after reconstruction");
        }
    }

    [Test]
    public async Task RecordBlockCreation_WithPartiallyPrunedState_ReconstructsStateFromNearestAvailable()
    {
        // Keep genesis + intermediate state root accessible, prune the rest
        long intermediateBlockNumber = 7;
        HashSet<long> blockNumbersToKeep = [0, intermediateBlockNumber];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep);

        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();
        long headNumber = (long)lastDigestMessage.Index;

        // Verify state roots except for genesis and the intermediate block are NOT available before reconstruction
        // Verified in SimulatePruning()

        // RecordBlockCreation should reconstruct from the intermediate block, not genesis
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // All state roots got reconstructed from the intermediate block to head - 1 (RecordBlockCreation is read only!),
        // but due to reconstructed state pruning, all these intermediate state root reconstructions got pruned,
        // except for the recorded block's parent's state which got pinned as the validHeaderCandidate.
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNumbersToKeep.Contains(blockNum) || blockNum == (long)lastDigestMessage.Index - 1)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"genesis and intermediate state roots only should be available before reconstruction");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available before reconstruction");
        }
    }

    [Test]
    public async Task RecordBlockCreation_StateAlreadyAvailable_SkipsReconstruction()
    {
        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();
        long targetParentBlockNumber = (long)lastDigestMessage.Index - 1;

        // Make only target's parent's state root available from the start
        HashSet<long> blockNumbersToKeep = [targetParentBlockNumber];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep);

        // Verify state roots except for target block are NOT available before reconstruction
        // Verified in SimulatePruning()

        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // As target's parent's state root is already available, EnsureStateAvailable is a no-op
        // and RecordBlockCreation is read only, so state roots availability should be unchanged after the call.
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)chain.LatestL2BlockIndex; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNumbersToKeep.Contains(blockNum))
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"parent state root for block {blockNum} should be available after RecordBlockCreation");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available after RecordBlockCreation");
        }
    }

    [Test]
    public void PrepareForRecord_WithFullyPrunedState_ReconstructsAllStatesInRange()
    {
        // Switch to pruned mode — only genesis root is "available"
        HashSet<long> blockNumbersToKeep = [0];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep);

        long headNumber = chain.BlockTree.Head!.Number;

        // Verify state roots are NOT available before PrepareForRecord
        // Verified in SimulatePruning()

        ulong start = 5;
        ulong end = 10;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end)).ShouldAsync().RequestSucceed();

        // State roots for all blocks in the range [start-1, end] (in addition to genesis) should now be available.
        // StateReconstructor also reconstructed the blocks before the start block (from nearest available,
        // here genesis) in order to reconstruct the blocks in the range, but those ones then got pruned
        // as not pinned/referenced (for future use).
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNumbersToKeep.Contains(blockNum) || blockNum >= (long)start - 1 && blockNum <= (long)end)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"state root for block {blockNum} should be available after PrepareForRecord");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available after PrepareForRecord");
        }
    }

    [Test]
    public void PrepareForRecord_WithPartiallyPrunedState_ReconstructsFromNearestAvailable()
    {
        // Switch to pruned mode — genesis and an intermediate block are available
        long intermediateBlockNumber = 9;
        HashSet<long> blockNumbersToKeep = [0, intermediateBlockNumber];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep);

        long headNumber = chain.BlockTree.Head!.Number;

        // Verify state roots except for genesis and the intermediate block are NOT available
        // Verified in SimulatePruning()

        ulong start = 13;
        ulong end = 17;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end)).ShouldAsync().RequestSucceed();

        // State roots for all blocks in the range [start-1, end] (in addition to the already available
        // genesis and the intermediate block) should now be available.
        // StateReconstructor also reconstructed the blocks before the start block (from nearest available,
        // here the intermediate block) in order to reconstruct the blocks in the range,
        // but those ones then got pruned as not pinned/referenced (for future use).
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNumbersToKeep.Contains(blockNum) || blockNum >= (long)start - 1 && blockNum <= (long)end)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"state root for block {blockNum} should be available after PrepareForRecord");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available after PrepareForRecord");
        }
    }

    [Test]
    public void PrepareForRecord_StateAlreadyAvailable_SkipsReconstruction()
    {
        // No state root controller passed to the chain, all state roots are available from the start
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording();

        // All state roots should already be available before PrepareForRecord
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)chain.LatestL2BlockIndex; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"state root for block {blockNum} should be available before PrepareForRecord");
        }

        // In archive mode, state is always available — PrepareForRecord is a no-op
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 10, End: 15)).ShouldAsync().RequestSucceed();

        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)chain.LatestL2BlockIndex; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"state root for block {blockNum} should be available after PrepareForRecord");
        }
    }

    [Test]
    public void PrepareForRecord_InvalidRange_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording();

        ulong start = 10;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end)).ShouldAsync().RequestFail($"Invalid range: start {start} > end {end}");
    }

    [Test]
    public async Task PrepareForRecord_ThenRecordBlockCreation_PreparedStateRemainsAvailable()
    {
        HashSet<long> blockNumbersToKeep = [0];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep);

        ulong prepareStart = 14;
        ulong prepareEnd = 17;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(prepareStart, prepareEnd)).ShouldAsync().RequestSucceed();

        // PrepareForRecord also includes the parent state (prepareStart-1) so RecordBlockCreation can access it
        long overlayStart = (long)prepareStart - 1;
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= chain.BlockTree.Head!.Number; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;

            bool shouldBeAvailable = blockNumbersToKeep.Contains(blockNum)
                || (blockNum >= overlayStart && blockNum <= (long)prepareEnd);

            trieStore.HasRoot(header.StateRoot!).Should().Be(shouldBeAvailable,
                $"block {blockNum} state should {(shouldBeAvailable ? "" : "not ")}be available after PrepareForRecord");
        }

        // RecordBlockCreation for block 18 uses block 17's already-prepared state — no reconstruction needed
        DigestMessageParameters lastMessage = GetLastDigestedMessage();
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastMessage.Index, lastMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // PrepareForRecord-pinned states are unaffected by the RecordBlockCreation
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= chain.BlockTree.Head!.Number; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;

            bool shouldBeAvailable = blockNumbersToKeep.Contains(blockNum)
                || (blockNum >= overlayStart && blockNum <= (long)prepareEnd);

            trieStore.HasRoot(header.StateRoot!).Should().Be(shouldBeAvailable,
                $"block {blockNum} state should {(shouldBeAvailable ? "" : "not ")}be available after RecordBlockCreation");
        }
    }

    [Test]
    public void PrepareForRecord_WithSmallMaxStateRootsInMem_EvictsOldStates()
    {
        HashSet<long> blockNumbersToKeep = [0];
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep, maxStateRootsInMem: 3);

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();

        // First PrepareForRecord: 4 states [4,5,6,7] prepared but max=3 → block 4 immediately evicted.
        // But block 4's state (first one in the range) got referenced as the validHeaderCandidate.
        // So, even if not in queue anymore, its state still exists in memDB.
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 5, End: 7)).ShouldAsync().RequestSucceed();

        trieStore.HasRoot(chain.BlockTree.FindHeader(4)!.StateRoot!).Should().BeTrue(
            "block 4 got referenced as validHeaderCandidate even if evicted from queue");
        trieStore.HasRoot(chain.BlockTree.FindHeader(5)!.StateRoot!).Should().BeTrue("block 5 should be available");
        trieStore.HasRoot(chain.BlockTree.FindHeader(6)!.StateRoot!).Should().BeTrue("block 6 should be available");
        trieStore.HasRoot(chain.BlockTree.FindHeader(7)!.StateRoot!).Should().BeTrue("block 7 should be available");

        // Second PrepareForRecord: 4 more states [9,10,11,12] added → queue [5,6,7,9,10,11,12], keep 3 most recent [10,11,12]
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 10, End: 12)).ShouldAsync().RequestSucceed();

        trieStore.HasRoot(chain.BlockTree.FindHeader(4)!.StateRoot!).Should().BeTrue("block 4 should still be referenced as validHeaderCandidate");
        trieStore.HasRoot(chain.BlockTree.FindHeader(5)!.StateRoot!).Should().BeFalse("block 5 should be evicted");
        trieStore.HasRoot(chain.BlockTree.FindHeader(6)!.StateRoot!).Should().BeFalse("block 6 should be evicted");
        trieStore.HasRoot(chain.BlockTree.FindHeader(7)!.StateRoot!).Should().BeFalse("block 7 should be evicted");
        trieStore.HasRoot(chain.BlockTree.FindHeader(9)!.StateRoot!).Should().BeFalse("block 9 should be evicted");
        trieStore.HasRoot(chain.BlockTree.FindHeader(10)!.StateRoot!).Should().BeTrue("block 10 should be available");
        trieStore.HasRoot(chain.BlockTree.FindHeader(11)!.StateRoot!).Should().BeTrue("block 11 should be available");
        trieStore.HasRoot(chain.BlockTree.FindHeader(12)!.StateRoot!).Should().BeTrue("block 12 should be available");
    }

    [Test]
    public async Task PrepareForRecord_InterleavedWithRecordBlockCreation_MaintainsCorrectAvailability()
    {
        HashSet<long> blockNumbersToKeep = [0];
        // max=5 so the first PrepareForRecord [3,4,5,6,7] fits exactly without eviction
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep, maxStateRootsInMem: 5);

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();

        // Phase 1: prepare states for blocks 3-7 (PrepareForRecord includes start-1=3)
        ulong start1 = 4;
        ulong end1 = 7;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start1, end1)).ShouldAsync().RequestSucceed();

        DigestMessageParameters lastDigestMsg = GetLastDigestedMessage();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)lastDigestMsg.Index; blockNum++)
        {
            if (blockNumbersToKeep.Contains(blockNum) || (blockNum >= (long)start1 - 1 && blockNum <= (long)end1))
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeTrue(
                    $"block {blockNum} state should be available after first PrepareForRecord");
            else
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state should not be available after first PrepareForRecord");
        }

        // Phase 2: (Unordered) RecordBlockCreation calls reuse prepared states — no reconstruction, prepared states unaffected
        DigestMessageParameters msg8 = GetDigestedMessage(8);
        ResultWrapper<RecordResult> record8 = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(msg8.Index, msg8.Message, WasmTargets: []));
        record8.Result.Should().Be(Result.Success);
        record8.Data.Preimages.Should().NotBeEmpty();

        DigestMessageParameters msg6 = GetDigestedMessage(6);
        ResultWrapper<RecordResult> record6 = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(msg6.Index, msg6.Message, WasmTargets: []));
        record6.Result.Should().Be(Result.Success);
        record6.Data.Preimages.Should().NotBeEmpty();

        // Prepared states are unchanged after read-only RecordBlockCreations
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)lastDigestMsg.Index; blockNum++)
        {
            if (blockNumbersToKeep.Contains(blockNum) || (blockNum >= (long)start1 - 1 && blockNum <= (long)end1))
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeTrue(
                    $"block {blockNum} state should be available after first RecordBlockCreation");
            else
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state should not be available after first RecordBlockCreation");
        }

        // Phase 3: second PrepareForRecord prepares [11,12,13,14] → queue [3,4,5,6,7,11,12,13,14], evict 4 oldest [3,4,5,6] to keep only 5
        // But the first PrepareForRecord call earlier set the first header in the range (start-1=3) as the _validHeaderCandidate
        // and therefore referenced it. So, even if that header got evicted from the queue, its state is still referenced.
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 12, End: 14)).ShouldAsync().RequestSucceed();

        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)lastDigestMsg.Index; blockNum++)
        {
            if (blockNumbersToKeep.Contains(blockNum) || blockNum == 3 || blockNum == 7 || (blockNum >= 11 && blockNum <= 14))
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeTrue(
                    $"block {blockNum} state should be available after second PrepareForRecord");
            else
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state should not be available after second PrepareForRecord");
        }

        // Phase 4: one last RecordBlockCreation call that does not find parent state: reconstructs it temporarily
        // and evicts it before the call returns. The prepared states remain unaffected.
        DigestMessageParameters msg7 = GetDigestedMessage(7);
        ResultWrapper<RecordResult> record7 = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(msg7.Index, msg7.Message, WasmTargets: []));
        record7.Result.Should().Be(Result.Success);
        record7.Data.Preimages.Should().NotBeEmpty();

        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)lastDigestMsg.Index; blockNum++)
        {
            if (blockNumbersToKeep.Contains(blockNum) || blockNum == 3 || blockNum == 7 || (blockNum >= 11 && blockNum <= 14))
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeTrue(
                    $"block {blockNum} state should be available after second RecordBlockCreation");
            else
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state should not be available after second RecordBlockCreation");
        }
    }

    /// <summary>
    /// Verifies that MaybeCap spills reconstructed state roots from the MemDb overlay to
    /// the main state DB when the configured size threshold is exceeded.
    ///
    /// Setup: non-genesis state root keys are physically deleted from the main state DB after the chain
    /// build so that HasRoot returns false for them. This simulates pruned state without a proxy wrapper.
    ///
    /// Flow:
    /// 1. PrepareForRecord(1, 9) reconstructs state roots for blocks 1–9 (0 is already available on disk).
    ///    MaybeCap fires: threshold = 0 → targetSize = 0.SaturateSub(BytesToEvictFromMemDb) = 0, so DirtySize > targetSize
    ///    It fires per block during reconstruction and the size threshold is exceeded,
    ///    therefore state gets evicted and flushed to disk immediately for each block.
    ///
    /// 2. RecordBlockCreation for block 11 needs block 10 as its parent state, which is not in MemDb.
    ///    EnsureStateAvailable reconstructs block 10 via ReExecuteBlocks, which calls MaybeCap.
    ///    MaybeCap fires (threshold = 0 as described above). The whole memDb overlay gets flushed to disk.
    ///
    /// Assertions:
    /// - After key deletion: HasRoot returns false for all non-genesis blocks.
    /// - After PrepareForRecord: blocks 1–9 already got flushed to disk.
    /// - After RecordBlockCreation: needed block 10's state actually also ends up on disk as MaybeCap fires before any
    ///    Dereference supposed to remove the temporary parent state pin. In practice, not the case because only a few hundreds
    ///    of oldest reconstructed nodes get flushed, so, just constructed-temporary parent state would not get flushed and
    ///    would end up getting dereferenced as expected (at the end of RecordBlockCreation).
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_WhenMemDbExceedsThreshold_SpillsOldestRootsToDiskAndPreservesNewRoots()
    {
        HashSet<long> blockNumbersToKeep = [0];
        // ValidatorReconstructedStateMemDBMaxSizeMb = 0 → limit = 0 bytes → MaybeCap fires after every block
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep, maxMemDbSizeMb: 0);

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        IDb mainStateDb = chain.Container.Resolve<IDbProvider>().StateDb;

        // For every block reconstruction, all state in MemDb is flushed to disk as target size is 0 (maxMemDbSizeMb - BytesToEvictFromMemDb)
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 1, End: 9)).ShouldAsync().RequestSucceed();

        // Blocks 1–9 got reconstructed in the MemDb overlay and already written to disk (evicted from memDb)
        trieStore.DirtySize.Should().Be(0,
            "PrepareForRecord should have already flushed its reconstructed state");

        for (long blockNum = 1; blockNum <= 9; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"block {blockNum} state root should be accessible via MemDb overlay after PrepareForRecord");

            byte[] key = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, header.StateRoot!);
            mainStateDb[key].Should().NotBeNull($"block {blockNum} state root should already be on disk");
        }

        // RecordBlockCreation(11) needs block 10 as parent state — not in MemDb, so EnsureStateAvailable
        // reconstructs it via ReExecuteBlocks. MaybeCap fires during that reconstruction and flushes
        // block 10's state to disk as well.
        DigestMessageParameters msg11 = GetDigestedMessage(11);
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(msg11.Index, msg11.Message, WasmTargets: []));
        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.Preimages.Should().NotBeEmpty();

        trieStore.DirtySize.Should().Be(0, "MaybeCap should have spilled all prepared states to disk, so MemDb should be empty");

        // Blocks 10 got spilled: evicted from the MemDb overlay and written to the main state DB
        BlockHeader header10 = chain.BlockTree.FindHeader(10)!;
        byte[] key10 = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, header10.StateRoot!);

        trieStore.HasRoot(header10.StateRoot!).Should().BeTrue(
            $"block 10 state root should be accessible via disk after spill");
        mainStateDb[key10].Should().NotBeNull(
            $"block 10 state root should be present in the main state DB after spill");

        // RecordBlockCreation being readonly should not have stored in memDb nor disk state for block 11
        BlockHeader header11 = chain.BlockTree.FindHeader(11)!;
        byte[] key11 = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, header11.StateRoot!);

        trieStore.HasRoot(header11.StateRoot!).Should().BeFalse(
            $"block 11 state root should not be available via MemDb");
        mainStateDb[key11].Should().BeNull(
            $"block 11 state root should not be on disk");
    }

    [Test]
    public void PrepareForRecord_WhenMemDbExceedsThreshold_SpillsOldestRootsToDiskAndPreservesNewRoots()
    {
        HashSet<long> blockNumbersToKeep = [0];
        // ValidatorReconstructedStateMemDBMaxSizeMb = 0 → limit = 0 bytes → MaybeCap fires after every block
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(blockNumbersToKeep, maxMemDbSizeMb: 0);

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        IDb mainStateDb = chain.Container.Resolve<IDbProvider>().StateDb;

        // For every block reconstruction, all state in MemDb is flushed to disk as target size is 0 (maxMemDbSizeMb - BytesToEvictFromMemDb)
        // As a reminder: after this call _preparedQueue = [block0, block1, …, block9].
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 1, End: 9)).ShouldAsync().RequestSucceed();

        // As mentioned all MemDb reconstructed nodes got spilled to disk after each block's reconstruction, so MemDb should be empty at the end.
        trieStore.DirtySize.Should().Be(0, "All added reconstructed trie nodes during 1st PrepareForRecord should have been spilled to disk by MaybeCap");

        for (long blockNum = 1; blockNum <= 9; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"block {blockNum} state root should have been flushed to disk, hence accessible via the MemDb overlay after PrepareForRecord");
            byte[] key = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, header.StateRoot!);
            mainStateDb[key].Should().NotBeNull(
                $"block {blockNum} state root should have been flushed to disk already");
        }

        // Once again for every block reconstruction, all state in MemDb is flushed to disk as target size is 0 (maxMemDbSizeMb - BytesToEvictFromMemDb)
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(Start: 11, End: 18)).ShouldAsync().RequestSucceed();

        trieStore.DirtySize.Should().Be(0,
            "MemDb overlay should have flushed reconstructed state for blocks 10-18 after MaybeCap");

        // Check newly reconstructed blocks got spilled: evicted from the MemDb overlay and written to the main state DB.
        // HasRoot finds them via the base store (disk), so it returns true.
        for (long blockNum = 10; blockNum <= 18; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;

            // Root can be found on disk through the trie store (written back by capping).
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"block {blockNum} state root should be accessible via disk after spill");

            // Roots are on disk
            byte[] key = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, header.StateRoot!);
            mainStateDb[key].Should().NotBeNull(
                $"block {blockNum} state root should be present in the main state DB after spill");
        }
    }

    /// <summary>
    /// Simulates disk pruning by removing all state-root keys from the main state DB except for
    /// <paramref name="blockNumbersToKeep"/>. After <see cref="ArbitrumTestBlockchainBuilder.Build"/>
    /// calls <c>FlushCache</c>, every block's state root is on disk; this helper then deletes all but
    /// the ones passed. No state is accessible through the reconstructed state MemDb overlay as
    /// this method is called right after the chain is built.
    /// </summary>
    public static void SimulatePruning(ArbitrumRpcTestBlockchain chain, HashSet<long> blockNumbersToKeep)
    {
        IDb mainStateDb = chain.Container.Resolve<IDbProvider>().StateDb;
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();

        long headNumber = chain.BlockTree.Head!.Number;
        for (long blockNum = 0; blockNum <= headNumber; blockNum++)
        {
            if (blockNumbersToKeep.Contains(blockNum))
                continue;

            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            mainStateDb.Remove(NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, header.StateRoot!));
        }

        for (long blockNum = 0; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNumbersToKeep.Contains(blockNum))
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue($"state root of block {blockNum} should still be accessible");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state root key was deleted from main state DB — should not be accessible");
        }
    }

    private static ArbitrumRpcTestBlockchain BuildChainWithRecording(
        HashSet<long>? blockNumbersToKeep = null,
        int? maxStateRootsInMem = null,
        int? maxMemDbSizeMb = null)
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);

        ArbitrumTestBlockchainBuilder builder = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording);

        Action<ArbitrumConfig> configure = cfg =>
        {
            cfg.ValidationEnabled = true;
            cfg.ValidatorMaxStateRootsInMem = maxStateRootsInMem ?? cfg.ValidatorMaxStateRootsInMem;
            cfg.ValidatorReconstructedStateMemOverlayMaxSizeMb = maxMemDbSizeMb ?? cfg.ValidatorReconstructedStateMemOverlayMaxSizeMb;
        };

        builder.WithArbitrumConfig(configure);

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore
        ArbitrumRpcTestBlockchain chain = builder.Build(chain => chain.WorldStateManager.FlushCache(CancellationToken.None));

        if (blockNumbersToKeep is not null)
            SimulatePruning(chain, blockNumbersToKeep);

        return chain;
    }

    private static DigestMessageParameters GetLastDigestedMessage()
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        return recording.GetDigestMessages().Last();
    }

    private static DigestMessageParameters GetDigestedMessage(ulong index)
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        return recording.GetDigestMessages().Single(m => m.Index == index);
    }
}
