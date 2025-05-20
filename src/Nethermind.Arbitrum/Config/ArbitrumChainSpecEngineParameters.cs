using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumChainSpecEngineParameters : IChainSpecEngineParameters
{
    public const string ArbitrumEngineName = "Arbitrum";

    public string? EngineName => SealEngineType;
    public string? SealEngineType => ArbitrumEngineName;
}
