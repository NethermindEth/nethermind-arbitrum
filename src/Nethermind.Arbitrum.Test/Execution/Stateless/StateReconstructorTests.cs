// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.JsonRpc;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Test.Execution.Stateless;

public class StateReconstructorTests
{
    private const string RecordingPath = "./Recordings/1__arbos32_basefee92.jsonl";

    [Test]
    public async Task RecordBlockCreation_WithFullyPrunedState_ReconstructsStateFromGenesis()
    {
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore);

        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();
        long headNumber = (long)lastDigestMessage.Index;

        // Switch to pruned mode — only genesis root is "available"
        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot });

        // Verify ALL non-genesis state roots are NOT available before reconstruction
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"genesis state root should be available before reconstruction");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available before reconstruction");
        }

        // RecordBlockCreation triggers state reconstruction from the nearest available state (genesis, in this case)
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // All state roots got reconstructed from genesis to head - 1 (RecordBlockCreation is read only!),
        // but due to reconstructed state pruning, all these intermediate state root reconstructions got pruned,
        // except for the recorded block's parent's state which got pinned as the validHeaderCandidate.
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum == (long)lastDigestMessage.Index - 1)
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
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore);

        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();
        long headNumber = (long)lastDigestMessage.Index;

        // Switch to pruned mode — genesis and an intermediate block are available
        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        long intermediateBlockNumber = (long)chain.GenesisBlockNumber + 7;
        Hash256 intermediateStateRoot = chain.BlockTree.FindHeader(intermediateBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot, intermediateStateRoot });

        // Verify state roots except for genesis and the intermediate block are NOT available before reconstruction
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum == intermediateBlockNumber)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"genesis and intermediate state roots only should be available before reconstruction");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available before reconstruction");
        }

        // RecordBlockCreation should reconstruct from the intermediate block, not genesis
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // All state roots got reconstructed from the intermediate block to head - 1 (RecordBlockCreation is read only!),
        // but due to reconstructed state pruning, all these intermediate state root reconstructions got pruned,
        // except for the recorded block's parent's state which got pinned as the validHeaderCandidate.
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum == intermediateBlockNumber || blockNum == (long)lastDigestMessage.Index - 1)
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
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore);

        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();

        // Make target's parent's state root available from the start
        long targetParentBlockNumber = (long)lastDigestMessage.Index - 1;
        Hash256 targetParentStateRoot = chain.BlockTree.FindHeader(targetParentBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { targetParentStateRoot });

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)chain.LatestL2BlockIndex; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == targetParentBlockNumber)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"parent state root for block {blockNum} should be available before RecordBlockCreation");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available before RecordBlockCreation");
        }

        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();

        // As target's parent's state root is already available, EnsureStateAvailable is a no-op
        // and RecordBlockCreation is read only, so state roots availability should be unchanged after the call.
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)chain.LatestL2BlockIndex; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == targetParentBlockNumber)
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
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore);

        long headNumber = chain.BlockTree.Head!.Number;

        // Switch to pruned mode — only genesis root is "available"
        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot });

        // Verify state roots are NOT available before PrepareForRecord
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"genesis state root should be available before PrepareForRecord");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available before PrepareForRecord");
        }

        ulong start = 5;
        ulong end = 10;
        ResultWrapper<EmptyResponse> result = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(start, end));
        result.Result.Should().Be(Result.Success);

        // State roots for all blocks in the range [start-1, end] (in addition to genesis) should now be available.
        // StateReconstructor also reconstructed the blocks before the start block (from nearest available,
        // here genesis) in order to reconstruct the blocks in the range, but those ones then got pruned
        // as not pinned/referenced (for future use).
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum >= (long)start - 1 && blockNum <= (long)end)
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
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore);

        long headNumber = chain.BlockTree.Head!.Number;

        // Switch to pruned mode — genesis and an intermediate block are available
        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        long intermediateBlockNumber = (long)chain.GenesisBlockNumber + 9;
        Hash256 intermediateStateRoot = chain.BlockTree.FindHeader(intermediateBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot, intermediateStateRoot });

        // Verify state roots after the intermediate block are NOT available
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == intermediateBlockNumber || blockNum == (long)chain.GenesisBlockNumber)
                trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                    $"state root for block {blockNum} should be available before PrepareForRecord");
            else
                trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                    $"state root for block {blockNum} should not be available before PrepareForRecord");
        }

        ulong start = 13;
        ulong end = 17;
        ResultWrapper<EmptyResponse> result = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(start, end));
        result.Result.Should().Be(Result.Success);

        // State roots for all blocks in the range [start-1, end] (in addition to the already available
        // genesis and the intermediate block) should now be available.
        // StateReconstructor also reconstructed the blocks before the start block (from nearest available,
        // here the intermediate block) in order to reconstruct the blocks in the range,
        // but those ones then got pruned as not pinned/referenced (for future use).
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum == intermediateBlockNumber || blockNum >= (long)start - 1 && blockNum <= (long)end)
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
        ResultWrapper<EmptyResponse> result = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(Start: 10, End: 15));

        result.Result.Should().Be(Result.Success);

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
        ResultWrapper<EmptyResponse> result = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(start, end));

        result.Result.Should().NotBe(Result.Success);
        result.Result.Error.Should().Be($"Invalid range: start {start} > end {end}");
    }

    [Test]
    public async Task PrepareForRecord_ThenRecordBlockCreation_PreparedStateRemainsAvailable()
    {
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore);

        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot });

        ulong prepareStart = 14;
        ulong prepareEnd = 17;
        ResultWrapper<EmptyResponse> prepareResult = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(prepareStart, prepareEnd));
        prepareResult.Result.Should().Be(Result.Success);

        // PrepareForRecord also includes the parent state (prepareStart-1) so RecordBlockCreation can access it
        long overlayStart = (long)prepareStart - 1;
        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= chain.BlockTree.Head!.Number; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;

            bool shouldBeAvailable = blockNum == (long)chain.GenesisBlockNumber
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

            bool shouldBeAvailable = blockNum == (long)chain.GenesisBlockNumber
                || (blockNum >= overlayStart && blockNum <= (long)prepareEnd);

            trieStore.HasRoot(header.StateRoot!).Should().Be(shouldBeAvailable,
                $"block {blockNum} state should {(shouldBeAvailable ? "" : "not ")}be available after RecordBlockCreation");
        }
    }

    [Test]
    public void PrepareForRecord_WithSmallMaxStateRootsInMem_EvictsOldStates()
    {
        SwitchableReadOnlyTrieStore switchableStore = new();
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore, maxStateRootsInMem: 3);

        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot });

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();

        // First PrepareForRecord: 4 states [4,5,6,7] prepared but max=3 → block 4 immediately evicted.
        // But block 4's state (first one in the range) got referenced as the validHeaderCandidate.
        // So, even if not in queue anymore, its state still exists in memDB.
        ResultWrapper<EmptyResponse> firstResult = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(Start: 5, End: 7));
        firstResult.Result.Should().Be(Result.Success);

        trieStore.HasRoot(chain.BlockTree.FindHeader(4)!.StateRoot!).Should().BeTrue(
            "block 4 got referenced as validHeaderCandidate even if evicted from queue");
        trieStore.HasRoot(chain.BlockTree.FindHeader(5)!.StateRoot!).Should().BeTrue("block 5 should be available");
        trieStore.HasRoot(chain.BlockTree.FindHeader(6)!.StateRoot!).Should().BeTrue("block 6 should be available");
        trieStore.HasRoot(chain.BlockTree.FindHeader(7)!.StateRoot!).Should().BeTrue("block 7 should be available");

        // Second PrepareForRecord: 4 more states [9,10,11,12] added → queue [5,6,7,9,10,11,12], keep 3 most recent [10,11,12]
        ResultWrapper<EmptyResponse> secondResult = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(Start: 10, End: 12));
        secondResult.Result.Should().Be(Result.Success);

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
        SwitchableReadOnlyTrieStore switchableStore = new();
        // max=5 so the first PrepareForRecord [3,4,5,6,7] fits exactly without eviction
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording(switchableStore, maxStateRootsInMem: 5);

        Hash256 genesisStateRoot = chain.BlockTree.FindHeader((long)chain.GenesisBlockNumber)!.StateRoot!;
        switchableStore.EnablePruning(new HashSet<Hash256> { genesisStateRoot });

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();

        // Phase 1: prepare states for blocks 3-7 (PrepareForRecord includes start-1=3)
        ulong start1 = 4;
        ulong end1 = 7;
        ResultWrapper<EmptyResponse> firstPrepare = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(start1, end1));
        firstPrepare.Result.Should().Be(Result.Success);

        DigestMessageParameters lastDigestMsg = GetLastDigestedMessage();
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)lastDigestMsg.Index; blockNum++)
        {
            if (blockNum == (long)chain.GenesisBlockNumber || (blockNum >= (long)start1 - 1 && blockNum <= (long)end1))
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
            if (blockNum == (long)chain.GenesisBlockNumber || (blockNum >= (long)start1 - 1 && blockNum <= (long)end1))
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeTrue(
                    $"block {blockNum} state should be available after first RecordBlockCreation");
            else
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state should not be available after first RecordBlockCreation");
        }

        // Phase 3: second PrepareForRecord prepares [11,12,13,14] → queue [3,4,5,6,7,11,12,13,14], evict 4 oldest [3,4,5,6] to keep only 5
        // But the first PrepareForRecord call earlier set the first header in the range (start-1=3) as the _validHeaderCandidate
        // and therefore referenced it. So, even if that header got evicted from the queue, its state is still referenced.
        ResultWrapper<EmptyResponse> secondPrepare = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(Start: 12, End: 14));
        secondPrepare.Result.Should().Be(Result.Success);

        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= (long)lastDigestMsg.Index; blockNum++)
        {
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum == 3 || blockNum == 7 || (blockNum >= 11 && blockNum <= 14))
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
            if (blockNum == (long)chain.GenesisBlockNumber || blockNum == 3 || blockNum == 7 || (blockNum >= 11 && blockNum <= 14))
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeTrue(
                    $"block {blockNum} state should be available after second RecordBlockCreation");
            else
                trieStore.HasRoot(chain.BlockTree.FindHeader(blockNum)!.StateRoot!).Should().BeFalse(
                    $"block {blockNum} state should not be available after second RecordBlockCreation");
        }
    }

    private static ArbitrumRpcTestBlockchain BuildChainWithRecording(
        SwitchableReadOnlyTrieStore? switchableStore = null,
        int? maxStateRootsInMem = null)
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);

        ArbitrumTestBlockchainBuilder builder = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording);

        if (switchableStore is not null)
            builder.WithContainerConfigurer(b => b.AddSingleton<ReconstructedStateTrieStore>(ctx =>
                new ReconstructedStateTrieStore(new MemDb(), switchableStore.Wrap(ctx.Resolve<IReadOnlyTrieStore>()))));

        if (maxStateRootsInMem.HasValue)
            builder.WithArbitrumConfig(cfg => cfg.ValidatorMaxStateRootsInMem = maxStateRootsInMem.Value);

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore
        return builder.Build(chain => chain.WorldStateManager.FlushCache(CancellationToken.None));
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

    /// <summary>
    /// A controller that wraps an IReadOnlyTrieStore with switchable HasRoot behavior.
    /// Initially passes through all calls (including TryLoadRlp used for HasRoot) to the real store.
    /// After EnablePruning() is called, TryLoadRlp returns false for roots not in the allowed set,
    /// while all other operations still delegate to the real store.
    /// This simulates pruning mode where the few available trie nodes are "on disk" while all the others have been evicted from the dirty cache.
    /// </summary>
    private class SwitchableReadOnlyTrieStore
    {
        private HashSet<Hash256>? _availableRoots;

        public IReadOnlyTrieStore Wrap(IReadOnlyTrieStore inner) => new Wrapper(inner, this);

        public void EnablePruning(HashSet<Hash256> availableRoots) => _availableRoots = availableRoots;

        private class Wrapper(IReadOnlyTrieStore inner, SwitchableReadOnlyTrieStore controller) : IReadOnlyTrieStore
        {
            public void Dispose() { }

            public TrieNode FindCachedOrUnknown(Hash256? address, in TreePath path, Hash256 hash)
                => inner.FindCachedOrUnknown(address, in path, hash);

            public byte[]? LoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
                => inner.LoadRlp(address, in path, hash, flags);

            // ReconstructedStateTrieStore.HasRoot() will call this method to check if a state root is available,
            // so we override it to implement the pruning behavior.
            // But we also need to make sure it does not impact regular calls to it when fetching nodes outside of state roots.
            public byte[]? TryLoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
            {
                // If the controller overrides available roots and we are loading a state root (address is null and path is empty),
                // only return the node if it's in the available set
                if (controller._availableRoots is not null && address is null && path.Length == 0)
                    return controller._availableRoots.Contains(hash) ? inner.TryLoadRlp(address, in path, hash, flags) : null;

                return inner.TryLoadRlp(address, in path, hash, flags);
            }

            public INodeStorage.KeyScheme Scheme => inner.Scheme;

            public ICommitter BeginCommit(Hash256? address, TrieNode? root, WriteFlags writeFlags)
                => inner.BeginCommit(address, root, writeFlags);

            // This method should not even be called as ReconstructedStateTrieStore.HasRoot() will call this.TryLoadRlp() directly!
            public bool HasRoot(Hash256 stateRoot)
                => throw new UnauthorizedAccessException("Method HasRoot should not be called");

            public IDisposable BeginScope(BlockHeader? baseBlock) => inner.BeginScope(baseBlock);

            public IScopedTrieStore GetTrieStore(Hash256? address) => inner.GetTrieStore(address);

            public IBlockCommitter BeginBlockCommit(long blockNumber) => inner.BeginBlockCommit(blockNumber);
        }
    }
}
