using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.State;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Tracing;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Precompiles.Parser;
using FluentAssertions;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class ArbInfoParserTests
{
    [Test]
    public void ParsesGetBalance_ValidInputData_ReturnsBalance()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        // Create test account
        Address testAccount = new("0x0000000000000000000000000000000000000123");
        UInt256 expectedBalance = 456;

        // Set the test account into the worldstate with the expected balance
        worldState.CreateAccount(testAccount, expectedBalance);
        worldState.Commit(London.Instance);

        string getBalanceMethodId = "0xf8b2cb4f";
        // remove the "0x" and pad with 0s to reach a 32-bytes address
        string leftPadded32BytesAddress = testAccount.ToString().Substring(2).PadLeft(64, '0');
        byte[] inputData = Bytes.FromHexString($"{getBalanceMethodId}{leftPadded32BytesAddress}");

        // Test GetBalance directly calling ArbInfo precompile
        ArbInfoParser arbInfoParser = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        byte[] balance = arbInfoParser.RunAdvanced(context, inputData);
        Assert.That(balance, Is.EqualTo(expectedBalance.ToBigEndian()), "ArbInfoParser.GetBalance should return the correct balance");
    }

    [Test]
    public void ParsesGetBalance_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        Address testAccount = new("0x0000000000000000000000000000000000000123");

        string getBalanceMethodId = "0xf8b2cb4f";
        string unpaddedAddress = testAccount.ToString().Substring(2);
        byte[] inputData = Bytes.FromHexString($"{getBalanceMethodId}{unpaddedAddress}");

        // Test GetBalance directly calling ArbInfo precompile
        ArbInfoParser arbInfoParser = new();
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, 0, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        Action action = () => arbInfoParser.RunAdvanced(context, inputData);
        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void ParsesGetCode_ValidInputData_ReturnsCode()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        // Create some contract whose code to get within the world state
        Address someContract = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(someContract, 0);
        byte[] runtimeCode = Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000123456");
        worldState.InsertCode(someContract, new ValueHash256(runtimeCode), runtimeCode, London.Instance, false);
        worldState.Commit(London.Instance);

        string getCodeMethodId = "0x7e105ce2";
        // remove the "0x" and pad with 0s to reach a 32-bytes address
        string leftPadded32BytesAddress = someContract.ToString().Substring(2).PadLeft(64, '0');
        byte[] inputData = Bytes.FromHexString($"{getCodeMethodId}{leftPadded32BytesAddress}");

        ArbInfoParser arbInfoParser = new();
        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        byte[] code = arbInfoParser.RunAdvanced(context, inputData);
        Assert.That(code, Is.EqualTo(runtimeCode), "ArbInfoParser.GetCode should return the correct code");
    }

    [Test]
    public void ParsesGetCode_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        Address someContract = new("0x0000000000000000000000000000000000000123");

        string getCodeMethodId = "0x7e105ce2";
        string unpaddedAddress = someContract.ToString().Substring(2);
        byte[] inputData = Bytes.FromHexString($"{getCodeMethodId}{unpaddedAddress}");

        ArbInfoParser arbInfoParser = new();
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, 0, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        Action action = () => arbInfoParser.RunAdvanced(context, inputData);
        action.Should().Throw<EndOfStreamException>();
    }
}
