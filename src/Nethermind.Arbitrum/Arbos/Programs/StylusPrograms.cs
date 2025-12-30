using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.ClearScript;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.GasPolicy;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Bytes32 = Nethermind.Arbitrum.Arbos.Stylus.Bytes32;

namespace Nethermind.Arbitrum.Arbos.Programs;

public class StylusPrograms(ArbosStorage storage, ulong arbosVersion)
{
    private static readonly byte[] ParamsKey = [0];
    private static readonly byte[] ProgramDataKey = [1];
    private static readonly byte[] ModuleHashesKey = [2];
    private static readonly byte[] DataPricerKey = [3];
    private static readonly byte[] CacheManagersKey = [4];

    public ArbosStorage ProgramsStorage { get; } = storage.OpenSubStorage(ProgramDataKey);
    public ArbosStorage ModuleHashesStorage { get; } = storage.OpenSubStorage(ModuleHashesKey);
    public DataPricer DataPricerStorage { get; } = new(storage.OpenSubStorage(DataPricerKey));
    public AddressSet CacheManagersStorage { get; } = new(storage.OpenSubStorage(CacheManagersKey));

    public ulong ArbosVersion { get; set; } = arbosVersion;

    public static void Initialize(ulong arbosVersion, ArbosStorage storage)
    {
        StylusParams.InitializeWithDefaults(storage.OpenSubStorage(ParamsKey), arbosVersion);
        DataPricer.Initialize(storage.OpenSubStorage(DataPricerKey));
        AddressSet.Initialize(storage.OpenSubStorage(CacheManagersKey));
    }

    public StylusParams GetParams()
    {
        ArbosStorage paramsStorage = storage.OpenSubStorage(ParamsKey);
        return StylusParams.CreateFromStorage(paramsStorage, ArbosVersion);
    }

    public ProgramActivationResult ActivateProgram(Address address, IWorldState state, IWasmStore wasmStore, ulong blockTimestamp, MessageRunMode runMode, bool debugMode)
    {
        if (state.IsDeadAccount(address))
            return ProgramActivationResult.Failure(takeAllGas: false, new(StylusOperationResultType.UnknownError, "Account self-destructed", []));

        ValueHash256 codeHash = state.GetCodeHash(address);

        StylusParams stylusParams = GetParams();
        Program program = GetProgram(in codeHash, blockTimestamp); // nitro programExists
        bool isExpired = program.ActivatedAtHours == 0 || program.AgeSeconds > ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);

        if (program.Version == stylusParams.StylusVersion && !isExpired) // already activated and up to date
            return ProgramActivationResult.Failure(takeAllGas: false, new(StylusOperationResultType.ProgramUpToDate, "", []));

        StylusOperationResult<byte[]> wasm = GetWasm(address, state, stylusParams.MaxWasmSize);
        if (!wasm.IsSuccess)
            return ProgramActivationResult.Failure(takeAllGas: false, wasm.Error.Value);

        ushort pageLimit = stylusParams.PageLimit.SaturateSub(wasmStore.GetStylusPagesOpen());
        IReadOnlyCollection<string> targets = wasmStore.GetWasmTargets();

        StylusOperationResult<StylusActivationResult> activationResult = ActivateProgramInternal(in codeHash, wasm.Value, pageLimit,
            stylusParams.StylusVersion, ArbosVersion, debugMode, storage.Burner, targets, activationIsMandatory: true);
        if (!activationResult.IsSuccess)
            return ProgramActivationResult.Failure(takeAllGas: true, activationResult.Error.Value);

        (StylusActivationInfo? info, IReadOnlyDictionary<string, byte[]> asmMap) = activationResult.Value;
        if (!info.HasValue)
            return ProgramActivationResult.Failure(takeAllGas: true, new(StylusOperationResultType.UnknownError, $"Contract {address} activation info must be set or error must be returned, but got none", []));

        ValueHash256 moduleHash = info.Value.ModuleHash;
        wasmStore.ActivateWasm(in moduleHash, asmMap);

        if (program.Cached)
        {
            ValueHash256 oldModuleHash = ModuleHashesStorage.Get(codeHash);
            EvictProgram(state, wasmStore, in oldModuleHash, program.Version, isExpired, runMode, debugMode);
        }

        ModuleHashesStorage.Set(codeHash, moduleHash);

        uint estimateKb = Utils.DivCeiling(info.Value.AsmEstimateBytes, 1024u);
        if (estimateKb > Utils.MaxUint24)
            return ProgramActivationResult.Failure(takeAllGas: true, new(StylusOperationResultType.UnknownError, "Estimate KB is too large for uint24", []));

        ulong dataFee = DataPricerStorage.UpdateModel(info.Value.AsmEstimateBytes, blockTimestamp);

        Program updatedProgram = new(
            stylusParams.StylusVersion,
            info.Value.InitGas,
            info.Value.CachedInitGas,
            info.Value.Footprint,
            ArbitrumTime.HoursSinceArbitrum(blockTimestamp),
            estimateKb,
            AgeSeconds: 0,
            Cached: program.Cached);

        if (program.Cached)
        {
            byte[] code = state.GetCode(in codeHash)
                ?? throw new InvalidOperationException($"Code of by {codeHash} of address {address} must be present");

            CacheProgram(state, wasmStore, in moduleHash, updatedProgram, address, code, in codeHash, stylusParams, blockTimestamp, runMode, debugMode);
        }

        SetProgram(in codeHash, updatedProgram);

        return ProgramActivationResult.Success(stylusParams.StylusVersion, codeHash, moduleHash, dataFee);
    }

    public StylusOperationResult<byte[]> CallProgram(IStylusVmHost vmHost, TracingInfo? tracingInfo, ulong chainId, ulong l1BlockNumber,
        bool reentrant, MessageRunMode runMode, bool debugMode)
    {
        ulong startingGas = (ulong)EthereumGasPolicy.GetRemainingGas(in vmHost.VmState.Gas);
        ulong gasAvailable = startingGas;
        StylusParams stylusParams = GetParams();
        Address codeSource = vmHost.VmState.Env.CodeSource
            ?? throw new InvalidOperationException("Code source must be set for Stylus program execution");
        ref readonly ValueHash256 codeHash = ref vmHost.WorldState.GetCodeHash(codeSource);

        StylusOperationResult<Program> program = GetActiveProgram(in codeHash, vmHost.BlockExecutionContext.Header.Timestamp, stylusParams);
        if (!program.IsSuccess)
            return program.CastFailure<byte[]>();

        ValueHash256 moduleHash = ModuleHashesStorage.Get(codeHash);
        StylusConfig stylusConfig = new() // progParams
        {
            Version = program.Value.Version,
            MaxDepth = stylusParams.MaxStackDepth,
            Pricing = new PricingParams { InkPrice = stylusParams.InkPrice }
        };

        // Pay for memory init
        (ushort openNow, ushort openEver) = vmHost.WasmStore.GetStylusPages();
        StylusMemoryModel memoryModel = new(stylusParams.FreePages, stylusParams.PageGas);
        ulong callCost = memoryModel.GetGasCost(program.Value.Footprint, openNow, openEver);

        // Pay for program init
        bool cached = program.Value.Cached || vmHost.WasmStore.GetRecentWasms().Insert(in codeHash, stylusParams.BlockCacheSize);
        if (cached || program.Value.Version > Arbos.ArbosVersion.One) // in version 1 cached cost is part of init cost
            callCost = callCost.SaturateAdd(program.Value.CachedGas(stylusParams));

        if (!cached)
            callCost = callCost.SaturateAdd(program.Value.InitGas(stylusParams));

        if (gasAvailable < callCost)
        {
            return StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ExecutionOutOfGas,
                $"Available gas {gasAvailable} is not enough to pay for callCost {callCost}", []));
        }

        gasAvailable -= callCost;

        using CloseOpenedPages _ = vmHost.WasmStore.AddStylusPagesWithClosing(program.Value.Footprint);

        StylusOperationResult<byte[]> localAsm = GetLocalAsm(vmHost.WasmStore, program.Value, codeSource, in moduleHash, in codeHash, vmHost.VmState.Env.CodeInfo.CodeSpan,
            stylusParams, vmHost.BlockExecutionContext.Header.Timestamp, debugMode);
        if (!localAsm.IsSuccess)
            return localAsm.CastFailure<byte[]>();

        uint arbosTag = runMode == MessageRunMode.MessageCommitMode ? vmHost.WasmStore.GetWasmCacheTag() : 0;
        EvmData evmData = new()
        {
            ArbosVersion = ArbosVersion,
            BlockBaseFee = new Bytes32(vmHost.BlockExecutionContext.Header.BaseFeePerGas.ToBigEndian()),
            ChainId = chainId,
            BlockCoinbase = new Bytes20(vmHost.BlockExecutionContext.Coinbase.Bytes),
            BlockGasLimit = (ulong)vmHost.BlockExecutionContext.Header.GasLimit,
            BlockNumber = l1BlockNumber,
            BlockTimestamp = vmHost.BlockExecutionContext.Header.Timestamp,
            ContractAddress = new Bytes20(vmHost.VmState.Env.ExecutingAccount.Bytes),
            ModuleHash = new Bytes32(moduleHash.Bytes),
            MsgSender = new Bytes20(vmHost.VmState.Env.Caller.Bytes),
            MsgValue = new Bytes32(vmHost.VmState.Env.Value.ToBigEndian()),
            TxGasPrice = new Bytes32(vmHost.TxExecutionContext.GasPrice.ToBigEndian()),
            TxOrigin = new Bytes20(vmHost.TxExecutionContext.Origin.Bytes[12..]),
            Reentrant = reentrant ? 1u : 0u,
            Cached = program.Value.Cached,
            Tracing = tracingInfo != null
        };

        IStylusEvmApi evmApi = new StylusEvmApi(vmHost, vmHost.VmState.Env.ExecutingAccount, memoryModel);
        StylusNativeResult<byte[]> callResult = StylusNative.Call(localAsm.Value, vmHost.VmState.Env.InputData.ToArray(), stylusConfig, evmApi, evmData,
            debugMode, arbosTag, ref gasAvailable);

        vmHost.VmState.Gas = EthereumGasPolicy.FromLong((long)gasAvailable);

        int resultLength = callResult.Value?.Length ?? 0;
        if (resultLength > 0 && ArbosVersion >= Arbos.ArbosVersion.StylusFixes)
        {
            ulong evmCost = GetEvmMemoryCost((ulong)resultLength);
            if (startingGas < evmCost)
            {
                vmHost.VmState.Gas = EthereumGasPolicy.FromLong(0);
                return StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ExecutionOutOfGas, "Run out of gas during EVM memory cost calculation", []));
            }

            ulong maxGasToReturn = startingGas - evmCost;
            vmHost.VmState.Gas = EthereumGasPolicy.FromLong((long)System.Math.Min(gasAvailable, maxGasToReturn));
        }

        return callResult.IsSuccess
            ? StylusOperationResult<byte[]>.Success(callResult.Value)
            : StylusOperationResult<byte[]>.Failure(
                new(callResult.Status.ToOperationResultType(isStylusActivation: false), $"{callResult.Status} {callResult.Error}", []),
                callResult.Value!).WithErrorContext($"address: {codeSource}, codeHash: {codeHash}, moduleHash: {moduleHash}");
    }

    public StylusOperationResult<UInt256> ProgramKeepalive(Hash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash.ValueHash256, timestamp, stylusParams);
        if (!result.IsSuccess)
            return StylusOperationResult<UInt256>.Failure(result.Error.Value);

        Program program = result.Value;

        if (program.AgeSeconds < ArbitrumTime.DaysToSeconds(stylusParams.KeepaliveDays))
            return StylusOperationResult<UInt256>.Failure(new(StylusOperationResultType.ProgramKeepaliveTooSoon, "", [program.AgeSeconds]));

        ushort stylusVersion = stylusParams.StylusVersion;
        if (program.Version != stylusVersion)
            return StylusOperationResult<UInt256>.Failure(new(StylusOperationResultType.ProgramNeedsUpgrade, "", [program.Version, stylusVersion]));

        ulong dataFee = DataPricerStorage.UpdateModel(program.AsmSize(), timestamp);
        program = program with { ActivatedAtHours = ArbitrumTime.HoursSinceArbitrum(timestamp) };
        SetProgram(new ValueHash256(codeHash.Bytes), program);
        return StylusOperationResult<UInt256>.Success(new UInt256(dataFee));
    }

    public StylusOperationResult<ushort> CodeHashVersion(Hash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash.ValueHash256, timestamp, stylusParams);
        return !result.IsSuccess
            ? StylusOperationResult<ushort>.Failure(result.Error.Value, 0)
            : StylusOperationResult<ushort>.Success(result.Value.Version);
    }

    public StylusOperationResult<ushort> CodeHashVersion(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        return !result.IsSuccess
            ? StylusOperationResult<ushort>.Failure(result.Error.Value, 0)
            : StylusOperationResult<ushort>.Success(result.Value.Version);
    }

    public StylusOperationResult<uint> ProgramAsmSize(Hash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash.ValueHash256, timestamp, stylusParams);
        return !result.IsSuccess
            ? StylusOperationResult<uint>.Failure(result.Error.Value, 0)
            : StylusOperationResult<uint>.Success(result.Value.AsmSize());
    }

    public StylusOperationResult<(ulong gas, ulong gasWhenCached)> ProgramInitGas(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        if (!result.IsSuccess)
            return result.CastFailure<(ulong gas, ulong gasWhenCached)>();

        Program program = result.Value;

        ulong cachedGas = program.CachedGas(stylusParams);
        ulong initGas = program.InitGas(stylusParams);
        if (stylusParams.StylusVersion > 1)
            initGas += cachedGas;
        return StylusOperationResult<(ulong gas, ulong gasWhenCached)>.Success((initGas, cachedGas));
    }

    public StylusOperationResult<ushort> ProgramMemoryFootprint(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        return !result.IsSuccess
            ? result.CastFailure<ushort>()
            : StylusOperationResult<ushort>.Success(result.Value.Footprint);
    }

    public StylusOperationResult<ulong> ProgramTimeLeft(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        if (!result.IsSuccess)
            return result.CastFailure<ulong>();

        Program program = result.Value;
        ulong age = ArbitrumTime.HoursToAgeSeconds(timestamp, program.ActivatedAtHours);
        ulong expiry = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);

        return age > expiry
            ? StylusOperationResult<ulong>.Failure(new(StylusOperationResultType.ProgramExpired, "", [age]))
            : StylusOperationResult<ulong>.Success(expiry.SaturateSub(age));
    }

    // Gets whether a program is cached. Note that the program may be expired.
    public StylusOperationResult<bool> ProgramCached(in ValueHash256 codeHash)
    {
        ValueHash256 data = ProgramsStorage.Get(codeHash);
        return StylusOperationResult<bool>.Success(data.Bytes[14] != 0);
    }

    // Sets whether a program is cached. Errors if trying to cache an expired program.
    // `address` must be present if setting cache to true as of ArbOS 31,
    // and if `address` is present it must have the specified codeHash.
    public StylusOperationResult<VoidResult> SetProgramCached(
        Action emitEvent, IWorldState worldState, IWasmStore wasmStore, in ValueHash256 codeHash,
        Address address, bool cache, ulong blockTimestamp, StylusParams stylusParams,
        MessageRunMode runMode, bool debugMode)
    {
        Program program = GetProgram(in codeHash, blockTimestamp);

        bool isExpired = program.AgeSeconds > ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);

        if (program.Version != stylusParams.StylusVersion && cache)
            return StylusOperationResult<VoidResult>.Failure(new(StylusOperationResultType.ProgramNeedsUpgrade, $"Program {codeHash} needs upgrade from {program.Version} to {stylusParams.StylusVersion}", [program.Version, stylusParams.StylusVersion]));

        if (isExpired && cache)
            return StylusOperationResult<VoidResult>.Failure(new(StylusOperationResultType.ProgramExpired, $"Program {codeHash} is expired with age {program.AgeSeconds}", [program.AgeSeconds]));

        if (program.Cached == cache)
            return StylusOperationResult<VoidResult>.Success(VoidResult.Value);

        emitEvent();

        // pay to cache the program, or to re-cache in case of upcoming revert
        ProgramsStorage.Burner.Burn(program.InitCost);

        ValueHash256 moduleHash = ModuleHashesStorage.Get(codeHash);
        if (cache)
        {
            byte[] code = worldState.GetCode(codeHash) ?? [];
            CacheProgram(worldState, wasmStore, in moduleHash, program, address, code, in codeHash, stylusParams, blockTimestamp, runMode, debugMode);
        }
        else
        {
            EvictProgram(worldState, wasmStore, in moduleHash, program.Version, debugMode, runMode, isExpired);
        }
        program = program with { Cached = cache };
        SetProgram(in codeHash, program);

        return StylusOperationResult<VoidResult>.Success(VoidResult.Value);
    }

    internal void SaveActiveProgramForRebuild(
        in ValueHash256 codeHash,
        byte[] code,
        ulong latestBlockTime,
        ulong rebuildStartBlockTime,
        bool debugMode,
        IReadOnlyCollection<string> targets,
        IWasmDb wasmDb,
        ILogger logger)
    {
        StylusParams progParams = GetParams();

        // Check if program is active
        if (!IsProgramActive(in codeHash, latestBlockTime, progParams))
        {
            if (logger.IsDebug)
                logger.Debug($"Program is not active: {codeHash}");
            return;
        }

        ProgramActivationData programData = GetProgramActivationData(in codeHash, latestBlockTime);
        if (programData.Version == 0)
            return;

        // Check if activated after rebuild started
        ulong currentHoursSince = ArbitrumTime.HoursSinceArbitrum(rebuildStartBlockTime);
        if (currentHoursSince < programData.ActivatedAtHours)
        {
            if (logger.IsDebug)
                logger.Debug($"Program {codeHash} was activated during rebuild session, skipping");
            return;
        }

        // Get expected moduleHash
        ValueHash256? expectedModuleHashNullable = GetModuleHash(in codeHash);
        if (expectedModuleHashNullable == null)
        {
            if (logger.IsWarn)
                logger.Warn($"Failed to get module hash for code hash {codeHash}");
            return;
        }

        // Extract value to avoid "temporary value" error with 'in' parameter
        ValueHash256 expectedModuleHash = expectedModuleHashNullable.Value;

        // Check which targets are missing
        List<string> missingTargets = targets
            .Where(t => !wasmDb.TryGetActivatedAsm(t, in expectedModuleHash, out _))
            .ToList();

        if (missingTargets.Count == 0)
        {
            if (logger.IsDebug)
                logger.Debug($"All targets already present for module {expectedModuleHash}");
            return;
        }

        // Extract and decompress WASM
        byte[] wasm;
        try
        {
            StylusOperationResult<byte[]> wasmResult = GetWasmFromContractCode(code, progParams.MaxWasmSize);
            if (!wasmResult.IsSuccess)
            {
                if (logger.IsError)
                    logger.Error($"Failed to extract WASM from code for {codeHash}: {wasmResult.Error}");
                return;
            }
            wasm = wasmResult.Value;
        }
        catch (Exception ex)
        {
            if (logger.IsError)
                logger.Error($"Failed to extract WASM from code for {codeHash}: {ex.Message}");
            return;
        }

        // Compile missing targets
        ulong zeroArbosVersion = 0;
        IBurner zeroGasBurner = new ZeroGasBurner();

        StylusOperationResult<StylusActivationResult> activationResult =
            ActivateProgramInternal(
                in codeHash,
                wasm,
                progParams.PageLimit,
                programData.Version,
                zeroArbosVersion,
                debugMode,
                zeroGasBurner,
                missingTargets,
                activationIsMandatory: false);

        if (!activationResult.IsSuccess)
        {
            if (logger.IsError)
                logger.Error($"Failed to compile program {codeHash}: {activationResult.Error}");
            return;
        }

        (StylusActivationInfo? info, IReadOnlyDictionary<string, byte[]> asmMap) = activationResult.Value;

        // Verify moduleHash matches
        if (info.HasValue && info.Value.ModuleHash != expectedModuleHash)
        {
            if (logger.IsError)
                logger.Error($"Failed to reactivate program while rebuilding wasm store. Expected moduleHash: {expectedModuleHash}, Got: {info.Value.ModuleHash}");
            return;
        }

        if (asmMap.Count == 0)
        {
            if (logger.IsWarn)
                logger.Warn($"No targets compiled successfully for {codeHash}");
            return;
        }

        // Write to store
        try
        {
            wasmDb.WriteActivation(expectedModuleHash, asmMap);

            if (logger.IsDebug)
                logger.Debug($"Successfully saved {asmMap.Count} target(s) for module {expectedModuleHash}");
        }
        catch (Exception ex)
        {
            if (logger.IsError)
                logger.Error($"Failed to write activation for {codeHash}: {ex.Message}");
            throw;
        }
    }

    private bool IsProgramActive(in ValueHash256 codeHash, ulong currentTimestamp, StylusParams stylusParams)
    {
        Program program = GetProgram(in codeHash, currentTimestamp);

        if (program.Version == 0)
            return false;

        if (program.Version != stylusParams.StylusVersion)
            return false;

        return program.AgeSeconds <= ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);
    }

    private ProgramActivationData GetProgramActivationData(in ValueHash256 codeHash, ulong currentTimestamp)
    {
        Program program = GetProgram(in codeHash, currentTimestamp);
        return new ProgramActivationData(
            program.Version,
            program.ActivatedAtHours,
            program.AgeSeconds,
            program.Cached);
    }

    private ValueHash256? GetModuleHash(in ValueHash256 codeHash)
    {
        try
        {
            ValueHash256 moduleHash = ModuleHashesStorage.Get(codeHash);
            return moduleHash.Equals(Hash256.Zero) ? null : moduleHash;
        }
        catch
        {
            return null;
        }
    }

    private static StylusOperationResult<byte[]> GetLocalAsm(IWasmStore wasmStore, Program program, Address address, scoped in ValueHash256 moduleHash,
        scoped ref readonly ValueHash256 codeHash, ReadOnlySpan<byte> code, StylusParams stylusParams, ulong blockTimestamp, bool debugMode)
    {
        string localTarget = StylusTargets.GetLocalTargetName();

        if (wasmStore.TryGetActivatedAsm(localTarget, in moduleHash, out byte[]? localAsm))
            return StylusOperationResult<byte[]>.Success(localAsm);

        StylusOperationResult<byte[]> wasm = GetWasmFromContractCode(code, stylusParams.MaxWasmSize);
        if (!wasm.IsSuccess)
            return wasm.WithErrorContext($"contract: {address}, moduleHash: {moduleHash}, codeHash: {codeHash}");

        // Don't charge gas
        ulong zeroArbosVersion = 0;
        IBurner zeroGasBurner = new ZeroGasBurner();

        IReadOnlyCollection<string> targets = wasmStore.GetWasmTargets();

        // We know program is activated, so it must be in correct version and not use too much memory
        StylusOperationResult<StylusActivationResult> activation = ActivateProgramInternal(in codeHash, wasm.Value, stylusParams.PageLimit, program.Version,
            zeroArbosVersion, debugMode, zeroGasBurner, targets, activationIsMandatory: false);
        if (!activation.IsSuccess)
            return activation.CastFailure<byte[]>().WithErrorContext($"contract: {address}, moduleHash: {moduleHash}, codeHash: {codeHash}");

        (StylusActivationInfo? info, IReadOnlyDictionary<string, byte[]> asmMap) = activation.Value;
        if (info.HasValue && info.Value.ModuleHash != moduleHash)
            return StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ModuleHashMismatch,
                $"Contract {address} module hash {info.Value.ModuleHash} does not match expected {moduleHash}", []));

        uint currentHoursSince = ArbitrumTime.HoursSinceArbitrum(blockTimestamp);
        if (currentHoursSince > program.ActivatedAtHours)
            wasmStore.WriteActivationToDb(in moduleHash, asmMap);
        else
            wasmStore.ActivateWasm(in moduleHash, asmMap);

        return asmMap.TryGetValue(localTarget, out byte[]? asm)
            ? StylusOperationResult<byte[]>.Success(asm)
            : StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ActivationFailed,
                $"Failed to reactivate program {address}, local target {localTarget} not found " +
                $"in available targets: {string.Join(", ", asmMap.Keys)}", []));
    }

    private static StylusOperationResult<StylusActivationResult> ActivateProgramInternal(scoped in ValueHash256 codeHash, byte[] wasm, ushort pageLimit,
        ushort stylusVersion, ulong arbosVersion, bool debugMode, IBurner burner, IReadOnlyCollection<string> targets, bool activationIsMandatory)
    {
        bool wavmFound = targets.Contains(StylusTargets.WavmTargetName);
        string[] nativeTargets = targets.Where(t => t != StylusTargets.WavmTargetName).ToArray();

        // Info will be set if WAVM activation is successful
        StylusActivationInfo? info = null;

        ConcurrentDictionary<string, byte[]> asmMap = new(Environment.ProcessorCount, nativeTargets.Length);
        ConcurrentBag<StylusActivateTaskResult> results = new();
        List<Task> tasks = new();

        // Module activation task (WAVM)
        if (activationIsMandatory || wavmFound)
        {
            Bytes32 codeHashBytes = new(codeHash.Bytes);
            Task wavmActivationTask = Task.Run(() =>
            {
                StylusNativeResult<ActivateResult> result = StylusNative.Activate(wasm, pageLimit, stylusVersion, arbosVersion, debugMode,
                    codeHashBytes, ref burner.GasLeft);

                // Add result to the collection even if activation fails (error will be set)
                results.Add(result.IsSuccess
                    ? new StylusActivateTaskResult(StylusTargets.WavmTargetName, result.Value.WavmModule, null, StylusOperationResultType.Success)
                    : new StylusActivateTaskResult(StylusTargets.WavmTargetName, null, result.Error, result.Status.ToOperationResultType(isStylusActivation: true)));

                // Set activation info if activation was successful
                if (result.IsSuccess)
                    info = new StylusActivationInfo(
                        new ValueHash256(result.Value.ModuleHash.ToArray()),
                        result.Value.ActivationInfo.InitCost,
                        result.Value.ActivationInfo.CachedInitCost,
                        result.Value.ActivationInfo.AsmEstimate,
                        result.Value.ActivationInfo.Footprint);
            });

            tasks.Add(wavmActivationTask);

            if (activationIsMandatory)
            {
                // Wait for module activation before starting native compilation
                wavmActivationTask.Wait();

                // Check if module activation failed
                StylusActivateTaskResult wavmActivationTaskResult = results.First(r => r.Target == StylusTargets.WavmTargetName);
                if (wavmActivationTaskResult.Error != null)
                    return StylusOperationResult<StylusActivationResult>.Failure(new(wavmActivationTaskResult.Status, wavmActivationTaskResult.Error, []));

                // Add WAVM result to asmMap if WAVM was a target
                if (wavmFound)
                    asmMap[StylusTargets.WavmTargetName] = wavmActivationTaskResult.Asm!;

                // Remove WAVM task from the list to avoid reprocessing
                tasks.Clear();
            }
        }

        // Native compilation tasks
        tasks.AddRange(nativeTargets.Select(target => Task.Run(() =>
        {
            StylusNativeResult<byte[]> result = StylusNative.Compile(wasm, stylusVersion, debugMode, target);
            results.Add(result.IsSuccess
                ? new StylusActivateTaskResult(target, result.Value, null, StylusOperationResultType.Success)
                : new StylusActivateTaskResult(target, null, result.Error, result.Status.ToOperationResultType(isStylusActivation: true)));
        })));

        Task.WaitAll(tasks);

        List<string> errors = new();
        foreach (StylusActivateTaskResult result in results)
        {
            if (result.Error != null)
                errors.Add($"{result.Target}: {result.Error}");
            else if (result.Asm != null)
                asmMap[result.Target] = result.Asm;
        }

        if (errors.Count > 0 && activationIsMandatory)
            throw new InvalidOperationException($"Compilation failed for one or more targets despite activation succeeding: {string.Join("; ", errors)}");

        return StylusOperationResult<StylusActivationResult>.Success(new StylusActivationResult(info, asmMap));
    }

    private void CacheProgram(IWorldState state, IWasmStore wasmStore, in ValueHash256 moduleHash, Program program, Address address, byte[] code, in ValueHash256 codeHash,
        StylusParams stylusParams, ulong blockTimestamp, MessageRunMode runMode, bool debugMode)
    {
        if (runMode != MessageRunMode.MessageCommitMode)
            return;

        uint cacheTag = wasmStore.GetWasmCacheTag();

        Debug.WriteLine($"Caching program: ModuleHash={moduleHash}, CodeHash={codeHash}, Address={address}, Tag={cacheTag}");

        // TODO: Implement native call to Rust cache
        // ...CacheWasmRust

        // TODO: Implement logic of returning the program to the cache if something blew up
        // ...EvictWasmRust
    }

    private void EvictProgram(IWorldState state, IWasmStore wasmStore, in ValueHash256 moduleHash, ushort programVersion, bool forever, MessageRunMode runMode, bool debugMode)
    {
        if (runMode != MessageRunMode.MessageCommitMode)
            return;

        uint cacheTag = wasmStore.GetWasmCacheTag();

        Debug.WriteLine("Evicting program from cache: " +
            $"ModuleHash: {moduleHash}, Version: {programVersion}, Expired: {forever}, RunMode: {runMode}, DebugMode: {debugMode}, Tag: {cacheTag}");

        // TODO: Implement native call to Rust cache
        // ...EvictWasmRust

        // TODO: Implement logic of returning the program to the cache if something blew up
        // if (!forever)
        //     ...CacheWasmRust
    }

    private static StylusOperationResult<byte[]> GetWasm(Address address, IWorldState state, uint maxWasmSize)
    {
        byte[]? prefixedWasm = state.GetCode(address);
        if (prefixedWasm is null)
            return StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.UnknownError, $"No WASM code found for {address} address", []));

        return GetWasmFromContractCode(prefixedWasm, maxWasmSize)
            .WithErrorContext("address: " + address);
    }

    private static StylusOperationResult<byte[]> GetWasmFromContractCode(ReadOnlySpan<byte> prefixedWasm, uint maxWasmSize)
    {
        if (prefixedWasm.Length == 0)
            return StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.ProgramNotWasm, "", []));

        StylusOperationResult<StylusBytes> stylusBytes = StylusCode.StripStylusPrefix(prefixedWasm);
        if (!stylusBytes.IsSuccess)
            return stylusBytes.CastFailure<byte[]>();

        try
        {
            byte[] decompressed = BrotliCompression.Decompress(stylusBytes.Value.Bytes, maxWasmSize, stylusBytes.Value.Dictionary);
            return StylusOperationResult<byte[]>.Success(decompressed);
        }
        catch (Exception e)
        {
            return StylusOperationResult<byte[]>.Failure(new(StylusOperationResultType.UnknownError, e.Message, []));
        }
    }

    private StylusOperationResult<Program> GetActiveProgram(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        Program program = GetProgram(in codeHash, timestamp);
        if (program.Version == 0)
            return StylusOperationResult<Program>.Failure(new(StylusOperationResultType.ProgramNotActivated, "", []));

        if (program.Version != stylusParams.StylusVersion)
            return StylusOperationResult<Program>.Failure(new(StylusOperationResultType.ProgramNeedsUpgrade,
                "", [program.Version, stylusParams.StylusVersion]));

        return program.AgeSeconds > ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays)
            ? StylusOperationResult<Program>.Failure(new(StylusOperationResultType.ProgramExpired, "", [program.AgeSeconds]))
            : StylusOperationResult<Program>.Success(program);
    }

    private Program GetProgram(in ValueHash256 codeHash, ulong timestamp)
    {
        ValueHash256 dataAsHash = ProgramsStorage.Get(codeHash);
        ReadOnlySpan<byte> data = dataAsHash.Bytes;

        ushort version = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort initCost = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort cachedCost = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort footprint = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        uint activatedAtHours = ArbitrumBinaryReader.ReadUIntFrom24OrFail(ref data);
        uint asmEstimateKb = ArbitrumBinaryReader.ReadUIntFrom24OrFail(ref data);
        bool cached = ArbitrumBinaryReader.ReadBoolOrFail(ref data);

        ulong ageSeconds = ArbitrumTime.HoursToAgeSeconds(timestamp, activatedAtHours);

        return new Program(version, initCost, cachedCost, footprint, activatedAtHours, asmEstimateKb, ageSeconds, cached);
    }

    private void SetProgram(in ValueHash256 codeHash, Program program)
    {
        Span<byte> data = stackalloc byte[32];

        BinaryPrimitives.WriteUInt16BigEndian(data, program.Version);
        BinaryPrimitives.WriteUInt16BigEndian(data[2..], program.InitCost);
        BinaryPrimitives.WriteUInt16BigEndian(data[4..], program.CachedCost);
        BinaryPrimitives.WriteUInt16BigEndian(data[6..], program.Footprint);
        ArbitrumBinaryWriter.WriteUInt24BigEndian(data[8..], program.ActivatedAtHours);
        ArbitrumBinaryWriter.WriteUInt24BigEndian(data[11..], program.AsmEstimateKb);
        ArbitrumBinaryWriter.WriteBool(data[14..], program.Cached);

        ProgramsStorage.Set(codeHash, new ValueHash256(data));
    }

    private ValueHash256? GetModuleHashForRebuild(in ValueHash256 codeHash)
    {
        try
        {
            ValueHash256 moduleHash = ModuleHashesStorage.Get(codeHash);
            return moduleHash.Equals(Hash256.Zero) ? null : moduleHash;
        }
        catch
        {
            return null;
        }
    }

    private static ulong GetEvmMemoryCost(ulong length)
    {
        ulong words = (length + 31) / 32;
        ulong linearCost = words * GasCostOf.Memory;
        ulong quadraticCost = words * words / 512; // Divisor for the quadratic particle of the memory cost equation.
        return linearCost + quadraticCost;
    }

    private record Program(
        ushort Version,
        ushort InitCost,
        ushort CachedCost,
        ushort Footprint,
        uint ActivatedAtHours,
        uint AsmEstimateKb,
        ulong AgeSeconds,
        bool Cached)
    {
        public ulong CachedGas(StylusParams stylusParams)
        {
            ulong baseGas = (ulong)stylusParams.MinCachedInitGas * StylusParams.MinCachedGasUnits;
            ulong dynoGas = Utils.SaturateMul(CachedCost, (ulong)stylusParams.CachedCostScalar * StylusParams.CostScalarPercent);
            return baseGas.SaturateAdd(Utils.DivCeiling(dynoGas, 100u));
        }

        public ulong InitGas(StylusParams stylusParams)
        {
            ulong baseGas = (ulong)stylusParams.MinInitGas * StylusParams.MinInitGasUnits;
            ulong dynoGas = Utils.SaturateMul(InitCost, (ulong)stylusParams.InitCostScalar * StylusParams.CostScalarPercent);
            return baseGas.SaturateAdd(Utils.DivCeiling(dynoGas, 100u));
        }

        public uint AsmSize() => AsmEstimateKb * 1024;
    }

    public readonly record struct StylusOperationError(StylusOperationResultType OperationResultType, string Message, object[]? Arguments);

    private readonly record struct ProgramActivationData(
        ushort Version,
        uint ActivatedAtHours,
        ulong AgeSeconds,
        bool Cached);

    public record struct StylusActivationInfo(ValueHash256 ModuleHash, ushort InitGas, ushort CachedInitGas, uint AsmEstimateBytes, ushort Footprint);

    private record struct StylusActivationResult(StylusActivationInfo? Info, IReadOnlyDictionary<string, byte[]> AsmMap);

    private record StylusActivateTaskResult(string Target, byte[]? Asm, string? Error, StylusOperationResultType Status);
}

public readonly ref struct ProgramActivationResult(ushort stylusVersion, ValueHash256 codeHash, ValueHash256 moduleHash, UInt256 dataFee,
    bool consumeAllGas, StylusPrograms.StylusOperationError? error)
{
    public ushort StylusVersion { get; } = stylusVersion;
    public ValueHash256 CodeHash { get; } = codeHash;
    public ValueHash256 ModuleHash { get; } = moduleHash;
    public UInt256 DataFee { get; } = dataFee;
    public bool TakeAllGas { get; } = consumeAllGas;
    public StylusPrograms.StylusOperationError? Error { get; } = error;
    public bool IsSuccess => Error is null;

    public static ProgramActivationResult Success(ushort stylusVersion, ValueHash256 codeHash, ValueHash256 moduleHash, UInt256 dataFee)
    {
        return new(stylusVersion, codeHash, moduleHash, dataFee, false, null);
    }

    public static ProgramActivationResult Failure(bool takeAllGas, StylusPrograms.StylusOperationError error)
    {
        return new(0, Hash256.Zero, Hash256.Zero, 0, takeAllGas, error);
    }
}

public readonly ref struct StylusBytes(ReadOnlySpan<byte> bytes, BrotliCompression.Dictionary dictionary)
{
    public readonly ReadOnlySpan<byte> Bytes = bytes;
    public readonly BrotliCompression.Dictionary Dictionary = dictionary;
}
