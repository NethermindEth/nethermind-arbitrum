// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Int256;
using System.Text.Json;

namespace Nethermind.Arbitrum.Precompiles;

// ArbOwner precompile provides owners with tools for managing the rollup.
// All calls to this precompile are authorized by the OwnerPrecompile wrapper,
// which ensures only a chain owner can access these methods. For methods that
// are safe for non-owners to call, see ArbOwnerOld
public static class ArbOwner
{
    public static Address Address => ArbosAddresses.ArbOwnerAddress;

    public const ulong NativeTokenEnableDelay = 7 * 24 * 60 * 60; // 1 week in seconds

    public static readonly AbiEventDescription OwnerActsEvent = Solgen.ArbOwner.Events.OwnerActs.ToAbiEventDescription();
    public static readonly AbiEventDescription ChainOwnerAddedEvent = Solgen.ArbOwner.Events.ChainOwnerAdded.ToAbiEventDescription();
    public static readonly AbiEventDescription ChainOwnerRemovedEvent = Solgen.ArbOwner.Events.ChainOwnerRemoved.ToAbiEventDescription();
    public static readonly AbiEventDescription NativeTokenOwnerAddedEvent = Solgen.ArbOwner.Events.NativeTokenOwnerAdded.ToAbiEventDescription();
    public static readonly AbiEventDescription NativeTokenOwnerRemovedEvent = Solgen.ArbOwner.Events.NativeTokenOwnerRemoved.ToAbiEventDescription();
    public static readonly AbiEventDescription TransactionFiltererAddedEvent = Solgen.ArbOwner.Events.TransactionFiltererAdded.ToAbiEventDescription();
    public static readonly AbiEventDescription TransactionFiltererRemovedEvent = Solgen.ArbOwner.Events.TransactionFiltererRemoved.ToAbiEventDescription();
    public static readonly AbiEventDescription FilteredFundsRecipientSetEvent = Solgen.ArbOwner.Events.FilteredFundsRecipientSet.ToAbiEventDescription();

    // AddChainOwner adds a new owner to the chain
    public static void AddChainOwner(ArbitrumPrecompileExecutionContext context, Address newOwner)
    {
        context.ArbosState.ChainOwners.Add(newOwner);
        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Sixty)
            EmitChainOwnerAddedEvent(context, newOwner);
    }

    // RemoveChainOwner removes an owner from the list of chain owners
    public static void RemoveChainOwner(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        if (!IsChainOwner(context, owner))
            throw ArbitrumPrecompileException.CreateFailureException("Tried to remove non-owner");

        context.ArbosState.ChainOwners.Remove(owner, context.ArbosState.CurrentArbosVersion);
        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Sixty)
            EmitChainOwnerRemovedEvent(context, owner);
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
        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Sixty)
            EmitNativeTokenOwnerAddedEvent(context, newOwner);
    }

    // RemoveNativeTokenOwner removes account from the list of native token owners
    public static void RemoveNativeTokenOwner(ArbitrumPrecompileExecutionContext context, Address account)
    {
        if (!IsNativeTokenOwner(context, account))
            throw ArbitrumPrecompileException.CreateFailureException("Tried to remove non native token owner");

        context.ArbosState.NativeTokenOwners.Remove(account, context.ArbosState.CurrentArbosVersion);
        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Sixty)
            EmitNativeTokenOwnerRemovedEvent(context, account);
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

    // SetMaxTxGasLimit sets the maximum size a tx (and block) can be
    public static void SetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context, ulong limit)
    {
        // Before ArbOS 50: SetMaxTxGasLimit controlled the block limit (backward compatibility)
        // After ArbOS 50: SetMaxTxGasLimit controls the per-transaction limit
        if (context.ArbosState.CurrentArbosVersion < ArbosVersion.Fifty)
            context.ArbosState.L2PricingState.SetMaxPerBlockGasLimit(limit);
        else
            context.ArbosState.L2PricingState.SetMaxPerTxGasLimit(limit);
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

    public static void SetParentGasFloorPerToken(ArbitrumPrecompileExecutionContext context, ulong floorPerToken)
    {
        context.ArbosState.L1PricingState.SetParentGasFloorPerToken(floorPerToken);
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

    // SetMaxStylusContractFragments sets the maximum number of fragments a fragmented Stylus root contract may list
    public static void SetMaxStylusContractFragments(ArbitrumPrecompileExecutionContext context, byte maxFragments)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetMaxFragmentCount(maxFragments);
        stylusParams.Save();
    }

    // SetCollectTips enables or disables sequencer tip collection. Available from ArbOS version 60.
    // When enabled, sequenced (non-delayed) transaction tips are credited to the network fee account
    // on their compute-gas portion. When disabled (the default), tips are dropped.
    public static void SetCollectTips(ArbitrumPrecompileExecutionContext context, bool collectTips)
    {
        context.ArbosState.SetCollectTips(collectTips);
    }

    // SetWasmActivationGas sets the constant gas charge applied before each Stylus contract activation.
    // Defaults to zero. Can be raised to deter DOS via activations, or set to a value exceeding
    // the block gas limit to block all activations entirely. Available from ArbOS version 59.
    public static void SetWasmActivationGas(ArbitrumPrecompileExecutionContext context, ulong gas)
    {
        context.ArbosState.Programs.SetActivationGas(gas);
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

    public static void AddTransactionFilterer(ArbitrumPrecompileExecutionContext context, Address filterer)
    {
        context.ArbosState.TransactionFilterers.Add(filterer);
        EmitTransactionFiltererAddedEvent(context, filterer);
    }

    public static void RemoveTransactionFilterer(ArbitrumPrecompileExecutionContext context, Address filterer)
    {
        if (!IsTransactionFilterer(context, filterer))
            throw ArbitrumPrecompileException.CreateFailureException("Tried to remove non-filterer");

        context.ArbosState.TransactionFilterers.Remove(filterer, context.ArbosState.CurrentArbosVersion);
        EmitTransactionFiltererRemovedEvent(context, filterer);
    }

    public static bool IsTransactionFilterer(ArbitrumPrecompileExecutionContext context, Address filterer)
    {
        return context.ArbosState.TransactionFilterers.IsMember(filterer);
    }

    public static Address[] GetAllTransactionFilterers(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.TransactionFilterers.AllMembers(AddressSet.MaxNumberOfOwners);
    }

    // GetFilteredFundsRecipient returns the address that receives funds from filtered transactions.
    // Returns the zero address if not set, which means the network fee account is used as a fallback.
    public static Address GetFilteredFundsRecipient(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.FilteredFundsRecipient.Get();
    }

    // SetFilteredFundsRecipient sets the address that receives funds from filtered transactions.
    // Setting to the zero address means the network fee account is used as a fallback.
    public static void SetFilteredFundsRecipient(ArbitrumPrecompileExecutionContext context, Address newRecipient)
    {
        context.ArbosState.FilteredFundsRecipient.Set(newRecipient);
        EmitFilteredFundsRecipientSetEvent(context, newRecipient);
    }

    // SetTransactionFilteringFrom sets a time in epoch seconds from which transaction filtering is active.
    // Setting to 0 disables filtering.
    public static void SetTransactionFilteringFrom(ArbitrumPrecompileExecutionContext context, ulong timestamp)
    {
        context.ArbosState.TransactionFilteringEnabledTime.Set(timestamp);
    }

    private static void EmitTransactionFiltererAddedEvent(ArbitrumPrecompileExecutionContext context, Address filterer)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(TransactionFiltererAddedEvent, Address, filterer);
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitTransactionFiltererRemovedEvent(ArbitrumPrecompileExecutionContext context, Address filterer)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(TransactionFiltererRemovedEvent, Address, filterer);
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitFilteredFundsRecipientSetEvent(ArbitrumPrecompileExecutionContext context, Address newRecipient)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(FilteredFundsRecipientSetEvent, Address, newRecipient);
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitChainOwnerAddedEvent(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(ChainOwnerAddedEvent, Address, owner);
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitChainOwnerRemovedEvent(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(ChainOwnerRemovedEvent, Address, owner);
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitNativeTokenOwnerAddedEvent(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(NativeTokenOwnerAddedEvent, Address, owner);
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitNativeTokenOwnerRemovedEvent(ArbitrumPrecompileExecutionContext context, Address owner)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(NativeTokenOwnerRemovedEvent, Address, owner);
        EventsEncoder.EmitEvent(context, log);
    }

    // SetCalldataPriceIncrease sets the increased calldata price feature on or off (EIP-7623)
    public static void SetCalldataPriceIncrease(ArbitrumPrecompileExecutionContext context, bool enabled)
    {
        context.ArbosState.Features.SetCalldataPriceIncrease(enabled);
    }

    // SetGasBacklog sets the L2 gas backlog directly (used by a single-constraint pricing model only)
    public static void SetGasBacklog(ArbitrumPrecompileExecutionContext context, ulong backlog)
    {
        context.ArbosState.L2PricingState.SetGasBacklog(backlog);
    }

    // SetGasPricingConstraints sets the gas pricing constraints used by the multi-constraint pricing model
    public static void SetGasPricingConstraints(ArbitrumPrecompileExecutionContext context, ulong[][] constraints)
    {
        context.ArbosState.L2PricingState.ClearConstraints();

        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.FiftyOne && constraints.Length > L2PricingState.GasConstraintsMaxNum)
            throw ArbitrumPrecompileException.CreateFailureException($"Too many constraints. Max: {L2PricingState.GasConstraintsMaxNum}");

        foreach (ulong[] constraint in constraints)
        {
            ulong gasTargetPerSecond = constraint[0];
            ulong adjustmentWindowSeconds = constraint[1];
            ulong startingBacklogValue = constraint[2];

            if (gasTargetPerSecond == 0 || adjustmentWindowSeconds == 0)
                throw ArbitrumPrecompileException.CreateFailureException($"invalid constraint with target {gasTargetPerSecond} and adjustment window {adjustmentWindowSeconds}");

            context.ArbosState.L2PricingState.AddConstraint(gasTargetPerSecond, adjustmentWindowSeconds, startingBacklogValue);
        }
    }

    // SetMultiGasPricingConstraints configures the multi-dimensional gas pricing model (ArbOS v60+)
    // constraintsArray contains tuples: ((uint8,uint64)[],uint32,uint64,uint64)[]
    // Each tuple is: (WeightedResource[], adjustmentWindowSecs, targetPerSec, backlog)
    public static void SetMultiGasPricingConstraints(ArbitrumPrecompileExecutionContext context, object[] constraintsArray)
    {
        context.ArbosState.L2PricingState.ClearMultiGasConstraints();

        foreach (object[] constraint in constraintsArray!)
        {
            // Parse the constraint tuple: (resources[], adjustmentWindowSecs, targetPerSec, backlog)
            object[] resourcesArray = (object[])constraint[0]!;
            uint adjustmentWindowSecs = Convert.ToUInt32(constraint[1]);
            ulong targetPerSec = Convert.ToUInt64(constraint[2]);
            ulong backlog = Convert.ToUInt64(constraint[3]);

            if (targetPerSec == 0 || adjustmentWindowSecs == 0)
                throw ArbitrumPrecompileException.CreateFailureException(
                    $"invalid constraint: target={targetPerSec} adjustmentWindow={adjustmentWindowSecs}");

            // Parse weighted resources: (resource: uint8, weight: uint64)[]
            Dictionary<ResourceKind, ulong> weights = new();
            foreach (object[] resource in resourcesArray)
            {
                byte resourceKind = Convert.ToByte(resource[0]);
                ulong weight = Convert.ToUInt64(resource[1]);
                weights[(ResourceKind)resourceKind] = weight;
            }

            context.ArbosState.L2PricingState.AddMultiGasConstraint(targetPerSec, adjustmentWindowSecs, backlog, weights);

            // Validate exponents don't exceed maximum
            long[] exponents = context.ArbosState.L2PricingState.CalcMultiGasConstraintsExponents();
            foreach (long exp in exponents)
            {
                if (exp > L2PricingState.MaxPricingExponentBips)
                    throw ArbitrumPrecompileException.CreateFailureException(
                        $"calculated exponent {exp} exceeds maximum allowed {L2PricingState.MaxPricingExponentBips}");
            }
        }
    }

    // SetMultiGasPricingConstraintsFromTuples handles ValueTuple arrays from ABI decoder
    // The ABI decoder returns ValueTuple<ValueTuple<byte,ulong>[], uint, ulong, ulong>[]
    public static void SetMultiGasPricingConstraintsFromTuples(ArbitrumPrecompileExecutionContext context, Array constraintsArray)
    {
        context.ArbosState.L2PricingState.ClearMultiGasConstraints();

        // Use ITuple interface to access ValueTuple elements
        foreach (System.Runtime.CompilerServices.ITuple constraint in constraintsArray!)
        {
            // constraint[0] = resources array (ValueTuple<byte, ulong>[])
            // constraint[1] = adjustmentWindowSecs (uint)
            // constraint[2] = targetPerSec (ulong)
            // constraint[3] = backlog (ulong)
            Array resourcesArray = (Array)constraint[0]!;
            uint adjustmentWindowSecs = Convert.ToUInt32(constraint[1]);
            ulong targetPerSec = Convert.ToUInt64(constraint[2]);
            ulong backlog = Convert.ToUInt64(constraint[3]);

            if (targetPerSec == 0 || adjustmentWindowSecs == 0)
                throw ArbitrumPrecompileException.CreateFailureException(
                    $"invalid constraint: target={targetPerSec} adjustmentWindow={adjustmentWindowSecs}");

            // Parse weighted resources: ValueTuple<byte, ulong>[]
            Dictionary<ResourceKind, ulong> weights = new();
            foreach (System.Runtime.CompilerServices.ITuple resource in resourcesArray)
            {
                byte resourceKind = Convert.ToByte(resource[0]);
                ulong weight = Convert.ToUInt64(resource[1]);
                weights[(ResourceKind)resourceKind] = weight;
            }

            context.ArbosState.L2PricingState.AddMultiGasConstraint(targetPerSec, adjustmentWindowSecs, backlog, weights);

            // Validate exponents don't exceed maximum
            long[] exponents = context.ArbosState.L2PricingState.CalcMultiGasConstraintsExponents();
            foreach (long exp in exponents)
            {
                if (exp > L2PricingState.MaxPricingExponentBips)
                    throw ArbitrumPrecompileException.CreateFailureException(
                        $"calculated exponent {exp} exceeds maximum allowed {L2PricingState.MaxPricingExponentBips}");
            }
        }
    }
}
