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
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;

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
    private static readonly uint _overwriteContractCodeId = PrecompileHelper.GetMethodId("overwriteContractCode(address,bytes)");

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

    [Test]
    public void OverwriteContractCode_WithValidInput_ReturnsOldCodeAndSetsNewCode()
    {
        Address targetAddress = new("0x0000000000000000000000000000000000000456");
        byte[] originalCode = [0x60, 0x80, 0x60, 0x40, 0x52];
        byte[] newCode = [0x60, 0x60, 0x60, 0x60, 0x50];

        _worldState.CreateAccount(targetAddress, UInt256.Zero);
        _worldState.InsertCode(targetAddress, originalCode, _context.ReleaseSpec);

        bool exists = ArbDebugParser.PrecompileImplementation.TryGetValue(_overwriteContractCodeId, out PrecompileHandler? overwriteContractCode);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbDebugParser.PrecompileFunctionDescription[_overwriteContractCodeId].AbiFunctionDescription;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            targetAddress, newCode
        );

        byte[] result = overwriteContractCode!(_context, calldata);

        byte[] expected = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            originalCode
        );

        result.Should().BeEquivalentTo(expected);

        byte[]? currentCode = _worldState.GetCode(targetAddress);
        currentCode.Should().BeEquivalentTo(newCode);
    }

    [Test]
    public void CallingDebugPrecompile_OverwriteContractCode_ReturnsOldCodeAndSetsNewCode()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, chain.SpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.MainWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Create a target contract with some initial code
        Address targetContract = new("0x0000000000000000000000000000000000000456");
        byte[] originalCode = [0x60, 0x80, 0x60, 0x40, 0x52]; // Some bytecode
        UInt256 contractBalance = 1.Ether();
        UInt256 contractNonce = 5;

        worldState.CreateAccount(targetContract, contractBalance, contractNonce);
        worldState.InsertCode(targetContract, originalCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        // Prepare new code to overwrite with
        byte[] newCode = [0x60, 0x60, 0x60, 0x60, 0x50];

        // Call overwriteContractCode(address,bytes) on ArbDebug
        uint overwriteContractCodeMethodId = PrecompileHelper.GetMethodId("overwriteContractCode(address,bytes)");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbDebugParser.PrecompileFunctionDescription[overwriteContractCodeMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            [targetContract, newCode]
        );

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbDebugAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(1_000_000)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None);

        // Verify the returned value is the old code
        byte[] expectedReturnValue = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbDebugParser.PrecompileFunctionDescription[overwriteContractCodeMethodId].AbiFunctionDescription.GetReturnInfo().Signature,
            originalCode
        );
        tracer.ReturnValue.Should().BeEquivalentTo(expectedReturnValue);

        // Verify the new code is set on the contract
        byte[]? currentCode = worldState.GetCode(targetContract);
        currentCode.Should().BeEquivalentTo(newCode);

        // Verify the code hash changed
        ValueHash256 newCodeHash = worldState.GetCodeHash(targetContract);
        newCodeHash.Should().Be(Keccak.Compute(newCode));

        // Verify other account data is preserved
        worldState.GetBalance(targetContract).Should().Be(contractBalance);
        worldState.GetNonce(targetContract).Should().Be(contractNonce);
        worldState.AccountExists(targetContract).Should().BeTrue();

        // Verify exact gas consumed (observed from test execution)
        tracer.GasSpent.Should().Be(22505);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - 22505 * baseFeePerGas);
    }

    [Test]
    public void CallingDebugPrecompile_OverwriteContractCode_WithNonExistentAccount_CreatesAccountAndSetsCode()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, chain.SpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.MainWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Target a non-existent contract
        Address targetContract = new("0x0000000000000000000000000000000000000789");
        worldState.AccountExists(targetContract).Should().BeFalse();

        byte[] newCode = [0x60, 0x60, 0x60, 0x60, 0x50];

        // Call overwriteContractCode(address,bytes) on ArbDebug
        uint overwriteContractCodeMethodId = PrecompileHelper.GetMethodId("overwriteContractCode(address,bytes)");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbDebugParser.PrecompileFunctionDescription[overwriteContractCodeMethodId].AbiFunctionDescription.GetCallInfo().Signature, targetContract, newCode);

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbDebugAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(1_000_000)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None);

        // Verify empty array is returned (no previous code)
        byte[] expectedReturnValue = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbDebugParser.PrecompileFunctionDescription[overwriteContractCodeMethodId].AbiFunctionDescription.GetReturnInfo().Signature,
            Array.Empty<byte>()
        );
        tracer.ReturnValue.Should().BeEquivalentTo(expectedReturnValue);

        // Verify account was created with the new code
        worldState.AccountExists(targetContract).Should().BeTrue();
        worldState.GetCode(targetContract).Should().BeEquivalentTo(newCode);
        worldState.GetBalance(targetContract).Should().Be(UInt256.Zero);
        worldState.GetNonce(targetContract).Should().Be(UInt256.Zero);
    }

    public static Hash256 Hash256FromUlong(ulong value) => new(new UInt256(value).ToBigEndian());
}
