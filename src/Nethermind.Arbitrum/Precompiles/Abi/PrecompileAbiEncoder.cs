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
            return EthereumAbiEncoder.Decode(encodingStyle, signature, data);
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
}
