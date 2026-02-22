// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Execution;

/// <summary>
/// Tests for Stylus contract CREATE/CREATE2 operations.
/// Based on nitro/system_tests/program_gas_test.go TestProgramCreateCost.
/// Uses realistic EVM bytecode matching ProgramTestMetaData.Bin from Nitro.
/// </summary>
public class StylusCreateContractTests
{
    [Test]
    public void StylusCreate1_WithRealisticDeployCode_DeploysContractSuccessfully()
    {
        TestContext context = SetupTestContext();

        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(ProgramTestDeployCode);
        Transaction createTx = BuildCreateTransaction(context, create1CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt txReceipt = context.Chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);
        txReceipt.Logs.Should().NotBeEmpty("CREATE1 should emit log with created address");

        Address createdAddress = new(txReceipt.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode = context.Chain.MainWorldState.GetCode(createdAddress)!;
            deployedCode.Should().NotBeEmpty("Contract code should be deployed at the created address");
        }
    }

    [Test]
    public void StylusCreate2_WithRealisticDeployCodeAndSalt_DeploysContractSuccessfully()
    {
        TestContext context = SetupTestContext();

        Hash256 salt = new("0x000000000000000000000000000000000000000000000000000000000000beef");
        byte[] create2CallData = CreateContractCallData.CreateCreate2CallData(ProgramTestDeployCode, salt);
        Transaction createTx = BuildCreateTransaction(context, create2CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt txReceipt = context.Chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);
        txReceipt.Logs.Should().NotBeEmpty("CREATE2 should emit log with created address");

        Address createdAddress = new(txReceipt.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode = context.Chain.MainWorldState.GetCode(createdAddress)!;
            deployedCode.Should().NotBeEmpty("Contract code should be deployed at the created address");
        }
    }

    [Test]
    public void StylusCreate1_WithZeroEndowment_DeploysContractSuccessfully()
    {
        TestContext context = SetupTestContext();

        byte[] zeroEndowment = Hash256.Zero.Bytes.ToArray();
        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(ProgramTestDeployCode, zeroEndowment);
        Transaction createTx = BuildCreateTransaction(context, create1CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt txReceipt = context.Chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);

        Address createdAddress = new(txReceipt.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode = context.Chain.MainWorldState.GetCode(createdAddress)!;
            deployedCode.Should().NotBeEmpty("Contract code should be deployed at the created address");
        }
    }

    [Test]
    public void StylusCreate2_WithZeroEndowmentAndBeefSalt_DeploysContractSuccessfully()
    {
        TestContext context = SetupTestContext();

        byte[] zeroEndowment = Hash256.Zero.Bytes.ToArray();
        Hash256 beefSalt = new("0x000000000000000000000000000000000000000000000000000000000000beef");
        byte[] create2CallData = CreateContractCallData.CreateCreate2CallData(ProgramTestDeployCode, beefSalt, zeroEndowment);
        Transaction createTx = BuildCreateTransaction(context, create2CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt txReceipt = context.Chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);

        Address createdAddress = new(txReceipt.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode = context.Chain.MainWorldState.GetCode(createdAddress)!;
            deployedCode.Should().NotBeEmpty("Contract code should be deployed at the created address");
        }
    }

    [Test]
    public void StylusCreate2_WithDifferentSalts_CreatesDifferentAddresses()
    {
        TestContext context = SetupTestContext();

        Hash256 salt1 = new("0x0000000000000000000000000000000000000000000000000000000000000001");
        Hash256 salt2 = new("0x0000000000000000000000000000000000000000000000000000000000000002");

        byte[] create2CallData1 = CreateContractCallData.CreateCreate2CallData(ProgramTestDeployCode, salt1);
        Transaction createTx1 = BuildCreateTransaction(context, create2CallData1, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx1)).ShouldAsync()
            .RequestSucceed();

        TxReceipt receipt1 = context.Chain.LatestReceipts()[1];
        receipt1.StatusCode.Should().Be(StatusCode.Success);
        receipt1.Logs.Should().NotBeEmpty();

        Address createdAddress1 = new(receipt1.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode = context.Chain.MainWorldState.GetCode(createdAddress1)!;
            deployedCode.Should().NotBeEmpty("Contract code should be deployed at the created address");
        }

        Hash256 address1Topic = receipt1.Logs![0].Topics[0];

        byte[] create2CallData2 = CreateContractCallData.CreateCreate2CallData(ProgramTestDeployCode, salt2);
        Transaction createTx2 = BuildCreateTransaction(context, create2CallData2, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx2)).ShouldAsync()
            .RequestSucceed();

        TxReceipt receipt2 = context.Chain.LatestReceipts()[1];
        receipt2.StatusCode.Should().Be(StatusCode.Success);
        receipt2.Logs.Should().NotBeEmpty();

        Address createdAddress = new(receipt2.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode = context.Chain.MainWorldState.GetCode(createdAddress)!;
            deployedCode.Should().NotBeEmpty("Contract code should be deployed at the created address");
        }

        Hash256 address2Topic = receipt2.Logs![0].Topics[0];

        address1Topic.Should().NotBe(address2Topic, "Different salts should produce different addresses");
    }

    [Test]
    public void StylusCreate1_WithInsufficientGasForCodeDeposit_FailsTransaction()
    {
        TestContext context = SetupTestContext();

        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(ProgramTestDeployCode);
        Transaction createTx = BuildCreateTransaction(context, create1CallData, gasLimit: 500_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed();

        TxReceipt txReceipt = context.Chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Failure, "CREATE with insufficient gas for code deposit should fail");
    }

    [Test]
    public void StylusCreate2_WithLargeDeployCode_ConsumesMoreGasThanCreate1()
    {
        TestContext context = SetupTestContext();

        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(ProgramTestDeployCode);
        Transaction createTx1 = BuildCreateTransaction(context, create1CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx1)).ShouldAsync()
            .RequestSucceed();

        TxReceipt receipt1 = context.Chain.LatestReceipts()[1];
        receipt1.StatusCode.Should().Be(StatusCode.Success);
        long create1Gas = receipt1.GasUsed;

        Address createdAddress1 = new(receipt1.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode1 = context.Chain.MainWorldState.GetCode(createdAddress1)!;
            deployedCode1.Should().NotBeEmpty("CREATE1 contract code should be deployed");
        }

        Hash256 salt = Hash256.Zero;
        byte[] create2CallData = CreateContractCallData.CreateCreate2CallData(ProgramTestDeployCode, salt);
        Transaction createTx2 = BuildCreateTransaction(context, create2CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx2)).ShouldAsync()
            .RequestSucceed();

        TxReceipt receipt2 = context.Chain.LatestReceipts()[1];
        receipt2.StatusCode.Should().Be(StatusCode.Success);
        long create2Gas = receipt2.GasUsed;

        Address createdAddress2 = new(receipt2.Logs![0].Topics[0].Bytes.Slice(12, 20).ToArray());
        using (context.Chain.MainWorldState.BeginScope(context.Chain.BlockTree.Head?.Header))
        {
            byte[] deployedCode2 = context.Chain.MainWorldState.GetCode(createdAddress2)!;
            deployedCode2.Should().NotBeEmpty("CREATE2 contract code should be deployed");
        }

        create2Gas.Should().BeGreaterThan(create1Gas, "CREATE2 should consume more gas than CREATE1 due to sha3 word cost for hashing init code");
    }

    /// <summary>
    /// Before the fix, StylusCreate enforced EIP-3860's init code size limit (2 * 24576 = 49152 bytes),
    /// rejecting CREATE1 with larger init code. Nitro's Stylus API create closure does not apply
    /// this limit, so we removed it from StylusCreate to match.
    /// </summary>
    [Test]
    public void StylusCreate1_WithInitCodeExceedingEip3860SizeLimit_DeploysContractSuccessfully()
    {
        TestContext context = SetupTestContext();

        byte[] oversizedInitCode = BuildMinimalInitCodeOfSize(size: 50_001);
        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(oversizedInitCode);
        Transaction createTx = BuildCreateTransaction(context, create1CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);
    }

    /// <summary>
    /// Same as the CREATE1 case: EIP-3860 size limit is not applied for CREATE2 either,
    /// and CREATE2 additionally requires the sha3 word cost for hashing the init code.
    /// </summary>
    [Test]
    public void StylusCreate2_WithInitCodeExceedingEip3860SizeLimit_DeploysContractSuccessfully()
    {
        TestContext context = SetupTestContext();

        Hash256 salt = new("0x0000000000000000000000000000000000000000000000000000000000000001");
        byte[] oversizedInitCode = BuildMinimalInitCodeOfSize(size: 50_001);
        byte[] create2CallData = CreateContractCallData.CreateCreate2CallData(oversizedInitCode, salt);
        Transaction createTx = BuildCreateTransaction(context, create2CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);
    }

    /// <summary>
    /// Verifies that the gas overhead of CREATE2 over CREATE1 (on identical init code) equals
    /// exactly the sha3 word cost for hashing the init code — no EIP-3860 word cost on top.
    ///
    /// The expected delta is: sha3WordCost * ceil(initCode.Length / 32) + 128 gas for the
    /// 32-byte zero-filled salt added to the transaction calldata.
    ///
    /// If EIP-3860 word cost (2 gas/word) were incorrectly applied on top, the delta would be
    /// roughly 2 * ceil(initCode.Length / 32) * 288 / 144 ≈ 33% higher than the upper bound.
    /// If sha3 word cost were missing entirely, the delta would drop to ~128 (calldata only),
    /// well below the lower bound.
    /// </summary>
    [Test]
    public void StylusCreate2VsCreate1_WithSameInitCode_GasDeltaMatchesSha3WordCostOnly()
    {
        TestContext context = SetupTestContext();

        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(ProgramTestDeployCode);
        Transaction create1Tx = BuildCreateTransaction(context, create1CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, create1Tx)).ShouldAsync()
            .RequestSucceed();

        TxReceipt create1Receipt = context.Chain.LatestReceipts()[1];
        create1Receipt.StatusCode.Should().Be(StatusCode.Success);
        long create1Gas = create1Receipt.GasUsed;

        Hash256 salt = Hash256.Zero;
        byte[] create2CallData = CreateContractCallData.CreateCreate2CallData(ProgramTestDeployCode, salt);
        Transaction create2Tx = BuildCreateTransaction(context, create2CallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, create2Tx)).ShouldAsync()
            .RequestSucceed();

        TxReceipt create2Receipt = context.Chain.LatestReceipts()[1];
        create2Receipt.StatusCode.Should().Be(StatusCode.Success);
        long create2Gas = create2Receipt.GasUsed;

        // sha3 word cost = GasCostOf.Sha3Word (6) per 32-byte word of init code
        long initCodeWords = (ProgramTestDeployCode.Length + 31) / 32;
        long expectedSha3Cost = GasCostOf.Sha3Word * initCodeWords;

        // Observed delta = sha3Cost + calldata overhead for 32-byte zero salt (128 gas) ± WASM overhead
        long gasDelta = create2Gas - create1Gas;
        gasDelta.Should().BeGreaterThan(expectedSha3Cost + 100,
            "CREATE2 must charge sha3 word cost ({0} gas for {1} words); too-low delta indicates sha3 is missing",
            expectedSha3Cost, initCodeWords);
        gasDelta.Should().BeLessThan(expectedSha3Cost + 300,
            "CREATE2 must not charge EIP-3860 word cost ({0} extra gas would be added); too-high delta indicates double-charging",
            GasCostOf.InitCodeWord * initCodeWords);
    }

    /// <summary>
    /// Regression test for the eip3860Cost subtraction bug: StylusCreate was returning
    /// (gasCost - eip3860Cost) + gasConsumed to Rust instead of gasCost + gasConsumed.
    /// Rust directly charges the WASM that value, so larger init code → larger eip3860Cost
    /// → more gas "given back" to WASM → lower transaction gas used than expected.
    ///
    /// The test uses two CREATE1 calls that differ only in init code size. Both init codes
    /// execute the same 3 instructions (PUSH1, PUSH1, RETURN) and deploy an empty contract,
    /// so the only expected gas difference between the two transactions is the calldata cost
    /// of the extra zero bytes. If the bug is present, the actual delta falls short of that
    /// by ~eip3860Cost(50000 bytes) ≈ 3126 gas — well outside the tolerance here.
    /// </summary>
    [Test]
    public void StylusCreate1_WithLargerInitCode_GasDeltaAlignedWithCallDataCostNotEip3860Savings()
    {
        TestContext context = SetupTestContext();

        byte[] smallInitCode = BuildMinimalInitCodeOfSize(size: 32);
        byte[] smallCallData = CreateContractCallData.CreateCreate1CallData(smallInitCode);
        Transaction smallTx = BuildCreateTransaction(context, smallCallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, smallTx)).ShouldAsync()
            .RequestSucceed();

        TxReceipt smallReceipt = context.Chain.LatestReceipts()[1];
        smallReceipt.StatusCode.Should().Be(StatusCode.Success);
        long smallGas = smallReceipt.GasUsed;

        byte[] largeInitCode = BuildMinimalInitCodeOfSize(size: 50_000);
        byte[] largeCallData = CreateContractCallData.CreateCreate1CallData(largeInitCode);
        Transaction largeTx = BuildCreateTransaction(context, largeCallData, gasLimit: 10_000_000);

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, largeTx)).ShouldAsync()
            .RequestSucceed();

        TxReceipt largeReceipt = context.Chain.LatestReceipts()[1];
        largeReceipt.StatusCode.Should().Be(StatusCode.Success);
        long largeGas = largeReceipt.GasUsed;

        // Both init codes share 4 non-zero bytes in calldata (kind + 3 EVM instruction bytes).
        // All extra bytes in the large version are zeros (STOP opcodes / endowment padding).
        long calldataCostDelta = (long)(largeCallData.Length - smallCallData.Length) * 4;

        // With the fix: actualDelta ≈ calldataCostDelta + WASM read overhead (positive)
        // With the bug: actualDelta ≈ calldataCostDelta - eip3860Cost(50000) + WASM read overhead
        //   eip3860Cost(50000 bytes) = 2 * ceil(50000/32) = 2 * 1563 = 3126 gas → delta shrinks by ~3126
        long actualDelta = largeGas - smallGas;
        actualDelta.Should().BeGreaterThan(calldataCostDelta - 1000,
            "gas delta must not be reduced by eip3860 word cost being subtracted from StylusCreate return value; " +
            "expected delta ≥ {0} (calldata cost difference minus tolerance), got {1}",
            calldataCostDelta - 1000, actualDelta);
    }

    [Test]
    public void StylusCreate1_Arbos50WithInsufficientGas_FailsTransaction()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 50)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        chain.PrefundAccount(sender, 1000.Ether()).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.DeployStylusContract(sender, "Arbos/Stylus/Resources/create.wat", out _, out Address createContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.ActivateStylusContract(sender, createContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        byte[] create1CallData = CreateContractCallData.CreateCreate1CallData(ProgramTestDeployCode);
        long gasLimit = 500_000;

        Transaction createTx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(createContractAddress)
            .WithData(create1CallData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(chain.WorldStateAccessor.GetNonce(sender))
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        chain.Digest(new TestL2Transactions(chain.InitialL1BaseFee, sender, createTx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Failure]);

        TxReceipt txReceipt = chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Failure, "CREATE with insufficient gas should fail");
        txReceipt.GasUsed.Should().BeGreaterThan(0, "Some gas should be consumed before CREATE fails");
    }

    private TestContext SetupTestContext()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        chain.PrefundAccount(sender, 1000.Ether()).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.DeployStylusContract(sender, "Arbos/Stylus/Resources/create.wat", out _, out Address createContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.ActivateStylusContract(sender, createContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        return new TestContext(chain, sender, createContractAddress);
    }

    private Transaction BuildCreateTransaction(TestContext context, byte[] callData, long gasLimit = 5_000_000, UInt256? value = null)
    {
        return Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(context.CreateContractAddress)
            .WithData(callData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(context.Chain.WorldStateAccessor.GetNonce(context.Sender))
            .WithValue(value ?? UInt256.Zero)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;
    }

    /// <summary>
    /// Builds minimal valid EVM init code of the requested byte length.
    /// The first 5 bytes are: PUSH1 0x00, PUSH1 0x00, RETURN — which deploys an empty contract.
    /// The remaining bytes are 0x00 (STOP opcodes), unreachable after RETURN.
    /// This lets tests control init code size independently of execution cost or deployed code size.
    /// </summary>
    private static byte[] BuildMinimalInitCodeOfSize(int size)
    {
        byte[] code = new byte[size];
        code[0] = 0x60; // PUSH1
        code[1] = 0x00; // 0 (return length)
        code[2] = 0x60; // PUSH1
        code[3] = 0x00; // 0 (return offset)
        code[4] = 0xF3; // RETURN → deploys 0-byte contract; bytes [5..] are STOP, never reached
        return code;
    }

    /// <summary>
    /// Realistic EVM contract bytecode from Nitro's mocksgen.ProgramTestMetaData.Bin.
    /// This is the same deploy code used in nitro/system_tests/program_gas_test.go TestProgramCreateCost.
    /// </summary>
    private static byte[] ProgramTestDeployCode => Bytes.FromHexString(
        "0x608060405234801561001057600080fd5b506111ff806100206000396000f3fe60806040526004361061005a5760003560e01c806396ec12e51161004357806396ec12e5146100b7578063aba8c4ba146100ca578063fd424462146100ea57600080fd5b80631d00bae41461005f5780633fdd58e214610081575b600080fd5b34801561006b57600080fd5b5061007f61007a366004610f0a565b61010a565b005b34801561008d57600080fd5b506100a161009c366004610f0a565b61022a565b6040516100ae9190610fc1565b60405180910390f35b6100a16100c5366004610fdb565b6102d2565b3480156100d657600080fd5b506100a16100e536600461105c565b6104c5565b3480156100f657600080fd5b5061007f6101053660046110bf565b610c17565b600080846001600160a01b031684846040516101279291906110da565b6000604051808303816000865af19150503d8060008114610164576040519150601f19603f3d011682016040523d82523d6000602084013e610169565b606091505b5091509150816101ae5760405162461bcd60e51b815260206004820152600b60248201526a18d85b1b0819985a5b195960aa1b60448201526064015b60405180910390fd5b60006101b9826110ea565b90507f224c8d9ad1bbf0f44a61d7bd8e7e9049b1a320e04b047da9910945675c31ba43816040516101ec91815260200190565b60405180910390a16102018460018188611111565b60405161020f9291906110da565b6040518091039020811461022257600080fd5b505050505050565b6060600080856001600160a01b031685856040516102499291906110da565b600060405180830381855afa9150503d8060008114610284576040519150601f19603f3d011682016040523d82523d6000602084013e610289565b606091505b5091509150816102c95760405162461bcd60e51b815260206004820152600b60248201526a18d85b1b0819985a5b195960aa1b60448201526064016101a5565b95945050505050565b6060600080876001600160a01b03163488886040516102f29291906110da565b60006040518083038185875af1925050503d806000811461032f576040519150601f19603f3d011682016040523d82523d6000602084013e610334565b606091505b509150915081156103875760405162461bcd60e51b815260206004820152601260248201527f756e65787065637465642073756363657373000000000000000000000000000060448201526064016101a5565b805184146103d75760405162461bcd60e51b815260206004820152601860248201527f77726f6e67207265766572742064617461206c656e677468000000000000000060448201526064016101a5565b60005b81518110156104b9578585828181106103f5576103f561113b565b9050013560f81c60f81b7effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff19168282815181106104335761043361113b565b01602001517fff0000000000000000000000000000000000000000000000000000000000000016146104a75760405162461bcd60e51b815260206004820152601460248201527f7265766572742064617461206d69736d6174636800000000000000000000000060448201526064016101a5565b806104b181611167565b9150506103da565b50979650505050505050565b6060600080876001600160a01b03168667ffffffffffffffff1686866040516104ef9291906110da565b6000604051808303818686fa925050503d806000811461052b576040519150601f19603f3d011682016040523d82523d6000602084013e610530565b606091505b5091509150816105705760405162461bcd60e51b815260206004820152600b60248201526a18d85b1b0819985a5b195960aa1b60448201526064016101a5565b60408051808201909152600d81527f626c6f636b206e756d626572200000000000000000000000000000000000000060208201526069906001906105bf9084906105ba8443611181565b610d82565b9250610601836040518060400160405280600d81526020017f636861696e20696420202020200000000000000000000000000000000000000081525046610d82565b9250610643836040518060400160405280600d81526020017f626173652066656520202020200000000000000000000000000000000000000081525048610d82565b9250610685836040518060400160405280600d81526020017f67617320707269636520202020000000000000000000000000000000000000008152503a610d82565b92506106c7836040518060400160405280600d81526020017f676173206c696d6974202020200000000000000000000000000000000000000081525045610d82565b925061070a836040518060400160405280600d81526020017f76616c75652020202020202020000000000000000000000000000000000000008152506000610d82565b925061074c836040518060400160405280600d81526020017f74696d657374616d70202020200000000000000000000000000000000000000081525042610d82565b9250610798836040518060400160405280600d81526020017f62616c616e6365202020202020000000000000000000000000000000000000008152508b6001600160a01b031631610d82565b92506107e3836040518060400160405280600d81526020017f72757374206164647265737320000000000000000000000000000000000000008152508c6001600160a01b0316610d82565b925061082e836040518060400160405280600d81526020017f73656e6465722020202020202000000000000000000000000000000000000000815250306001600160a01b0316610d82565b9250610879836040518060400160405280600d81526020017f6f726967696e2020202020202000000000000000000000000000000000000000815250326001600160a01b0316610d82565b92506108c4836040518060400160405280600d81526020017f636f696e62617365202020202000000000000000000000000000000000000000815250416001600160a01b0316610d82565b9250610913836040518060400160405280600d81526020017f7275737420636f646568617368000000000000000000000000000000000000008152508c6001600160a01b03163f60001c610d82565b9250610962836040518060400160405280600d81526020017f61726220636f6465686173682000000000000000000000000000000000000000815250846001600160a01b03163f60001c610d82565b92506109b1836040518060400160405280600d81526020017f65746820636f6465686173682000000000000000000000000000000000000000815250836001600160a01b03163f60001c610d82565b925060008a6001600160a01b03163b67ffffffffffffffff8111156109d8576109d861119a565b6040519080825280601f01601f191660200182016040528015610a02576020820181803683370190505b50905060005b8b6001600160a01b03163b811015610a8a57848181518110610a2c57610a2c61113b565b602001015160f81c60f81b828281518110610a4957610a4961113b565b60200101907effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1916908160001a90535080610a8281611167565b915050610a08565b508a6001600160a01b0316803b806020016040519081016040528181526000908060200190933c80519060200120818051906020012014610b0f5760405162461bcd60e51b81526004016101a59060208082526004908201527f636f646500000000000000000000000000000000000000000000000000000000604082015260600190565b60008b6001600160a01b03163b8551610b289190611181565b67ffffffffffffffff811115610b4057610b4061119a565b6040519080825280601f01601f191660200182016040528015610b6a576020820181803683370190505b5090506001600160a01b038c163b5b8551811015610c0757858181518110610b9457610b9461113b565b602001015160f81c60f81b828e6001600160a01b03163b83610bb69190611181565b81518110610bc657610bc661113b565b60200101907effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1916908160001a90535080610bff81611167565b915050610b79565b509b9a5050505050505050505050565b7feddecf107b5740cef7f5a01e3ea7e287665c4e75a8eb6afae2fda2e3d43677866401000003d0197fc6178c2de1078cd36c3bd302cde755340d7f17fcb3fcc0b9c333ba03b217029f820990507fc6178c2de1078cd36c3bd302cde755340d7f17fcb3fcc0b9c333ba03b217029f6401000003d0198208905067eddecf107b5740ce8167fffffffefffffc2f9190040a905067c6178c2de1078cd381069050600080836001600160a01b03166040516000604051808303816000865af19150503d8060008114610d03576040519150601f19603f3d011682016040523d82523d6000602084013e610d08565b606091505b509150915081610d485760405162461bcd60e51b815260206004820152600b60248201526a18d85b1b0819985a5b195960aa1b60448201526064016101a5565b60408051602081018590520160405160208183030381529060405280519060200120818051906020012014610d7c57600080fd5b50505050565b6060600084806020019051810190610d9a91906111b0565b905083838214610dbd5760405162461bcd60e51b81526004016101a59190610fc1565b50600060208651610dce9190611181565b67ffffffffffffffff811115610de657610de661119a565b6040519080825280601f01601f191660200182016040528015610e10576020820181803683370190505b50905060205b8651811015610e9b57868181518110610e3157610e3161113b565b602001015160f81c60f81b82602083610e4a9190611181565b81518110610e5a57610e5a61113b565b60200101907effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1916908160001a90535080610e9381611167565b915050610e16565b5095945050505050565b80356001600160a01b0381168114610ebc57600080fd5b919050565b60008083601f840112610ed357600080fd5b50813567ffffffffffffffff811115610eeb57600080fd5b602083019150836020828501011115610f0357600080fd5b9250929050565b600080600060408486031215610f1f57600080fd5b610f2884610ea5565b9250602084013567ffffffffffffffff811115610f4457600080fd5b610f5086828701610ec1565b9497909650939450505050565b6000815180845260005b81811015610f8357602081850181015186830182015201610f67565b5060006020828601015260207fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f83011685010191505092915050565b602081526000610fd46020830184610f5d565b9392505050565b600080600080600060608688031215610ff357600080fd5b610ffc86610ea5565b9450602086013567ffffffffffffffff8082111561101957600080fd5b61102589838a01610ec1565b9096509450604088013591508082111561103e57600080fd5b5061104b88828901610ec1565b969995985093965092949392505050565b60008060008060006080868803121561107457600080fd5b61107d86610ea5565b945061108b60208701610ea5565b9350604086013567ffffffffffffffff80821682146110a957600080fd5b9093506060870135908082111561103e57600080fd5b6000602082840312156110d157600080fd5b610fd482610ea5565b8183823760009101908152919050565b8051602080830151919081101561110b576000198160200360031b1b821691505b50919050565b6000808585111561112157600080fd5b8386111561112e57600080fd5b5050820193919092039150565b634e487b7160e01b600052603260045260246000fd5b634e487b7160e01b600052601160045260246000fd5b6000600019820361117a5761117a611151565b5060010190565b8181038181111561119457611194611151565b92915050565b634e487b7160e01b600052604160045260246000fd5b6000602082840312156111c257600080fd5b505191905056fea2646970667358221220597cde1bb7eee207d3d4952b075dafdb1bb7db5e9892e0371a5a1c3aabcdf00f64736f6c63430008110033");

    private sealed record TestContext(
        ArbitrumRpcTestBlockchain Chain,
        Address Sender,
        Address CreateContractAddress);
}
