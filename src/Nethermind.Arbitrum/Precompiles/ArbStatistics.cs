using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

// ArbStatistics provides statistics about the rollup right before the Nitro upgrade.
// In Classic, this was how a user would get info such as the total number of accounts,
// but there's now better ways to do that with geth.
public static class ArbStatistics
{
    public static readonly string Abi =
        "[{\"inputs\":[],\"name\":\"getStats\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";
    public static Address Address => ArbosAddresses.ArbStatisticsAddress;

    // GetStats returns the current block number and some statistics about the rollup's pre-Nitro state
    public static ArbStatisticsResult GetStats(ArbitrumPrecompileExecutionContext context)
        => new(
            BlockNumber: context.BlockExecutionContext.Number,
            ClassicNumAccounts: 0,
            ClassicStorageSum: 0,
            ClassicGasSum: 0,
            ClassicNumTxes: 0,
            ClassicNumContracts: 0);

    public record struct ArbStatisticsResult(UInt256 BlockNumber, UInt256 ClassicNumAccounts, UInt256 ClassicStorageSum, UInt256 ClassicGasSum, UInt256 ClassicNumTxes, UInt256 ClassicNumContracts);
}
