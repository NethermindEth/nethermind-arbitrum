// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Int256;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumGasPriceOracle(IGasPriceOracle baseOracle) : IGasPriceOracle
{
    public ValueTask<UInt256> GetGasPriceEstimate() => baseOracle.GetGasPriceEstimate();

    public UInt256 GetMaxPriorityGasFeeEstimate() => UInt256.Zero;
}
