using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbAddressTable
{
    public const string Abi = "[{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"addressExists\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"compress\",\"outputs\":[{\"internalType\":\"bytes\",\"name\":\"\",\"type\":\"bytes\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes\",\"name\":\"buf\",\"type\":\"bytes\"},{\"internalType\":\"uint256\",\"name\":\"offset\",\"type\":\"uint256\"}],\"name\":\"decompress\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"lookup\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"index\",\"type\":\"uint256\"}],\"name\":\"lookupIndex\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"register\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"size\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";
    public static Address Address => ArbosAddresses.ArbAddressTableAddress;

    /// <summary>
    /// Checks if an address exists in the table
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="address">The address to check</param>
    /// <returns>True if the address exists in the table</returns>
    public static bool AddressExists(ArbitrumPrecompileExecutionContext context, Address address)
        => context.ArbosState.AddressTable.AddressExists(address);

    /// <summary>
    /// Compresses an address and returns the bytes that represent it
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="address">The address to compress</param>
    /// <returns>The compressed representation as bytes</returns>
    public static byte[] Compress(ArbitrumPrecompileExecutionContext context, Address address)
        => context.ArbosState.AddressTable.Compress(address);

    /// <summary>
    /// Decompresses the compressed bytes at the given offset with those of the corresponding account
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="buffer">The buffer containing compressed data</param>
    /// <param name="offset">The offset in the buffer</param>
    /// <returns>A tuple containing (address, bytesRead)</returns>
    /// <exception cref="ArbitrumPrecompileException">Thrown when the offset is invalid</exception>
    public static (Address Address, UInt256 BytesRead) Decompress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> buffer, UInt256 offset)
    {
        if (offset > int.MaxValue)
            throw ArbitrumPrecompileException.CreateFailureException($"Offset {offset} exceeds maximum allowed value {int.MaxValue} in ArbAddressTable.Decompress");

        int offsetValue = (int)offset;
        if (offsetValue > buffer.Length)
            throw ArbitrumPrecompileException.CreateFailureException($"Offset {offsetValue} exceeds buffer length {buffer.Length} in ArbAddressTable.Decompress");

        ReadOnlySpan<byte> bufferSpan = buffer[offsetValue..];

        (Address address, ulong bytesRead) = context.ArbosState.AddressTable.Decompress(bufferSpan);

        return (address, new UInt256(bytesRead));
    }

    /// <summary>
    /// Looks up the index of an address in the table
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="address">The address to look up</param>
    /// <returns>The index of the address in the table</returns>
    /// <exception cref="ArbitrumPrecompileException">Thrown when the address does not exist in the table</exception>
    public static UInt256 Lookup(ArbitrumPrecompileExecutionContext context, Address address)
    {
        (ulong index, bool exists) = context.ArbosState.AddressTable.Lookup(address);

        return !exists
            ? throw ArbitrumPrecompileException.CreateFailureException($"Address {address} does not exist in AddressTable")
            : new UInt256(index);
    }

    /// <summary>
    /// Looks up an address in the table by index
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="index">The index to look up</param>
    /// <returns>The address at the given index</returns>
    /// <exception cref="ArbitrumPrecompileException">Thrown when the index does not exist in the table</exception>
    public static Address LookupIndex(ArbitrumPrecompileExecutionContext context, UInt256 index)
    {
        if (index > ulong.MaxValue)
            throw ArbitrumPrecompileException.CreateFailureException($"Index {index} exceeds maximum allowed value {ulong.MaxValue} in ArbAddressTable.LookupIndex");

        ulong indexValue = (ulong)index;
        (Address address, bool exists) = context.ArbosState.AddressTable.LookupIndex(indexValue);

        return !exists
            ? throw ArbitrumPrecompileException.CreateFailureException($"Index {indexValue} does not exist in AddressTable (table size: {context.ArbosState.AddressTable.Size()})")
            : address;
    }

    /// <summary>
    /// Registers an address in the table, shrinking its compressed representation
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <param name="address">The address to register</param>
    /// <returns>The index assigned to the address</returns>
    public static UInt256 Register(ArbitrumPrecompileExecutionContext context, Address address)
    {
        ulong slot = context.ArbosState.AddressTable.Register(address);
        return new UInt256(slot);
    }

    /// <summary>
    /// Gets the number of addresses in the table
    /// </summary>
    /// <param name="context">The precompile execution context</param>
    /// <returns>The size of the table</returns>
    public static UInt256 Size(ArbitrumPrecompileExecutionContext context)
    {
        ulong size = context.ArbosState.AddressTable.Size();
        return new UInt256(size);
    }
}
