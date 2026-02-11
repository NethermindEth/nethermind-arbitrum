// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Execution;

public class StylusEvmDataTests
{
    private static readonly Address EcrecoverAddress = new("0x0000000000000000000000000000000000000001");
    private static readonly Address ArbTestAddress = ArbosAddresses.ArbosTestAddress;

    [Test]
    public void EvmData_StylusContractAccount_ReturnsCorrectContractCodeHashes()
    {
        TestContext context = SetupTestContext();

        byte[] evmDataCode = context.Chain.WorldStateAccessor.GetCode(context.EvmDataContractAddress)!;
        Hash256 expectedContractCodeHash = Keccak.Compute(evmDataCode);

        byte[] callData = EvmDataCallData.CreateCallData(
            balanceCheckAddress: context.Sender,
            ethPrecompileAddress: EcrecoverAddress,
            arbTestAddress: ArbTestAddress,
            contractAddress: context.EvmDataContractAddress,
            burnCallData: EvmDataCallData.CreateBurnArbGasCallData(new UInt256(1000)));

        EvmDataResponse response = CallEvmDataContract(context, callData, evmDataCode.Length);

        response.ContractCodeHash.Should().Be(expectedContractCodeHash);
        response.ContractCode.Should().BeEquivalentTo(evmDataCode);
        response.ArbPrecompileCodeHash.Should().Be(Nethermind.Arbitrum.Arbos.Precompiles.InvalidCodeHash);
        response.EthPrecompileCodeHash.Should().Be(Hash256.Zero);
    }

    [Test]
    public void EvmData_NonExistentAccount_ReturnsZeroHash()
    {
        TestContext context = SetupTestContext();
        Address address = new("0x000000000000000000000000000000000000dead");

        byte[] callData = EvmDataCallData.CreateCallData(
            balanceCheckAddress: address,
            ethPrecompileAddress: EcrecoverAddress,
            arbTestAddress: ArbTestAddress,
            contractAddress: address,
            burnCallData: EvmDataCallData.CreateBurnArbGasCallData(new UInt256(1000)));

        EvmDataResponse response = CallEvmDataContract(context, callData, expectedCodeLength: 0);

        response.ContractCodeHash.Should().Be(Hash256.Zero);
        response.ContractCode.Should().BeEmpty();
    }

    [Test]
    public void EvmData_EoaAccount_ReturnsEmptyCodeHash()
    {
        TestContext context = SetupTestContext();
        Address eoa = FullChainSimulationAccounts.AccountA.Address;

        context.Chain.PrefundAccount(eoa, 1.Ether()).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        byte[] callData = EvmDataCallData.CreateCallData(
            balanceCheckAddress: eoa,
            ethPrecompileAddress: EcrecoverAddress,
            arbTestAddress: ArbTestAddress,
            contractAddress: eoa,
            burnCallData: EvmDataCallData.CreateBurnArbGasCallData(new UInt256(1000)));

        EvmDataResponse response = CallEvmDataContract(context, callData, expectedCodeLength: 0);

        response.ContractCodeHash.Should().Be(Keccak.OfAnEmptyString);
        response.ContractCode.Should().BeEmpty();
    }

    [Test]
    public void EvmData_Always_ReturnsCorrectBlockData()
    {
        UInt256 now = (UInt256)DateTimeOffset.Now.ToUnixTimeSeconds();
        TestContext context = SetupTestContext();

        byte[] evmDataCode = context.Chain.WorldStateAccessor.GetCode(context.EvmDataContractAddress)!;
        byte[] callData = EvmDataCallData.CreateCallData(
            balanceCheckAddress: context.Sender,
            ethPrecompileAddress: EcrecoverAddress,
            arbTestAddress: ArbTestAddress,
            contractAddress: context.EvmDataContractAddress,
            burnCallData: EvmDataCallData.CreateBurnArbGasCallData(new UInt256(1000)));

        EvmDataResponse response = CallEvmDataContract(context, callData, evmDataCode.Length);

        response.BlockNumberMinusOne.Should().Be((UInt256)context.Chain.BlockTree.Head!.Number - 1);
        response.ChainId.Should().Be(context.Chain.ChainSpec.ChainId);
        response.BaseFee.Should().Be(0);
        response.GasPrice.Should().Be(1);
        response.GasLimit.Should().Be(1125899906842624);
        response.Value.Should().Be(1);
        response.Timestamp.Should().BeGreaterOrEqualTo(now);
    }

    [Test]
    public void EvmData_Always_ReturnsCorrectGasValues()
    {
        TestContext context = SetupTestContext();

        byte[] evmDataCode = context.Chain.WorldStateAccessor.GetCode(context.EvmDataContractAddress)!;
        byte[] callData = EvmDataCallData.CreateCallData(
            balanceCheckAddress: context.Sender,
            ethPrecompileAddress: EcrecoverAddress,
            arbTestAddress: ArbTestAddress,
            contractAddress: context.EvmDataContractAddress,
            burnCallData: EvmDataCallData.CreateBurnArbGasCallData(new UInt256(1000)));

        EvmDataResponse response = CallEvmDataContract(context, callData, evmDataCode.Length);

        response.InkPrice.Should().Be(10000);
        response.GasLeftBefore.Should().Be(9953681);
        response.InkLeftBefore.Should().Be(99536805087);
        response.GasLeftAfter.Should().Be(9952561);
        response.InkLeftAfter.Should().Be(99525599658);
    }

    [Test]
    public void EvmData_AccountBalance_ReturnsCorrectBalance()
    {
        TestContext context = SetupTestContext();
        Address accountToCheck = FullChainSimulationAccounts.AccountB.Address;
        UInt256 expectedBalance = 5.Ether();

        context.Chain.PrefundAccount(accountToCheck, expectedBalance).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        byte[] evmDataCode = context.Chain.WorldStateAccessor.GetCode(context.EvmDataContractAddress)!;
        byte[] callData = EvmDataCallData.CreateCallData(
            balanceCheckAddress: accountToCheck,
            ethPrecompileAddress: EcrecoverAddress,
            arbTestAddress: ArbTestAddress,
            contractAddress: context.EvmDataContractAddress,
            burnCallData: EvmDataCallData.CreateBurnArbGasCallData(new UInt256(1000)));

        EvmDataResponse response = CallEvmDataContract(context, callData, evmDataCode.Length);

        response.AddressBalance.Should().Be(expectedBalance);
    }

    private static TestContext SetupTestContext()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        chain.PrefundAccount(sender, 1000.Ether()).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.DeployStylusContract(sender, "Arbos/Stylus/Resources/evm-data.wat", out _, out Address evmDataAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.ActivateStylusContract(sender, evmDataAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        return new TestContext(chain, sender, evmDataAddress);
    }

    private static EvmDataResponse CallEvmDataContract(TestContext context, byte[] callData, int expectedCodeLength)
    {
        Transaction tx = Build.A.Transaction
            .WithTo(context.EvmDataContractAddress)
            .WithData(callData)
            .WithGasLimit(10_000_000)
            .WithSenderAddress(context.Sender)
            .WithValue(1)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<string> result = context.Chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success, $"eth_call should succeed: {result.Result.Error}");
        result.Data.Should().NotBeNullOrEmpty("Return data should not be empty");

        byte[] responseBytes = Bytes.FromHexString(result.Data!);
        return EvmDataCallData.ParseResponse(responseBytes, expectedCodeLength);
    }

    private sealed record TestContext(
        ArbitrumRpcTestBlockchain Chain,
        Address Sender,
        Address EvmDataContractAddress);
}
