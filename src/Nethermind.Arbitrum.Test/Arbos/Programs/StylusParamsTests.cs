// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

[TestFixture]
public class StylusParamsTests
{
    private const byte InitialMaxFragmentCount = 4;
    private const uint InitialMaxWasmSize = 128 * 1024;
    private const uint ArbOS60MaxWasmSize = 256 * 1024;

    // Offsets within slot 0: base = 25 bytes; MaxWasmSize at 25..29 (uint, v40+); MaxFragmentCount at 29 (v60+).
    private const int MaxWasmSizeOffsetInSlot0 = 25;
    private const int MaxFragmentCountOffsetInSlot0 = 29;

    [Test]
    public void Save_AtArbosVersion60_RoundTripsMaxFragmentCount()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Sixty, storage);

        StylusParams loaded = new StylusPrograms(storage, ArbosVersion.Sixty).GetParams();

        loaded.MaxFragmentCount.Should().Be(InitialMaxFragmentCount);
    }

    [Test]
    public void Save_AtArbosVersionBelow60_DoesNotPersistMaxFragmentCountByte()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Fifty, storage);

        ArbosStorage paramsStorage = storage.OpenSubStorage([0]);
        ReadOnlySpan<byte> slot0 = paramsStorage.GetFree(new UInt256(0).ToValueHash()).Bytes;

        slot0[MaxFragmentCountOffsetInSlot0].Should().Be(0, "v50 Save must not write the MaxFragmentCount byte");
    }

    [Test]
    public void Save_AtArbosVersion60_WritesMaxWasmSizeAndMaxFragmentCountBytes()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Sixty, storage);

        ArbosStorage paramsStorage = storage.OpenSubStorage([0]);
        ReadOnlySpan<byte> slot0 = paramsStorage.GetFree(new UInt256(0).ToValueHash()).Bytes;

        uint persistedMaxWasmSize = BinaryPrimitives.ReadUInt32BigEndian(slot0.Slice(MaxWasmSizeOffsetInSlot0, sizeof(uint)));
        persistedMaxWasmSize.Should().Be(ArbOS60MaxWasmSize);
        slot0[MaxFragmentCountOffsetInSlot0].Should().Be(InitialMaxFragmentCount);
    }

    [Test]
    public void CreateFromStorage_AtArbosVersionBelow60_ReturnsZeroMaxFragmentCount()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Fifty, storage);

        StylusParams loaded = new StylusPrograms(storage, ArbosVersion.Fifty).GetParams();

        loaded.MaxFragmentCount.Should().Be(0, "Nitro parity: below v60 the byte is not stored and the deserializer returns 0");
    }

    [Test]
    public void Save_AtArbosVersion60_FitsInSameSlotCountAsBelow60()
    {
        using IDisposable scopeV50 = TestArbosStorage.Create(out TrackingWorldState stateV50, out ArbosStorage storageV50);
        StylusPrograms.Initialize(ArbosVersion.Fifty, storageV50);
        int slotsWrittenAtV50 = stateV50.SetRecords.Count;

        using IDisposable scopeV60 = TestArbosStorage.Create(out TrackingWorldState stateV60, out ArbosStorage storageV60);
        StylusPrograms.Initialize(ArbosVersion.Sixty, storageV60);

        stateV60.SetRecords.Count.Should().Be(slotsWrittenAtV50, "the new byte fits in the existing slot 0 slack (29 + 1 = 30 bytes)");
    }

    [Test]
    public void UpgradeToArbosVersion_From51To60_InitializesMaxFragmentCountToDefault()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.FiftyOne, storage);

        StylusPrograms programs = new(storage, ArbosVersion.FiftyOne);
        StylusParams paramsV51 = programs.GetParams();
        paramsV51.UpgradeToArbosVersion(ArbosVersion.Sixty);
        paramsV51.Save();

        programs.ArbosVersion = ArbosVersion.Sixty;
        StylusParams paramsV60 = programs.GetParams();

        paramsV60.MaxFragmentCount.Should().Be(InitialMaxFragmentCount);
    }

    [Test]
    public void UpgradeToArbosVersion_From51To60_RaisesMaxWasmSizeTo256KiB()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.FiftyOne, storage);

        StylusPrograms programs = new(storage, ArbosVersion.FiftyOne);
        StylusParams paramsV51 = programs.GetParams();
        paramsV51.MaxWasmSize.Should().Be(InitialMaxWasmSize, "v51 carries the 128 KiB ceiling");

        paramsV51.UpgradeToArbosVersion(ArbosVersion.Sixty);
        paramsV51.Save();

        programs.ArbosVersion = ArbosVersion.Sixty;
        StylusParams paramsV60 = programs.GetParams();

        paramsV60.MaxWasmSize.Should().Be(ArbOS60MaxWasmSize);
    }

    [Test]
    public void InitializeWithDefaults_AtArbosVersion60_SetsMaxWasmSizeTo256KiB()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Sixty, storage);

        StylusParams loaded = new StylusPrograms(storage, ArbosVersion.Sixty).GetParams();

        loaded.MaxWasmSize.Should().Be(ArbOS60MaxWasmSize);
    }

    [Test]
    public void InitializeWithDefaults_AtArbosVersionBelow60_KeepsInitialMaxWasmSize()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Fifty, storage);

        StylusParams loaded = new StylusPrograms(storage, ArbosVersion.Fifty).GetParams();

        loaded.MaxWasmSize.Should().Be(InitialMaxWasmSize, "below v60 the v40-era 128 KiB ceiling stays in effect");
    }

    [Test]
    public void UpgradeToArbosVersion_AlreadyAtSixty_Throws()
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Sixty, storage);

        StylusParams stylusParams = new StylusPrograms(storage, ArbosVersion.Sixty).GetParams();

        Action act = () => stylusParams.UpgradeToArbosVersion(ArbosVersion.Sixty);

        act.Should().Throw<InvalidOperationException>();
    }

    [TestCase((byte)0)]
    [TestCase((byte)1)]
    [TestCase((byte)5)]
    [TestCase((byte)255)]
    public void SetMaxFragmentCount_AfterSaveAndReload_RoundTripsValue(byte value)
    {
        using IDisposable scope = TestArbosStorage.Create(out _, out ArbosStorage storage);
        StylusPrograms.Initialize(ArbosVersion.Sixty, storage);

        StylusPrograms programs = new(storage, ArbosVersion.Sixty);
        StylusParams stylusParams = programs.GetParams();
        stylusParams.SetMaxFragmentCount(value);
        stylusParams.Save();

        StylusParams reloaded = new StylusPrograms(storage, ArbosVersion.Sixty).GetParams();
        reloaded.MaxFragmentCount.Should().Be(value);
    }

    [Test]
    public void UpgradeArbosVersion_From50To60_RunsStylusParamsMigration()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateScope = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Sixty, false, worldState, London.Instance);

        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.Sixty);
        StylusParams upgraded = context.ArbosState.Programs.GetParams();
        upgraded.MaxFragmentCount.Should().Be(InitialMaxFragmentCount);
        upgraded.MaxWasmSize.Should().Be(ArbOS60MaxWasmSize);
    }
}
