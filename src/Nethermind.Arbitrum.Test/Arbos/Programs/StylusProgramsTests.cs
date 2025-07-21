// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.State;
using Nethermind.Int256;
using System.Security.Cryptography;

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
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);

        Address randomAddress = new(RandomNumberGenerator.GetBytes(Address.Size));
        ProgramActivationResult result = programs.ActivateProgram(randomAddress, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Account self-destructed");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_AddressHasNoCode_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);
        Address contract = new(RandomNumberGenerator.GetBytes(Address.Size));

        state.CreateAccountIfNotExists(contract, balance: 1, nonce: 0);
        state.Commit(ReleaseSpec);
        state.CommitTree(0);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith(ArbWasm.Errors.ProgramNotWasm);
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_ContractIsNotWasm_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository, prependStylusPrefix: false);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Specified bytecode is not a Stylus program");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_ContractIsNotCompressed_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository, compress: false);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Failed to decompress data");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_NotEnoughGasForActivation_FailsAndConsumesAllGas()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("out of gas");
        result.TakeAllGas.Should().BeTrue();
    }

    [Test]
    public void ActivateProgram_ValidContractAndEnoughGas_Succeeds()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, InitBudget + ActivationBudget);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);

        result.IsSuccess.Should().BeTrue();
        result.ModuleHash.Should().Be(new ValueHash256("0x304b0b0001000000120000007eadd9abfe33b873f4118db208c03acbb671e223"));
        result.DataFee.Should().Be(UInt256.Parse("497569206"));
    }

    [Test]
    public void CallProgram_ProgramIsNotActivated_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, InitBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using IStylusEvmApi evmApi = new StylusEvmApi(state, contract);
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(ArbWasm.Errors.ProgramNotActivated);
    }

    [Test]
    public void CallProgram_StylusVersionIsHigherThanPrograms_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        stylusParams.UpgradeToStylusVersion(2); // Set a higher Stylus version than the program supports
        stylusParams.Save();

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using IStylusEvmApi evmApi = new StylusEvmApi(state, contract);
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(ArbWasm.Errors.ProgramNeedsUpgrade(programVersion: 1, stylusVersion: 2));
    }

    [Test]
    public void CallProgram_ProgramExpired_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        stylusParams.SetExpiryDays(0); // Set expiry to 0 days to simulate an expired program
        stylusParams.Save();

        byte[] callData = CounterContractCallData.GetNumberCalldata();
        using IStylusEvmApi evmApi = new StylusEvmApi(state, contract);
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith("ProgramExpired");
    }

    [Test]
    public void CallProgram_CorruptedCallData_Fails()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, InitBudget + ActivationBudget + CallBudget);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        byte[] callData = [0x1, 0x2, 0x3]; // Corrupted call data that does not match the expected format
        using IStylusEvmApi evmApi = new StylusEvmApi(state, contract);
        using EvmState evmState = CreateEvmState(state, caller, contract, codeInfo, callData);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        OperationResult<byte[]> callResult = programs.CallProgram(evmState, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        callResult.Error.Should().StartWith(nameof(UserOutcomeKind.Revert));
    }

    [Test]
    public void CallProgram_SetGetNumber_SuccessfullySetsAndGets()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, (InitBudget + ActivationBudget + CallBudget) * 10);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        using IStylusEvmApi evmApi = new StylusEvmApi(state, contract);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        // Set number to 9
        byte[] setNumberCallData1 = CounterContractCallData.GetSetNumberCalldata(9);
        using EvmState setNumberEvmState1 = CreateEvmState(state, caller, contract, codeInfo, setNumberCallData1);
        OperationResult<byte[]> setNumberResult1 = programs.CallProgram(setNumberEvmState1, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        setNumberResult1.IsSuccess.Should().BeTrue();

        // Read number back
        byte[] getNumberCallData2 = CounterContractCallData.GetNumberCalldata();
        using EvmState getNumberEvmState2 = CreateEvmState(state, caller, contract, codeInfo, getNumberCallData2);
        OperationResult<byte[]> getNumberResult2 = programs.CallProgram(getNumberEvmState2, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        getNumberResult2.IsSuccess.Should().BeTrue();
        getNumberResult2.Value.Should().BeEquivalentTo(new UInt256(9).ToBigEndian());
    }

    [Test]
    public void CallProgram_IncrementNumber_SuccessfullyIncrements()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, (InitBudget + ActivationBudget + CallBudget) * 10);
        (Address caller, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ICodeInfo codeInfo = repository.GetCachedCodeInfo(state, contract, ReleaseSpec, out _);

        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        using IStylusEvmApi evmApi = new StylusEvmApi(state, contract);
        (BlockExecutionContext blockContext, TxExecutionContext transactionContext) = CreateExecutionContext(repository, caller, header);

        // Increment number from 0 to 1
        byte[] incrementCallData1 = CounterContractCallData.GetIncrementCalldata();
        using EvmState incrementEvmState1 = CreateEvmState(state, caller, contract, codeInfo, incrementCallData1);
        OperationResult<byte[]> incrementResult1 = programs.CallProgram(incrementEvmState1, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        incrementResult1.IsSuccess.Should().BeTrue();

        // Read number back
        byte[] getNumberCallData2 = CounterContractCallData.GetNumberCalldata();
        using EvmState getNumberEvmState2 = CreateEvmState(state, caller, contract, codeInfo, getNumberCallData2);
        OperationResult<byte[]> getNumberResult2 = programs.CallProgram(getNumberEvmState2, in blockContext, in transactionContext, state, evmApi,
            tracingInfo: null, SpecProvider, l1BlockNumber: 0, reentrant: false, MessageRunMode.MessageCommitMode, debugMode: true);

        getNumberResult2.IsSuccess.Should().BeTrue();
        getNumberResult2.Value.Should().BeEquivalentTo(new UInt256(1).ToBigEndian());
    }

    private static (Address caller, Address contract, BlockHeader block) DeployCounterContract(IWorldState state, ICodeInfoRepository repository,
        bool compress = true, bool prependStylusPrefix = true)
    {
        Address caller = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address contract = new(RandomNumberGenerator.GetBytes(Address.Size));

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
            code = [.. StylusCode.NewStylusPrefix(dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary), .. code];

        ValueHash256 codeHash = Keccak.Compute(code);
        repository.InsertCode(state, code, contract, ReleaseSpec);

        state.Commit(ReleaseSpec);
        state.CommitTree(0);

        BlockHeader header = new BlockHeaderBuilder()
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .TestObject;

        return (caller, contract, header);
    }

    private EvmState CreateEvmState(IWorldState state, Address caller, Address contract, ICodeInfo codeInfo, byte[] callData, long gasAvailable = 1_000_000_000)
    {
        ExecutionEnvironment env = new(codeInfo, caller, caller, contract, 0, 0, 0, callData);
        return EvmState.RentTopLevel(gasAvailable, ExecutionType.TRANSACTION, in env, new StackAccessTracker(), state.TakeSnapshot());
    }

    private (BlockExecutionContext, TxExecutionContext) CreateExecutionContext(ICodeInfoRepository repository, Address caller, BlockHeader header)
    {
        BlockExecutionContext blockContext = new(header, ReleaseSpec);
        TxExecutionContext transactionContext = new(caller, repository, [], 0);

        return (blockContext, transactionContext);
    }

    [Test]
    public void ProgramKeepalive_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);
        Hash256 nonActivatedCodeHash = Hash256.Zero;
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        Action act = () => programs.ProgramKeepalive(nonActivatedCodeHash, timestamp, stylusParams);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramKeepalive_WithTooEarlyKeepalive_ThrowsInvalidOperation()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        Hash256 codeHashValue = new(codeHash.Bytes);

        Action act = () => programs.ProgramKeepalive(codeHashValue, header.Timestamp, stylusParams);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ProgramKeepaliveTooSoon*");
    }

    [Test]
    public void CodeHashVersion_WithNonActivatedProgram_ReturnsZero()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);
        Hash256 nonActivatedCodeHash = Hash256.Zero;
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        ushort version = programs.CodeHashVersion(nonActivatedCodeHash, timestamp, stylusParams);

        version.Should().Be(0);
    }

    [Test]
    public void CodeHashVersion_WithActivatedProgram_ReturnsVersion()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        Hash256 codeHashValue = new(codeHash.Bytes);

        ushort version = programs.CodeHashVersion(codeHashValue, header.Timestamp, stylusParams);

        version.Should().Be(stylusParams.StylusVersion);
    }

    [Test]
    public void ProgramAsmSize_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository _) = CreateTestPrograms(state);
        Hash256 nonActivatedCodeHash = Hash256.Zero;
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        Action act = () => programs.ProgramAsmSize(nonActivatedCodeHash, timestamp, stylusParams);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramAsmSize_WithActivatedProgram_ReturnsSize()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();
        Hash256 codeHashValue = new(codeHash.Bytes);

        uint asmSize = programs.ProgramAsmSize(codeHashValue, header.Timestamp, stylusParams);

        asmSize.Should().BeGreaterThan(0); // Actual size depends on the compiled program
    }

    [Test]
    public void ProgramInitGas_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);
        ValueHash256 nonActivatedCodeHash = Hash256.Zero;
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        Action act = () => programs.ProgramInitGas(nonActivatedCodeHash, timestamp, stylusParams);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramInitGas_WithActivatedProgram_ReturnsGasValues()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();

        (ulong gas, ulong gasWhenCached) = programs.ProgramInitGas(codeHash, header.Timestamp, stylusParams);

        gas.Should().BeGreaterThan(0); // Actual gas depends on program size
        gasWhenCached.Should().BeGreaterThan(0); // Cached gas is lower
        gas.Should().BeGreaterThan(gasWhenCached); // Non-cached should cost more
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);
        ValueHash256 nonActivatedCodeHash = Hash256.Zero;
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        Action act = () => programs.ProgramMemoryFootprint(nonActivatedCodeHash, timestamp, stylusParams);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramMemoryFootprint_WithActivatedProgram_ReturnsFootprint()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();

        ushort footprint = programs.ProgramMemoryFootprint(codeHash, header.Timestamp, stylusParams);

        footprint.Should().BeGreaterThan(0); // Actual footprint depends on program
    }

    [Test]
    public void ProgramTimeLeft_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);
        ValueHash256 nonActivatedCodeHash = Hash256.Zero;
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        StylusParams stylusParams = programs.GetParams();

        Action act = () => programs.ProgramTimeLeft(nonActivatedCodeHash, timestamp, stylusParams);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramTimeLeft_WithActivatedProgram_ReturnsTimeLeft()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ICodeInfoRepository repository) = CreateTestPrograms(state, availableGas: InitBudget + ActivationBudget * 10);
        (_, Address contract, BlockHeader header) = DeployCounterContract(state, repository);
        ValueHash256 codeHash = state.GetCodeHash(contract);

        // Activate the program first
        ProgramActivationResult result = programs.ActivateProgram(contract, state, header.Timestamp, MessageRunMode.MessageCommitMode, true);
        result.IsSuccess.Should().BeTrue();

        StylusParams stylusParams = programs.GetParams();

        ulong timeLeft = programs.ProgramTimeLeft(codeHash, header.Timestamp, stylusParams);

        timeLeft.Should().BeGreaterThan(0); // Time depends on the activation timestamp
    }

    [Test]
    public void GetParams_Always_ReturnsValidParams()
    {
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, _) = CreateTestPrograms(state);

        StylusParams stylusParams = programs.GetParams();

        stylusParams.Should().NotBeNull();
        stylusParams.StylusVersion.Should().Be(1); // Default Stylus version
        stylusParams.InkPrice.Should().Be(10000u); // InitialInkPrice
        stylusParams.MaxStackDepth.Should().Be(262144u); // InitialStackDepth
    }

    private static (StylusPrograms programs, ArbitrumCodeInfoRepository repository) CreateTestPrograms(TrackingWorldState state, ulong availableGas = InitBudget)
    {
        new ArbitrumInitializeStylusNative(new StylusTargetConfig())
            .Execute(CancellationToken.None).GetAwaiter().GetResult();

        ArbitrumCodeInfoRepository repository = new(new EthereumCodeInfoRepository());
        TestArbosStorage.TestBurner burner = new(availableGas, null);
        var storage = TestArbosStorage.Create(state, burner: burner);

        StylusPrograms.Initialize(DefaultArbosVersion, storage);
        StylusPrograms programs = new(storage, DefaultArbosVersion);

        return (programs, repository);
    }
}
