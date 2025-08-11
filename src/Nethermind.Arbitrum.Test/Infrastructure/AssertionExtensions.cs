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
            .Using<Memory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<Memory<byte>>()
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<T> ForArbitrumTransaction<T>(this EquivalencyAssertionOptions<T> options)
        where T : ArbitrumTransaction
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }
}
