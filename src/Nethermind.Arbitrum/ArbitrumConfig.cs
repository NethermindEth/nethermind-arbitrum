// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Arbitrum

{
    public class ArbitrumConfig : IArbitrumConfig
    {
        public bool Enabled { get; set; } = true;
        public bool AllowDebugPrecompiles { get; set; } = false;
        public bool DataAvailabilityCommittee { get; set; } = false;
        public ulong InitialArbOSVersion { get; set; } = 0;
        public Address InitialChainOwner { get; set; } = Address.Zero;
        public ulong GenesisBlockNum { get; set; } = 0;
        public ulong? MaxCodeSize { get; set; } = 0;
        public ulong? MaxInitCodeSize { get; set; } = 0;
    }
}
