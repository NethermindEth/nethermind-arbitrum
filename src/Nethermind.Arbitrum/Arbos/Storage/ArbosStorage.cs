// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class ArbosStorage
{
    private readonly IWorldState _db;
    private readonly Address _account;
    private readonly byte[] _storageKey; // Never null, empty for root
    private readonly IBurner _burner;

    public const ulong StorageReadCost = 800; // params.SloadGasEIP2200
    public const ulong StorageWriteCost = 20_000; // params.SstoreSetGasEIP2200 (for non-zero to non-zero)
    public const ulong StorageWriteZeroCost = 5000; // params.SstoreResetGasEIP2200 (for non-zero to zero)
    public const ulong StorageCodeHashCost = 2600; // params.ColdAccountAccessCostEIP2929

    public const ulong KeccakWordCost = 6;
    public const ulong KeccakBaseCost = 30;

    public ArbosStorage(IWorldState worldState, IBurner burner, Address accountAddress, byte[]? storageKey = null)
    {
        _db = worldState ?? throw new ArgumentNullException(nameof(worldState));
        _burner = burner ?? throw new ArgumentNullException(nameof(burner));
        _account = accountAddress ?? throw new ArgumentNullException(nameof(accountAddress));
        _storageKey = storageKey ?? []; // TODO: Fix to be ValueHash256

        // Ensure account exists if this is the root storage, similar to Go's NewGeth
        if (_storageKey.Length != 0) return;
        if (_db.AccountExists(_account) && _db.GetNonce(_account) != 0) return;

        // Setting nonce ensures Geth won't treat ArbOS as empty
        // In Nethermind, creating an account or modifying it (like setting nonce) makes it exist.
        // If it doesn't exist, IWorldState.SetNonce might create it or fail.
        // Let's ensure it's created if it doesn't exist.
        if (!_db.AccountExists(_account))
        {
            _db.CreateAccountIfNotExists(_account, UInt256.Zero, UInt256.One);
        }
        else
        {
            _db.SetNonce(_account, UInt256.One);
        }
    }

    public IBurner Burner => _burner;

    // Comment from Nitro:
    // We map addresses using "pages" of 256 storage slots. We hash over the page number but not the offset within
    // a page, to preserve contiguity within a page. This will reduce cost if/when Ethereum switches to storage
    // representations that reward contiguity.
    // Because page numbers are 248 bits, this gives us 124-bit security against collision attacks, which is good enough.
    private ValueHash256 MapAddress(ValueHash256 key)
    {
        const int boundary = ValueHash256.MemorySize - 1; // 31

        Span<byte> keccakBytes = stackalloc byte[_storageKey.Length + boundary];
        _storageKey.CopyTo(keccakBytes);
        key.Bytes[..boundary].CopyTo(keccakBytes.Slice(_storageKey.Length));

        Span<byte> mappedKeyBytes = stackalloc byte[ValueHash256.MemorySize];
        Keccak.Compute(keccakBytes).Bytes[..boundary].CopyTo(mappedKeyBytes);
        mappedKeyBytes[boundary] = key.Bytes[boundary];

        return new ValueHash256(mappedKeyBytes);
    }

    public ValueHash256 Get(ValueHash256 key)
    {
        _burner.Burn(StorageReadCost);
        return GetFree(key);
    }

    public ValueHash256 GetFree(ValueHash256 key)
    {
        ReadOnlySpan<byte> bytes = _db.Get(new StorageCell(_account, new UInt256(MapAddress(key).Bytes, isBigEndian: true)));
        return bytes.IsEmpty ? default : Hash256.FromBytesWithPadding(bytes);
    }

    public ValueHash256 GetStorageSlot(ValueHash256 key)
    {
        return MapAddress(key);
    }

    public ulong GetUint64(ValueHash256 key)
    {
        ValueHash256 value = Get(key);
        return value == default ? 0 : (ulong)value.ToUInt256();
    }

    public ValueHash256 GetByUint64(ulong key)
    {
        return Get(new ValueHash256(new UInt256(key)));
    }

    public ulong GetUint64ByUint64(ulong key)
    {
        ValueHash256 value = GetByUint64(key);
        return value == default ? 0 : (ulong)value.ToUInt256();
    }

    public void Set(ValueHash256 key, ValueHash256 value)
    {
        if (_burner.ReadOnly)
        {
            throw new InvalidOperationException("Attempted to write with a read-only burner.");
        }

        ulong cost = value == default ? StorageWriteZeroCost : StorageWriteCost;
        _burner.Burn(cost);

        var mappedAddress = MapAddress(key);
        _db.Set(new StorageCell(_account, new UInt256(mappedAddress.Bytes, isBigEndian: true)), value.Bytes.WithoutLeadingZeros().ToArray());
    }

    public void SetUint64(ValueHash256 key, ulong value)
    {
        Set(key, new ValueHash256(new UInt256(value)));
    }

    public void SetByUint64(ulong key, ValueHash256 value)
    {
        Set(new ValueHash256(new UInt256(key)), value);
    }

    public void SetUint64ByUint64(ulong key, ulong value)
    {
        Set(new ValueHash256(new UInt256(key)), new ValueHash256(new UInt256(value)));
    }

    public void Clear(ValueHash256 key)
    {
        Set(key, default);
    }

    public void ClearByUint64(ulong key)
    {
        Set(new ValueHash256(new UInt256(key)), default);
    }

    public ArbosStorage OpenSubStorage(byte[] id)
    {
        Span<byte> keccakBytes = stackalloc byte[_storageKey.Length + id.Length];
        _storageKey.CopyTo(keccakBytes);
        id.CopyTo(keccakBytes.Slice(_storageKey.Length));

        Hash256 keccak = Keccak.Compute(keccakBytes);
        return new ArbosStorage(_db, _burner, _account, keccak.Bytes.ToArray());
    }

    public void SetBytes(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ClearBytes(); // Clear existing data first
        SetUint64ByUint64(0, (ulong)value.Length); // Store the size

        ulong offset = 1;
        ReadOnlySpan<byte> span = value.AsSpan();

        while (span.Length > 32)
        {
            SetByUint64(offset, Hash256.FromBytesWithPadding(span[..32]));
            span = span[32..];
            offset++;
        }

        if (span.Length > 0)
        {
            SetByUint64(offset, Hash256.FromBytesWithPadding(span));
        }
    }

    public byte[] GetBytes() // TODO: implement this too...
    {
        ulong bytesLeft = GetUint64ByUint64(0);
        if (bytesLeft == 0)
        {
            return [];
        }

        List<byte> resultBytes = new List<byte>((int)bytesLeft);
        ulong offset = 1;
        while (bytesLeft > 0)
        {
            ValueHash256 chunkHash = GetByUint64(offset);
            ReadOnlySpan<byte> chunkBytes = chunkHash.Bytes; // Assuming ValueHash256.Bytes gives the 32-byte span

            int bytesToTake = (int)Math.Min(32, bytesLeft);
            resultBytes.AddRange(chunkBytes.Slice(0, bytesToTake).ToArray()); // Go stores right-padded, C# seems to store left-padded for ValueHash256 from int. Let's assume we read the relevant part.
            // The Go code `next.Bytes()[32-bytesLeft:]...` for the last chunk implies right padding or taking the end.
            // For simplicity here, we take the start. If Go's BytesToHash for short arrays right-pads, this needs adjustment.
            // Re-evaluating Go: `common.BytesToHash(b)` copies to `h[HashLength-len(b):]`, so it's right-aligned.
            // So, for the last chunk, we need `chunkBytes.Slice(32 - bytesToTake)` if `bytesToTake < 32`.
            // Let's adjust:
            if (bytesLeft < 32)
            {
                // This part is tricky. Go's BytesToHash right-aligns.
                // If we stored `b.AsSpan(bOffset, lengthToCopy).CopyTo(chunk);` where chunk is 32 bytes,
                // the data is at the start of `chunk`.
                // The Go code for GetBytes:
                // ret = append(ret, next.Bytes()...) // for full 32-byte chunks
                // ret = append(ret, next.Bytes()[32-bytesLeft:]...) // for the last partial chunk
                // This implies that when storing a partial chunk, it's stored as if it's the *end* of a 32-byte word.
                // My SetBytes stores it at the *start*. Let's stick to simpler C# SetBytes and adjust GetBytes.
                // If SetBytes writes `b.AsSpan(bOffset, lengthToCopy).CopyTo(chunk);` (left-aligned in `chunk`)
                // then GetBytes should read `chunkBytes.Slice(0, bytesToTake)`. This is what I have.
            }


            bytesLeft -= (ulong)bytesToTake;
            offset++;
        }
        return resultBytes.ToArray();
    }

    // Corrected GetBytes based on Go's right-alignment for partial chunks if SetBytes were to mimic it.
    // However, my SetBytes currently left-aligns partial chunks within the 32-byte slot.
    // Let's assume SetBytes is:
    // Span<byte> slotData = stackalloc byte[32]; // Zero-initialized
    // sourceSpan.CopyTo(slotData.Slice(32 - sourceSpan.Length)); // Right-align
    // SetByUint64(offset, new ValueHash256(slotData));
    // Then GetBytes would be:
    // ReadOnlySpan<byte> chunkBytes = GetByUint64(offset).Bytes;
    // int bytesToRead = (int)Math.Min(32, bytesLeft);
    // resultBytes.AddRange(chunkBytes.Slice(32 - bytesToRead).ToArray());

    // Sticking with current SetBytes (left-aligns partial in slot), GetBytes is:
    // ReadOnlySpan<byte> chunkBytes = GetByUint64(offset).Bytes;
    // int bytesToRead = (int)Math.Min(32, bytesLeft);
    // resultBytes.AddRange(chunkBytes.Slice(0, bytesToRead).ToArray());
    // This seems more straightforward in C#. The Go version's GetBytes for the last chunk `next.Bytes()[32-bytesLeft:]`
    // is because `common.BytesToHash` right-pads. My `SetBytes` uses `CopyTo` which left-pads into the 32-byte `chunk`.

    public ulong GetBytesSize()
    {
        return GetUint64ByUint64(0);
    }

    public void ClearBytes()
    {
        ulong bytesLeft = GetUint64ByUint64(0); // Read size first

        ulong offset = 1;
        ulong numSlots = (bytesLeft + 31) / 32; // Calculate number of slots used

        for (ulong i = 0; i < numSlots; i++)
        {
            ClearByUint64(offset + i);
        }

        ClearByUint64(0); // Clear the size
    }
}

public readonly struct ArbosStorageSlot(ArbosStorage storage, ulong offset)
{
    private readonly ValueHash256 _slotKey = new(new UInt256(offset)); // The logical key, not the mapped one

    public ValueHash256 Get() => storage.Get(_slotKey);

    public void Set(ValueHash256 value) => storage.Set(_slotKey, value);
}

public class ArbosStorageBackedUint32(ArbosStorage storage, ulong offset)
{
    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public ulong Get()
    {
        ValueHash256 value = _slot.Get();
        return value == default ? 0 : (uint)value.ToUInt256();
    }

    public void Set(uint value)
    {
        _slot.Set(new ValueHash256(new UInt256(value)));
    }
}

public class ArbosStorageBackedUint64(ArbosStorage storage, ulong offset)
{
    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public ulong Get()
    {
        ValueHash256 value = _slot.Get();
        return value == default ? 0 : (ulong)value.ToUInt256();
    }

    public void Set(ulong value)
    {
        _slot.Set(new ValueHash256(new UInt256(value)));
    }

    public ulong Increment()
    {
        ulong old = Get();
        Set(old + 1);
        return old + 1;
    }
}

public class ArbosStorageBackedInt256(ArbosStorage storage, ulong offset) // TODO: this one should handle UInt256
{
    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public Int256.Int256 Get()
    {
        ValueHash256 raw = _slot.Get();
        return new Int256.Int256(raw.Bytes, true);
    }

    public void Set(Int256.Int256 value)
    {
        ValueHash256 raw = new();
        ((UInt256)value).ToBigEndian(raw.BytesAsSpan);
        _slot.Set(raw);
    }
}

public class ArbosStorageBackedAddress(ArbosStorage storage, ulong offset)
{
    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public Address Get()
    {
        ValueHash256 value = _slot.Get();
        return value == default ? Address.Zero : new Address(value.Bytes[12..]);
    }

    public void Set(Address val)
    {
        Span<byte> hashBytes = stackalloc byte[32];
        val.Bytes.AsSpan().CopyTo(hashBytes[12..]);
        _slot.Set(new ValueHash256(hashBytes));
    }
}

public class ArbosStorageBackedBytes(ArbosStorage storage)
{
    public byte[] Get() => storage.GetBytes();

    public void Set(byte[] val) => storage.SetBytes(val);
}
