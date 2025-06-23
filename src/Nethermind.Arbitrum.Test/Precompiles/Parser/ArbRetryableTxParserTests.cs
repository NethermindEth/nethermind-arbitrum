
using FluentAssertions;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbRetryableTxParserTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ParsesRedeem_ValidInputData_ReturnsCreatedRetryTxHash()
    {
        // Initialize ArbOS state
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);
        ulong gasSupplied = ulong.MaxValue;

        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState().WithBlockExecutionContext(genesis);

        ulong calldataSize = 65;
        byte[] calldata = new byte[calldataSize];
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired
        Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        ArbRetryableTxTests.ComputeRedeemCost(out ulong gasToDonate, gasSupplied, calldataSize);
        ulong nonce = retryable.NumTries.Get(); // 0
        UInt256 maxRefund = UInt256.MaxValue;

        ArbitrumRetryTx expectedRetryInnerTx = new(
            setupContext.ChainId,
            nonce,
            retryable.From.Get(),
            setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            gasToDonate,
            retryable.To?.Get(),
            retryable.CallValue.Get(),
            retryable.Calldata.Get(),
            ticketIdHash,
            setupContext.Caller!,
            maxRefund,
            0
        );
        var expectedTx = new ArbitrumTransaction<ArbitrumRetryTx>(expectedRetryInnerTx);
        Hash256 expectedTxHash = expectedTx.CalculateHash();

        // Setup context
        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied);
        newContext.WithArbosState().WithBlockExecutionContext(genesis).WithTransactionProcessor();
        newContext.ArbosState.L2PricingState.GasBacklogStorage.Set(System.Math.Min(long.MaxValue, gasToDonate) + 1);
        newContext.TxProcessor.CurrentRetryable = Hash256.Zero;
        // Reset gas for correct retry tx hash computation (context initialization consumes gas)
        newContext.ResetGasLeft();

        // Setup input data
        string redeemMethodId = "0xeda1122c";
        string ticketIdStrWithoutOx = ticketIdHash.ToString(false);
        byte[] inputData = Bytes.FromHexString($"{redeemMethodId}{ticketIdStrWithoutOx}");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(newContext, inputData);

        result.Should().BeEquivalentTo(expectedTxHash.BytesToArray());
    }

    [Test]
    public void ParsesRedeem_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        byte[] redeemMethodId = Bytes.FromHexString("0xeda1122c");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[redeemMethodId.Length + Keccak.Size - 1];
        redeemMethodId.CopyTo(invalidInputData);

        ArbRetryableTxParser arbRetryableTxParser = new();
        PrecompileTestContextBuilder context = new(worldState, 0);

        byte[] invalidInputDataBytes = invalidInputData.ToArray();
        Action action = () => arbRetryableTxParser.RunAdvanced(context, invalidInputDataBytes);
        action.Should().Throw<EndOfStreamException>();
    }
}
