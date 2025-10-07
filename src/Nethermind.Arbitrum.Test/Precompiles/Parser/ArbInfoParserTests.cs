using Nethermind.Evm;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Precompiles.Parser;
using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.State;
using Nethermind.Arbitrum.Precompiles.Exceptions;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbInfoParserTests
{
    [Test]
    public void ParsesGetBalance_ValidInputData_ReturnsBalance()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // Create test account
        Address testAccount = new("0x0000000000000000000000000000000000000123");
        UInt256 expectedBalance = 456;

        // Set the test account into the worldstate with the expected balance
        worldState.CreateAccount(testAccount, expectedBalance);
        worldState.Commit(London.Instance);

        string getBalanceMethodId = "0xf8b2cb4f";
        // remove the "0x" and pad with 0s to reach a 32-bytes address
        string leftPadded32BytesAddress = testAccount.ToString(false, false).PadLeft(64, '0');
        byte[] inputData = Bytes.FromHexString($"{getBalanceMethodId}{leftPadded32BytesAddress}");

        ArbInfoParser arbInfoParser = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        byte[] balance = arbInfoParser.RunAdvanced(context, inputData);
        Assert.That(balance, Is.EqualTo(expectedBalance.ToBigEndian()), "ArbInfoParser.GetBalance should return the correct balance");
    }

    [Test]
    public void ParsesGetBalance_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address testAccount = new("0x0000000000000000000000000000000000000123");

        string getBalanceMethodId = "0xf8b2cb4f";
        string unpaddedAddress = testAccount.ToString(false, false);
        byte[] invalidInputData = Bytes.FromHexString($"{getBalanceMethodId}{unpaddedAddress}");

        ArbInfoParser arbInfoParser = new();
        PrecompileTestContextBuilder context = new(worldState, 0);

        Action action = () => arbInfoParser.RunAdvanced(context, invalidInputData);
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesGetCode_ValidInputData_ReturnsCode()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // Create some contract whose code to get within the world state
        Address someContract = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(someContract, 0);
        byte[] runtimeCode = Bytes.FromHexString("0x123456");
        Hash256 codeHash = Keccak.Compute(runtimeCode);
        worldState.InsertCode(someContract, codeHash, runtimeCode, London.Instance, false);
        worldState.Commit(London.Instance);

        string getCodeMethodId = "0x7e105ce2";
        // remove the "0x" and pad with 0s to reach a 32-bytes address
        string leftPadded32BytesAddress = someContract.ToString(false, false).PadLeft(64, '0');
        byte[] inputData = Bytes.FromHexString($"{getCodeMethodId}{leftPadded32BytesAddress}");

        ArbInfoParser arbInfoParser = new();
        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        byte[] expectedAbiEncodedCode = new byte[Hash256.Size * 3];
        // offset to data section: right after in our case as only the code is the only returned data from the function
        expectedAbiEncodedCode[Hash256.Size - 1] = 32;
        // the 2nd word contains the data length
        expectedAbiEncodedCode[Hash256.Size * 2 - 1] = (byte)runtimeCode.Length;
        // the 3rd word contains the data right padded with 0s
        runtimeCode.CopyTo(expectedAbiEncodedCode, Hash256.Size * 2);

        byte[] code = arbInfoParser.RunAdvanced(context, inputData);

        Assert.That(code, Is.EqualTo(expectedAbiEncodedCode), "ArbInfoParser.GetCode should return the correct code");
    }

    [Test]
    public void ParsesGetCode_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address someContract = new("0x0000000000000000000000000000000000000123");

        string getCodeMethodId = "0x7e105ce2";
        string unpaddedAddress = someContract.ToString(false, false);
        byte[] invalidInputData = Bytes.FromHexString($"{getCodeMethodId}{unpaddedAddress}");

        ArbInfoParser arbInfoParser = new();
        PrecompileTestContextBuilder context = new(worldState, 0);

        Action action = () => arbInfoParser.RunAdvanced(context, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }
}
