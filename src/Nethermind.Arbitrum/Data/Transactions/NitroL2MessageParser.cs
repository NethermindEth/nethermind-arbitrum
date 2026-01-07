using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Arbitrum.Precompiles.Abi;

namespace Nethermind.Arbitrum.Data.Transactions;

public static class NitroL2MessageParser
{
    public static IReadOnlyList<Transaction> ParseTransactions(L1IncomingMessage message, ulong chainId, ulong lastArbosVersion, ILogger logger)
    {
        if (message.L2Msg is null || message.L2Msg.Length == 0)
        {
            logger.Warn("L1 message contains no L2 message data.");
            return [];
        }

        if (message.L2Msg.Length > ArbitrumConstants.MaxL2MessageSize)
            throw new ArgumentException($"L2 message size {message.L2Msg.Length} mustn't exceed maximum of {ArbitrumConstants.MaxL2MessageSize}.");

        ReadOnlySpan<byte> l2Message = message.L2Msg.AsSpan();

        try
        {
            switch (message.Header.Kind)
            {
                case ArbitrumL1MessageKind.L2Message:
                    return ParseL2MessageFormat(ref l2Message, message.Header.Sender, message.Header.Timestamp, message.Header.RequestId, chainId, 0, logger);

                case ArbitrumL1MessageKind.L2FundedByL1:
                    return ParseL2FundedByL1(ref l2Message, message.Header, chainId);

                case ArbitrumL1MessageKind.SubmitRetryable:
                    return ParseSubmitRetryable(ref l2Message, message.Header, chainId);

                case ArbitrumL1MessageKind.EthDeposit:
                    return ParseEthDeposit(ref l2Message, message.Header, chainId);

                case ArbitrumL1MessageKind.BatchPostingReport:
                    return ParseBatchPostingReport(ref l2Message, chainId, lastArbosVersion, message);

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
                    if (logger.IsWarn)
                        logger.Warn($"Ignoring L1 message with unknown kind: {message.Header.Kind}");
                    return [];
            }
        }
        catch (Exception ex)
        {
            if (logger.IsWarn)
                logger.Warn($"Error parsing incoming messages for: {message.Header.Kind} - {ex}");
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
        ArbitrumL2MessageKind l2Kind = (ArbitrumL2MessageKind)ArbitrumBinaryReader.ReadByteOrFail(ref data);
        if (!Enum.IsDefined(l2Kind))
            throw new ArgumentException($"L2 message kind {l2Kind} is not defined.");

        switch (l2Kind)
        {
            case ArbitrumL2MessageKind.UnsignedUserTx:
            case ArbitrumL2MessageKind.ContractTx:
                Transaction parsedTx = ParseUnsignedTx(ref data, poster, l1RequestId, chainId, l2Kind);
                return [parsedTx];

            case ArbitrumL2MessageKind.Batch:
                const int maxDepth = 16;
                if (depth >= maxDepth)
                    throw new ArgumentException($"L2 message batch depth exceeds maximum of {maxDepth}");

                List<Transaction> transactions = new();
                UInt256 index = UInt256.Zero;
                while (!data.IsEmpty) // Loop until the span is consumed
                {
                    ReadOnlySpan<byte> nextMsgData = ArbitrumBinaryReader.ReadByteStringOrFail(ref data, ArbitrumConstants.MaxL2MessageSize);

                    Hash256? nextRequestId = null;
                    if (l1RequestId != null)
                    {
                        // Calculate sub-request ID: keccak256(l1RequestId, index)
                        Span<byte> combined = new byte[64];
                        l1RequestId.Bytes.CopyTo(combined[..32]);
                        index.ToBigEndian(combined[32..]);
                        nextRequestId = Keccak.Compute(combined);
                    }

                    transactions.AddRange(ParseL2MessageFormat(ref nextMsgData, poster, timestamp, nextRequestId, chainId, depth + 1, logger));

                    index.Add(UInt256.One, out index);
                }
                return transactions;

            case ArbitrumL2MessageKind.SignedTx:
                Rlp.ValueDecoderContext decoderContext = data.AsRlpValueContext();
                Transaction? legacyTx = TxDecoder.Instance.Decode(ref decoderContext,
                    RlpBehaviors.AllowUnsigned | RlpBehaviors.SkipTypedWrapping | RlpBehaviors.InMempoolForm);

                if (legacyTx is null)
                    throw new ArgumentException($"Unable to deserialize {ArbitrumL2MessageKind.SignedTx} from {data.ToHexString()}");

                if (legacyTx.Type is >= (TxType)ArbitrumTxType.ArbitrumDeposit or TxType.Blob)
                    throw new ArgumentException($"Unsupported transaction type {legacyTx.Type} encountered for {ArbitrumL2MessageKind.SignedTx}.");

                return [legacyTx];

            case ArbitrumL2MessageKind.Heartbeat:
                if (timestamp >= ArbitrumConstants.HeartbeatsDisabledAt)
                    throw new ArgumentException("Heartbeat message received after disable time.");

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
        ulong gasLimit = (ulong)ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        UInt256 maxFeePerGas = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        ulong nonce = kind == ArbitrumL2MessageKind.UnsignedUserTx
            ? (ulong)ArbitrumBinaryReader.ReadBigInteger256OrFail(ref data)
            : 0;

        Address? destination = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data);
        destination = destination == Address.Zero ? null : destination;

        UInt256 value = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

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
            ArbitrumL2MessageKind.ContractTx => l1RequestId is not null
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
        if (header.RequestId is null)
            throw new ArgumentException("Cannot process L2FundedByL1 message without L1 request ID.");

        ArbitrumL2MessageKind kind = (ArbitrumL2MessageKind)ArbitrumBinaryReader.ReadByteOrFail(ref data);
        if (!Enum.IsDefined(kind))
            throw new ArgumentException($"Invalid L2FundedByL1 message kind: {kind}");

        // Calculate request IDs
        // depositRequestId = keccak256(requestId, 0)
        // unsignedRequestId = keccak256(requestId, 1)
        Span<byte> requestBytes = stackalloc byte[64];
        header.RequestId.Bytes.CopyTo(requestBytes[..32]);

        Hash256 depositRequestId = Keccak.Compute(requestBytes);

        requestBytes[63] = 1;
        Hash256 unsignedRequestId = Keccak.Compute(requestBytes);

        Transaction unsignedTx = ParseUnsignedTx(ref data, header.Sender, unsignedRequestId, chainId, kind);
        ArbitrumDepositTransaction depositData = new()
        {
            ChainId = chainId,
            L1RequestId = depositRequestId,
            SenderAddress = Address.Zero,
            To = header.Sender,
            Value = unsignedTx.Value
        };
        Transaction depositTx = ConvertParsedDataToTransaction(depositData);

        return [depositTx, unsignedTx];
    }

    private static List<Transaction> ParseEthDeposit(ref ReadOnlySpan<byte> data, L1IncomingMessageHeader header, ulong chainId)
    {
        if (header.RequestId is null)
            throw new ArgumentException("Cannot process EthDeposit message without L1 request ID.");

        Address to = ArbitrumBinaryReader.ReadAddressOrFail(ref data);
        UInt256 value = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        ArbitrumDepositTransaction depositData = new()
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

        Address? retryTo = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data);
        retryTo = retryTo == Address.Zero ? null : retryTo;

        UInt256 retryValue = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        UInt256 depositValue = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        UInt256 maxSubmissionFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        Address feeRefundAddress = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data);
        Address callvalueRefundAddress = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref data); // Beneficiary

        UInt256 gasLimit256 = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        if (gasLimit256 > ulong.MaxValue)
            throw new ArgumentException("Retryable gas limit overflows ulong.");

        ulong gasLimit = (ulong)gasLimit256;
        UInt256 maxFeePerGas = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        UInt256 dataLength256 = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        if (dataLength256 > ArbitrumConstants.MaxL2MessageSize)
            throw new ArgumentException("Retryable data too large.");

        ReadOnlyMemory<byte> retryData = ArbitrumBinaryReader.ReadBytesOrFail(ref data, (int)dataLength256).ToArray();

        ArbitrumSubmitRetryableTransaction retryableData = new()
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

    private static List<Transaction> ParseBatchPostingReport(ref ReadOnlySpan<byte> data, ulong chainId, ulong lastArbosVersion, L1IncomingMessage message)
    {
        UInt256 batchTimestamp = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        Address batchPosterAddr = ArbitrumBinaryReader.ReadAddressOrFail(ref data);
        _ = ArbitrumBinaryReader.ReadHash256OrFail(ref data); // dataHash is not used directly in tx, but parsed
        UInt256 batchNum256 = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);
        if (batchNum256 > ulong.MaxValue)
            throw new ArgumentException("Batch number overflows ulong.");

        ulong batchNum = (ulong)batchNum256;
        UInt256 l1BaseFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref data);

        // Extra gas is optional in Go, try reading it
        ulong extraGas = 0;
        if (!data.IsEmpty && !ArbitrumBinaryReader.TryReadULongBigEndian(ref data, out extraGas))
            // If reading fails but data is not empty, it's an error
            // Otherwise, EOF is fine, extraGas remains 0
            throw new ArgumentException("Invalid data after L1 base fee in BatchPostingReport.");

        ulong legacyGas;
        if (message.BatchDataStats is not null)
        {
            ulong gas = 4 * (message.BatchDataStats.Length - message.BatchDataStats.NonZeros) + 16 * message.BatchDataStats.NonZeros;
            ulong keccakWords = (message.BatchDataStats.Length + 31) / 32;
            gas += 30 + (keccakWords * 6);
            gas += 2 * 20000;
            legacyGas = gas;
        }
        else
        {
            if (message.BatchGasCost == null)
                throw new ArgumentException("no gas data field in a batch posting report");

            legacyGas = message.BatchGasCost.Value;
        }

        byte[] packedData;
        if (lastArbosVersion < 50)
        {
            ulong batchDataGas = legacyGas.SaturateAdd(extraGas);
            packedData = AbiMetadata.PackInput(AbiMetadata.BatchPostingReport, batchTimestamp, batchPosterAddr, batchNum, batchDataGas,
                l1BaseFee);
        }
        else
        {
            if (message.BatchDataStats is null)
                throw new InvalidOperationException("no gas data stats in a batch posting report post arbos 50");

            packedData = AbiMetadata.PackInput(
                AbiMetadata.BatchPostingReportV2,
                batchTimestamp,
                batchPosterAddr,
                batchNum,
                message.BatchDataStats!.Length,
                message.BatchDataStats!.NonZeros,
                extraGas,
                l1BaseFee
            );
        }
        ArbitrumInternalTransaction internalTxParsed = new()
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
}
