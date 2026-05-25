// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Security.Cryptography;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public class DeployTestsContract
{
    // See StylusProgramsTests.cs for details
    private const ulong DefaultArbosVersion = ArbosVersion.Forty;
    private const ulong InitBudget = 110900;
    private static ISpecProvider GetSpecProvider()
        => FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();

    public static (StylusPrograms programs, ArbitrumCodeInfoRepository repository) CreateTestPrograms(IWorldState state, ulong availableGas = InitBudget, ulong arbosVersion = DefaultArbosVersion)
    {
        new ArbitrumInitializeStylusNative(new StylusTargetConfig())
            .Execute(CancellationToken.None).GetAwaiter().GetResult();

        IArbosVersionProvider versionProvider = new TestArbosVersionProvider(arbosVersion);
        ArbitrumCodeInfoRepository repository = new(new EthereumCodeInfoRepository(state), versionProvider);
        TestArbosStorage.TestBurner burner = new(availableGas, null);
        ArbosStorage storage = TestArbosStorage.Create(state, burner: burner);

        StylusPrograms.Initialize(arbosVersion, storage);
        StylusPrograms programs = new(storage, arbosVersion);

        return (programs, repository);
    }

    public static (Address caller, Address rootAddress, IReadOnlyList<Address> fragmentAddresses, BlockHeader block) DeployFragmentedCounterContract(
        IWorldState state,
        ICodeInfoRepository repository,
        int fragmentCount = 2)
    {
        if (fragmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(fragmentCount), "fragmentCount must be at least 1");

        Address caller = new(RandomNumberGenerator.GetBytes(Address.Size));
        state.CreateAccountIfNotExists(caller, balance: 1.Ether, nonce: 0);

        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmResult = StylusNative.WatToWasm(wat);
        if (!wasmResult.IsSuccess)
            throw new InvalidOperationException("Failed to convert WAT to WASM: " + wasmResult.Error);

        byte[] compressed = BrotliCompression.Compress(wasmResult.Value, 1).ToArray();
        ReadOnlySpan<byte> compressedSpan = compressed;

        Address[] fragmentAddresses = new Address[fragmentCount];
        int chunkSize = compressed.Length / fragmentCount;

        for (int i = 0; i < fragmentCount; i++)
        {
            int start = i * chunkSize;
            int end = i == fragmentCount - 1 ? compressed.Length : (i + 1) * chunkSize;
            ReadOnlySpan<byte> chunk = compressedSpan[start..end];

            byte[] fragmentCode = StylusCode.NewStylusFragmentPrefix(chunk);
            Address fragmentAddress = new(RandomNumberGenerator.GetBytes(Address.Size));
            state.CreateAccountIfNotExists(fragmentAddress, balance: 0, nonce: 0);
            repository.InsertCode(fragmentCode, fragmentAddress, GetSpecProvider().GenesisSpec);
            fragmentAddresses[i] = fragmentAddress;
        }

        byte[] rootCode = StylusCode.NewStylusRootPrefix(
            dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary,
            decompressedLength: (uint)wasmResult.Value.Length,
            fragments: fragmentAddresses);

        Address rootAddress = new(RandomNumberGenerator.GetBytes(Address.Size));
        state.CreateAccountIfNotExists(rootAddress, balance: 0, nonce: 0);
        repository.InsertCode(rootCode, rootAddress, GetSpecProvider().GenesisSpec);

        state.Commit(GetSpecProvider().GenesisSpec);
        state.CommitTree(0);

        BlockHeader header = new BlockHeaderBuilder()
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .TestObject;

        return (caller, rootAddress, fragmentAddresses, header);
    }

    public static (Address rootAddress, BlockHeader block) DeployCustomRoot(
        IWorldState state,
        ICodeInfoRepository repository,
        byte dictionary,
        uint declaredDecompressedLength,
        IReadOnlyList<Address> fragmentAddresses)
    {
        byte[] rootCode = StylusCode.NewStylusRootPrefix(dictionary, declaredDecompressedLength, fragmentAddresses.ToArray());
        Address rootAddress = new(RandomNumberGenerator.GetBytes(Address.Size));
        state.CreateAccountIfNotExists(rootAddress, balance: 0, nonce: 0);
        repository.InsertCode(rootCode, rootAddress, GetSpecProvider().GenesisSpec);

        state.Commit(GetSpecProvider().GenesisSpec);
        state.CommitTree(0);

        BlockHeader header = new BlockHeaderBuilder()
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .TestObject;

        return (rootAddress, header);
    }

    public static (Address caller, Address contract, BlockHeader block) DeployCounterContract(IWorldState state, ICodeInfoRepository repository,
        bool compress = true, bool prependStylusPrefix = true)
    {
        Address caller = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address contract = new(RandomNumberGenerator.GetBytes(Address.Size));

        state.CreateAccountIfNotExists(caller, balance: 1.Ether, nonce: 0);
        state.CreateAccountIfNotExists(contract, balance: 0, nonce: 0);

        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmResult = StylusNative.WatToWasm(wat);

        if (!wasmResult.IsSuccess)
            throw new InvalidOperationException("Failed to convert WAT to WASM: " + wasmResult.Error);

        byte[] code = wasmResult.Value;
        if (compress) // Stylus contracts are compressed
            code = BrotliCompression.Compress(code, 1).ToArray();

        if (prependStylusPrefix) // Valid Stylus programs must have the Stylus prefix
            code = [.. StylusCode.NewStylusPrefix(dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary), .. code];

        ValueHash256 codeHash = Keccak.Compute(code);
        repository.InsertCode(code, contract, GetSpecProvider().GenesisSpec);

        state.Commit(GetSpecProvider().GenesisSpec);
        state.CommitTree(0);

        BlockHeader header = new BlockHeaderBuilder()
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .TestObject;

        return (caller, contract, header);
    }

}
