using System.Text.Json;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

// ArbOwner precompile provides owners with tools for managing the rollup.
// All calls to this precompile are authorized by the OwnerPrecompile wrapper,
// which ensures only a chain owner can access these methods. For methods that
// are safe for non-owners to call, see ArbOwnerOld
public static class ArbOwner
{
    public static Address Address => ArbosAddresses.ArbOwnerAddress;

    public static readonly string Abi =
        "[{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes4\",\"name\":\"method\",\"type\":\"bytes4\"},{\"indexed\":true,\"internalType\":\"address\",\"name\":\"owner\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"bytes\",\"name\":\"data\",\"type\":\"bytes\"}],\"name\":\"OwnerActs\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"newOwner\",\"type\":\"address\"}],\"name\":\"addChainOwner\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"newOwner\",\"type\":\"address\"}],\"name\":\"addNativeTokenOwner\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"manager\",\"type\":\"address\"}],\"name\":\"addWasmCacheManager\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getAllChainOwners\",\"outputs\":[{\"internalType\":\"address[]\",\"name\":\"\",\"type\":\"address[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getAllNativeTokenOwners\",\"outputs\":[{\"internalType\":\"address[]\",\"name\":\"\",\"type\":\"address[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getInfraFeeAccount\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getNetworkFeeAccount\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"isChainOwner\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"isNativeTokenOwner\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"maxWeiToRelease\",\"type\":\"uint256\"}],\"name\":\"releaseL1PricerSurplusFunds\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"ownerToRemove\",\"type\":\"address\"}],\"name\":\"removeChainOwner\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"ownerToRemove\",\"type\":\"address\"}],\"name\":\"removeNativeTokenOwner\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"manager\",\"type\":\"address\"}],\"name\":\"removeWasmCacheManager\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"newVersion\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"timestamp\",\"type\":\"uint64\"}],\"name\":\"scheduleArbOSUpgrade\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"cap\",\"type\":\"uint64\"}],\"name\":\"setAmortizedCostCapBips\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"level\",\"type\":\"uint64\"}],\"name\":\"setBrotliCompressionLevel\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bool\",\"name\":\"enable\",\"type\":\"bool\"}],\"name\":\"setCalldataPriceIncrease\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"string\",\"name\":\"chainConfig\",\"type\":\"string\"}],\"name\":\"setChainConfig\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"newInfraFeeAccount\",\"type\":\"address\"}],\"name\":\"setInfraFeeAccount\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint32\",\"name\":\"price\",\"type\":\"uint32\"}],\"name\":\"setInkPrice\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"inertia\",\"type\":\"uint64\"}],\"name\":\"setL1BaseFeeEstimateInertia\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"pricePerUnit\",\"type\":\"uint256\"}],\"name\":\"setL1PricePerUnit\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"equilibrationUnits\",\"type\":\"uint256\"}],\"name\":\"setL1PricingEquilibrationUnits\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"inertia\",\"type\":\"uint64\"}],\"name\":\"setL1PricingInertia\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"weiPerUnit\",\"type\":\"uint64\"}],\"name\":\"setL1PricingRewardRate\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"recipient\",\"type\":\"address\"}],\"name\":\"setL1PricingRewardRecipient\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"priceInWei\",\"type\":\"uint256\"}],\"name\":\"setL2BaseFee\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"sec\",\"type\":\"uint64\"}],\"name\":\"setL2GasBacklogTolerance\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"sec\",\"type\":\"uint64\"}],\"name\":\"setL2GasPricingInertia\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"limit\",\"type\":\"uint64\"}],\"name\":\"setMaxBlockGasLimit\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"limit\",\"type\":\"uint64\"}],\"name\":\"setMaxTxGasLimit\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"priceInWei\",\"type\":\"uint256\"}],\"name\":\"setMinimumL2BaseFee\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"timestamp\",\"type\":\"uint64\"}],\"name\":\"setNativeTokenManagementFrom\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"newNetworkFeeAccount\",\"type\":\"address\"}],\"name\":\"setNetworkFeeAccount\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"int64\",\"name\":\"cost\",\"type\":\"int64\"}],\"name\":\"setPerBatchGasCharge\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"limit\",\"type\":\"uint64\"}],\"name\":\"setSpeedLimit\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"count\",\"type\":\"uint16\"}],\"name\":\"setWasmBlockCacheSize\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"_days\",\"type\":\"uint16\"}],\"name\":\"setWasmExpiryDays\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"pages\",\"type\":\"uint16\"}],\"name\":\"setWasmFreePages\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"percent\",\"type\":\"uint64\"}],\"name\":\"setWasmInitCostScalar\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"_days\",\"type\":\"uint16\"}],\"name\":\"setWasmKeepaliveDays\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint32\",\"name\":\"size\",\"type\":\"uint32\"}],\"name\":\"setWasmMaxSize\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint32\",\"name\":\"depth\",\"type\":\"uint32\"}],\"name\":\"setWasmMaxStackDepth\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint8\",\"name\":\"gas\",\"type\":\"uint8\"},{\"internalType\":\"uint16\",\"name\":\"cached\",\"type\":\"uint16\"}],\"name\":\"setWasmMinInitGas\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"gas\",\"type\":\"uint16\"}],\"name\":\"setWasmPageGas\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"limit\",\"type\":\"uint16\"}],\"name\":\"setWasmPageLimit\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";
    public const ulong NativeTokenEnableDelay = 7 * 24 * 60 * 60; // 1 week in seconds

    public static readonly AbiEventDescription OwnerActsEvent;

    static ArbOwner()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi)!;
        OwnerActsEvent = allEvents["OwnerActs"];
    }

    // AddChainOwner adds a new owner to the chain
    public static void AddChainOwner(ArbitrumPrecompileExecutionContext context, Address newOwner)
    {
        context.ArbosState.ChainOwners.Add(newOwner);
    }

    // RemoveChainOwner removes an owner from the list of chain owners
    public static void RemoveChainOwner(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        if (!IsChainOwner(context, owner))
            throw ArbitrumPrecompileException.CreateFailureException("Tried to remove non-owner");

        context.ArbosState.ChainOwners.Remove(owner, context.ArbosState.CurrentArbosVersion);
    }

    // IsChainOwner checks if the account is a chain owner
    public static bool IsChainOwner(ArbitrumPrecompileExecutionContext context, Address account)
    {
        return context.ArbosState.ChainOwners.IsMember(account);
    }

    // GetAllChainOwners retrieves the list of chain owners
    public static Address[] GetAllChainOwners(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.ChainOwners.AllMembers(AddressSet.MaxNumberOfOwners);
    }

    // SetNativeTokenManagementFrom sets a time in epoch seconds when the native token
    // management becomes enabled. Setting it to 0 disables the feature.
    // If the feature is currently disabled, then the time must be at least 7 days in the future.
    public static void SetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context, ulong timestamp)
    {
        if (timestamp == 0)
        {
            context.ArbosState.NativeTokenEnabledTime.Set(timestamp);
            return;
        }

        ulong currentEnabledTime = context.ArbosState.NativeTokenEnabledTime.Get();

        ulong now = context.BlockExecutionContext.Header.Timestamp;
        ulong sevenDaysFromNow = now + NativeTokenEnableDelay;

        // If the feature is disabled, then the time must be at least 7 days in the future.
        // If the feature is already scheduled to be enabled in more than 7 days,
        // then the new time must be at least 7 days in the future.
        if ((currentEnabledTime == 0 && timestamp < sevenDaysFromNow) ||
            (currentEnabledTime > sevenDaysFromNow && timestamp < sevenDaysFromNow))
            throw ArbitrumPrecompileException.CreateFailureException("native token feature must be enabled at least 7 days in the future");

        // If the feature is scheduled to be enabled earlier than the minimum delay,
        // then the new time to enable it must be only further in the future.
        if (currentEnabledTime > now && currentEnabledTime <= sevenDaysFromNow && timestamp < currentEnabledTime)
            throw ArbitrumPrecompileException.CreateFailureException("native token feature cannot be updated to a time earlier than the current time at which it is scheduled to be enabled");

        context.ArbosState.NativeTokenEnabledTime.Set(timestamp);
    }

    // AddNativeTokenOwner adds account as a native token owner
    public static void AddNativeTokenOwner(ArbitrumPrecompileExecutionContext context, Address newOwner)
    {
        ulong currentEnabledTime = context.ArbosState.NativeTokenEnabledTime.Get();

        ulong now = context.BlockExecutionContext.Header.Timestamp;
        if (currentEnabledTime == 0 || currentEnabledTime > now)
            throw ArbitrumPrecompileException.CreateFailureException("native token feature is not enabled yet");

        context.ArbosState.NativeTokenOwners.Add(newOwner);
    }

    // RemoveNativeTokenOwner removes account from the list of native token owners
    public static void RemoveNativeTokenOwner(ArbitrumPrecompileExecutionContext context, Address account)
    {
        if (!IsNativeTokenOwner(context, account))
            throw ArbitrumPrecompileException.CreateFailureException("Tried to remove non native token owner");

        context.ArbosState.NativeTokenOwners.Remove(account, context.ArbosState.CurrentArbosVersion);
    }

    // IsNativeTokenOwner checks if the account is a native token owner
    public static bool IsNativeTokenOwner(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        return context.ArbosState.NativeTokenOwners.IsMember(owner);
    }

    // GetAllNativeTokenOwners retrieves the list of native token owners
    public static Address[] GetAllNativeTokenOwners(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.NativeTokenOwners.AllMembers(AddressSet.MaxNumberOfOwners);
    }

    // SetL1BaseFeeEstimateInertia sets how slowly ArbOS updates its estimate of the L1 basefee
    public static void SetL1BaseFeeEstimateInertia(ArbitrumPrecompileExecutionContext context, ulong inertia)
    {
        context.ArbosState.L1PricingState.SetInertia(inertia);
    }

    // SetL2BaseFee sets the L2 gas price directly, bypassing the pool calculus
    public static void SetL2BaseFee(ArbitrumPrecompileExecutionContext context, UInt256 l2BaseFee)
    {
        context.ArbosState.L2PricingState.SetBaseFeeWei(l2BaseFee);
    }

    // SetMinimumL2BaseFee sets the minimum base fee needed for a transaction to succeed
    public static void SetMinimumL2BaseFee(ArbitrumPrecompileExecutionContext context, UInt256 priceInWei)
    {
        //TODO: check TxRunMode here, also shouldn't it be a || instead of && here ?
        bool isCallNonMutating = false;
        if (isCallNonMutating && priceInWei == 0)
            throw ArbitrumPrecompileException.CreateFailureException("Minimum base fee must be nonzero");

        context.ArbosState.L2PricingState.SetMinBaseFeeWei(priceInWei);
    }

    // SetSpeedLimit sets the computational speed limit for the chain
    public static void SetSpeedLimit(ArbitrumPrecompileExecutionContext context, ulong limit)
    {
        if (limit == 0)
            throw ArbitrumPrecompileException.CreateFailureException("speed limit must be nonzero");

        context.ArbosState.L2PricingState.SetSpeedLimitPerSecond(limit);
    }

    public static void SetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context, ulong limit)
    {
        // Before ArbOS 50: SetMaxTxGasLimit controlled the block limit (backward compatibility)
        // After ArbOS 50: SetMaxTxGasLimit controls the per-transaction limit
        if (context.ArbosState.CurrentArbosVersion < ArbosVersion.Fifty)
        {
            context.ArbosState.L2PricingState.SetMaxPerBlockGasLimit(limit);
        }
        else
        {
            context.ArbosState.L2PricingState.SetMaxPerTxGasLimit(limit);
        }
    }

    public static void SetMaxBlockGasLimit(ArbitrumPrecompileExecutionContext context, ulong limit)
    {
        context.ArbosState.L2PricingState.SetMaxPerBlockGasLimit(limit);
    }

    // SetL2GasPricingInertia sets the L2 gas pricing inertia
    public static void SetL2GasPricingInertia(ArbitrumPrecompileExecutionContext context, ulong sec)
    {
        if (sec == 0)
            throw ArbitrumPrecompileException.CreateFailureException("price inertia must be nonzero");

        context.ArbosState.L2PricingState.SetPricingInertia(sec);
    }

    // SetL2GasBacklogTolerance sets the L2 gas backlog tolerance
    public static void SetL2GasBacklogTolerance(ArbitrumPrecompileExecutionContext context, ulong sec)
    {
        context.ArbosState.L2PricingState.SetBacklogTolerance(sec);
    }

    // GetNetworkFeeAccount gets the network fee collector
    public static Address GetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.NetworkFeeAccount.Get();
    }

    // GetInfraFeeAccount gets the infrastructure fee collector
    public static Address GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.InfraFeeAccount.Get();
    }

    // SetNetworkFeeAccount sets the network fee collector to the new network fee account
    public static void SetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, Address newNetworkFeeAccount)
    {
        context.ArbosState.NetworkFeeAccount.Set(newNetworkFeeAccount);
    }

    // SetInfraFeeAccount sets the infra fee collector to the new network fee account
    public static void SetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, Address newInfraFeeAccount)
    {
        context.ArbosState.InfraFeeAccount.Set(newInfraFeeAccount);
    }

    // ScheduleArbOSUpgrade to the requested version at the requested timestamp
    public static void ScheduleArbOSUpgrade(ArbitrumPrecompileExecutionContext context, ulong version, ulong timestamp)
    {
        context.ArbosState.ScheduleArbOSUpgrade(version, timestamp);
    }

    // Sets equilibration units parameter for L1 price adjustment algorithm
    public static void SetL1PricingEquilibrationUnits(ArbitrumPrecompileExecutionContext context, UInt256 units)
    {
        context.ArbosState.L1PricingState.SetEquilibrationUnits(units);
    }

    // Sets inertia parameter for L1 price adjustment algorithm
    public static void SetL1PricingInertia(ArbitrumPrecompileExecutionContext context, ulong inertia)
    {
        context.ArbosState.L1PricingState.SetInertia(inertia);
    }

    // Sets reward recipient address for L1 price adjustment algorithm
    public static void SetL1PricingRewardRecipient(ArbitrumPrecompileExecutionContext context, Address recipient)
    {
        context.ArbosState.L1PricingState.SetPayRewardsTo(recipient);
    }

    // Sets reward amount for L1 price adjustment algorithm, in wei per unit
    public static void SetL1PricingRewardRate(ArbitrumPrecompileExecutionContext context, ulong weiPerUnit)
    {
        context.ArbosState.L1PricingState.SetPerUnitReward(weiPerUnit);
    }

    // Set how much ArbOS charges per L1 gas spent on transaction data.
    public static void SetL1PricePerUnit(ArbitrumPrecompileExecutionContext context, UInt256 pricePerUnit)
    {
        context.ArbosState.L1PricingState.SetPricePerUnit(pricePerUnit);
    }

    // Sets the base charge (in L1 gas) attributed to each data batch in the calldata pricer
    public static void SetPerBatchGasCharge(ArbitrumPrecompileExecutionContext context, ulong baseCharge)
    {
        context.ArbosState.L1PricingState.SetPerBatchGasCost(baseCharge);
    }

    // Sets the cost amortization cap in basis points
    public static void SetAmortizedCostCapBips(ArbitrumPrecompileExecutionContext context, ulong cap)
    {
        context.ArbosState.L1PricingState.SetAmortizedCostCapBips(cap);
    }

    // Sets the Brotli compression level used for fast compression
    // Available in ArbOS version 12 with default level as 1
    public static void SetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context, ulong level)
    {
        context.ArbosState.SetBrotliCompressionLevel(level);
    }

    // Releases surplus funds from L1PricerFundsPoolAddress for use
    public static UInt256 ReleaseL1PricerSurplusFunds(ArbitrumPrecompileExecutionContext context, UInt256 maxWeiToRelease)
    {
        UInt256 poolBalance = context.WorldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
        UInt256 recognized = context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get();

        if (UInt256.SubtractUnderflow(poolBalance, recognized, out UInt256 weiToTransfer))
            return 0;

        if (weiToTransfer > maxWeiToRelease)
            weiToTransfer = maxWeiToRelease;

        context.ArbosState.L1PricingState.AddToL1FeesAvailable(weiToTransfer);

        return weiToTransfer;
    }

    // Sets the amount of ink 1 gas buys
    public static void SetInkPrice(ArbitrumPrecompileExecutionContext context, uint inkPrice)
    {
        if (inkPrice == 0 || inkPrice > StylusParams.MaxInkPrice)
            throw ArbitrumPrecompileException.CreateFailureException("ink price must be a positive uint24");

        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetInkPrice(inkPrice);
        stylusParams.Save();
    }

    // Sets the maximum depth (in wasm words) a wasm stack may grow
    public static void SetWasmMaxStackDepth(ArbitrumPrecompileExecutionContext context, uint maxStackDepth)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetMaxStackDepth(maxStackDepth);
        stylusParams.Save();
    }

    // Gets the number of free wasm pages a tx gets
    public static void SetWasmFreePages(ArbitrumPrecompileExecutionContext context, ushort freePages)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetFreePages(freePages);
        stylusParams.Save();
    }

    // Sets the base cost of each additional wasm page
    public static void SetWasmPageGas(ArbitrumPrecompileExecutionContext context, ushort pageGas)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetPageGas(pageGas);
        stylusParams.Save();
    }

    // Sets the initial number of pages a wasm may allocate
    public static void SetWasmPageLimit(ArbitrumPrecompileExecutionContext context, ushort pageCountLimit)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetPageLimit(pageCountLimit);
        stylusParams.Save();
    }

    // Sets the minimum costs to invoke a program
    public static void SetWasmMinInitGas(ArbitrumPrecompileExecutionContext context, ulong gas, ulong cached)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();

        byte minInitGas = byte.CreateSaturating(Utils.DivCeiling(gas, StylusParams.MinInitGasUnits));
        stylusParams.SetMinInitGas(minInitGas);

        byte minCachedInitGas = byte.CreateSaturating(Utils.DivCeiling(cached, StylusParams.MinCachedGasUnits));
        stylusParams.SetMinCachedInitGas(minCachedInitGas);

        stylusParams.Save();
    }

    // Sets the linear adjustment made to program init costs
    public static void SetWasmInitCostScalar(ArbitrumPrecompileExecutionContext context, ulong percent)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();

        byte initCostScalar = byte.CreateSaturating(Utils.DivCeiling(percent, StylusParams.CostScalarPercent));
        stylusParams.SetInitCostScalar(initCostScalar);

        stylusParams.Save();
    }

    // Sets the number of days after which programs deactivate
    public static void SetWasmExpiryDays(ArbitrumPrecompileExecutionContext context, ushort expiryDays)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetExpiryDays(expiryDays);
        stylusParams.Save();
    }

    // Sets the age a program must be to perform a keepalive
    public static void SetWasmKeepaliveDays(ArbitrumPrecompileExecutionContext context, ushort keepaliveDays)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetKeepaliveDays(keepaliveDays);
        stylusParams.Save();
    }

    // Sets the number of extra programs ArbOS caches during a given block
    public static void SetWasmBlockCacheSize(ArbitrumPrecompileExecutionContext context, ushort blockCacheSize)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetBlockCacheSize(blockCacheSize);
        stylusParams.Save();
    }

    // SetMaxWasmSize sets the maximum size the wasm code can be in bytes after compression
    public static void SetWasmMaxSize(ArbitrumPrecompileExecutionContext context, uint maxWasmSize)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetWasmMaxSize(maxWasmSize);
        stylusParams.Save();
    }

    // Adds account as a wasm cache manager
    public static void AddWasmCacheManager(ArbitrumPrecompileExecutionContext context, Address manager)
    {
        context.ArbosState.Programs.CacheManagersStorage.Add(manager);
    }

    // Removes account from the list of wasm cache managers
    public static void RemoveWasmCacheManager(ArbitrumPrecompileExecutionContext context, Address manager)
    {
        if (!context.ArbosState.Programs.CacheManagersStorage.IsMember(manager))
            throw ArbitrumPrecompileException.CreateFailureException("Tried to remove non-manager");

        context.ArbosState.Programs.CacheManagersStorage.Remove(manager, context.ArbosState.CurrentArbosVersion);
    }

    // Sets serialized chain config in ArbOS state
    public static void SetChainConfig(ArbitrumPrecompileExecutionContext context, byte[] serializedChainConfig)
    {
        //TODO: add support for TxRunMode
        // issue: https://github.com/NethermindEth/nethermind-arbitrum/issues/108
        bool isCallNonMutating = false;
        if (isCallNonMutating)
        {
            ChainConfig chainConfigSpec = JsonSerializer.Deserialize<ChainConfig>(serializedChainConfig)
                ?? throw ArbitrumPrecompileException.CreateFailureException("Failed to deserialize new chain config");

            if (chainConfigSpec.ChainId == 0)
                throw ArbitrumPrecompileException.CreateFailureException("Invalid chain config: missing chain id");

            if (chainConfigSpec.ChainId != context.ArbosState.ChainId.Get())
                throw ArbitrumPrecompileException.CreateFailureException($"Invalid chain config: chain id mismatch, want {context.ArbosState.ChainId.Get()}, got {chainConfigSpec.ChainId}");

            byte[] currentConfig = context.ArbosState.ChainConfigStorage.Get();
            if (serializedChainConfig.Equals(currentConfig))
                throw ArbitrumPrecompileException.CreateFailureException("New chain config is the same as old one in ArbOS state");

            if (currentConfig.Length != 0)
            {
                ChainConfig currentChainConfig = JsonSerializer.Deserialize<ChainConfig>(currentConfig)
                ?? throw ArbitrumPrecompileException.CreateFailureException("Failed to deserialize current chain config");

                currentChainConfig.CheckCompatibilityWith(
                    chainConfigSpec,
                    context.BlockExecutionContext.Number,
                    context.BlockExecutionContext.Header.Timestamp
                );
            }

            //TODO: CheckCompatible with evm chain config?
        }

        context.ArbosState.ChainConfigStorage.Set(serializedChainConfig);
    }

    // SetCalldataPriceIncrease sets the increased calldata price feature on or off (EIP-7623)
    public static void SetCalldataPriceIncrease(ArbitrumPrecompileExecutionContext context, bool enabled)
    {
        context.ArbosState.Features.SetCalldataPriceIncrease(enabled);
    }
}
