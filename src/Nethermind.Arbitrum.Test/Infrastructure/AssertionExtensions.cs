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

    public static EquivalencyAssertionOptions<ArbitrumRetryTransaction> ForArbitrumRetryTransaction(
        this EquivalencyAssertionOptions<ArbitrumRetryTransaction> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumSubmitRetryableTransaction> ForArbitrumSubmitRetryableTransaction(
        this EquivalencyAssertionOptions<ArbitrumSubmitRetryableTransaction> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumDepositTransaction> ForArbitrumDepositTransaction(
        this EquivalencyAssertionOptions<ArbitrumDepositTransaction> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumInternalTransaction> ForArbitrumInternalTransaction(
        this EquivalencyAssertionOptions<ArbitrumInternalTransaction> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumUnsignedTransaction> ForArbitrumUnsignedTransaction(
        this EquivalencyAssertionOptions<ArbitrumUnsignedTransaction> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }

    public static EquivalencyAssertionOptions<ArbitrumContractTransaction> ForArbitrumContractTransaction(
        this EquivalencyAssertionOptions<ArbitrumContractTransaction> options)
    {
        return options
            .Using<ReadOnlyMemory<byte>>(context =>
                context.Subject.Span.SequenceEqual(context.Expectation.Span).Should().BeTrue())
            .WhenTypeIs<ReadOnlyMemory<byte>>()
            .Excluding(t => t.Hash);
    }
}
