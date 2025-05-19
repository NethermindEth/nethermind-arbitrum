using FluentAssertions;
using FluentAssertions.Equivalency;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System;

namespace Nethermind.Arbitrum.Tests.Rpc.DigestMessage;

public class NitroNitroL2MessageParserTests
{
    private const int ChainId = 1;

    [Test]
    public void Parse_SubmitRetryable_ParsesCorrectly()
    {
        var message = new L1IncomingMessage(
            new(
                ArbitrumL1MessageKind.SubmitRetryable,
                new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                117,
                1745999206,
                new Hash256("0x0000000000000000000000000000000000000000000000000000000000000001"),
                295),
            "AAAAAAAAAAAAAAAAP6sYRiLcGbYQk0m5SBFJO/KkU2IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAI4byb8EAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAjmgvhUZ1IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGTUgAAAAAAAAAAAAAAACTtMEUtA7PH8NHRUAKG5uRFcNOQgAAAAAAAAAAAAAAAJO0wRS0Ds8fw0dFQAobm5EVw05CAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            null);

        var transaction = (ArbitrumTransaction<ArbitrumSubmitRetryableTx>)NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

        transaction.Inner.Should().BeEquivalentTo(new ArbitrumSubmitRetryableTx(
            ChainId,
            new("0x0000000000000000000000000000000000000000000000000000000000000001"),
            new("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
            295,
            10021000000413000,
            1000000000,
            21000,
            new("0x3fAB184622Dc19b6109349B94811493BF2a45362"),
            10000000000000000,
            new("0x93B4c114B40ECf1Fc34745400a1b9B9115c34E42"),
            413000,
            new("0x93B4c114B40ECf1Fc34745400a1b9B9115c34E42"),
            Array.Empty<byte>()));
    }

    [Test]
    public void Parse_L2Message_EthLegacy_ParsesCorrectly()
    {
        var message = new L1IncomingMessage(
            new(
                ArbitrumL1MessageKind.L2Message,
                new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                117,
                1745999206,
                new Hash256("0x0000000000000000000000000000000000000000000000000000000000000002"),
                295),
            "BPilgIUXSHboAIMBhqCAgLhTYEWAYA5gADmAYADzUP5//////////////////////////////////////////+A2AWAAgWAggjeANYKCNPWAFRVgOVeBgv1bgIJSUFBQYBRgDPMboCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIioCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIi",
            null);

        var transaction = NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

        transaction.Should().BeEquivalentTo(new Transaction
        {
            Type = TxType.Legacy,
            Nonce = 0,
            GasPrice = 100000000000,
            GasLimit = 100000,
            To = null,
            Value = 0,
            Data = Convert.FromHexString(
                "604580600e600039806000f350fe7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf3"),
            Signature = new(
                UInt256.Parse("15438945231642159389809464667825054380435997955418741871927677867721750618658"),
                UInt256.Parse("15438945231642159389809464667825054380435997955418741871927677867721750618658"),
                27)
        }, o => o.ForTransaction());
    }

    [Test]
    public void Parse_Deposit_ParsesCorrectly()
    {
        var message = new L1IncomingMessage(
            new(
                ArbitrumL1MessageKind.EthDeposit,
                new Address("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
                165,
                1745999255,
                new Hash256("0x0000000000000000000000000000000000000000000000000000000000000009"),
                8),
            "Px6ufUbYjwj8L47Sf8sqsYPrLQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAFS0Cx+FK9oAAAA==",
            null);

        var transaction = (ArbitrumTransaction<ArbitrumDepositTx>)NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

        transaction.Inner.Should().BeEquivalentTo(new ArbitrumDepositTx(
            ChainId,
            new("0x0000000000000000000000000000000000000000000000000000000000000009"),
            new("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
            new("0x3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E"),
            UInt256.Parse("100000000000000000000000")));
    }

    [Test]
    public void Parse_L2Message_DynamicFeeTx_ParsesCorrectly()
    {
        var message = new L1IncomingMessage(
            new(
                ArbitrumL1MessageKind.L2Message,
                new Address("0xA4b000000000000000000073657175656e636572"),
                166,
                1745999257,
                null,
                8),
            "BAL4doMGSrqAhFloLwCEZVPxAIJSCJReFJfdHwjIey2P4j6aq2wd6DPZJ4kFa8deLWMQAACAwICgTJ7ERDhsUJoSmXYhVhdHIN5YgHJ2PBS1e9YImp0iAfmgTkKAGg0ukQ/BHPiMnbTpFqIuHlSBgQff7dPFFlMlhP4=",
            null);

        var transaction = NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

        transaction.Should().BeEquivalentTo(new Transaction
        {
            ChainId = 412346,
            Type = TxType.EIP1559,
            Nonce = 0,
            GasPrice = 1500000000, // DynamicFeeTx.GasTipCap
            DecodedMaxFeePerGas = 1700000000, // DynamicFeeTx.GasFeeCap
            GasLimit = 21000,
            To = new("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            Value = UInt256.Parse("100000000000000000000"),
            Data = Array.Empty<byte>(),
            Signature = new(
                UInt256.Parse("34656292910065621035852780818211523586495092995652367972786234253091016933881"),
                UInt256.Parse("35397898221649370395961710411641180996206548691370223704696374300050614224126"),
                27)
        }, o => o.ForTransaction());
    }

    [Test]
    public void Parse_Internal_ParsesCorrectly()
    {
        var message = new L1IncomingMessage(
            new(
                ArbitrumL1MessageKind.BatchPostingReport,
                new Address("0xe2148eE53c0755215Df69b2616E552154EdC584f"),
                185,
                1745999275,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                8),
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgR1aviFI7lPAdVIV32myYW5VIVTtxYTy77YI0r5OtTqBq17j1Lv4FmDlUUIb5DT9toNVdVxepdAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAA",
            148376);

        var transaction = (ArbitrumTransaction<ArbitrumInternalTx>)NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

        transaction.Inner.Should().BeEquivalentTo(new ArbitrumInternalTx(
            ChainId,
            1745999275,
            new("0xe2148ee53c0755215df69b2616e552154edc584f"),
            1,
            148376,
            8));
    }

    [Test]
    public void Parse_L2FundedByL1_Contract_ParsesCorrectly()
    {
        var message = new L1IncomingMessage(
            new(
                ArbitrumL1MessageKind.L2FundedByL1,
                new Address("0x502fae7d46d88f08fc2f8ed27fcb2ab183eb3e1f"),
                194,
                1746443431,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000b"),
                8),
            "AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABMLAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACPDRgAAAAAAAAAAAAAAAAARtX/jSFhPBC5DbGv3w8Pe8XHeSQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA0J3gig==",
            null);

        var transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, new());
        var deposit = (ArbitrumTransaction<ArbitrumDepositTx>)transactions[0];
        var contract = (ArbitrumTransaction<ArbitrumContractTx>)transactions[1];

        deposit.Inner.Should().BeEquivalentTo(new ArbitrumDepositTx(
            ChainId,
            new("0x9115655cbcdb654012cf1b2f7e5dbf11c9ef14e152a19d5f8ea75a329092d5a6"),
            new("0x0000000000000000000000000000000000000000"),
            new("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
            UInt256.Zero));
        contract.Inner.Should().BeEquivalentTo(new ArbitrumContractTx(
            ChainId,
            new("0xfc80cd5fe514767bc6e66ec558e68a5429ea70b50fa6caa3b53fc9278e918632"),
            new("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
            600000000,
            312000,
            new("0x11B57FE348584f042E436c6Bf7c3c3deF171de49"),
            UInt256.Zero,
            Convert.FromHexString("d09de08a")), o => o.ForArbitrumContractTx());
    }

    [Test]
    public void Parse_L1Initialize_ParsesCorrectly()
    {
        ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba01000000000000000000000000000000000000000000000000000000000000009a7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d");

        ParsedInitMessage result = NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);

        var json = JsonConvert.SerializeObject(result);

        byte[] expectedSerializedConfig = Convert.FromHexString("7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d");

        ChainConfig expectedChainConfig = new ChainConfig
            {
                ChainId = 412346,
                HomesteadBlock = 0,
                DaoForkSupport = true,
                Eip150Block = 0,
                Eip155Block = 0,
                Eip158Block = 0,
                ByzantiumBlock = 0,
                ConstantinopleBlock = 0,
                PetersburgBlock = 0,
                IstanbulBlock = 0,
                MuirGlacierBlock = 0,
                BerlinBlock = 0,
                LondonBlock = 0,
                TerminalTotalDifficultyPassed = false,
                Clique = new CliqueConfigDTO
                {
                    Period = 0,
                    Epoch = 0
                },
                ArbitrumChainParams = new ArbitrumConfig
                {
                    Enabled = true,
                    AllowDebugPrecompiles = true,
                    DataAvailabilityCommittee = false,
                    InitialArbOSVersion = 32,
                    InitialChainOwner = new("0x5e1497dd1f08c87b2d8fe23e9aab6c1de833d927"),
                    GenesisBlockNum = 0,
                    MaxCodeSize = 0,
                    MaxInitCodeSize = 0
                }
            };

        ParsedInitMessage expectedResult = new(
            chainId: 412346,
            initialBaseFee: 154,
            chainConfigSpec: expectedChainConfig,
            serializedChainConfig: expectedSerializedConfig
        );

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void Parse_L1InitializeWithoutChainConfig_ParsesCorrectly()
    {
        ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba");
        ParsedInitMessage result = NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);

        ParsedInitMessage expectedResult = new(
            chainId: 412346,
            initialBaseFee: NitroL2MessageParser.DefaultInitialL1BaseFee
        );

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void Parse_L1InitializeWithInvalidChainConfig_ParsingFails()
    {
        var ex = Assert.Throws<ArgumentException>(() => {
            ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba01000000000000000000000000000000000000000000000000000000000000009a");
            NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);
        });

        ArgumentNullException expectedError = new("Cannot process L1 initialize message without chain spec");
        Assert.That(ex.Message, Does.Contain($"Failed deserializing chain config: {expectedError}"));
    }

    [Test]
    public void Parse_L1InitializeWithInvalidDataLength_ParsingFails()
    {
        var ex = Assert.Throws<ArgumentException>(() => {
            ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0123");
            NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);
        });

        Assert.That(ex.Message, Is.EqualTo("Invalid init message data 0123"));
    }
}

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
