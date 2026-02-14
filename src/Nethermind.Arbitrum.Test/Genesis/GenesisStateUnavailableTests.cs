// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Genesis;

/// <summary>
/// Tests for GenesisStateUnavailable mode (comparison/system tests).
/// </summary>
public class GenesisStateUnavailableTests
{
    [Test]
    public void NoOpGenesisLoader_Load_DoesNotThrow()
    {
        NoOpGenesisLoader loader = new(LimboLogs.Instance);

        Action act = () => loader.Load();

        act.Should().NotThrow();
    }

    [Test]
    public void ArbitrumGenesisBuilder_GenesisStateUnavailable_ReturnsGenesisWithoutStateInit()
    {
        Block expectedGenesis = Build.A.Block.Genesis.TestObject;

        ChainSpec chainSpec = new()
        {
            GenesisStateUnavailable = true,
            Genesis = expectedGenesis
        };

        ISpecProvider specProvider = Substitute.For<ISpecProvider>();
        IArbitrumSpecHelper specHelper = Substitute.For<IArbitrumSpecHelper>();
        IWorldState worldState = Substitute.For<IWorldState>();

        // Create real state initializer - it won't be called when GenesisStateUnavailable=true
        ArbitrumGenesisStateInitializer stateInitializer = new(chainSpec, specHelper, LimboLogs.Instance);

        ArbitrumGenesisBuilder builder = new(
            chainSpec,
            specProvider,
            specHelper,
            worldState,
            stateInitializer,
            LimboLogs.Instance);

        Block result = builder.Build();

        // Verify genesis block is returned with hash calculated
        result.Should().Be(expectedGenesis);
        result.Header.Hash.Should().NotBeNull();
    }
}
