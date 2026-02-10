// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbOwnerPublic precompile provides non-owners with info about the current chain owners.
/// The calls to this precompile do not require the sender be a chain owner.
/// For those that are, see ArbOwner
/// </summary>
public static class ArbOwnerPublic
{
    public static Address Address => ArbosAddresses.ArbOwnerPublicAddress;

    public static readonly string Abi =
        "[{\"inputs\":[],\"name\":\"getAllChainOwners\",\"outputs\":[{\"internalType\":\"address[]\",\"name\":\"\",\"type\":\"address[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getAllNativeTokenOwners\",\"outputs\":[{\"internalType\":\"address[]\",\"name\":\"\",\"type\":\"address[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getBrotliCompressionLevel\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getInfraFeeAccount\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getNativeTokenManagementFrom\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getNetworkFeeAccount\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getParentGasFloorPerToken\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getScheduledUpgrade\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"arbosVersion\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"scheduledForTimestamp\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"isCalldataPriceIncreaseEnabled\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"isChainOwner\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"isNativeTokenOwner\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"ownerToRectify\",\"type\":\"address\"}],\"name\":\"rectifyChainOwner\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"internalType\":\"address\",\"name\":\"rectifiedOwner\",\"type\":\"address\"}],\"name\":\"ChainOwnerRectified\",\"type\":\"event\"}]";

    public static readonly AbiEventDescription ChainOwnerRectifiedEvent;

    static ArbOwnerPublic()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi)!;
        ChainOwnerRectifiedEvent = allEvents["ChainOwnerRectified"];
    }

    /// <summary>
    /// IsChainOwner checks if the user is a chain owner
    /// </summary>
    public static bool IsChainOwner(ArbitrumPrecompileExecutionContext context, Address addr)
    {
        return context.ArbosState.ChainOwners.IsMember(addr);
    }

    /// <summary>
    /// GetAllChainOwners retrieves the list of chain owners
    /// </summary>
    public static Address[] GetAllChainOwners(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.ChainOwners.AllMembers(AddressSet.MaxNumberOfOwners);
    }

    /// <summary>
    /// RectifyChainOwner checks if the account is a chain owner
    /// </summary>
    public static void RectifyChainOwner(ArbitrumPrecompileExecutionContext context, Address addr)
    {
        context.ArbosState.ChainOwners.RectifyMapping(addr);
        EmitChainOwnerRectifiedEvent(context, addr);
    }

    /// <summary>
    /// IsNativeTokenOwner checks if the account is a native token owner
    /// </summary>
    public static bool IsNativeTokenOwner(ArbitrumPrecompileExecutionContext context, Address addr)
    {
        return context.ArbosState.NativeTokenOwners.IsMember(addr);
    }

    /// <summary>
    /// GetAllNativeTokenOwners retrieves the list of native token owners
    /// </summary>
    public static Address[] GetAllNativeTokenOwners(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.NativeTokenOwners.AllMembers(AddressSet.MaxNumberOfOwners);
    }

    /// <summary>
    /// GetNetworkFeeAccount gets the network fee collector
    /// </summary>
    public static Address GetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.NetworkFeeAccount.Get();
    }

    /// <summary>
    /// GetInfraFeeAccount gets the infrastructure fee collector
    /// </summary>
    public static Address GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.CurrentArbosVersion < ArbosVersion.Six
            ? context.ArbosState.NetworkFeeAccount.Get()
            : context.ArbosState.InfraFeeAccount.Get();
    }

    /// <summary>
    /// GetBrotliCompressionLevel gets the current brotli compression level used for fast compression
    /// </summary>
    public static ulong GetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.BrotliCompressionLevel.Get();
    }

    /// <summary>
    /// GetScheduledUpgrade gets the next scheduled ArbOS version upgrade and its activation timestamp.
    /// Returns (0, 0) if no ArbOS upgrade is scheduled.
    /// </summary>
    public static (ulong version, ulong timestamp) GetScheduledUpgrade(ArbitrumPrecompileExecutionContext context)
    {
        ulong version = context.ArbosState.UpgradeVersion.Get();
        ulong timestamp = context.ArbosState.UpgradeTimestamp.Get();

        return context.ArbosState.CurrentArbosVersion >= version
            ? (0, 0)
            : (version, timestamp);
    }

    /// <summary>
    /// IsCalldataPriceIncreaseEnabled checks if the increased calldata price feature
    /// (EIP-7623) is enabled
    /// </summary>
    public static bool IsCalldataPriceIncreaseEnabled(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.Features.IsCalldataPriceIncreaseEnabled();
    }

    /// <summary>
    /// GetParentGasFloorPerToken gets the L1 gas floor value per native token
    /// </summary>
    public static ulong GetParentGasFloorPerToken(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.L1PricingState.ParentGasFloorPerToken();
    }

    /// <summary>
    /// GetNativeTokenManagementFrom returns the time in epoch seconds when native token management becomes enabled
    /// Available from ArbOS v50+
    /// </summary>
    public static ulong GetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.NativeTokenEnabledTime.Get();
    }

    private static void EmitChainOwnerRectifiedEvent(ArbitrumPrecompileExecutionContext context, Address rectifiedOwner)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(ChainOwnerRectifiedEvent, Address, rectifiedOwner);
        EventsEncoder.EmitEvent(context, eventLog);
    }
}
