using System.Numerics;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class BatchPostersTable(ArbosStorage storage)
{
    private const ulong TotalFundsDueOffset = 0;

    private static readonly byte[] PosterAddressKey = [0];
    private static readonly byte[] PosterInfoKey = [1];

    private readonly AddressSet _posterAddresses = new(storage.OpenSubStorage(PosterAddressKey));
    private readonly ArbosStorage _posterInfo = storage.OpenSubStorage(PosterInfoKey);
    private readonly ArbosStorageBackedBigInteger _totalFundsDue = new(storage, TotalFundsDueOffset);

    public static void Initialize(ArbosStorage storage)
    {
        var totalFundsDue = new ArbosStorageBackedBigInteger(storage, TotalFundsDueOffset);
        totalFundsDue.Set(0);

        AddressSet.Initialize(storage.OpenSubStorage(PosterAddressKey));
    }

    public BatchPoster AddPoster(Address posterAddress, Address payToAddress)
    {
        if (ContainsPoster(posterAddress))
        {
            throw new InvalidOperationException($"Tried to add a batch poster {posterAddress} that already exists.");
        }

        ArbosStorage batchPosterStorage = _posterInfo.OpenSubStorage(posterAddress.Bytes);
        ArbosStorageBackedBigInteger fundsDueStorage = new(batchPosterStorage, 0);
        fundsDueStorage.Set(0);

        ArbosStorageBackedAddress payToAddressStorage = new(batchPosterStorage, 1);
        payToAddressStorage.Set(payToAddress);

        _posterAddresses.Add(posterAddress);

        return new BatchPoster(batchPosterStorage, this);
    }

    public BatchPoster OpenPoster(Address posterAddress, bool createIfNotExists)
    {
        if (ContainsPoster(posterAddress))
        {
            return new BatchPoster(_posterInfo.OpenSubStorage(posterAddress.Bytes), this);
        }

        return createIfNotExists
            ? AddPoster(posterAddress, posterAddress)
            : throw new InvalidOperationException($"Tried to open a batch poster {posterAddress} that does not exists.");
    }

    public bool ContainsPoster(Address posterAddress)
    {
        return _posterAddresses.IsMember(posterAddress);
    }

    public IReadOnlyCollection<Address> GetAllPosters(ulong maxNumToReturn)
    {
        return _posterAddresses.AllMembers(maxNumToReturn);
    }

    public BigInteger GetTotalFundsDue()
    {
        return _totalFundsDue.Get();
    }

    public class BatchPoster(ArbosStorage storage, BatchPostersTable postersTable)
    {
        private readonly ArbosStorageBackedBigInteger _fundsDue = new(storage, 0);
        private readonly ArbosStorageBackedAddress _payTo = new(storage, 1);

        public BigInteger GetFundsDue()
        {
            return _fundsDue.Get();
        }

        public bool SetFundsDueSaturating(BigInteger fundsDue)
        {
            BigInteger totalFundsDue = postersTable.GetTotalFundsDue();
            BigInteger posterFundsDue = _fundsDue.Get();
            BigInteger newTotalFundsDue = totalFundsDue + fundsDue - posterFundsDue;

            bool totalFundsSetSaturated = postersTable._totalFundsDue.SetSaturating(newTotalFundsDue);
            bool fundsDueSaturated = _fundsDue.SetSaturating(fundsDue);

            return totalFundsSetSaturated || fundsDueSaturated;
        }

        public Address GetPayTo()
        {
            return _payTo.Get();
        }

        public void SetPayTo(Address payTo)
        {
            _payTo.Set(payTo);
        }
    }
}

public class BatchPoster(ArbosStorage storage)
{
    private readonly ArbosStorageBackedUInt256 _fundsDue = new(storage, 0);
    private readonly ArbosStorageBackedAddress _payTo = new(storage, 1);

    public UInt256 FundsDue
    {
        get => _fundsDue.Get();
        set => _fundsDue.Set(value);
    }

    public Address PayTo
    {
        get => _payTo.Get();
        set => _payTo.Set(value);
    }
}
