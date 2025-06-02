using Nethermind.Api;
using Nethermind.Consensus.Producers;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchainStep(INethermindApi api) : InitializeBlockchain(api)
{
    protected override async Task InitBlockchain()
    {
        // Set Arbitrum precompiles like:
        // OverridableCodeInfoRepository codeInfoRepository = new(new CodeInfoRepository());
        // codeInfoRepository.SetCodeOverwrite(worldState, London.Instance, ArbInfoParser.Address, new PrecompileInfo(ArbInfoParser.Instance));
        //
        // Or, maybe have them already set in _api.WorldStateManager!.GlobalWorldState

        // Create ArbVirtualMachine instance instead of standard Nethermind EVM
        // Might not even have to change base CreateVirtualMachine to virtual if we fully override this InitBlockchain function

        // No need to override CreateTransactionProcessor I believe

        await base.InitBlockchain();
    }

    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;
}
