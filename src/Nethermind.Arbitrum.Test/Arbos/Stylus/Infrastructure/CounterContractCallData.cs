// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public static class CounterContractCallData
{
    /* Stylus contract interface for a simple counter

    interface ICounter  {
        function number() external view returns (uint256);
        function setNumber(uint256 new_number) external;
        function mulNumber(uint256 new_number) external;
        function addNumber(uint256 new_number) external;
        function increment() external;
        function addFromMsgValue() external payable;
    }
    */

    public static byte[] GetNumberCalldata()
    {
        return GetFunctionSelector("number()");
    }

    public static byte[] GetSetNumberCalldata(ulong value)
    {
        return GetCallDataWithUint256("setNumber(uint256)", value);
    }

    public static byte[] GetMulNumberCalldata(ulong value)
    {
        return GetCallDataWithUint256("mulNumber(uint256)", value);
    }

    public static byte[] GetAddNumberCalldata(ulong value)
    {
        return GetCallDataWithUint256("addNumber(uint256)", value);
    }

    public static byte[] GetIncrementCalldata()
    {
        return GetFunctionSelector("increment()");
    }

    public static byte[] GetAddFromMsgValueCalldata()
    {
        return GetFunctionSelector("addFromMsgValue()");
    }

    private static byte[] GetFunctionSelector(string signature)
    {
        // Get Keccak256 hash of the signature
        byte[] hash = KeccakHash.ComputeHashBytes(Encoding.UTF8.GetBytes(signature));

        // Take first 4 bytes as selector
        byte[] selector = new byte[4];
        Array.Copy(hash, 0, selector, 0, 4);

        return selector;
    }

    private static byte[] GetCallDataWithUint256(string signature, ulong value)
    {
        byte[] selector = GetFunctionSelector(signature);
        byte[] parameter = EncodeUint256(value);

        byte[] calldata = new byte[selector.Length + parameter.Length];
        Array.Copy(selector, 0, calldata, 0, selector.Length);
        Array.Copy(parameter, 0, calldata, selector.Length, parameter.Length);

        return calldata;
    }

    private static byte[] EncodeUint256(ulong value)
    {
        byte[] encoded = new byte[32];
        byte[] valueBytes = BitConverter.GetBytes(value);

        if (BitConverter.IsLittleEndian)
            Array.Reverse(valueBytes);

        // Copy to the end of the 32-byte array (right-padded with zeros)
        Array.Copy(valueBytes, 0, encoded, 32 - valueBytes.Length, valueBytes.Length);

        return encoded;
    }
}
