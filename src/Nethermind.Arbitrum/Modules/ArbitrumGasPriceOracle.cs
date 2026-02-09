// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Int256;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumGasPriceOracle(IGasPriceOracle baseOracle) : IGasPriceOracle
{
    public ValueTask<UInt256> GetGasPriceEstimate() => baseOracle.GetGasPriceEstimate();

    public UInt256 GetMaxPriorityGasFeeEstimate() => UInt256.Zero;
}
