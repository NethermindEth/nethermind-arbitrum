// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Config;

/// <summary>
/// Interface for accessing Arbitrum chain specification parameters with appropriate default values.
/// This abstraction provides a clean way to access chainspec configuration while ensuring
/// sensible defaults when values are not specified in the chainspec file.
/// </summary>
public interface IArbitrumSpecHelper
{
    bool Enabled { get; }
    ulong InitialArbOSVersion { get; }
    Address InitialChainOwner { get; }
    ulong GenesisBlockNum { get; }
    UInt256 InitialL1BaseFee { get; }
    bool EnableArbOS { get; }
    bool AllowDebugPrecompiles { get; }
    bool DataAvailabilityCommittee { get; }
    ulong? MaxCodeSize { get; }
    ulong? MaxInitCodeSize { get; }

    /// <summary>
    /// Base64-encoded JSON of the ChainConfig structure.
    /// When provided, this is decoded and used directly (same as DigestInitMessage RPC).
    /// When null, the ChainConfig is assembled from individual parameters.
    /// </summary>
    string? SerializedChainConfig { get; }
}

/// <summary>
/// Implementation of <see cref="IArbitrumSpecHelper"/> that provides access to Arbitrum chainspec parameters
/// with appropriate default values. This class bridges the gap between raw chainspec parameters and
/// the values actually used throughout the Arbitrum plugin ecosystem.
/// </summary>
public class ArbitrumSpecHelper(ArbitrumChainSpecEngineParameters parameters) : IArbitrumSpecHelper
{
    /// <summary>
    /// Default initial L1 base fee (50 gwei) used if not specified in chainspec
    /// </summary>
    public static readonly UInt256 DefaultInitialL1BaseFee = 50_000_000_000; // 50 gwei

    public bool Enabled => parameters.Enabled ?? true;
    public ulong InitialArbOSVersion => parameters.InitialArbOSVersion ?? 32;
    public Address InitialChainOwner => parameters.InitialChainOwner ?? new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927");
    public ulong GenesisBlockNum => parameters.GenesisBlockNum ?? 0;
    public UInt256 InitialL1BaseFee => parameters.InitialL1BaseFee ?? DefaultInitialL1BaseFee;
    public bool EnableArbOS => parameters.EnableArbOS ?? true;
    public bool AllowDebugPrecompiles => parameters.AllowDebugPrecompiles ?? true;
    public bool DataAvailabilityCommittee => parameters.DataAvailabilityCommittee ?? false;
    public ulong? MaxCodeSize => parameters.MaxCodeSize;
    public ulong? MaxInitCodeSize => parameters.MaxInitCodeSize;
    public string? SerializedChainConfig => parameters.SerializedChainConfig;
}
