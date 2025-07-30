// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

public class StylusProgramsTests
{
    // 20000 + 5000 + 20000 + 20000 + 20000 + 5000 - StylusPrograms init (20k - write value, 5k - write zero)
    // 100 - StylusParams warmed-up read
    // 800 - Program read
    private const ulong StylusProgramsInitBudget = 110900;

    private const ulong DefaultArbosVersion = ArbosVersion.Forty;

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
        (StylusPrograms programs, TrackingWorldState state) = CreateTestPrograms();

        Address randomAddress = new(RandomNumberGenerator.GetBytes(20));
        ProgramActivationResult result = programs.ActivateProgram(randomAddress, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Account self-destructed");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_AddressHasNoCode_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state) = CreateTestPrograms();
        Address address = new(RandomNumberGenerator.GetBytes(20));

        state.CreateAccountIfNotExists(address, balance: 1, nonce: 0);
        state.Commit(FullChainSimulationReleaseSpec.Instance);
        state.CommitTree(0);

        ProgramActivationResult result = programs.ActivateProgram(address, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith(ArbWasmErrors.ProgramNotWasm);
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_ContractIsNotWasm_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state) = CreateTestPrograms();
        Address address = DeployCounterContract(state, prependStylusPrefix: false);

        ProgramActivationResult result = programs.ActivateProgram(address, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Specified bytecode is not a Stylus program");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_ContractIsNotCompressed_Fails()
    {
        (StylusPrograms programs, TrackingWorldState state) = CreateTestPrograms();
        Address address = DeployCounterContract(state, compress: false);

        ProgramActivationResult result = programs.ActivateProgram(address, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("Failed to decompress data");
        result.TakeAllGas.Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_NotEnoughGasForActivation_FailsAndConsumesAllGas()
    {
        (StylusPrograms programs, TrackingWorldState state) = CreateTestPrograms();
        Address address = DeployCounterContract(state);

        ProgramActivationResult result = programs.ActivateProgram(address, state, 0, MessageRunMode.MessageCommitMode, true);

        result.Error.Should().StartWith("out of gas");
        result.TakeAllGas.Should().BeTrue();
    }

    [Test]
    public void ActivateProgram_ValidContractAndEnoughGas_Succeeds()
    {
        (StylusPrograms programs, TrackingWorldState state) = CreateTestPrograms(availableGas: StylusProgramsInitBudget + 1_000_000);
        Address address = DeployCounterContract(state);

        // Activation consumes ~700k gas
        ProgramActivationResult result = programs.ActivateProgram(address, state, 0, MessageRunMode.MessageCommitMode, true);

        result.IsSuccess.Should().BeTrue();
        result.ModuleHash.Should().Be(new ValueHash256("0x304b0b0001000000120000007eadd9abfe33b873f4118db208c03acbb671e223"));
        result.DataFee.Should().Be(UInt256.Parse("4975692060000"));
    }

    private static Address DeployCounterContract(IWorldState state, bool compress = true, bool prependStylusPrefix = true)
    {
        Address address = new(RandomNumberGenerator.GetBytes(20));

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
        state.CreateAccountIfNotExists(address, balance: 0, nonce: 0);
        state.InsertCode(address, in codeHash, code, FullChainSimulationReleaseSpec.Instance);
        state.Commit(FullChainSimulationReleaseSpec.Instance);
        state.CommitTree(0);

        return address;
    }

    private static (StylusPrograms programs, TrackingWorldState state) CreateTestPrograms(ulong availableGas = StylusProgramsInitBudget)
    {
        StylusTargets.PopulateStylusTargetCache(new StylusTargetConfig());

        TestBurner burner = new(availableGas);
        (ArbosStorage storage, TrackingWorldState state) = TestArbosStorage.Create(burner: burner);
        StylusPrograms.Initialize(DefaultArbosVersion, storage);
        StylusPrograms programs = new(storage, DefaultArbosVersion);

        return (programs, state);
    }
}
