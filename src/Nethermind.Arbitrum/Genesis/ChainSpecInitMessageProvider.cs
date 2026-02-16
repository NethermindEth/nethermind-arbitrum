// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

public class ChainSpecInitMessageProvider(ChainSpec chainSpec, IArbitrumSpecHelper specHelper) : IInitMessageProvider
{
    public ParsedInitMessage GetInitMessage()
    {
        if (string.IsNullOrEmpty(specHelper.SerializedChainConfig))
            throw new InvalidOperationException("SerializedChainConfig must be provided in chainspec.");

        byte[] serializedChainConfig = Convert.FromBase64String(specHelper.SerializedChainConfig);

        if (!ChainConfig.TryDeserialize(serializedChainConfig, out ChainConfig? chainConfig))
            throw new InvalidOperationException("Failed to deserialize ChainConfig.");

        if (chainConfig.ChainId != chainSpec.ChainId)
            throw new InvalidOperationException($"ChainId mismatch: chainspec has {chainSpec.ChainId}, serializedChainConfig has {chainConfig.ChainId}");

        return new ParsedInitMessage(chainSpec.ChainId, specHelper.InitialL1BaseFee, chainConfig, serializedChainConfig);
    }
}
