// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers.Binary;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Int256;
using static Nethermind.Core.Extensions.Bytes;

namespace Nethermind.Arbitrum.Test.Infrastructure;

/// <summary>
/// Helper methods for parser tests to create method call data
/// </summary>
public static class ParserTestHelpers
{
    /// <summary>
    /// Creates method call data from a method ID string and optional parameters
    /// </summary>
    /// <param name="methodId">The method ID as hex string (e.g., "0xa5025222")</param>
    /// <param name="parameters">Optional parameters as hex string without 0x prefix</param>
    /// <returns>Byte array containing the method call data</returns>
    public static byte[] CreateMethodCallDataFromHex(string methodId, ReadOnlySpan<char> parameters = default)
    {
        string hexString = methodId + new string(parameters);
        return FromHexString(hexString);
    }

    /// <summary>
    /// Creates method call data from a method signature using MethodIdHelper
    /// </summary>
    /// <param name="methodSignature">The method signature (e.g., "stylusVersion()")</param>
    /// <returns>Byte array containing the method call data</returns>
    public static byte[] CreateMethodCallData(string methodSignature)
    {
        uint methodId = MethodIdHelper.GetMethodId(methodSignature);
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, methodId);
        return data;
    }

    /// <summary>
    /// Creates method call data with an address parameter
    /// </summary>
    /// <param name="methodSignature">The method signature</param>
    /// <param name="address">The address parameter</param>
    /// <returns>Byte array containing the method call data with address</returns>
    public static byte[] CreateMethodCallDataWithAddress(string methodSignature, Address address)
    {
        uint methodId = MethodIdHelper.GetMethodId(methodSignature);
        byte[] data = new byte[36]; // 4 bytes method ID + 32 bytes address
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0, 4), methodId);

        // Encode address as 32-byte value (left-padded with zeros)
        address.Bytes.CopyTo(data, 4 + (32 - Address.Size));

        return data;
    }

    /// <summary>
    /// Creates method call data with a bytes32 parameter
    /// </summary>
    /// <param name="methodSignature">The method signature</param>
    /// <param name="bytes32">The 32-byte parameter</param>
    /// <returns>Byte array containing the method call data with bytes32</returns>
    public static byte[] CreateMethodCallDataWithBytes32(string methodSignature, ReadOnlySpan<byte> bytes32)
    {
        uint methodId = MethodIdHelper.GetMethodId(methodSignature);
        byte[] data = new byte[36]; // 4 bytes method ID + 32 bytes hash
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0, 4), methodId);
        bytes32.CopyTo(data.AsSpan(4, 32));
        return data;
    }

    /// <summary>
    /// Creates method call data with a UInt256 parameter
    /// </summary>
    /// <param name="methodSignature">The method signature</param>
    /// <param name="value">The UInt256 parameter</param>
    /// <returns>Byte array containing the method call data with UInt256</returns>
    public static byte[] CreateMethodCallDataWithUInt256(string methodSignature, UInt256 value)
    {
        uint methodId = MethodIdHelper.GetMethodId(methodSignature);
        byte[] data = new byte[36]; // 4 bytes method ID + 32 bytes value
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0, 4), methodId);
        value.ToBigEndian(data.AsSpan(4, 32));
        return data;
    }
}
