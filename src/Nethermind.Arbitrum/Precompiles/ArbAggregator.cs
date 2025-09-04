using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbAggregator provides aggregators and their users methods for configuring how they participate in L1 aggregation.
/// Arbitrum One's default aggregator is the Sequencer, which a user will prefer unless SetPreferredAggregator()
/// is invoked to change it.
/// </summary>
public static class ArbAggregator
{
    public static Address Address
        => ArbosAddresses.ArbAggregatorAddress;

    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"address\",\"name\":\"newBatchPoster\",\"type\":\"address\"}],\"name\":\"addBatchPoster\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"batchPoster\",\"type\":\"address\"}],\"name\":\"getFeeCollector\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"getPreferredAggregator\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"},{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getDefaultAggregator\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getBatchPosters\",\"outputs\":[{\"internalType\":\"address[]\",\"name\":\"\",\"type\":\"address[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"newFeeCollector\",\"type\":\"address\"}],\"name\":\"setFeeCollector\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"aggregator\",\"type\":\"address\"}],\"name\":\"getTxBaseFee\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"aggregator\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"feeInL1Gas\",\"type\":\"uint256\"}],\"name\":\"setTxBaseFee\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

    /// <summary>
    /// Gets the preferred aggregator for a given address.
    /// If the address hasn't set a preferred aggregator, returns the default aggregator.
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <param name="addr">The address to query</param>
    /// <returns>Tuple of (aggregator address, is default)</returns>
    public static (Address aggregator, bool isDefault) GetPreferredAggregator(ArbitrumPrecompileExecutionContext context, Address addr)
        => (ArbosAddresses.BatchPosterAddress, true);

    /// <summary>
    /// Gets the default aggregator address.
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <returns>The default aggregator address</returns>
    public static Address GetDefaultAggregator(ArbitrumPrecompileExecutionContext context)
        => ArbosAddresses.BatchPosterAddress;

    /// <summary>
    /// Gets the list of batch posters.
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <returns>Array of batch poster addresses</returns>
    public static Address[] GetBatchPosters(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.BatchPosterTable.GetAllPosters(65536).ToArray();

    /// <summary>
    /// Adds a new batch poster. Only callable by chain owner.
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <param name="newBatchPoster">The address to add as a batch poster</param>
    public static void AddBatchPoster(ArbitrumPrecompileExecutionContext context, Address newBatchPoster)
    {
        if (!context.ArbosState.ChainOwners.IsMember(context.Caller))
            throw new InvalidOperationException("must be called by chain owner");

        BatchPostersTable batchPosters = context.ArbosState.L1PricingState.BatchPosterTable;

        if (!batchPosters.ContainsPoster(newBatchPoster))
            batchPosters.AddPoster(newBatchPoster, newBatchPoster);
    }

    /// <summary>
    /// Gets the fee collector for a given batch poster.
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <param name="batchPoster">The batch poster address</param>
    /// <returns>The fee collector address</returns>
    public static Address GetFeeCollector(ArbitrumPrecompileExecutionContext context, Address batchPoster)
    {
        BatchPostersTable batchPosters = context.ArbosState.L1PricingState.BatchPosterTable;

        // This will throw InvalidOperationException if the poster doesn't exist (matches Go behavior)
        BatchPostersTable.BatchPoster poster = batchPosters.OpenPoster(batchPoster, false);
        return poster.GetPayTo();
    }

    /// <summary>
    /// Sets the fee collector for a specific batch poster.
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <param name="batchPoster">The batch poster address</param>
    /// <param name="newFeeCollector">The new fee collector address</param>
    public static void SetFeeCollector(ArbitrumPrecompileExecutionContext context, Address batchPoster, Address newFeeCollector)
    {
        BatchPostersTable batchPosters = context.ArbosState.L1PricingState.BatchPosterTable;
        BatchPostersTable.BatchPoster posterInfo = batchPosters.OpenPoster(batchPoster, false);
        Address oldFeeCollector = posterInfo.GetPayTo();
        Address caller = context.Caller;
        if (caller != batchPoster && caller != oldFeeCollector && !context.ArbosState.ChainOwners.IsMember(caller))
            throw new InvalidOperationException("only a batch poster (or its fee collector / chain owner) may change its fee collector");
        posterInfo.SetPayTo(newFeeCollector);
    }

    /// <summary>
    /// Gets the transaction base fee for an aggregator (deprecated, always returns 0).
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <param name="aggregator">The aggregator address</param>
    /// <returns>Always returns 0</returns>
    public static UInt256 GetTxBaseFee(ArbitrumPrecompileExecutionContext context, Address aggregator)
        => UInt256.Zero;

    /// <summary>
    /// Sets the transaction base fee for an aggregator (deprecated, does nothing).
    /// </summary>
    /// <param name="context">The execution context</param>
    /// <param name="aggregator">The aggregator address</param>
    /// <param name="feeInL1Gas">The fee in L1 gas (ignored)</param>
    public static void SetTxBaseFee(ArbitrumPrecompileExecutionContext context, Address aggregator, UInt256 feeInL1Gas)
    {
        // This method is deprecated and does nothing
    }
}
