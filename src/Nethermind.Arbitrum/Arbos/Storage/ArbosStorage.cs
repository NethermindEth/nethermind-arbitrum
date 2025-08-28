// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.State;
using System.Numerics;

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
        _burner.TracingInfo?.RecordStorageGet(MapAddress(key));
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

    public ValueHash256 Get(ulong key)
    {
        return Get(new UInt256(key).ToValueHash());
    }

    public ulong GetULong(ulong key)
    {
        ValueHash256 value = Get(key);
        return value == default ? 0 : (ulong)value.ToUInt256();
    }

    public void Set(ValueHash256 key, ValueHash256 value)
    {
        ulong cost = value == default ? StorageWriteZeroCost : StorageWriteCost;
        _burner.Burn(cost);
        _burner.TracingInfo?.RecordStorageSet(MapAddress(key), value);

        var mappedAddress = MapAddress(key);
        _db.Set(new StorageCell(_account, new UInt256(mappedAddress.Bytes, isBigEndian: true)), value.Bytes.WithoutLeadingZeros().ToArray());
    }

    public void Set(ValueHash256 key, ulong value)
    {
        Set(key, new UInt256(value).ToValueHash());
    }

    public void Set(ulong key, ValueHash256 value)
    {
        Set(new UInt256(key).ToValueHash(), value);
    }

    public void Set(ulong key, ulong value)
    {
        Set(new UInt256(key).ToValueHash(), new UInt256(value).ToValueHash());
    }

    public void Clear(ValueHash256 key)
    {
        Set(key, (ValueHash256)default);
    }

    public void Clear(ulong key)
    {
        Set(new UInt256(key).ToValueHash(), (ValueHash256)default);
    }

    public ArbosStorage OpenSubStorage(byte[] id)
    {
        Span<byte> keccakBytes = stackalloc byte[_storageKey.Length + id.Length];
        _storageKey.CopyTo(keccakBytes);
        id.CopyTo(keccakBytes.Slice(_storageKey.Length));

        Hash256 keccak = Keccak.Compute(keccakBytes);
        return new ArbosStorage(_db, _burner, _account, keccak.BytesToArray());
    }

    public void Set(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ClearBytes();
        Set(0, (ulong)value.Length);

        ulong offset = 1;
        ReadOnlySpan<byte> span = value.AsSpan();

        while (span.Length > 32)
        {
            Set(offset, Hash256.FromBytesWithPadding(span[..32]));
            span = span[32..];
            offset++;
        }

        if (span.Length > 0)
        {
            Set(offset, Hash256.FromBytesWithPadding(span));
        }
    }

    public byte[] GetBytes()
    {
        ulong bytesLeft = GetBytesSize();
        if (bytesLeft == 0)
        {
            return [];
        }

        byte[] result = new byte[bytesLeft];
        Span<byte> resultSpan = result.AsSpan();
        ulong offset = 1;

        while (bytesLeft >= 32)
        {
            ValueHash256 chunk = Get(offset);
            chunk.Bytes.CopyTo(resultSpan);
            resultSpan = resultSpan[32..];
            bytesLeft -= 32;
            offset++;
        }

        if (bytesLeft > 0)
        {
            ValueHash256 lastChunk = Get(offset);
            lastChunk.Bytes.Slice((int)(32 - bytesLeft)).CopyTo(resultSpan);
        }

        return result;
    }

    public ulong GetBytesSize()
    {
        return GetULong(0);
    }

    public void ClearBytes()
    {
        ulong bytesLeft = GetBytesSize();

        ulong offset = 1;
        ulong numSlots = (bytesLeft + 31) / 32;

        for (ulong i = 0; i < numSlots; i++)
        {
            Clear(offset + i);
        }

        Clear(0);
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

    public ValueHash256 GetCodeHash(Address address)
    {
        _burner.Burn(StorageCodeHashCost);
        return _db.GetCodeHash(address);
    }

    public ValueHash256 ComputeKeccakHash(ReadOnlySpan<byte> memory)
    {
        ulong words = Math.Utils.Div32Ceiling((ulong)memory.Length);
        Burner.Burn(KeccakBaseCost + KeccakWordCost * words);
        return ValueKeccak.Compute(memory);
    }
}

public readonly struct ArbosStorageSlot(ArbosStorage storage, ulong offset)
{
    private readonly ValueHash256 _slotKey = new UInt256(offset).ToValueHash(); // The logical key, not the mapped one

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
        _slot.Set(new UInt256(value).ToValueHash());
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
        _slot.Set(new UInt256(value).ToValueHash());
    }

    public ulong Increment()
    {
        ulong value = Get();
        if (value + 1 < value)
        {
            throw new OverflowException($"Increment overflows ArbosStorage slot {offset} value.");
        }

        ulong newValue = value + 1;
        Set(newValue);
        return newValue;
    }

    public ulong Decrement()
    {
        ulong value = Get();
        if (value == 0)
        {
            throw new OverflowException($"Decrement underflows ArbosStorage slot {offset} value.");
        }

        ulong newValue = value - 1;
        Set(newValue);
        return newValue;
    }
}

public class ArbosStorageBackedUInt256(ArbosStorage storage, ulong offset) // Nitro uses BigInteger with boundaries >= 0 and < 2^256
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

public class ArbosStorageBackedBigInteger(ArbosStorage storage, ulong offset)
{
    public static readonly BigInteger TwoToThe256 = BigInteger.One << 256; // 2^256
    public static readonly BigInteger TwoToThe256MinusOne = TwoToThe256 - 1; // 2^256 - 1
    public static readonly BigInteger TwoToThe255 = BigInteger.One << 255; // 2^255
    public static readonly BigInteger TwoToThe255MinusOne = TwoToThe255 - 1; // 2^255 - 1
    public static readonly BigInteger MaxValue = TwoToThe255MinusOne; // Maximum value for ArbosStorageBackedBigInteger

    private readonly ArbosStorageSlot _slot = new(storage, offset);

    public BigInteger Get()
    {
        ValueHash256 raw = _slot.Get();
        return raw == default
            ? BigInteger.Zero // Return zero if the slot is empty
            : new BigInteger(raw.Bytes, isUnsigned: false, isBigEndian: true);
    }

    public void SetChecked(BigInteger value)
    {
        SetInternal(value, saturateOnOverflow: false);
    }

    public bool SetSaturating(BigInteger value)
    {
        return SetInternal(value, saturateOnOverflow: true);
    }

    public void SetPreVersion7(BigInteger value)
    {
        // Go's big.Int.Bytes() returns BigInteger unsigned representation.
        // On the contrary, .NET BigInteger.ToByteArray() returns signed representation.
        // To match Go's behavior, we need to convert negative values to positive.
        value = value < 0 ? -value : value;

        _slot.Set(ToHash(value));
    }

    public void Set(ulong value)
    {
        _slot.Set(new UInt256(value).ToValueHash());
    }

    private bool SetInternal(BigInteger value, bool saturateOnOverflow)
    {
        bool saturated = false;
        if (value < 0)
        {
            value += TwoToThe256; // Convert negative to positive in the range [0, 2^256 - 1]
            if (value.GetBitLength() < 256 || value <= 0)
            {
                saturated = true;
                value = !saturateOnOverflow
                    ? throw new OverflowException($"Value {value} underflows ArbosStorage slot {offset} value.")
                    : TwoToThe255;
            }
        }
        else if (value.GetBitLength() >= 256) // Check if value is greater than or equal to 2^256
        {
            saturated = true;
            value = !saturateOnOverflow
                ? throw new OverflowException($"Value {value} overflows ArbosStorage slot {offset} value.")
                : TwoToThe255MinusOne;
        }

        _slot.Set(ToHash(value));
        return saturated;
    }

    private static ValueHash256 ToHash(BigInteger value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.ToBytes32(bytes, isBigEndian: true);
        return new ValueHash256(bytes);
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

    public void Set(byte[] val) => storage.Set(val);

    public ulong Size() => storage.GetBytesSize();
}
