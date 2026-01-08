// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Arbos.Storage;

public sealed class AddressTable(ArbosStorage storage)
{
    private readonly ArbosStorage _backingStorage = storage;
    private readonly ArbosStorage _byAddressStorage = storage.OpenSubStorage([]); // 0 means item isn't in the table; n > 0 means it's in the table at slot n-1
    private readonly ArbosStorageBackedULong _numItemsStorage = new(storage, 0);

    public static void Initialize(ArbosStorage _)
    {
        // No initialization needed.
    }

    /// <summary>
    /// Registers an address in the table and returns its index.
    /// If the address is already registered, returns its existing index.
    /// </summary>
    /// <param name="address">The address to register</param>
    /// <returns>The index of the address in the table (0-based)</returns>
    public ulong Register(Address address)
    {
        ValueHash256 addressAsHash = address.ToHash();
        ValueHash256 existingIndex = _byAddressStorage.Get(addressAsHash);

        if (existingIndex != default)
            return (ulong)existingIndex.ToUInt256() - 1;

        // Address isn't in the table, so add it
        ulong newNumItems = _numItemsStorage.Increment();

        _backingStorage.Set(newNumItems, addressAsHash);
        _byAddressStorage.Set(addressAsHash, new UInt256(newNumItems).ToValueHash());

        return newNumItems - 1;
    }

    /// <summary>
    /// Looks up an address in the table and returns its index if found.
    /// </summary>
    /// <param name="address">The address to look up</param>
    /// <returns>A tuple containing (index, exists) where exists indicates if the address was found</returns>
    public (ulong, bool) Lookup(Address address)
    {
        ValueHash256 addressAsHash = address.ToHash();
        ulong result = _byAddressStorage.GetULong(addressAsHash);

        return result == 0 ? (0, false) : (result - 1, true);
    }

    /// <summary>
    /// Checks if an address exists in the table.
    /// </summary>
    /// <param name="address">The address to check</param>
    /// <returns>True if the address exists in the table</returns>
    public bool AddressExists(Address address) => Lookup(address).Item2;

    /// <summary>
    /// Returns the number of addresses in the table.
    /// </summary>
    /// <returns>The size of the table</returns>
    public ulong Size() => _numItemsStorage.Get();

    /// <summary>
    /// Looks up an address by its index in the table.
    /// </summary>
    /// <param name="index">The index to look up (0-based)</param>
    /// <returns>A tuple containing (address, exists) where exists indicates if the index was valid</returns>
    public (Address, bool) LookupIndex(ulong index)
    {
        ulong items = _numItemsStorage.Get();
        if (index >= items)
            return (Address.Zero, false);

        ValueHash256 value = _backingStorage.Get(index + 1);
        // Starting from 12 bytes to get the address part
        return (new Address(value.Bytes[12..]), true);
    }

    /// <summary>
    /// Compresses an address. If the address is in the table, returns its index as RLP-encoded bytes.
    /// If not in the table, returns the full address as RLP-encoded bytes.
    /// </summary>
    /// <param name="address">The address to compress</param>
    /// <returns>The compressed representation as bytes</returns>
    public byte[] Compress(Address address)
    {
        (ulong index, bool exists) = Lookup(address);

        return exists
            ? Rlp.Encode(index).Bytes
            : Rlp.Encode(address.Bytes).Bytes;
    }

    /// <summary>
    /// Decompresses a compressed address representation back to an address.
    /// </summary>
    /// <param name="buffer">The compressed data</param>
    /// <returns>A tuple containing (address, bytesRead) where bytesRead is the number of bytes consumed from the buffer</returns>
    /// <exception cref="InvalidOperationException">Thrown when the compressed data contains an invalid index</exception>
    public (Address, ulong) Decompress(ReadOnlySpan<byte> buffer)
    {
        RlpStream rlpStream = new(buffer.ToArray()); // Note: ToArray allocation unavoidable due to RlpStream API

        // Peek at the decoded item to determine if it's an address or index
        (_, int contentLength) = rlpStream.PeekPrefixAndContentLength();

        if (contentLength == 20)
        {
            // Full address
            byte[] decodedAddressBytes = rlpStream.DecodeByteArray();
            ulong bytesRead = (ulong)rlpStream.Position;
            return (new Address(decodedAddressBytes), bytesRead);
        }

        // Could be an index or a large number representing a full address
        ulong index = rlpStream.DecodeULong();
        ulong bytesConsumed = (ulong)rlpStream.Position;
        (Address address, bool exists) = LookupIndex(index);
        if (!exists)
            throw new InvalidOperationException("Invalid index in compressed address");

        return (address, bytesConsumed);
    }
}
