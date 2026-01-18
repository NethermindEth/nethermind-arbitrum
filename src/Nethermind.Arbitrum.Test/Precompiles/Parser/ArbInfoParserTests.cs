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
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Abi;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbInfoParserTests
{
    private static readonly uint _getBalanceId = PrecompileHelper.GetMethodId("getBalance(address)");
    private static readonly uint _getCodeId = PrecompileHelper.GetMethodId("getCode(address)");

    [Test]
    public void ParsesGetBalance_ValidInputData_ReturnsBalance()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // Create test account
        Address testAccount = new("0x0000000000000000000000000000000000000123");
        UInt256 expectedBalance = 456;

        // Set the test account into the worldstate with the expected balance
        worldState.CreateAccount(testAccount, expectedBalance);
        worldState.Commit(London.Instance);

        ulong gasSupplied = GasCostOf.BalanceEip1884;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        bool exists = ArbInfoParser.PrecompileImplementation.TryGetValue(_getBalanceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbInfoParser.PrecompileFunctionDescription[_getBalanceId].AbiFunctionDescription;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            testAccount
        );

        byte[] result = implementation!(context, calldata);

        Assert.That(result, Is.EqualTo(expectedBalance.ToBigEndian()), "ArbInfoParser.GetBalance should return the correct balance");
    }

    [Test]
    public void ParsesGetBalance_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbInfoParser.PrecompileImplementation.TryGetValue(_getBalanceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Address testAccount = new("0x0000000000000000000000000000000000000123");
        byte[] malformedCalldata = testAccount.Bytes; // Not left-padded to 32 bytes

        Action action = () => implementation!(context, malformedCalldata);
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesGetCode_ValidInputData_ReturnsCode()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // Create some contract whose code to get within the world state
        Address someContract = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(someContract, 0);
        byte[] runtimeCode = Bytes.FromHexString("0x123456");
        Hash256 codeHash = Keccak.Compute(runtimeCode);
        worldState.InsertCode(someContract, codeHash, runtimeCode, London.Instance, false);
        worldState.Commit(London.Instance);

        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);

        bool exists = ArbInfoParser.PrecompileImplementation.TryGetValue(_getCodeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbInfoParser.PrecompileFunctionDescription[_getCodeId].AbiFunctionDescription;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            someContract
        );

        byte[] code = implementation!(context, calldata);

        byte[] expectedAbiEncodedCode = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [runtimeCode]
        );

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

        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbInfoParser.PrecompileImplementation.TryGetValue(_getCodeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] malformedCalldata = someContract.Bytes; // Not left-padded to 32 bytes

        Action action = () => implementation!(context, malformedCalldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }
}
