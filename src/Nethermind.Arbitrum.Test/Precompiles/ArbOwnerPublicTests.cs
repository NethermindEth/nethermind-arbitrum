using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbOwnerPublicTests
{
    private static readonly Address InitialChainOwner = FullChainSimulationAccounts.Owner.Address;

    [Test]
    public void IsChainOwner_WithExistingOwner_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        Address testOwner = TestItem.AddressA;
        context.ArbosState.ChainOwners.Add(testOwner);

        bool result = ArbOwnerPublic.IsChainOwner(context, testOwner);

        result.Should().BeTrue();
    }

    [Test]
    public void IsChainOwner_WithNonOwner_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        Address nonOwner = TestItem.AddressA;

        bool result = ArbOwnerPublic.IsChainOwner(context, nonOwner);

        result.Should().BeFalse();
    }

    [Test]
    public void GetAllChainOwners_AfterInitialization_ReturnsInitialOwner()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        Address[] result = ArbOwnerPublic.GetAllChainOwners(context);

        result.Should().BeEquivalentTo([InitialChainOwner]);
    }

    [Test]
    public void GetAllChainOwners_WithSingleOwner_ReturnsOwnerArray()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        Address owner = TestItem.AddressA;
        context.ArbosState.ChainOwners.Add(owner);

        Address[] result = ArbOwnerPublic.GetAllChainOwners(context);

        result.Should().BeEquivalentTo([InitialChainOwner, owner]);
    }

    [Test]
    public void GetAllChainOwners_WithMultipleOwners_ReturnsAllOwners()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        Address owner1 = TestItem.AddressA;
        Address owner2 = TestItem.AddressB;
        Address owner3 = TestItem.AddressC;
        context.ArbosState.ChainOwners.Add(owner1);
        context.ArbosState.ChainOwners.Add(owner2);
        context.ArbosState.ChainOwners.Add(owner3);

        Address[] result = ArbOwnerPublic.GetAllChainOwners(context);

        result.Should().BeEquivalentTo([InitialChainOwner, owner1, owner2, owner3]);
    }

    [Test]
    public void RectifyChainOwner_WithNonOwner_ThrowsException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Eleven);

        Address nonOwner = TestItem.AddressA;

        Action action = () => ArbOwnerPublic.RectifyChainOwner(context, nonOwner);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"Address {nonOwner} is not an owner.");
    }

    [Test]
    public void RectifyChainOwner_WithCorrectlyMappedOwner_ThrowsException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Eleven);

        Address owner = TestItem.AddressA;
        context.ArbosState.ChainOwners.Add(owner);

        Action action = () => ArbOwnerPublic.RectifyChainOwner(context, owner);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"Owner address {owner} is correctly mapped.");
    }

    [Test]
    public void RectifyChainOwner_WithCorruptedMapping_EmitsEventWithCorrectAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Eleven);

        // Add an owner to create the initial state
        Address ownerToCorrupt = TestItem.AddressA;
        context.ArbosState.ChainOwners.Add(ownerToCorrupt);

        // Get the slot number for this owner (should be 2, since InitialChainOwner is at slot 1)
        ulong slot = 2;

        // Directly set the storage at this slot to a different hash to corrupt the mapping
        // This simulates a pre-ArbOS v11 corrupted state
        ValueHash256 corruptedHash = new(TestItem.KeccakB.Bytes);
        context.ArbosState.BackingStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace).Set(slot, corruptedHash);

        // Now RectifyChainOwner should succeed and emit an event
        ArbOwnerPublic.RectifyChainOwner(context, ownerToCorrupt);

        // Verify event was emitted
        context.EventLogs.Should().HaveCount(1);
        LogEntry eventLog = context.EventLogs[0];
        eventLog.Address.Should().Be(ArbOwnerPublic.Address);

        // Calculate expected event hash manually
        Hash256 expectedEventHash = Keccak.Compute("ChainOwnerRectified(address)"u8);
        eventLog.Topics[0].Should().Be(expectedEventHash);

        // The event has one non-indexed parameter (rectifiedOwner), which should be in the data field
        object[] decodedData = AbiEncoder.Instance.Decode(AbiEncodingStyle.None, ArbOwnerPublic.ChainOwnerRectifiedEvent.GetCallInfo().Signature, eventLog.Data);
        Address rectifiedOwner = (Address)decodedData[0];
        rectifiedOwner.Should().Be(ownerToCorrupt);
    }

    [Test]
    public void IsNativeTokenOwner_WithExistingOwner_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne);

        Address testOwner = TestItem.AddressA;
        context.ArbosState.NativeTokenOwners.Add(testOwner);

        bool result = ArbOwnerPublic.IsNativeTokenOwner(context, testOwner);

        result.Should().BeTrue();
    }

    [Test]
    public void IsNativeTokenOwner_WithNonOwner_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne);

        Address nonOwner = TestItem.AddressA;

        bool result = ArbOwnerPublic.IsNativeTokenOwner(context, nonOwner);

        result.Should().BeFalse();
    }

    [Test]
    public void GetAllNativeTokenOwners_WithNoOwners_ReturnsEmptyArray()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne);

        Address[] result = ArbOwnerPublic.GetAllNativeTokenOwners(context);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetAllNativeTokenOwners_WithMultipleOwners_ReturnsAllOwners()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne);

        Address owner1 = TestItem.AddressA;
        Address owner2 = TestItem.AddressB;
        context.ArbosState.NativeTokenOwners.Add(owner1);
        context.ArbosState.NativeTokenOwners.Add(owner2);

        Address[] result = ArbOwnerPublic.GetAllNativeTokenOwners(context);

        result.Should().HaveCount(2);
        result.Should().Contain(owner1);
        result.Should().Contain(owner2);
    }

    [Test]
    public void GetNetworkFeeAccount_Always_ReturnsCorrectAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState();

        Address expectedAccount = TestItem.AddressA;
        context.ArbosState.NetworkFeeAccount.Set(expectedAccount);

        Address result = ArbOwnerPublic.GetNetworkFeeAccount(context);

        result.Should().Be(expectedAccount);
    }

    [Test]
    public void GetInfraFeeAccount_WithArbosVersionLessThan6_ReturnsNetworkFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Five);

        Address networkFeeAccount = TestItem.AddressA;
        Address infraFeeAccount = TestItem.AddressB;
        context.ArbosState.NetworkFeeAccount.Set(networkFeeAccount);
        context.ArbosState.InfraFeeAccount.Set(infraFeeAccount);

        Address result = ArbOwnerPublic.GetInfraFeeAccount(context);

        result.Should().Be(networkFeeAccount, "because ArbOS version < 6 should fall back to NetworkFeeAccount");
    }

    [Test]
    public void GetInfraFeeAccount_WithArbosVersion6OrHigher_ReturnsInfraFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Six);

        Address networkFeeAccount = TestItem.AddressA;
        Address infraFeeAccount = TestItem.AddressB;
        context.ArbosState.NetworkFeeAccount.Set(networkFeeAccount);
        context.ArbosState.InfraFeeAccount.Set(infraFeeAccount);

        Address result = ArbOwnerPublic.GetInfraFeeAccount(context);

        result.Should().Be(infraFeeAccount, "because ArbOS version >= 6 should use InfraFeeAccount");
    }

    [Test]
    public void GetBrotliCompressionLevel_Always_ReturnsCorrectLevel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Twenty);

        const ulong expectedLevel = 5;
        context.ArbosState.BrotliCompressionLevel.Set(expectedLevel);

        ulong result = ArbOwnerPublic.GetBrotliCompressionLevel(context);

        result.Should().Be(expectedLevel);
    }

    [Test]
    public void GetScheduledUpgrade_WithNoScheduledUpgrade_ReturnsZeros()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Twenty);

        context.ArbosState.UpgradeVersion.Set(0);
        context.ArbosState.UpgradeTimestamp.Set(0);

        (ulong version, ulong timestamp) = ArbOwnerPublic.GetScheduledUpgrade(context);

        version.Should().Be(0);
        timestamp.Should().Be(0);
    }

    [Test]
    public void GetScheduledUpgrade_WithFutureUpgrade_ReturnsVersionAndTimestamp()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Twenty);

        const ulong scheduledVersion = 50;
        const ulong scheduledTimestamp = 1234567890;
        context.ArbosState.UpgradeVersion.Set(scheduledVersion);
        context.ArbosState.UpgradeTimestamp.Set(scheduledTimestamp);

        (ulong version, ulong timestamp) = ArbOwnerPublic.GetScheduledUpgrade(context);

        version.Should().Be(scheduledVersion);
        timestamp.Should().Be(scheduledTimestamp);
    }

    [Test]
    public void GetScheduledUpgrade_WithCurrentOrPastVersion_ReturnsZeros()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        const ulong currentVersion = ArbosVersion.Thirty;
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(currentVersion);

        context.ArbosState.UpgradeVersion.Set(currentVersion);
        context.ArbosState.UpgradeTimestamp.Set(1234567890);

        (ulong version, ulong timestamp) = ArbOwnerPublic.GetScheduledUpgrade(context);

        version.Should().Be(0, "because current version >= scheduled version");
        timestamp.Should().Be(0, "because current version >= scheduled version");
    }

    [Test]
    public void IsCalldataPriceIncreaseEnabled_WhenEnabled_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty);

        context.ArbosState.Features.SetCalldataPriceIncrease(true);

        bool result = ArbOwnerPublic.IsCalldataPriceIncreaseEnabled(context);

        result.Should().BeTrue();
    }

    [Test]
    public void IsCalldataPriceIncreaseEnabled_WhenDisabled_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ArbitrumPrecompileExecutionContext context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty);

        context.ArbosState.Features.SetCalldataPriceIncrease(false);

        bool result = ArbOwnerPublic.IsCalldataPriceIncreaseEnabled(context);

        result.Should().BeFalse();
    }

    [Test]
    public void GetAllChainOwners_ViaRpcCall_ReturnsOwnersList()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        // Calldata to call getAllChainOwners() on ArbOwnerPublic precompile
        uint methodId = PrecompileHelper.GetMethodId("getAllChainOwners()");
        AbiFunctionDescription functionDescription = ArbOwnerPublicParser.PrecompileFunctionDescription[methodId].AbiFunctionDescription;
        AbiSignature signature = functionDescription.GetCallInfo().Signature;
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, signature);

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbOwnerPublicAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(nonce)
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<string> result = chain.ArbitrumEthRpcModule.eth_call(TransactionForRpc.FromTransaction(transaction), BlockParameter.Latest);
        result.Result.Should().Be(Result.Success);

        object[] precompileResponse = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionDescription.GetReturnInfo().Signature,
            Bytes.FromHexString(result.Data));

        ((Address[])precompileResponse[0]).Should().BeEquivalentTo([InitialChainOwner]);
    }
}
