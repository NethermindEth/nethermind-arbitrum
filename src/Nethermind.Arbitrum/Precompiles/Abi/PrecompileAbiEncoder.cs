// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Precompiles.Abi;

public class PrecompileAbiEncoder : IAbiEncoder
{
    public static readonly PrecompileAbiEncoder Instance = new();

    private static readonly AbiEncoder EthereumAbiEncoder = AbiEncoder.Instance;

    public object[] Decode(AbiEncodingStyle encodingStyle, AbiSignature signature, byte[] data)
    {
        try
        {
            return DecodeAndIgnoreExtraData(encodingStyle, signature, data);
        }
        catch (Exception e)
        {
            throw ArbitrumPrecompileException.CreateRevertException(
                $"Failed to decode data {data.ToHexString()} with signature {signature}, got exception {e}",
                calldataDecoding: true
            );
        }
    }

    public byte[] Encode(AbiEncodingStyle encodingStyle, AbiSignature signature, params object[] arguments)
    {
        try
        {
            return EthereumAbiEncoder.Encode(encodingStyle, signature, arguments);
        }
        catch (Exception e)
        {
            throw ArbitrumPrecompileException.CreateRevertException(
                $"Failed to encode arguments {arguments} with signature {signature}, got exception {e}"
            );
        }
    }

    /// <summary>
    /// Abi decoding which does not throw exception if there is unparsed data beyond what is described in the signature
    /// </summary>
    /// <param name="encodingStyle"></param>
    /// <param name="signature"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="AbiException"></exception>
    private object[] DecodeAndIgnoreExtraData(AbiEncodingStyle encodingStyle, AbiSignature signature, byte[] data)
    {
        bool packed = (encodingStyle & AbiEncodingStyle.Packed) == AbiEncodingStyle.Packed;
        bool includeSig = encodingStyle == AbiEncodingStyle.IncludeSignature;
        int sigOffset = includeSig ? 4 : 0;
        if (includeSig)
        {
            if (!Bytes.AreEqual(AbiSignature.GetAddress(data), signature.Address))
            {
                throw new AbiException($"Signature in encoded ABI data is not consistent with {signature}");
            }
        }

        (object[] arguments, int position) = AbiType.DecodeSequence(signature.Types.Length, signature.Types, data, packed, sigOffset);

        return arguments;
    }
}
