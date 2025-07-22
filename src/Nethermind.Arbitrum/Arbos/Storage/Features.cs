using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Storage;

// Features is a thin wrapper around ArbosStorageBackedUInt256 that
// provides accessors for various feature toggles
public class Features(ArbosStorage storage)
{
    public ArbosStorageBackedUInt256 FeaturesStorage { get; } = new(storage, 0);

    // This should work for the first 256 features. After that, either add
    // another member to the Features class, or switch to StorageBackedBytes
    private const int IncreasedCalldataFeature = 0;

    // SetIncreasedCalldataPriceIncrease sets the increased calldata price feature
    // ON/OFF depending on the value of enabled
    public void SetCalldataPriceIncrease(bool enabled) => SetBit(IncreasedCalldataFeature, enabled);

    // IsIncreasedCalldataPriceEnabled returns true if the increased calldata price
    // feature is enabled.
    public bool IsCalldataPriceIncreaseEnabled() => IsBitSet(IncreasedCalldataFeature);

    private void SetBit(int bit, bool enabled)
    {
        // Features cannot be uninitialized.
        UInt256 features = FeaturesStorage.Get();

        UInt256 featureToUpdate = (UInt256)(1 << bit);
        features = enabled ? features | featureToUpdate : features & ~featureToUpdate;

        FeaturesStorage.Set(features);
    }

    private bool IsBitSet(int bit) => (FeaturesStorage.Get() & (UInt256)(1 << bit)) != 0;
}
