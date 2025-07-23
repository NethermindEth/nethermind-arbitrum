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
            .Using<ReadOnlyMemory<byte>>(context => context.Subject.ToArray().Should().BeEquivalentTo(context.Expectation.ToArray())).WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumContractTx> ForArbitrumContractTx(this EquivalencyAssertionOptions<ArbitrumContractTx> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context => context.Subject.ToArray().Should().BeEquivalentTo(context.Expectation.ToArray()))
            .WhenTypeIs<ReadOnlyMemory<byte>>();
    }

    public static EquivalencyAssertionOptions<ArbitrumTransaction<T>> ForTransaction<T>(this EquivalencyAssertionOptions<ArbitrumTransaction<T>> options)
        where T : IArbitrumTransactionData
    {
        return options
<<<<<<< HEAD
            .Using<Memory<byte>>(context => context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<Memory<byte>>()
            .Using<ReadOnlyMemory<byte>>(context => context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>();
=======
            .Using<Memory<byte>>(context => context.Subject.ToArray().Should().BeEquivalentTo(context.Expectation.ToArray())).WhenTypeIs<Memory<byte>>()
            .Using<ReadOnlyMemory<byte>>(context => context.Subject.ToArray().Should().BeEquivalentTo(context.Expectation.ToArray())).WhenTypeIs<ReadOnlyMemory<byte>>();
>>>>>>> 9a86282 (PR Review comments)
    }
}
