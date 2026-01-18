using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Primitives;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.JsonRpc;
using static Nethermind.Arbitrum.Arbos.Programs.StylusPrograms;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class AssertionExtensions
{
    public static EquivalencyAssertionOptions<ArbitrumPrecompileException> ForArbitrumPrecompileException(
    this EquivalencyAssertionOptions<ArbitrumPrecompileException> options)
    {
        return options
            .Excluding(e => e.Message) // Ignore Message property as not really relevant for implementation
            .Excluding(e => e.StackTrace)
            .Excluding(e => e.TargetSite)
            .Excluding(e => e.Source)
            .Excluding(e => e.HResult)
            .Excluding(e => e.HelpLink)
            .Excluding(e => e.Data);
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

    public static EquivalencyAssertionOptions<StylusOperationError?> ForStylusOperationError(this EquivalencyAssertionOptions<StylusOperationError?> options)
    {
        return options
            .Using<string>(context => context.Subject.StartsWith(context.Expectation).Should().BeTrue())
            .WhenTypeIs<string>();
    }
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

    public static ResultWrapperAssertions<T> Should<T>(this ResultWrapper<T> instance)
    {
        return new ResultWrapperAssertions<T>(instance);
    }

    public static ResultWrapperAssertions<T> ShouldAsync<T>(this Task<ResultWrapper<T>> instance)
    {
        return new ResultWrapperAssertions<T>(instance.GetAwaiter().GetResult());
    }
}

public class ResultWrapperAssertions<T>(ResultWrapper<T> instance)
    : ReferenceTypeAssertions<ResultWrapper<T>, ResultWrapperAssertions<T>>(instance)
{
    protected override string Identifier => nameof(ResultWrapperAssertions<>);

    [CustomAssertion]
    public AndConstraint<ResultWrapperAssertions<T>> BeEquivalentTo(ResultWrapper<T> expectation, string because = "", params object[] becauseArgs)
    {
        new ObjectAssertions(Subject).BeEquivalentTo(expectation, because, becauseArgs); // Use ObjectAssertions to avoid infinite recursion
        return new AndConstraint<ResultWrapperAssertions<T>>(this);
    }

    [CustomAssertion]
    public AndConstraint<ResultWrapperAssertions<T>> RequestSucceed(string because = "", params object[] becauseArgs)
    {
        Subject.Result.Should().Be(Result.Success, because, becauseArgs);
        return new AndConstraint<ResultWrapperAssertions<T>>(this);
    }

    [CustomAssertion]
    public AndConstraint<ResultWrapperAssertions<T>> TransactionStatusesBe(ArbitrumRpcTestBlockchain chain, byte[] statuses, string because = "", params object[] becauseArgs)
    {
        chain.LatestReceiptStatuses().Should().BeEquivalentTo(statuses, because, becauseArgs);
        return new AndConstraint<ResultWrapperAssertions<T>>(this);
    }
}
