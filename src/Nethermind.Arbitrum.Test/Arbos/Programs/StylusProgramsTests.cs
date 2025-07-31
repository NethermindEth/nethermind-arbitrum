// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

public class StylusProgramsTests
{
    // 20000 + 5000 + 20000 + 20000 + 20000 + 5000 - StylusPrograms init (20k - write value, 5k - write zero)
    // 100 - StylusParams warmed-up read   |
    // 800 - Program read                  | activation part
    private const ulong InitBudget = 110900;

    // Activation consumes ~706k gas
    private const ulong ActivationBudget = 706_000;
    private const long CallBudget = 1_000_000;

    private const ulong DefaultArbosVersion = ArbosVersion.Forty;
    private static readonly IReleaseSpec ReleaseSpec = FullChainSimulationReleaseSpec.Instance;
    private static readonly ISpecProvider SpecProvider = FullChainSimulationSpecProvider.Instance;

    [Test]
    public void Initialize_EmptyState_InitializesState()
    {
        (ArbosStorage storage, TrackingWorldState state) = TestArbosStorage.Create();
        StylusPrograms.Initialize(DefaultArbosVersion, storage);
        StylusPrograms programs = new(storage, DefaultArbosVersion);

        // Default Stylus params, 1 slot
        programs.GetParams().Should().BeEquivalentTo(new StylusParams(
            DefaultArbosVersion,
            storage,
            stylusVersion: 1,
            inkPrice: 10000,
            maxStackDepth: 262144,
            freePages: 2,
            pageGas: 1000,
            pageRamp: 620674314,
            pageLimit: 128,
            minInitGas: 72,
            minCachedInitGas: 11,
            initCostScalar: 50,
            cachedCostScalar: 50,
            expiryDays: 365,
            keepaliveDays: 31,
            blockCacheSize: 32,
            maxWasmSize: 131072));

        // DataPricer, 5 slots
        ArbosStorage dataPricerStorage = storage.OpenSubStorage([3]);
        dataPricerStorage.GetULong(0).Should().Be(0); // Initial demand
        dataPricerStorage.GetULong(1).Should().Be(34865); // Initial bytes per second
        dataPricerStorage.GetULong(2).Should().Be(ArbitrumTime.StartTime); // Initial last update time
        dataPricerStorage.GetULong(3).Should().Be(82928201); // Initial min price
        dataPricerStorage.GetULong(4).Should().Be(21360419); // Initial inertia

        // CacheManagers, 1 slot
        ArbosStorage addressSetStorage = storage.OpenSubStorage([4]);
        addressSetStorage.GetULong(0).Should().Be(0); // Initial size

        // Total set of slots changed
        state.SetRecords.Should().HaveCount(7);
    }

    [Test]
    public void ActivateProgram_NoProgram_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, _) = CreateTestPrograms();

        Address randomAddress = new(RandomNumberGenerator.GetBytes(20));
        ProgramActivationResult result = programs.ActivateProgram(randomAddress, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Account self-destructed");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_AddressHasNoCode_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, _) = CreateTestPrograms();
        Address contract = new(RandomNumberGenerator.GetBytes(20));

        state.CreateAccountIfNotExists(contract, balance: 1, nonce: 0);
        state.Commit(ReleaseSpec);
        state.CommitTree(0);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith(ArbWasmErrors.ProgramNotWasm);
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_ContractIsNotWasm_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms();
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository, prependStylusPrefix: false);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Specified bytecode is not a Stylus program");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_ContractIsNotCompressed_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms();
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository, compress: false);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Failed to decompress data");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_NotEnoughGasForActivation_FailsAndConsumesAllGas()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms();
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("out of gas");
        result.TakeAllGas.Should().BeTrue();
    }

    [Test]
    public void ActivateProgram_ValidContractAndEnoughGas_Succeeds()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms(availableGas: InitBudget + ActivationBudget);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.IsSuccess.Should().BeTrue();
        result.ModuleHash.Should().Be(new ValueHash256("0x304b0b0001000000120000007eadd9abfe33b873f4118db208c03acbb671e223"));
        result.DataFee.Should().Be(UInt256.Parse("4975692060000"));
    }

    [Test]
    public void CallProgram_ProgramIsNotActivated_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms(availableGas: InitBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, new StylusEvmApi(),
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: true, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(ArbWasmErrors.ProgramNotActivated);
    }

    [Test]
    public void CallProgram_StylusVersionIsHigherThanPrograms_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms(availableGas: InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        stylusParams.UpgradeToStylusVersion(2); // Set a higher Stylus version than the program supports
        stylusParams.Save();

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, new StylusEvmApi(),
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: true, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(ArbWasmErrors.ProgramNeedsUpgrade(programVersion: 1, stylusVersion: 2));
    }

    [Test]
    public void CallProgram_ProgramExpired_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms(availableGas: InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        stylusParams.SetExpiryDays(0); // Set expiry to 0 days to simulate an expired program
        stylusParams.Save();

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, new StylusEvmApi(),
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: true, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(nameof(ArbWasmErrors.ProgramExpired));
    }

    [Test]
    public void CallProgram_CorruptedCallData_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state, ICodeInfoRepository repository) = CreateTestPrograms(availableGas: InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        byte[] callData = [0x1, 0x2, 0x3]; // Corrupted call data that does not match the expected format
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, new StylusEvmApi(),
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: true, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(nameof(UserOutcomeKind.Revert));
    }

    private static (Address caller, Address contract, BlockHeader block) DeployCounterContract(IWorldState state, ICodeInfoRepository repository,
        bool compress = true, bool prependStylusPrefix = true)
    {
        Address caller = new(RandomNumberGenerator.GetBytes(20));
        Address contract = new(RandomNumberGenerator.GetBytes(20));

        state.CreateAccountIfNotExists(caller, balance: 1.Ether(), nonce: 0);
        state.CreateAccountIfNotExists(contract, balance: 0, nonce: 0);

        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusResult<byte[]> wasmResult = StylusNative.WatToWasm(wat);
        if (!wasmResult.IsSuccess)
            throw new InvalidOperationException("Failed to convert WAT to WASM: " + wasmResult.Error);

        byte[] code = wasmResult.Value;
        if (compress) // Stylus contracts are compressed
            code = BrotliCompression.Compress(code, 1).ToArray();

        if (prependStylusPrefix) // Valid Stylus programs must have the Stylus prefix
            code = [..StylusCode.NewStylusPrefix(dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary), ..code];

        ValueHash256 codeHash = Keccak.Compute(code);
        repository.InsertCode(state, code, contract, ReleaseSpec);

        state.Commit(ReleaseSpec);
        state.CommitTree(0);

        BlockHeader header = new BlockHeaderBuilder()
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .TestObject;

        return (caller, contract, header);
    }

    private EvmState CreateEvmState(IWorldState state, Address caller, Address contract, ICodeInfo codeInfo, byte[] callData)
    {
        ExecutionEnvironment env = new(codeInfo, caller, caller, contract, 0, 0, 0, callData);
        return EvmState.RentTopLevel(gasAvailable: 0, ExecutionType.TRANSACTION, in env, new StackAccessTracker(), state.TakeSnapshot());
    }

    private (BlockExecutionContext, TxExecutionContext ) CreateExecutionContext(ICodeInfoRepository repository, Address caller, BlockHeader header)
    {
        BlockExecutionContext blockContext = new(header, ReleaseSpec);
        TxExecutionContext transactionContext = new(caller, repository, [], 0);

        return (blockContext, transactionContext);
    }

    private static (StylusPrograms programs, TrackingWorldState state, ArbitrumCodeInfoRepository repository) CreateTestPrograms(ulong availableGas = InitBudget)
    {
        StylusTargets.PopulateStylusTargetCache(new StylusTargetConfig());

        ArbitrumCodeInfoRepository repository = new(new CodeInfoRepository());
        TestBurner burner = new(availableGas);
        (ArbosStorage storage, TrackingWorldState state) = TestArbosStorage.Create(burner: burner);

        StylusPrograms.Initialize(DefaultArbosVersion, storage);
        StylusPrograms programs = new(storage, DefaultArbosVersion);

        return (programs, state, repository);
    }
}
