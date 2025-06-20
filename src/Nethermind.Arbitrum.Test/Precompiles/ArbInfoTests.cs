using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Tracing;
using Nethermind.Core.Crypto;

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

        ArbInfo arbInfo = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext()
        );
        UInt256 balance = arbInfo.GetBalance(context, testAccount);

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

        ArbInfo arbInfo = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884 - 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext()
        );

        Assert.Throws<OutOfGasException>(() => arbInfo.GetBalance(context, testAccount));
    }

    [Test]
    public void GetBalance_NonExistentAccount_Returns0()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        Address unsetTestAccount = new("0x0000000000000000000000000000000000000123");

        ArbInfo arbInfo = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext()
        );

        UInt256 balance = arbInfo.GetBalance(context, unsetTestAccount);

        Assert.That(balance, Is.EqualTo(UInt256.Zero), "ArbInfo.GetBalance should return 0 for non-existing account");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetBalance should consume the correct amount of gas");
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

        ArbInfo arbInfo = new();
        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext()
        );
        byte[] code = arbInfo.GetCode(context, someContract);

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

        ArbInfo arbInfo = new();
        ulong gasSupplied = 0;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext()
        );
        Assert.Throws<OutOfGasException>(() => arbInfo.GetCode(context, someContract));
    }

    [Test]
    public void GetCode_NonExistentContract_ReturnsEmptyCode()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        Address unsetContract = new("0x0000000000000000000000000000000000000123");

        ArbInfo arbInfo = new();
        ulong gasSupplied = GasCostOf.ColdSLoad + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext()
        );
        byte[] code = arbInfo.GetCode(context, unsetContract);

        Assert.That(code, Is.EqualTo(new byte[] { }), "ArbInfo.GetCode should return the correct code");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetCode should consume the correct amount of gas"); ;
    }
}
