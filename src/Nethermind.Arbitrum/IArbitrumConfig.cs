// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;
using Nethermind.Core;
using Newtonsoft.Json;

namespace Nethermind.Arbitrum
{
    public interface IArbitrumConfig : IConfig
    {
        [ConfigItem(Description = "Whether to enable the Arbitrum endpoints.", DefaultValue = "false")]
        bool Enabled { get; set; }

        [ConfigItem(Description = "Whether to allow debug precompiles.", DefaultValue = "false")]
        bool AllowDebugPrecompiles { get; set; }

        [ConfigItem(Description = "Whether to allow data availability committee.", DefaultValue = "false")]
        bool DataAvailabilityCommittee { get; set; }

        [ConfigItem(Description = "Initial ArbOS version.", DefaultValue = "0")]
        ulong InitialArbOSVersion { get; set; }

        [ConfigItem(Description = "Initial chain owner.", DefaultValue = "0x0"), JsonProperty(nameof(InitialChainOwner))]
        Address InitialChainOwner { get; set; }

        [ConfigItem(Description = "Genesis block number.", DefaultValue = "0")]
        public ulong GenesisBlockNum { get; set; }

        [ConfigItem(Description = "Maximum bytecode to permit for a contract. 0 value implies params.DefaultMaxCodeSize.", DefaultValue = "0")]
        public ulong? MaxCodeSize { get; set; }

        [ConfigItem(Description = "Maximum initcode to permit in a creation transaction and create instructions. 0 value implies params.DefaultMaxInitCodeSize.", DefaultValue = "0")]
        public ulong? MaxInitCodeSize { get; set; }
    }
}
