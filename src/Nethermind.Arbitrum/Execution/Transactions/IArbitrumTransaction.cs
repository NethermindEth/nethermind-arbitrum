namespace Nethermind.Arbitrum.Execution.Transactions;

public interface IArbitrumTransaction
{
    IArbitrumTransactionData GetInner();
}

public interface IArbitrumTransactionData;
