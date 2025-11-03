// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Stylus;

[TestFixture]
public class WasmStoreRebuildTests : IDisposable
{
    private readonly ArbitrumRpcTestBlockchain _blockchain;
    private readonly IWasmDb _wasmDb;
    private readonly IDb _codeDb;
    private readonly IWorldState _worldState;
    private readonly ICodeInfoRepository _codeRepository;
    private readonly IDisposable _worldStateScope;

    public WasmStoreRebuildTests()
    {
        _blockchain = CreateBlockchain(new ArbitrumConfig { RebuildLocalWasm = "false" }, suggestGenesis: false);
        _wasmDb = _blockchain.WasmDB;
        _codeDb = _blockchain.CodeDB;
        _worldState = _blockchain.WorldStateManager.GlobalWorldState;

        BlockHeader? genesisBlock = _blockchain.BlockTree.Genesis;
        _worldStateScope = _worldState.BeginScope(genesisBlock);

        DeployTestsContract.CreateTestPrograms(_worldState);

        EthereumCodeInfoRepository baseRepository = new(_worldState);
        _codeRepository = new ArbitrumCodeInfoRepository(baseRepository);
    }

    public void Dispose()
    {
        _worldStateScope?.Dispose();
        _blockchain?.Dispose();
    }

    [Test]
    public void UpgradeWasmerVersion_WhenDatabaseEmpty_SetsCurrentVersion()
    {
        _blockchain.InitializeWasmDb();

        _wasmDb.GetWasmerSerializeVersion().Should().Be(WasmStoreSchema.WasmerSerializeVersion);
    }

    [Test]
    public void UpgradeWasmerVersion_WhenOldVersionExists_RemovesEntriesAndUpdatesVersion()
    {
        _wasmDb.SetWasmerSerializeVersion(7);
        byte[] oldKey = WasmStoreSchema.GetActivatedKey(StylusTargets.Arm64TargetName, Keccak.Compute("test"));
        _wasmDb.Set(oldKey, [1, 2, 3]);

        _blockchain.InitializeWasmDb();

        _wasmDb.GetWasmerSerializeVersion().Should().Be(WasmStoreSchema.WasmerSerializeVersion);
        _wasmDb.Get(oldKey).Should().BeNull();
    }

    [Test]
    public void UpgradeSchemaVersion_WhenDatabaseEmpty_SetsCurrentVersion()
    {
        _blockchain.InitializeWasmDb();

        _wasmDb.GetWasmSchemaVersion().Should().Be(WasmStoreSchema.WasmSchemaVersion);
    }

    [Test]
    public void UpgradeSchemaVersion_WhenVersionZero_RemovesDeprecatedEntries()
    {
        _wasmDb.SetWasmSchemaVersion(0);
        (IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int keyLength) = WasmStoreSchema.DeprecatedPrefixesV0();
        byte[] deprecatedKey = CreateKeyWithPrefix(prefixes[0], keyLength);
        _wasmDb.Set(deprecatedKey, [1, 2, 3]);

        _blockchain.InitializeWasmDb();

        _wasmDb.GetWasmSchemaVersion().Should().Be(WasmStoreSchema.WasmSchemaVersion);
        _wasmDb.Get(deprecatedKey).Should().BeNull();
    }

    [Test]
    public void UpgradeSchemaVersion_WhenUnsupportedVersion_ThrowsException()
    {
        _wasmDb.SetWasmSchemaVersion(99);

        Action act = () => _blockchain.InitializeWasmDb();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported wasm database schema version*");
    }

    [Test]
    public void InitializeRebuild_WhenNoBlocks_MarksDone()
    {
        _blockchain.InitializeWasmDb();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void InitializeRebuild_WhenOnlyGenesis_MarksDone()
    {
        using ArbitrumRpcTestBlockchain blockchainWithGenesis = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: true);

        blockchainWithGenesis.InitializeWasmDb();

        blockchainWithGenesis.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void InitializeRebuild_WhenGenesisBlock_CompletesSuccessfully()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: true);

        blockchain.InitializeWasmDb();

        blockchain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone,
            "initialization with genesis should complete successfully");
    }

    [Test]
    public void InitializeRebuild_WhenForceMode_StartsFromZero()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "force" },
            suggestGenesis: false);
        blockchain.WasmDB.SetRebuildingPosition(TestItem.KeccakA);

        blockchain.InitializeWasmDb();

        Hash256? position = blockchain.WasmDB.GetRebuildingPosition();
        (position == Keccak.Zero || position == WasmStoreSchema.RebuildingDone).Should().BeTrue();
    }

    [Test]
    public void InitializeRebuild_WhenAutoModeWithoutPosition_InitializesPosition()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: false);

        blockchain.InitializeWasmDb();

        Hash256? position = blockchain.WasmDB.GetRebuildingPosition();
        (position == Keccak.Zero || position == WasmStoreSchema.RebuildingDone).Should().BeTrue();
    }

    [Test]
    public void InitializeRebuild_WhenAutoModeWithPosition_PreservesPosition()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: false);
        Hash256 existingPosition = Keccak.Compute("existing");
        blockchain.WasmDB.SetRebuildingPosition(existingPosition);

        blockchain.InitializeWasmDb();

        Hash256? position = blockchain.WasmDB.GetRebuildingPosition();
        (position == existingPosition || position == WasmStoreSchema.RebuildingDone).Should().BeTrue();
    }

    [Test]
    public void InitializeRebuild_WhenAlreadyComplete_ReturnsDone()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: true);
        blockchain.WasmDB.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);

        blockchain.InitializeWasmDb();

        WasmStoreSchema.IsRebuildingDone(blockchain.WasmDB.GetRebuildingPosition()!).Should().BeTrue();
    }

    [Test]
    public void Rebuild_WhenEmptyCodeDatabase_CompletesImmediately()
    {
        _blockchain.RebuildWasmStore();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void Rebuild_WhenOnlyEvmContracts_CompletesWithoutProcessing()
    {
        DeployContract(ContractType.Evm);

        _blockchain.RebuildWasmStore();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void Rebuild_WhenSingleStylusContract_CompilesAndStores()
    {
        Hash256 codeHash = DeployContract(ContractType.Stylus);

        _blockchain.RebuildWasmStore();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(codeHash);
        VerifyActivationExists(codeHash);
        CountStylusContracts().Should().BeGreaterThan(0);
    }

    [Test]
    public void Rebuild_WhenMultipleStylusContracts_ProcessesAll()
    {
        List<Hash256> codeHashes = DeployMultipleContracts(ContractType.Stylus, count: 3);

        _blockchain.RebuildWasmStore();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        codeHashes.ForEach(VerifyStylusContractExists);
        codeHashes.ForEach(VerifyActivationExists);
        CountStylusContracts().Should().BeGreaterThan(0, "deployed contracts share same code hash due to deduplication");
    }

    [Test]
    public void Rebuild_WhenMixedContracts_ProcessesOnlyStylus()
    {
        DeployContract(ContractType.Evm);
        Hash256 stylusHash1 = DeployContract(ContractType.Stylus);
        DeployContract(ContractType.Evm);
        Hash256 stylusHash2 = DeployContract(ContractType.Stylus);

        _blockchain.RebuildWasmStore();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(stylusHash1);
        VerifyStylusContractExists(stylusHash2);
        VerifyActivationExists(stylusHash1);
        VerifyActivationExists(stylusHash2);
        CountStylusContracts().Should().Be(2);
    }

    [Test]
    public void Rebuild_WhenStartPositionProvided_SkipsPreviousContracts()
    {
        Hash256 hash1 = DeployContract(ContractType.Stylus);
        Hash256 hash2 = DeployContract(ContractType.Stylus);
        Hash256 hash3 = DeployContract(ContractType.Stylus);
        _wasmDb.SetRebuildingPosition(hash2);

        _blockchain.RebuildWasmStore(startPosition: hash2);

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(hash3);
        VerifyActivationExists(hash3);
    }

    [Test]
    public void Rebuild_WhenResuming_SkipsProcessedContracts()
    {
        // Deploy EVM contract, then Stylus contracts
        // EVM contracts have different code hashes, providing unique entries in code DB
        Hash256 evmHash1 = DeployEvmContract();
        Hash256 stylusHash = DeployContract(ContractType.Stylus);
        Hash256 evmHash2 = DeployEvmContract();

        // Start rebuild from the Stylus contract position
        // This should process the Stylus contract and evmHash2, but skip evmHash1
        _blockchain.RebuildWasmStore(startPosition: stylusHash);

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);

        // Stylus contract should be processed
        VerifyStylusContractExists(stylusHash);
        VerifyActivationExists(stylusHash);

        // All EVM contracts should still exist in code DB
        _codeDb.Get(evmHash1.Bytes.ToArray()).Should().NotBeNull();
        _codeDb.Get(evmHash2.Bytes.ToArray()).Should().NotBeNull();
    }

    [Test]
    public void Rebuild_WhenInvalidStylusCode_SkipsAndContinues()
    {
        Hash256 validHash = DeployContract(ContractType.Stylus);
        Hash256 invalidHash = DeployContract(ContractType.InvalidStylus);
        Hash256 valid2Hash = DeployContract(ContractType.Stylus);

        _blockchain.RebuildWasmStore();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(validHash);
        VerifyStylusContractExists(valid2Hash);
        VerifyActivationExists(validHash);
        VerifyActivationExists(valid2Hash);
        _codeDb.Get(invalidHash.Bytes.ToArray()).Should().NotBeNull("invalid code should still exist in database");
    }

    [Test]
    public void Rebuild_WhenMultipleTargets_CompilesForAll()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchainWithMultipleTargets();

        IWorldState worldState = blockchain.WorldStateManager.GlobalWorldState;
        BlockHeader? genesisBlock = blockchain.BlockTree.Genesis;
        using IDisposable scope = worldState.BeginScope(genesisBlock);

        DeployTestsContract.CreateTestPrograms(worldState);
        EthereumCodeInfoRepository baseRepository = new(worldState);
        ICodeInfoRepository codeRepository = new ArbitrumCodeInfoRepository(baseRepository);

        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(
            worldState,
            codeRepository,
            compress: true,
            prependStylusPrefix: true);

        ValueHash256 codeHash = worldState.GetCodeHash(contract);
        Hash256 hash = new(codeHash.Bytes);

        blockchain.RebuildWasmStore();

        ValueHash256 moduleHash = GetModuleHashForCode(hash, blockchain.CodeDB);
        IReadOnlyCollection<string> targets = blockchain.StylusTargetConfig.GetWasmTargets();
        targets.Should().HaveCountGreaterThan(1, "test should use multiple targets");

        foreach (string target in targets)
        {
            bool hasActivation = blockchain.WasmDB.TryGetActivatedAsm(target, moduleHash, out byte[]? asm);
            hasActivation.Should().BeTrue($"activation should exist for target {target}");
            asm.Should().NotBeEmpty($"activation for target {target} should not be empty");
        }
    }

    private Hash256 DeployContract(ContractType type)
    {
        return type switch
        {
            ContractType.Stylus => DeployStylusContract(),
            ContractType.Evm => DeployEvmContract(),
            ContractType.InvalidStylus => DeployInvalidStylusContract(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private List<Hash256> DeployMultipleContracts(ContractType type, int count)
    {
        List<Hash256> hashes = [];
        for (int i = 0; i < count; i++)
        {
            hashes.Add(DeployContract(type));
        }
        return hashes;
    }

    private Hash256 DeployStylusContract()
    {
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(
            _worldState,
            _codeRepository,
            compress: true,
            prependStylusPrefix: true);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        return new Hash256(codeHash.Bytes);
    }

    private Hash256 DeployEvmContract()
    {
        // Generate unique EVM code each time by including a random value
        byte[] evmCode = [0x60, (byte)Random.Shared.Next(256), 0x60, 0x40, 0x52];
        return DeployCodeAndCommit(evmCode);
    }

    private Hash256 DeployInvalidStylusContract()
    {
        byte[] invalidCode = [.. StylusCode.NewStylusPrefix(1), .. "invalid compressed data"u8.ToArray()];
        return DeployCodeAndCommit(invalidCode);
    }

    private Hash256 DeployCodeAndCommit(byte[] code)
    {
        Address contractAddress = Address.FromNumber((UInt256)Random.Shared.Next());
        _worldState.CreateAccount(contractAddress, UInt256.Zero, UInt256.Zero);
        Hash256 codeHash = Keccak.Compute(code);
        _codeRepository.InsertCode(code, contractAddress, FullChainSimulationReleaseSpec.Instance);

        _worldState.Commit(FullChainSimulationReleaseSpec.Instance);
        _worldState.CommitTree(0);

        return codeHash;
    }

    private void VerifyStylusContractExists(Hash256 codeHash)
    {
        byte[]? code = _codeDb.Get(codeHash.Bytes.ToArray());
        code.Should().NotBeNull($"code for {codeHash} should exist");
        code.Should().NotBeEmpty();
        StylusCode.IsStylusProgram(code).Should().BeTrue($"code {codeHash} should be Stylus program");
    }

    private void VerifyActivationExists(Hash256 codeHash)
    {
        ValueHash256 moduleHash = GetModuleHashForCode(codeHash);
        string localTarget = StylusTargets.GetLocalTargetName();

        bool hasActivation = _wasmDb.TryGetActivatedAsm(localTarget, moduleHash, out byte[]? activatedAsm);
        hasActivation.Should().BeTrue($"activation for codeHash {codeHash} (moduleHash {moduleHash}) should exist in WASM database");
        activatedAsm.Should().NotBeNull("activated ASM should not be null");
        activatedAsm.Should().NotBeEmpty("activated ASM should not be empty");
    }

    private ValueHash256 GetModuleHashForCode(Hash256 codeHash, IDb? codeDb = null)
    {
        codeDb ??= _codeDb;
        byte[]? code = codeDb.Get(codeHash.Bytes.ToArray());
        code.Should().NotBeNull("code should exist for module hash calculation");

        StylusOperationResult<StylusBytes> stylusBytes = StylusCode.StripStylusPrefix(code!);
        stylusBytes.IsSuccess.Should().BeTrue("should be valid Stylus code");

        byte[] wasm = BrotliCompression.Decompress(
            stylusBytes.Value.Bytes,
            maxSize: 128 * 1024 * 1024,
            stylusBytes.Value.Dictionary);
        wasm.Should().NotBeEmpty("decompressed WASM should not be empty");

        ulong unusedGas = 0;
        StylusNativeResult<ActivateResult> result = StylusNative.Activate(
            wasm,
            pageLimit: ushort.MaxValue,
            stylusVersion: 1,
            arbosVersionForGas: 0,
            debug: false,
            new Nethermind.Arbitrum.Arbos.Stylus.Bytes32(codeHash.Bytes),
            ref unusedGas);

        result.IsSuccess.Should().BeTrue($"compilation should succeed, got: {result.Status}");
        result.Value.WavmModule.Should().NotBeNull("WAVM module should not be null");

        return Keccak.Compute(result.Value.WavmModule!);
    }

    private int CountStylusContracts()
    {
        return _codeDb.GetAll()
            .Count(entry => entry.Value != null && StylusCode.IsStylusProgram(entry.Value));
    }

    private static byte[] CreateKeyWithPrefix(ReadOnlyMemory<byte> prefix, int totalLength)
    {
        byte[] key = new byte[totalLength];
        prefix.Span.CopyTo(key);
        Random.Shared.NextBytes(key.AsSpan(prefix.Length));
        return key;
    }

    private static ArbitrumRpcTestBlockchain CreateBlockchain(ArbitrumConfig config, bool suggestGenesis)
    {
        Action<ContainerBuilder> configurer = cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = suggestGenesis,
                FillWithTestDataOnStart = false
            });
            cb.AddSingleton<IArbitrumConfig>(config);
            cb.AddSingleton<IStylusTargetConfig>(new StylusTargetConfig());
            cb.AddSingleton(WasmStore.Instance);
            cb.AddSingleton(new ArbitrumChainSpecEngineParameters
            {
                GenesisBlockNum = 0,
                EnableArbOS = true
            });
        };

        return ArbitrumRpcTestBlockchain.CreateDefault(configurer);
    }

    private static ArbitrumRpcTestBlockchain CreateBlockchainWithMultipleTargets()
    {
        Action<ContainerBuilder> configurer = cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = false,
                FillWithTestDataOnStart = false
            });
            cb.AddSingleton<IArbitrumConfig>(new ArbitrumConfig { RebuildLocalWasm = "false" });

            StylusTargetConfig targetConfig = new()
            {
                ExtraArchs = [StylusTargets.WavmTargetName, StylusTargets.Arm64TargetName]
            };
            cb.AddSingleton<IStylusTargetConfig>(targetConfig);

            cb.AddSingleton(WasmStore.Instance);
            cb.AddSingleton(new ArbitrumChainSpecEngineParameters
            {
                GenesisBlockNum = 0,
                EnableArbOS = true
            });
        };

        return ArbitrumRpcTestBlockchain.CreateDefault(configurer);
    }

    private enum ContractType
    {
        Stylus,
        Evm,
        InvalidStylus
    }
}
