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
    }

    public IBurner Burner => _burner;

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

    public ulong GetULong(ValueHash256 key)
    {
        ValueHash256 value = Get(key);
        return value == default ? 0 : (ulong)value.ToUInt256();
    }

    public ValueHash256 GetByULong(ulong key)
    {
        return Get(new ValueHash256(new UInt256(key)));
    }

    public ulong GetULongByULong(ulong key)
    {
        ValueHash256 value = GetByULong(key);
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

    public void SetULong(ValueHash256 key, ulong value)
    {
        Set(key, new ValueHash256(new UInt256(value)));
    }

    public void SetByULong(ulong key, ValueHash256 value)
    {
        Set(new ValueHash256(new UInt256(key)), value);
    }

    public void SetULongByULong(ulong key, ulong value)
    {
        Set(new ValueHash256(new UInt256(key)), new ValueHash256(new UInt256(value)));
    }

    public void Clear(ValueHash256 key)
    {
        Set(key, default);
    }

    public void ClearByULong(ulong key)
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

        ClearBytes();
        SetULongByULong(0, (ulong)value.Length);

        ulong offset = 1;
        ReadOnlySpan<byte> span = value.AsSpan();

        while (span.Length > 32)
        {
            SetByULong(offset, Hash256.FromBytesWithPadding(span[..32]));
            span = span[32..];
            offset++;
        }

        if (span.Length > 0)
        {
            SetByULong(offset, Hash256.FromBytesWithPadding(span));
        }
    }

    public byte[] GetBytes()
    {
        ulong bytesLeft = GetULongByULong(0);
        if (bytesLeft == 0)
        {
            return [];
        }

        byte[] result = new byte[bytesLeft];
        Span<byte> resultSpan = result.AsSpan();
        ulong offset = 1;

        while (bytesLeft >= 32)
        {
            ValueHash256 chunk = GetByULong(offset);
            chunk.Bytes.CopyTo(resultSpan);
            resultSpan = resultSpan[32..];
            bytesLeft -= 32;
            offset++;
        }

        if (bytesLeft > 0)
        {
            ValueHash256 lastChunk = GetByULong(offset);
            lastChunk.Bytes.Slice((int)(32 - bytesLeft)).CopyTo(resultSpan);
        }

        return result;
    }

    public ulong GetBytesSize()
    {
        return GetULongByULong(0);
    }

    public void ClearBytes()
    {
        ulong bytesLeft = GetULongByULong(0);

        ulong offset = 1;
        ulong numSlots = (bytesLeft + 31) / 32;

        for (ulong i = 0; i < numSlots; i++)
        {
            ClearByULong(offset + i);
        }

        ClearByULong(0);
    }

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
}

public readonly struct ArbosStorageSlot(ArbosStorage storage, ulong offset)
{
    private readonly ValueHash256 _slotKey = new(new UInt256(offset)); // The logical key, not the mapped one

    public ValueHash256 Get() => storage.Get(_slotKey);

    public void Set(ValueHash256 value) => storage.Set(_slotKey, value);
}

public class ArbosStorageBackedUInt(ArbosStorage storage, ulong offset)
{
    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public uint Get()
    {
        ValueHash256 value = _slot.Get();
        return value == default ? 0 : (uint)value.ToUInt256();
    }

    public void Set(uint value)
    {
        _slot.Set(new ValueHash256(new UInt256(value)));
    }
}

public class ArbosStorageBackedULong(ArbosStorage storage, ulong offset)
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

public class ArbosStorageBackedUInt256(ArbosStorage storage, ulong offset) // Nitro uses BigInteger with boundaries < 0 and < 2^256
{
    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public UInt256 Get()
    {
        ValueHash256 raw = _slot.Get();
        return new UInt256(raw.Bytes, true);
    }

    public void Set(UInt256 value)
    {
        ValueHash256 raw = new();
        value.ToBigEndian(raw.BytesAsSpan);
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
