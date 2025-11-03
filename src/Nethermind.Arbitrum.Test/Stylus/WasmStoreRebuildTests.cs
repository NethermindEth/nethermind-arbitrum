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
using Nethermind.Logging;

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
        ArbitrumConfig config = new() { RebuildLocalWasm = "false" };
        _blockchain = CreateBlockchain(config, suggestGenesis: false);
        _wasmDb = _blockchain.Container.Resolve<IWasmDb>();
        _codeDb = _blockchain.Container.ResolveKeyed<IDb>("code");
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
        ExecuteWasmerUpgrade();

        _wasmDb.GetWasmerSerializeVersion().Should().Be(WasmStoreSchema.WasmerSerializeVersion);
    }

    [Test]
    public void UpgradeWasmerVersion_WhenOldVersionExists_RemovesEntriesAndUpdatesVersion()
    {
        _wasmDb.SetWasmerSerializeVersion(7);
        byte[] oldKey = WasmStoreSchema.GetActivatedKey(StylusTargets.Arm64TargetName, Keccak.Compute("test"));
        _wasmDb.Set(oldKey, [1, 2, 3]);

        ExecuteWasmerUpgrade();

        _wasmDb.GetWasmerSerializeVersion().Should().Be(WasmStoreSchema.WasmerSerializeVersion);
        _wasmDb.Get(oldKey).Should().BeNull();
    }

    [Test]
    public void UpgradeSchemaVersion_WhenDatabaseEmpty_SetsCurrentVersion()
    {
        ExecuteSchemaUpgrade();

        _wasmDb.GetWasmSchemaVersion().Should().Be(WasmStoreSchema.WasmSchemaVersion);
    }

    [Test]
    public void UpgradeSchemaVersion_WhenVersionZero_RemovesDeprecatedEntries()
    {
        _wasmDb.SetWasmSchemaVersion(0);
        (IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int keyLength) = WasmStoreSchema.DeprecatedPrefixesV0();
        byte[] deprecatedKey = CreateKeyWithPrefix(prefixes[0], keyLength);
        _wasmDb.Set(deprecatedKey, [1, 2, 3]);

        ExecuteSchemaUpgrade();

        _wasmDb.GetWasmSchemaVersion().Should().Be(WasmStoreSchema.WasmSchemaVersion);
        _wasmDb.Get(deprecatedKey).Should().BeNull();
    }

    [Test]
    public void UpgradeSchemaVersion_WhenUnsupportedVersion_ThrowsException()
    {
        _wasmDb.SetWasmSchemaVersion(99);

        Action act = ExecuteSchemaUpgrade;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported wasm database schema version*");
    }

    [Test]
    public void InitializeRebuild_WhenNoBlocks_MarksDone()
    {
        ExecuteInitStep();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void InitializeRebuild_WhenOnlyGenesis_MarksDone()
    {
        using ArbitrumRpcTestBlockchain blockchainWithGenesis = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: true);
        IWasmDb wasmDb = blockchainWithGenesis.Container.Resolve<IWasmDb>();

        ExecuteInitStep(blockchainWithGenesis);

        wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void InitializeRebuild_WhenForceMode_StartsFromZero()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "force" },
            suggestGenesis: false);
        IWasmDb wasmDb = blockchain.Container.Resolve<IWasmDb>();
        wasmDb.SetRebuildingPosition(TestItem.KeccakA);

        ExecuteInitStep(blockchain);

        Hash256? position = wasmDb.GetRebuildingPosition();
        (position == Keccak.Zero || position == WasmStoreSchema.RebuildingDone).Should().BeTrue();
    }

    [Test]
    public void InitializeRebuild_WhenAutoModeWithoutPosition_InitializesPosition()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: false);
        IWasmDb wasmDb = blockchain.Container.Resolve<IWasmDb>();

        ExecuteInitStep(blockchain);

        Hash256? position = wasmDb.GetRebuildingPosition();
        (position == Keccak.Zero || position == WasmStoreSchema.RebuildingDone).Should().BeTrue();
    }

    [Test]
    public void InitializeRebuild_WhenAutoModeWithPosition_PreservesPosition()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: false);
        IWasmDb wasmDb = blockchain.Container.Resolve<IWasmDb>();
        Hash256 existingPosition = Keccak.Compute("existing");
        wasmDb.SetRebuildingPosition(existingPosition);

        ExecuteInitStep(blockchain);

        Hash256? position = wasmDb.GetRebuildingPosition();
        (position == existingPosition || position == WasmStoreSchema.RebuildingDone).Should().BeTrue();
    }

    [Test]
    public void InitializeRebuild_WhenAlreadyComplete_ReturnsDone()
    {
        using ArbitrumRpcTestBlockchain blockchain = CreateBlockchain(
            new ArbitrumConfig { RebuildLocalWasm = "auto" },
            suggestGenesis: true);
        IWasmDb wasmDb = blockchain.Container.Resolve<IWasmDb>();
        wasmDb.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);

        ExecuteInitStep(blockchain);

        WasmStoreSchema.IsRebuildingDone(wasmDb.GetRebuildingPosition()!).Should().BeTrue();
    }

    [Test]
    public void Rebuild_WhenEmptyCodeDatabase_CompletesImmediately()
    {
        ExecuteRebuild();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void Rebuild_WhenOnlyEvmContracts_CompletesWithoutProcessing()
    {
        DeployContract(ContractType.Evm);

        ExecuteRebuild();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void Rebuild_WhenSingleStylusContract_CompilesAndStores()
    {
        Hash256 codeHash = DeployContract(ContractType.Stylus);

        ExecuteRebuild();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(codeHash);
        VerifyActivationExists(codeHash);
        CountStylusContracts().Should().BeGreaterThan(0);
    }

    [Test]
    public void Rebuild_WhenMultipleStylusContracts_ProcessesAll()
    {
        List<Hash256> codeHashes = DeployMultipleContracts(ContractType.Stylus, count: 3);

        ExecuteRebuild();

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

        ExecuteRebuild();

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

        ExecuteRebuild(startPosition: hash2);

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(hash3);
        VerifyActivationExists(hash3);
    }

    [Test]
    public void Rebuild_WhenInvalidStylusCode_SkipsAndContinues()
    {
        Hash256 validHash = DeployContract(ContractType.Stylus);
        Hash256 invalidHash = DeployContract(ContractType.InvalidStylus);
        Hash256 valid2Hash = DeployContract(ContractType.Stylus);

        ExecuteRebuild();

        _wasmDb.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
        VerifyStylusContractExists(validHash);
        VerifyStylusContractExists(valid2Hash);
        VerifyActivationExists(validHash);
        VerifyActivationExists(valid2Hash);
        _codeDb.Get(invalidHash.Bytes.ToArray()).Should().NotBeNull("invalid code should still exist in database");
    }

    private void ExecuteWasmerUpgrade()
    {
        if (_wasmDb.IsEmpty()) return;
        uint versionInDB = _wasmDb.GetWasmerSerializeVersion();
        if (versionInDB == WasmStoreSchema.WasmerSerializeVersion) return;
        _wasmDb.DeleteWasmEntries(WasmStoreSchema.WasmPrefixesExceptWavm());
        _wasmDb.SetWasmerSerializeVersion(WasmStoreSchema.WasmerSerializeVersion);
    }

    private void ExecuteSchemaUpgrade()
    {
        if (!_wasmDb.IsEmpty())
        {
            byte version = _wasmDb.GetWasmSchemaVersion();
            switch (version)
            {
                case > WasmStoreSchema.WasmSchemaVersion:
                    throw new InvalidOperationException($"Unsupported wasm database schema version: {version}");
                case 0:
                    (IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int keyLength) = WasmStoreSchema.DeprecatedPrefixesV0();
                    _wasmDb.DeleteWasmEntries(prefixes, keyLength);
                    break;
            }
        }
        _wasmDb.SetWasmSchemaVersion(WasmStoreSchema.WasmSchemaVersion);
    }

    private void ExecuteInitStep(ArbitrumRpcTestBlockchain? blockchain = null)
    {
        blockchain ??= _blockchain;
        IWasmDb wasmDb = blockchain.Container.Resolve<IWasmDb>();
        IWasmStore wasmStore = blockchain.Container.Resolve<IWasmStore>();
        IDb codeDb = blockchain.Container.ResolveKeyed<IDb>("code");
        IBlockTree blockTree = blockchain.Container.Resolve<IBlockTree>();
        IArbitrumConfig config = blockchain.Container.Resolve<IArbitrumConfig>();
        IStylusTargetConfig targetConfig = blockchain.Container.Resolve<IStylusTargetConfig>();
        ArbitrumChainSpecEngineParameters chainParams = blockchain.Container.Resolve<ArbitrumChainSpecEngineParameters>();

        ArbitrumInitializeWasmDb step = new(
            wasmDb,
            wasmStore,
            codeDb,
            blockTree,
            config,
            targetConfig,
            chainParams,
            NullLogManager.Instance);

        step.Execute(CancellationToken.None).GetAwaiter().GetResult();
    }

    private void ExecuteRebuild(Hash256? startPosition = null)
    {
        IWasmStore wasmStore = _blockchain.Container.Resolve<IWasmStore>();
        IStylusTargetConfig targetConfig = _blockchain.Container.Resolve<IStylusTargetConfig>();
        WasmStoreRebuilder rebuilder = new(_wasmDb, wasmStore, targetConfig, NullLogManager.Instance.GetClassLogger());

        rebuilder.RebuildWasmStore(
            _codeDb,
            startPosition ?? Keccak.Zero,
            latestBlockTime: (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            rebuildStartBlockTime: 1000,
            debugMode: false,
            CancellationToken.None);
    }

    private Hash256 DeployContract(ContractType type, int? uniqueId = null)
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
            hashes.Add(DeployContract(type, uniqueId: i));
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
        byte[] evmCode = "0x60806040"u8.ToArray();
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
        byte[]? code = _codeDb.Get(codeHash.Bytes.ToArray());
        code.Should().NotBeNull("code should exist for activation verification");

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

        ValueHash256 moduleHash = Keccak.Compute(result.Value.WavmModule!);
        string localTarget = StylusTargets.GetLocalTargetName();

        bool hasActivation = _wasmDb.TryGetActivatedAsm(localTarget, moduleHash, out byte[]? activatedAsm);
        hasActivation.Should().BeTrue($"activation for codeHash {codeHash} (moduleHash {moduleHash}) should exist in WASM database");
        activatedAsm.Should().NotBeNull("activated ASM should not be null");
        activatedAsm.Should().NotBeEmpty("activated ASM should not be empty");
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

    private enum ContractType
    {
        Stylus,
        Evm,
        InvalidStylus
    }
}
