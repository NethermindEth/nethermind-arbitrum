// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class ArbosStateTestExtensions
{
    public static void SetCurrentArbosVersion(this PrecompileTestContextBuilder context, ulong version)
    {
        // Set the backing storage
        context.FreeArbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, version);

        // Update the property using reflection since it has a private setter
        System.Reflection.PropertyInfo? property = typeof(ArbosState).GetProperty("CurrentArbosVersion");
        property?.SetValue(context.FreeArbosState, version);
        property?.SetValue(context.ArbosState, version);
        context.FreeArbosState.L2PricingState.CurrentArbosVersion = version;
        context.ArbosState.L2PricingState.CurrentArbosVersion = version;
    }

    public static void SetL1BlockNumber(this Blockhashes blockHashes, ulong blockNumber)
    {
        blockHashes.RecordNewL1Block(blockNumber, ValueKeccak.Zero, ArbosVersion.Forty);
    }
}
