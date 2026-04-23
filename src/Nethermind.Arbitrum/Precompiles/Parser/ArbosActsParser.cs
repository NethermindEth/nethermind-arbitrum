// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbosActsParser : IArbitrumPrecompile<ArbosActsParser>
{
    public static readonly ArbosActsParser Instance = new();

    public static Address Address { get; } = ArbosActs.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbosActs.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint _startBlockId = Solgen.ArbosActs.Methods.StartBlock;
    private const uint _batchPostingReportId = Solgen.ArbosActs.Methods.BatchPostingReport;
    private const uint _batchPostingReportV2Id = Solgen.ArbosActs.Methods.BatchPostingReportV2;

    static ArbosActsParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _startBlockId, StartBlock },
            { _batchPostingReportId, BatchPostingReport },
            { _batchPostingReportV2Id, BatchPostingReportV2 },
        }.ToFrozenDictionary();
    }

    private static byte[] StartBlock(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_startBlockId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 l1BaseFee = (UInt256)decoded[0];
        ulong l1BlockNumber = (ulong)decoded[1];
        ulong l2BlockNumber = (ulong)decoded[2];
        ulong timePassed = (ulong)decoded[3];

        ArbosActs.StartBlock(context, l1BaseFee, l1BlockNumber, l2BlockNumber, timePassed);

        return Array.Empty<byte>();
    }

    private static byte[] BatchPostingReport(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_batchPostingReportId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 batchTimestamp = (UInt256)decoded[0];
        Address batchPosterAddress = (Address)decoded[1];
        ulong batchNumber = (ulong)decoded[2];
        ulong batchDataGas = (ulong)decoded[3];
        UInt256 l1BaseFeeWei = (UInt256)decoded[4];

        ArbosActs.BatchPostingReport(context, batchTimestamp, batchPosterAddress, batchNumber, batchDataGas, l1BaseFeeWei);

        return Array.Empty<byte>();
    }

    private static byte[] BatchPostingReportV2(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_batchPostingReportV2Id].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 batchTimestamp = (UInt256)decoded[0];
        Address batchPosterAddress = (Address)decoded[1];
        ulong batchNumber = (ulong)decoded[2];
        ulong batchCallDataLength = (ulong)decoded[3];
        ulong batchCallDataNonZeros = (ulong)decoded[4];
        ulong batchExtraGas = (ulong)decoded[5];
        UInt256 l1BaseFeeWei = (UInt256)decoded[6];

        ArbosActs.BatchPostingReportV2(context, batchTimestamp, batchPosterAddress, batchNumber, batchCallDataLength, batchCallDataNonZeros, batchExtraGas, l1BaseFeeWei);

        return Array.Empty<byte>();
    }
}
