using System.Text;
using System.Text.Json;
using Autofac;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class ArbitrumRpcTestBlockchain : ArbitrumTestBlockchainBase
{
    private ulong _genesisBlockNumber;
    private ulong _latestL1BlockNumber;
    private ulong _latestL2BlockIndex;
    private ulong _latestDelayedMessagesRead;

    private ArbitrumRpcTestBlockchain(ChainSpec chainSpec) : base(chainSpec)
    {
    }

    public IArbitrumRpcModule ArbitrumRpcModule { get; private set; } = null!;
    public IArbitrumSpecHelper SpecHelper => Dependencies.SpecHelper;

    public ulong GenesisBlockNumber => _genesisBlockNumber;
    public ulong LatestL1BlockNumber => _latestL1BlockNumber;
    public ulong LatestL2BlockNumber => _genesisBlockNumber + _latestL2BlockIndex;
    public ulong LatestL2BlockIndex => _latestL2BlockIndex;
    public ulong LatestDelayedMessagesRead => _latestDelayedMessagesRead;

    public static ArbitrumRpcTestBlockchain CreateDefault(Action<ContainerBuilder>? configurer = null, ChainSpec? chainSpec = null)
    {
        return CreateInternal(new ArbitrumRpcTestBlockchain(chainSpec ?? FullChainSimulationChainSpecProvider.Create()), configurer);
    }

    public async Task<ResultWrapper<MessageResult>> Digest(TestEthDeposit deposit)
    {
        ArbitrumDepositTransaction transaction = new()
        {
            SourceHash = deposit.RequestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = UInt256.Zero,
            GasLimit = 0,
            IsOPSystemTransaction = false,
            Mint = deposit.Value,

            ChainId = ChainSpec.ChainId,
            L1RequestId = deposit.RequestId,
            Value = deposit.Value,
            SenderAddress = deposit.Sender,
            To = deposit.Receiver
        };

        DigestMessageParameters parameters = CreateDigestMessage(ArbitrumL1MessageKind.EthDeposit, deposit.RequestId, deposit.L1BaseFee, deposit.Sender, transaction);

        return await ArbitrumRpcModule.DigestMessage(parameters);
    }

    public async Task<ResultWrapper<MessageResult>> Digest(TestSubmitRetryable retryable)
    {
        ArbitrumSubmitRetryableTransaction transaction = new()
        {
            SourceHash = retryable.RequestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = retryable.GasFee,
            GasLimit = (long)retryable.GasLimit,
            Value = 0, // Tx value is 0, L2 execution value is in RetryValue
            Data = retryable.RetryData,
            IsOPSystemTransaction = false,
            Mint = retryable.DepositValue,

            ChainId = ChainSpec.ChainId,
            RequestId = retryable.RequestId,
            SenderAddress = retryable.Sender,
            L1BaseFee = retryable.L1BaseFee,
            DepositValue = retryable.DepositValue,
            GasFeeCap = retryable.GasFee,
            Gas = retryable.GasLimit,
            RetryTo = retryable.Receiver,
            RetryValue = retryable.RetryValue,
            Beneficiary = retryable.Beneficiary,
            MaxSubmissionFee = retryable.MaxSubmissionFee,
            FeeRefundAddr = retryable.Beneficiary,
            RetryData = retryable.RetryData
        };

        DigestMessageParameters parameters = CreateDigestMessage(ArbitrumL1MessageKind.SubmitRetryable, retryable.RequestId, retryable.L1BaseFee,
            retryable.Sender, transaction);

        return await ArbitrumRpcModule.DigestMessage(parameters);
    }

    public async Task<ResultWrapper<MessageResult>> Digest(TestL2FundedByL1Transfer message)
    {
        ArbitrumDepositTransaction deposit = new()
        {
            SourceHash = message.RequestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = UInt256.Zero,
            GasLimit = 0,
            IsOPSystemTransaction = false,
            Mint = message.DepositValue,

            ChainId = ChainSpec.ChainId,
            L1RequestId = message.RequestId,
            Value = message.DepositValue,
            SenderAddress = message.Sponsor,
            To = message.Sender
        };

        ArbitrumUnsignedTransaction unsigned = new()
        {
            ChainId = ChainSpec.ChainId,
            SenderAddress = message.Sender,
            Nonce = message.Nonce,
            DecodedMaxFeePerGas = message.MaxFeePerGas,
            GasFeeCap = message.MaxFeePerGas,
            GasLimit = (long)message.GasLimit,
            Gas = message.GasLimit,
            To = message.Receiver,
            Value = message.TransferValue,
            Data = Array.Empty<byte>()
        };

        DigestMessageParameters parameters = CreateDigestMessage(ArbitrumL1MessageKind.L2FundedByL1, message.RequestId, message.L1BaseFee, message.Sponsor,
            deposit, unsigned);

        return await ArbitrumRpcModule.DigestMessage(parameters);
    }

    public async Task<ResultWrapper<MessageResult>> Digest(TestL2FundedByL1Contract message)
    {
        ArbitrumDepositTransaction deposit = new()
        {
            SourceHash = message.RequestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = UInt256.Zero,
            GasLimit = 0,
            IsOPSystemTransaction = false,
            Mint = message.DepositValue,

            ChainId = ChainSpec.ChainId,
            L1RequestId = message.RequestId,
            Value = message.DepositValue,
            SenderAddress = message.Sponsor,
            To = message.Sender
        };

        ArbitrumUnsignedTransaction unsigned = new()
        {
            ChainId = ChainSpec.ChainId,
            SenderAddress = message.Sender,
            Nonce = 0,
            DecodedMaxFeePerGas = message.MaxFeePerGas,
            GasFeeCap = message.MaxFeePerGas,
            GasLimit = (long)message.GasLimit,
            Gas = message.GasLimit,
            To = message.Contract,
            Value = message.TransferValue,
            Data = message.Data
        };

        DigestMessageParameters parameters = CreateDigestMessage(ArbitrumL1MessageKind.L2FundedByL1, message.RequestId, message.L1BaseFee, message.Sponsor,
            deposit, unsigned);

        return await ArbitrumRpcModule.DigestMessage(parameters);
    }

    public async Task<ResultWrapper<MessageResult>> Digest(TestL2Transactions message)
    {
        DigestMessageParameters parameters = CreateDigestMessage(ArbitrumL1MessageKind.L2Message, message.RequestId, message.L1BaseFee,
            message.Sender, message.Transactions);

        return await ArbitrumRpcModule.DigestMessage(parameters);
    }

    public void DumpBlocks()
    {
        List<Block> blocks = new();
        Block? current = BlockTree.Head;
        while (current != null)
        {
            blocks.Add(current);
            current = current.ParentHash is not null ? BlockTree.FindBlock(current.ParentHash) : null;
        }

        blocks.Reverse();

        StringBuilder sb = new();
        foreach (Block block in blocks)
            sb.Append(block.ToString(Block.Format.Full));

        Console.WriteLine("\n\n# Chain blocks:\n");
        Console.WriteLine(sb.ToString());
    }

    private static ArbitrumRpcTestBlockchain CreateInternal(ArbitrumRpcTestBlockchain chain, Action<ContainerBuilder>? configurer)
    {
        chain.Build(configurer);

        chain.ArbitrumRpcModule = new ArbitrumRpcModuleWrapper(chain, new ArbitrumRpcModuleFactory(
                chain.Container.Resolve<ArbitrumBlockTreeInitializer>(),
                chain.BlockTree,
                chain.BlockProductionTrigger,
                chain.ArbitrumRpcTxSource,
                chain.ChainSpec,
                chain.Dependencies.SpecHelper,
                chain.LogManager,
                chain.Dependencies.CachedL1PriceData,
                chain.BlockProcessingQueue,
                chain.Container.Resolve<IArbitrumConfig>())
            .Create());

        return chain;
    }

    private DigestMessageParameters CreateDigestMessage(ArbitrumL1MessageKind kind, Hash256 requestId, UInt256 l1BaseFee, Address sender, params Transaction[] transactions)
    {
        L1IncomingMessageHeader header = new(kind, sender, _latestL1BlockNumber + 1, (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            requestId, l1BaseFee);

        byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions(transactions, header);
        MessageWithMetadata messageWithMetadata = new(new L1IncomingMessage(header, l2Msg, null), _latestDelayedMessagesRead);
        DigestMessageParameters parameters = new(_latestL2BlockIndex + 1, messageWithMetadata, null);

        return parameters;
    }

    private class ArbitrumRpcModuleWrapper(ArbitrumRpcTestBlockchain chain, IArbitrumRpcModule rpc) : IArbitrumRpcModule
    {
        public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
        {
            try
            {
                Utf8JsonReader jsonReader = new(message.SerializedChainConfig!);
                ChainConfig chainConfig = chain.JsonSerializer.Deserialize<ChainConfig>(ref jsonReader);

                chain._genesisBlockNumber = chainConfig.ArbitrumChainParams.GenesisBlockNum;
            }
            catch (Exception e)
            {
                // Swallow exception as broken message can be a part of the test
            }

            return rpc.DigestInitMessage(message);
        }

        public Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        {
            chain._latestL1BlockNumber = System.Math.Max(chain._latestL1BlockNumber, parameters.Message.Message.Header.BlockNumber);
            chain._latestL2BlockIndex = System.Math.Max(chain._latestL2BlockIndex, parameters.Number);
            chain._latestDelayedMessagesRead = System.Math.Max(chain._latestDelayedMessagesRead, parameters.Message.DelayedMessagesRead);
            return rpc.DigestMessage(parameters);
        }

        public Task<ResultWrapper<MessageResult>> ResultAtPos(ulong messageIndex)
        {
            return rpc.ResultAtPos(messageIndex);
        }

        public Task<ResultWrapper<ulong>> HeadMessageNumber()
        {
            return rpc.HeadMessageNumber();
        }

        public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
        {
            return rpc.MessageIndexToBlockNumber(messageIndex);
        }

        public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
        {
            return rpc.BlockNumberToMessageIndex(blockNumber);
        }

        public ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters)
        {
            return rpc.SetFinalityData(parameters);
        }

        public ResultWrapper<string> MarkFeedStart(ulong to)
        {
            return rpc.MarkFeedStart(to);
        }
    }
}

public record TestEthDeposit(Hash256 RequestId, UInt256 L1BaseFee, Address Sender, Address Receiver, UInt256 Value);

public record TestSubmitRetryable(Hash256 RequestId, UInt256 L1BaseFee, Address Sender, Address Receiver, Address Beneficiary, UInt256 DepositValue,
    UInt256 RetryValue, UInt256 GasFee, ulong GasLimit, UInt256 MaxSubmissionFee)
{
    public byte[] RetryData { get; set; } = [];
}

public record TestL2FundedByL1Transfer(Hash256 RequestId, UInt256 L1BaseFee, Address Sponsor, Address Sender, Address Receiver,
    UInt256 DepositValue, UInt256 TransferValue, UInt256 MaxFeePerGas, ulong GasLimit, UInt256 Nonce);

public record TestL2FundedByL1Contract(Hash256 RequestId, UInt256 L1BaseFee, Address Sponsor, Address Sender, Address Contract,
    UInt256 DepositValue, UInt256 TransferValue, UInt256 MaxFeePerGas, ulong GasLimit, byte[] Data);

public record TestL2Transactions(Hash256 RequestId, UInt256 L1BaseFee, Address Sender, params Transaction[] Transactions);
