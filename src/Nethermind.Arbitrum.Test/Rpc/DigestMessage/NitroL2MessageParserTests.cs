using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Specs.Test.ChainSpecStyle;
using static NUnit.Framework.Assert;

namespace Nethermind.Arbitrum.Test.Rpc.DigestMessage
{
    [TestFixture]
    public class NitroL2MessageParserTests
    {
        private const int ChainId = 1;

        [Test]
        public static void Parse_SubmitRetryable_ParsesCorrectly()
        {
            var message = new L1IncomingMessage(
                new(
                    ArbitrumL1MessageKind.SubmitRetryable,
                    new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                    117,
                    1745999206,
                    new Hash256("0x0000000000000000000000000000000000000000000000000000000000000001"),
                    295),
                Convert.FromBase64String("AAAAAAAAAAAAAAAAP6sYRiLcGbYQk0m5SBFJO/KkU2IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAI4byb8EAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAjmgvhUZ1IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGTUgAAAAAAAAAAAAAAACTtMEUtA7PH8NHRUAKG5uRFcNOQgAAAAAAAAAAAAAAAJO0wRS0Ds8fw0dFQAobm5EVw05CAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
                null);

            ArbitrumSubmitRetryableTransaction transaction = (ArbitrumSubmitRetryableTransaction)NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

            ArbitrumSubmitRetryableTransaction expectedTransaction = new ArbitrumSubmitRetryableTransaction
            {
                ChainId = ChainId,
                RequestId = new("0x0000000000000000000000000000000000000000000000000000000000000001"),
                SenderAddress = new("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                L1BaseFee = 295,
                DepositValue = 10021000000413000,
                DecodedMaxFeePerGas = 1000000000,
                GasFeeCap = 1000000000,
                GasLimit = 21000,
                Gas = 21000,
                RetryTo = new("0x3fAB184622Dc19b6109349B94811493BF2a45362"),
                RetryValue = 10000000000000000,
                Beneficiary = new("0x93B4c114B40ECf1Fc34745400a1b9B9115c34E42"),
                MaxSubmissionFee = 413000,
                FeeRefundAddr = new("0x93B4c114B40ECf1Fc34745400a1b9B9115c34E42"),
                RetryData = Array.Empty<byte>(),
                Data = Array.Empty<byte>(),
                Nonce = 0,
                Mint = 10021000000413000,
                SourceHash = new("0x0000000000000000000000000000000000000000000000000000000000000001"), // RequestId -> SourceHash
                GasPrice = UInt256.Zero,
                Value = UInt256.Zero,
                IsOPSystemTransaction = false
            };

            transaction.Should().BeEquivalentTo(expectedTransaction);
        }

        [Test]
        public static void Parse_L2Message_EthLegacy_ParsesCorrectly()
        {
            var message = new L1IncomingMessage(
                new(
                    ArbitrumL1MessageKind.L2Message,
                    new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                    117,
                    1745999206,
                    new Hash256("0x0000000000000000000000000000000000000000000000000000000000000002"),
                    295),
                Convert.FromBase64String("BPilgIUXSHboAIMBhqCAgLhTYEWAYA5gADmAYADzUP5//////////////////////////////////////////+A2AWAAgWAggjeANYKCNPWAFRVgOVeBgv1bgIJSUFBQYBRgDPMboCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIioCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIi"),
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
        public static void Parse_Deposit_ParsesCorrectly()
        {
            var message = new L1IncomingMessage(
                new(
                    ArbitrumL1MessageKind.EthDeposit,
                    new Address("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
                    165,
                    1745999255,
                    new Hash256("0x0000000000000000000000000000000000000000000000000000000000000009"),
                    8),
                Convert.FromBase64String("Px6ufUbYjwj8L47Sf8sqsYPrLQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAFS0Cx+FK9oAAAA=="),
                null);

            ArbitrumDepositTransaction transaction = (ArbitrumDepositTransaction)NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

            ArbitrumDepositTransaction expectedTransaction = new ArbitrumDepositTransaction
            {
                ChainId = ChainId,
                L1RequestId = new("0x0000000000000000000000000000000000000000000000000000000000000009"),
                SenderAddress = new("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
                To = new("0x3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E"),
                Value = UInt256.Parse("100000000000000000000000"),
                SourceHash = new("0x0000000000000000000000000000000000000000000000000000000000000009"), // L1RequestId -> SourceHash
                Nonce = UInt256.Zero,
                GasPrice = UInt256.Zero,
                DecodedMaxFeePerGas = UInt256.Zero,
                GasLimit = 0,
                IsOPSystemTransaction = false,
                Mint = UInt256.Parse("100000000000000000000000"), // Value -> Mint
            };

            transaction.Should().BeEquivalentTo(expectedTransaction);
        }

        [Test]
        public static void Parse_L2Message_DynamicFeeTx_ParsesCorrectly()
        {
            var message = new L1IncomingMessage(
                new(
                    ArbitrumL1MessageKind.L2Message,
                    new Address("0xA4b000000000000000000073657175656e636572"),
                    166,
                    1745999257,
                    null,
                    8),
                Convert.FromBase64String("BAL4doMGSrqAhFloLwCEZVPxAIJSCJReFJfdHwjIey2P4j6aq2wd6DPZJ4kFa8deLWMQAACAwICgTJ7ERDhsUJoSmXYhVhdHIN5YgHJ2PBS1e9YImp0iAfmgTkKAGg0ukQ/BHPiMnbTpFqIuHlSBgQff7dPFFlMlhP4="),
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
        public static void Parse_RoundTrip_EthLegacy_ParsesCorrectly()
        {
            L1IncomingMessage message = new(
                new L1IncomingMessageHeader(
                    ArbitrumL1MessageKind.L2Message,
                    new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                    117,
                    1745999206,
                    new Hash256("0x0000000000000000000000000000000000000000000000000000000000000002"),
                    295),
                Convert.FromBase64String("BPilgIUXSHboAIMBhqCAgLhTYEWAYA5gADmAYADzUP5//////////////////////////////////////////+A2AWAAgWAggjeANYKCNPWAFRVgOVeBgv1bgIJSUFBQYBRgDPMboCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIioCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIi"),
                null);

            Transaction transaction = NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

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

            L1IncomingMessage? parsedMessage = NitroL2MessageParser.ParseMessageFromTransactions(message.Header, [transaction]);
            parsedMessage.Should().NotBeNull();
            parsedMessage!.L2Msg.Should().BeEquivalentTo(message.L2Msg);
        }

        [Test]
        public static void Parse_RoundTrip_DynamicFeeTx_ParsesCorrectly()
        {
            L1IncomingMessage message = new(
                new L1IncomingMessageHeader(
                    ArbitrumL1MessageKind.L2Message,
                    new Address("0xA4b000000000000000000073657175656e636572"),
                    166,
                    1745999257,
                    null,
                    8),
                Convert.FromBase64String("BAL4doMGSrqAhFloLwCEZVPxAIJSCJReFJfdHwjIey2P4j6aq2wd6DPZJ4kFa8deLWMQAACAwICgTJ7ERDhsUJoSmXYhVhdHIN5YgHJ2PBS1e9YImp0iAfmgTkKAGg0ukQ/BHPiMnbTpFqIuHlSBgQff7dPFFlMlhP4="),
                null);

            Transaction transaction = NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

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

            L1IncomingMessage? parsedMessage = NitroL2MessageParser.ParseMessageFromTransactions(message.Header, [transaction]);
            parsedMessage.Should().NotBeNull();
            parsedMessage!.L2Msg.Should().BeEquivalentTo(message.L2Msg);
        }

        [Test]
        public static void Parse_RoundTrip_EthLegacyAndDynamicFeeTx_ParsesCorrectly()
        {
            L1IncomingMessage message = new(
                new L1IncomingMessageHeader(
                    ArbitrumL1MessageKind.L2Message,
                    new Address("0xA4b000000000000000000073657175656e636572"),
                    166,
                    1745999257,
                    null,
                    8),
                Convert.FromBase64String("AwAAAAAAAAB6BAL4doMGSrqAhFloLwCEZVPxAIJSCJReFJfdHwjIey2P4j6aq2wd6DPZJ4kFa8deLWMQAACAwICgTJ7ERDhsUJoSmXYhVhdHIN5YgHJ2PBS1e9YImp0iAfmgTkKAGg0ukQ/BHPiMnbTpFqIuHlSBgQff7dPFFlMlhP4AAAAAAAAAqAT4pYCFF0h26ACDAYaggIC4U2BFgGAOYAA5gGAA81D+f//////////////////////////////////////////gNgFgAIFgIII3gDWCgjT1gBUVYDlXgYL9W4CCUlBQUGAUYAzzG6AiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIqAiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIg=="),
                null);

            IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, new());

            L1IncomingMessage? parsedMessage = NitroL2MessageParser.ParseMessageFromTransactions(message.Header, transactions);
            parsedMessage.Should().NotBeNull();
            parsedMessage!.L2Msg.Should().BeEquivalentTo(message.L2Msg);
        }

        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgR1aviFI7lPAdVIV32myYW5VIVTtxYTy77YI0r5OtTqBq17j1Lv4FmDlUUIb5DT9toNVdVxepdAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAA", Description = "With extra data")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgR1aviFI7lPAdVIV32myYW5VIVTtxYTy77YI0r5OtTqBq17j1Lv4FmDlUUIb5DT9toNVdVxepdAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACA==", Description = "Without extra data")]
        public static void Parse_BatchPostingReport_ParsesCorrectly(string l2Msg)
        {
            ulong batchTimestamp = 1745999275;
            var batchPosterAddr = new Address("0xe2148eE53c0755215Df69b2616E552154EdC584f");
            ulong l1BaseFee = 8;
            ulong batchDataCost = 148376;

            var message = new L1IncomingMessage(
                new(
                    ArbitrumL1MessageKind.BatchPostingReport,
                    batchPosterAddr,
                    185,
                    1745999275,
                    new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                    8),
                Convert.FromBase64String(l2Msg),
                batchDataCost);

            ArbitrumInternalTransaction transaction = (ArbitrumInternalTransaction)NitroL2MessageParser.ParseTransactions(message, ChainId, new()).Single();

            var packedData = AbiMetadata.PackInput(AbiMetadata.BatchPostingReport, batchTimestamp, batchPosterAddr, 1, batchDataCost,
                l1BaseFee);

            ArbitrumInternalTransaction expectedTransaction = new ArbitrumInternalTransaction
            {
                ChainId = ChainId,
                Data = packedData,
                SenderAddress = ArbosAddresses.ArbosAddress,
                To = ArbosAddresses.ArbosAddress,
                Nonce = UInt256.Zero,
                GasPrice = UInt256.Zero,
                DecodedMaxFeePerGas = UInt256.Zero,
                GasLimit = 0,
                Value = UInt256.Zero,
            };

            transaction.Should().BeEquivalentTo(expectedTransaction, options =>
                options.Using<ReadOnlyMemory<byte>>(ctx =>
                        ctx.Subject.Span.SequenceEqual(ctx.Expectation.Span).Should().BeTrue())
                    .WhenTypeIs<ReadOnlyMemory<byte>>());
        }

        [Test]
        public static void Parse_L2FundedByL1_Contract_ParsesCorrectly()
        {
            var message = new L1IncomingMessage(
                new(
                    ArbitrumL1MessageKind.L2FundedByL1,
                    new Address("0x502fae7d46d88f08fc2f8ed27fcb2ab183eb3e1f"),
                    194,
                    1746443431,
                    new Hash256("0x000000000000000000000000000000000000000000000000000000000000000b"),
                    8),
                Convert.FromBase64String("AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABMLAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACPDRgAAAAAAAAAAAAAAAAARtX/jSFhPBC5DbGv3w8Pe8XHeSQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA0J3gig=="),
                null);

            var transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, new());
            var deposit = (ArbitrumDepositTransaction)transactions[0];
            var contract = (ArbitrumContractTransaction)transactions[1];

            ArbitrumDepositTransaction expectedDeposit = new ArbitrumDepositTransaction
            {
                ChainId = ChainId,
                L1RequestId = new("0x9115655cbcdb654012cf1b2f7e5dbf11c9ef14e152a19d5f8ea75a329092d5a6"),
                SenderAddress = new("0x0000000000000000000000000000000000000000"),
                To = new("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
                Value = UInt256.Zero,
                SourceHash = new("0x9115655cbcdb654012cf1b2f7e5dbf11c9ef14e152a19d5f8ea75a329092d5a6"), // L1RequestId -> SourceHash
                Nonce = UInt256.Zero,
                GasPrice = UInt256.Zero,
                DecodedMaxFeePerGas = UInt256.Zero,
                GasLimit = 0,
                IsOPSystemTransaction = false,
                Mint = UInt256.Zero // Value -> Mint (which is 0 in this case)
            };

            ArbitrumContractTransaction expectedContract = new ArbitrumContractTransaction
            {
                ChainId = ChainId,
                RequestId = new("0xfc80cd5fe514767bc6e66ec558e68a5429ea70b50fa6caa3b53fc9278e918632"),
                SenderAddress = new("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
                DecodedMaxFeePerGas = 600000000,
                GasFeeCap = 600000000,
                GasLimit = 312000,
                Gas = 312000,
                To = new("0x11B57FE348584f042E436c6Bf7c3c3deF171de49"),
                Value = UInt256.Zero,
                Data = Convert.FromHexString("d09de08a"),
                Nonce = 0
            };

            deposit.Should().BeEquivalentTo(expectedDeposit);
            contract.Should().BeEquivalentTo(expectedContract, o => o.ForArbitrumTransaction());
        }

        [Test]
        public void Parse_L1Initialize_ParsesCorrectly()
        {
            // The blob in this test is a hex encoded string taken directly from running a nitro node and parsing
            // a L1IncomingMessage.L2Msg of kind L1MessageType_Initialize. See https://github.com/OffchainLabs/nitro/blob/v3.5.5/arbos/arbostypes/incomingmessage.go#L275.
            // The blobs in the tests below are derived from that one.
            ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba01000000000000000000000000000000000000000000000000000000000000009a7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d");

            ParsedInitMessage result = NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);

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
                ArbitrumChainParams = new ArbitrumChainParams
                {
                    Enabled = true,
                    AllowDebugPrecompiles = true,
                    InitialArbOSVersion = 32,
                    InitialChainOwner = new("0x5e1497dd1f08c87b2d8fe23e9aab6c1de833d927"),
                    GenesisBlockNum = 0,
                    DataAvailabilityCommittee = false,
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
            var ex = Throws<ArgumentException>(() =>
            {
                ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba01000000000000000000000000000000000000000000000000000000000000009a");
                NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);
            })!;

            ArgumentException expectedError = new("Cannot process L1 initialize message without chain spec");
            That(ex.Message, Is.Not.Null);
            That(ex.Message, Does.Contain($"Failed deserializing chain config: {expectedError}"));
        }

        [Test]
        public void Parse_L1InitializeWithInvalidDataLength_ParsingFails()
        {
            var ex = Throws<ArgumentException>(() =>
            {
                ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0123");
                NitroL2MessageParser.ParseL1Initialize(ref l2MsgSpan);
            })!;

            That(ex.Message, Is.Not.Null);
            That(ex.Message, Is.EqualTo("Invalid init message data 0123"));
        }

        private const ulong TestChainId = 412346;
        private const ulong TestInitialBaseFee = 154;
        private const uint TestInitialArbOSVersion = 32;
        private const string TestChainOwnerAddress = "0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927";
        private const ulong TestGenesisBlockNum = 0;
        private const uint TestMaxCodeSize = 24576;
        private const uint TestMaxInitCodeSize = 49152;

        [Test]
        public void IsCompatibleWith_WhenAllParametersMatch_ShouldReturnNull()
        {
            var initMessage = CreateInitMessageWithDefaults();
            var chainSpec = CreateChainSpec(TestChainId);

            var result = initMessage.IsCompatibleWith(chainSpec);

            result.Should().BeNull();
        }

        [Test]
        public void IsCompatibleWith_WhenChainIdMismatches_ShouldReturnErrorMessage()
        {
            const ulong mismatchedChainId = 999999;
            var initMessage = CreateInitMessageWithDefaults();
            var mismatchedChainSpec = CreateChainSpec(mismatchedChainId);

            var result = initMessage.IsCompatibleWith(mismatchedChainSpec);

            var expectedError = $"Chain ID mismatch: L1 init message has chain ID {TestChainId}, but local chainspec expects {mismatchedChainId}";
            result.Should().BeEquivalentTo(expectedError);
        }

        [Test]
        public void IsCompatibleWith_WhenInitialArbOSVersionMismatches_ShouldReturnErrorMessage()
        {
            const uint mismatchedVersion = 99;
            var initMessage = CreateInitMessageWithDefaults();
            var chainSpec = CreateChainSpec(TestChainId, mismatchedVersion);

            var result = initMessage.IsCompatibleWith(chainSpec);

            var expectedError = $"Initial ArbOS version mismatch: L1 init message has version {TestInitialArbOSVersion}, but local chainspec expects {mismatchedVersion}";
            result.Should().BeEquivalentTo(expectedError);
        }

        [Test]
        public void GetCanonicalArbitrumParameters_WhenL1ConfigIsAvailable_ShouldReturnL1Config()
        {
            var chainConfig = CreateChainConfig();
            var serializedConfig = System.Text.Encoding.UTF8.GetBytes("{}");
            var initMessage = CreateInitMessage(chainConfig, serializedConfig);
            var fallbackParams = CreateFallbackArbitrumSpecHelper();

            var expectedParams = new ArbitrumChainSpecEngineParameters
            {
                Enabled = true,
                InitialArbOSVersion = TestInitialArbOSVersion,
                InitialChainOwner = new Address(TestChainOwnerAddress),
                GenesisBlockNum = TestGenesisBlockNum,
                AllowDebugPrecompiles = true,
                DataAvailabilityCommittee = false,
                MaxCodeSize = TestMaxCodeSize,
                MaxInitCodeSize = TestMaxInitCodeSize,
                SerializedChainConfig = Convert.ToBase64String(serializedConfig)
            };

            var canonicalParams = initMessage.GetCanonicalArbitrumParameters(fallbackParams);

            canonicalParams.Should().BeEquivalentTo(expectedParams);
        }

        [Test]
        public void GetCanonicalArbitrumParameters_WhenL1ConfigIsUnavailable_ShouldUseFallbackParams()
        {
            var initMessage = CreateInitMessage(null, null);
            var fallbackParams = CreateFallbackArbitrumSpecHelper();

            var expectedParams = new ArbitrumChainSpecEngineParameters
            {
                Enabled = true,
                InitialArbOSVersion = 10,
                InitialChainOwner = Address.Zero,
                GenesisBlockNum = 100,
                AllowDebugPrecompiles = true,
                DataAvailabilityCommittee = false,
                MaxCodeSize = null,
                MaxInitCodeSize = null,
                SerializedChainConfig = null
            };

            var canonicalParams = initMessage.GetCanonicalArbitrumParameters(fallbackParams);

            canonicalParams.Should().BeEquivalentTo(expectedParams);
        }

        private static ChainConfig CreateChainConfig()
        {
            return new ChainConfig
            {
                ChainId = TestChainId,
                ArbitrumChainParams = new ArbitrumChainParams
                {
                    Enabled = true,
                    AllowDebugPrecompiles = true,
                    InitialArbOSVersion = TestInitialArbOSVersion,
                    InitialChainOwner = new Address(TestChainOwnerAddress),
                    GenesisBlockNum = TestGenesisBlockNum,
                    DataAvailabilityCommittee = false,
                    MaxCodeSize = TestMaxCodeSize,
                    MaxInitCodeSize = TestMaxInitCodeSize
                }
            };
        }

        private static ParsedInitMessage CreateInitMessage(
            ChainConfig? chainConfigSpec = null,
            byte[]? serializedChainConfig = null)
        {
            return new ParsedInitMessage(
                chainId: TestChainId,
                initialBaseFee: TestInitialBaseFee,
                chainConfigSpec: chainConfigSpec,
                serializedChainConfig: serializedChainConfig
            );
        }

        private static ParsedInitMessage CreateInitMessageWithDefaults()
        {
            var chainConfigSpec = CreateChainConfig();
            var serializedChainConfig = System.Text.Encoding.UTF8.GetBytes("{}");

            return CreateInitMessage(chainConfigSpec, serializedChainConfig);
        }

        private static ChainSpec CreateChainSpec(ulong chainId)
        {
            var chainSpec = new ChainSpec { ChainId = chainId };
            var arbitrumParams = CreateArbitrumChainSpecEngineParameters();
            chainSpec.EngineChainSpecParametersProvider = new TestChainSpecParametersProvider(arbitrumParams);
            return chainSpec;
        }

        private static ChainSpec CreateChainSpec(ulong chainId, uint initialArbOSVersion)
        {
            var chainSpec = new ChainSpec { ChainId = chainId };
            var arbitrumParams = CreateArbitrumChainSpecEngineParameters(initialArbOSVersion);
            chainSpec.EngineChainSpecParametersProvider = new TestChainSpecParametersProvider(arbitrumParams);
            return chainSpec;
        }

        private static ArbitrumChainSpecEngineParameters CreateArbitrumChainSpecEngineParameters(
            uint initialArbOSVersion = TestInitialArbOSVersion)
        {
            return new ArbitrumChainSpecEngineParameters
            {
                EnableArbOS = true,
                InitialArbOSVersion = initialArbOSVersion,
                InitialChainOwner = new Address(TestChainOwnerAddress),
                GenesisBlockNum = TestGenesisBlockNum,
                AllowDebugPrecompiles = true,
                DataAvailabilityCommittee = false
            };
        }

        private static ArbitrumSpecHelper CreateFallbackArbitrumSpecHelper()
        {
            return new ArbitrumSpecHelper(new ArbitrumChainSpecEngineParameters
            {
                EnableArbOS = true,
                InitialArbOSVersion = 10,
                InitialChainOwner = Address.Zero,
                GenesisBlockNum = 100
            });
        }
    }
}
