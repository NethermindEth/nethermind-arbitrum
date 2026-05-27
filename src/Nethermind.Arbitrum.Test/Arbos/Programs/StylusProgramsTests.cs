// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Arbitrum.Evm;
using Nethermind.Int256;
using Nethermind.Evm.State;
using System.Security.Cryptography;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Specs.Forks;

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

    [Test]
    public void Initialize_EmptyState_InitializesState()
    {
        using IDisposable disposable = TestArbosStorage.Create(out TrackingWorldState state, out ArbosStorage storage);
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
            maxWasmSize: 131072,
            maxFragmentCount: 0));

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
    public void ActivateProgram_NonExistentAccount_DoesNotReturnSelfDestructError()
    {
        using StackAccessTracker tracker = new();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        IWasmStore wasmStore = TestWasmStore.Create();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = DeployTestsContract.CreateTestPrograms(state);

        Address randomAddress = new(RandomNumberGenerator.GetBytes(Address.Size));
        ProgramActivationResult result = programs.ActivateProgram(randomAddress, Cancun.Instance, state, wasmStore, 0, MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.ProgramNotWasm);
    }

    [Test]
    public void ActivateProgram_SelfDestructedAccount_FailsWithSelfDestructError()
    {
        using StackAccessTracker tracker = new();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        IWasmStore wasmStore = TestWasmStore.Create();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = DeployTestsContract.CreateTestPrograms(state);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));
        tracker.ToBeDestroyed(address);

        ProgramActivationResult result = programs.ActivateProgram(address, Cancun.Instance, state, wasmStore, 0, MessageRunMode.MessageCommitMode, true, tracker);

        ProgramActivationResult expected = ProgramActivationResult.Failure(false, new(StylusOperationResultType.UnknownError, "Account self-destructed", []));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(expected.Error);
    }

    [Test]
    public void ActivateProgram_AddressHasNoCode_Fails()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = DeployTestsContract.CreateTestPrograms(state);
        Address contract = new(RandomNumberGenerator.GetBytes(Address.Size));

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);

        state.CreateAccountIfNotExists(contract, balance: 1, nonce: 0);
        state.Commit(specProvider.GenesisSpec);
        state.CommitTree(0);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, 0, MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.ProgramNotWasm);
        result.Error!.Value.Arguments.Should().BeEmpty();
    }

    [Test]
    public void ActivateProgram_ContractIsNotWasm_Fails()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository, prependStylusPrefix: false);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);

        ProgramActivationResult expected = ProgramActivationResult.Failure(false, new(StylusOperationResultType.InvalidByteCode, "Specified bytecode is not a Stylus program", []));
        result.Error.Should().BeEquivalentTo(expected.Error, o => o.ForStylusOperationError());
    }

    [Test]
    public void ActivateProgram_ContractIsNotCompressed_Fails()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository, compress: false);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);

        ProgramActivationResult expected = ProgramActivationResult.Failure(false, new(StylusOperationResultType.UnknownError, "Failed to decompress data", []));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(expected.Error, o => o.ForStylusOperationError());
    }

    [Test]
    public void ActivateProgram_NotEnoughGasForActivation_FailsAndConsumesAllGas()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);

        ProgramActivationResult expected = ProgramActivationResult.Failure(true, new(StylusOperationResultType.ActivationFailed, "out of gas", []));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(expected.Error, o => o.ForStylusOperationError());
    }

    [Test]
    public void ActivateProgram_ValidContractAndEnoughGas_Succeeds()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, InitBudget + ActivationBudget);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeTrue();
        result.ModuleHash.Should().Be(new ValueHash256("0xdfa80e333e8a1bf4e46c4bf27eadd9abfe33b873f4118db208c03acbb671e223"));
        result.DataFee.Should().Be(63539344027312);
    }

    [Test]
    public void CallProgram_ProgramIsNotActivated_Fails()
    {
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, InitBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);

        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using VmState<ArbitrumGasPolicy> vmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);
        TestStylusVmHost vmHost = new(blockContext, transactionContext, vmState, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> callResult = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        StylusOperationResult<byte[]> expected = StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ProgramNotActivated, "", []));
        callResult.IsSuccess.Should().BeFalse();
        callResult.Error.Should().BeEquivalentTo(expected.Error);
    }

    [Test]
    public void CallProgram_StylusVersionIsHigherThanPrograms_Fails()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);
        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        stylusParams.UpgradeToStylusVersion(2); // Set a higher Stylus version than the program supports
        stylusParams.Save();

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using VmState<ArbitrumGasPolicy> vmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);
        TestStylusVmHost vmHost = new(blockContext, transactionContext, vmState, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> callResult = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        StylusOperationResult<byte[]> expected = StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ProgramNeedsUpgrade, "", [1, 2]));
        callResult.IsSuccess.Should().BeFalse();
        callResult.Error.Should().BeEquivalentTo(expected.Error);
    }

    [Test]
    public void CallProgram_ProgramExpired_Fails()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);
        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        stylusParams.SetExpiryDays(0); // Set expiry to 0 days to simulate an expired program
        stylusParams.Save();

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using VmState<ArbitrumGasPolicy> vmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);
        TestStylusVmHost vmHost = new(blockContext, transactionContext, vmState, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> callResult = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        Program program = GetProgram(programs.ProgramsStorage, result.CodeHash, header.Timestamp);
        StylusOperationResult<byte[]> expected = StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ProgramExpired, "", [program.AgeSeconds]));
        callResult.IsSuccess.Should().BeFalse();
        callResult.Error.Should().BeEquivalentTo(expected.Error);
    }

    [Test]
    public void CallProgram_CorruptedCallData_Fails()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);
        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        byte[] callData = [0x1, 0x2, 0x3]; // Corrupted call data that does not match the expected format
        using VmState<ArbitrumGasPolicy> vmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);
        TestStylusVmHost vmHost = new(blockContext, transactionContext, vmState, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> callResult = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        StylusOperationResult<byte[]> expected = StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ExecutionRevert, nameof(UserOutcomeKind.Revert), []));
        callResult.IsSuccess.Should().BeFalse();
        callResult.Error.Should().BeEquivalentTo(expected.Error, o => o.ForStylusOperationError());
    }

    [Test]
    public void CallProgram_SetGetNumber_SuccessfullySetsAndGets()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, (InitBudget + ActivationBudget + CallBudget) * 10);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);
        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        // Set number to 9
        byte[] setNumberCallData1 = CounterContractCallData.GetSetNumberCalldata(9);
        using VmState<ArbitrumGasPolicy> setNumberVmState1 = CreateEvmState(state, caller, contract, codeInfo, setNumberCallData1);
        TestStylusVmHost vmHost = new(blockContext, transactionContext, setNumberVmState1, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> setNumberResult1 = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        setNumberResult1.IsSuccess.Should().BeTrue();

        // Read number back
        byte[] getNumberCallData2 = CounterContractCallData.GetNumberCalldata();
        using VmState<ArbitrumGasPolicy> getNumberVmState2 = CreateEvmState(state, caller, contract, codeInfo, getNumberCallData2);
        vmHost = new(blockContext, transactionContext, getNumberVmState2, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> getNumberResult2 = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        getNumberResult2.IsSuccess.Should().BeTrue();
        getNumberResult2.Value.Should().BeEquivalentTo(new UInt256(9).ToBigEndian());
    }

    [Test]
    public void CallProgram_IncrementNumber_SuccessfullyIncrements()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, (InitBudget + ActivationBudget + CallBudget) * 10);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(ArbosVersion.Forty);
        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        // Increment number from 0 to 1
        byte[] incrementCallData1 = CounterContractCallData.GetIncrementCalldata();
        using VmState<ArbitrumGasPolicy> incrementVmState1 = CreateEvmState(state, caller, contract, codeInfo, incrementCallData1);
        TestStylusVmHost vmHost = new(blockContext, transactionContext, incrementVmState1, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> incrementResult1 = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        incrementResult1.IsSuccess.Should().BeTrue();

        // Read number back
        byte[] getNumberCallData2 = CounterContractCallData.GetNumberCalldata();
        using VmState<ArbitrumGasPolicy> getNumberVmState2 = CreateEvmState(state, caller, contract, codeInfo, getNumberCallData2);
        vmHost = new(blockContext, transactionContext, getNumberVmState2, state, store, specProvider.GenesisSpec);

        StylusOperationResult<byte[]> getNumberResult2 = programs.CallProgram(vmHost,
            tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        getNumberResult2.IsSuccess.Should().BeTrue();
        getNumberResult2.Value.Should().BeEquivalentTo(new UInt256(1).ToBigEndian());
    }

    [TestCaseSource(nameof(NonActivatedQueryCases))]
    public void StylusQuery_WithNonActivatedProgram_ReturnsProgramNotActivatedFailure(NonActivatedQuery query)
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = DeployTestsContract.CreateTestPrograms(state);
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        (bool isSuccess, StylusPrograms.StylusOperationError? error) = query(programs, timestamp, stylusParams);

        StylusPrograms.StylusOperationError expected = new(StylusOperationResultType.ProgramNotActivated, "", []);
        isSuccess.Should().BeFalse();
        error.Should().Be(expected);
    }

    [Test]
    public void ProgramKeepalive_WithTooEarlyKeepalive_ReturnsFailure()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        Hash256 codeHashValue = new(codeHash.Bytes);

        StylusOperationResult<UInt256> keepAliveResult = programs.ProgramKeepalive(codeHashValue, header.Timestamp, stylusParams);

        Program program = GetProgram(programs.ProgramsStorage, codeHash, header.Timestamp);
        StylusOperationResult<UInt256> expected = StylusOperationResult<UInt256>.Failure(new(StylusOperationResultType.ProgramKeepaliveTooSoon, "", [program.AgeSeconds]));

        keepAliveResult.IsSuccess.Should().BeFalse();
        keepAliveResult.Error.Should().BeEquivalentTo(expected.Error);
    }

    [Test]
    public void CodeHashVersion_WithActivatedProgram_ReturnsVersion()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        Hash256 codeHashValue = new(codeHash.Bytes);

        StylusOperationResult<ushort> version = programs.CodeHashVersion(codeHashValue, header.Timestamp, stylusParams);

        version.IsSuccess.Should().BeTrue();
        version.Value.Should().Be(stylusParams.StylusVersion);
    }

    [Test]
    public void ProgramAsmSize_WithActivatedProgram_ReturnsSize()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        Hash256 codeHashValue = new(codeHash.Bytes);

        StylusOperationResult<uint> asmSize = programs.ProgramAsmSize(codeHashValue, header.Timestamp, stylusParams);

        asmSize.IsSuccess.Should().BeTrue();
        asmSize.Value.Should().BeGreaterThan(0); // Actual size depends on the compiled program
    }

    [Test]
    public void ProgramInitGas_WithActivatedProgram_ReturnsGasValues()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();

        StylusOperationResult<(ulong gas, ulong gasWhenCached)> initGas = programs.ProgramInitGas(codeHash, header.Timestamp, stylusParams);

        initGas.IsSuccess.Should().BeTrue();
        initGas.Value.gas.Should().BeGreaterThan(0); // Actual gas depends on program size
        initGas.Value.gasWhenCached.Should().BeGreaterThan(0); // Cached gas is lower
        initGas.Value.gas.Should().BeGreaterThan(initGas.Value.gasWhenCached); // Non-cached should cost more
    }

    [Test]
    public void ProgramMemoryFootprint_WithActivatedProgram_ReturnsFootprint()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();

        StylusOperationResult<ushort> footprint = programs.ProgramMemoryFootprint(codeHash, header.Timestamp, stylusParams);

        footprint.IsSuccess.Should().BeTrue();
        footprint.Value.Should().BeGreaterThan(0); // Actual footprint depends on program
    }

    [Test]
    public void ProgramTimeLeft_WithActivatedProgram_ReturnsTimeLeft()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, true, tracker);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();

        StylusOperationResult<ulong> timeLeft = programs.ProgramTimeLeft(codeHash, header.Timestamp, stylusParams);

        timeLeft.IsSuccess.Should().BeTrue();
        timeLeft.Value.Should().BeGreaterThan(0); // Time depends on the activation timestamp
    }

    [Test]
    public void GetParams_Always_ReturnsValidParams()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = DeployTestsContract.CreateTestPrograms(state);

        StylusParams stylusParams = programs.GetParams();

        stylusParams.Should().NotBeNull();
        stylusParams.StylusVersion.Should().Be(1); // Default Stylus version
        stylusParams.InkPrice.Should().Be(10000u); // InitialInkPrice
        stylusParams.MaxStackDepth.Should().Be(262144u); // InitialStackDepth
    }

    [Test]
    public void CallProgram_PreV60SameContractTwiceInBlock_ChargesFullInitGasOnBoth()
    {
        CounterCallScenario scenario = SetupCounterCallScenario(ArbosVersion.Fifty);

        long gasAfterCall1 = scenario.InvokeCounter();
        long gasAfterCall2 = scenario.InvokeCounter();

        gasAfterCall2.Should().Be(gasAfterCall1);
    }

    [Test]
    public void CallProgram_V60SameContractTwiceInBlock_SecondCallSavesFullMinusCachedInitGas()
    {
        CounterCallScenario scenario = SetupCounterCallScenario(ArbosVersion.Sixty);

        long gasAfterCall1 = scenario.InvokeCounter();
        long gasAfterCall2 = scenario.InvokeCounter();

        ulong expectedSavings = scenario.FullInitGas - scenario.CachedInitGas;
        ((ulong)(gasAfterCall2 - gasAfterCall1)).Should().Be(expectedSavings);
    }

    [Test]
    public void CallProgram_V60SameContractAcrossBlocks_BothCallsChargeFullInitGas()
    {
        CounterCallScenario scenario = SetupCounterCallScenario(ArbosVersion.Sixty);

        long gasAfterCall1 = scenario.InvokeCounter();
        scenario.WasmStore.GetRecentWasms().Clear();
        long gasAfterCall2 = scenario.InvokeCounter();

        gasAfterCall2.Should().Be(gasAfterCall1);
    }

    // Pre-fix, the call-site OR short-circuited Insert whenever program.Cached was true, so the
    // per-block LRU never observed the codeHash on call 1. Call 2 (Cached=false) then missed the
    // LRU and was charged full initGas, diverging from Nitro by initGas-cachedGas.
    [Test]
    public void CallProgram_V60CachedFlagFlippedBetweenCalls_BothCallsEndWithIdenticalGas()
    {
        CounterCallScenario scenario = SetupCounterCallScenario(ArbosVersion.Sixty);

        scenario.SetCached(true);
        long gasAfterCall1 = scenario.InvokeCounter();
        scenario.SetCached(false);
        long gasAfterCall2 = scenario.InvokeCounter();

        gasAfterCall2.Should().Be(gasAfterCall1);
    }

    // Pins the v60 gate: pre-v60 Insert is suppressed under the same flip scenario, so call 2
    // (Cached=false) misses the LRU and pays full initGas instead of cachedGas.
    [Test]
    public void CallProgram_PreV60CachedFlagFlippedBetweenCalls_SecondCallPaysFullInitGas()
    {
        CounterCallScenario scenario = SetupCounterCallScenario(ArbosVersion.Fifty);

        scenario.SetCached(true);
        long gasAfterCall1 = scenario.InvokeCounter();
        scenario.SetCached(false);
        long gasAfterCall2 = scenario.InvokeCounter();

        ulong expectedExtraCost = scenario.FullInitGas - scenario.CachedInitGas;
        ((ulong)(gasAfterCall1 - gasAfterCall2)).Should().Be(expectedExtraCost);
    }

    public delegate (bool IsSuccess, StylusPrograms.StylusOperationError? Error) NonActivatedQuery(
        StylusPrograms programs, ulong timestamp, StylusParams stylusParams);

    private static IEnumerable<TestCaseData> NonActivatedQueryCases()
    {
        yield return new TestCaseData(new NonActivatedQuery((programs, timestamp, stylusParams) =>
        {
            StylusOperationResult<ushort> result = programs.CodeHashVersion(Hash256.Zero, timestamp, stylusParams);
            return (result.IsSuccess, result.Error);
        })).SetName("CodeHashVersion");

        yield return new TestCaseData(new NonActivatedQuery((programs, timestamp, stylusParams) =>
        {
            StylusOperationResult<uint> result = programs.ProgramAsmSize(Hash256.Zero, timestamp, stylusParams);
            result.Value.Should().Be(0);
            return (result.IsSuccess, result.Error);
        })).SetName("ProgramAsmSize");

        yield return new TestCaseData(new NonActivatedQuery((programs, timestamp, stylusParams) =>
        {
            StylusOperationResult<(ulong gas, ulong gasWhenCached)> result = programs.ProgramInitGas(Hash256.Zero, timestamp, stylusParams);
            return (result.IsSuccess, result.Error);
        })).SetName("ProgramInitGas");

        yield return new TestCaseData(new NonActivatedQuery((programs, timestamp, stylusParams) =>
        {
            StylusOperationResult<ushort> result = programs.ProgramMemoryFootprint(Hash256.Zero, timestamp, stylusParams);
            return (result.IsSuccess, result.Error);
        })).SetName("ProgramMemoryFootprint");

        yield return new TestCaseData(new NonActivatedQuery((programs, timestamp, stylusParams) =>
        {
            StylusOperationResult<ulong> result = programs.ProgramTimeLeft(Hash256.Zero, timestamp, stylusParams);
            return (result.IsSuccess, result.Error);
        })).SetName("ProgramTimeLeft");

        yield return new TestCaseData(new NonActivatedQuery((programs, timestamp, stylusParams) =>
        {
            StylusOperationResult<UInt256> result = programs.ProgramKeepalive(Hash256.Zero, timestamp, stylusParams);
            return (result.IsSuccess, result.Error);
        })).SetName("ProgramKeepalive");
    }

    private static VmState<ArbitrumGasPolicy> CreateEvmState(IWorldState state, Address caller, Address contract, CodeInfo codeInfo, byte[] callData, long gasAvailable = 1_000_000_000)
    {
        ExecutionEnvironment env = ExecutionEnvironment.Rent(codeInfo, caller, caller, contract, 0, 0, 0, callData);
        return VmState<ArbitrumGasPolicy>.RentTopLevel(ArbitrumGasPolicy.FromLong(gasAvailable), ExecutionType.TRANSACTION, env, new StackAccessTracker(), state.TakeSnapshot());
    }

    private static (BlockExecutionContext, TxExecutionContext) CreateExecutionContext(ICodeInfoRepository repository, Address caller, BlockHeader header, ulong arbosVersion = ArbosVersion.Forty)
    {
        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(arbosVersion);
        BlockExecutionContext blockContext = new(header, specProvider.GenesisSpec);
        TxExecutionContext transactionContext = new(caller, repository, [], 0);

        return (blockContext, transactionContext);
    }

    private static CounterCallScenario SetupCounterCallScenario(ulong arbosVersion)
    {
        const ulong activationBudget = (InitBudget + ActivationBudget + CallBudget) * 10;

        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(state, activationBudget, arbosVersion);
        (Address caller, Address contract, BlockHeader header) = DeployTestsContract.DeployCounterContract(state, repository);

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(arbosVersion);
        CodeInfo codeInfo = repository.GetCachedCodeInfo(contract, specProvider.GenesisSpec, out _);

        ProgramActivationResult activation = programs.ActivateProgram(
            contract, Cancun.Instance, state, store, header.Timestamp, MessageRunMode.MessageCommitMode, debugMode: true, tracker);
        activation.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        StylusOperationResult<(ulong gas, ulong gasWhenCached)> initGasResult = programs.ProgramInitGas(activation.CodeHash, header.Timestamp, stylusParams);
        initGasResult.IsSuccess.Should().BeTrue();

        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header, arbosVersion);
        byte[] callData = CounterContractCallData.GetNumberCalldata();

        long Invoke()
        {
            using VmState<ArbitrumGasPolicy> vmState = CreateEvmState(state, caller, contract, codeInfo, callData);
            TestStylusVmHost vmHost = new(blockContext, transactionContext, vmState, state, store, specProvider.GenesisSpec);

            StylusOperationResult<byte[]> result = programs.CallProgram(vmHost,
                tracingInfo: null, specProvider.ChainId, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);
            result.IsSuccess.Should().BeTrue();

            return ArbitrumGasPolicy.GetRemainingGas(in vmHost.VmState.Gas);
        }

        return new CounterCallScenario(
            programs.ProgramsStorage,
            store,
            activation.CodeHash,
            initGasResult.Value.gas,
            initGasResult.Value.gasWhenCached,
            Invoke);
    }

    private static Program GetProgram(ArbosStorage programStorage, in ValueHash256 codeHash, ulong timestamp)
    {
        ValueHash256 dataAsHash = programStorage.Get(codeHash);
        ReadOnlySpan<byte> data = dataAsHash.Bytes;

        ushort version = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort initCost = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort cachedCost = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort footprint = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        uint activatedAtHours = ArbitrumBinaryReader.ReadUIntFrom24OrFail(ref data);
        uint asmEstimateKb = ArbitrumBinaryReader.ReadUIntFrom24OrFail(ref data);
        bool cached = ArbitrumBinaryReader.ReadBoolOrFail(ref data);

        ulong ageSeconds = ArbitrumTime.HoursToAgeSeconds(timestamp, activatedAtHours);

        return new Program(version, initCost, cachedCost, footprint, activatedAtHours, asmEstimateKb, ageSeconds, cached);
    }

    private record Program(
        ushort Version,
        ushort InitCost,
        ushort CachedCost,
        ushort Footprint,
        uint ActivatedAtHours,
        uint AsmEstimateKb,
        ulong AgeSeconds,
        bool Cached);

    // Test-only scenario for activate-then-call-counter-twice flows. SetCached bypasses
    // ArbWasm.SetProgramCached's cache-managers gate by writing the Cached bit directly
    // into the program-storage slot StylusPrograms.CallProgram reads at the OR site.
    private sealed class CounterCallScenario(
        ArbosStorage programsStorage,
        IWasmStore wasmStore,
        ValueHash256 codeHash,
        ulong fullInitGas,
        ulong cachedInitGas,
        Func<long> invoke)
    {
        public IWasmStore WasmStore { get; } = wasmStore;
        public ulong FullInitGas { get; } = fullInitGas;
        public ulong CachedInitGas { get; } = cachedInitGas;

        public long InvokeCounter() => invoke();

        public void SetCached(bool cached)
        {
            ValueHash256 programData = programsStorage.Get(codeHash);
            ArbitrumBinaryWriter.WriteBool(programData.BytesAsSpan[14..], cached);
            programsStorage.Set(codeHash, programData);
        }
    }
}
