using FluentAssertions;
using Nethermind.Logging;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core.Test;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class OwnerWrapperTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ParsesArbOwnerAddChainOwner_CallerIsNotOwner_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        Address caller = new("0x0000000000000000000000000000000000000001"); // not a chain owner
        context.WithCaller(caller);
        context.WithArbosState();

        // Setup input data
        string addChainOwnerMethodId = "0x481f8dbf";
        Address newOwner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{addChainOwnerMethodId}{newOwner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ResetGasLeft(); // for correct gas assertion
        OwnerWrapper<ArbOwnerParser> arbOwnerParser = new(new(), ArbOwner.OwnerActsEvent);
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action.Should().Throw<UnauthorizedCallerException>();

        // non-chain owner has to pay for IsMember() check read cost
        context.GasLeft.Should().Be(context.GasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesArbOwnerAddChainOwner_CallerIsOwner_AddsOwner()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        Address caller = new("0x0000000000000000000000000000000000000001");
        context.WithCaller(caller);
        context.WithArbosState();

        context.ArbosState.ChainOwners.Add(caller);

        // Setup input data
        string addChainOwnerMethodId = "0x481f8dbf";
        Address newOwner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{addChainOwnerMethodId}{newOwner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ResetGasLeft(); // for correct gas assertion
        OwnerWrapper<ArbOwnerParser> arbOwnerParser = new(new(), ArbOwner.OwnerActsEvent);
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        ulong spentGas = context.GasSupplied - context.GasLeft;

        result.Should().BeEmpty();
        context.ArbosState.ChainOwners.IsMember(newOwner).Should().BeTrue();

        // Event gets emitted because context is not ReadOnly
        LogEntry ownerActsEvent = EventsEncoder.BuildLogEntryFromEvent(
            ArbOwner.OwnerActsEvent, ArbOwner.Address, Bytes.FromHexString(addChainOwnerMethodId), caller, inputData
        );
        context.EventLogs.Should().BeEquivalentTo(new[] { ownerActsEvent });

        ulong dataGasCost = GasCostOf.DataCopy * 1; // (inputData.Length - 4) / div32Ceiling = 1
        ulong addChainOwnerCost = ArbosStorage.StorageReadCost * 3 + ArbosStorage.StorageWriteCost * 3;
        // isMember() in OwnerWrapper as well as event emission are free
        // As a side note, the owner will not even end up paying below cost (see ArbitrumVM's RunPrecompile())
        spentGas.Should().Be(dataGasCost + addChainOwnerCost);
    }
}
