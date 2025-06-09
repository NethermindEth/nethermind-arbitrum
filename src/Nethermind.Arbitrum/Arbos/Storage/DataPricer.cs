namespace Nethermind.Arbitrum.Arbos.Storage;

public class DataPricer(ArbosStorage storage)
{
    private const ulong DemandOffset = 0;
    private const ulong BytesPerSecondOffset = 1;
    private const ulong LastUpdateTimeOffset = 2;
    private const ulong MinPriceOffset = 3;
    private const ulong InertiaOffset = 4;

    private const ulong ArbitrumStartTime = 1421388000; // the day it all began

    private const uint InitialDemand = 0; // no demand
    private const uint InitialHourlyBytes = 125515026; // 1 * (1 << 40) / (365 * 24), 1Tb total footprint
    private const uint InitialBytesPerSecond = InitialHourlyBytes / (60 * 60); // refill each second
    private const ulong InitialLastUpdateTime = ArbitrumStartTime;
    private const uint InitialMinPrice = 82928201; // 5Mb = $1
    private const uint InitialInertia = 21360419; // expensive at 1Tb

    private readonly ArbosStorageBackedUInt _demandStorage = new(storage, DemandOffset);
    private readonly ArbosStorageBackedUInt _bytesPerSecondStorage = new(storage, BytesPerSecondOffset);
    private readonly ArbosStorageBackedULong _lastUpdateTimeStorage = new(storage, LastUpdateTimeOffset);
    private readonly ArbosStorageBackedUInt _minPriceStorage = new(storage, MinPriceOffset);
    private readonly ArbosStorageBackedUInt _inertiaStorage = new(storage, InertiaOffset);

    public static void Initialize(ArbosStorage storage)
    {
        var pricer = new DataPricer(storage);
        pricer._demandStorage.Set(InitialDemand);
        pricer._bytesPerSecondStorage.Set(InitialBytesPerSecond);
        pricer._lastUpdateTimeStorage.Set(InitialLastUpdateTime);
        pricer._minPriceStorage.Set(InitialMinPrice);
        pricer._inertiaStorage.Set(InitialInertia);
    }
}
