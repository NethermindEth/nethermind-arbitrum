// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Config;

/// <summary>
/// Defines the engine-specific parameters for Arbitrum chains as specified in the chainspec configuration.
/// These parameters control the initialization and behavior of the ArbOS operating system and Arbitrum-specific features.
///
/// This class follows the Nethermind plugin pattern
/// and integrates with the standard chainspec loading mechanism.
/// </summary>
/// <remarks>
/// The parameters are automatically deserialized from the chainspec JSON file's "engine.Arbitrum" section.
/// Each property maps to camelCase JSON properties thanks to Nethermind's JsonNamingPolicy.CamelCase configuration.
///
/// Example chainspec configuration:
/// <code>
/// {
///   "engine": {
///     "Arbitrum": {
///       "enabled": true,
///       "initialArbOSVersion": 32,
///       "initialChainOwner": "0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927",
///       "genesisBlockNum": 0,
///       "enableArbOS": true,
///       "allowDebugPrecompiles": true,
///       "dataAvailabilityCommittee": false
///     }
///   }
/// }
/// </code>
/// </remarks>
public class ArbitrumChainSpecEngineParameters : IChainSpecEngineParameters
{
    public const string ArbitrumEngineName = "Arbitrum";
    public bool? AllowDebugPrecompiles { get; set; }

    public ulong CurrentArbosVersion => InitialArbOSVersion ?? 0;
    public bool? DataAvailabilityCommittee { get; set; }
    public bool? EnableArbOS { get; set; }

    public bool? Enabled { get; set; }

    public string? EngineName => SealEngineType;
    public ulong? GenesisBlockNum { get; set; }
    public ulong? InitialArbOSVersion { get; set; }
    public Address? InitialChainOwner { get; set; }
    public ulong? MaxCodeSize { get; set; }
    public ulong? MaxInitCodeSize { get; set; }
    public string? SealEngineType => ArbitrumEngineName;
    public string? SerializedChainConfig { get; set; }

    public void ApplyToReleaseSpec(ReleaseSpec spec, long startBlock, ulong? startTimestamp)
    {
        // Arbitrum-specific release spec modifications can be applied here
        // Currently no specific modifications needed
    }
}
