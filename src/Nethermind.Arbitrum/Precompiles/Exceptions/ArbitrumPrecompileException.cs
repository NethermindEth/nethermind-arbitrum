
namespace Nethermind.Arbitrum.Precompiles.Exceptions;

public class ArbitrumPrecompileException: Exception
{
    public readonly byte[] Output;

    public readonly PrecompileExceptionType Type;

    public bool IsRevertDuringCalldataDecoding { get; init; }

    private ArbitrumPrecompileException(byte[] output, PrecompileExceptionType type, string message = "")
        : base(message)
    {
        Output = output;
        Type = type;
    }

    public static ArbitrumPrecompileException CreateSolidityException(byte[] output)
        => new(output, PrecompileExceptionType.Solidity);

    public static ArbitrumPrecompileException CreateProgramActivationError(string message)
        => new(output: [], PrecompileExceptionType.ProgramActivation, message);

    public static ArbitrumPrecompileException CreateRevertException(string message, bool calldataDecoding = false)
        => new(output: [], PrecompileExceptionType.Revert, message)
        {
            IsRevertDuringCalldataDecoding = calldataDecoding
        };

    public static ArbitrumPrecompileException CreateFailureException(string message)
        => new(output: [], PrecompileExceptionType.Other, message);

    public enum PrecompileExceptionType
    {
        Solidity,
        ProgramActivation,
        Revert,
        Other
    }
}
