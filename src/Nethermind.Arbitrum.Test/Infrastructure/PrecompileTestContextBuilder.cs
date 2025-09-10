using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public record PrecompileTestContextBuilder(IWorldState WorldState, ulong GasSupplied)
    : ArbitrumPrecompileExecutionContext(Address.Zero, UInt256.Zero, GasSupplied, false, WorldState, new BlockExecutionContext(), 0, null)
{
    public PrecompileTestContextBuilder WithArbosState()
    {
        ArbosState = ArbosState.OpenArbosState(WorldState, this, LimboLogs.Instance.GetClassLogger());
        FreeArbosState = ArbosState.OpenArbosState(WorldState, new ZeroGasBurner(), LimboLogs.Instance.GetClassLogger());
        return this;
    }

    public PrecompileTestContextBuilder WithBlockExecutionContext(BlockHeader blockHeader)
    {
        BlockExecutionContext = new BlockExecutionContext(blockHeader, London.Instance);
        return this;
    }

    public PrecompileTestContextBuilder WithReleaseSpec()
    {
        ReleaseSpec = London.Instance;
        return this;
    }

    public PrecompileTestContextBuilder WithCaller(Address caller)
    {
        Caller = caller;
        return this;
    }

    public void ResetGasLeft(ulong gasLeft = 0)
    {
        GasLeft = gasLeft == 0 ? GasSupplied : gasLeft;
    }

    public PrecompileTestContextBuilder WithArbosVersion(ulong version)
    {
        PrecompileTestContextBuilder context = this;
        if (FreeArbosState == null || ArbosState == null)
        {
            context = WithArbosState();
        }
        context.SetCurrentArbosVersion(version);
        return context;
    }

    public PrecompileTestContextBuilder WithBlockNumber(long blockNumber)
    {
        // Create a default block header if none exists
        BlockHeader currentHeader = BlockExecutionContext.Header ?? new BlockHeader(
            TestItem.KeccakA,
            Keccak.OfAnEmptySequenceRlp,
            TestItem.AddressA,
            UInt256.One,
            100,
            30_000_000,
            1700000000,
            []
        );

        BlockHeader newHeader = new(
            currentHeader.ParentHash!,
            currentHeader.UnclesHash!,
            currentHeader.Beneficiary!,
            currentHeader.Difficulty,
            blockNumber,
            currentHeader.GasLimit,
            currentHeader.Timestamp,
            currentHeader.ExtraData
        );
        return this with { BlockExecutionContext = new(newHeader, ReleaseSpec) };
    }

    public PrecompileTestContextBuilder WithCallDepth(int depth)
    {
        return this with { CallDepth = depth };
    }

    public PrecompileTestContextBuilder WithOrigin(ValueHash256 origin)
    {
        return this with { Origin = origin };
    }

    public PrecompileTestContextBuilder WithGrandCaller(Address grandCaller)
    {
        return this with { GrandCaller = grandCaller };
    }

    public PrecompileTestContextBuilder WithValue(UInt256 value)
    {
        return this with { Value = value };
    }

    public PrecompileTestContextBuilder WithBlockHashProvider(IBlockhashProvider provider)
    {
        return this with { BlockHashProvider = provider };
    }

    public PrecompileTestContextBuilder WithTopLevelTxType(ArbitrumTxType txType)
    {
        return this with { TopLevelTxType = txType };
    }

    public PrecompileTestContextBuilder WithPosterFee(UInt256 posterFee)
    {
        return this with { PosterFee = posterFee };
    }

    public PrecompileTestContextBuilder WithNativeTokenOwners(params Address[] owners)
    {
        PrecompileTestContextBuilder context = this;
        if (FreeArbosState == null || ArbosState == null)
        {
            context = context.WithArbosState();
        }

        foreach (Address owner in owners)
        {
            context.FreeArbosState.NativeTokenOwners.Add(owner);
        }

        return context;
    }

    public PrecompileTestContextBuilder WithChainId(ulong chainId)
    {
        return this with { ChainId = chainId };
    }

    public PrecompileTestContextBuilder WithGasSupplied(ulong gasSupplied)
    {
        return this with { GasSupplied = gasSupplied, GasLeft = gasSupplied };
    }

    // Helper method to create a test block hash provider
    public static IBlockhashProvider CreateTestBlockHashProvider(params (long blockNumber, Hash256 hash)[] blockHashes)
    {
        TestBlockhashProvider provider = new();
        foreach ((long blockNumber, Hash256 hash) in blockHashes)
        {
            provider.SetBlockhash(blockNumber, hash);
        }
        return provider;
    }

    // Test helper to create a mock blockhash provider
    private class TestBlockhashProvider : IBlockhashProvider
    {
        private readonly Dictionary<long, Hash256> _blockHashes = new();

        public void SetBlockhash(long blockNumber, Hash256 hash)
        {
            _blockHashes[blockNumber] = hash;
        }

        public Hash256? GetBlockhash(BlockHeader currentBlock, long number)
        {
            return _blockHashes.TryGetValue(number, out Hash256? hash) ? hash : null;
        }

        public Hash256? GetBlockhash(BlockHeader currentBlock, long number, IReleaseSpec? spec)
        {
            return _blockHashes.TryGetValue(number, out Hash256? hash) ? hash : null;
        }
    }

}
