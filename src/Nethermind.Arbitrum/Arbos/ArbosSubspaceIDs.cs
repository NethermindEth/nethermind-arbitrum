// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Arbos;

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
    public static readonly byte[] NativeTokenOwnerSubspace = [10];
}
