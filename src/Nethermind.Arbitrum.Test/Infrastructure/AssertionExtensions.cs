using FluentAssertions;
using FluentAssertions.Equivalency;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class AssertionExtensions
{
    public static EquivalencyAssertionOptions<Transaction> ForTransaction(this EquivalencyAssertionOptions<Transaction> options)
    {
        return options
            .Using<Memory<byte>>(context => context.Subject.ToArray().Should().BeEquivalentTo(context.Expectation.ToArray())).WhenTypeIs<Memory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumContractTx> ForArbitrumContractTx(this EquivalencyAssertionOptions<ArbitrumContractTx> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context => context.Subject.ToArray().Should().BeEquivalentTo(context.Expectation.ToArray()))
            .WhenTypeIs<ReadOnlyMemory<byte>>();
    }
}
