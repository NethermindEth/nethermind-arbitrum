// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Arbos.Storage;

/// <summary>
/// SubStorageVector is a storage space that contains a vector of sub-storages.
/// It keeps track of the number of sub-storages and allows accessing them by index.
/// </summary>
public class SubStorageVector(ArbosStorage storage)
{
    private const ulong LengthOffset = 0;

    private readonly ArbosStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly ArbosStorageBackedULong _length = new(storage, LengthOffset);

    /// <summary>
    /// Returns the number of sub-storages in the vector.
    /// </summary>
    public ulong Length()
    {
        return _length.Get();
    }

    /// <summary>
    /// Returns the sub-storage at the given index.
    /// NOTE: This method does not verify bounds.
    /// </summary>
    public ArbosStorage At(ulong index)
    {
        // Encode index as a big-endian byte array for sub-storage ID
        byte[] id = new byte[sizeof(ulong)];
        for (int i = 0; i < sizeof(ulong); i++)
            id[i] = (byte)(index >> (8 * (7 - i)));

        return _storage.OpenSubStorage(id);
    }

    /// <summary>
    /// Adds a new sub-storage at the end of the vector and returns it.
    /// </summary>
    public ArbosStorage Push()
    {
        ulong length = _length.Get();

        // Encode length as a big-endian byte array for sub-storage ID
        byte[] id = new byte[sizeof(ulong)];
        for (int i = 0; i < sizeof(ulong); i++)
            id[i] = (byte)(length >> (8 * (7 - i)));

        ArbosStorage subStorage = _storage.OpenSubStorage(id);
        _length.Set(length + 1);

        return subStorage;
    }

    /// <summary>
    /// Removes and returns the last sub-storage from the vector.
    /// </summary>
    public ArbosStorage Pop()
    {
        ulong length = _length.Get();
        if (length == 0)
            throw new InvalidOperationException("sub-storage vector: can't pop empty");

        // Encode (length - 1) as a big-endian byte array for sub-storage ID
        byte[] id = new byte[sizeof(ulong)];
        for (int i = 0; i < sizeof(ulong); i++)
            id[i] = (byte)((length - 1) >> (8 * (7 - i)));

        ArbosStorage subStorage = _storage.OpenSubStorage(id);
        _length.Set(length - 1);

        return subStorage;
    }
}
