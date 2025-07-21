// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core.Crypto;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Test.Stylus;

public class WasmStoreTests
{
    [Test]
    public void GetWasmTargets_Always_ReturnsConfiguredTargets()
    {
        (IStylusTargetConfig config, _, IWasmStore store) = CreateStore();

        store.GetWasmTargets().Should().BeEquivalentTo(config.GetWasmTargets());
    }

    [TestCase(0u)]
    [TestCase(1u)]
    public void GetWasmCacheTag_Always_ReturnsConfiguredCacheTag(uint cacheTag)
    {
        (_, _, IWasmStore store) = CreateStore(cacheTag);

        store.GetWasmCacheTag().Should().Be(cacheTag);
    }

    [Test]
    public void GetStylusPages_EmptyStore_ReturnsZeroPages()
    {
        (_, _, IWasmStore store) = CreateStore();

        (ushort openNow, ushort openEver) = store.GetStylusPages();

        openNow.Should().Be(0);
        openEver.Should().Be(0);
    }

    [Test]
    public void AddStylusPages_ZeroPages_AddsPagesAndResetsOpened()
    {
        (_, _, IWasmStore store) = CreateStore();

        ushort pagesOpened = 5;

        CloseOpenedPages openedPages = store.AddStylusPages(pagesOpened);
        (ushort openNowBefore, ushort openEverBefore) = store.GetStylusPages();
        openedPages.Dispose();

        (ushort openNowAfter, ushort openEverAfter) = store.GetStylusPages();

        openNowBefore.Should().Be(pagesOpened);
        openEverBefore.Should().Be(pagesOpened);

        openNowAfter.Should().Be(0); // Opened pages should be reset
        openEverAfter.Should().Be(pagesOpened); // Ever opened pages should remain the same
    }

    [Test]
    public void AddStylusPages_OpenMultipleTimes_IncrementsOpenEver()
    {
        (_, _, IWasmStore store) = CreateStore();

        ushort pagesOpened1 = 3;
        CloseOpenedPages openedPages1 = store.AddStylusPages(pagesOpened1);
        openedPages1.Dispose();

        ushort pagesOpened2 = 4;
        CloseOpenedPages openedPages2 = store.AddStylusPages(pagesOpened2);
        openedPages2.Dispose();

        (ushort openNow, ushort openEver) = store.GetStylusPages();

        openNow.Should().Be(0); // Opened pages should be reset
        openEver.Should().Be(System.Math.Max(pagesOpened1, pagesOpened2)); // Ever opened has max opened ever
    }

    [Test]
    public void ActivateWasm_NotCommitted_AvailableForQueryButNotPersisted()
    {
        (_, IWasmDb db, IWasmStore store) = CreateStore();
        ValueHash256 moduleHash = new(RandomNumberGenerator.GetBytes(32));

        store.ActivateWasm(in moduleHash, new Dictionary<string, byte[]>
        {
            [StylusTargets.WavmTargetName] = RandomNumberGenerator.GetBytes(32),
        });

        store.TryGetActivatedAsm(StylusTargets.WavmTargetName, in moduleHash, out _).Should().BeTrue();
        db.TryGetActivatedAsm(StylusTargets.WavmTargetName, in moduleHash, out _).Should().BeFalse();
    }

    [Test]
    public void ActivateWasm_Committed_PersistsActivation()
    {
        (_, IWasmDb db, IWasmStore store) = CreateStore();
        ValueHash256 moduleHash = new(RandomNumberGenerator.GetBytes(32));

        store.ActivateWasm(in moduleHash, new Dictionary<string, byte[]>
        {
            [StylusTargets.WavmTargetName] = RandomNumberGenerator.GetBytes(32),
        });

        store.Commit();

        store.TryGetActivatedAsm(StylusTargets.WavmTargetName, in moduleHash, out _).Should().BeTrue();
        db.TryGetActivatedAsm(StylusTargets.WavmTargetName, in moduleHash, out _).Should().BeTrue();
    }

    [Test]
    public void Commit_HasOpenedPages_ResetsOpenedPages()
    {
        (_, _, IWasmStore store) = CreateStore();

        CloseOpenedPages openedPages = store.AddStylusPages(5);
        openedPages.Dispose();
        store.Commit();

        (ushort openNow, ushort openEver) = store.GetStylusPages();

        openNow.Should().Be(0);
        openEver.Should().Be(0);
    }

    private static (IStylusTargetConfig config, IWasmDb db, IWasmStore store) CreateStore(uint cacheTag = 0)
    {
        StylusTargetConfig config = new();
        WasmDb wasmDb = new(new MemDb());
        IWasmStore wasmStore = new WasmStore(wasmDb, config, cacheTag);

        return (config, wasmDb, wasmStore);
    }
}
