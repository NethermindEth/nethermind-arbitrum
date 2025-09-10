using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
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
using Nethermind.Evm.State;
using Nethermind.Int256;
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

    public ProgramActivationResult ActivateProgram(Address address, IWorldState state, ulong blockTimestamp, MessageRunMode runMode, bool debugMode)
    {
        if (state.IsDeadAccount(address))
        {
            return ProgramActivationResult.Failure(takeAllGas: false, "Account self-destructed");
        }

        ValueHash256 codeHash = state.GetCodeHash(address);

        StylusParams stylusParams = GetParams();
        Program program = GetProgram(in codeHash, blockTimestamp); // nitro programExists
        bool isExpired = program.ActivatedAtHours == 0 || program.AgeSeconds > ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);

        if (program.Version == stylusParams.StylusVersion && !isExpired) // already activated and up to date
        {
            return ProgramActivationResult.Failure(takeAllGas: false, ArbWasm.Errors.ProgramUpToDate);
        }

        StylusOperationResult<byte[]> wasm = GetWasm(address, state, stylusParams.MaxWasmSize);
        if (!wasm.IsSuccess)
        {
            return ProgramActivationResult.Failure(takeAllGas: false, wasm.Error);
        }

        ushort pageLimit = stylusParams.PageLimit.SaturateSub(WasmStore.Instance.GetStylusPagesOpen());
        IReadOnlyCollection<string> targets = WasmStore.Instance.GetWasmTargets();

        StylusOperationResult<StylusActivationResult> activationResult = ActivateProgramInternal(in codeHash, wasm.Value, pageLimit,
            stylusParams.StylusVersion, ArbosVersion, debugMode, storage.Burner, targets, activationIsMandatory: true);
        if (!activationResult.IsSuccess)
        {
            return ProgramActivationResult.Failure(takeAllGas: true, activationResult.Error);
        }

        (StylusActivationInfo? info, IReadOnlyDictionary<string, byte[]> asmMap) = activationResult.Value;
        if (!info.HasValue)
        {
            throw new InvalidOperationException($"Contract {address} activation info must be set or error must be returned, but got none");
        }

        ValueHash256 moduleHash = info.Value.ModuleHash;
        WasmStore.Instance.ActivateWasm(in moduleHash, asmMap);

        if (program.Cached)
        {
            ValueHash256 oldModuleHash = ModuleHashesStorage.Get(codeHash);
            EvictProgram(state, in oldModuleHash, program.Version, isExpired, runMode, debugMode);
        }

        ModuleHashesStorage.Set(codeHash, moduleHash);

        uint estimateKb = Utils.DivCeiling(info.Value.AsmEstimateBytes, 1024u);
        if (estimateKb > Utils.MaxUint24)
        {
            return ProgramActivationResult.Failure(takeAllGas: true, $"Estimate KB {estimateKb} of {address} is too large for uint24");
        }

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

            CacheProgram(state, in moduleHash, updatedProgram, address, code, in codeHash, stylusParams, blockTimestamp, runMode, debugMode);
        }

        SetProgram(in codeHash, updatedProgram);

        return ProgramActivationResult.Success(stylusParams.StylusVersion, codeHash, moduleHash, dataFee);
    }

    public StylusOperationResult<byte[]> CallProgram(EvmState evmState, in BlockExecutionContext blockContext, in TxExecutionContext transactionContext,
        IWorldState worldState, IStylusEvmApi evmApi, TracingInfo? tracingInfo, ISpecProvider specProvider, ulong l1BlockNumber,
        bool reentrant, MessageRunMode runMode, bool debugMode)
    {
        ulong startingGas = (ulong)evmState.GasAvailable;
        StylusParams stylusParams = GetParams();
        Address codeSource = evmState.Env.CodeSource
            ?? throw new InvalidOperationException("Code source must be set for Stylus program execution");
        ref readonly ValueHash256 codeHash = ref worldState.GetCodeHash(codeSource);

        StylusOperationResult<Program> program = GetActiveProgram(in codeHash, blockContext.Header.Timestamp, stylusParams);
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
        (ushort openNow, ushort openEver) = WasmStore.Instance.GetStylusPages();
        StylusMemoryModel memoryModel = new(stylusParams.FreePages, stylusParams.PageGas);
        ulong callCost = memoryModel.GetGasCost(program.Value.Footprint, openNow, openEver);

        // Pay for program init
        bool cached = program.Value.Cached || WasmStore.Instance.GetRecentWasms().Insert(in codeHash, stylusParams.BlockCacheSize);
        if (cached || program.Value.Version > Arbos.ArbosVersion.One) // in version 1 cached cost is part of init cost
        {
            callCost = callCost.SaturateAdd(program.Value.CachedGas(stylusParams));
        }

        if (!cached)
        {
            callCost = callCost.SaturateAdd(program.Value.InitGas(stylusParams));
        }

        storage.Burner.Burn(callCost);
        using CloseOpenedPages _ = WasmStore.Instance.AddStylusPages(program.Value.Footprint);

        StylusOperationResult<byte[]> localAsm = GetLocalAsm(program.Value, codeSource, in moduleHash, in codeHash, evmState.Env.CodeInfo.CodeSpan,
            stylusParams, blockContext.Header.Timestamp, debugMode);
        if (!localAsm.IsSuccess)
        {
            return localAsm.CastFailure<byte[]>();
        }

        uint arbosTag = runMode == MessageRunMode.MessageCommitMode ? WasmStore.Instance.GetWasmCacheTag() : 0;
        EvmData evmData = new()
        {
            ArbosVersion = ArbosVersion,
            BlockBaseFee = new Bytes32(blockContext.Header.BaseFeePerGas.ToBigEndian()),
            ChainId = specProvider.ChainId,
            BlockCoinbase = new Bytes20(blockContext.Coinbase.Bytes),
            BlockGasLimit = (ulong)blockContext.Header.GasLimit,
            BlockNumber = l1BlockNumber,
            BlockTimestamp = blockContext.Header.Timestamp,
            ContractAddress = new Bytes20(codeSource.Bytes),
            ModuleHash = new Bytes32(moduleHash.Bytes),
            MsgSender = new Bytes20(evmState.Env.ExecutingAccount.Bytes),
            MsgValue = new Bytes32(evmState.Env.Value.ToBigEndian()),
            TxGasPrice = new Bytes32(transactionContext.GasPrice.ToBigEndian()),
            TxOrigin = new Bytes20(transactionContext.Origin.Bytes[12..]),
            Reentrant = reentrant ? 1u : 0u,
            Cached = program.Value.Cached,
            Tracing = tracingInfo != null
        };

        StylusNativeResult<byte[]> callResult = StylusNative.Call(localAsm.Value, evmState.Env.InputData.ToArray(), stylusConfig, evmApi, evmData,
            debugMode, arbosTag, ref storage.Burner.GasLeft);

        int resultLength = callResult.Value?.Length ?? 0;
        if (resultLength > 0 && ArbosVersion >= Arbos.ArbosVersion.StylusFixes)
        {
            ulong evmCost = GetEvmMemoryCost((ulong)resultLength);
            if (storage.Burner.GasLeft < evmCost)
            {
                evmState.GasAvailable = 0;
                return StylusOperationResult<byte[]>.Failure(StylusOperationResultType.ExecutionOutOfGas, "Run out of gas during EVM memory cost calculation");
            }

            ulong maxGasToReturn = storage.Burner.GasLeft - evmCost;
            evmState.GasAvailable = (long)System.Math.Min(startingGas, maxGasToReturn);
        }

        return callResult.IsSuccess
            ? StylusOperationResult<byte[]>.Success(callResult.Value)
            : StylusOperationResult<byte[]>.Failure(
                callResult.Status.ToOperationResultType(), $"{callResult.Status} {callResult.Error}",
                callResult.Value).WithErrorContext($"address: {codeSource}, codeHash: {codeHash}, moduleHash: {moduleHash}");
    }

    public UInt256 ProgramKeepalive(Hash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash.ValueHash256, timestamp, stylusParams);
        Program program = result.IsSuccess ? result.Value : throw new InvalidOperationException(result.Error);

        if (program.AgeSeconds < ArbitrumTime.DaysToSeconds(stylusParams.KeepaliveDays))
            throw new InvalidOperationException(ArbWasm.Errors.ProgramKeepaliveTooSoon(program.AgeSeconds));

        ushort stylusVersion = stylusParams.StylusVersion;
        if (program.Version != stylusVersion)
            throw new InvalidOperationException(ArbWasm.Errors.ProgramNeedsUpgrade(program.Version, stylusVersion));

        ulong dataFee = DataPricerStorage.UpdateModel(program.AsmSize(), timestamp);
        program = program with { ActivatedAtHours = ArbitrumTime.HoursSinceArbitrum(timestamp) };
        SetProgram(new ValueHash256(codeHash.Bytes), program);
        return new UInt256(dataFee);
    }

    public ushort CodeHashVersion(Hash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash.ValueHash256, timestamp, stylusParams);
        return !result.IsSuccess ? (ushort)0 : result.Value.Version;
    }

    public ushort CodeHashVersion(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        return !result.IsSuccess ? (ushort)0 : result.Value.Version;
    }

    public uint ProgramAsmSize(Hash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash.ValueHash256, timestamp, stylusParams);
        return !result.IsSuccess ? throw new InvalidOperationException(result.Error) : result.Value.AsmSize();
    }

    public (ulong gas, ulong gasWhenCached) ProgramInitGas(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        Program program = result.IsSuccess ? result.Value : throw new InvalidOperationException(result.Error);
        var cachedGas = program.CachedGas(stylusParams);
        var initGas = program.InitGas(stylusParams);
        if (stylusParams.StylusVersion > 1)
            initGas += cachedGas;
        return (initGas, cachedGas);
    }

    public ushort ProgramMemoryFootprint(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        return !result.IsSuccess ? throw new InvalidOperationException(result.Error) : result.Value.Footprint;
    }

    public ulong ProgramTimeLeft(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        StylusOperationResult<Program> result = GetActiveProgram(in codeHash, timestamp, stylusParams);
        Program program = result.IsSuccess ? result.Value : throw new InvalidOperationException(result.Error);
        var age = ArbitrumTime.HoursToAgeSeconds(timestamp, program.ActivatedAtHours);
        var expiry = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays);
        return age > expiry ? 0 : expiry.SaturateSub(age);
    }

    private StylusOperationResult<byte[]> GetLocalAsm(Program program, Address address, scoped in ValueHash256 moduleHash,
        scoped ref readonly ValueHash256 codeHash, ReadOnlySpan<byte> code, StylusParams stylusParams, ulong blockTimestamp, bool debugMode)
    {
        string localTarget = StylusTargets.GetLocalTargetName();

        if (WasmStore.Instance.TryGetActivatedAsm(localTarget, in moduleHash, out byte[]? localAsm))
        {
            return StylusOperationResult<byte[]>.Success(localAsm);
        }

        StylusOperationResult<byte[]> wasm = GetWasmFromContractCode(code, stylusParams.MaxWasmSize);
        if (!wasm.IsSuccess)
        {
            return wasm.WithErrorContext($"contract: {address}, moduleHash: {moduleHash}, codeHash: {codeHash}");
        }

        // Don't charge gas
        ulong zeroArbosVersion = 0;
        IBurner zeroGasBurner = new ZeroGasBurner();

        IReadOnlyCollection<string> targets = WasmStore.Instance.GetWasmTargets();

        // We know program is activated, so it must be in correct version and not use too much memory
        StylusOperationResult<StylusActivationResult> activation = ActivateProgramInternal(in codeHash, wasm.Value, stylusParams.PageLimit, program.Version,
            zeroArbosVersion, debugMode, zeroGasBurner, targets, activationIsMandatory: false);
        if (!activation.IsSuccess)
        {
            return activation.CastFailure<byte[]>().WithErrorContext($"contract: {address}, moduleHash: {moduleHash}, codeHash: {codeHash}");
        }

        (StylusActivationInfo? info, IReadOnlyDictionary<string, byte[]> asmMap) = activation.Value;
        if (info.HasValue && info.Value.ModuleHash != moduleHash)
        {
            return StylusOperationResult<byte[]>.Failure(StylusOperationResultType.ModuleHashMismatch,
                $"Contract {address} module hash {info.Value.ModuleHash} does not match expected {moduleHash}");
        }

        uint currentHoursSince = ArbitrumTime.HoursSinceArbitrum(blockTimestamp);
        if (currentHoursSince > program.ActivatedAtHours)
        {
            WasmStore.Instance.WriteActivationToDb(in moduleHash, asmMap);
        }
        else
            WasmStore.Instance.ActivateWasm(in moduleHash, asmMap);

        return asmMap.TryGetValue(localTarget, out byte[]? asm)
            ? StylusOperationResult<byte[]>.Success(asm)
            : StylusOperationResult<byte[]>.Failure(StylusOperationResultType.ActivationFailed,
                $"Failed to reactivate program {address}, local target {localTarget} not found " +
                $"in available targets: {string.Join(", ", asmMap.Keys)}");
    }

    private StylusOperationResult<StylusActivationResult> ActivateProgramInternal(scoped in ValueHash256 codeHash, byte[] wasm, ushort pageLimit,
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
                    : new StylusActivateTaskResult(StylusTargets.WavmTargetName, null, result.Error, result.Status.ToOperationResultType()));

                // Set activation info if activation was successful
                if (result.IsSuccess)
                {
                    info = new StylusActivationInfo(
                        new ValueHash256(result.Value.ModuleHash.ToArray()),
                        result.Value.ActivationInfo.InitCost,
                        result.Value.ActivationInfo.CachedInitCost,
                        result.Value.ActivationInfo.AsmEstimate,
                        result.Value.ActivationInfo.Footprint);
                }
            });

            tasks.Add(wavmActivationTask);

            if (activationIsMandatory)
            {
                // Wait for module activation before starting native compilation
                wavmActivationTask.Wait();

                // Check if module activation failed
                StylusActivateTaskResult wavmActivationTaskResult = results.First(r => r.Target == StylusTargets.WavmTargetName);
                if (wavmActivationTaskResult.Error != null)
                {
                    return StylusOperationResult<StylusActivationResult>.Failure(
                        wavmActivationTaskResult.Status,
                        wavmActivationTaskResult.Error);
                }

                // Add WAVM result to asmMap if WAVM was a target
                if (wavmFound)
                {
                    asmMap[StylusTargets.WavmTargetName] = wavmActivationTaskResult.Asm!;
                }

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
                : new StylusActivateTaskResult(target, null, result.Error, result.Status.ToOperationResultType()));
        })));

        Task.WaitAll(tasks);

        List<string> errors = new();
        foreach (StylusActivateTaskResult result in results)
        {
            if (result.Error != null)
            {
                errors.Add($"{result.Target}: {result.Error}");
            }
            else if (result.Asm != null)
            {
                asmMap[result.Target] = result.Asm;
            }
        }

        if (errors.Count > 0 && activationIsMandatory)
        {
            throw new InvalidOperationException($"Compilation failed for one or more targets despite activation succeeding: {string.Join("; ", errors)}");
        }

        return StylusOperationResult<StylusActivationResult>.Success(new StylusActivationResult(info, asmMap));
    }

    private void CacheProgram(IWorldState state, in ValueHash256 moduleHash, Program program, Address address, byte[] code, in ValueHash256 codeHash,
        StylusParams stylusParams, ulong blockTimestamp, MessageRunMode runMode, bool debugMode)
    {
        if (runMode != MessageRunMode.MessageCommitMode)
        {
            return;
        }

        uint cacheTag = WasmStore.Instance.GetWasmCacheTag();

        Debug.WriteLine($"Caching program: ModuleHash={moduleHash}, CodeHash={codeHash}, Address={address}, Tag={cacheTag}");

        // TODO: Implement native call to Rust cache
        // ...CacheWasmRust

        // TODO: Implement logic of returning the program to the cache if something blew up
        // ...EvictWasmRust
    }

    private void EvictProgram(IWorldState state, in ValueHash256 moduleHash, ushort programVersion, bool forever, MessageRunMode runMode, bool debugMode)
    {
        if (runMode != MessageRunMode.MessageCommitMode)
        {
            return;
        }

        uint cacheTag = WasmStore.Instance.GetWasmCacheTag();

        Debug.WriteLine("Evicting program from cache: " +
            $"ModuleHash: {moduleHash}, Version: {programVersion}, Expired: {forever}, RunMode: {runMode}, DebugMode: {debugMode}, Tag: {cacheTag}");

        // TODO: Implement native call to Rust cache
        // ...EvictWasmRust

        // TODO: Implement logic of returning the program to the cache if something blew up
        // if (!forever)
        //     ...CacheWasmRust
    }

    private StylusOperationResult<byte[]> GetWasm(Address address, IWorldState state, uint maxWasmSize)
    {
        byte[] prefixedWasm = state.GetCode(address)
            ?? throw new InvalidOperationException($"No WASM code found for {address} address");

        return GetWasmFromContractCode(prefixedWasm, maxWasmSize)
            .WithErrorContext("address: " + address);
    }

    private StylusOperationResult<byte[]> GetWasmFromContractCode(ReadOnlySpan<byte> prefixedWasm, uint maxWasmSize)
    {
        if (prefixedWasm.Length == 0)
        {
            return StylusOperationResult<byte[]>.Failure(StylusOperationResultType.ProgramNotWasm, ArbWasm.Errors.ProgramNotWasm);
        }

        StylusOperationResult<StylusBytes> stylusOperationBytes = StylusCode.StripStylusPrefix(prefixedWasm);
        if (!stylusOperationBytes.IsSuccess)
        {
            return stylusOperationBytes.CastFailure<byte[]>();
        }

        try
        {
            byte[] decompressed = BrotliCompression.Decompress(stylusOperationBytes.Value.Bytes, maxWasmSize, stylusOperationBytes.Value.Dictionary);
            return StylusOperationResult<byte[]>.Success(decompressed);
        }
        catch (Exception e)
        {
            return StylusOperationResult<byte[]>.Failure(StylusOperationResultType.UnknownError, e.Message);
        }
    }

    private StylusOperationResult<Program> GetActiveProgram(in ValueHash256 codeHash, ulong timestamp, StylusParams stylusParams)
    {
        Program program = GetProgram(in codeHash, timestamp);
        if (program.Version == 0)
            return StylusOperationResult<Program>.Failure(StylusOperationResultType.ProgramNotActivated, ArbWasm.Errors.ProgramNotActivated);

        if (program.Version != stylusParams.StylusVersion)
        {
            return StylusOperationResult<Program>.Failure(StylusOperationResultType.ProgramNeedsUpgrade,
                ArbWasm.Errors.ProgramNeedsUpgrade(program.Version, stylusParams.StylusVersion));
        }

        return program.AgeSeconds > ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays)
            ? StylusOperationResult<Program>.Failure(StylusOperationResultType.ProgramExpired, ArbWasm.Errors.ProgramExpired(program.AgeSeconds))
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

    private record struct StylusActivationInfo(ValueHash256 ModuleHash, ushort InitGas, ushort CachedInitGas, uint AsmEstimateBytes, ushort Footprint);

    private record struct StylusActivationResult(StylusActivationInfo? Info, IReadOnlyDictionary<string, byte[]> AsmMap);

    private record StylusActivateTaskResult(string Target, byte[]? Asm, string? Error, StylusOperationResultType Status);
}

public readonly ref struct ProgramActivationResult(ushort stylusVersion, ValueHash256 codeHash, ValueHash256 moduleHash, UInt256 dataFee,
    bool consumeAllGas, string? error)
{
    public ushort StylusVersion { get; } = stylusVersion;
    public ValueHash256 CodeHash { get; } = codeHash;
    public ValueHash256 ModuleHash { get; } = moduleHash;
    public UInt256 DataFee { get; } = dataFee;
    public bool TakeAllGas { get; } = consumeAllGas;
    public string? Error { get; } = error;
    public bool IsSuccess => Error is null;

    public static ProgramActivationResult Success(ushort stylusVersion, ValueHash256 codeHash, ValueHash256 moduleHash, UInt256 dataFee)
    {
        return new(stylusVersion, codeHash, moduleHash, dataFee, false, null);
    }

    public static ProgramActivationResult Failure(bool takeAllGas, string error)
    {
        return new(0, Hash256.Zero, Hash256.Zero, 0, takeAllGas, error);
    }
}

public readonly ref struct StylusBytes(ReadOnlySpan<byte> bytes, BrotliCompression.Dictionary dictionary)
{
    public readonly ReadOnlySpan<byte> Bytes = bytes;
    public readonly BrotliCompression.Dictionary Dictionary = dictionary;
}
