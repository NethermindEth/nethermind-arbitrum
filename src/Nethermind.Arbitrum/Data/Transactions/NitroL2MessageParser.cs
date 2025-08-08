using System.Buffers.Binary;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using System.Text;
using System.Text.Json;
using Nethermind.Arbitrum.Precompiles;

namespace Nethermind.Arbitrum.Data.Transactions;

public static class NitroL2MessageParser
{

    private static readonly TxDecoder _decoder;
    static NitroL2MessageParser()
    {
        TxDecoder decoder = TxDecoder.Instance;
        decoder.RegisterDecoder(new ArbitrumInternalTxDecoder());
        decoder.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder());
        decoder.RegisterDecoder(new ArbitrumRetryTxDecoder());
        decoder.RegisterDecoder(new ArbitrumDepositTxDecoder());
        _decoder = decoder;
    }
    public static IReadOnlyList<Transaction> ParseTransactions(L1IncomingMessage message, ulong chainId, ILogger logger)
    {
        if (message.L2Msg == null || message.L2Msg.Length == 0)
        {
            logger.Warn("L2 message is null or empty.");
            return [];
        }

        if (message.L2Msg.Length > ArbitrumConstants.MaxL2MessageSize)
        {
            logger.Warn($"L2 message size {message.L2Msg.Length} exceeds maximum {ArbitrumConstants.MaxL2MessageSize}, ignoring.");
            return [];
        }

        ReadOnlySpan<byte> l2MsgSpan = message.L2Msg.AsSpan();

        switch (message.Header.Kind)
        {
            case ArbitrumL1MessageKind.L2Message:
                return ParseL2MessageFormat(ref l2MsgSpan, message.Header.Sender, message.Header.Timestamp, message.Header.RequestId, chainId, 0, logger);

            case ArbitrumL1MessageKind.L2FundedByL1:
                return ParseL2FundedByL1(ref l2MsgSpan, message.Header, chainId);

            case ArbitrumL1MessageKind.SubmitRetryable:
                return ParseSubmitRetryable(ref l2MsgSpan, message.Header, chainId);

            case ArbitrumL1MessageKind.EthDeposit:
                return ParseEthDeposit(ref l2MsgSpan, message.Header, chainId);

            case ArbitrumL1MessageKind.BatchPostingReport:
                return ParseBatchPostingReport(ref l2MsgSpan, chainId, message.BatchGasCost);

            case ArbitrumL1MessageKind.EndOfBlock:
            case ArbitrumL1MessageKind.RollupEvent:
                return []; // No transactions for these types

            case ArbitrumL1MessageKind.Initialize:
                // Should be handled explicitly at genesis, not during normal operation
                throw new ArgumentException("Initialize message encountered outside of genesis.", nameof(message));

            case ArbitrumL1MessageKind.BatchForGasEstimation:
                throw new NotImplementedException("L1 message type BatchForGasEstimation is unimplemented.");

            case ArbitrumL1MessageKind.Invalid:
                throw new ArgumentException("Invalid L1 message type (0xFF).", nameof(message));

            default:
                // Ignore unknown/invalid message types as per Go implementation
                logger.Warn($"Ignoring L1 message with unknown kind: {message.Header.Kind}");
                return [];
        }
    }

    private static List<Transaction> ParseL2MessageFormat(
        ref ReadOnlySpan<byte> data,
        Address poster,
        ulong timestamp, // Note: timestamp is not directly used in tx creation here
        Hash256? l1RequestId,
        ulong chainId,
        int depth,
        ILogger logger)
    {
        var l2Kind = (ArbitrumL2MessageKind)ArbitrumBinaryReader.ReadByteOrFail(ref data);
        if (!Enum.IsDefined(l2Kind))
        {
            throw new ArgumentException($"L2 message kind {l2Kind} is not defined.");
        }

        switch (l2Kind)
        {
            case ArbitrumL2MessageKind.UnsignedUserTx:
            case ArbitrumL2MessageKind.ContractTx:
                var parsedTx = ParseUnsignedTx(ref data, poster, l1RequestId, chainId, l2Kind);
                return [parsedTx];

            case ArbitrumL2MessageKind.Batch:
                const int maxDepth = 16;
                if (depth >= maxDepth)
                {
                    throw new ArgumentException($"L2 message batch depth exceeds maximum of {maxDepth}");
                }

                var transactions = new List<Transaction>();
                var index = UInt256.Zero;
                while (!data.IsEmpty) // Loop until the span is consumed
                {
                    ReadOnlyMemory<byte> nextMsgData = ArbitrumBinaryReader.ReadByteStringOrFail(ref data, ArbitrumConstants.MaxL2MessageSize);
                    ReadOnlySpan<byte> nextMsgSpan = nextMsgData.Span;

                    Hash256? nextRequestId = null;
                    if (l1RequestId != null)
                    {
                        // Calculate sub-request ID: keccak256(l1RequestId, index)
                        Span<byte> combined = new byte[64];
                        l1RequestId.Bytes.CopyTo(combined[..32]);
                        index.ToBigEndian(combined[32..]);
                        nextRequestId = Keccak.Compute(combined);
                    }

                    transactions.AddRange(ParseL2MessageFormat(ref nextMsgSpan, poster, timestamp, nextRequestId, chainId, depth + 1, logger));

                    if (!nextMsgSpan.IsEmpty)
                    {
                        logger.Warn($"Nested L2 message parsing did not consume all data. Kind: {l2Kind}, Depth: {depth}, Remaining: {nextMsgSpan.Length} bytes.");
                    }

                    index.Add(UInt256.One, out index);
                }
                return transactions;

            case ArbitrumL2MessageKind.SignedTx:
                var legacyTx = Rlp.Decode<Transaction>(data.ToArray(),
                    RlpBehaviors.AllowUnsigned | RlpBehaviors.SkipTypedWrapping | RlpBehaviors.InMempoolForm);

                if (legacyTx.Type >= (TxType)ArbitrumTxType.ArbitrumDeposit || legacyTx.Type == TxType.Blob)
                {
                    throw new ArgumentException($"Unsupported transaction type {legacyTx.Type} encountered in L2MessageKind_SignedTx.");
                }

                return [legacyTx];

            case ArbitrumL2MessageKind.Heartbeat:
                if (timestamp >= ArbitrumConstants.HeartbeatsDisabledAt)
                {
                    throw new ArgumentException("Heartbeat message received after disable time.");
                }

                return [];

            case ArbitrumL2MessageKind.NonmutatingCall:
                throw new NotImplementedException("L2 message kind NonmutatingCall is unimplemented.");
            case ArbitrumL2MessageKind.SignedCompressedTx:
                throw new NotImplementedException("L2 message kind SignedCompressedTx is unimplemented.");

            default:
                // Ignore invalid/unknown message kind as per Go implementation
                logger.Warn($"Ignoring L2 message with unknown kind: {l2Kind}");
                return [];
        }
    }

    private static Transaction ParseUnsignedTx(ref ReadOnlySpan<byte> data, Address poster, Hash256? l1RequestId, ulong chainId, ArbitrumL2MessageKind kind)
    {
        var gasLimit = (ulong)ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        var maxFeePerGas = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        var nonce = kind == ArbitrumL2MessageKind.UnsignedUserTx
            ? (ulong)ArbitrumBinaryReader.ReadBigInteger256OrFail(ref data)
            : 0;

        var destination = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data);
        destination = destination == Address.Zero ? null : destination;

        var value = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        // The rest of the data is the calldata
        ReadOnlyMemory<byte> calldata = data.ToArray();

        return kind switch
        {
            ArbitrumL2MessageKind.UnsignedUserTx => new ArbitrumUnsignedTransaction
            {
                ChainId = chainId,
                SenderAddress = poster,
                Nonce = nonce,
                DecodedMaxFeePerGas = maxFeePerGas,
                GasFeeCap = maxFeePerGas,
                GasLimit = (long)gasLimit,
                Gas = gasLimit,
                To = destination,
                Value = value,
                Data = calldata
            },
            ArbitrumL2MessageKind.ContractTx => l1RequestId != null
                ? new ArbitrumContractTransaction
                {
                    ChainId = chainId,
                    RequestId = l1RequestId,
                    SenderAddress = poster,
                    DecodedMaxFeePerGas = maxFeePerGas,
                    GasFeeCap = maxFeePerGas,
                    GasLimit = (long)gasLimit,
                    Gas = gasLimit,
                    To = destination,
                    Value = value,
                    Data = calldata,
                    Nonce = 0
                }
                : throw new ArgumentException("Cannot create ArbitrumContractTransaction without L1 request ID."),
            _ => throw new ArgumentException($"Invalid txKind '{kind}' passed to ParseUnsignedTx.")
        };
    }

    private static List<Transaction> ParseL2FundedByL1(ref ReadOnlySpan<byte> data, L1IncomingMessageHeader header, ulong chainId)
    {
        if (header.RequestId == null)
        {
            throw new ArgumentException("Cannot process L2FundedByL1 message without L1 request ID.");
        }

        var kind = (ArbitrumL2MessageKind)ArbitrumBinaryReader.ReadByteOrFail(ref data);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException($"Invalid L2FundedByL1 message kind: {kind}");
        }

        // Calculate request IDs
        // depositRequestId = keccak256(requestId, 0)
        // unsignedRequestId = keccak256(requestId, 1)
        Span<byte> requestBytes = stackalloc byte[64];
        header.RequestId.Bytes.CopyTo(requestBytes[..32]);

        var depositRequestId = Keccak.Compute(requestBytes);

        requestBytes[63] = 1;
        var unsignedRequestId = Keccak.Compute(requestBytes);

        var unsignedTx = ParseUnsignedTx(ref data, header.Sender, unsignedRequestId, chainId, kind);
        ArbitrumDepositTransaction depositData = new ArbitrumDepositTransaction
        {
            ChainId = chainId,
            L1RequestId = depositRequestId,
            SenderAddress = Address.Zero,
            To = header.Sender,
            Value = unsignedTx.Value
        };
        var depositTx = ConvertParsedDataToTransaction(depositData);

        return [depositTx, unsignedTx];
    }

    private static List<Transaction> ParseEthDeposit(ref ReadOnlySpan<byte> data, L1IncomingMessageHeader header, ulong chainId)
    {
        if (header.RequestId == null)
        {
            throw new ArgumentException("Cannot process EthDeposit message without L1 request ID.");
        }

        var to = ArbitrumBinaryReader.ReadAddressOrFail(ref data);
        var value = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        ArbitrumDepositTransaction depositData = new ArbitrumDepositTransaction
        {
            ChainId = chainId,
            L1RequestId = header.RequestId,
            SenderAddress = header.Sender,
            To = to,
            Value = value
        };

        return [ConvertParsedDataToTransaction(depositData)];
    }

    private static List<Transaction> ParseSubmitRetryable(ref ReadOnlySpan<byte> data, L1IncomingMessageHeader header, ulong chainId)
    {
        ArgumentNullException.ThrowIfNull(header.RequestId, "Cannot process SubmitRetryable message without L1 request ID.");

        var retryTo = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data);
        retryTo = retryTo == Address.Zero ? null : retryTo;

        var retryValue = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        var depositValue = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        var maxSubmissionFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        var feeRefundAddress = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data);
        var callvalueRefundAddress = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data); // Beneficiary

        var gasLimit256 = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        if (gasLimit256 > ulong.MaxValue)
        {
            throw new ArgumentException("Retryable gas limit overflows ulong.");
        }

        var gasLimit = (ulong)gasLimit256;
        var maxFeePerGas = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        var dataLength256 = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        if (dataLength256 > ArbitrumConstants.MaxL2MessageSize)
        {
            throw new ArgumentException("Retryable data too large.");
        }

        ReadOnlyMemory<byte> retryData = ArbitrumBinaryReader.ReadBytesOrFail(ref data, (int)dataLength256).ToArray();

        ArbitrumSubmitRetryableTransaction retryableData = new ArbitrumSubmitRetryableTransaction
        {
            ChainId = chainId,
            RequestId = header.RequestId,
            SenderAddress = header.Sender,
            L1BaseFee = header.BaseFeeL1,
            DepositValue = depositValue,
            DecodedMaxFeePerGas = maxFeePerGas,
            GasFeeCap = maxFeePerGas,
            GasLimit = (long)gasLimit,
            Gas = gasLimit,
            RetryTo = retryTo,
            RetryValue = retryValue,
            Beneficiary = callvalueRefundAddress,
            MaxSubmissionFee = maxSubmissionFee,
            FeeRefundAddr = feeRefundAddress,
            RetryData = retryData,
            Data = retryData,
            Nonce = 0,
            Mint = depositValue
        };

        return [ConvertParsedDataToTransaction(retryableData)];
    }

    private static List<Transaction> ParseBatchPostingReport(ref ReadOnlySpan<byte> data, ulong chainId, ulong? batchGasCostFromMsg)
    {
        ArgumentNullException.ThrowIfNull(batchGasCostFromMsg, "Cannot process BatchPostingReport message without Gas cost.");

        var batchTimestamp = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        var batchPosterAddr = ArbitrumBinaryReader.ReadAddressOrFail(ref data);
        _ = ArbitrumBinaryReader.ReadHash256OrFail(ref data); // dataHash is not used directly in tx, but parsed
        var batchNum256 = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        if (batchNum256 > ulong.MaxValue)
        {
            throw new ArgumentException("Batch number overflows ulong.");
        }

        var batchNum = (ulong)batchNum256;
        var l1BaseFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        // Extra gas is optional in Go, try reading it
        ulong extraGas = 0;
        if (!ArbitrumBinaryReader.TryReadULongBigEndian(ref data, out extraGas))
        {
            // If reading fails but data is not empty, it's an error
            // Otherwise, EOF is fine, extraGas remains 0
            throw new ArgumentException("Invalid data after L1 base fee in BatchPostingReport.");
        }

        // Calculate total gas cost (matches Go logic) following SaturatingAdd go implementation
        var batchDataGas = batchGasCostFromMsg > ulong.MaxValue - extraGas ? ulong.MaxValue : batchGasCostFromMsg.Value + extraGas;

        var packedData = AbiMetadata.PackInput(AbiMetadata.BatchPostingReport, batchTimestamp, batchPosterAddr, batchNum, batchDataGas,
            l1BaseFee);
        ArbitrumInternalTransaction internalTxParsed = new ArbitrumInternalTransaction
        {
            ChainId = chainId,
            Data = packedData
        };

        return [ConvertParsedDataToTransaction(internalTxParsed)];
    }

    public static Transaction ConvertParsedDataToTransaction(object parsedData)
    {
        return parsedData switch
        {
            ArbitrumUnsignedTransaction d => ParseArbitrumUnsignedTransaction(d),
            ArbitrumContractTransaction d => ParseArbitrumContractTransaction(d),
            ArbitrumDepositTransaction d => ParseArbitrumDepositTransaction(d),
            ArbitrumSubmitRetryableTransaction d => ParseArbitrumSubmitRetryableTransaction(d),
            ArbitrumInternalTransaction d => ParseArbitrumInternalTransaction(d),
            _ => throw new ArgumentException($"Unsupported parsed data type: {parsedData.GetType().Name}")
        };
    }

    private static ArbitrumUnsignedTransaction ParseArbitrumUnsignedTransaction(ArbitrumUnsignedTransaction d)
    {
        // Apply old wrapper mappings:
        d.GasPrice = UInt256.Zero;
        d.DecodedMaxFeePerGas = d.GasFeeCap;
        d.GasLimit = (long)d.Gas;
        return d;
    }

    private static ArbitrumContractTransaction ParseArbitrumContractTransaction(ArbitrumContractTransaction d)
    {
        // Apply old wrapper mappings:
        d.SourceHash = d.RequestId;
        d.Nonce = UInt256.Zero;
        d.GasPrice = UInt256.Zero;
        d.DecodedMaxFeePerGas = d.GasFeeCap;
        d.GasLimit = (long)d.Gas;
        d.IsOPSystemTransaction = true;
        return d;
    }

    private static ArbitrumDepositTransaction ParseArbitrumDepositTransaction(ArbitrumDepositTransaction d)
    {
        // Apply old wrapper mappings:
        d.SourceHash = d.L1RequestId;
        d.Nonce = UInt256.Zero;
        d.GasPrice = UInt256.Zero;
        d.DecodedMaxFeePerGas = UInt256.Zero;
        d.GasLimit = 0;
        d.IsOPSystemTransaction = false;
        d.Mint = d.Value;
        return d;
    }

    private static ArbitrumSubmitRetryableTransaction ParseArbitrumSubmitRetryableTransaction(ArbitrumSubmitRetryableTransaction d)
    {
        // Apply old wrapper mappings:
        d.SourceHash = d.RequestId;
        d.Nonce = UInt256.Zero;
        d.GasPrice = UInt256.Zero;
        d.DecodedMaxFeePerGas = d.GasFeeCap;
        d.GasLimit = (long)d.Gas;
        d.Value = UInt256.Zero; // Tx value is 0, L2 execution value is in RetryValue
        d.Data = d.RetryData.ToArray();
        d.IsOPSystemTransaction = false;
        d.Mint = d.DepositValue;
        return d;
    }

    private static ArbitrumInternalTransaction ParseArbitrumInternalTransaction(ArbitrumInternalTransaction d)
    {
        // Apply old wrapper mappings:
        d.SenderAddress = ArbosAddresses.ArbosAddress;
        d.To = ArbosAddresses.ArbosAddress;
        d.Nonce = UInt256.Zero;
        d.GasPrice = UInt256.Zero;
        d.DecodedMaxFeePerGas = UInt256.Zero;
        d.GasLimit = 0;
        d.Value = UInt256.Zero;
        return d;
    }

    // The initial L1 pricing basefee starts at 50 GWei unless set in the init message
    public static readonly UInt256 DefaultInitialL1BaseFee = 50.GWei();

    public static ParsedInitMessage ParseL1Initialize(ref ReadOnlySpan<byte> data)
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
                        if (
                            string.IsNullOrEmpty(chainConfigStr) ||
                            JsonSerializer.Deserialize<ChainConfig>(chainConfigStr) is not ChainConfig chainConfigSpec
                        )
                        {
                            throw new ArgumentException("Cannot process L1 initialize message without chain spec");
                        }
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

    public static L1IncomingMessage? ParseMessageFromTransactions(L1IncomingMessageHeader header, IReadOnlyList<Transaction> txes)
    {
        byte[] l2Message;
        if (txes.Count == 1)
        {
            var messageSizeLength = _decoder.GetLength(txes[0], RlpBehaviors.SkipTypedWrapping) + 1;
            l2Message = new byte[messageSizeLength];
            RlpStream stream = new(l2Message);
            stream.WriteByte((byte)ArbitrumL2MessageKind.SignedTx);
            _decoder.Encode(stream, txes[0], RlpBehaviors.SkipTypedWrapping);
        }
        else
        {
            int messageSizeLength = 1;
            foreach (Transaction t in txes)
            {
                messageSizeLength += 8; // size of the transaction
                messageSizeLength += 1; // transaction type
                messageSizeLength += _decoder.GetLength(t, RlpBehaviors.SkipTypedWrapping);
            }

            l2Message = new byte[messageSizeLength];
            RlpStream stream = new(l2Message);
            stream.WriteByte((byte)ArbitrumL2MessageKind.Batch);
            Span<byte> sizeBuf = stackalloc byte[8];
            foreach (Transaction tx in txes)
            {
                BinaryPrimitives.WriteUInt64BigEndian(sizeBuf,
                    (ulong)_decoder.GetLength(tx, RlpBehaviors.SkipTypedWrapping) + 1);
                stream.Write(sizeBuf);
                stream.WriteByte((byte)ArbitrumL2MessageKind.SignedTx);
                _decoder.Encode(stream, tx, RlpBehaviors.SkipTypedWrapping);
            }
        }

        return l2Message.Length > ArbitrumConstants.MaxL2MessageSize
            ? null
            : new L1IncomingMessage(header, l2Message, null);
    }
}
