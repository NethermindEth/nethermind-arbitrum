// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class ArbDebugTests
{
    private const ulong DefaultGasSupplied = 1_000_000;
    private PrecompileTestContextBuilder _context = null!;
    private IDisposable _worldStateScope = null!;
    private IWorldState _worldState = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis); // Store the scope

        _ = ArbOSInitialization.Create(_worldState);

        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied).WithArbosState();
        _context.ResetGasLeft();
    }

    [TearDown]
    public void TearDown()
    {
        _worldStateScope?.Dispose();
    }

    [Test]
    public void BasicEvent_Always_EmitsEvent()
    {
        string eventSignature = "Basic(bool,bytes32)";
        bool flag = true;
        Hash256 value = ArbDebugParserTests.Hash256FromUlong(1); // indexed

        ArbDebug.EmitBasicEvent(_context, flag, value);

        Hash256[] expectedEventTopics = [Keccak.Compute(eventSignature), value]; // value is indexed, and already 32 bytes long

        byte[] expectedEventData = new byte[32];
        expectedEventData[31] = flag ? (byte)1 : (byte)0;

        LogEntry expectedLogEntry = new(ArbDebug.Address, expectedEventData, expectedEventTopics);

        ulong expectedGasCost = EventsEncoder.EventCost(expectedLogEntry);
        _context.GasLeft.Should().Be(_context.GasSupplied - expectedGasCost);

        _context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void MixedEvent_Always_EmitsEvent()
    {
        string eventSignature = "Mixed(bool,bool,bytes32,address,address)";
        bool flag = true; // indexed
        bool not = false;
        Hash256 value = ArbDebugParserTests.Hash256FromUlong(1); // indexed
        Address conn = ArbDebug.Address;
        Address caller = ArbDebug.Address; // indexed

        ArbDebug.EmitMixedEvent(_context, flag, not, value, conn, caller);

        byte[] flagAsTopic = new byte[32];
        flagAsTopic[31] = flag ? (byte)1 : (byte)0;

        byte[] callerAsTopic = new byte[32];
        caller.Bytes.CopyTo(callerAsTopic, 12);

        Hash256[] expectedEventTopics = [Keccak.Compute(eventSignature), new Hash256(flagAsTopic), value, new Hash256(callerAsTopic)];

        byte[] expectedEventData = new byte[64];
        expectedEventData[31] = not ? (byte)1 : (byte)0;
        conn.Bytes.CopyTo(expectedEventData, 64 - Address.Size);

        LogEntry expectedLogEntry = new(ArbDebug.Address, expectedEventData, expectedEventTopics);

        ulong expectedGasCost = EventsEncoder.EventCost(expectedLogEntry);
        _context.GasLeft.Should().Be(_context.GasSupplied - expectedGasCost);

        _context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void CustomSolidityError_Always_ReturnsError()
    {
        string errorSignature = "Custom(uint64,string,bool)";
        ulong number = 123;
        string message = "This spider family wards off bugs: /\\oo/\\ //\\(oo)//\\ /\\oo/\\";
        bool flag = true;

        ArbitrumPrecompileException result = ArbDebug.CustomSolidityError(number, message, flag);

        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        bool isMultipleOf32 = messageBytes.Length % 32 == 0;
        int numberOfWordsForMsg = messageBytes.Length / 32 + (isMultipleOf32 ? 0 : 1);

        byte[] expectedErrorData = new byte[4 + Hash256.Size * (4 + numberOfWordsForMsg)];

        int offset = 0;
        Keccak.Compute(errorSignature).Bytes[..4].CopyTo(expectedErrorData); // error signature
        offset += 4;
        new UInt256(number).ToBigEndian().CopyTo(expectedErrorData, offset); // number static data
        offset += 32;
        new UInt256(96).ToBigEndian().CopyTo(expectedErrorData, offset); // message dynamic data start in encoding (from start of data section, ie omitting error signature)
        offset += 32;
        new UInt256(flag ? 1UL : 0UL).ToBigEndian().CopyTo(expectedErrorData, offset); // flag static data
        offset += 32;
        new UInt256((ulong)messageBytes.Length).ToBigEndian().CopyTo(expectedErrorData, offset); // message dynamic data length
        offset += 32;
        messageBytes.CopyTo(expectedErrorData, offset); // message dynamic data
        offset += messageBytes.Length;
        if (!isMultipleOf32)
        {
            new byte[expectedErrorData.Length - offset].CopyTo(expectedErrorData, offset); // right padding if needed
        }

        result.Output.Should().BeEquivalentTo(expectedErrorData);
        result.Type.Should().Be(ArbitrumPrecompileException.PrecompileExceptionType.SolidityError);
    }

    [Test]
    public void OverwriteContractCode_WithExistingCode_ReturnsOldCodeAndSetsNewCode()
    {
        Address targetAddress = new("0x0000000000000000000000000000000000000456");
        byte[] originalCode = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };
        byte[] newCode = new byte[] { 0x60, 0x60, 0x60, 0x60, 0x50 };

        _worldState.CreateAccount(targetAddress, UInt256.Zero);
        _worldState.InsertCode(targetAddress, originalCode, _context.ReleaseSpec);

        byte[] returnedCode = ArbDebug.OverwriteContractCode(_context, targetAddress, newCode);

        returnedCode.Should().BeEquivalentTo(originalCode);

        byte[]? currentCode = _worldState.GetCode(targetAddress);
        currentCode.Should().BeEquivalentTo(newCode);
    }

    [Test]
    public void OverwriteContractCode_WithNoExistingCode_ReturnsEmptyAndSetsNewCode()
    {
        Address targetAddress = new("0x0000000000000000000000000000000000000789");
        byte[] newCode = new byte[] { 0x60, 0x60, 0x60, 0x60, 0x50 };

        byte[] returnedCode = ArbDebug.OverwriteContractCode(_context, targetAddress, newCode);

        returnedCode.Should().BeEmpty();

        byte[]? currentCode = _worldState.GetCode(targetAddress);
        currentCode.Should().BeEquivalentTo(newCode);
    }

    [Test]
    public void OverwriteContractCode_WithEmptyNewCode_ReturnsOldCodeAndClearsCode()
    {
        Address targetAddress = new("0x0000000000000000000000000000000000000ABC");
        byte[] originalCode = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };
        byte[] emptyCode = Array.Empty<byte>();

        _worldState.CreateAccount(targetAddress, UInt256.Zero);
        _worldState.InsertCode(targetAddress, originalCode, _context.ReleaseSpec);

        byte[] returnedCode = ArbDebug.OverwriteContractCode(_context, targetAddress, emptyCode);

        returnedCode.Should().BeEquivalentTo(originalCode);

        byte[]? currentCode = _worldState.GetCode(targetAddress);
        currentCode.Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbDebug.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("becomeChainOwner()"),
            PrecompileTestAbiHelpers.GetMethodId("events(bool,bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("eventsView()"),
            PrecompileTestAbiHelpers.GetMethodId("customRevert(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("panic()"),
            PrecompileTestAbiHelpers.GetMethodId("legacyError()"),
            PrecompileTestAbiHelpers.GetMethodId("overwriteContractCode(address,bytes)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, AbiEventDescription> allEvents = PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbDebug.Abi);

        allEvents.Keys.Should().BeEquivalentTo("Basic", "Mixed", "Store");
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedErrors()
    {
        Dictionary<string, AbiErrorDescription> allErrors = PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbDebug.Abi);

        allErrors.Keys.Should().BeEquivalentTo("Custom", "Unused");
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("becomeChainOwner()").Should().Be(Solgen.ArbDebug.Methods.BecomeChainOwner);
        PrecompileTestAbiHelpers.GetMethodId("events(bool,bytes32)").Should().Be(Solgen.ArbDebug.Methods.Events);
        PrecompileTestAbiHelpers.GetMethodId("eventsView()").Should().Be(Solgen.ArbDebug.Methods.EventsView);
        PrecompileTestAbiHelpers.GetMethodId("customRevert(uint64)").Should().Be(Solgen.ArbDebug.Methods.CustomRevert);
        PrecompileTestAbiHelpers.GetMethodId("panic()").Should().Be(Solgen.ArbDebug.Methods.Panic);
        PrecompileTestAbiHelpers.GetMethodId("legacyError()").Should().Be(Solgen.ArbDebug.Methods.LegacyError);
        PrecompileTestAbiHelpers.GetMethodId("overwriteContractCode(address,bytes)").Should().Be(Solgen.ArbDebug.Methods.OverwriteContractCode);
    }

    [Test]
    public void Panic_BelowStylusArbOSVersion_IsRejected()
    {
        // ArbDebugParser sets panic()'s ArbOSVersion to Stylus while the precompile itself stays v0,
        // so panic is method-gated (revert below) while sibling methods like becomeChainOwner stay
        // reachable at v0 — the negative control guards against accidental precompile-wide gating.

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Stylus - 1);

        bool result = ArbDebugParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId("panic()"), out bool shouldRevert, out PrecompileHandler? _);

        result.Should().BeFalse();
        shouldRevert.Should().BeTrue();
    }

    [Test]
    public void Panic_AtStylusArbOSVersion_IsDispatched()
    {
        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Stylus)
            .WithExecutingAccount(ArbDebugParser.Address);

        bool result = ArbDebugParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId("panic()"), out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }

    [Test]
    public void BecomeChainOwner_AtArbOSVersionZero_IsDispatched()
    {
        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Zero)
            .WithExecutingAccount(ArbDebugParser.Address);

        bool result = ArbDebugParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId("becomeChainOwner()"), out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }
}
