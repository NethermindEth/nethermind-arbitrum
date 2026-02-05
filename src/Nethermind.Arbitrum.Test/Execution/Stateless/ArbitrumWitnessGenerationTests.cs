using FluentAssertions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumWitnessGenerationTests
{
    [TestCaseSource(nameof(ExecutionWitnessWithoutWasmsSource))]
    public async Task RecordBlockCreation_WitnessWithoutUserWasms_StatelessExecutionIsSuccessful(ulong messageIndex)
    {
        FullChainSimulationRecordingFile recording = new("./Recordings/1__arbos32_basefee92.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .Build();

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        ArbitrumWitness witness = recordResult.Witness;

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();
        ArbitrumStatelessBlockProcessingEnv blockProcessingEnv =
            new(witness, specProvider, Always.Valid, chain.StylusTargetConfig, chain.ArbosVersionProvider, chain.LogManager, chain.ArbitrumConfig);

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

    [TestCaseSource(nameof(ExecutionWitnessWithWasmsSource))]
    public async Task RecordBlockCreation_WitnessWithUserWasms_StatelessExecutionIsSuccessful(ulong messageIndex)
    {
        FullChainSimulationRecordingFile recording = new("./Recordings/5__stylus.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .Build();

        string[] wasmTargets = chain.StylusTargetConfig.GetWasmTargets().ToArray();
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: wasmTargets));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        ArbitrumWitness witness = recordResult.Witness;

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();
        ArbitrumStatelessBlockProcessingEnv blockProcessingEnv =
            new(witness, specProvider, Always.Valid, chain.StylusTargetConfig, chain.ArbosVersionProvider, chain.LogManager, chain.ArbitrumConfig);

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

    /// <summary>
    /// Verifies that EXTCODESIZE correctly records the target contract's code in the witness,
    /// even when followed by ISZERO (which would trigger a peephole optimization in base Nethermind).
    /// Arbitrum overrides EXTCODESIZE to always access the contract bytecode for witness generation.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_ExtCodeSizeFollowedByIsZero_StillRecordsTargetCodeInWitness()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Step 1: Fund the sender account with ETH deposit
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether());
        ResultWrapper<MessageResult> depositResult = await chain.Digest(deposit);
        depositResult.Result.Should().Be(Result.Success);

        // Step 2: Deploy a target contract with some bytecode (does not matter what it does)
        // Target contract runtime code: simply returns 42 when called
        // PUSH1 42, PUSH1 0, MSTORE, PUSH1 32, PUSH1 0, RETURN
        byte[] targetRuntimeCode = Prepare.EvmCode
            .PushData(42)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        // Init code that deploys the runtime code
        byte[] targetInitCode = Prepare.EvmCode
            .ForInitOf(targetRuntimeCode)
            .Done;

        Transaction deployTargetTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            deployTargetTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(null) // Contract creation
                .WithData(targetInitCode)
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> deployTargetResult = await chain.Digest(new TestL2Transactions(l1BaseFee, sender, deployTargetTx));
        deployTargetResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // Compute the deployed target contract address
        Address targetAddress = ContractAddress.From(sender, deployTargetTx.Nonce);

        // Step 3: Deploy a caller contract that uses EXTCODESIZE followed by ISZERO
        // This pattern would trigger peephole optimization in base Nethermind consequently not fetching the contract bytecode,
        // but Arbitrum should still record the full bytecode for witness generation.
        // Caller contract: EXTCODESIZE(target), ISZERO, PUSH1 0, MSTORE, PUSH1 32, PUSH1 0, RETURN
        byte[] callerRuntimeCode = Prepare.EvmCode
            .EXTCODESIZE(targetAddress)
            .Op(Instruction.ISZERO) // This triggers the peephole optimization pattern in base Nethermind
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        byte[] callerInitCode = Prepare.EvmCode
            .ForInitOf(callerRuntimeCode)
            .Done;

        Transaction deployCallerTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            deployCallerTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(null) // Contract creation
                .WithData(callerInitCode)
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> deployCallerResult = await chain.Digest(new TestL2Transactions(l1BaseFee, sender, deployCallerTx));
        deployCallerResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        Address callerAddress = ContractAddress.From(sender, deployCallerTx.Nonce);

        // Step 4: Call the caller contract (triggers EXTCODESIZE on target)
        Transaction callCallerTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            callCallerTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callerAddress)
                .WithData([])
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        // Use DigestAndGetParams to get the message parameters for RecordBlockCreation
        (ResultWrapper<MessageResult> callResult, DigestMessageParameters callParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, callCallerTx));
        callResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // Step 5: Call RecordBlockCreation to generate the witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        // Step 6: Verify the witness contains the target contract's code
        ArbitrumWitness witness = recordResult.Witness;
        byte[][] witnessCodes = witness.Witness.Codes;

        witnessCodes.Length.Should().Be(2, "Witness should contain both caller and target contract codes");

        // The target contract's code should be in the witness
        // (EXTCODESIZE should have triggered GetCode on the target)
        Hash256 targetCodeHash = Keccak.Compute(targetRuntimeCode);
        bool targetCodeInWitness = witnessCodes.Any(code => Keccak.Compute(code) == targetCodeHash);

        targetCodeInWitness.Should().BeTrue(
            "Target contract's code should be recorded in witness when EXTCODESIZE is called, " +
            "even if followed by ISZERO (peephole optimization pattern)");

        // Also verify the caller contract's code is in the witness (since we executed it)
        Hash256 callerCodeHash = Keccak.Compute(callerRuntimeCode);
        bool callerCodeInWitness = witnessCodes.Any(code => Keccak.Compute(code) == callerCodeHash);

        callerCodeInWitness.Should().BeTrue(
            "Caller contract's code should be recorded in witness since we executed it");
    }

    private static IEnumerable<TestCaseData> ExecutionWitnessWithoutWasmsSource()
    {
        // 18 blocks in the test where this test case source is used
        for (ulong blockNumber = 1; blockNumber <= 18; blockNumber++)
            yield return new TestCaseData(blockNumber);
    }

    private static IEnumerable<TestCaseData> ExecutionWitnessWithWasmsSource()
    {
        // 47 blocks in the test where this test case source is used
        for (ulong blockNumber = 1; blockNumber <= 47; blockNumber++)
            yield return new TestCaseData(blockNumber);
    }

    private static T ThrowOnFailure<T>(ResultWrapper<T> result, ulong msgIndex)
    {
        if (result.Result != Result.Success)
            throw new InvalidOperationException($"Failed to execute RPC method, message index {msgIndex}, code {result.ErrorCode}: {result.Result.Error}");

        return result.Data;
    }
}
