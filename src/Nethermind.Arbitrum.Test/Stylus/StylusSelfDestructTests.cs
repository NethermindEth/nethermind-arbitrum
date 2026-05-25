// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class StylusSelfDestructTests
{
    // SELFDESTRUCT(CALLER): caller becomes beneficiary, victim is drained.
    private static readonly Address VictimAddress = new("0x00000000000000000000000000000000000000aa");
    private static byte[] VictimCode => Prepare.EvmCode
        .Op(Instruction.CALLER)
        .Op(Instruction.SELFDESTRUCT)
        .Done;

    [TestCase(ArbosVersion.Thirty)]
    [TestCase(ArbosVersion.Fifty)]
    [TestCase(ArbosVersion.Sixty)]
    public void SelfDestruct_FromNonStylusContract_MovesBalanceToBeneficiary(ulong arbosVersion)
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        const ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, chain.SpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.MainWorldState;
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, arbosVersion);
        // Default chain spec initializes at v32; manually bumping to v50+ skips the v50 upgrade
        // that seeds PerTxGasLimitStorage. Seed it directly so the per-tx cap doesn't zero gasLeft.
        arbosState.L2PricingState.PerTxGasLimitStorage.Set(L2PricingState.InitialPerTxGasLimit);

        UInt256 victimBalance = 5.Ether;

        worldState.CreateAccount(VictimAddress, victimBalance);
        worldState.InsertCode(VictimAddress, VictimCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        const long gasLimit = 500_000;
        Transaction tx = Build.A.Transaction
            .WithTo(VictimAddress)
            .WithValue(0)
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        worldState.GetBalance(VictimAddress).Should().Be(UInt256.Zero,
            "Non-Stylus SELFDESTRUCT must drain the victim's balance");

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        UInt256 gasCharged = (ulong)tracer.GasSpent * baseFeePerGas;
        senderFinalBalance.Should().Be(senderInitialBalance + victimBalance - gasCharged,
            "Beneficiary (the caller) receives the victim's balance net of gas paid");
    }

    [Test]
    public void SelfDestruct_FromStylusDelegateCallToEvmVictim_RevertsAndPreservesContract()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: ArbosVersion.Forty)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        chain.PrefundAccount(sender, 1000.Ether).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.DeployStylusContract(sender, "Arbos/Stylus/Resources/multicall.wat", out _, out Address multicallAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.ActivateStylusContract(sender, multicallAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        // Fund the Stylus contract so SELFDESTRUCT, if it succeeded, would transfer balance to
        // CALLER. Preserving this balance after the failed inner call is the strongest evidence
        // that the override fired without state mutation.
        UInt256 multicallBalance = 7.Ether;
        chain.AppendBlock(c =>
        {
            c.MainWorldState.CreateAccount(VictimAddress, 0);
            c.MainWorldState.InsertCode(VictimAddress, VictimCode, c.SpecProvider.GenesisSpec);
            c.MainWorldState.AddToBalance(multicallAddress, multicallBalance, c.SpecProvider.GenesisSpec);
        });

        byte[] multicallData = MulticallCallData.CreateDelegateCall(VictimAddress, []);

        Transaction tx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(multicallAddress)
            .WithData(multicallData)
            .WithMaxFeePerGas(10.GWei)
            .WithGasLimit(2_000_000)
            .WithNonce(chain.WorldStateAccessor.GetNonce(sender))
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        // Inner SELFDESTRUCT reverts at the Stylus actor; multicall propagates the failure.
        chain.Digest(new TestL2Transactions(chain.InitialL1BaseFee, sender, tx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Failure]);

        // The Stylus contract must remain intact AND retain its balance — without the override,
        // SELFDESTRUCT would transfer multicall's balance to CALLER (and on chains without
        // EIP-6780 scope, also schedule deletion).
        chain.WorldStateAccessor.HasCode(multicallAddress).Should().BeTrue("Stylus multicall contract must survive a guarded SELFDESTRUCT attempt");
        chain.WorldStateAccessor.GetBalance(multicallAddress).Should().Be(multicallBalance, "Guarded SELFDESTRUCT must not transfer the Stylus contract's balance");
    }
}
