using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class ArbInfoTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void GetBalance_PositiveBalanceAndEnoughGas_ReturnsBalance()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        // Create test account
        Address testAccount = new("0x0000000000000000000000000000000000000123");
        UInt256 expectedBalance = 456;

        // Set the test account into the worldstate with the expected balance
        worldState.CreateAccount(testAccount, expectedBalance);
        worldState.Commit(London.Instance);

        ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        UInt256 balance = ArbInfo.GetBalance(context, testAccount);

        Assert.That(balance, Is.EqualTo(expectedBalance), "ArbInfo.GetBalance should return the correct balance");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetBalance should consume the correct amount of gas");
    }

    [Test]
    public void GetBalance_NotEnoughGas_ThrowsOutOfGasException()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        // Create test account
        Address testAccount = new("0x0000000000000000000000000000000000000123");
        UInt256 expectedBalance = 456;

        // Set the test account into the worldstate with the expected balance
        worldState.CreateAccount(testAccount, expectedBalance);
        worldState.Commit(London.Instance);

        ulong gasSupplied = GasCostOf.BalanceEip1884 - 1;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        Assert.Throws<OutOfGasException>(() => ArbInfo.GetBalance(context, testAccount));
    }

    [Test]
    public void GetBalance_NonExistentAccount_Returns0()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        Address unsetTestAccount = new("0x0000000000000000000000000000000000000123");

        ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        UInt256 balance = ArbInfo.GetBalance(context, unsetTestAccount);

        Assert.That(balance, Is.EqualTo(UInt256.Zero), "ArbInfo.GetBalance should return 0 for non-existing account");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetBalance should consume the correct amount of gas");
    }

    [Test]
    public async Task GetBalance_DoesntHaveEnoughBalance_Fails()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);

        // Calldata to call getBalance(address) on ArbInfo precompile
        byte[] addressBytes = new byte[32];
        sender.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("getBalance(address)"u8)[..4], .. addressBytes];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbInfoAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(21432) // Enough to cover intrinsic gas 21432, but less than required 22938
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, 92, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2); // 2 transactions succeeded: internal, contract call
    }

    [Test]
    public void GetCode_ExistingContractAndEnoughGas_ReturnsCode()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        // Create some contract whose code to get within the world state
        Address someContract = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(someContract, 0);
        byte[] runtimeCode = Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000123456");
        worldState.InsertCode(someContract, new ValueHash256(runtimeCode), runtimeCode, London.Instance, false);
        worldState.Commit(London.Instance);

        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords + 1;

        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        byte[] code = ArbInfo.GetCode(context, someContract);

        Assert.That(code, Is.EqualTo(runtimeCode), "ArbInfo.GetCode should return the correct code");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetCode should consume the correct amount of gas");
    }

    [Test]
    public void GetCode_NotEnoughGas_ThrowsOutOfGasException()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        // Create some contract whose code to get within the world state
        Address someContract = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(someContract, 0);
        byte[] runtimeCode = Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000123456");
        worldState.InsertCode(someContract, new ValueHash256(runtimeCode), runtimeCode, London.Instance, false);
        worldState.Commit(London.Instance);

        ulong gasSupplied = 0;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        Assert.Throws<OutOfGasException>(() => ArbInfo.GetCode(context, someContract));
    }

    [Test]
    public void GetCode_NonExistentContract_ReturnsEmptyCode()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        Address unsetContract = new("0x0000000000000000000000000000000000000123");

        ulong gasSupplied = GasCostOf.ColdSLoad + 1;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        byte[] code = ArbInfo.GetCode(context, unsetContract);

        Assert.That(code, Is.EqualTo(Array.Empty<byte>()), "ArbInfo.GetCode should return the correct code");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetCode should consume the correct amount of gas"); ;
    }
}
