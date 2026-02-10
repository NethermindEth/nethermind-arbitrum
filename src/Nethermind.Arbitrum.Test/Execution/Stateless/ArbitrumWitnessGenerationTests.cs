using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Validators;
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
using Nethermind.Evm.Tracing;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumWitnessGenerationTests
{
    [TestCaseSource(nameof(ExecutionWitnessWithoutStylusSource))]
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
            new(witness, chain.SpecHelper, specProvider, Always.Valid, chain.StylusTargetConfig, chain.ArbosVersionProvider, chain.LogManager, chain.ArbitrumConfig);

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
            new(witness, chain.SpecHelper, specProvider, Always.Valid, chain.StylusTargetConfig, chain.ArbosVersionProvider, chain.LogManager, chain.ArbitrumConfig);

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
        FullChainSimulationRecordingFile recording = new("./Recordings/5__stylus.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .Build();

        string[] wasmTargets = chain.StylusTargetConfig.GetWasmTargets().ToArray();
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message, WasmTargets: wasmTargets));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        ArbitrumWitness witness = recordResult.Witness;

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

        // Build actual dictionary from witness by hashing ASM byte arrays
        Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>> actual = witness.UserWasms?
            .ToDictionary(
                kvp => kvp.Key.ToHash256(),
                kvp => (IReadOnlyDictionary<string, byte[]>)kvp.Value.ToDictionary(
                    asm => asm.Key,
                    asm => asm.Value))
            ?? [];

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
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account with ETH deposit
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether());
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
                .WithMaxFeePerGas(10.GWei())
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
                .WithMaxFeePerGas(10.GWei())
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

        // Call RecordBlockCreation to generate the witness
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        // Verify the witness codes
        ArbitrumWitness witness = recordResult.Witness;
        byte[][] witnessCodes = witness.Witness.Codes;

        // Arbitrum precompile bytecode is 0xfe (INVALID opcode)
        byte[] arbitrumPrecompileCode = Arbitrum.Arbos.Precompiles.InvalidCode;

        // The witness should contain the Arbitrum precompile's code (0xfe)
        bool arbitrumPrecompileCodeInWitness = witnessCodes.Any(code => code.SequenceEqual(arbitrumPrecompileCode));
        arbitrumPrecompileCodeInWitness.Should().BeTrue(
            "Arbitrum precompile bytecode (0xfe) should be recorded in witness when calling ArbSys");

        // Verify that no empty code is recorded (Ethereum precompiles have no stored bytecode)
        bool emptyCodeInWitness = witnessCodes.Any(code => code.Length == 0);
        emptyCodeInWitness.Should().BeFalse("Ethereum precompiles empty bytecode should not be recorded in witness");

        // The witness should have exactly 1 code: Arbitrum precompile (0xfe)
        // No code from Ethereum precompile since it has empty bytecode
        witnessCodes.Length.Should().Be(1, "Witness should contain only Arbitrum precompile code (0xfe)");
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
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        // Fund the sender account with ETH deposit
        TestEthDeposit deposit = new(
            Keccak.Compute("deposit"),
            l1BaseFee,
            sender,
            sender,
            100.Ether());
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
                .WithMaxFeePerGas(10.GWei())
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
                .WithMaxFeePerGas(10.GWei())
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

        // Step 2: Call the contract again (would use cached value from the block that just got built
        // if cache were persisted into witness-generating env)
        // Making sure witness-generating VM has its own empty cache and must access storage, recording the trie nodes.
        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(call2Params.Index, call2Params.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, call2Params.Index);

        ArbitrumWitness witness = recordResult.Witness;

        // The storage slot accessed is: 1 + l1BlockNumber % 256 in the Blockhashes substorage (see GetL1BlockHash)
        // Too difficult to predict exact trie node hash here, so, using the hardcoded value (found during debugging)
        witness.Witness.State.Any(node => Keccak.Compute(node) == new Hash256("0x30cfd2590e997a3c3bee0c89572aec183bae0976e06334354832b85514d0d37a")).Should().BeTrue(
            "Witness state should contain leaf trie node for BLOCKHASH storage access");
        // Similarly, checking for an intermediate node capture when accessing the storage slot
        witness.Witness.State.Any(node => Keccak.Compute(node) == new Hash256("0xad9a2d73baabd92487dd1840cd076a06a3eded05e8cbdebb930ddad669e51880")).Should().BeTrue(
            "Witness state should contain intermediate trie node for BLOCKHASH storage access");
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
                .WithMaxFeePerGas(10.GWei())
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

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(callParams.Index, callParams.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, callParams.Index);

        ArbitrumWitness witness = recordResult.Witness;

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
            DepositValue: 10.Ether(),
            RetryValue: 1.Ether(),
            GasFee: 1.GWei(),
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
        // A bit of hack but without this, setting up the test is almost impossible / kinda random
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

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        ArbitrumWitness witness = recordResult.Witness;

        // Assert some trie node on the path to the calldata storage slot has been captured (not captured elsewhere during block recording ofc, otherwise test is useless)
        // Here I assert the leaf node hash (found during debugging).
        witness.Witness.State.Any(node => Keccak.Compute(node) == new Hash256("0xb2020a6fea12f86ace9de5bed3312ca953a2f8ae0730062fa9df4fc833c99782")).Should().BeTrue(
            "Witness state should contain trie node for retryable empty calldata storage slot");
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
            DepositValue: 10.Ether(),
            RetryValue: 1.Ether(),
            GasFee: 1.GWei(),
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
                .WithMaxFeePerGas(1.GWei())
                .WithGasLimit(21_000)
                .WithValue(1)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestL2Transactions(l1BaseFee, sender, transferTx));
        result.Result.Should().Be(Result.Success);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        ArbitrumWitness witness = recordResult.Witness;

        // Assert the leaf trie node for TimeoutWindowsLeft (offset 6) has been captured.
        // Trie node hash determined during debugging — without the fix, this node would NOT be in the witness.
        witness.Witness.State.Any(node => Keccak.Compute(node) == new Hash256("0xb9b0e8140da26e36ad74be6f20e6dc5073cda81b1ed9c3c8d63388f69640f24e")).Should().BeTrue(
            "Witness state should contain trie node for retryable TimeoutWindowsLeft storage slot");
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
    /// internal tx's CanAddTransaction path, not by any user tx execution or gas charging.
    /// </summary>
    [Test]
    public async Task RecordBlockCreation_NonUserTransaction_RecordsBrotliCompressionLevelInWitness()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock()
            .Build();

        UInt256 l1BaseFee = 92;

        // EndOfBlock message produces a block with only the StartBlock internal tx (no user txs).
        // BrotliCompressionLevel can only be captured by the internal tx's CanAddTransaction path.
        (ResultWrapper<MessageResult> result, DigestMessageParameters digestParams) =
            await chain.DigestAndGetParams(new TestEndOfBlock(l1BaseFee));
        result.Result.Should().Be(Result.Success);

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(
            new RecordBlockCreationParameters(digestParams.Index, digestParams.Message, WasmTargets: []));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestParams.Index);

        ArbitrumWitness witness = recordResult.Witness;

        // Assert the leaf trie node for BrotliCompressionLevel (offset 7) has been captured.
        // Trie node hash determined during debugging — without the fix, this node would NOT be
        // in the witness because non-user txs returned early from CanAddTransaction.
        witness.Witness.State.Any(node => Keccak.Compute(node) == new Hash256("0x9bcf99179b305f1d54185508b47cc61fb0f8b804dd449a9b60ed068af7b1d62f")).Should().BeTrue(
            "Witness state should contain trie node for BrotliCompressionLevel storage slot (offset 7)");
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

    private static T ThrowOnFailure<T>(ResultWrapper<T> result, ulong msgIndex)
    {
        if (result.Result != Result.Success)
            throw new InvalidOperationException($"Failed to execute RPC method, message index {msgIndex}, code {result.ErrorCode}: {result.Result.Error}");

        return result.Data;
    }
}
