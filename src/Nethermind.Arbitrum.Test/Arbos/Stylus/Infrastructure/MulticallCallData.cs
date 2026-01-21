// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

/// <summary>
/// Helper class for encoding calldata for the multicall Stylus contract.
/// The multicall contract batches multiple call operations.
/// See nitro/system_tests/program_test.go argsForMulticall|multicall* methods for reference.
/// </summary>
public static class MulticallCallData
{
    private const byte KindCall = 0x00;
    private const byte KindDelegateCall = 0x01;
    private const byte KindStaticCall = 0x02;

    /// <summary>
    /// Creates multicall data for a single CALL operation.
    /// Format: [count:1][length:4][kind:1][value:32][address:20][calldata:N]
    /// </summary>
    /// <param name="target">Target contract address</param>
    /// <param name="calldata">Calldata to pass to the target</param>
    /// <param name="value">Optional value to send with the call (defaults to zero)</param>
    public static byte[] CreateCall(Address target, byte[] calldata, UInt256? value = null)
    {
        return EncodeCall(KindCall, target, calldata, value ?? UInt256.Zero);
    }

    /// <summary>
    /// Creates multicall data for a single DELEGATECALL operation.
    /// Format: [count:1][length:4][kind:1][address:20][calldata:N]
    /// </summary>
    /// <param name="target">Target contract address</param>
    /// <param name="calldata">Calldata to pass to the target</param>
    public static byte[] CreateDelegateCall(Address target, byte[] calldata)
    {
        return EncodeCall(KindDelegateCall, target, calldata, null);
    }

    /// <summary>
    /// Creates multicall data for a single STATICCALL operation.
    /// Format: [count:1][length:4][kind:1][address:20][calldata:N]
    /// </summary>
    /// <param name="target">Target contract address</param>
    /// <param name="calldata">Calldata to pass to the target</param>
    public static byte[] CreateStaticCall(Address target, byte[] calldata)
    {
        return EncodeCall(KindStaticCall, target, calldata, null);
    }

    private static byte[] EncodeCall(byte kind, Address target, byte[] calldata, UInt256? value)
    {
        bool includeValue = kind == KindCall;

        // Calculate action length: kind(1) + [value(32)] + address(20) + calldata
        int actionLength = 1 + (includeValue ? 32 : 0) + 20 + calldata.Length;

        // Total size: count(1) + length(4) + action
        int totalSize = 1 + 4 + actionLength;
        byte[] result = new byte[totalSize];

        int offset = 0;

        // Count: 1 action
        result[offset++] = 1;

        // Length: 4 bytes big-endian
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(offset, 4), (uint)actionLength);
        offset += 4;

        // Kind: 1 byte
        result[offset++] = kind;

        // Value: 32 bytes big-endian (only for CALL)
        if (includeValue)
        {
            UInt256 val = value ?? UInt256.Zero;
            val.ToBigEndian(result.AsSpan(offset, 32));
            offset += 32;
        }

        // Address: 20 bytes
        target.Bytes.CopyTo(result.AsSpan(offset, 20));
        offset += 20;

        // Calldata
        calldata.CopyTo(result.AsSpan(offset));

        return result;
    }
}
