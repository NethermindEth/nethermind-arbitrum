// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbosActs precompile represents ArbOS's internal actions as calls it makes to itself.
/// Calling this precompile will always revert and should not be done.
/// </summary>
public static class ArbosActs
{
    public const string Abi = Solgen.ArbosActs.Abi;

    public static Address Address => ArbosAddresses.ArbosAddress;

    public static readonly AbiErrorDescription CallerNotArbOS;

    static ArbosActs()
    {
        CallerNotArbOS = Solgen.ArbosActs.Errors.CallerNotArbOS.ToAbiErrorDescription();
    }

    public static void StartBlock(
        ArbitrumPrecompileExecutionContext context,
        UInt256 l1BaseFee,
        ulong l1BlockNumber,
        ulong l2BlockNumber,
        ulong timePassed)
    {
        throw CallerNotArbOSSolidityError();
    }

    public static void BatchPostingReport(
        ArbitrumPrecompileExecutionContext context,
        UInt256 batchTimestamp,
        Address batchPosterAddress,
        ulong batchNumber,
        ulong batchDataGas,
        UInt256 l1BaseFeeWei)
    {
        throw CallerNotArbOSSolidityError();
    }

    public static void BatchPostingReportV2(
        ArbitrumPrecompileExecutionContext context,
        UInt256 batchTimestamp,
        Address batchPosterAddress,
        ulong batchNumber,
        ulong batchCallDataLength,
        ulong batchCallDataNonZeros,
        ulong batchExtraGas,
        UInt256 l1BaseFeeWei)
    {
        throw CallerNotArbOSSolidityError();
    }

    public static ArbitrumPrecompileException CallerNotArbOSSolidityError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(CallerNotArbOS.Name, CallerNotArbOS.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }
}
