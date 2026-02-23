// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

/// <summary>
/// Helper class for encoding calldata for the Stylus create contract.
/// The create contract deploys new contracts using CREATE or CREATE2.
/// See nitro/arbitrator/stylus/tests/create/src/main.rs for reference.
/// </summary>
public static class CreateContractCallData
{
    private const byte KindCreate1 = 0x01;
    private const byte KindCreate2 = 0x02;

    /// <summary>
    /// Creates calldata for deploying a contract using CREATE.
    /// Format: [kind:1][endowment:32][code:N]
    /// </summary>
    /// <param name="code">Bytecode to deploy</param>
    /// <param name="endowment">Value to send to the created contract (optional, defaults to zero)</param>
    public static byte[] CreateCreate1CallData(byte[] code, byte[]? endowment = null)
    {
        endowment ??= new byte[32]; // Zero endowment by default

        if (endowment.Length != 32)
            throw new ArgumentException("Endowment must be 32 bytes", nameof(endowment));

        // Total size: kind(1) + endowment(32) + code
        int totalSize = 1 + 32 + code.Length;
        byte[] result = new byte[totalSize];

        int offset = 0;

        // Kind: 1 byte (0x01 for CREATE1)
        result[offset++] = KindCreate1;

        // Endowment: 32 bytes big-endian
        endowment.CopyTo(result.AsSpan(offset, 32));
        offset += 32;

        // Code
        code.CopyTo(result.AsSpan(offset));

        return result;
    }

    /// <summary>
    /// Creates calldata for deploying a contract using CREATE2.
    /// Format: [kind:1][endowment:32][salt:32][code:N]
    /// </summary>
    /// <param name="code">Bytecode to deploy</param>
    /// <param name="salt">Salt for CREATE2 address derivation</param>
    /// <param name="endowment">Value to send to the created contract (optional, defaults to zero)</param>
    public static byte[] CreateCreate2CallData(byte[] code, Hash256 salt, byte[]? endowment = null)
    {
        endowment ??= new byte[32]; // Zero endowment by default

        if (endowment.Length != 32)
            throw new ArgumentException("Endowment must be 32 bytes", nameof(endowment));

        // Total size: kind(1) + endowment(32) + salt(32) + code
        int totalSize = 1 + 32 + 32 + code.Length;
        byte[] result = new byte[totalSize];

        int offset = 0;

        // Kind: 1 byte (0x02 for CREATE2)
        result[offset++] = KindCreate2;

        // Endowment: 32 bytes big-endian
        endowment.CopyTo(result.AsSpan(offset, 32));
        offset += 32;

        // Salt: 32 bytes
        salt.Bytes.CopyTo(result.AsSpan(offset, 32));
        offset += 32;

        // Code
        code.CopyTo(result.AsSpan(offset));

        return result;
    }
}
