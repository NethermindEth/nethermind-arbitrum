// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Precompiles.Exceptions;

public class ArbitrumPrecompileException : Exception
{
    public readonly byte[] Output;

    public readonly PrecompileExceptionType Type;

    public bool IsRevertDuringCalldataDecoding { get; init; }

    public bool OutOfGas { get; init; }

    private ArbitrumPrecompileException(byte[] output, PrecompileExceptionType type, string message = "")
        : base(message)
    {
        Output = output;
        Type = type;
    }

    public static ArbitrumPrecompileException CreateSolidityException(byte[] output)
        => new(output, PrecompileExceptionType.SolidityError);

    public static ArbitrumPrecompileException CreateProgramActivationError(string message)
        => new(output: [], PrecompileExceptionType.ProgramActivation, message);

    public static ArbitrumPrecompileException CreateRevertException(string message, bool calldataDecoding = false)
        => new(output: [], PrecompileExceptionType.Revert, message)
        {
            IsRevertDuringCalldataDecoding = calldataDecoding
        };

    public static ArbitrumPrecompileException CreateFailureException(string message)
        => new(output: [], PrecompileExceptionType.Failure, message);

    public static ArbitrumPrecompileException CreateOutOfGasException()
        => new(output: [], PrecompileExceptionType.Failure)
        {
            OutOfGas = true
        };

    public enum PrecompileExceptionType
    {
        SolidityError,
        ProgramActivation,
        Revert,
        Failure
    }
}
