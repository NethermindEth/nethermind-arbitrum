// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.State;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.State;

public class ZombieAccountTests
{
    private static readonly Address AddressA = new(Keccak.Compute("zombie-test-a"));
    private static readonly Address AddressB = new(Keccak.Compute("zombie-test-b"));

    [Test]
    public void CreateEmptyAccountIfDeleted_NewEmptyAccountPrunedThenCrossTxZombieCreation_PreservesAccount()
    {
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis);

        // TX 1: Create empty account, commit triggers EIP-158 pruning
        worldState.CreateAccount(AddressA, 0);
        worldState.Commit(London.Instance);

        // Account should be pruned by EIP-158
        worldState.AccountExists(AddressA).Should().BeFalse();

        // TX 2: Zombie creation via cross-TX path
        worldState.CreateEmptyAccountIfDeleted(AddressA);
        worldState.Commit(London.Instance);

        // Zombie should survive EIP-158 via RecreateEmpty
        worldState.AccountExists(AddressA).Should().BeTrue();
    }

    [Test]
    public void CreateEmptyAccountIfDeleted_IntraTxCallOnEmptyAccount_DoesNotCreateZombie()
    {
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis);

        // Within single TX: Create empty account then call CreateEmptyAccountIfDeleted
        worldState.CreateAccount(AddressA, 0);
        worldState.CreateEmptyAccountIfDeleted(AddressA);
        worldState.Commit(London.Instance);

        // Should be pruned by EIP-158 — no zombie within same TX
        worldState.AccountExists(AddressA).Should().BeFalse();
    }

    [Test]
    public void CreateEmptyAccountIfDeleted_TouchedEmptyAccountPruned_TrackedForZombieCreation()
    {
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        BlockHeader baseBlock;
        using (IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis))
        {
            // Create account in Frontier (no EIP-158 pruning)
            worldState.CreateAccount(AddressA, 0);
            worldState.Commit(Frontier.Instance);
            worldState.CommitTree(0);
            baseBlock = Build.A.BlockHeader.WithStateRoot(worldState.StateRoot).TestObject;
        }

        using (IDisposable scope = worldState.BeginScope(baseBlock))
        {
            // TX 1: Touch the empty account with zero balance (triggers EIP-158 pruning)
            worldState.AddToBalance(AddressA, 0, London.Instance);
            worldState.Commit(London.Instance);

            // Account should be pruned
            worldState.AccountExists(AddressA).Should().BeFalse();

            // TX 2: Zombie creation — should work because Touch+empty is tracked
            worldState.CreateEmptyAccountIfDeleted(AddressA);
            worldState.Commit(London.Instance);

            worldState.AccountExists(AddressA).Should().BeTrue();
        }
    }

    [Test]
    public void CreateEmptyAccountIfDeleted_SelfDestructedAccount_TrackedForZombieCreation()
    {
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        BlockHeader baseBlock;
        using (IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis))
        {
            // Create account with balance so it persists
            worldState.CreateAccount(AddressA, 1);
            worldState.Commit(London.Instance);
            worldState.CommitTree(0);
            baseBlock = Build.A.BlockHeader.WithStateRoot(worldState.StateRoot).TestObject;
        }

        using (IDisposable scope = worldState.BeginScope(baseBlock))
        {
            // TX 1: Self-destruct the account
            worldState.DeleteAccount(AddressA);
            worldState.Commit(London.Instance);

            worldState.AccountExists(AddressA).Should().BeFalse();

            // TX 2: Zombie creation — should work because self-destruct is tracked
            worldState.CreateEmptyAccountIfDeleted(AddressA);
            worldState.Commit(London.Instance);

            worldState.AccountExists(AddressA).Should().BeTrue();
        }
    }

    [Test]
    public void CreateEmptyAccountIfDeleted_ZombieRevertedThenRecreated_PreservesAccount()
    {
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis);

        // TX 1: Create and prune empty account
        worldState.CreateAccount(AddressA, 0);
        worldState.Commit(London.Instance);

        worldState.AccountExists(AddressA).Should().BeFalse();

        // TX 2: Create zombie, then revert, then re-create
        Snapshot snapshot = worldState.TakeSnapshot();

        worldState.CreateEmptyAccountIfDeleted(AddressA);
        worldState.AccountExists(AddressA).Should().BeTrue();

        // Revert the zombie creation
        worldState.Restore(snapshot);
        worldState.AccountExists(AddressA).Should().BeFalse();

        // Re-create zombie — should succeed because _deletedThisBlock still has the address
        worldState.CreateEmptyAccountIfDeleted(AddressA);
        worldState.Commit(London.Instance);

        worldState.AccountExists(AddressA).Should().BeTrue();
    }

    [Test]
    public void CreateEmptyAccountIfDeleted_BlockReset_ClearsDeletedTracking()
    {
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis);

        // TX 1: Create and prune empty account
        worldState.CreateAccount(AddressA, 0);
        worldState.Commit(London.Instance);

        worldState.AccountExists(AddressA).Should().BeFalse();

        // Reset block state (simulates block boundary)
        worldState.Reset();

        // Zombie creation should fail — deleted set was cleared
        worldState.CreateEmptyAccountIfDeleted(AddressA);
        worldState.Commit(London.Instance);

        worldState.AccountExists(AddressA).Should().BeFalse();
    }
}
