// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Autofac;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.State.Proofs;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumWitnessGenerationTests
{
    // Shared, lazily-built chains keyed by recording file path, reused read-only across every
    // recording-driven test case in this fixture (the StatelessExecution, CaptureAsms and
    // CapturesExactWitness sources). Recording 5's 47-block chain, for example, used to be rebuilt
    // ~140 times; now it is built once and shared. Disposed once after the whole fixture runs. Tests
    // here execute sequentially (no [Parallelizable]), but the lock keeps GetOrBuild safe regardless.
    private static readonly Dictionary<string, (ArbitrumRpcTestBlockchain Chain, FullChainSimulationRecordingFile Recording)> SharedRecordingChains = new();
    private static readonly Lock SharedRecordingChainsLock = new();

    [OneTimeTearDown]
    public void DisposeSharedRecordingChains()
    {
        lock (SharedRecordingChainsLock)
        {
            foreach ((ArbitrumRpcTestBlockchain chain, _) in SharedRecordingChains.Values)
                chain.Dispose();

            SharedRecordingChains.Clear();
        }
    }

    [TestCaseSource(nameof(ExecutionWitnessWithoutStylusSource))]
    public async Task RecordBlockCreation_WitnessWithoutUserWasms_StatelessExecutionIsSuccessful(ulong messageIndex)
    {
        (ArbitrumRpcTestBlockchain chain, FullChainSimulationRecordingFile recording) = GetReadOnlySharedRecordingChain("./Recordings/1__arbos32_basefee92.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        // Just to get witness because RecordResult just contains the whole mixed preimages dictionary
        using ArbitrumWitness witness = await chain.BuildBlockWitness(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: []));
        AssertWitnessMatchesRecordResult(witness, recordResult);

        ISpecProvider specProvider = chain.Container.Resolve<ISpecProvider>();
        using IArbitrumStatelessBlockProcessingEnvScope blockProcessingEnv =
            chain.Container.Resolve<ArbitrumStatelessBlockProcessingEnvFactory>().CreateScope(witness);

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

    [TestCaseSource(nameof(ExecutionWitnessWithStylusSource))]
    public async Task RecordBlockCreation_WitnessWithUserWasms_StatelessExecutionIsSuccessful(ulong messageIndex, Address[] _)
    {
        (ArbitrumRpcTestBlockchain chain, FullChainSimulationRecordingFile recording) = GetReadOnlySharedRecordingChain("./Recordings/5__stylus.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        string[] wasmTargets = chain.StylusTargetConfig.GetWasmTargets().ToArray();
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: wasmTargets));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        using ArbitrumWitness witness = await chain.BuildBlockWitness(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: wasmTargets));
        AssertWitnessMatchesRecordResult(witness, recordResult);

        ISpecProvider specProvider = chain.Container.Resolve<ISpecProvider>();
        using IArbitrumStatelessBlockProcessingEnvScope blockProcessingEnv =
            chain.Container.Resolve<ArbitrumStatelessBlockProcessingEnvFactory>().CreateScope(witness);

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

    [TestCaseSource(nameof(ExecutionWitnessWithStylusSource))]
    public async Task RecordBlockCreation_WitnessWithUserWasms_CaptureAsms(ulong messageIndex, Address[] executedStylusContracts)
    {
        (ArbitrumRpcTestBlockchain chain, FullChainSimulationRecordingFile recording) = GetReadOnlySharedRecordingChain("./Recordings/5__stylus.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        string[] wasmTargets = chain.StylusTargetConfig.GetWasmTargets().ToArray();
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: wasmTargets));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        // Build expected dictionary from chain state using the stylus contract addresses
        Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>> expected = new();
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            ArbosState arbosState = ArbosState.OpenArbosState(chain.MainWorldState, new SystemBurner(), NullLogger.Instance);

            foreach (Address contract in executedStylusContracts)
            {
                ValueHash256 codeHash = chain.MainWorldState.GetCodeHash(contract);
                ValueHash256 moduleHash = arbosState.Programs.ModuleHashesStorage.Get(codeHash);

                Dictionary<string, byte[]> expectedAsms = new();
                foreach (string target in wasmTargets)
                {
                    chain.WasmStore.TryGetActivatedAsm(target, in moduleHash, out byte[]? asmBytes).Should().BeTrue(
                        $"WasmStore should contain ASM for module {moduleHash}, target '{target}'");
                    expectedAsms[target] = asmBytes!;
                }

                expected[moduleHash.ToHash256()] = expectedAsms;
            }
        }

        Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>> actual = recordResult.UserWasms ?? [];

        actual.Should().BeEquivalentTo(expected);
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
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build(); // no need to flush trie here, do it before RecordBlockCreation call

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Step 1: Fund the sender account with ETH deposit
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether);
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
                .WithMaxFeePerGas(10.GWei)
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
                .WithMaxFeePerGas(10.GWei)
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
                .WithMaxFeePerGas(10.GWei)
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

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        // Step 5: Call RecordBlockCreation to generate the witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        using ArbitrumWitness witness = await chain.BuildBlockWitness(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        AssertWitnessMatchesRecordResult(witness, recordResult);

        // Step 6: Verify the witness contains the target contract's code
        byte[][] witnessCodes = witness.Witness.Codes.ToArray();

        witnessCodes.Length.Should().Be(2, "Witness should contain both caller and target contract codes");

        // The target contract's code should be in the witness
        // (EXTCODESIZE should have triggered GetCode on the target)
        Hash256 targetCodeHash = Keccak.Compute(targetRuntimeCode);
        witnessCodes.Any(code => Keccak.Compute(code) == targetCodeHash).Should().BeTrue(
            "Target contract's code should be recorded in witness when EXTCODESIZE is called, " +
            "even if followed by ISZERO (peephole optimization pattern)");

        // Also verify the caller contract's code is in the witness (since we executed it)
        Hash256 callerCodeHash = Keccak.Compute(callerRuntimeCode);
        witnessCodes.Any(code => Keccak.Compute(code) == callerCodeHash).Should().BeTrue(
            "Caller contract's code should be recorded in witness since we executed it");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_ExtCodeSizeFollowedByIsZero_StillRecordsTargetCodeInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that witness generation correctly records precompile bytecodes:
    /// - Arbitrum precompiles (e.g., ArbSys): should record 0xfe (INVALID opcode)
    /// - Ethereum precompiles (e.g., ecrecover): should NOT record any bytecode (empty code)
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_PrecompileCalls_RecordsArbitrumPrecompileCodeButNotEthereumPrecompileCode()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account with ETH deposit
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether);
        ResultWrapper<MessageResult> depositResult = await chain.Digest(deposit);
        depositResult.Result.Should().Be(Result.Success);

        // Create two transactions:
        // 1. Call ArbSys (Arbitrum precompile at 0x64) - has 0xfe bytecode stored
        // 2. Call ecrecover (Ethereum precompile at 0x01) - has no bytecode stored

        Address arbSysAddress = ArbSys.Address; // 0x64
        Address ecrecoverAddress = new("0x0000000000000000000000000000000000000001");

        byte[] arbBlockNumberCalldata = Keccak.Compute("arbBlockNumber()"u8).Bytes[..4].ToArray();

        Transaction callArbSysTx;
        Transaction callEcrecoverTx;

        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Transaction 1: Call ArbSys.arbBlockNumber()
            callArbSysTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(arbSysAddress)
                .WithData(arbBlockNumberCalldata)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(100_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;

            // Transaction 2: Call ecrecover with dummy data (will fail but still executes precompile)
            callEcrecoverTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(ecrecoverAddress)
                .WithData(new byte[128]) // ecrecover expects 128 bytes (hash, v, r, s)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(100_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender) + 1)
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        // Digest both transactions in a single block
        (ResultWrapper<MessageResult> callResult, DigestMessageParameters callParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, callArbSysTx, callEcrecoverTx));
        callResult.Result.Should().Be(Result.Success);

        // Both transactions should succeed (ecrecover returns empty on invalid input but doesn't revert)
        TxReceipt[] receipts = chain.LatestReceipts();
        receipts[1].StatusCode.Should().Be(StatusCode.Success, "ArbSys call should succeed");
        receipts[2].StatusCode.Should().Be(StatusCode.Success, "ecrecover call should succeed");

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        // Call RecordBlockCreation to generate the witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        using ArbitrumWitness witness = await chain.BuildBlockWitness(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        AssertWitnessMatchesRecordResult(witness, recordResult);

        byte[][] witnessCodes = witness.Witness.Codes.ToArray();

        // Arbitrum precompile bytecode is 0xfe (INVALID opcode)
        byte[] arbitrumPrecompileCode = Arbitrum.Arbos.Precompiles.InvalidCode;

        // The witness should contain the Arbitrum precompile's code (0xfe)
        witnessCodes.Any(code => code.SequenceEqual(arbitrumPrecompileCode)).Should().BeTrue(
            "Arbitrum precompile bytecode (0xfe) should be recorded in witness when calling ArbSys");

        // Verify that no empty code is recorded (Ethereum precompiles have no stored bytecode)
        witnessCodes.Any(code => code.Length == 0).Should().BeFalse(
            "Ethereum precompiles empty bytecode should not be recorded in witness");

        // The witness should have exactly 1 code: Arbitrum precompile (0xfe)
        // No code from Ethereum precompile since it has empty bytecode
        witnessCodes.Length.Should().Be(1, "Witness should contain only Arbitrum precompile code (0xfe)");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_PrecompileCalls_RecordsArbitrumPrecompileCodeButNotEthereumPrecompileCode),
            recordResult);
    }

    /// <summary>
    /// Verifies that witness generation for BLOCKHASH always accesses storage (not the L1BlockCache).
    /// The witness-generating VM has its own fresh cache and must access storage to get block hashes.
    /// The storage access records the corresponding trie nodes in the witness.
    /// Storage slot: 1 + l1BlockNumber % 256 in Blockhashes substorage.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_BlockHashOpcode_RecordsStorageTrieNodeInWitness()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account with ETH deposit
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether);
        ResultWrapper<MessageResult> depositResult = await chain.Digest(deposit);
        depositResult.Result.Should().Be(Result.Success);

        // Get the current L1 block number from the chain to compute a valid block number for BLOCKHASH
        // BLOCKHASH returns the hash of the given L1 block number if it's within the last 256 blocks
        ulong currentL1BlockNumber = chain.LatestL1BlockNumber;

        // Deploy a contract that uses BLOCKHASH opcode
        // Contract: BLOCKHASH(currentL1BlockNumber - 1), PUSH1 0, MSTORE, PUSH1 32, PUSH1 0, RETURN
        // This returns the hash of a previous L1 block
        ulong targetL1BlockNumber = currentL1BlockNumber > 0 ? currentL1BlockNumber - 1 : 0;

        byte[] blockhashCallerCode = Prepare.EvmCode
            .PushData(targetL1BlockNumber)
            .Op(Instruction.BLOCKHASH)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        byte[] blockhashCallerInitCode = Prepare.EvmCode
            .ForInitOf(blockhashCallerCode)
            .Done;

        Transaction deployTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            deployTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(null) // Contract creation
                .WithData(blockhashCallerInitCode)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> deployResult = await chain.Digest(new TestL2Transactions(l1BaseFee, sender, deployTx));
        deployResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        Address contractAddress = ContractAddress.From(sender, deployTx.Nonce);

        // Step 1: Call the contract (this populates the main VM's L1BlockCache)
        Transaction callTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            callTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(contractAddress)
                .WithData([])
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(100_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> call2Result, DigestMessageParameters call2Params) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, callTx));
        call2Result.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        // Step 2: Call the contract again (would use cached value from the block that just got built
        // if cache were persisted into witness-generating env)
        // Making sure witness-generating VM has its own empty cache and must access storage, recording the trie nodes.
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(call2Params.Index, call2Params.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, call2Params.Index);

        // The storage slot accessed is: 1 + l1BlockNumber % 256 in the Blockhashes substorage (see GetL1BlockHash)
        // Too difficult to predict exact trie node hash here, so, using the hardcoded value (found during debugging)
        recordResult.Preimages.ContainsKey(new Hash256("0x30cfd2590e997a3c3bee0c89572aec183bae0976e06334354832b85514d0d37a")).Should().BeTrue(
            "Witness state should contain leaf trie node for BLOCKHASH storage access");
        // Similarly, checking for an intermediate node capture when accessing the storage slot
        recordResult.Preimages.ContainsKey(new Hash256("0xad9a2d73baabd92487dd1840cd076a06a3eded05e8cbdebb930ddad669e51880")).Should().BeTrue(
            "Witness state should contain intermediate trie node for BLOCKHASH storage access");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_BlockHashOpcode_RecordsStorageTrieNodeInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that calling ArbSys.ArbBlockHash records all headers between the requested block
    /// and the current block's parent in the witness.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_ArbBlockHash_RecordsHeadersInWitness()
    {
        FullChainSimulationRecordingFile recording = new("./Recordings/1__arbos32_basefee92.jsonl");

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 l1BaseFee = 92;

        // After the recording, we have blocks 0 (genesis) through 18. We'll call ArbSys.arbBlockHash for block 7
        ulong targetBlockNumber = 7;
        byte[] arbBlockHashCalldata = Keccak.Compute("arbBlockHash(uint256)"u8).Bytes[..4].ToArray();
        byte[] calldata = new byte[36];
        arbBlockHashCalldata.CopyTo(calldata, 0);
        new UInt256(targetBlockNumber).ToBigEndian(calldata.AsSpan(4));

        Transaction callTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            callTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(ArbSys.Address)
                .WithData(calldata)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(100_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> callResult, DigestMessageParameters callParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, callTx));
        callResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success, "ArbBlockHash call should succeed");

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        using ArbitrumWitness witness = await chain.BuildBlockWitness(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        AssertWitnessMatchesRecordResult(witness, recordResult);

        // The witness should contain all RLP-encoded headers from targetBlockNumber to parentBlockNumber (inclusive)
        long parentBlockNumber = chain.BlockTree.Head!.Number - 1;

        HashSet<Hash256> expectedHeaderHashes = new();
        for (long blockNum = (long)targetBlockNumber; blockNum <= parentBlockNumber; blockNum++)
            expectedHeaderHashes.Add(chain.BlockTree.FindBlock(blockNum)!.Hash!);

        // Witness headers are raw rlp-encoded headers; compute their hashes for comparison
        HashSet<Hash256> actualHeaderHashes = witness.Witness.Headers
            .Select(header => Keccak.Compute(header))
            .ToHashSet();

        // Compare hashsets instead of lists to avoid ordering issues, as order in witness does not matter
        actualHeaderHashes.Should().BeEquivalentTo(expectedHeaderHashes);

        AssertExpectedWitness(
            nameof(RecordBlockCreation_ArbBlockHash_RecordsHeadersInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that submitting a retryable transaction with empty calldata still traverses the trie
    /// for the calldata storage slot and records the trie nodes up that path in the witness.
    /// This tests the fix in ArbosStorage.Set(byte[]) where "Set(offset, Hash256.FromBytesWithPadding(span));"
    /// was previously surrounded by "if (span.Length > 0)" to ensure it is now always called.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_SubmitRetryableWithEmptyCalldata_RecordsCalldataTrieNodeInWitness()
    {
        FullChainSimulationRecordingFile recording = new("./Recordings/1__arbos32_basefee92.jsonl");

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address receiver = TestItem.AddressA;
        Address beneficiary = TestItem.AddressB;
        UInt256 l1BaseFee = 92;

        // GasLimit is set to 0 to make submit retryable tx finish early (without emitting RedeemScheduledEvent event)
        // Hence, no call to retryable.Calldata.Get() and therefore no trie node recorded without the fix.
        TestSubmitRetryable retryable = new(
            Hash256.FromBytesWithPadding([0x1]),
            l1BaseFee,
            sender,
            receiver,
            beneficiary,
            DepositValue: 10.Ether,
            RetryValue: 1.Ether,
            GasFee: 1.GWei,
            GasLimit: 0,
            MaxSubmissionFee: 128800);

        // Compute the tx hash to know which substorage path CreateRetryable will use
        ArbitrumSubmitRetryableTransaction transaction = new()
        {
            SourceHash = retryable.RequestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = retryable.GasFee,
            GasLimit = (long)retryable.GasLimit,
            Value = 0,
            Data = retryable.RetryData,
            IsOPSystemTransaction = false,
            Mint = retryable.DepositValue,
            ChainId = chain.ChainSpec.ChainId,
            RequestId = retryable.RequestId,
            SenderAddress = retryable.Sender,
            L1BaseFee = retryable.L1BaseFee,
            DepositValue = retryable.DepositValue,
            GasFeeCap = retryable.GasFee,
            Gas = retryable.GasLimit,
            RetryTo = retryable.Receiver,
            RetryValue = retryable.RetryValue,
            Beneficiary = retryable.Beneficiary,
            MaxSubmissionFee = retryable.MaxSubmissionFee,
            FeeRefundAddr = retryable.Beneficiary,
            RetryData = retryable.RetryData
        };
        Hash256 txHash = transaction.CalculateHash();

        // Pre-populate the calldata substorage offset 1 with a non-zero value.
        // This ensures a leaf trie node exists at that slot. When CreateRetryable calls
        // Calldata.Set([]) with the fix, it writes zero to offset 1 (deleting the leaf),
        // which captures the leaf trie node (to know where to create the new leaf) in the witness.
        // Without the fix, offset 1 is never accessed and the trie nodes along that storage slot path are not captured.
        //
        // Create a fake block with the new state root so the next DigestMessage sees it.
        //
        // A bit of a hack but without this, setting up the test is almost impossible / kinda random
        // and hardly maintainable. Because, then you'd need to record an intermediate trie
        // node instead of the leaf node, because regular scenarios won't let you have a leaf node there beforehand.
        // And to do that, you'd need to influence the tx parameters to change its hash to change the calldata storage slot path,
        // and pray that it accesses some intermediate trie node that is not already accessed elsewhere in the block,
        // which is very fragile. And some slight future code change could easily change the trie structure
        // and break the test without any code changes to the test or witness generation itself.
        // Trust me, I spent too much time on this test.
        chain.AppendBlock(chain =>
        {
            ArbosStorage calldataStorage = new ArbosStorage(chain.MainWorldState, new SystemBurner(), ArbosAddresses.ArbosSystemAccount)
                .OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace)
                .OpenSubStorage(txHash.BytesToArray())
                .OpenSubStorage(Retryable.CallDataKey);
            // Just giving the offset 1 some random non-zero value to create a leaf node in the trie for that storage slot
            calldataStorage.Set(1, Hash256.FromBytesWithPadding([0x5]));
        });

        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(retryable);
        result.Result.Should().Be(Result.Success);

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        // Assert some trie node on the path to the calldata storage slot has been captured (not captured elsewhere during block recording ofc, otherwise test is useless)
        // Here I assert the leaf node hash (found during debugging).
        recordResult.Preimages.ContainsKey(new Hash256("0xb2020a6fea12f86ace9de5bed3312ca953a2f8ae0730062fa9df4fc833c99782")).Should().BeTrue(
            "Witness state should contain trie node for retryable empty calldata storage slot");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_SubmitRetryableWithEmptyCalldata_RecordsCalldataTrieNodeInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that TryReapOneRetryable reads the TimeoutWindowsLeft storage slot (offset 6)
    /// even when the retryable has not expired (early return path at timeout >= currentTimestamp).
    /// This tests the fix where TimeoutWindowsLeft.Get() was moved before the expiration check,
    /// matching Nitro's behavior and ensuring the storage slot is captured in the witness.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_TryReapRetryableNotExpired_RecordsTimeoutWindowsLeftInWitness()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock()
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 l1BaseFee = 92;

        // Step 1: Submit a retryable with GasLimit: 0 (no auto-redeem).
        // This creates a retryable ticket and enqueues it in the timeout queue.
        // Timeout is set to currentTimestamp + 1 week, so it won't expire in the next block.
        TestSubmitRetryable retryable = new(
            Hash256.FromBytesWithPadding([0x1]),
            l1BaseFee,
            sender,
            TestItem.AddressA,
            TestItem.AddressB,
            DepositValue: 10.Ether,
            RetryValue: 1.Ether,
            GasFee: 1.GWei,
            GasLimit: 0,
            MaxSubmissionFee: 128800);

        ResultWrapper<MessageResult> retryableResult = await chain.Digest(retryable);
        retryableResult.Result.Should().Be(Result.Success);

        // Compute the tx hash to know which substorage path CreateRetryable used
        ArbitrumSubmitRetryableTransaction transaction = new()
        {
            SourceHash = retryable.RequestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = retryable.GasFee,
            GasLimit = (long)retryable.GasLimit,
            Value = 0,
            Data = retryable.RetryData,
            IsOPSystemTransaction = false,
            Mint = retryable.DepositValue,
            ChainId = chain.ChainSpec.ChainId,
            RequestId = retryable.RequestId,
            SenderAddress = retryable.Sender,
            L1BaseFee = retryable.L1BaseFee,
            DepositValue = retryable.DepositValue,
            GasFeeCap = retryable.GasFee,
            Gas = retryable.GasLimit,
            RetryTo = retryable.Receiver,
            RetryValue = retryable.RetryValue,
            Beneficiary = retryable.Beneficiary,
            MaxSubmissionFee = retryable.MaxSubmissionFee,
            FeeRefundAddr = retryable.Beneficiary,
            RetryData = retryable.RetryData
        };
        Hash256 txHash = transaction.CalculateHash();

        // CreateRetryable calls TimeoutWindowsLeft.Set(0) which stores empty bytes, deleting any leaf at that slot.
        // Without a leaf, reading the slot only traverses shared intermediate nodes that might likely also be captured
        // by the many other ArbOS storage accesses in the same block — making any assertion on those nodes unreliable.
        // Pre-populating with a non-zero value after retryable creation creates a unique leaf,
        // so the assertion targets something that only appears when TimeoutWindowsLeft.Get() is called.
        // Same reason and hack as the previous test to create a fake block with the new state root.
        chain.AppendBlock(chain =>
        {
            ArbosStorage retryableStorage = new ArbosStorage(chain.MainWorldState, new SystemBurner(), ArbosAddresses.ArbosSystemAccount)
                .OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace)
                .OpenSubStorage(txHash.BytesToArray());
            retryableStorage.Set(Retryable.TimeoutWindowsLeftOffset, Hash256.FromBytesWithPadding([0x5]));
        });

        // Step 3: Create the next block to trigger a start tx, which calls TryReapOneRetryable.
        // The retryable's timeout (~1 week from now) >= currentTimestamp, so it returns early.
        // With the fix, TimeoutWindowsLeft.Get() is called before the check, capturing the storage slot.
        Transaction transferTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            transferTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(TestItem.AddressC)
                .WithData([])
                .WithMaxFeePerGas(1.GWei)
                .WithGasLimit(21_000)
                .WithValue(1)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, transferTx));
        result.Result.Should().Be(Result.Success);

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        // Assert the leaf trie node for TimeoutWindowsLeft (offset 6) has been captured.
        // Trie node hash determined during debugging — without the fix, this node would NOT be in the witness.
        recordResult.Preimages.ContainsKey(new Hash256("0xb9b0e8140da26e36ad74be6f20e6dc5073cda81b1ed9c3c8d63388f69640f24e")).Should().BeTrue(
            "Witness state should contain trie node for retryable TimeoutWindowsLeft storage slot");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_TryReapRetryableNotExpired_RecordsTimeoutWindowsLeftInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that CanAddTransaction reads BrotliCompressionLevel (ArbOS root offset 7) for
    /// non-user transactions (such as the StartBlock tx), matching Nitro's behavior where
    /// the data gas calculation runs for all transactions regardless of type.
    /// Previously, non-user txs returned early from CanAddTransaction before reaching the
    /// BrotliCompressionLevel.Get() call, so the corresponding trie node was not captured.
    ///
    /// An EndOfBlock message produces a block containing only the StartBlock internal tx (no user
    /// transactions). This isolates the test: BrotliCompressionLevel can only be captured by the
    /// internal tx's CanAddTransaction path, not by any user tx execution or gas charging hook.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_NonUserTransaction_RecordsBrotliCompressionLevelInWitness()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock()
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        UInt256 l1BaseFee = 92;

        // EndOfBlock message produces a block with only the StartBlock internal tx (no user txs).
        // BrotliCompressionLevel can only be captured by the internal tx's CanAddTransaction path.
        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestEndOfBlock(l1BaseFee));
        result.Result.Should().Be(Result.Success);

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        // Assert the leaf trie node for BrotliCompressionLevel (offset 7) has been captured.
        // Trie node hash determined during debugging — without the fix, this node would NOT be
        // in the witness because non-user txs returned early from CanAddTransaction.
        recordResult.Preimages.ContainsKey(new Hash256("0x9bcf99179b305f1d54185508b47cc61fb0f8b804dd449a9b60ed068af7b1d62f")).Should().BeTrue(
            "Witness state should contain trie node for BrotliCompressionLevel storage slot (offset 7)");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_NonUserTransaction_RecordsBrotliCompressionLevelInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that when a storage slot is modified by one transaction and reset to its original
    /// value through SSTORE opcode within the same block, the witness still captures the storage
    /// trie nodes.
    ///
    /// Even if the final net change is zero, the storage slot is anyway accessed (read) during SSTORE execution
    /// and therefore trie nodes should be contained in the witness.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_WhenStorageSlotModifiedAndResetInSameBlockThroughSStoreOpcode_StillRecordsStorageTrieNodes()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account
        ResultWrapper<MessageResult> depositResult = await chain.Digest(new TestEthDeposit(
            Keccak.Compute("deposit"), l1BaseFee, sender, sender, 100.Ether));
        depositResult.Result.Should().Be(Result.Success);

        // Deploy a simple setter contract: SSTORE(slot=0, value=CALLDATALOAD(0))
        // Constructor also initializes slot 0 to value 1.
        UInt256 storageSlot = 0;
        UInt256 initialValue = 1;
        byte[] setterRuntimeCode = Prepare.EvmCode
            .PushData(0)                    // offset for CALLDATALOAD
            .Op(Instruction.CALLDATALOAD)   // load 32 bytes from calldata
            .PushData(storageSlot)          // storage slot
            .Op(Instruction.SSTORE)         // store
            .Op(Instruction.STOP)
            .Done;

        byte[] setterInitCode = Prepare.EvmCode
            .PushData(initialValue)
            .PushData(0)                    // storage slot 0
            .Op(Instruction.SSTORE)         // initialize slot 0 with initial value
            .ForInitOf(setterRuntimeCode)   // 3 instructions above correspond to constructor
            .Done;

        Transaction deployTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            deployTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(null) // contract creation
                .WithData(setterInitCode)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> deployResult = await chain.Digest(new TestL2Transactions(l1BaseFee, sender, deployTx));
        deployResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success, "contract deployment should succeed");

        Address contractAddress = ContractAddress.From(sender, deployTx.Nonce);

        // Parent header is the state BEFORE the modify/reset block
        BlockHeader parentHeader = chain.BlockTree.Head!.Header;

        // Create two transactions in the same block:
        // TX1: set slot 0 to value 2 (modifies storage)
        // TX2: set slot 0 back to value 1 (resets to original)
        byte[] setTo2 = new UInt256(2).ToBigEndian();
        byte[] setToInitialValue = initialValue.ToBigEndian();

        Transaction tx1;
        Transaction tx2;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            UInt256 nonce = chain.MainWorldState.GetNonce(sender);

            tx1 = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(contractAddress)
                .WithData(setTo2)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(100_000)
                .WithValue(0)
                .WithNonce(nonce)
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;

            tx2 = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(contractAddress)
                .WithData(setToInitialValue)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(100_000)
                .WithValue(0)
                .WithNonce(nonce + 1)
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, tx1, tx2));
        result.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success, "TX1 (set to 2) should succeed");
        chain.LatestReceipts()[2].StatusCode.Should().Be(StatusCode.Success, "TX2 (reset to 1) should succeed");

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        // Record block creation and generate witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        // Assert the storage slot still has its original value
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            chain.MainWorldState.Get(new(contractAddress, storageSlot)).ToArray().Should().BeEquivalentTo(
                initialValue.ToBigEndian().WithoutLeadingZeros().ToArray());
        }

        // Collect the expected storage proof from the parent state.
        AccountProofCollector collector = new(contractAddress, [storageSlot]);
        chain.StateReader.RunTreeVisitor(collector, parentHeader);
        AccountProof accountProof = collector.BuildResult();

        byte[][] storageProofNodes = accountProof.StorageProofs!
            .SelectMany(sp => sp.Proof!)
            .ToArray();

        storageProofNodes.Should().NotBeEmpty(
            "the contract should have a non-empty storage proof for slot 0 in the parent state");

        foreach (byte[] proofNode in storageProofNodes)
        {
            recordResult.Preimages.ContainsKey(Keccak.Compute(proofNode)).Should().BeTrue(
                "witness should contain storage trie proof node even when the net storage change " +
                "is zero (slot was modified by TX1 then reset to original value by TX2)");
        }

        AssertExpectedWitness(
            nameof(RecordBlockCreation_WhenStorageSlotModifiedAndResetInSameBlockThroughSStoreOpcode_StillRecordsStorageTrieNodes),
            recordResult);
    }

    /// <summary>
    /// Similar to the above (storage slot set then reset) but instead of using SSTORE, we modify the
    /// state directly through the WorldState, and therefore the storage slot written to has not been read before.
    ///
    /// Since Nethermind caches writes and commits storage changes per-block, the net change is zero and
    /// but the trie nodes are still traversed during commit. This makes sense because even if the value has been reset,
    /// we do not know its original value.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_WhenStateModifiedAndResetDirectlyViaWorldStateNotSStoreOpcode_StillRecordsStorageTrieNodes()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether);
        ResultWrapper<MessageResult> depositResult = await chain.Digest(deposit);
        depositResult.Result.Should().Be(Result.Success);

        // Pre-populate NetworkFeeAccount (ArbOS root storage, offset 3) with a known original value.
        // This creates a leaf trie node at that storage path so the assertion targets a unique trie node.
        Address originalFeeAccount = TestItem.AddressB;
        chain.AppendBlock(chain =>
        {
            ArbosState arbosState = ArbosState.OpenArbosState(chain.MainWorldState, new SystemBurner(), NullLogger.Instance);
            arbosState.NetworkFeeAccount.Set(originalFeeAccount);
        });

        BlockHeader parentHeader = chain.BlockTree.Head!.Header;

        byte[] selector = Keccak.Compute("setNetworkFeeAccount(address)"u8).Bytes[..4].ToArray();

        Address newFeeAccount = TestItem.AddressC;

        byte[] setToNewCalldata = new byte[36];
        selector.CopyTo(setToNewCalldata, 0);
        newFeeAccount.Bytes.CopyTo(setToNewCalldata.AsSpan(16));

        byte[] resetToOriginalCalldata = new byte[36];
        selector.CopyTo(resetToOriginalCalldata, 0);
        originalFeeAccount.Bytes.CopyTo(resetToOriginalCalldata.AsSpan(16));

        // TX1: Set NetworkFeeAccount to newFeeAccount
        // TX2: Reset NetworkFeeAccount back to originalFeeAccount
        Transaction tx1;
        Transaction tx2;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            UInt256 nonce = chain.MainWorldState.GetNonce(sender);

            tx1 = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(ArbosAddresses.ArbOwnerAddress)
                .WithData(setToNewCalldata)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(nonce)
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;

            tx2 = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(ArbosAddresses.ArbOwnerAddress)
                .WithData(resetToOriginalCalldata)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(nonce + 1)
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, tx1, tx2));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.LatestReceipts();
        receipts[1].StatusCode.Should().Be(StatusCode.Success, "TX1 (set to new) should succeed");
        receipts[2].StatusCode.Should().Be(StatusCode.Success, "TX2 (reset to original) should succeed");

        // Assert NetworkFeeAccount still has its original value
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ArbosState arbosState = ArbosState.OpenArbosState(chain.MainWorldState, new SystemBurner(), NullLogger.Instance);
            arbosState.NetworkFeeAccount.Get().Should().Be(originalFeeAccount,
                "NetworkFeeAccount should retain its original value after modify + reset in the same block");
        }

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        // Record block creation and generate witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        // Assert the network fee account is indeed the original one (changed then reset)
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ArbosState arbosState = ArbosState.OpenArbosState(chain.MainWorldState, new SystemBurner(), NullLogger.Instance);
            arbosState.NetworkFeeAccount.Get().Should().Be(originalFeeAccount);
        }

        // NetworkFeeAccount is at root BackingStorage (empty storageKey), offset 3
        UInt256 networkFeeAccountSlot = ComputeMappedStorageSlot([], ArbosStateOffsets.NetworkFeeAccountOffset);

        // Collect expected storage proof from the parent state
        AccountProofCollector collector = new(ArbosAddresses.ArbosSystemAccount, [networkFeeAccountSlot]);
        chain.StateReader.RunTreeVisitor(collector, parentHeader);
        AccountProof accountProof = collector.BuildResult();

        byte[][] storageProofNodes = accountProof.StorageProofs!
            .SelectMany(sp => sp.Proof!)
            .ToArray();

        storageProofNodes.Should().NotBeEmpty(
            "NetworkFeeAccount slot should have a non-empty storage proof in the parent state");

        foreach (byte[] proofNode in storageProofNodes)
        {
            recordResult.Preimages.ContainsKey(Keccak.Compute(proofNode)).Should().BeTrue(
                "witness should contain storage trie proof node for NetworkFeeAccount " +
                "even when the net storage change is zero (modified by TX1 then reset by TX2)");
        }

        AssertExpectedWitness(
            nameof(RecordBlockCreation_WhenStateModifiedAndResetDirectlyViaWorldStateNotSStoreOpcode_StillRecordsStorageTrieNodes),
            recordResult);
    }

    /// <summary>
    /// Verifies that when a transaction reverts, the witness still captures the storage trie nodes
    /// for the storage slots written during execution.
    /// In Nethermind, storage writes are cached and only applied to the trie during the commit phase.
    /// A revert discards the cached writes, so the trie is never traversed for those paths. The
    /// AccountProofCollector pass in GetWitness compensates by explicitly collecting proofs for all
    /// tracked storage slots, matching Nitro's behavior where trie nodes are captured regardless of reverts.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_TransactionSetsSomeStateButReverts_StillRecordsStorageTrieNodes()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether);
        ResultWrapper<MessageResult> depositResult = await chain.Digest(deposit);
        depositResult.Result.Should().Be(Result.Success);

        // Pre-populate AddressTable._backingStorage at offset 1 with a non-zero value to create
        // a unique leaf node. When Register is called for the first time on a new address, it
        // increments numItems from 0 to 1 and writes to _backingStorage at offset 1. Pre-populating
        // ensures a leaf trie node exists at that path, so the assertion targets a node that only
        // appears when this specific storage slot is accessed — not captured by other ArbOS operations.
        // Just a hack to make test deterministic and reliable.
        chain.AppendBlock(chain =>
        {
            ArbosStorage backingStorage = new ArbosStorage(chain.MainWorldState, new SystemBurner(), ArbosAddresses.ArbosSystemAccount)
                .OpenSubStorage(ArbosSubspaceIDs.AddressTableSubspace);
            backingStorage.Set(1, Hash256.FromBytesWithPadding([0x5]));
        });

        BlockHeader parentHeader = chain.BlockTree.Head!.Header;

        // Build calldata for register(address)
        Address addressToRegister = TestItem.AddressA;
        byte[] registerSelector = Keccak.Compute("register(address)"u8).Bytes[..4].ToArray();
        byte[] calldata = new byte[36];
        registerSelector.CopyTo(calldata, 0);
        addressToRegister.Bytes.CopyTo(calldata.AsSpan(16));

        // Gas limit must be high enough for ArbAddressTable.Register to execute _backingStorage.Set(1, ...) — the
        // pure write whose trie traversal we want to verify — but low enough that the transaction
        // ultimately reverts
        Transaction registerTx;

        // intrinsic cost for transaction with 36 bytes of data
        long intrinsicGasCost = 21_432;
        // gas cost for precompile input data (32 calldata bytes excluding 4-bytes selector) + opening arbos as non-pure method
        long precompileInputAndOpeningArbosGasCost = 3 + (long)ArbosStorage.StorageReadCost;
        // gas cost for precompile execution (2 reads, 3 writes)
        ulong precompileExecGasCost = 2 * ArbosStorage.StorageReadCost + 3 * ArbosStorage.StorageWriteCost;
        long precompileOutputGasCost = 3;
        // gasLimit does not contain enough gas for paying for output data causing revert
        long gasLimit = intrinsicGasCost + precompileInputAndOpeningArbosGasCost + (long)precompileExecGasCost + precompileOutputGasCost - 1;

        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            registerTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(ArbosAddresses.ArbAddressTableAddress)
                .WithData(calldata)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(gasLimit) // Not enough gas, causing revert
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, registerTx));
        result.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Failure,
            "Register should revert due to insufficient gas");

        // Assert the address was not registered (state changes were reverted)
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ArbosState arbosState = ArbosState.OpenArbosState(chain.MainWorldState, new SystemBurner(), NullLogger.Instance);
            arbosState.AddressTable.AddressExists(addressToRegister).Should().BeFalse(
                "address should not be registered since the transaction reverted");
        }

        // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        // Record the block and generate the witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        // Compute the mapped Ethereum storage slot for AddressTable._backingStorage at offset 1.
        // This replicates ArbosStorage.MapAddress to determine the actual storage trie key.
        byte[] addressTableStorageKey = Keccak.Compute(ArbosSubspaceIDs.AddressTableSubspace).BytesToArray();
        UInt256 backingStorageSlot = ComputeMappedStorageSlot(addressTableStorageKey, 1);

        // Collect expected storage proof from the parent state
        AccountProofCollector collector = new(ArbosAddresses.ArbosSystemAccount, [backingStorageSlot]);
        chain.StateReader.RunTreeVisitor(collector, parentHeader);
        AccountProof accountProof = collector.BuildResult();

        byte[][] storageProofNodes = accountProof.StorageProofs!
            .SelectMany(sp => sp.Proof!)
            .ToArray();

        storageProofNodes.Should().NotBeEmpty(
            "pre-populated slot should have a non-empty storage proof in the parent state");

        foreach (byte[] proofNode in storageProofNodes)
        {
            recordResult.Preimages.ContainsKey(Keccak.Compute(proofNode)).Should().BeTrue(
                "witness should contain storage trie proof node for AddressTable._backingStorage " +
                "even when the transaction reverted");
        }

        AssertExpectedWitness(
            nameof(RecordBlockCreation_TransactionSetsSomeStateButReverts_StillRecordsStorageTrieNodes),
            recordResult);
    }

    /// <summary>
    /// Verifies that ProcessParentBlockHash (called from the StartBlock ArbitrumInternalTransaction
    /// for ArbOS >= 40) records the EIP-2935 history storage contract code and the ArbSys precompile
    /// code in the witness. The Arbitrum EIP-2935 contract STATICCALLs ArbSys (0x64) via
    /// arbBlockNumber() to get the current L2 block number, which triggers GetCode(ArbSys) during
    /// dynamic gas calculation for the STATICCALL instruction.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_ProcessParentBlockHash_RecordsHistoryStorageAndArbSysCodesInWitness()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee, arbosVersion: ArbosVersion.Forty)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
            .Build(chain => chain.WorldStateManager.FlushCache(CancellationToken.None));

        // EndOfBlock message produces a block with only the StartBlock internal tx (no user txs).
        // ProcessParentBlockHash executes the EIP-2935 contract via EVM, which STATICCALLs ArbSys
        // to get the current block number — recording both codes in the witness.
        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestEndOfBlock(l1BaseFee));
        result.Result.Should().Be(Result.Success);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        recordResult.Preimages.ContainsKey(Arbitrum.Arbos.Precompiles.HistoryStorageCodeHash).Should().BeTrue(
            "EIP-2935 history storage contract code should be recorded in witness " +
            "when ProcessParentBlockHash executes the contract via EVM");

        recordResult.Preimages.ContainsKey(Arbitrum.Arbos.Precompiles.InvalidCodeHash).Should().BeTrue(
            "ArbSys precompile code (0xfe) should be recorded in witness because the EIP-2935 " +
            "contract STATICCALLs ArbSys to get the current block number");

        AssertExpectedWitness(
            nameof(RecordBlockCreation_ProcessParentBlockHash_RecordsHistoryStorageAndArbSysCodesInWitness),
            recordResult);
    }

    /// <summary>
    /// Verifies that when a CALL with value transfer runs out of gas during its gas calculation
    /// phase inside InstructionCall, the target address is still recorded in the witness.
    ///
    /// In Nitro (go-ethereum), gasCall() is a pure accumulator: StateDB.Empty(address) always
    /// executes before the single OOG check in the interpreter. In Nethermind, gas is deducted
    /// inline, so the fix moves the IsDeadAccount (and AccountExists) state reads before any gas
    /// deduction that could abort early.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_CallWithValueOogDuringGasCalculation_StillRecordsTargetAddressInWitness()
    {
        UInt256 l1BaseFee = 92;

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: (ulong)l1BaseFee)
            .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender so it can pay transaction fees.
        ResultWrapper<MessageResult> depositResult = await chain.Digest(new TestEthDeposit(
            Keccak.Compute("deposit"), l1BaseFee, sender, sender, 100.Ether));
        depositResult.Result.Should().Be(Result.Success);

        // Create an account for the target address so that it creates some trie nodes unique to it,
        // so that we can assert the resulting witness contains those nodes that would not get
        // recorded without the fix.
        Address target = TestItem.AddressA;
        chain.AppendBlock(c =>
        {
            c.MainWorldState.CreateAccount(target, balance: 0, nonce: 1);
        });

        // Deploy a contract that executes a value-transferring CALL to target.
        // The sub-call gas (0) is irrelevant — the OOG happens in the CALLER's frame
        // during InstructionCall gas calculation, before the sub-call ever starts.
        //
        // Stack layout for CALL (top to bottom): gas, addr, value, argsOffset, argsLength, retOffset, retLength
        byte[] callerRuntimeCode = Prepare.EvmCode
            .PushData(0)         // retLength
            .PushData(0)         // retOffset
            .PushData(0)         // argsLength
            .PushData(0)         // argsOffset
            .PushData(1)         // value = 1 (non-zero so the IsDeadAccount path is reached, but actually the AccountExists path would also work)
            .PushData(target)    // target: existing, non-dead account
            .PushData(0)         // gas forwarded to sub-call
            .Op(Instruction.CALL)
            .Op(Instruction.STOP)
            .Done;

        byte[] callerInitCode = Prepare.EvmCode
            .ForInitOf(callerRuntimeCode)
            .Done;

        Transaction deployTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            deployTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(null)
                .WithData(callerInitCode)
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(500_000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> deployResult = await chain.Digest(new TestL2Transactions(l1BaseFee, sender, deployTx));
        deployResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success, "contract deployment should succeed");

        Address callerAddress = ContractAddress.From(sender, deployTx.Nonce);

        // Parent state: target's account trie proof is collected from here for the assertion below.
        BlockHeader parentHeader = chain.BlockTree.Head!.Header;

        // Call the contract with a gas limit calibrated to have the CALL instruction OOGs at the first gas deducing step
        // and make sure the target address is still recorded in the witness.
        //
        // Intrinsic (21_000) + 7 PUSH1 opcodes + not enough gas for the 1st gas cost in CALL instruction,
        // which will fail (ConsumeCallValueTransfer(9_000))
        const long callGasLimit = GasCostOf.Transaction + GasCostOf.VeryLow * 7 + (GasCostOf.CallValue - 1);

        Transaction callTx;
        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            callTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callerAddress)
                .WithData([])
                .WithMaxFeePerGas(10.GWei)
                .WithGasLimit(callGasLimit)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> callResult, DigestMessageParameters callParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, callTx));
        callResult.Result.Should().Be(Result.Success);
        // The user transaction should fail: CALL OOGs at ConsumeCallValueTransfer.
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Failure,
            "transaction should fail: CALL OOGs during gas calculation inside InstructionCall");

        // Flush trie nodes to make state roots accessible during witness generation.
        chain.WorldStateManager.FlushCache(CancellationToken.None);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        // Collect the account trie proof for target from the parent state.
        // If IsDeadAccount was called (as the fix ensures), the trie nodes along
        // the path to target are traversed and captured in the witness.
        //
        // Without the fix, IsDeadAccount is never reached (OOG fires first), so those
        // trie nodes are absent from the witness.
        AccountProofCollector collector = new(target);
        chain.StateReader.RunTreeVisitor(collector, parentHeader);
        AccountProof accountProof = collector.BuildResult();

        byte[][] accountProofNodes = accountProof.Proof!;

        accountProofNodes.Should().NotBeEmpty(
            "target should have a non-empty account trie proof in the parent state");

        foreach (byte[] proofNode in accountProofNodes)
        {
            recordResult.Preimages.ContainsKey(Keccak.Compute(proofNode)).Should().BeTrue(
                "witness should contain account trie proof node for target address even when " +
                "CALL OOGs at ConsumeCallValueTransfer — IsDeadAccount must be evaluated before any gas deduction");
        }

        AssertExpectedWitness(
            nameof(RecordBlockCreation_CallWithValueOogDuringGasCalculation_StillRecordsTargetAddressInWitness),
            recordResult);
    }

    [TestCaseSource(nameof(ExecutionWitnessForRecording1))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording2))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording3))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording3Cost))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording4Nested))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording5))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording6ContractAddress))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording7AccessList))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording8))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording8Payable))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording9))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording10))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording11))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording12))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording13))]
    [TestCaseSource(nameof(ExecutionWitnessForRecording14))]
    public async Task RecordBlockCreation_InPassedRecording_CapturesExactWitness(string recordingFile, ulong messageIndex, RecordResult expectedRecordResult)
    {
        // The chain is built (full recording replayed) once per recording file and reused across all
        // of that recording's block test cases. RecordBlockCreation and BuildBlockWitness only read
        // the canonical chain (they execute in their own processing-env scopes), so sharing is safe.
        // Without this, each of the ~480 cases rebuilt its whole chain from genesis — O(B²) block
        // productions (incl. Stylus activation) per recording. See DisposeSharedRecordingChains.
        (ArbitrumRpcTestBlockchain chain, FullChainSimulationRecordingFile recording) = GetReadOnlySharedRecordingChain(recordingFile);
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: ["wavm"]));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        using ArbitrumWitness witness = await chain.BuildBlockWitness(
            new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: ["wavm"]));
        AssertWitnessMatchesRecordResult(witness, recordResult);

        AssertRecordResultEquivalent(recordResult, expectedRecordResult);

        // To generate the expected witness files, use following lines instead
        // if (expectedRecordResult is null)
        //     ExpectedWitnessRecording.WriteBootstrapEntry(recordingFile, recordResult.Pos, recordResult.BlockHash, witness, recordResult.UserWasms);
    }

    /// <summary>
    /// Returns the chain for <paramref name="recordingFile"/>, building it once (full recording
    /// replayed) and caching it for reuse across all of that recording's test cases.
    /// <para>
    /// The returned instance is SHARED and must be treated as READ-ONLY: do not digest messages,
    /// append blocks, or otherwise mutate its canonical state — doing so would leak into every other
    /// test case for the same recording. Witness generation (RecordBlockCreation / BuildBlockWitness)
    /// and stateless processing are safe because they run in their own processing-env scopes.
    /// </para>
    /// </summary>
    private static (ArbitrumRpcTestBlockchain Chain, FullChainSimulationRecordingFile Recording) GetReadOnlySharedRecordingChain(string recordingFile)
    {
        lock (SharedRecordingChainsLock)
        {
            if (SharedRecordingChains.TryGetValue(recordingFile, out (ArbitrumRpcTestBlockchain, FullChainSimulationRecordingFile) entry))
                return entry;

            FullChainSimulationRecordingFile recording = new(recordingFile);
            ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
                .WithRecording(recording)
                .WithArbitrumConfig(cfg => cfg.ValidationEnabled = true)
                // Flush trie nodes to underlying nodeStorage to make state roots accessible for ReconstructedStateTrieStore during witness generation
                .Build(chain => chain.WorldStateManager.FlushCache(CancellationToken.None));

            entry = (chain, recording);
            SharedRecordingChains[recordingFile] = entry;

            return entry;
        }
    }

    private static IEnumerable<TestCaseData> ExecutionWitnessWithoutStylusSource()
    {
        // 18 blocks in the test where this test case source is used
        for (ulong blockNumber = 1; blockNumber <= 18; blockNumber++)
            yield return new TestCaseData(blockNumber);
    }

    private static IEnumerable<TestCaseData> ExecutionWitnessWithStylusSource()
    {
        // 47 blocks in the test where this test case source is used
        // Yield both the block number and the stylus contract addresses executed/called (activated ones should not be recorded) in that block
        for (ulong blockNumber = 1; blockNumber <= 47; blockNumber++)
        {
            if (blockNumber == 25)
                yield return new TestCaseData((ulong)25, new[] { new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 26)
                yield return new TestCaseData((ulong)26, new[] { new Address("0xe1080224b632a93951a7cfa33eeea9fd81558b5e") });
            else if (blockNumber == 32)
                yield return new TestCaseData((ulong)32, new[] { new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 34)
                yield return new TestCaseData((ulong)34, new[] { new Address("0x4af567288e68cad4aa93a272fe6139ca53859c70"), new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 38)
                yield return new TestCaseData((ulong)38, new[] { new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 39)
                yield return new TestCaseData((ulong)39, new[] { new Address("0x408da76e87511429485c32e4ad647dd14823fdc4"), new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 41)
                yield return new TestCaseData((ulong)41, new[] { new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 42)
                yield return new TestCaseData((ulong)42, new[] { new Address("0x408da76e87511429485c32e4ad647dd14823fdc4"), new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else if (blockNumber == 47)
                yield return new TestCaseData((ulong)47, new[] { new Address("0x841118047f42754332d0ad4db8a2893761dd7f5d"), new Address("0x1294b86822ff4976bfe136cb06cf43ec7fcf2574") });
            else
                yield return new TestCaseData(blockNumber, Array.Empty<Address>());
        }
    }

    /// <summary>
    /// Replicates ArbosStorage.MapAddress to compute the Ethereum storage slot
    /// from a subspace storage key and a logical offset.
    /// </summary>
    private static UInt256 ComputeMappedStorageSlot(byte[] storageKey, ulong offset)
    {
        byte[] keyBytes = new byte[32];
        new UInt256(offset).ToBigEndian(keyBytes);

        const int boundary = 31;
        byte[] keccakInput = new byte[storageKey.Length + boundary];
        storageKey.CopyTo(keccakInput, 0);
        Array.Copy(keyBytes, 0, keccakInput, storageKey.Length, boundary);

        byte[] hash = Keccak.Compute(keccakInput).BytesToArray();
        byte[] mappedKey = new byte[32];
        Array.Copy(hash, 0, mappedKey, 0, boundary);
        mappedKey[boundary] = keyBytes[boundary];

        return new UInt256(mappedKey, isBigEndian: true);
    }

    private static void AssertWitnessMatchesRecordResult(ArbitrumWitness witness, RecordResult recordResult)
    {
        RecordResult fromWitness = new(recordResult.Pos, recordResult.BlockHash, witness);

        fromWitness.Pos.Should().Be(recordResult.Pos);
        fromWitness.BlockHash.Should().Be(recordResult.BlockHash);

        AssertPreimagesEqual(fromWitness.Preimages, recordResult.Preimages);
        AssertUserWasmsEqual(fromWitness.UserWasms, recordResult.UserWasms);
    }

    private static T ThrowOnFailure<T>(ResultWrapper<T> result, ulong msgIndex)
    {
        if (result.Result != Result.Success)
            throw new InvalidOperationException($"Failed to execute RPC method, message index {msgIndex}, code {result.ErrorCode}: {result.Result.Error}");

        return result.Data;
    }

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording1()
        => EnumerateExpectedWitnessCases("./Recordings/1__arbos32_basefee92.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording2()
        => EnumerateExpectedWitnessCases("./Recordings/2__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording3()
        => EnumerateExpectedWitnessCases("./Recordings/3__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording3Cost()
        => EnumerateExpectedWitnessCases("./Recordings/3__stylus_cost.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording4Nested()
        => EnumerateExpectedWitnessCases("./Recordings/4__stylus_nested.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording5()
        => EnumerateExpectedWitnessCases("./Recordings/5__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording6ContractAddress()
        => EnumerateExpectedWitnessCases("./Recordings/6__stylus_contract_address.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording7AccessList()
        => EnumerateExpectedWitnessCases("./Recordings/7__stylus_accesslist.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording8()
        => EnumerateExpectedWitnessCases("./Recordings/8__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording8Payable()
        => EnumerateExpectedWitnessCases("./Recordings/8__stylus_payable.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording9()
        => EnumerateExpectedWitnessCases("./Recordings/9__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording10()
        => EnumerateExpectedWitnessCases("./Recordings/10__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording11()
        => EnumerateExpectedWitnessCases("./Recordings/11__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording12()
        => EnumerateExpectedWitnessCases("./Recordings/12__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording13()
        => EnumerateExpectedWitnessCases("./Recordings/13__stylus.jsonl");

    private static IEnumerable<TestCaseData> ExecutionWitnessForRecording14()
        => EnumerateExpectedWitnessCases("./Recordings/14__stylus.jsonl");

    /// <summary>
    /// Yields one <see cref="TestCaseData"/> per block in the given recording, deriving the
    /// block count from the expected witness JSONL. If the expected witness file does not exist,
    /// fails loudly so an accidentally-deleted expected file never silently gets unnoticed.
    /// Note that changing a test name will also fail as json files are named after them.
    /// The expected witness files got generated using the current version of the repo,
    /// which is considered to be correct.
    /// </summary>
    private static IEnumerable<TestCaseData> EnumerateExpectedWitnessCases(string recordingFilePath)
    {
        string expectedFilePath = ExpectedWitnessRecording.RecordingWitnessExpectedFilePath(recordingFilePath);

        Assert.IsTrue(File.Exists(expectedFilePath));

        foreach (ExpectedWitnessRecording entry in ExpectedWitnessRecording.ReadAll(expectedFilePath))
            yield return new TestCaseData(recordingFilePath, entry.Pos, entry.ToRecordResult());

        // If want to bootstrap some expected witness files, use following lines instead to feed the existing recordings
        // which will be used for generating witnesses that can then be written as expected files for future tests.
        //
        // FullChainSimulationRecordingFile recording = new(recordingFilePath);
        // foreach (DigestMessageParameters m in recording.GetDigestMessages())
        //     yield return new TestCaseData(recordingFilePath, m.Index, (RecordResult?)null);
    }

    /// <summary>
    /// For custom (on-the-fly chain) tests in this fixture: make sure the expected witness JSONL exists,
    /// compare against it; if it is missing, fail loud so an accidentally-deleted expected file
    /// never silently gets unnoticed.
    /// Note that changing a test name will also fail as json files are named after them.
    /// The expected witness files got generated using the current
    /// version of the repo, which is considered to be correct.
    /// </summary>
    private static void AssertExpectedWitness(string testName, RecordResult recordResult)
    {
        string path = ExpectedWitnessRecording.CustomTestWitnessExpectedFilePath(testName);

        Assert.IsTrue(File.Exists(path), "Either test was renamed or expected witness file got deleted");

        RecordResult expected = ExpectedWitnessRecording.ReadAll(path).Single().ToRecordResult();
        AssertRecordResultEquivalent(recordResult, expected);

        // To generate the expected witness files, use following lines instead
        //
        // ExpectedWitnessRecording.WriteCustomTestEntry(
        //     testName, recordResult.Pos, recordResult.BlockHash, witness, recordResult.UserWasms);
    }

    /// <summary>
    /// Compare two <see cref="RecordResult"/>s by member equivalence, but treat <c>UserWasms</c>
    /// specially: only the cross-platform <c>wavm</c> target is compared. Platform-specific
    /// machine code (<c>host</c>/<c>amd64</c>/<c>arm64</c>) is filtered out on both sides via
    /// <see cref="ExpectedWitnessRecording.KeepWavmOnly"/> so a bootstrap captured on one OS
    /// stays valid when the tests run on another.
    /// </summary>
    private static void AssertRecordResultEquivalent(RecordResult actual, RecordResult expected)
    {
        actual.Pos.Should().Be(expected.Pos);
        actual.BlockHash.Should().Be(expected.BlockHash);
        AssertPreimagesEqual(actual.Preimages, expected.Preimages);

        // Expected results in JSONL already have all UserWasms filtered to keep only the wavm target,
        // so apply the same filter to actual.UserWasms before comparison.
        Dictionary<Hash256, Dictionary<string, byte[]>> actualWavmOnly = ExpectedWitnessRecording.KeepWavmOnly(actual.UserWasms);
        AssertUserWasmsEqual(
            actualWavmOnly.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyDictionary<string, byte[]>)kvp.Value),
            expected.UserWasms);
    }

    // Direct structural comparison of the witness payloads. Replaces FluentAssertions' BeEquivalentTo,
    // whose reflection-based deep graph walk over these large dictionaries of byte[] dominated the
    // runtime of the recording-driven tests (~half of RecordBlockCreation_InPassedRecording_CapturesExactWitness:
    // measured 40s -> 21s when the asserts were disabled).
    private static void AssertPreimagesEqual(IReadOnlyDictionary<Hash256, byte[]?> actual, IReadOnlyDictionary<Hash256, byte[]?> expected)
    {
        foreach ((Hash256 hash, byte[]? expectedBytes) in expected)
        {
            if (!actual.TryGetValue(hash, out byte[]? actualBytes))
                Assert.Fail($"Preimage {hash} missing from actual");

            if (!((ReadOnlySpan<byte>)actualBytes).SequenceEqual(expectedBytes))
                Assert.Fail($"Preimage {hash} bytes differ from expected");
        }
    }

    private static void AssertUserWasmsEqual(IReadOnlyDictionary<Hash256, IReadOnlyDictionary<string, byte[]>> actual, IReadOnlyDictionary<Hash256, IReadOnlyDictionary<string, byte[]>> expected)
    {
        foreach ((Hash256 moduleHash, IReadOnlyDictionary<string, byte[]> expectedTargets) in expected)
        {
            if (!actual.TryGetValue(moduleHash, out IReadOnlyDictionary<string, byte[]>? actualTargets))
            {
                Assert.Fail($"UserWasms module {moduleHash} missing from actual");
                return; // unreachable; satisfies nullable flow analysis
            }

            foreach ((string target, byte[] expectedAsm) in expectedTargets)
            {
                if (!actualTargets.TryGetValue(target, out byte[]? actualAsm))
                    Assert.Fail($"UserWasms module {moduleHash} missing target '{target}'");

                if (!((ReadOnlySpan<byte>)actualAsm).SequenceEqual(expectedAsm))
                    Assert.Fail($"UserWasms asm bytes differ for module {moduleHash}, target '{target}'");
            }
        }
    }
}
