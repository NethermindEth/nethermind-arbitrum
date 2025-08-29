// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public sealed class ArbWasm
{
    private const ulong ActivationFixedCost = 1659168;

    public static Address Address => ArbosAddresses.ArbWasmAddress;
    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"ageInSeconds\",\"type\":\"uint64\"}],\"name\":\"ProgramExpired\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"have\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"want\",\"type\":\"uint256\"}],\"name\":\"ProgramInsufficientValue\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"ageInSeconds\",\"type\":\"uint64\"}],\"name\":\"ProgramKeepaliveTooSoon\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"uint16\",\"name\":\"version\",\"type\":\"uint16\"},{\"internalType\":\"uint16\",\"name\":\"stylusVersion\",\"type\":\"uint16\"}],\"name\":\"ProgramNeedsUpgrade\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"ProgramNotActivated\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"ProgramNotWasm\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"ProgramUpToDate\",\"type\":\"error\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"bytes32\",\"name\":\"moduleHash\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"program\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"dataFee\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint16\",\"name\":\"version\",\"type\":\"uint16\"}],\"name\":\"ProgramActivated\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"dataFee\",\"type\":\"uint256\"}],\"name\":\"ProgramLifetimeExtended\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"program\",\"type\":\"address\"}],\"name\":\"activateProgram\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"version\",\"type\":\"uint16\"},{\"internalType\":\"uint256\",\"name\":\"dataFee\",\"type\":\"uint256\"}],\"stateMutability\":\"payable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"blockCacheSize\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"count\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"}],\"name\":\"codehashAsmSize\",\"outputs\":[{\"internalType\":\"uint32\",\"name\":\"size\",\"type\":\"uint32\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"}],\"name\":\"codehashKeepalive\",\"outputs\":[],\"stateMutability\":\"payable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"codehash\",\"type\":\"bytes32\"}],\"name\":\"codehashVersion\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"version\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"expiryDays\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"_days\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"freePages\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"pages\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"initCostScalar\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"percent\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"inkPrice\",\"outputs\":[{\"internalType\":\"uint32\",\"name\":\"price\",\"type\":\"uint32\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"keepaliveDays\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"_days\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"maxStackDepth\",\"outputs\":[{\"internalType\":\"uint32\",\"name\":\"depth\",\"type\":\"uint32\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"minInitGas\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"gas\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"cached\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"pageGas\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"gas\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"pageLimit\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"limit\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"pageRamp\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"ramp\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"program\",\"type\":\"address\"}],\"name\":\"programInitGas\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"gas\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"gasWhenCached\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"program\",\"type\":\"address\"}],\"name\":\"programMemoryFootprint\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"footprint\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"program\",\"type\":\"address\"}],\"name\":\"programTimeLeft\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"_secs\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"program\",\"type\":\"address\"}],\"name\":\"programVersion\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"version\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"stylusVersion\",\"outputs\":[{\"internalType\":\"uint16\",\"name\":\"version\",\"type\":\"uint16\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";

    private static readonly AbiEventDescription ProgramActivatedEvent;
    private static readonly AbiEventDescription ProgramLifetimeExtendedEvent;

    private static readonly AbiEncodingInfo ProgramNotWasmError;
    private static readonly AbiEncodingInfo ProgramNotActivatedError;
    private static readonly AbiEncodingInfo ProgramNeedsUpgradeError;
    private static readonly AbiEncodingInfo ProgramExpiredError;
    private static readonly AbiEncodingInfo ProgramUpToDateError;
    private static readonly AbiEncodingInfo ProgramKeepaliveTooSoonError;
    private static readonly AbiEncodingInfo ProgramInsufficientValueError;

    static ArbWasm()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi);
        ProgramActivatedEvent = allEvents["ProgramActivated"];
        ProgramLifetimeExtendedEvent = allEvents["ProgramLifetimeExtended"];

        Dictionary<string, AbiErrorDescription> allErrors = AbiMetadata.GetAllErrorDescriptions(Abi);
        ProgramNotWasmError = allErrors["ProgramNotWasm"].GetCallInfo();
        ProgramNotActivatedError = allErrors["ProgramNotActivated"].GetCallInfo();
        ProgramNeedsUpgradeError = allErrors["ProgramNeedsUpgrade"].GetCallInfo();
        ProgramExpiredError = allErrors["ProgramExpired"].GetCallInfo();
        ProgramUpToDateError = allErrors["ProgramUpToDate"].GetCallInfo();
        ProgramKeepaliveTooSoonError = allErrors["ProgramKeepaliveTooSoon"].GetCallInfo();
        ProgramInsufficientValueError = allErrors["ProgramInsufficientValue"].GetCallInfo();

    }

    // Compile a wasm program with the latest instrumentation
    public static ArbWasmActivateProgramResult ActivateProgram(ArbitrumPrecompileExecutionContext context, Address program, UInt256 value)
    {
        // charge a fixed cost up front to begin activation
        context.Burn(ActivationFixedCost);

        MessageRunMode runMode = MessageRunMode.MessageCommitMode;
        bool debugMode = true;

        //TODO: add support for TxRunMode
        // issue: https://github.com/NethermindEth/nethermind-arbitrum/issues/108
        ProgramActivationResult result = context.ArbosState.Programs.ActivateProgram(program, context.WorldState,
            context.BlockExecutionContext.Header.Timestamp, runMode, debugMode);

        if (result.TakeAllGas)
            context.BurnOut();

        if (!result.IsSuccess)
            throw new InvalidOperationException("Activation failed with the error: " + result.Error);

        PayActivationDataFee(context, value, result.DataFee);

        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(ProgramActivatedEvent, Address, result.CodeHash.ToByteArray(), result.ModuleHash.ToByteArray(),
            program, result.DataFee, result.StylusVersion);
        EventsEncoder.EmitEvent(context, eventLog);

        return new(result.StylusVersion, result.DataFee);
    }

    private static void PayActivationDataFee(ArbitrumPrecompileExecutionContext context, UInt256 value, UInt256 dataFee)
    {
        if (value < dataFee)
            throw PrecompileSolidityError.Create(ProgramInsufficientValueError, value, dataFee);

        Address networkFeeAccount = context.ArbosState.NetworkFeeAccount.Get();
        UInt256 repay = value - dataFee;

        // transfer the fee to the network account, and the rest back to the user
        ArbitrumTransactionProcessor.TransferBalance(Address, networkFeeAccount, dataFee, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo);
        ArbitrumTransactionProcessor.TransferBalance(Address, context.Caller, repay, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo);
    }
}

public record struct ArbWasmActivateProgramResult(ushort Version, UInt256 DataFee);
