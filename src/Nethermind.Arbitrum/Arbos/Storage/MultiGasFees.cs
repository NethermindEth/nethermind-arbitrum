// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Storage;

/// <summary>
/// Tracks per-resource-kind base fees for multi-gas pricing.
/// <para>
/// Storage layout (16 slots total):
/// <list type="bullet">
///   <item>[0..7] nextBlockFees[0..7] - base fees for future blocks</item>
///   <item>[8..15] currentBlockFees[0..7] - base fees for the current block</item>
/// </list>
/// </para>
/// <para>
/// The next field is the base fee for future blocks, updated alongside l2pricing.baseFee
/// whenever updateMultiGasConstraintsBacklogs is called.
/// The current field is the base fee for the current block, updated in ProduceBlockAdvanced
/// before executing transactions.
/// </para>
/// </summary>
public sealed class MultiGasFees
{
    private const ulong NextBlockFeesOffset = 0;
    private const ulong CurrentBlockFeesOffset = 8; // NumResourceKinds

    private readonly ArbosStorageBackedUInt256[] _nextBlockFees;
    private readonly ArbosStorageBackedUInt256[] _currentBlockFees;

    public MultiGasFees(ArbosStorage storage)
    {
        _nextBlockFees = new ArbosStorageBackedUInt256[MultiGas.NumResourceKinds];
        _currentBlockFees = new ArbosStorageBackedUInt256[MultiGas.NumResourceKinds];

        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            _nextBlockFees[i] = new ArbosStorageBackedUInt256(storage, NextBlockFeesOffset + (ulong)i);
            _currentBlockFees[i] = new ArbosStorageBackedUInt256(storage, CurrentBlockFeesOffset + (ulong)i);
        }
    }

    /// <summary>
    /// Gets the next-block base fee for the specified resource kind.
    /// </summary>
    public UInt256 GetNextBlockFee(ResourceKind kind)
    {
        MultiGas.CheckResourceKind(kind);
        return _nextBlockFees[(int)kind].Get();
    }

    /// <summary>
    /// Gets the current-block base fee for the specified resource kind.
    /// </summary>
    public UInt256 GetCurrentBlockFee(ResourceKind kind)
    {
        MultiGas.CheckResourceKind(kind);
        return _currentBlockFees[(int)kind].Get();
    }

    /// <summary>
    /// Sets the next-block base fee for the specified resource kind.
    /// </summary>
    public void SetNextBlockFee(ResourceKind kind, UInt256 value)
    {
        MultiGas.CheckResourceKind(kind);
        _nextBlockFees[(int)kind].Set(value);
    }

    /// <summary>
    /// Rotates next-block fees into current-block fees.
    /// Called at the start of a new block.
    /// </summary>
    public void CommitNextToCurrent()
    {
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            UInt256 nextFee = _nextBlockFees[i].Get();
            _currentBlockFees[i].Set(nextFee);
        }
    }
}
