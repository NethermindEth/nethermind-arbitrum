using System.Security.Cryptography;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Arbos.Stylus;
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

    public static (StylusPrograms programs, ArbitrumCodeInfoRepository repository) CreateTestPrograms(IWorldState state, ulong availableGas = InitBudget)
    {
        new ArbitrumInitializeStylusNative(new StylusTargetConfig())
            .Execute(CancellationToken.None).GetAwaiter().GetResult();

        ArbitrumCodeInfoRepository repository = new(new EthereumCodeInfoRepository(state));
        TestArbosStorage.TestBurner burner = new(availableGas, null);
        ArbosStorage storage = TestArbosStorage.Create(state, burner: burner);

        StylusPrograms.Initialize(DefaultArbosVersion, storage);
        StylusPrograms programs = new(storage, DefaultArbosVersion);

        return (programs, repository);
    }

    public static (Address caller, Address contract, BlockHeader block) DeployCounterContract(IWorldState state, ICodeInfoRepository repository,
        bool compress = true, bool prependStylusPrefix = true)
    {
        Address caller = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address contract = new(RandomNumberGenerator.GetBytes(Address.Size));

        state.CreateAccountIfNotExists(caller, balance: 1.Ether(), nonce: 0);
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
