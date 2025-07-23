using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class StylusParams
{
    public const uint MaxInkPrice = 0xFFFFFF; // 24 bits

    public const ushort MinInitGasUnits = 128; // 128 gas for each unit
    public const ushort MinCachedGasUnits = 32; // 32 gas for each unit
    public const ushort CostScalarPercent = 2; // 2% for each unit

    private const uint InitialMaxWasmSize = 128 * 1024; // max decompressed wasm size (programs are also bounded by compressed size)
    private const uint InitialStackDepth = 4 * 65536; // 4 page stack.
    private const ushort InitialFreePages = 2; // 2 pages come free (per tx).
    private const ushort InitialPageGas = 1000; // linear cost per allocation.
    private const ulong InitialPageRamp = 620674314; // targets 8MB costing 32 million gas, minus the linear term.
    private const ushort InitialPageLimit = 128; // reject wasms with memories larger than 8MB.
    private const ushort InitialInkPrice = 10000; // 1 evm gas buys 10k ink.
    private const byte InitialMinInitGas = 72; // charge 72 * 128 = 9216 gas.
    private const byte InitialMinCachedGas = 11; // charge 11 *  32 = 352 gas.
    private const byte InitialInitCostScalar = 50; // scale costs 1:1 (100%)
    private const byte InitialCachedCostScalar = 50; // scale costs 1:1 (100%)
    private const ushort InitialExpiryDays = 365; // deactivate after 1 year.
    private const ushort InitialKeepaliveDays = 31; // wait a month before allowing reactivation.
    private const ushort InitialRecentCacheSize = 32; // cache the 32 most recent programs.

    private const byte V2MinInitGas = 69; // charge 69 * 128 = 8832 gas (minCachedGas will also be charged in v2).

    private const ulong MaxWasmSizeArbosVersion = 40;

    private ulong _arbosVersion;
    private readonly ArbosStorage _storage;

    private StylusParams(ulong arbosVersion, ArbosStorage storage, ushort stylusVersion, uint inkPrice, uint maxStackDepth, ushort freePages, ushort pageGas,
        ulong pageRamp, ushort pageLimit, byte minInitGas, byte minCachedInitGas, byte initCostScalar, byte cachedCostScalar, ushort expiryDays,
        ushort keepaliveDays, ushort blockCacheSize, uint maxWasmSize)
    {
        _arbosVersion = arbosVersion;
        _storage = storage;
        StylusVersion = stylusVersion;
        InkPrice = inkPrice <= MaxInkPrice ? inkPrice : throw new ArgumentException("InkPrice exceeds 24 bits");
        MaxStackDepth = maxStackDepth;
        FreePages = freePages;
        PageGas = pageGas;
        PageRamp = pageRamp;
        PageLimit = pageLimit;
        MinInitGas = minInitGas;
        MinCachedInitGas = minCachedInitGas;
        InitCostScalar = initCostScalar;
        CachedCostScalar = cachedCostScalar;
        ExpiryDays = expiryDays;
        KeepaliveDays = keepaliveDays;
        BlockCacheSize = blockCacheSize;
        MaxWasmSize = maxWasmSize;
    }

    public ushort StylusVersion { get; private set; }
    public uint InkPrice { get; private set; } // 24 bits
    public uint MaxStackDepth { get; private set; }
    public ushort FreePages { get; private set; }
    public ushort PageGas { get; private set; }
    public ulong PageRamp { get; }
    public ushort PageLimit { get; private set; }
    public byte MinInitGas { get; private set; }
    public byte MinCachedInitGas { get; private set; }
    public byte InitCostScalar { get; private set; }
    public byte CachedCostScalar { get; }
    public ushort ExpiryDays { get; private set; }
    public ushort KeepaliveDays { get; private set; }
    public ushort BlockCacheSize { get; private set; }
    public uint MaxWasmSize { get; private set; }

    [SuppressMessage("Reliability", "CA2014:Do not use stackalloc in loops")]
    [SuppressMessage("ReSharper", "StackAllocInsideLoop")]
    public void Save()
    {
        // Version (ushort)        : 2 bytes
        // InkPrice (uint24)       : 3 bytes
        // MaxStackDepth (uint)    : 4 bytes
        // FreePages (ushort)      : 2 bytes
        // PageGas (ushort)        : 2 bytes
        // PageRamp                : not stored
        // PageLimit (ushort)      : 2 bytes
        // MinInitGas (byte)       : 1 byte
        // MinCachedInitGas (byte) : 1 byte
        // InitCostScalar (byte)   : 1 byte
        // CachedCostScalar (byte) : 1 byte
        // ExpiryDays (ushort)     : 2 bytes
        // KeepaliveDays (ushort)  : 2 bytes
        // BlockCacheSize (ushort) : 2 bytes
        // Base size = 25 bytes.
        int baseSerializationSize = 7 * sizeof(short) + 3 + sizeof(uint) + 4 * sizeof(byte);

        var includeMaxWasmSize = _arbosVersion >= MaxWasmSizeArbosVersion;
        int totalSerializationSize = baseSerializationSize + (includeMaxWasmSize ? sizeof(uint) : 0);

        Span<byte> buffer = stackalloc byte[totalSerializationSize];
        int currentOffset = 0;

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), StylusVersion);
        currentOffset += sizeof(ushort);

        ArbitrumBinaryWriter.WriteUInt24BigEndian(buffer.Slice(currentOffset, 3), InkPrice);
        currentOffset += 3;

        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(currentOffset), MaxStackDepth);
        currentOffset += sizeof(uint);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), FreePages);
        currentOffset += sizeof(ushort);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), PageGas);
        currentOffset += sizeof(ushort);

        // PageRamp is not stored in the storage.

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), PageLimit);
        currentOffset += sizeof(ushort);

        buffer[currentOffset++] = MinInitGas;
        buffer[currentOffset++] = MinCachedInitGas;
        buffer[currentOffset++] = InitCostScalar;
        buffer[currentOffset++] = CachedCostScalar;

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), ExpiryDays);
        currentOffset += sizeof(ushort);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), KeepaliveDays);
        currentOffset += sizeof(ushort);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(currentOffset), BlockCacheSize);
        currentOffset += sizeof(ushort);

        if (includeMaxWasmSize)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(currentOffset), MaxWasmSize);
        }

        ulong currentSlot = 0;
        ReadOnlySpan<byte> remainingDataToStore = buffer;

        while (remainingDataToStore.Length > 0)
        {
            int chunkSize = System.Math.Min(32, remainingDataToStore.Length);
            ReadOnlySpan<byte> currentChunk = remainingDataToStore.Slice(0, chunkSize);

            Span<byte> rightPaddedChunk = stackalloc byte[32];
            currentChunk.CopyTo(rightPaddedChunk);

            _storage.Set(currentSlot, new ValueHash256(rightPaddedChunk));

            remainingDataToStore = remainingDataToStore.Slice(chunkSize);
            currentSlot++;
        }
    }

    public void UpgradeToStylusVersion(ushort newStylusVersion)
    {
        if (newStylusVersion != 2)
        {
            throw new InvalidOperationException($"Unsupported version upgrade to {newStylusVersion}. Only version 2 is supported.");
        }

        if (StylusVersion != 1)
        {
            throw new InvalidOperationException($"Cannot upgrade from version {StylusVersion} to version {newStylusVersion}. Version 1 is required.");
        }

        StylusVersion = newStylusVersion;
        MinInitGas = V2MinInitGas;
    }

    public void UpgradeToArbosVersion(ulong newArbosVersion)
    {
        if (newArbosVersion == MaxWasmSizeArbosVersion)
        {
            if (_arbosVersion >= newArbosVersion)
            {
                throw new InvalidOperationException($"Unexpected ArbOS version upgrade from {_arbosVersion} to {newArbosVersion}.");
            }

            if (StylusVersion != 2)
            {
                throw new InvalidOperationException($"Unexpected ArbOS version upgrade to {newArbosVersion} with Stylus version {StylusVersion}.");
            }

            MaxWasmSize = InitialMaxWasmSize;
        }

        _arbosVersion = newArbosVersion;
    }

    public static void InitializeWithDefaults(ArbosStorage storage, ulong arbosVersion)
    {
        var parameters = new StylusParams(
            arbosVersion,
            storage,
            1,
            InitialInkPrice,
            InitialStackDepth,
            InitialFreePages,
            InitialPageGas,
            InitialPageRamp,
            InitialPageLimit,
            InitialMinInitGas,
            InitialMinCachedGas,
            InitialInitCostScalar,
            InitialCachedCostScalar,
            InitialExpiryDays,
            InitialKeepaliveDays,
            InitialRecentCacheSize,
            arbosVersion >= MaxWasmSizeArbosVersion ? InitialMaxWasmSize : 0);

        parameters.Save();
    }

    public static StylusParams CreateFromStorage(ArbosStorage storage, ulong arbosVersion)
    {
        // Assume reads are warm due to the frequency of access
        storage.Burner.Burn(GasCostOf.CallPrecompileEip2929);

        ulong currentSlot = 0;
        ReadOnlySpan<byte> buffer = [];

        return new StylusParams(
            arbosVersion,
            storage,
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            ReadUInt24BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 3)),
            BinaryPrimitives.ReadUInt32BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 4)),
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            InitialPageRamp,
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            ReadFromStorage(storage, ref buffer, ref currentSlot, 1)[0],
            ReadFromStorage(storage, ref buffer, ref currentSlot, 1)[0],
            ReadFromStorage(storage, ref buffer, ref currentSlot, 1)[0],
            ReadFromStorage(storage, ref buffer, ref currentSlot, 1)[0],
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 2)),
            arbosVersion >= MaxWasmSizeArbosVersion
                ? BinaryPrimitives.ReadUInt32BigEndian(ReadFromStorage(storage, ref buffer, ref currentSlot, 4))
                : InitialMaxWasmSize);

        static uint ReadUInt24BigEndian(ReadOnlySpan<byte> source)
        {
            uint result = 0;
            result |= (uint)(source[0] << 16);
            result |= (uint)(source[1] << 8);
            result |= source[2];
            return result;
        }
    }

    public void SetInkPrice(uint inkPrice)
    {
        InkPrice = inkPrice;
    }

    public void SetMaxStackDepth(uint maxStackDepth)
    {
        MaxStackDepth = maxStackDepth;
    }

    public void SetFreePages(ushort freePages)
    {
        FreePages = freePages;
    }

    public void SetPageGas(ushort pageGas)
    {
        PageGas = pageGas;
    }

    public void SetPageLimit(ushort pageLimit)
    {
        PageLimit = pageLimit;
    }

    public void SetMinInitGas(byte minInitGas)
    {
        MinInitGas = minInitGas;
    }

    public void SetMinCachedInitGas(byte minCachedInitGas)
    {
        MinCachedInitGas = minCachedInitGas;
    }

    public void SetInitCostScalar(byte initCostScalar)
    {
        InitCostScalar = initCostScalar;
    }

    public void SetExpiryDays(ushort expiryDays)
    {
        ExpiryDays = expiryDays;
    }

    public void SetKeepaliveDays(ushort keepaliveDays)
    {
        KeepaliveDays = keepaliveDays;
    }

    public void SetBlockCacheSize(ushort blockCacheSize)
    {
        BlockCacheSize = blockCacheSize;
    }

    public void SetWasmMaxSize(uint maxWasmSize)
    {
        MaxWasmSize = maxWasmSize;
    }

    private static ReadOnlySpan<byte> ReadFromStorage(ArbosStorage storage, ref ReadOnlySpan<byte> buffer, ref ulong currentSlot, int count)
    {
        if (buffer.Length < count)
        {
            buffer = storage.GetFree(new UInt256(currentSlot).ToValueHash()).Bytes.ToArray(); // TODO: rewrite to have proper reader abstraction
            currentSlot++;
        }

        var value = buffer[..count];
        buffer = buffer[count..];
        return value;
    }
}
