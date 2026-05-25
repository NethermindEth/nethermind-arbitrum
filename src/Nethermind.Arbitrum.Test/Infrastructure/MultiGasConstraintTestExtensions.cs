// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;

namespace Nethermind.Arbitrum.Test.Infrastructure;

/// <summary>
/// Test-only extension methods for <see cref="MultiGasConstraint"/>.
/// These provide convenience accessors that allocate (iterator, dictionary)
/// and are intentionally excluded from production code.
/// </summary>
public static class MultiGasConstraintTestExtensions
{
    /// <summary>
    /// Returns the resource kinds that have non-zero weights.
    /// </summary>
    public static IEnumerable<ResourceKind> UsedResources(this MultiGasConstraint constraint)
    {
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
            if (constraint.GetResourceWeight((ResourceKind)i) != 0)
                yield return (ResourceKind)i;
    }

    /// <summary>
    /// Returns all resources that have non-zero weights as a dictionary.
    /// </summary>
    public static Dictionary<ResourceKind, ulong> GetResourcesWithWeights(this MultiGasConstraint constraint)
    {
        Dictionary<ResourceKind, ulong> result = new();
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ulong weight = constraint.GetResourceWeight((ResourceKind)i);
            if (weight != 0)
                result[(ResourceKind)i] = weight;
        }
        return result;
    }
}
