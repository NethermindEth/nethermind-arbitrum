// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Core;
using Nethermind.Evm;
using Nethermind.Int256;

public static class TransactionExecutionExtensions
{
    /// <summary>
    /// Returns the original (non-zeroed) base fee — Nitro's BaseFeeInBlock
    /// (go-ethereum:consensus-v51/core/vm/evm.go:BlockContext). Use for internal Arbitrum gas accounting
    /// (fee distribution, L1 poster costs, precompile pricing).
    ///
    /// Do NOT use for EVM validation (premium checks, fee cap checks) — use header.BaseFeePerGas
    /// instead, which corresponds to Nitro's BaseFee and is zeroed during eth_call.
    /// See go-ethereum:consensus-v51/core/state_transition.go:execute for validation,
    /// nitro:v3.9.8/arbos/tx_processor.go:GetPaidGasPrice for where BaseFeeInBlock is consumed.
    /// </summary>
    public static UInt256 GetEffectiveBaseFeeForGasCalculations(this in BlockExecutionContext context)
    {
        // Check if we're using an ArbitrumBlockHeader with original base fee
        if (context.Header is ArbitrumBlockHeader arbitrumHeader)
        {
            return arbitrumHeader.OriginalBaseFee;
        }

        // Fallback to header's base fee
        return context.Header.BaseFeePerGas;
    }
}
