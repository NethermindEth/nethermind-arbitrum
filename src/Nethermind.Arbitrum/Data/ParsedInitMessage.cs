// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Data
{
    public class ParsedInitMessage(
        ulong chainId,
        UInt256 initialBaseFee,
        ChainConfig? chainConfigSpec = null,
        byte[]? serializedChainConfig = null)
    {
        public ulong ChainId = chainId;

        public UInt256 InitialBaseFee = initialBaseFee;

        public ChainConfig? ChainConfigSpec = chainConfigSpec;

        public byte[]? SerializedChainConfig = serializedChainConfig;

        public string? IsCompatibleWith(ChainSpec localChainSpec)
        {
            // Chain ID must match exactly
            if (ChainId != localChainSpec.ChainId)
            {
                return $"Chain ID mismatch: L1 init message has chain ID {ChainId}, but local chainspec expects {localChainSpec.ChainId}";
            }

            // If we have a parsed chain config from L1, validate Arbitrum parameters
            if (ChainConfigSpec?.ArbitrumChainParams == null) {
                    return null; // Compatible
            }

            ArbitrumChainParams l1ArbitrumParams = ChainConfigSpec.ArbitrumChainParams;
            ArbitrumChainSpecEngineParameters localArbitrumParams = localChainSpec.EngineChainSpecParametersProvider
                .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();

            // Key Arbitrum parameters must match
            if (localArbitrumParams.EnableArbOS.HasValue &&
                l1ArbitrumParams.Enabled != localArbitrumParams.EnableArbOS.Value)
            {
                return $"ArbOS enablement mismatch: L1 init message has EnableArbOS={l1ArbitrumParams.Enabled}, but local chainspec expects {localArbitrumParams.EnableArbOS.Value}";
            }

            if (localArbitrumParams.InitialArbOSVersion.HasValue &&
                l1ArbitrumParams.InitialArbOSVersion != localArbitrumParams.InitialArbOSVersion.Value)
            {
                return $"Initial ArbOS version mismatch: L1 init message has version {l1ArbitrumParams.InitialArbOSVersion}, but local chainspec expects {localArbitrumParams.InitialArbOSVersion.Value}";
            }

            if (localArbitrumParams.InitialChainOwner != null &&
                l1ArbitrumParams.InitialChainOwner != localArbitrumParams.InitialChainOwner)
            {
                return $"Initial chain owner mismatch: L1 init message has owner {l1ArbitrumParams.InitialChainOwner}, but local chainspec expects {localArbitrumParams.InitialChainOwner}";
            }

            if (localArbitrumParams.GenesisBlockNum.HasValue &&
                l1ArbitrumParams.GenesisBlockNum != localArbitrumParams.GenesisBlockNum.Value)
            {
                return $"Genesis block number mismatch: L1 init message has block {l1ArbitrumParams.GenesisBlockNum}, but local chainspec expects {localArbitrumParams.GenesisBlockNum.Value}";
            }

            if (localArbitrumParams.DataAvailabilityCommittee != null &&
                l1ArbitrumParams.DataAvailabilityCommittee != localArbitrumParams.DataAvailabilityCommittee)
            {
                return $"Data availability committee mismatch: L1 init message has committee {l1ArbitrumParams.DataAvailabilityCommittee}, but local chainspec expects {localArbitrumParams.DataAvailabilityCommittee}";
            }

            return null; // Compatible
        }

        public ArbitrumChainSpecEngineParameters GetCanonicalArbitrumParameters(IArbitrumSpecHelper specHelper)
        {
            var l1Params = ChainConfigSpec?.ArbitrumChainParams;
            if (l1Params == null)
            {
                // No L1 config available, use specHelper defaults
                return new ArbitrumChainSpecEngineParameters
                {
                    Enabled = specHelper.Enabled,
                    InitialArbOSVersion = specHelper.InitialArbOSVersion,
                    InitialChainOwner = specHelper.InitialChainOwner,
                    GenesisBlockNum = specHelper.GenesisBlockNum,
                    AllowDebugPrecompiles = specHelper.AllowDebugPrecompiles,
                    DataAvailabilityCommittee = specHelper.DataAvailabilityCommittee,
                    MaxCodeSize = specHelper.MaxCodeSize,
                    MaxInitCodeSize = specHelper.MaxInitCodeSize
                };
            }

            // Create canonical parameters from L1 data with specHelper fallbacks
            var canonicalParams = new ArbitrumChainSpecEngineParameters
            {
                Enabled = l1Params.Enabled,
                InitialArbOSVersion = l1Params.InitialArbOSVersion,
                InitialChainOwner = l1Params.InitialChainOwner,
                GenesisBlockNum = l1Params.GenesisBlockNum,
                AllowDebugPrecompiles = l1Params.AllowDebugPrecompiles,
                DataAvailabilityCommittee = l1Params.DataAvailabilityCommittee,
                SerializedChainConfig = SerializedChainConfig != null ? Convert.ToBase64String(SerializedChainConfig) : null,
                MaxCodeSize = l1Params.MaxCodeSize,
                MaxInitCodeSize = l1Params.MaxInitCodeSize
            };

            // Validate critical parameters are not null
            if (canonicalParams.InitialArbOSVersion == null)
            {
                throw new InvalidOperationException("InitialArbOSVersion cannot be null");
            }

            if (canonicalParams.InitialChainOwner == null)
            {
                throw new InvalidOperationException("InitialChainOwner cannot be null");
            }

            if (canonicalParams.GenesisBlockNum == null)
            {
                throw new InvalidOperationException("GenesisBlockNum cannot be null");
            }

            return canonicalParams;
        }
    }
}
