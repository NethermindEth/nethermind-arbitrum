using Nethermind.Arbitrum.Precompiles.Parser;
using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Abi;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Logging;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Evm;
using Nethermind.Arbitrum.Precompiles.Exceptions;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbDebugParserTests
{
    private const ulong DefaultGasSupplied = 1_000_000;
    private PrecompileTestContextBuilder _context = null!;
    private IDisposable _worldStateScope = null!;
    private IWorldState _worldState = null!;
    private ArbosState _freeArbosState = null!;

    private static readonly uint _becomeChainOwnerId = PrecompileHelper.GetMethodId("becomeChainOwner()");
    private static readonly uint _eventsId = PrecompileHelper.GetMethodId("events(bool,bytes32)");
    private static readonly uint _eventsViewId = PrecompileHelper.GetMethodId("eventsView()");
    private static readonly uint _customRevertId = PrecompileHelper.GetMethodId("customRevert(uint64)");
    private static readonly uint _panicId = PrecompileHelper.GetMethodId("panic()");
    private static readonly uint _legacyErrorId = PrecompileHelper.GetMethodId("legacyError()");

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis); // Store the scope

        _ = ArbOSInitialization.Create(_worldState);

        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied).WithArbosState();
        _context.ResetGasLeft();

        _freeArbosState = ArbosState.OpenArbosState(_worldState, new ZeroGasBurner(), LimboLogs.Instance.GetClassLogger());
    }

    [TearDown]
    public void TearDown()
    {
        _worldStateScope?.Dispose();
    }

    [Test]
    public void BecomeChainOwner_Always_AddsSenderAsChainOwner()
    {
        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_becomeChainOwnerId, out PrecompileHandler? becomeChainOwner);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_becomeChainOwnerId].AbiFunctionDescription;

        byte[] result = becomeChainOwner!(_context, []);

        result.Should().BeEmpty();
        _context.FreeArbosState.ChainOwners.IsMember(_context.Caller).Should().BeTrue();
    }

    [Test]
    public void Events_Always_EmitsEvents()
    {
        Address sender = new("0x0000000000000000000000000000000000000123");
        UInt256 paid = 1; // value sent to precompile
        _context = _context.WithCaller(sender).WithValue(paid);

        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_eventsId, out PrecompileHandler? events);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_eventsId].AbiFunctionDescription;

        bool flag = true;
        Hash256 value = Hash256FromUlong(2);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            flag, value
        );

        byte[] result = events!(_context, calldata);

        byte[] expected = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            sender, paid
        );

        result.Should().BeEquivalentTo(expected);

        LogEntry basicEventLog = EventsEncoder.BuildLogEntryFromEvent(ArbDebug.Basic, ArbDebug.Address, !flag, value);
        LogEntry mixedEventLog = EventsEncoder.BuildLogEntryFromEvent(ArbDebug.Mixed, ArbDebug.Address, flag, !flag, value, ArbDebug.Address, sender);
        _context.EventLogs.Should().BeEquivalentTo(new[] { basicEventLog, mixedEventLog });
    }

    [Test]
    public void EventsView_Always_ThrowsAsViewFunction()
    {
        ArbitrumPrecompileExecutionContext context = new(Address.Zero, UInt256.Zero, DefaultGasSupplied, _worldState, new BlockExecutionContext(), 0, null)
        {
            ArbosState = _freeArbosState,
            ReadOnly = true,
        };

        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_eventsViewId, out PrecompileHandler? eventsView);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_eventsViewId].AbiFunctionDescription;

        Action action = () => eventsView!(context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException(EvmExceptionExtensions.GetEvmExceptionDescription(EvmExceptionType.StaticCallViolation)!);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CustomRevert_Always_ThrowsSolidityError()
    {
        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_customRevertId, out PrecompileHandler? customRevert);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_customRevertId].AbiFunctionDescription;

        ulong number = 1;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            number
        );

        Action action = () => customRevert!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbDebug.CustomSolidityError(number, "This spider family wards off bugs: /\\oo/\\ //\\(oo)//\\ /\\oo/\\", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Panic_Always_ThrowsFailureException()
    {
        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_panicId, out PrecompileHandler? panic);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_panicId].AbiFunctionDescription;

        Action action = () => panic!(_context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("called ArbDebug's debug-only Panic method");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void LegacyError_Always_ThrowsFailureException()
    {
        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_legacyErrorId, out PrecompileHandler? legacyError);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_legacyErrorId].AbiFunctionDescription;

        Action action = () => legacyError!(_context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("example legacy error");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    public static Hash256 Hash256FromUlong(ulong value) => new(new UInt256(value).ToBigEndian());
}
