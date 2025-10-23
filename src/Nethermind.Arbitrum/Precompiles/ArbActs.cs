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
public static class ArbActs
{
    public static Address Address => ArbosAddresses.ArbosAddress;

    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"l1BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"l2BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"timePassed\",\"type\":\"uint64\"}],\"name\":\"startBlock\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchDataGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReport\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchCallDataLength\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchCallDataNonZeros\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchExtraGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReportV2\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"CallerNotArbOS\",\"type\":\"error\"}]";

    public static readonly AbiErrorDescription CallerNotArbOS;

    static ArbActs()
    {
        Dictionary<string, AbiErrorDescription> allErrors = AbiMetadata.GetAllErrorDescriptions(Abi);
        CallerNotArbOS = allErrors["CallerNotArbOS"];
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
