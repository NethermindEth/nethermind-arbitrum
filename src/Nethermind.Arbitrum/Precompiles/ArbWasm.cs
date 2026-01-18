// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Arbos.Programs.StylusPrograms;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbWasm
{
    public const string Abi = """[{"type":"function","name":"activateProgram","inputs":[{"name":"program","type":"address","internalType":"address"}],"outputs":[{"name":"version","type":"uint16","internalType":"uint16"},{"name":"dataFee","type":"uint256","internalType":"uint256"}],"stateMutability":"payable"},{"type":"function","name":"blockCacheSize","inputs":[],"outputs":[{"name":"count","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"codehashAsmSize","inputs":[{"name":"codehash","type":"bytes32","internalType":"bytes32"}],"outputs":[{"name":"size","type":"uint32","internalType":"uint32"}],"stateMutability":"view"},{"type":"function","name":"codehashKeepalive","inputs":[{"name":"codehash","type":"bytes32","internalType":"bytes32"}],"outputs":[],"stateMutability":"payable"},{"type":"function","name":"codehashVersion","inputs":[{"name":"codehash","type":"bytes32","internalType":"bytes32"}],"outputs":[{"name":"version","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"expiryDays","inputs":[],"outputs":[{"name":"_days","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"freePages","inputs":[],"outputs":[{"name":"pages","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"initCostScalar","inputs":[],"outputs":[{"name":"percent","type":"uint64","internalType":"uint64"}],"stateMutability":"view"},{"type":"function","name":"inkPrice","inputs":[],"outputs":[{"name":"price","type":"uint32","internalType":"uint32"}],"stateMutability":"view"},{"type":"function","name":"keepaliveDays","inputs":[],"outputs":[{"name":"_days","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"maxStackDepth","inputs":[],"outputs":[{"name":"depth","type":"uint32","internalType":"uint32"}],"stateMutability":"view"},{"type":"function","name":"minInitGas","inputs":[],"outputs":[{"name":"gas","type":"uint8","internalType":"uint8"},{"name":"cached","type":"uint8","internalType":"uint8"}],"stateMutability":"view"},{"type":"function","name":"pageGas","inputs":[],"outputs":[{"name":"gas","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"pageLimit","inputs":[],"outputs":[{"name":"limit","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"pageRamp","inputs":[],"outputs":[{"name":"ramp","type":"uint64","internalType":"uint64"}],"stateMutability":"view"},{"type":"function","name":"programInitGas","inputs":[{"name":"program","type":"address","internalType":"address"}],"outputs":[{"name":"gas","type":"uint64","internalType":"uint64"},{"name":"gasWhenCached","type":"uint64","internalType":"uint64"}],"stateMutability":"view"},{"type":"function","name":"programMemoryFootprint","inputs":[{"name":"program","type":"address","internalType":"address"}],"outputs":[{"name":"footprint","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"programTimeLeft","inputs":[{"name":"program","type":"address","internalType":"address"}],"outputs":[{"name":"_secs","type":"uint64","internalType":"uint64"}],"stateMutability":"view"},{"type":"function","name":"programVersion","inputs":[{"name":"program","type":"address","internalType":"address"}],"outputs":[{"name":"version","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"function","name":"stylusVersion","inputs":[],"outputs":[{"name":"version","type":"uint16","internalType":"uint16"}],"stateMutability":"view"},{"type":"event","name":"ProgramActivated","inputs":[{"name":"codehash","type":"bytes32","indexed":true,"internalType":"bytes32"},{"name":"moduleHash","type":"bytes32","indexed":false,"internalType":"bytes32"},{"name":"program","type":"address","indexed":false,"internalType":"address"},{"name":"dataFee","type":"uint256","indexed":false,"internalType":"uint256"},{"name":"version","type":"uint16","indexed":false,"internalType":"uint16"}],"anonymous":false},{"type":"event","name":"ProgramLifetimeExtended","inputs":[{"name":"codehash","type":"bytes32","indexed":true,"internalType":"bytes32"},{"name":"dataFee","type":"uint256","indexed":false,"internalType":"uint256"}],"anonymous":false},{"type":"error","name":"ProgramExpired","inputs":[{"name":"ageInSeconds","type":"uint64","internalType":"uint64"}]},{"type":"error","name":"ProgramInsufficientValue","inputs":[{"name":"have","type":"uint256","internalType":"uint256"},{"name":"want","type":"uint256","internalType":"uint256"}]},{"type":"error","name":"ProgramKeepaliveTooSoon","inputs":[{"name":"ageInSeconds","type":"uint64","internalType":"uint64"}]},{"type":"error","name":"ProgramNeedsUpgrade","inputs":[{"name":"version","type":"uint16","internalType":"uint16"},{"name":"stylusVersion","type":"uint16","internalType":"uint16"}]},{"type":"error","name":"ProgramNotActivated","inputs":[]},{"type":"error","name":"ProgramNotWasm","inputs":[]},{"type":"error","name":"ProgramUpToDate","inputs":[]}]""";

    private const ulong ActivationFixedCost = 1_659_168;

    private static readonly AbiEventDescription ProgramActivatedEvent;
    private static readonly AbiErrorDescription ProgramExpired;
    private static readonly AbiErrorDescription ProgramInsufficientValue;
    private static readonly AbiErrorDescription ProgramKeepaliveTooSoon;
    private static readonly AbiEventDescription ProgramLifetimeExtendedEvent;
    private static readonly AbiErrorDescription ProgramNeedsUpgrade;
    private static readonly AbiErrorDescription ProgramNotActivated;

    // Solidity errors
    private static readonly AbiErrorDescription ProgramNotWasm;
    private static readonly AbiErrorDescription ProgramUpToDate;
    public static Address Address => ArbosAddresses.ArbWasmAddress;

    static ArbWasm()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi);
        ProgramActivatedEvent = allEvents["ProgramActivated"];
        ProgramLifetimeExtendedEvent = allEvents["ProgramLifetimeExtended"];

        Dictionary<string, AbiErrorDescription> allErrors = AbiMetadata.GetAllErrorDescriptions(Abi);
        ProgramNotWasm = allErrors["ProgramNotWasm"];
        ProgramNotActivated = allErrors["ProgramNotActivated"];
        ProgramNeedsUpgrade = allErrors["ProgramNeedsUpgrade"];
        ProgramExpired = allErrors["ProgramExpired"];
        ProgramUpToDate = allErrors["ProgramUpToDate"];
        ProgramKeepaliveTooSoon = allErrors["ProgramKeepaliveTooSoon"];
        ProgramInsufficientValue = allErrors["ProgramInsufficientValue"];
    }

    /// <summary>
    /// Compile a wasm program with the latest instrumentation
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="program">The address of the program to activate</param>
    /// <returns>A result containing the stylus version and data fee</returns>
    /// <exception cref="ArbitrumPrecompileException">Thrown when activation fails</exception>
    public static ArbWasmActivateProgramResult ActivateProgram(ArbitrumPrecompileExecutionContext context, Address program)
    {
        // charge a fixed cost up front to begin activation
        context.Burn(ActivationFixedCost);

        MessageRunMode runMode = MessageRunMode.MessageCommitMode;

        byte[] currentConfig = context.FreeArbosState.ChainConfigStorage.Get();
        ChainConfig chainConfig = JsonSerializer.Deserialize<ChainConfig>(currentConfig)
            ?? throw ArbitrumPrecompileException.CreateFailureException("Failed to deserialize chain config");
        bool debugMode = chainConfig.ArbitrumChainParams.AllowDebugPrecompiles;

        //TODO: add support for TxRunMode
        // issue: https://github.com/NethermindEth/nethermind-arbitrum/issues/108
        ProgramActivationResult result = context.ArbosState.Programs.ActivateProgram(program, context.WorldState, context.WasmStore,
            context.BlockExecutionContext.Header.Timestamp, runMode, debugMode);

        if (result.TakeAllGas)
            context.GasLeft = 0; // Burnout without throwing

        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error!.Value);

        PayActivationDataFee(context, result.DataFee);

        EmitProgramActivatedEvent(context, result.CodeHash, result.ModuleHash, program, result.DataFee, result.StylusVersion);

        return new ArbWasmActivateProgramResult(result.StylusVersion, result.DataFee);
    }

    /// <summary>
    /// Gets the number of extra programs ArbOS caches during a given block
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The number of additional programs cached per block</returns>
    public static ushort BlockCacheSize(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().BlockCacheSize;

    /// <summary>
    /// Gets a program's asm size in bytes
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="codeHash">The code hash of the program to query</param>
    /// <returns>The size of the program's assembly code in bytes</returns>
    /// <exception cref="InvalidOperationException">Thrown when the program is not activated or has expired</exception>
    public static uint CodeHashAsmSize(ArbitrumPrecompileExecutionContext context, Hash256 codeHash)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        StylusOperationResult<uint> result = context.ArbosState.Programs.ProgramAsmSize(
            codeHash,
            context.BlockExecutionContext.Header.Timestamp,
            stylusParams);
        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error.Value);

        return result.Value;
    }

    /// <summary>
    /// Extends a program's expiration date (reverts if too soon)
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="codeHash">The code hash of the program to extend</param>
    /// <exception cref="InvalidOperationException">Thrown when keepalive is called too soon or the program is not activated</exception>
    public static void CodeHashKeepAlive(
        ArbitrumPrecompileExecutionContext context,
        Hash256 codeHash)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        StylusOperationResult<UInt256> dataFee = context.ArbosState.Programs.ProgramKeepalive(
            codeHash,
            context.BlockExecutionContext.Header.Timestamp,
            stylusParams);
        if (!dataFee.IsSuccess)
            throw CreateExceptionFromStylusOperationError(dataFee.Error.Value);

        PayActivationDataFee(context, dataFee.Value);

        EmitProgramLifetimeExtendedEvent(context, codeHash, dataFee.Value);
    }

    /// <summary>
    /// Gets the stylus version that program with codeHash was most recently compiled with
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="codeHash">The code hash of the program to query</param>
    /// <returns>The stylus version of the program was compiled with, or 0 if not activated</returns>
    public static ushort CodeHashVersion(ArbitrumPrecompileExecutionContext context, Hash256 codeHash)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        StylusOperationResult<ushort> result = context.ArbosState.Programs.CodeHashVersion(
            codeHash,
            context.BlockExecutionContext.Header.Timestamp,
            stylusParams);
        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error.Value);

        return result.Value;
    }

    public static ArbitrumPrecompileException CreateExceptionFromStylusOperationError(StylusOperationError error)
        => error switch
        {
            { OperationResultType: StylusOperationResultType.ActivationFailed } => ArbitrumPrecompileException.CreateProgramActivationError(error.Message),
            { OperationResultType: StylusOperationResultType.ProgramNotWasm } => ProgramNotWasmError(),
            { OperationResultType: StylusOperationResultType.ProgramNotActivated } => ProgramNotActivatedError(),
            { OperationResultType: StylusOperationResultType.ProgramNeedsUpgrade } => ProgramNeedsUpgradeError((ushort)error.Arguments![0], (ushort)error.Arguments![1]),
            { OperationResultType: StylusOperationResultType.ProgramExpired } => ProgramExpiredError((ulong)error.Arguments![0]),
            { OperationResultType: StylusOperationResultType.ProgramUpToDate } => ProgramUpToDateError(),
            { OperationResultType: StylusOperationResultType.ProgramKeepaliveTooSoon } => ProgramKeepaliveTooSoonError((ulong)error.Arguments![0]),
            { OperationResultType: StylusOperationResultType.ProgramInsufficientValue } => ProgramInsufficientValueError((UInt256)error.Arguments![0], (UInt256)error.Arguments![1]),
            _ => ArbitrumPrecompileException.CreateFailureException($"Error type: {error.OperationResultType}, message: {error.Message}")
        };

    /// <summary>
    /// Gets the number of days after which programs deactivate
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The number of days before a program expires and becomes inactive</returns>
    public static ushort ExpiryDays(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().ExpiryDays;

    /// <summary>
    /// Gets the number of free wasm pages a tx gets
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The number of free memory pages allocated per transaction</returns>
    public static ushort FreePages(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().FreePages;

    /// <summary>
    /// Gets the linear adjustment made to program init costs
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The scalar percentage applied to program initialization costs</returns>
    public static ulong InitCostScalar(ArbitrumPrecompileExecutionContext context)
        => (ulong)context.ArbosState.Programs.GetParams().InitCostScalar * StylusParams.CostScalarPercent;

    /// <summary>
    /// Gets the amount of ink 1 gas buys
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The ink price per gas unit</returns>
    public static uint InkPrice(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().InkPrice;

    /// <summary>
    /// Gets the age a program must be to perform a keepalive
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The minimum number of days a program must be active before it can be kept alive</returns>
    public static ushort KeepaliveDays(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().KeepaliveDays;

    /// <summary>
    /// Gets the wasm stack size limit
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The maximum stack depth allowed for wasm programs</returns>
    public static uint MaxStackDepth(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().MaxStackDepth;

    /// <summary>
    /// Gets the minimum costs to invoke a program
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>A tuple containing (gas, cached) - the minimum gas costs for program initialization</returns>
    /// <exception cref="ArbitrumPrecompileException">Thrown when called on unsupported ArbOS versions</exception>
    public static (ulong gas, ulong cached) MinInitGas(ArbitrumPrecompileExecutionContext context)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();

        if (context.ArbosState.CurrentArbosVersion < ArbosVersion.StylusChargingFixes)
            throw ArbitrumPrecompileException.CreateRevertException(
                $"MinInitGas called on ArbOS version {context.ArbosState.CurrentArbosVersion}, expected at least {ArbosVersion.StylusChargingFixes}"
            );

        ulong init = (ulong)stylusParams.MinInitGas * StylusParams.MinInitGasUnits;
        ulong cached = (ulong)stylusParams.MinCachedInitGas * StylusParams.MinCachedGasUnits;

        return (init, cached);
    }

    /// <summary>
    /// Gets the base cost of each additional wasm page
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The gas cost per additional memory page</returns>
    public static ushort PageGas(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().PageGas;

    /// <summary>
    /// Gets the maximum initial number of pages a wasm may allocate
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The maximum number of memory pages that can be allocated initially</returns>
    public static ushort PageLimit(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().PageLimit;

    /// <summary>
    /// Gets the ramp that drives exponential memory costs
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The ramp factor for exponential memory cost scaling</returns>
    public static ulong PageRamp(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().PageRamp;

    public static ArbitrumPrecompileException ProgramExpiredError(ulong ageInSeconds)
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramExpired.Name, ProgramExpired.Inputs.Select(p => p.Type).ToArray()),
            [ageInSeconds]
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    /// <summary>
    /// Gets the cost to invoke the program
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="program">The address of the program to query</param>
    /// <returns>A tuple containing (gas, gasWhenCached) - the gas costs for program initialization</returns>
    /// <exception cref="InvalidOperationException">Thrown when the program is not activated or has expired</exception>
    public static (ulong gas, ulong gasWhenCached) ProgramInitGas(ArbitrumPrecompileExecutionContext context, Address program)
    {
        (ValueHash256 codeHash, StylusParams stylusParams) = GetCodeHashAndParams(context, program);
        StylusOperationResult<(ulong gas, ulong gasWhenCached)> result = context.ArbosState.Programs.ProgramInitGas(
            in codeHash,
            context.BlockExecutionContext.Header.Timestamp,
            stylusParams);
        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error.Value);

        return result.Value;
    }

    public static ArbitrumPrecompileException ProgramInsufficientValueError(UInt256 value, UInt256 dataFee)
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramInsufficientValue.Name, ProgramInsufficientValue.Inputs.Select(p => p.Type).ToArray()),
            [value, dataFee]
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    public static ArbitrumPrecompileException ProgramKeepaliveTooSoonError(ulong ageInSeconds)
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramKeepaliveTooSoon.Name, ProgramKeepaliveTooSoon.Inputs.Select(p => p.Type).ToArray()),
            [ageInSeconds]
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    /// <summary>
    /// Gets the footprint of the program at addr
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="program">The address of the program to query</param>
    /// <returns>The memory footprint of the program in pages</returns>
    /// <exception cref="InvalidOperationException">Thrown when the program is not activated or has expired</exception>
    public static ushort ProgramMemoryFootprint(ArbitrumPrecompileExecutionContext context, Address program)
    {
        (ValueHash256 codeHash, StylusParams stylusParams) = GetCodeHashAndParams(context, program);
        StylusOperationResult<ushort> result = context.ArbosState.Programs.ProgramMemoryFootprint(
            in codeHash,
            context.BlockExecutionContext.Header.Timestamp,
            stylusParams);
        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error.Value);

        return result.Value;
    }

    public static ArbitrumPrecompileException ProgramNeedsUpgradeError(ushort programVersion, ushort stylusVersion)
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramNeedsUpgrade.Name, ProgramNeedsUpgrade.Inputs.Select(p => p.Type).ToArray()),
            [programVersion, stylusVersion]
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    public static ArbitrumPrecompileException ProgramNotActivatedError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramNotActivated.Name, ProgramNotActivated.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    public static ArbitrumPrecompileException ProgramNotWasmError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramNotWasm.Name, ProgramNotWasm.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    /// <summary>
    /// Gets the amount of time remaining until the program expires
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="program">The address of the program to query</param>
    /// <returns>The number of seconds remaining before the program expires, or 0 if already expired</returns>
    /// <exception cref="InvalidOperationException">Thrown when the program is not activated</exception>
    public static ulong ProgramTimeLeft(ArbitrumPrecompileExecutionContext context, Address program)
    {
        (ValueHash256 codeHash, StylusParams stylusParams) = GetCodeHashAndParams(context, program);
        StylusOperationResult<ulong> result = context.ArbosState.Programs.ProgramTimeLeft(
            in codeHash,
            context.BlockExecutionContext.Header.Timestamp,
            stylusParams);
        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error.Value);

        return result.Value;
    }

    public static ArbitrumPrecompileException ProgramUpToDateError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(ProgramUpToDate.Name, ProgramUpToDate.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    /// <summary>
    /// Gets the stylus version that program at addr was most recently compiled with
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="program">The address of the program to query</param>
    /// <returns>The stylus version of the program was compiled with, or 0 if not activated</returns>
    public static ushort ProgramVersion(ArbitrumPrecompileExecutionContext context, Address program)
    {
        ValueHash256 codeHash = context.ArbosState.BackingStorage.GetCodeHash(program);
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        StylusOperationResult<ushort> result = context.ArbosState.Programs.CodeHashVersion(in codeHash, context.BlockExecutionContext.Header.Timestamp, stylusParams);
        if (!result.IsSuccess)
            throw CreateExceptionFromStylusOperationError(result.Error.Value);

        return result.Value;
    }

    /// <summary>
    /// Gets the latest stylus version
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The current stylus version</returns>
    public static ushort StylusVersion(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.Programs.GetParams().StylusVersion;

    /// <summary>
    /// Emits a ProgramActivated event
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="codeHash">The code hash of the activated program</param>
    /// <param name="moduleHash">The module hash of the activated program</param>
    /// <param name="program">The address of the activated program</param>
    /// <param name="dataFee">The data fee charged for activation</param>
    /// <param name="version">The stylus version</param>
    private static void EmitProgramActivatedEvent(ArbitrumPrecompileExecutionContext context, ValueHash256 codeHash, ValueHash256 moduleHash, Address program, UInt256 dataFee, ushort version)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(ProgramActivatedEvent, Address, codeHash.ToByteArray(), moduleHash.ToByteArray(),
            program, dataFee, version);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    /// <summary>
    /// Emits a ProgramLifetimeExtended event
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="codeHash">The code hash of the program whose lifetime was extended</param>
    /// <param name="dataFee">The data fee charged for the lifetime extension</param>
    private static void EmitProgramLifetimeExtendedEvent(ArbitrumPrecompileExecutionContext context, Hash256 codeHash, UInt256 dataFee)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(
            ProgramLifetimeExtendedEvent,
            Address,
            codeHash.Bytes.ToArray(),
            dataFee);

        EventsEncoder.EmitEvent(context, eventLog);
    }

    /// <summary>
    /// Helper method to get both the code hash and stylus parameters for a program
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="program">The address of the program</param>
    /// <returns>A tuple containing the code hash and stylus parameters</returns>
    private static (ValueHash256 codeHash, StylusParams stylusParams) GetCodeHashAndParams(
        ArbitrumPrecompileExecutionContext context,
        Address program)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        ValueHash256 codeHash = context.ArbosState.BackingStorage.GetCodeHash(program);
        return (codeHash, stylusParams);
    }

    /// <summary>
    /// Helper method to handle payment of activation data fees
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="dataFee">The required data fee</param>
    /// <exception cref="ArbitrumPrecompileException">Thrown when insufficient value is provided</exception>
    private static void PayActivationDataFee(
        ArbitrumPrecompileExecutionContext context,
        UInt256 dataFee)
    {
        UInt256 value = context.Value;
        if (value < dataFee)
            throw ProgramInsufficientValueError(value, dataFee);

        Address networkFeeAccount = context.ArbosState.NetworkFeeAccount.Get();
        UInt256 repay = value - dataFee;

        // transfer the fee to the network account, and the rest back to the user
        ArbitrumTransactionProcessor.TransferBalance(Address, networkFeeAccount, dataFee, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo, BalanceChangeReason.BalanceChangeTransferActivationFee);
        ArbitrumTransactionProcessor.TransferBalance(Address, context.Caller, repay, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo, BalanceChangeReason.BalanceChangeTransferActivationReimburse);
    }
}

/// <summary>
/// Result returned by the ActivateProgram method
/// </summary>
/// <param name="Version">The stylus version the program was compiled with</param>
/// <param name="DataFee">The data fee charged for activation</param>
public record struct ArbWasmActivateProgramResult(ushort Version, UInt256 DataFee);
