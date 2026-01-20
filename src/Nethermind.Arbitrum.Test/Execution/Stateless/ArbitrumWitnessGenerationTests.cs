using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Stateless;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.JsonRpc;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumWitnessGenerationTests
{
    [TestCase(1ul)]
    [TestCase(2ul)]
    [TestCase(3ul)]
    [TestCase(4ul)]
    [TestCase(5ul)]
    [TestCase(6ul)]
    [TestCase(7ul)]
    [TestCase(8ul)]
    [TestCase(9ul)]
    [TestCase(10ul)]
    [TestCase(11ul)]
    [TestCase(12ul)]
    [TestCase(13ul)]
    [TestCase(14ul)]
    [TestCase(15ul)]
    [TestCase(16ul)]
    [TestCase(17ul)]
    [TestCase(18ul)]
    public async Task RecordBlockCreation_Witness_AllowsStatelessExecution(ulong messageIndex)
    {
        FullChainSimulationRecordingFile recording = new("./Recordings/1__arbos32_basefee92.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .Build();

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        Witness witness = recordResult.Witness;

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();
        ArbitrumStatelessBlockProcessingEnv blockProcessingEnv =
            new(witness, specProvider, Always.Valid, chain.WasmStore, chain.ArbosVersionProvider, chain.LogManager);

        Block block = chain.BlockFinder.FindBlock(recordResult.BlockHash)
            ?? throw new ArgumentException($"Unable to find block {recordResult.BlockHash}");
        BlockHeader parent = chain.BlockFinder.FindHeader(block.ParentHash!)
            ?? throw new ArgumentException($"Unable to find parent for block {recordResult.BlockHash}");

        using (blockProcessingEnv.WorldState.BeginScope(parent))
        {
            (Block processed, TxReceipt[] _) = blockProcessingEnv.BlockProcessor.ProcessOne(
                block,
                ProcessingOptions.DoNotUpdateHead | ProcessingOptions.ReadOnlyChain,
                NullBlockTracer.Instance,
                specProvider.GetSpec(block.Header));

            Assert.That(processed.Hash, Is.EqualTo(block.Hash));
        }
    }

    private static T ThrowOnFailure<T>(ResultWrapper<T> result, ulong msgIndex)
    {
        if (result.Result != Result.Success)
            throw new InvalidOperationException($"Failed to execute RPC method, message index {msgIndex}, code {result.ErrorCode}: {result.Result.Error}");

        return result.Data;
    }
}
