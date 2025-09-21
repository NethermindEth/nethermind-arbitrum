using Nethermind.Abi;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbitrumFunctionDescription(AbiFunctionDescription abiFunctionDescription)
{
    public AbiFunctionDescription AbiFunctionDescription { get; } = abiFunctionDescription;

    // Minimum ArbOS version required for a precompile's method to be active
    public ulong ArbOSVersion { get; set; }

    // Maximum ArbOS version for a precompile's method until which to stay active
    public ulong? MaxArbOSVersion { get; set; }
}