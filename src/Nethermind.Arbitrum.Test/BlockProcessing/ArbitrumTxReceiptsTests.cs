using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.BlockProcessing;

public class ArbitrumTxReceiptsTests
{
    private static readonly UInt256 _baseFeePerGas = 1_000;

    [Test]
    public void BlockProcessing_TransactionProcessed_ReceiptHasProperGasUsedForL1()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBase.CreateTestBlockchainWithGenesis();

        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000)
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockToProduce block = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx, _baseFeePerGas);
        ArbitrumTxReceipt receipt = (ArbitrumTxReceipt)BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block)[1];

        ulong callDataUnits = BlockProcessingUtilities.GetCallDataUnits(chain.WorldStateManager.GlobalWorldState, transferTx);
        ulong posterGas = GetPosterGas(chain.WorldStateManager.GlobalWorldState, _baseFeePerGas, callDataUnits);

        receipt.GasUsedForL1.Should().Be(posterGas);
    }

    private static ulong GetPosterGas(IWorldState worldState, UInt256 baseFeePerGas, ulong calldataUnits)
    {
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));

        UInt256 pricePerUnit = arbosState.L1PricingState.PricePerUnitStorage.Get();
        UInt256 posterCost = pricePerUnit * calldataUnits;
        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe();

        return posterGas;
    }
}
