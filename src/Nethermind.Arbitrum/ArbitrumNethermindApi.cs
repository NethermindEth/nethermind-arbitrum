using Nethermind.Api;
using static Nethermind.Api.NethermindApi;

// Overrides moved to IOC
public class ArbitrumNethermindApi(Dependencies dependencies) : NethermindApi(dependencies)
{
}
