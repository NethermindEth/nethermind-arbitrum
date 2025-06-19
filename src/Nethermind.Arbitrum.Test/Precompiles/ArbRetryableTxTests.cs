using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Tracing;
using Nethermind.Core.Crypto;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos.Storage;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class ArbRetryableTxTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void TicketCreated_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "TicketCreated(bytes32)";
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash };
        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

        ulong gasSupplied = GasCostOf.Log + GasCostOf.LogTopic * (ulong)expectedEventTopics.Length + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.TicketCreated(context, ticketIdHash);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.TicketCreated should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void RedeemScheduled_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "RedeemScheduled(bytes32,bytes32,uint64,uint64,address,uint256,uint256)";

        // Construct event topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash256 = new(ticketId.ToBigEndian());
        UInt256 retryTxHash = 456;
        Hash256 retryTxHash256 = new(retryTxHash.ToBigEndian());
        UInt256 sequenceNum = 1;
        Hash256 sequenceNumHash256 = new(sequenceNum.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash256, retryTxHash256, sequenceNumHash256 };

        // Construct event data
        ulong donatedGas = 1;
        Address donor = Address.Zero;
        UInt256 maxRefund = 2;
        UInt256 submissionFeeRefund = 3;
        object[] data = new object[] { donatedGas, donor, maxRefund, submissionFeeRefund };
        byte[] eventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, new[] { AbiUInt.UInt64, AbiAddress.Instance, AbiUInt.UInt256, AbiUInt.UInt256 }),
            data);

        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, eventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)eventData.Length + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.RedeemScheduled(
            context, ticketIdHash256, retryTxHash256, (ulong)sequenceNum, donatedGas, donor, maxRefund, submissionFeeRefund
        );

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.RedeemScheduled should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void LifetimeExtended_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "LifetimeExtended(bytes32,uint256)";

        // topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash };

        // data
        UInt256 newTimeout = 456;
        object[] data = { newTimeout };
        byte[] eventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, new[] { AbiUInt.UInt256 }),
            data);

        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, eventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)eventData.Length + 1;

        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.LifetimeExtended(context, ticketIdHash, newTimeout);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.LifetimeExtended should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void Canceled_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "Canceled(bytes32)";

        // topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash };
        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

        ulong gasSupplied = GasCostOf.Log + GasCostOf.LogTopic * (ulong)expectedEventTopics.Length + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.Canceled(context, ticketIdHash);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.Canceled should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    private void CreateRetryable(RetryableState retryableState, Hash256 ticketId)
    {
        // 
    }
}
