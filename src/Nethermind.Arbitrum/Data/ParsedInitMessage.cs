// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Data
{
    public class ParsedInitMessage(
        ulong chainId,
        UInt256 initialBaseFee,
        ChainConfig? chainConfigSpec = null,
        byte[]? serializedChainConfig = null) : IArbitrumTransactionData
    {
        public ulong ChainId = chainId;

        public UInt256 InitialBaseFee = initialBaseFee;

        public ChainConfig? ChainConfigSpec = chainConfigSpec;

        public byte[]? SerializedChainConfig = serializedChainConfig;

        public bool IsCompatibleWith(ChainSpec localChainSpec, ArbitrumChainSpecEngineParameters localArbitrumParams)
        {
            // Chain ID must match exactly
            if (ChainId != localChainSpec.ChainId)
            {
                return false;
            }

            // If we have a parsed chain config from L1, validate Arbitrum parameters
            if (ChainConfigSpec?.ArbitrumChainParams != null)
            {
                var l1ArbitrumParams = ChainConfigSpec.ArbitrumChainParams;

                // Key Arbitrum parameters must match
                if (localArbitrumParams.EnableArbOS.HasValue &&
                    l1ArbitrumParams.Enabled != localArbitrumParams.EnableArbOS.Value)
                {
                    return false;
                }

                if (localArbitrumParams.InitialArbOSVersion.HasValue &&
                    l1ArbitrumParams.InitialArbOSVersion != localArbitrumParams.InitialArbOSVersion.Value)
                {
                    return false;
                }

                if (localArbitrumParams.InitialChainOwner != null &&
                    l1ArbitrumParams.InitialChainOwner != localArbitrumParams.InitialChainOwner)
                {
                    return false;
                }

                if (localArbitrumParams.GenesisBlockNum.HasValue &&
                    l1ArbitrumParams.GenesisBlockNum != localArbitrumParams.GenesisBlockNum.Value)
                {
                    return false;
                }

                if (localArbitrumParams.DataAvailabilityCommittee != null &&
                    l1ArbitrumParams.DataAvailabilityCommittee != localArbitrumParams.DataAvailabilityCommittee)
                {
                    return false;
                }
            }

            return true;
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
            return new ArbitrumChainSpecEngineParameters
            {
                Enabled = l1Params.Enabled,
                InitialArbOSVersion = l1Params.InitialArbOSVersion,
                InitialChainOwner = l1Params.InitialChainOwner ?? specHelper.InitialChainOwner,
                GenesisBlockNum = l1Params.GenesisBlockNum,
                AllowDebugPrecompiles = l1Params.AllowDebugPrecompiles,
                DataAvailabilityCommittee = l1Params.DataAvailabilityCommittee,
                SerializedChainConfig = SerializedChainConfig != null ? Convert.ToBase64String(SerializedChainConfig) : null,
                MaxCodeSize = l1Params.MaxCodeSize ?? specHelper.MaxCodeSize,
                MaxInitCodeSize = l1Params.MaxInitCodeSize ?? specHelper.MaxInitCodeSize
            };
        }
    }
}
