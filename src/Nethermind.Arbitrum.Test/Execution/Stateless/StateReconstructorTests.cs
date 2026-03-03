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

        // ALL state roots from genesis to head should now be available
        // StateReconstructor reconstructed until head-1, and RecordBlockCreation should have reconstructed the head block's state as well
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"state root for block {blockNum} should be available after reconstruction");
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

        // State roots AFTER the intermediate block should now be available
        // StateReconstructor reconstructed until head-1, and RecordBlockCreation should have reconstructed the head block's state as well
        for (long blockNum = intermediateBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeTrue(
                $"state root for block {blockNum} should be available after reconstruction from intermediate block");
        }

        // State roots BEFORE the intermediate block should NOT have been reconstructed
        for (long blockNum = (long)chain.GenesisBlockNumber + 1; blockNum < intermediateBlockNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            trieStore.HasRoot(header.StateRoot!).Should().BeFalse(
                $"state root for block {blockNum} should not be reconstructed (before nearest available state)");
        }
    }

    [Test]
    public async Task RecordBlockCreation_StateAlreadyAvailable_SkipsReconstruction()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChainWithRecording();

        ReconstructedStateTrieStore trieStore = chain.Container.Resolve<ReconstructedStateTrieStore>();
        trieStore.HasRoot(chain.BlockTree.Head!.StateRoot!).Should().BeTrue(
            "head state root should already be available before RecordBlockCreation");

        // In archive mode, state is always available — EnsureStateAvailable is a no-op
        DigestMessageParameters lastDigestMessage = GetLastDigestedMessage();
        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastDigestMessage.Index, lastDigestMessage.Message, WasmTargets: []));

        recordResult.Result.Should().Be(Result.Success);
        recordResult.Data.BlockHash.Should().Be(new Hash256(RecordingTests.Block18Hash));
        recordResult.Data.Preimages.Should().NotBeEmpty();
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

        ulong end = 10;
        ResultWrapper<EmptyResponse> result = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(Start: 5, end));
        result.Result.Should().Be(Result.Success);

        // State roots for all blocks in the range should now be available.
        // StateReconstructor also reconstructed the blocks before the start block (from nearest available,
        // here genesis) in order to reconstruct the blocks in the range
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum >= (long)chain.GenesisBlockNumber && blockNum <= (long)end)
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
        long intermediateBlockNumber = (long)chain.GenesisBlockNumber + 11;
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

        ulong end = 17;
        ResultWrapper<EmptyResponse> result = chain.ArbitrumRpcModule.PrepareForRecord(
            new PrepareForRecordParameters(Start: 13, end));
        result.Result.Should().Be(Result.Success);

        // State roots for all blocks in the range should now be available
        // StateReconstructor also reconstructed the blocks before the start block (from nearest available,
        //  here the intermediate block) in order to reconstruct the blocks in the range
        for (long blockNum = (long)chain.GenesisBlockNumber; blockNum <= headNumber; blockNum++)
        {
            BlockHeader header = chain.BlockTree.FindHeader(blockNum)!;
            if (blockNum == (long)chain.GenesisBlockNumber || (blockNum >= intermediateBlockNumber && blockNum <= (long)end))
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

    private static ArbitrumRpcTestBlockchain BuildChainWithRecording(SwitchableReadOnlyTrieStore? switchableStore = null)
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);

        ArbitrumTestBlockchainBuilder builder = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording);

        if (switchableStore is not null)
            builder.WithContainerConfigurer(b => b.AddSingleton<ReconstructedStateTrieStore>(ctx =>
                new ReconstructedStateTrieStore(new MemDb(), switchableStore.Wrap(ctx.Resolve<IReadOnlyTrieStore>()))));

        return builder.Build();
    }

    private static DigestMessageParameters GetLastDigestedMessage()
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        return recording.GetDigestMessages().Last();
    }

    /// <summary>
    /// A controller that wraps an IReadOnlyTrieStore with switchable HasRoot behavior.
    /// Initially passes through all calls (including HasRoot) to the real store.
    /// After EnablePruning() is called, HasRoot returns false for roots not in the allowed set,
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

            public byte[]? TryLoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
                => inner.TryLoadRlp(address, in path, hash, flags);

            public INodeStorage.KeyScheme Scheme => inner.Scheme;

            public ICommitter BeginCommit(Hash256? address, TrieNode? root, WriteFlags writeFlags)
                => inner.BeginCommit(address, root, writeFlags);

            public bool HasRoot(Hash256 stateRoot)
                => controller._availableRoots?.Contains(stateRoot) ?? inner.HasRoot(stateRoot);

            public IDisposable BeginScope(BlockHeader? baseBlock) => inner.BeginScope(baseBlock);

            public IScopedTrieStore GetTrieStore(Hash256? address) => inner.GetTrieStore(address);

            public IBlockCommitter BeginBlockCommit(long blockNumber) => inner.BeginBlockCommit(blockNumber);
        }
    }
}
