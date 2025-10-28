using System.Text.Json;
using Microsoft.ClearScript;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbWasmCache
{
    public static Address Address => ArbosAddresses.ArbWasmCacheAddress;

    public static readonly string Abi =
        "[{\"inputs\":[],\"name\":\"allCacheManagers\",\"outputs\":[{\"internalType\":\"address[]\",\"name\":\"managers\",\"type\":\"address[]\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"}],\"name\":\"cacheCodehash\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"cacheProgram\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"}],\"name\":\"codehashIsCached\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"}],\"name\":\"evictCodehash\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"manager\",\"type\":\"address\"}],\"name\":\"isCacheManager\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"address\",\"name\":\"manager\",\"type\":\"address\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"bool\",\"name\":\"cached\",\"type\":\"bool\"}],\"name\":\"UpdateProgramCache\",\"type\":\"event\"}]";

    // Events
    public static readonly AbiEventDescription UpdateProgramCache;

    static ArbWasmCache()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi);
        UpdateProgramCache = allEvents["UpdateProgramCache"];
    }

    public static void EmitUpdateProgramCacheEvent(ArbitrumPrecompileExecutionContext context, Address caller, Hash256 codeHash, bool cached)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(UpdateProgramCache, Address, caller, codeHash, cached);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    /// <summary>
    /// See if the user is a cache manager owner.
    /// </summary>
    public static bool IsCacheManager(ArbitrumPrecompileExecutionContext context, Address account)
        => context.ArbosState.Programs.CacheManagersStorage.IsMember(account);

    /// <summary>
    /// Retrieve all authorized address managers.
    /// </summary>
    public static Address[] AllCacheManagers(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.CacheManagersStorage.AllMembers(65536).ToArray();

    /// <summary>
    /// Deprecated: replaced with CacheProgram.
    /// </summary>
    public static void CacheCodehash(ArbitrumPrecompileExecutionContext context, ValueHash256 codeHash)
        => SetProgramCached(context, Address.Zero, codeHash, cached: true);

    /// <summary>
    /// Caches all programs with a codehash equal to the given address. Caller must be a cache manager or chain owner.
    /// </summary>
    public static void CacheProgram(ArbitrumPrecompileExecutionContext context, Address account)
    {
        ValueHash256 codeHash = context.ArbosState.BackingStorage.GetCodeHash(account);
        SetProgramCached(context, account, codeHash, cached: true);
    }

    /// <summary>
    /// Evicts all programs with the given codehash. Caller must be a cache manager or chain owner.
    /// </summary>
    public static void EvictProgram(ArbitrumPrecompileExecutionContext context, ValueHash256 codeHash)
        => SetProgramCached(context, Address.Zero, codeHash, cached: false);

    /// <summary>
    /// Gets whether a program is cached. Note that the program may be expired.
    /// </summary>
    public static bool CodehashIsCached(ArbitrumPrecompileExecutionContext context, ValueHash256 codeHash)
        => context.ArbosState.Programs.ProgramCached(codeHash).Value;

    /// <summary>
    /// Caches all programs with the given codehash.
    /// </summary>
    private static void SetProgramCached(ArbitrumPrecompileExecutionContext context, Address address, ValueHash256 codeHash, bool cached)
    {
        if (!HasAccess(context))
            context.BurnOut(); // throws

        StylusPrograms stylusPrograms = context.ArbosState.Programs;
        StylusParams stylusParams = stylusPrograms.GetParams();

        byte[] currentConfig = context.FreeArbosState.ChainConfigStorage.Get();
        ChainConfig chainConfig = JsonSerializer.Deserialize<ChainConfig>(currentConfig)
            ?? throw ArbitrumPrecompileException.CreateFailureException("Failed to deserialize chain config");

        bool debugMode = chainConfig.ArbitrumChainParams.AllowDebugPrecompiles;
        MessageRunMode runMode = MessageRunMode.MessageCommitMode;

        StylusOperationResult<VoidResult> result = stylusPrograms.SetProgramCached(context, in codeHash, address, cached, stylusParams, runMode, debugMode);
        if (!result.IsSuccess)
            throw ArbWasm.CreateExceptionFromStylusOperationError(result.Error.Value);
    }

    private static bool HasAccess(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.CacheManagersStorage.IsMember(context.Caller)
            || context.ArbosState.ChainOwners.IsMember(context.Caller);
}
