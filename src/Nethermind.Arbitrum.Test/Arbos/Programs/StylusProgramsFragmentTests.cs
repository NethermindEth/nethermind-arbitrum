// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

public class StylusProgramsFragmentTests
{
    // Mirrors StylusProgramsTests.InitBudget; v60 init writes the same slots (MaxFragmentCount fits in slot 0).
    private const ulong InitBudget = 110900;
    private const ulong ActivationBudget = 706_000;
    // Headroom for fragmented activation under Nitro-equivalent semantics: each fragment incurs a single
    // cold-cost charge of ColdAccountAccess + ToWordSize(actualSize)*CopyGas at most ~5k per fragment.
    private const ulong FragmentActivationOverhead = 12_000;

    [Test]
    public void ActivateProgram_RootContractWithTwoFragmentsAtV60_Succeeds()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + ActivationBudget + FragmentActivationOverhead, ArbosVersion.Sixty);
        (_, Address rootAddress, _, BlockHeader header) = DeployTestsContract.DeployFragmentedCounterContract(state, repository, fragmentCount: 2);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeTrue();
        // Module hash differs from the classic v40 activation because the activator hashes ArbOS version
        // into the module — the test only asserts that activation produced a valid (non-zero) module hash,
        // which is the user-visible signal that reassembly + activation succeeded.
        result.ModuleHash.Should().NotBe(default(ValueHash256));
        result.DataFee.Should().BeGreaterThan(0);
    }

    [Test]
    public void ActivateProgram_FragmentDirectlyAtV60_FailsWithFragmentedActivationDirect()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + ActivationBudget + FragmentActivationOverhead, ArbosVersion.Sixty);
        (_, _, IReadOnlyList<Address> fragmentAddresses, BlockHeader header) = DeployTestsContract.DeployFragmentedCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(fragmentAddresses[0], Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.FragmentedActivationDirect);
        result.Error.Value.Message.Should().StartWith("Fragmented stylus programs cannot be activated directly; activate the root program instead");
    }

    [Test]
    public void ActivateProgram_RootContractAtV50_FailsAsInvalidByteCode()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + ActivationBudget, ArbosVersion.Fifty);
        (_, Address rootAddress, _, BlockHeader header) = DeployTestsContract.DeployFragmentedCounterContract(state, repository);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
    }

    [Test]
    public void ActivateProgram_RootWithZeroFragments_FailsWithFragmentCountZero()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + ActivationBudget + FragmentActivationOverhead, ArbosVersion.Sixty);

        (Address rootAddress, BlockHeader header) = DeployTestsContract.DeployCustomRoot(
            state, repository, dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary,
            declaredDecompressedLength: 100, fragmentAddresses: []);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.FragmentCountZero);
        result.Error.Value.Message.Should().StartWith("Invalid wasm: fragment count cannot be zero");
    }

    [Test]
    public void ActivateProgram_RootWithFragmentsAboveLimit_FailsWithFragmentCountExceedsLimit()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + ActivationBudget + FragmentActivationOverhead, ArbosVersion.Sixty);
        StylusParams stylusParams = programs.GetParams();

        Address[] fragments = new Address[stylusParams.MaxFragmentCount + 1];
        for (int i = 0; i < fragments.Length; i++)
            fragments[i] = new Address(RandomNumberGenerator.GetBytes(Address.Size));

        (Address rootAddress, BlockHeader header) = DeployTestsContract.DeployCustomRoot(
            state, repository, dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary,
            declaredDecompressedLength: 100, fragmentAddresses: fragments);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.FragmentCountExceedsLimit);
        result.Error.Value.Message.Should().StartWith($"Invalid wasm: fragment count exceeds limit of {stylusParams.MaxFragmentCount}");
    }

    [Test]
    public void ActivateProgram_RootWithDecompressedLengthExceedingMaxWasmSize_FailsWithDecompressedLengthExceedsMax()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + ActivationBudget + FragmentActivationOverhead, ArbosVersion.Sixty);

        StylusParams stylusParams = programs.GetParams();
        Address[] fragments = [new(RandomNumberGenerator.GetBytes(Address.Size))];
        (Address rootAddress, BlockHeader header) = DeployTestsContract.DeployCustomRoot(
            state, repository, dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary,
            declaredDecompressedLength: stylusParams.MaxWasmSize + 1, fragmentAddresses: fragments);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.DecompressedLengthExceedsMax);
        result.Error.Value.Message.Should().StartWith($"Invalid wasm: decompressedLength {stylusParams.MaxWasmSize + 1} is greater then MaxWasmSize {stylusParams.MaxWasmSize}");
    }

    [Test]
    public void FragmentReadCharger_BurnerWithJustEnoughForOneFragment_FailsBeforeReadingSecondFragment()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        // Two-phase Nitro semantics: EnsureCanReadFragment pre-flights against the worst-case (MaxCodeSize)
        // read and calls burner.BurnOut() if remaining gas is insufficient. Budget is enough to charge the
        // first fragment's actual cost but not enough for the second fragment's worst-case pre-flight.
        ulong tightBudget = InitBudget + 5000;
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, tightBudget, ArbosVersion.Sixty);
        (_, Address rootAddress, IReadOnlyList<Address> fragmentAddresses, BlockHeader header) = DeployTestsContract.DeployFragmentedCounterContract(state, repository, fragmentCount: 2);

        // TestBurner.BurnOut throws InvalidOperationException; activation propagates it, confirming the
        // affordability gate fired before the second fragment was read from state.
        Action act = () => programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        act.Should().Throw<InvalidOperationException>();
        // Determinism guarantee from acceptance criteria: no per-fragment partial state mutation on OOG.
        // The second fragment must remain cold — its address must not have been added to the access list.
        tracker.IsCold(fragmentAddresses[1]).Should().BeTrue();
    }

    [Test]
    public void ActivateProgram_RootWithBadDictionaryAndTwoFragments_ChargesFragmentGasBeforeFailing()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + FragmentActivationOverhead, ArbosVersion.Sixty);

        // Two real fragment contracts (so EXTCODECOPY succeeds and StripStylusFragmentPrefix passes),
        // referenced from a custom root carrying dict byte 0xFF — outside the supported {0, 1} set.
        Address[] fragments = DeployValidFragments(state, repository, count: 2);
        (Address rootAddress, BlockHeader header) = DeployTestsContract.DeployCustomRoot(
            state, repository, dictionary: 0xFF, declaredDecompressedLength: 100, fragmentAddresses: fragments);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.UnsupportedCompressionDict);
        result.Error.Value.Message.Should().StartWith("Unsupported dictionary type: 255");
        // Gas-burn invariant: both fragment addresses must have been warmed by ChargeForReadingFragment
        // before the post-loop dict validation fires — demonstrates the loop ran to completion.
        tracker.IsCold(fragments[0]).Should().BeFalse();
        tracker.IsCold(fragments[1]).Should().BeFalse();
    }

    [Test]
    public void ActivateProgram_RootWithBadDictionaryByte171_ErrorMessageFormatIsDecimalByte()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, InitBudget + FragmentActivationOverhead, ArbosVersion.Sixty);

        Address[] fragments = DeployValidFragments(state, repository, count: 1);
        (Address rootAddress, BlockHeader header) = DeployTestsContract.DeployCustomRoot(
            state, repository, dictionary: 0xAB, declaredDecompressedLength: 100, fragmentAddresses: fragments);

        ProgramActivationResult result = programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.UnsupportedCompressionDict);
        // Decimal byte rendering: 0xAB → 171; not "AB", not "0xab", not the enum name.
        result.Error.Value.Message.Should().StartWith("Unsupported dictionary type: 171");
    }

    [Test]
    public void ActivateProgram_RootWithBadDictionaryAndInsufficientGas_FailsOnGasBeforeDictValidation()
    {
        using StackAccessTracker tracker = new();
        IWasmStore store = TestWasmStore.Create();
        TrackingWorldState state = TrackingWorldState.CreateNewInMemory();
        state.BeginScope(IWorldState.PreGenesis);
        // Same tight budget as the BurnOut test above: enough for one fragment's actual cost, not enough
        // for the second pre-flight against MaxCodeSize. If dict validation had run early (in Parse),
        // we'd see a bad-dict failure instead of the OOG throw — that's the ordering this test proves.
        ulong tightBudget = InitBudget + 5000;
        (StylusPrograms programs, ArbitrumCodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(
            state, tightBudget, ArbosVersion.Sixty);

        Address[] fragments = DeployValidFragments(state, repository, count: 2);
        (Address rootAddress, BlockHeader header) = DeployTestsContract.DeployCustomRoot(
            state, repository, dictionary: 0xFF, declaredDecompressedLength: 100, fragmentAddresses: fragments);

        Action act = () => programs.ActivateProgram(rootAddress, Cancun.Instance, state, store, header.Timestamp,
            MessageRunMode.MessageCommitMode, true, tracker);

        act.Should().Throw<InvalidOperationException>();
        // Pins the failure site to the second-fragment EnsureCanReadFragment pre-flight (mirrors the
        // sibling FragmentReadCharger_BurnerWithJustEnoughForOneFragment_FailsBeforeReadingSecondFragment).
        // Without this, the throw could come from any unrelated InvalidOperationException source and the
        // "before dict validation" claim in the test name would be unverifiable.
        tracker.IsCold(fragments[1]).Should().BeTrue();
    }

    [Test]
    public void FragmentReadGasCost_ColdAccess_TagsStorageAccess()
    {
        MultiGas cost = StylusPrograms.FragmentReadGasCost(warm: false, codeSize: 0);

        cost.Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdAccountAccess);
        cost.Get(ResourceKind.Computation).Should().Be(0);
        cost.SingleGas().Should().Be(GasCostOf.ColdAccountAccess);
    }

    [Test]
    public void FragmentReadGasCost_WarmAccess_TagsComputation()
    {
        MultiGas cost = StylusPrograms.FragmentReadGasCost(warm: true, codeSize: 0);

        cost.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        cost.Get(ResourceKind.StorageAccess).Should().Be(0);
        cost.SingleGas().Should().Be(GasCostOf.WarmStateRead);
    }

    [Test]
    public void FragmentReadGasCost_ColdAccessWith1024ByteCode_TagsCopyCostAsStorageAccess()
    {
        // 1024 bytes = 32 words; copy cost = 32 * GasCostOf.Memory.
        const long codeSize = 1024;
        ulong expectedCopy = 32 * (ulong)GasCostOf.Memory;

        MultiGas cost = StylusPrograms.FragmentReadGasCost(warm: false, codeSize);

        // Cold-access cost + copy cost both live in StorageAccess; Computation stays at zero.
        cost.Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdAccountAccess + expectedCopy);
        cost.Get(ResourceKind.Computation).Should().Be(0);
        cost.SingleGas().Should().Be(GasCostOf.ColdAccountAccess + expectedCopy);
    }

    [Test]
    public void FragmentReadGasCost_WarmAccessWith1024ByteCode_SplitsAcrossDimensions()
    {
        // Warm-access cost lands in Computation; copy cost lands in StorageAccess.
        const long codeSize = 1024;
        ulong expectedCopy = 32 * (ulong)GasCostOf.Memory;

        MultiGas cost = StylusPrograms.FragmentReadGasCost(warm: true, codeSize);

        cost.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        cost.Get(ResourceKind.StorageAccess).Should().Be(expectedCopy);
        cost.SingleGas().Should().Be(GasCostOf.WarmStateRead + expectedCopy);
    }

    [Test]
    public void FragmentReadGasCost_NonWordAlignedCodeSize_RoundsUpWords()
    {
        // 33 bytes ceils to 2 words.
        MultiGas cost = StylusPrograms.FragmentReadGasCost(warm: false, codeSize: 33);

        ulong expectedCopy = 2 * (ulong)GasCostOf.Memory;
        cost.Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdAccountAccess + expectedCopy);
    }

    [Test]
    public void IBurner_BurnMultiGas_CollapsesToSingleGasViaDefaultImplementation()
    {
        IBurner burner = new TestArbosStorage.TestBurner(availableGas: 1000);
        MultiGas cost = new();
        cost.Increment(ResourceKind.StorageAccess, 600);
        cost.Increment(ResourceKind.Computation, 100);

        burner.Burn(in cost);

        // SingleGas == 700 (sum across dimensions); TestBurner's GasLeft drops by exactly that.
        burner.GasLeft.Should().Be(300);
    }

    // Builds N fragments whose code is the bare fragment prefix + a 1-byte payload. The bad-dict tests
    // never decompress (they bail at ResolveStylusCompressionDict), so the payload bytes don't need to
    // form a valid Brotli stream — we only need EXTCODECOPY to succeed and StripStylusFragmentPrefix
    // to recognize the prefix, so the fragment-read loop runs to completion.
    private static Address[] DeployValidFragments(TrackingWorldState state, ArbitrumCodeInfoRepository repository, int count)
    {
        Address[] addresses = new Address[count];
        for (int i = 0; i < count; i++)
        {
            byte[] fragmentCode = StylusCode.NewStylusFragmentPrefix([(byte)i]);
            Address addr = new(RandomNumberGenerator.GetBytes(Address.Size));
            state.CreateAccountIfNotExists(addr, balance: 0, nonce: 0);
            repository.InsertCode(fragmentCode, addr, Cancun.Instance);
            addresses[i] = addr;
        }
        state.Commit(Cancun.Instance);
        state.CommitTree(0);
        return addresses;
    }
}
