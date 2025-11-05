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
using Nethermind.Core.Extensions;
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

            // Clear existing activations for this moduleHash
            ClearActivations(chain, moduleHash);

            // Start rebuild from beginning
            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
            chain.RebuildWasmStore();

            // Should complete successfully
            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            // Should compile ALL targets
            int expectedTargets = chain.StylusTargetConfig.GetWasmTargets().Count;
            int activationsAfter = CountActivations(chain, moduleHash);
            activationsAfter.Should().Be(expectedTargets,
                $"all {expectedTargets} targets should be compiled during rebuild");
        }
    }

    [Test]
    public void Rebuild_WithNoStylusContracts_CompletesWithoutError()
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

            // Should complete successfully even with no contracts
            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        }
    }

    [Test]
    public void Rebuild_WhenAlreadyCompleted_SkipsProcessing()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Mark as already completed
            chain.WasmDB.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);

            // Should complete immediately without processing
            chain.RebuildWasmStore();

            // Position should remain as completed
            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        }
    }

    [Test]
    public void Rebuild_WithPartialActivations_CompilesMissingTargets()
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
            targets.Should().HaveCountGreaterThan(1, "test requires multiple targets");

            // Clear all existing activations
            ClearActivations(chain, moduleHash);

            // Write only FIRST target manually (simulating partial activation)
            Dictionary<string, byte[]> partialAsmMap = new()
            {
                [targets[0]] = [0x01, 0x02, 0x03]
            };
            chain.WasmDB.WriteActivation(moduleHash, partialAsmMap);

            // Verify only one target exists before rebuild
            int activationsBefore = CountActivations(chain, moduleHash);
            activationsBefore.Should().Be(1, "only one target should exist before rebuild");

            // Rebuild should detect missing targets and compile them
            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            // Should now have ALL targets
            int activationsAfter = CountActivations(chain, moduleHash);
            activationsAfter.Should().Be(targets.Count,
                "rebuild should compile all missing targets");
        }
    }

    [Test]
    public void Rebuild_WithInactiveContracts_SkipsProcessing()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Create an inactive contract (just code, not activated in StylusPrograms)
            ValueHash256 inactiveCodeHash = new(Keccak.Compute("inactive_contract").Bytes);
            byte[] inactiveCode = CreateTestStylusCode();
            EnsureContractInCodeDb(chain, inactiveCodeHash, inactiveCode);

            ArbosState arbosState = GetArbosState(chain);
            StylusPrograms stylusPrograms = arbosState.Programs;
            StylusParams stylusParams = stylusPrograms.GetParams();
            ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Verify it's not activated
            bool isActive = stylusPrograms.IsProgramActive(inactiveCodeHash, timestamp, stylusParams);
            isActive.Should().BeFalse("contract should not be activated in StylusPrograms");

            // Get count of activated contracts before rebuild
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> activatedContractsBefore = GetAllActivatedContracts(chain, arbosState);
            int countBefore = activatedContractsBefore.Count;

            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            // Should NOT have created activations for the inactive contract
            // The number of activated contracts should remain the same
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> activatedContractsAfter = GetAllActivatedContracts(chain, arbosState);
            activatedContractsAfter.Count.Should().Be(countBefore,
                "inactive contracts should be skipped during rebuild");
        }
    }

    [Test]
    public void Rebuild_FromSavedPosition_ProcessesRemainingContracts()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ArbosState arbosState = GetArbosState(chain);
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> activatedContracts = GetAllActivatedContracts(chain, arbosState);

            if (activatedContracts.Count < 2)
            {
                Assert.Inconclusive("Test requires at least 2 activated contracts in the recording");
                return;
            }

            // Sort by codeHash to get deterministic order (same as rebuild iterates)
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> sortedContracts = activatedContracts
                .OrderBy(c => c.codeHash.Bytes.ToArray(), Bytes.Comparer)
                .ToList();

            // Clear all activations
            foreach ((ValueHash256 _, ValueHash256 moduleHash) in sortedContracts)
            {
                ClearActivations(chain, moduleHash);
            }

            // Set position to AFTER first contract (so first is skipped, rest are processed)
            Hash256 resumePosition = new(sortedContracts[0].codeHash.Bytes);
            chain.WasmDB.SetRebuildingPosition(resumePosition);

            chain.RebuildWasmStore();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            int expectedTargets = chain.StylusTargetConfig.GetWasmTargets().Count;

            // First contract should be skipped (before/at resume position)
            int firstActivations = CountActivations(chain, sortedContracts[0].moduleHash);
            firstActivations.Should().Be(0,
                "contracts at or before resume position should be skipped");

            // All contracts AFTER resume position should be rebuilt
            for (int i = 1; i < sortedContracts.Count; i++)
            {
                int activations = CountActivations(chain, sortedContracts[i].moduleHash);
                activations.Should().Be(expectedTargets,
                    $"contract {i} (after resume position) should have all targets compiled");
            }
        }
    }

    [Test]
    public void Rebuild_WithNonStylusContracts_IgnoresThem()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Add a regular EVM contract (not Stylus)
            ValueHash256 evmCodeHash = new(Keccak.Compute("evm_contract").Bytes);
            byte[] evmCode = [0x60, 0x80, 0x60, 0x40]; // Simple EVM bytecode
            EnsureContractInCodeDb(chain, evmCodeHash, evmCode);

            ArbosState arbosState = GetArbosState(chain);
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> activatedContractsBefore = GetAllActivatedContracts(chain, arbosState);
            int countBefore = activatedContractsBefore.Count;

            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);

            // Should not throw on non-Stylus contracts
            Action rebuildAction = () => chain.RebuildWasmStore();
            rebuildAction.Should().NotThrow();

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            // EVM contracts should be ignored
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> activatedContractsAfter = GetAllActivatedContracts(chain, arbosState);
            activatedContractsAfter.Count.Should().Be(countBefore,
                "non-Stylus contracts should be ignored");
        }
    }

    [Test]
    public void Rebuild_WithActivatedContract_EnsuresAllTargetsPresent()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();
        Address stylusContract = new(StylusCounterAddress);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            ValueHash256 codeHash = chain.WorldStateManager.GlobalWorldState.GetCodeHash(stylusContract);
            byte[] code = chain.WorldStateManager.GlobalWorldState.GetCode(codeHash) ?? [];

            if (!StylusCode.IsStylusProgram(code))
            {
                Assert.Inconclusive("Test contract is not a Stylus program");
                return;
            }

            EnsureContractInCodeDb(chain, codeHash, code);

            ArbosState arbosState = GetArbosState(chain);
            StylusPrograms stylusPrograms = arbosState.Programs;
            ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            StylusPrograms.Program program = stylusPrograms.GetProgram(codeHash, timestamp);

            if (program.Version == 0)
            {
                Assert.Inconclusive("Test contract is not activated");
                return;
            }

            ValueHash256 moduleHash = stylusPrograms.ModuleHashesStorage.Get(codeHash);

            // Run rebuild from start
            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);

            Action rebuild = () => chain.RebuildWasmStore();
            rebuild.Should().NotThrow("rebuild should complete successfully");

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone,
                "rebuild should mark completion");

            // After rebuild, all targets should be present
            // (either they were already there, or rebuild added them)
            int expectedTargets = chain.StylusTargetConfig.GetWasmTargets().Count;
            int activations = CountActivations(chain, moduleHash);
            activations.Should().Be(expectedTargets,
                $"all {expectedTargets} targets should be present after rebuild");
        }
    }

    [Test]
    public void Rebuild_WithCorruptedCodeEntry_ContinuesProcessing()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestChain();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Add a corrupted Stylus code entry
            ValueHash256 corruptedCodeHash = new(Keccak.Compute("corrupted").Bytes);
            byte[] corruptedCode = [0xFF, 0xFF, 0xFF]; // Invalid Stylus code
            EnsureContractInCodeDb(chain, corruptedCodeHash, corruptedCode);

            // Get legitimate contracts
            ArbosState arbosState = GetArbosState(chain);
            List<(ValueHash256 codeHash, ValueHash256 moduleHash)> activatedContracts = GetAllActivatedContracts(chain, arbosState);

            if (activatedContracts.Count > 0)
            {
                // Clear legitimate contracts
                foreach ((ValueHash256 _, ValueHash256 moduleHash) in activatedContracts)
                {
                    ClearActivations(chain, moduleHash);
                }
            }

            chain.WasmDB.SetRebuildingPosition(Keccak.Zero);

            // Should not throw even with corrupted entries
            Action rebuildAction = () => chain.RebuildWasmStore();
            rebuildAction.Should().NotThrow("rebuild should handle errors gracefully");

            chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

            // Valid contracts should still be rebuilt
            int expectedTargets = chain.StylusTargetConfig.GetWasmTargets().Count;
            foreach ((ValueHash256 codeHash, ValueHash256 moduleHash) in activatedContracts)
            {
                int activations = CountActivations(chain, moduleHash);
                activations.Should().Be(expectedTargets,
                    $"valid contract {codeHash} should be rebuilt despite corrupted entries");
            }
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

    private static void ClearActivations(ArbitrumRpcTestBlockchain chain, ValueHash256 moduleHash)
    {
        // Build activation keys manually for each target and delete them
        List<string> targets = chain.StylusTargetConfig.GetWasmTargets().ToList();

        foreach (string target in targets)
        {
            // Activation keys in WasmStore are: 'a' + moduleHash(32) + target_name
            byte[] targetBytes = System.Text.Encoding.UTF8.GetBytes(target);
            byte[] key = new byte[1 + 32 + targetBytes.Length];
            key[0] = (byte)'a'; // activation prefix
            moduleHash.Bytes.CopyTo(key.AsSpan()[1..33]);
            targetBytes.CopyTo(key.AsSpan()[33..]);

            // Try to get first to see if it exists, then remove
            byte[]? existing = chain.WasmDB.Get(key);
            if (existing != null)
            {
                chain.WasmDB.Set(key, []); // Clear by setting empty
            }
        }
    }

    private static void ClearCodeDb(ArbitrumRpcTestBlockchain chain)
    {
        // Get all entries first to avoid modifying collection during enumeration
        List<KeyValuePair<byte[], byte[]?>> allEntries = chain.CodeDB.GetAll().ToList();

        foreach (KeyValuePair<byte[], byte[]?> entry in allEntries)
        {
            chain.CodeDB.Remove(entry.Key);
        }
    }

    private static List<(ValueHash256 codeHash, ValueHash256 moduleHash)>
        GetAllActivatedContracts(ArbitrumRpcTestBlockchain chain, ArbosState arbosState)
    {
        List<(ValueHash256, ValueHash256)> result = [];
        StylusPrograms programs = arbosState.Programs;
        StylusParams stylusParams = programs.GetParams();
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach ((byte[] key, byte[]? value) in chain.CodeDB.GetAll())
        {
            if (value == null || value.Length == 0)
                continue;
            if (key.Length != 33 || key[0] != 0x63)
                continue; // 'c' prefix for code

            ValueHash256 codeHash = new(key.AsSpan()[1..33].ToArray());

            // Must be Stylus program
            if (!StylusCode.IsStylusProgram(value))
                continue;

            // Check if activated using existing GetProgram method
            StylusPrograms.Program program = programs.GetProgram(codeHash, timestamp);

            // Skip if not activated (version == 0) or expired
            if (program.Version == 0)
                continue;

            ulong expirySeconds = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);
            if (program.AgeSeconds > expirySeconds)
                continue;

            // Must match current Stylus version
            if (program.Version != stylusParams.StylusVersion)
                continue;

            // Get moduleHash
            try
            {
                ValueHash256 moduleHash = programs.ModuleHashesStorage.Get(codeHash);
                result.Add((codeHash, moduleHash));
            }
            catch
            {
                // No moduleHash means not properly activated
                continue;
            }
        }

        return result;
    }

    private static byte[] CreateTestStylusCode()
    {
        // Create minimal valid WASM module
        byte[] wasmCode = [
            0x00, 0x61, 0x73, 0x6D, // WASM magic number
            0x01, 0x00, 0x00, 0x00  // WASM version
        ];

        byte[] compressed = BrotliCompression.Compress(wasmCode, compressionLevel: 11);
        byte[] stylusPrefix = StylusCode.NewStylusPrefix(dictionary: 1);

        return stylusPrefix.Concat(compressed).ToArray();
    }

    private static byte[] CreateCodeDbKey(ValueHash256 codeHash)
    {
        byte[] key = new byte[33];
        key[0] = 0x63; // 'c' prefix for code keys
        codeHash.Bytes.CopyTo(key.AsSpan()[1..]);
        return key;
    }
}
