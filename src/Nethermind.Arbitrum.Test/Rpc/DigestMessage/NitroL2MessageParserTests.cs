using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Specs.Test.ChainSpecStyle;
using static NUnit.Framework.Assert;
using ArbitrumBinaryWriter = Nethermind.Arbitrum.Test.Infrastructure.ArbitrumBinaryWriter;

namespace Nethermind.Arbitrum.Test.Rpc.DigestMessage;

[TestFixture]
public class NitroL2MessageParserTests
{
    private const int ChainId = 1;

    // The initial L1 pricing basefee starts at 50 GWei unless set in the init message
    private static readonly UInt256 DefaultInitialL1BaseFee = 50.GWei();

    [Test]
    public static void Parse_SubmitRetryable_ParsesCorrectly()
    {
        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.SubmitRetryable,
                new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                117,
                1745999206,
                new Hash256("0x0000000000000000000000000000000000000000000000000000000000000001"),
                295),
            Convert.FromBase64String("AAAAAAAAAAAAAAAAP6sYRiLcGbYQk0m5SBFJO/KkU2IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAI4byb8EAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAjmgvhUZ1IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGTUgAAAAAAAAAAAAAAACTtMEUtA7PH8NHRUAKG5uRFcNOQgAAAAAAAAAAAAAAAJO0wRS0Ds8fw0dFQAobm5EVw05CAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
            null, null);

        ArbitrumSubmitRetryableTransaction transaction = (ArbitrumSubmitRetryableTransaction)NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new()).Single();

        ArbitrumSubmitRetryableTransaction expectedTransaction = new()
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
        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.L2Message,
                new Address("0xDD6Bd74674C356345DB88c354491C7d3173c6806"),
                117,
                1745999206,
                new Hash256("0x0000000000000000000000000000000000000000000000000000000000000002"),
                295),
            Convert.FromBase64String("BPilgIUXSHboAIMBhqCAgLhTYEWAYA5gADmAYADzUP5//////////////////////////////////////////+A2AWAAgWAggjeANYKCNPWAFRVgOVeBgv1bgIJSUFBQYBRgDPMboCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIioCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIi"),
            null, null);

        Transaction transaction = NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new()).Single();

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
        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.EthDeposit,
                new Address("0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F"),
                165,
                1745999255,
                new Hash256("0x0000000000000000000000000000000000000000000000000000000000000009"),
                8),
            Convert.FromBase64String("Px6ufUbYjwj8L47Sf8sqsYPrLQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAFS0Cx+FK9oAAAA=="),
            null, null);

        ArbitrumDepositTransaction transaction = (ArbitrumDepositTransaction)NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new()).Single();

        ArbitrumDepositTransaction expectedTransaction = new()
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
        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.L2Message,
                new Address("0xA4b000000000000000000073657175656e636572"),
                166,
                1745999257,
                null,
                8),
            Convert.FromBase64String("BAL4doMGSrqAhFloLwCEZVPxAIJSCJReFJfdHwjIey2P4j6aq2wd6DPZJ4kFa8deLWMQAACAwICgTJ7ERDhsUJoSmXYhVhdHIN5YgHJ2PBS1e9YImp0iAfmgTkKAGg0ukQ/BHPiMnbTpFqIuHlSBgQff7dPFFlMlhP4="),
            null, null);

        Transaction transaction = NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new()).Single();

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

    [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgR1aviFI7lPAdVIV32myYW5VIVTtxYTy77YI0r5OtTqBq17j1Lv4FmDlUUIb5DT9toNVdVxepdAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAA", Description = "With extra data")]
    [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGgR1aviFI7lPAdVIV32myYW5VIVTtxYTy77YI0r5OtTqBq17j1Lv4FmDlUUIb5DT9toNVdVxepdAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACA==", Description = "Without extra data")]
    public static void Parse_BatchPostingReport_ParsesCorrectly(string l2Msg)
    {
        ulong batchTimestamp = 1745999275;
        Address batchPosterAddr = new("0xe2148eE53c0755215Df69b2616E552154EdC584f");
        ulong l1BaseFee = 8;
        ulong batchDataCost = 148376;

        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.BatchPostingReport,
                batchPosterAddr,
                185,
                1745999275,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                8),
            Convert.FromBase64String(l2Msg),
            batchDataCost, null);

        ArbitrumInternalTransaction transaction = (ArbitrumInternalTransaction)NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new()).Single();

        byte[] packedData = AbiMetadata.PackInput(AbiMetadata.BatchPostingReport, batchTimestamp, batchPosterAddr, 1, batchDataCost,
            l1BaseFee);

        ArbitrumInternalTransaction expectedTransaction = new()
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
        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.L2FundedByL1,
                new Address("0x502fae7d46d88f08fc2f8ed27fcb2ab183eb3e1f"),
                194,
                1746443431,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000b"),
                8),
            Convert.FromBase64String("AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABMLAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACPDRgAAAAAAAAAAAAAAAAARtX/jSFhPBC5DbGv3w8Pe8XHeSQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA0J3gig=="),
            null, null);

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new());
        ArbitrumDepositTransaction deposit = (ArbitrumDepositTransaction)transactions[0];
        ArbitrumContractTransaction contract = (ArbitrumContractTransaction)transactions[1];

        ArbitrumDepositTransaction expectedDeposit = new()
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

        ArbitrumContractTransaction expectedContract = new()
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
    public static void Parse_MalformedL2Message_ReturnsEmpty()
    {
        //empty L2 message with valid kind byte
        byte[] emptyL2MsgBytes = [(byte)ArbitrumL2MessageKind.UnsignedUserTx];

        L1IncomingMessage message = new(
            new(
                ArbitrumL1MessageKind.L2Message,
                new Address("0xA4b000000000000000000073657175656e636572"),
                166,
                1745999257,
                null,
                8),
            emptyL2MsgBytes,
            null, null);

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new());

        transactions.Should().BeEmpty();
    }

    [Test]
    public void Parse_L1Initialize_ParsesCorrectly()
    {
        // The blob in this test is a hex encoded string taken directly from running a nitro node and parsing
        // a L1IncomingMessage.L2Msg of kind L1MessageType_Initialize. See https://github.com/OffchainLabs/nitro/blob/v3.5.5/arbos/arbostypes/incomingmessage.go#L275.
        // The blobs in the tests below are derived from that one.
        ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba01000000000000000000000000000000000000000000000000000000000000009a7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d");

        ParsedInitMessage result = ParseL1Initialize(ref l2MsgSpan);

        byte[] expectedSerializedConfig = Convert.FromHexString("7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d");

        ChainConfig expectedChainConfig = new()
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
        ParsedInitMessage result = ParseL1Initialize(ref l2MsgSpan);

        ParsedInitMessage expectedResult = new(
            chainId: 412346,
            initialBaseFee: DefaultInitialL1BaseFee
        );

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void Parse_L1InitializeWithInvalidChainConfig_ParsingFails()
    {
        ArgumentException ex = Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000064aba01000000000000000000000000000000000000000000000000000000000000009a");
            ParseL1Initialize(ref l2MsgSpan);
        })!;

        ArgumentException expectedError = new("Cannot process L1 initialize message without chain spec");
        That(ex.Message, Is.Not.Null);
        That(ex.Message, Does.Contain($"Failed deserializing chain config: {expectedError}"));
    }

    [Test]
    public void Parse_L1InitializeWithInvalidDataLength_ParsingFails()
    {
        ArgumentException ex = Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<byte> l2MsgSpan = Convert.FromHexString("0123");
            ParseL1Initialize(ref l2MsgSpan);
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
        ParsedInitMessage initMessage = CreateInitMessageWithDefaults();
        ChainSpec chainSpec = CreateChainSpec(TestChainId);

        string? result = initMessage.IsCompatibleWith(chainSpec);

        result.Should().BeNull();
    }

    [Test]
    public void IsCompatibleWith_WhenChainIdMismatches_ShouldReturnErrorMessage()
    {
        const ulong mismatchedChainId = 999999;
        ParsedInitMessage initMessage = CreateInitMessageWithDefaults();
        ChainSpec mismatchedChainSpec = CreateChainSpec(mismatchedChainId);

        string? result = initMessage.IsCompatibleWith(mismatchedChainSpec);

        string expectedError = $"Chain ID mismatch: L1 init message has chain ID {TestChainId}, but local chainspec expects {mismatchedChainId}";
        result.Should().BeEquivalentTo(expectedError);
    }

    [Test]
    public void IsCompatibleWith_WhenInitialArbOSVersionMismatches_ShouldReturnErrorMessage()
    {
        const uint mismatchedVersion = 99;
        ParsedInitMessage initMessage = CreateInitMessageWithDefaults();
        ChainSpec chainSpec = CreateChainSpec(TestChainId, mismatchedVersion);

        string? result = initMessage.IsCompatibleWith(chainSpec);

        string expectedError = $"Initial ArbOS version mismatch: L1 init message has version {TestInitialArbOSVersion}, but local chainspec expects {mismatchedVersion}";
        result.Should().BeEquivalentTo(expectedError);
    }

    [Test]
    public void GetCanonicalArbitrumParameters_WhenL1ConfigIsAvailable_ShouldReturnL1Config()
    {
        ChainConfig chainConfig = CreateChainConfig();
        byte[] serializedConfig = "{}"u8.ToArray();
        ParsedInitMessage initMessage = CreateInitMessage(chainConfig, serializedConfig);
        ArbitrumSpecHelper fallbackParams = CreateFallbackArbitrumSpecHelper();

        ArbitrumChainSpecEngineParameters expectedParams = new()
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

        ArbitrumChainSpecEngineParameters canonicalParams = initMessage.GetCanonicalArbitrumParameters(fallbackParams);

        canonicalParams.Should().BeEquivalentTo(expectedParams);
    }

    [Test]
    public void GetCanonicalArbitrumParameters_WhenL1ConfigIsUnavailable_ShouldUseFallbackParams()
    {
        ParsedInitMessage initMessage = CreateInitMessage();
        ArbitrumSpecHelper fallbackParams = CreateFallbackArbitrumSpecHelper();

        ArbitrumChainSpecEngineParameters expectedParams = new()
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

        ArbitrumChainSpecEngineParameters canonicalParams = initMessage.GetCanonicalArbitrumParameters(fallbackParams);

        canonicalParams.Should().BeEquivalentTo(expectedParams);
    }

    [Test]
    public static void Parse_BatchPostingReport_ArbOS40_WithBatchDataStats_UsesLegacyGasCalculation()
    {
        // Test that ArbOS < 50 calculates gas from BatchDataStats when present
        ulong batchTimestamp = 1745999275;
        Address batchPosterAddr = new("0xe2148eE53c0755215Df69b2616E552154EdC584f");
        ulong l1BaseFee = 8;
        ulong batchNum = 1;
        Hash256 dataHash = Keccak.Zero;

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        ArbitrumBinaryWriter.WriteUInt256(writer, batchTimestamp);
        ArbitrumBinaryWriter.WriteAddress(writer, batchPosterAddr);
        ArbitrumBinaryWriter.WriteHash256(writer, dataHash);
        ArbitrumBinaryWriter.WriteUInt256(writer, batchNum);
        ArbitrumBinaryWriter.WriteUInt256(writer, l1BaseFee);

        BatchDataStats batchDataStats = new(1000, 800);

        L1IncomingMessage message = new(
            Header: new(
                ArbitrumL1MessageKind.BatchPostingReport,
                batchPosterAddr,
                185,
                batchTimestamp,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                l1BaseFee),
            L2Msg: stream.ToArray(),
            BatchGasCost: null, // Can be null when BatchDataStats is present
            BatchDataStats: batchDataStats);

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new());

        transactions.Should().NotBeEmpty("Parser should successfully parse with BatchDataStats present");

        ArbitrumInternalTransaction transaction = (ArbitrumInternalTransaction)transactions.Single();

        // Calculate expected legacy gas from BatchDataStats (same as parser logic)
        ulong gas = 4 * (batchDataStats.Length - batchDataStats.NonZeros) + 16 * batchDataStats.NonZeros;
        ulong keccakWords = (batchDataStats.Length + 31) / 32;
        gas += 30 + (keccakWords * 6);
        gas += 2 * 20000;
        ulong legacyGas = gas;

        // Should use BatchPostingReport (legacy format) with calculated gas from BatchDataStats
        byte[] packedData = AbiMetadata.PackInput(AbiMetadata.BatchPostingReport, batchTimestamp, batchPosterAddr, batchNum, legacyGas, l1BaseFee);

        transaction.Data.ToArray().Should().BeEquivalentTo(packedData);
    }

    [Test]
    public static void Parse_BatchPostingReport_ArbOS40_WithoutBatchDataStats_RequiresBatchGasCost()
    {
        // Test that ArbOS < 50 requires BatchGasCost when BatchDataStats is absent
        ulong batchTimestamp = 1745999275;
        Address batchPosterAddr = new("0xe2148eE53c0755215Df69b2616E552154EdC584f");
        ulong l1BaseFee = 8;
        ulong batchNum = 1;
        Hash256 dataHash = Keccak.Zero;

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        ArbitrumBinaryWriter.WriteUInt256(writer, batchTimestamp);
        ArbitrumBinaryWriter.WriteAddress(writer, batchPosterAddr);
        ArbitrumBinaryWriter.WriteHash256(writer, dataHash);
        ArbitrumBinaryWriter.WriteUInt256(writer, batchNum);
        ArbitrumBinaryWriter.WriteUInt256(writer, l1BaseFee);

        L1IncomingMessage message = new(
            Header: new(
                ArbitrumL1MessageKind.BatchPostingReport,
                batchPosterAddr,
                185,
                batchTimestamp,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                l1BaseFee),
            L2Msg: stream.ToArray(),
            BatchGasCost: null, // Null without BatchDataStats should fail
            BatchDataStats: null);

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 40, new());

        // Parser catches the exception and returns empty list
        transactions.Should().BeEmpty("Parser should return empty when both BatchGasCost and BatchDataStats are null");
    }

    [Test]
    public static void Parse_BatchPostingReport_ArbOS50_ParsesCorrectly()
    {
        ulong batchTimestamp = 1745999275;
        Address batchPosterAddr = new("0xe2148eE53c0755215Df69b2616E552154EdC584f");
        ulong l1BaseFee = 8;
        ulong extraGas = 100;
        ulong batchNum = 1;
        Hash256 dataHash = Keccak.Zero;

        // Build L2Msg
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        ArbitrumBinaryWriter.WriteUInt256(writer, batchTimestamp);
        ArbitrumBinaryWriter.WriteAddress(writer, batchPosterAddr);
        ArbitrumBinaryWriter.WriteHash256(writer, dataHash);
        ArbitrumBinaryWriter.WriteUInt256(writer, batchNum);
        ArbitrumBinaryWriter.WriteUInt256(writer, l1BaseFee);
        ArbitrumBinaryWriter.WriteULongBigEndian(writer, extraGas);

        BatchDataStats batchDataStats = new(1000, 800);

        L1IncomingMessage message = new(
            Header: new(
                ArbitrumL1MessageKind.BatchPostingReport,
                batchPosterAddr,
                185,
                batchTimestamp,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                l1BaseFee),
            L2Msg: stream.ToArray(),
            BatchGasCost: null, // Can be null for ArbOS >= 50
            BatchDataStats: batchDataStats);

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 50, new());

        transactions.Should().NotBeEmpty();
        ArbitrumInternalTransaction transaction = (ArbitrumInternalTransaction)transactions.Single();

        // For ArbOS 50+, use BatchPostingReportV2
        byte[] packedData = AbiMetadata.PackInput(
            AbiMetadata.BatchPostingReportV2,
            batchTimestamp,
            batchPosterAddr,
            batchNum,
            batchDataStats.Length,
            batchDataStats.NonZeros,
            extraGas,
            l1BaseFee
        );

        transaction.Data.ToArray().Should().BeEquivalentTo(packedData);
    }

    [Test]
    public static void Parse_BatchPostingReport_ArbOS50_WithoutExtraGas_ParsesCorrectly()
    {
        ulong batchTimestamp = 1745999275;
        Address batchPosterAddr = new("0xe2148eE53c0755215Df69b2616E552154EdC584f");
        ulong l1BaseFee = 8;
        ulong batchNum = 1;
        Hash256 dataHash = Keccak.Zero;

        // Build L2Msg without extra gas
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        ArbitrumBinaryWriter.WriteUInt256(writer, batchTimestamp);
        ArbitrumBinaryWriter.WriteAddress(writer, batchPosterAddr);
        ArbitrumBinaryWriter.WriteHash256(writer, dataHash);
        ArbitrumBinaryWriter.WriteUInt256(writer, batchNum);
        ArbitrumBinaryWriter.WriteUInt256(writer, l1BaseFee);
        // No extra gas

        BatchDataStats batchDataStats = new(1000, 800);

        L1IncomingMessage message = new(
            Header: new(
                ArbitrumL1MessageKind.BatchPostingReport,
                batchPosterAddr,
                185,
                batchTimestamp,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                l1BaseFee),
            L2Msg: stream.ToArray(),
            BatchGasCost: null,
            BatchDataStats: batchDataStats);

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 50, new());

        transactions.Should().NotBeEmpty();
        ArbitrumInternalTransaction transaction = (ArbitrumInternalTransaction)transactions.Single();

        // For ArbOS 50+, use BatchPostingReportV2 with extraGas = 0
        byte[] packedData = AbiMetadata.PackInput(
            AbiMetadata.BatchPostingReportV2,
            batchTimestamp,
            batchPosterAddr,
            batchNum,
            batchDataStats.Length,
            batchDataStats.NonZeros,
            0UL, // extraGas defaults to 0
            l1BaseFee
        );

        transaction.Data.ToArray().Should().BeEquivalentTo(packedData);
    }

    [Test]
    public static void Parse_BatchPostingReport_ArbOS50_WithoutBatchDataStats_ReturnsEmpty()
    {
        // ArbOS >= 50 requires BatchDataStats
        ulong batchTimestamp = 1745999275;
        Address batchPosterAddr = new("0xe2148eE53c0755215Df69b2616E552154EdC584f");
        ulong l1BaseFee = 8;
        ulong batchNum = 1;
        Hash256 dataHash = Keccak.Zero;

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        ArbitrumBinaryWriter.WriteUInt256(writer, batchTimestamp);
        ArbitrumBinaryWriter.WriteAddress(writer, batchPosterAddr);
        ArbitrumBinaryWriter.WriteHash256(writer, dataHash);
        ArbitrumBinaryWriter.WriteUInt256(writer, batchNum);
        ArbitrumBinaryWriter.WriteUInt256(writer, l1BaseFee);

        L1IncomingMessage message = new(
            Header: new(
                ArbitrumL1MessageKind.BatchPostingReport,
                batchPosterAddr,
                185,
                batchTimestamp,
                new Hash256("0x000000000000000000000000000000000000000000000000000000000000000a"),
                l1BaseFee),
            L2Msg: stream.ToArray(),
            BatchGasCost: null,
            BatchDataStats: null); // No BatchDataStats provided

        IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(message, ChainId, 50, new());

        // Parser catches the exception and returns empty list
        transactions.Should().BeEmpty("Parser should return empty when BatchDataStats is missing for ArbOS >= 50");
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
        ChainConfig chainConfigSpec = CreateChainConfig();
        byte[] serializedChainConfig = System.Text.Encoding.UTF8.GetBytes("{}");

        return CreateInitMessage(chainConfigSpec, serializedChainConfig);
    }

    private static ChainSpec CreateChainSpec(ulong chainId)
    {
        ChainSpec chainSpec = new() { ChainId = chainId };
        ArbitrumChainSpecEngineParameters arbitrumParams = CreateArbitrumChainSpecEngineParameters();
        chainSpec.EngineChainSpecParametersProvider = new TestChainSpecParametersProvider(arbitrumParams);
        return chainSpec;
    }

    private static ChainSpec CreateChainSpec(ulong chainId, uint initialArbOSVersion)
    {
        ChainSpec chainSpec = new() { ChainId = chainId };
        ArbitrumChainSpecEngineParameters arbitrumParams = CreateArbitrumChainSpecEngineParameters(initialArbOSVersion);
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

    private static ParsedInitMessage ParseL1Initialize(ref ReadOnlySpan<byte> data)
    {
        if (data.Length == 32)
        {
            ulong chainId = (ulong)ArbitrumBinaryReader.ReadBigInteger256OrFail(ref data);
            return new ParsedInitMessage(chainId, DefaultInitialL1BaseFee);
        }

        if (data.Length > 32)
        {
            ulong chainId = (ulong)ArbitrumBinaryReader.ReadBigInteger256OrFail(ref data);
            byte version = ArbitrumBinaryReader.ReadByteOrFail(ref data);
            UInt256 baseFee = DefaultInitialL1BaseFee;
            switch (version)
            {
                case 1:
                    baseFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
                    goto case 0;
                case 0:
                    byte[] serializedChainConfig = data.ToArray();
                    string chainConfigStr = Encoding.UTF8.GetString(serializedChainConfig);
                    try
                    {
                        if (string.IsNullOrEmpty(chainConfigStr) || JsonSerializer.Deserialize<ChainConfig>(chainConfigStr) is not { } chainConfigSpec)
                            throw new ArgumentException("Cannot process L1 initialize message without chain spec");

                        return new ParsedInitMessage(chainId, baseFee, chainConfigSpec, serializedChainConfig);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Failed deserializing chain config: {e}");
                    }
            }
        }

        throw new ArgumentException($"Invalid init message data {Convert.ToHexString(data)}");
    }
}
