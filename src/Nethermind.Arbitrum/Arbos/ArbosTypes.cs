// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos
{
    public static class ArbosConstants
    {
        public static class ArbosStateOffsets
        {
            public const ulong VersionOffset = 0;
            public const ulong UpgradeVersionOffset = 1;
            public const ulong UpgradeTimestampOffset = 2;
            public const ulong NetworkFeeAccountOffset = 3;
            public const ulong ChainIdOffset = 4;
            public const ulong GenesisBlockNumOffset = 5;
            public const ulong InfraFeeAccountOffset = 6;
            public const ulong BrotliCompressionLevelOffset = 7;
        }

        public static class ArbosSubspaceIDs
        {
            public static readonly byte[] L1PricingSubspace = [0];
            public static readonly byte[] L2PricingSubspace = [1];
            public static readonly byte[] RetryablesSubspace = [2];
            public static readonly byte[] AddressTableSubspace = [3];
            public static readonly byte[] ChainOwnerSubspace = [4];
            public static readonly byte[] SendMerkleSubspace = [5];
            public static readonly byte[] BlockhashesSubspace = [6];
            public static readonly byte[] ChainConfigSubspace = [7];
            public static readonly byte[] ProgramsSubspace = [8];
            public static readonly byte[] FeaturesSubspace = [9];
        }
    }

    public class ParsedInitMessage
    {
        public byte[] SerializedChainConfig { get; set; } = [];
        public Int256.Int256 InitialL1BaseFee { get; set; } = Int256.Int256.Zero;
    }
}
