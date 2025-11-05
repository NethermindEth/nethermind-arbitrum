// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Stylus;

[TestFixture]
public class WasmStoreRebuildTests
{
    private const string StylusCounterAddress = "0x0bdad990640a488400565fe6fb1d879ffe12da37";
    private static readonly string RecordingPath = "./Recordings/2__stylus.jsonl";

    [Test]
    public void Rebuild_WithActivatedStylusContract_CompilesAllTargets()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();
        Address stylusContract = new(StylusCounterAddress);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ValueHash256 codeHash = chain.WorldStateManager.GlobalWorldState.GetCodeHash(stylusContract);
            byte[] code = chain.WorldStateManager.GlobalWorldState.GetCode(codeHash) ?? [];

            EnsureContractInCodeDb(chain, codeHash, code);

            ArbosState arbosState = GetArbosState(chain);
            StylusPrograms stylusPrograms = arbosState.Programs;
            ValueHash256 moduleHash = stylusPrograms.ModuleHashesStorage.Get(codeHash);

            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            int activationsAfter = CountActivations(chain, moduleHash);
            activationsAfter.Should().BeGreaterThan(0);
            activationsAfter.Should().Be(chain.StylusTargetConfig.GetWasmTargets().Count);
        }
    }

    [Test]
    public void Rebuild_WhenNoStylusContracts_CompletesWithoutError()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Clear CodeDB to ensure no Stylus contracts
            ClearCodeDb(chain);

            // Set rebuild position to start
            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);

            // This should not throw and should complete
            Action rebuildAction = () => chain.RebuildWasmStore();
            rebuildAction.Should().NotThrow();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        }
    }

    [Test]
    public void Rebuild_WhenAlreadyCompleted_SkipsImmediately()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            chain.WasmDB.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);

            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        }
    }

    [Test]
    public void Rebuild_WithMultipleStylusContracts_CompilesAll()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();
        Address stylusContract = new(StylusCounterAddress);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ValueHash256 codeHash = chain.WorldStateManager.GlobalWorldState.GetCodeHash(stylusContract);
            byte[] code = chain.WorldStateManager.GlobalWorldState.GetCode(codeHash) ?? [];

            EnsureContractInCodeDb(chain, codeHash, code);

            ValueHash256 secondCodeHash = new(Keccak.Compute("test").Bytes);
            byte[] secondCode = CreateTestStylusCode();
            EnsureContractInCodeDb(chain, secondCodeHash, secondCode);

            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            ArbosState arbosState = GetArbosState(chain);
            StylusPrograms stylusPrograms = arbosState.Programs;
            ValueHash256 moduleHash = stylusPrograms.ModuleHashesStorage.Get(codeHash);

            int activationsAfter = CountActivations(chain, moduleHash);
            activationsAfter.Should().BeGreaterThan(0);
        }
    }

    [Test]
    public void Rebuild_WhenPartialActivationsExist_CompilesOnlyMissing()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();
        Address stylusContract = new(StylusCounterAddress);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ValueHash256 codeHash = chain.WorldStateManager.GlobalWorldState.GetCodeHash(stylusContract);
            byte[] code = chain.WorldStateManager.GlobalWorldState.GetCode(codeHash) ?? [];

            EnsureContractInCodeDb(chain, codeHash, code);

            ArbosState arbosState = GetArbosState(chain);
            StylusPrograms stylusPrograms = arbosState.Programs;
            ValueHash256 moduleHash = stylusPrograms.ModuleHashesStorage.Get(codeHash);

            List<string> targets = chain.StylusTargetConfig.GetWasmTargets().ToList();
            if (targets.Count > 1)
            {
                Dictionary<string, byte[]> partialAsmMap = new()
                {
                    [targets[0]] = [0x01, 0x02, 0x03]
                };
                chain.WasmDB.WriteActivation(moduleHash, partialAsmMap);
            }

            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            int activationsAfter = CountActivations(chain, moduleHash);
            activationsAfter.Should().Be(targets.Count);
        }
    }

    private static ArbitrumRpcTestBlockchain CreateTestChain()
    {
        return new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), 22)
            .Build();
    }

    private static void EnsureContractInCodeDb(ArbitrumRpcTestBlockchain chain, ValueHash256 codeHash, byte[] code)
    {
        byte[] codeDbKey = CreateCodeDbKey(codeHash);
        chain.CodeDB.Set(codeDbKey, code);
    }

    private static ArbosState GetArbosState(ArbitrumRpcTestBlockchain chain)
    {
        return ArbosState.OpenArbosState(
            chain.WorldStateManager.GlobalWorldState,
            new SystemBurner(),
            LimboNoErrorLogger.Instance);
    }

    private static int CountActivations(ArbitrumRpcTestBlockchain chain, ValueHash256 moduleHash)
    {
        return chain.StylusTargetConfig.GetWasmTargets()
            .Count(target => chain.WasmDB.TryGetActivatedAsm(target, moduleHash, out _));
    }

    private static void ClearCodeDb(ArbitrumRpcTestBlockchain chain)
    {
        foreach (KeyValuePair<byte[], byte[]?> entry in chain.CodeDB.GetAll().ToList())
        {
            chain.CodeDB.Remove(entry.Key);
        }
    }

    private static byte[] CreateTestStylusCode()
    {
        byte[] wasmCode = [0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00];
        byte[] compressed = BrotliCompression.Compress(wasmCode, 11UL); // Use compression level 11
        byte[] stylusCode = StylusCode.NewStylusPrefix(1).Concat(compressed).ToArray();
        return stylusCode;
    }

    private static byte[] CreateCodeDbKey(ValueHash256 codeHash)
    {
        byte[] key = new byte[33];
        key[0] = 0x63;
        codeHash.Bytes.CopyTo(key.AsSpan()[1..]);
        return key;
    }
}
