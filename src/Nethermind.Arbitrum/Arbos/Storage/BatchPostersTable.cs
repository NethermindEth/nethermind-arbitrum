using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class BatchPostersTable(ArbosStorage storage)
{
    private const ulong TotalFundsDueOffset = 0;

    private static readonly byte[] PosterAddressKey = [0];
    private static readonly byte[] PosterInfoKey = [1];

    private readonly AddressSet _posterAddresses = new(storage.OpenSubStorage(PosterAddressKey));
    private readonly ArbosStorage _posterInfo = storage.OpenSubStorage(PosterInfoKey);
    private readonly ArbosStorageBackedUInt256 _totalFundsDue = new(storage, TotalFundsDueOffset);

    public static void Initialize(ArbosStorage storage)
    {
        var totalFundsDue = new ArbosStorageBackedUInt256(storage, TotalFundsDueOffset);
        totalFundsDue.Set(0);

        AddressSet.Initialize(storage.OpenSubStorage(PosterAddressKey));
    }

    public void AddPoster(Address posterAddress, Address payToAddress)
    {
        ArbosStorage batchPosterState = _posterInfo.OpenSubStorage(posterAddress.Bytes);
        ArbosStorageBackedUInt256 fundsDueStorage = new(batchPosterState, 0);
        fundsDueStorage.Set(0);

        ArbosStorageBackedAddress payToAddressStorage = new(batchPosterState, 1);
        payToAddressStorage.Set(payToAddress);

        _posterAddresses.Add(posterAddress);
    }
}
