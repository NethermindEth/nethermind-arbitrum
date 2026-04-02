// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Reflection;
using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.JsonRpc;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Test.Execution;

public class MarkValidTests
{
    private const string RecordingPath = "./Recordings/1__arbos32_basefee92.jsonl";

    /// <summary>
    /// PrepareForRecord(start, end) reconstructs state for blocks [start-1, end) and sets
    /// _validHdrCandidate to the oldest block in the range (start-1).
    /// MarkValid then promotes that candidate to _validHdr.
    /// </summary>
    [Test]
    public void MarkValid_AfterPrepareForRecord_PromotesCandidateToValidHeader()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        ulong start = 3;
        ulong end = 5;

        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);

        // _validHdrCandidate is block start-1=2 (the oldest block PrepareForRecord touches).
        // MarkValid at pos=end promotes it because candidate.Number (2) <= blockNumber(end) (5).
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.MarkValid(new MarkValidParameters(end, endHeader.Hash!))
            .Result.Should().Be(Result.Success);

        BlockHeader? validHdr = ReadValidHdr(chain.Container.Resolve<StateReconstructor>());
        validHdr.Should().NotBeNull();
        validHdr!.Number.Should().Be((long)start - 1);
    }

    /// <summary>
    /// RecordBlockCreation(index) sets _validHdrCandidate to the parent block (index-1).
    /// MarkValid then promotes it to _validHdr.
    /// </summary>
    [Test]
    public async Task MarkValid_AfterRecordBlockCreation_UpdatesValidHeader()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        DigestMessageParameters lastMessage = GetLastDigestedMessage();

        ResultWrapper<RecordResult> recordResult = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastMessage.Index, lastMessage.Message, WasmTargets: []));
        recordResult.Result.Should().Be(Result.Success);

        // RecordBlockCreation sets _validHdrCandidate to the parent (lastMessage.Index - 1).
        // MarkValid at lastMessage.Index promotes it.
        BlockHeader lastHeader = chain.BlockTree.FindHeader((long)lastMessage.Index, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.MarkValid(new MarkValidParameters(lastMessage.Index, lastHeader.Hash!))
            .Result.Should().Be(Result.Success);

        BlockHeader? validHdr = ReadValidHdr(chain.Container.Resolve<StateReconstructor>());
        validHdr.Should().NotBeNull();
        validHdr!.Number.Should().Be((long)lastMessage.Index - 1);
    }

    /// <summary>
    /// After PrepareForRecord + MarkValid advances _validHdr, a subsequent RecordBlockCreation
    /// followed by MarkValid advances _validHdr again to the recorded block's parent.
    /// </summary>
    [Test]
    public async Task MarkValid_CalledTwice_AdvancesValidHeaderEachTime()
    {
        using ArbitrumRpcTestBlockchain chain = BuildChain();

        // First promotion: PrepareForRecord(3, 5) → MarkValid(5) → _validHdr = block 2
        ulong start = 3;
        ulong end = 5;
        chain.ArbitrumRpcModule.PrepareForRecord(new PrepareForRecordParameters(start, end))
            .Result.Should().Be(Result.Success);
        BlockHeader endHeader = chain.BlockTree.FindHeader((long)end, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.MarkValid(new MarkValidParameters(end, endHeader.Hash!))
            .Result.Should().Be(Result.Success);

        BlockHeader? firstValidHdr = ReadValidHdr(chain.Container.Resolve<StateReconstructor>());
        firstValidHdr!.Number.Should().Be((long)start - 1, "first MarkValid should promote block start-1");

        // Second promotion: RecordBlockCreation → MarkValid → _validHdr = parent of last block
        DigestMessageParameters lastMessage = GetLastDigestedMessage();
        await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(lastMessage.Index, lastMessage.Message, WasmTargets: []));

        BlockHeader lastHeader = chain.BlockTree.FindHeader((long)lastMessage.Index, BlockTreeLookupOptions.RequireCanonical)!;
        chain.ArbitrumRpcModule.MarkValid(new MarkValidParameters(lastMessage.Index, lastHeader.Hash!))
            .Result.Should().Be(Result.Success);

        BlockHeader? secondValidHdr = ReadValidHdr(chain.Container.Resolve<StateReconstructor>());
        secondValidHdr!.Number.Should().Be((long)lastMessage.Index - 1,
            "second MarkValid should advance _validHdr to the recorded block's parent");
        secondValidHdr.Number.Should().BeGreaterThan(firstValidHdr.Number,
            "_validHdr should only advance forward");
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

        chain.ArbitrumRpcModule.MarkValid(new MarkValidParameters(end, Keccak.Zero))
            .Result.Should().Be(Result.Success); // returns success but silently skips

        ReadValidHdr(chain.Container.Resolve<StateReconstructor>()).Should().BeNull(
            "wrong ResultHash should not promote the candidate");
    }

    private static ArbitrumRpcTestBlockchain BuildChain() =>
        new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath))
            .Build(chain => chain.WorldStateManager.FlushCache(CancellationToken.None));

    private static DigestMessageParameters GetLastDigestedMessage()
    {
        FullChainSimulationRecordingFile recording = new(RecordingPath);
        return recording.GetDigestMessages().Last();
    }

    private static BlockHeader? ReadValidHdr(StateReconstructor stateReconstructor) =>
        (BlockHeader?)typeof(StateReconstructor)
            .GetField("_validHdr", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(stateReconstructor);
}
