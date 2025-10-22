using Nethermind.Core;
using Nethermind.Arbitrum.Precompiles.Parser;
using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Data.Transactions.ArbitrumBinaryWriter;
using Nethermind.Logging;
using Nethermind.Core.Crypto;
using System.Buffers.Binary;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Evm;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbWasmCacheParserTests
{
    private const ulong DefaultGasSupplied = 1_000_000;

    private PrecompileTestContextBuilder _context = null!;
    private IDisposable _worldStateScope = null!;
    private IWorldState _worldState = null!;
    private Block _genesis = null!;
    private ArbosState _freeArbosState = null!;

    private static readonly uint _isCacheManagerId = PrecompileHelper.GetMethodId("isCacheManager(address)");
    private static readonly uint _allCacheManagersId = PrecompileHelper.GetMethodId("allCacheManagers()");
    private static readonly uint _cacheCodehashId = PrecompileHelper.GetMethodId("cacheCodehash(bytes32)");
    private static readonly uint _cacheProgramId = PrecompileHelper.GetMethodId("cacheProgram(address)");
    private static readonly uint _evictProgramId = PrecompileHelper.GetMethodId("evictCodehash(bytes32)");
    private static readonly uint _codehashIsCachedId = PrecompileHelper.GetMethodId("codehashIsCached(bytes32)");

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis); // Store the scope

        _genesis = ArbOSInitialization.Create(_worldState);

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
    [TestCase(true)]
    [TestCase(false)]
    public void IsCacheManager_Always_ReturnsResult(bool setAsCacheManager)
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_isCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbWasmCacheParser.PrecompileFunctionDescription[_isCacheManagerId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");

        if (setAsCacheManager)
            _freeArbosState.Programs.CacheManagersStorage.Add(account);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            account
        );

        byte[] result = implementation!(_context, calldata);

        UInt256 expectedResult = setAsCacheManager ? UInt256.One : UInt256.Zero;
        result.Should().BeEquivalentTo(expectedResult.ToBigEndian());
    }

    [Test]
    public void CacheCodehash_ValidCodehash_CachesProgram()
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_cacheCodehashId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription functionAbi = ArbWasmCacheParser.PrecompileFunctionDescription[_cacheCodehashId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");
        _freeArbosState.Programs.CacheManagersStorage.Add(account);

        // Insert DeployCounter contract into world state as well as arbos state
        (_, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(_worldState);
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(_worldState, repository);

        StylusParams stylusParams = _freeArbosState.Programs.GetParams();
        ulong programAgeSeconds = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays) - 1; // make program not expired
        ushort programVersion = stylusParams.StylusVersion;
        bool programCached = false;
        Program program = new(programVersion, 0, 0, 0, 0, 0, programAgeSeconds, programCached);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        SetProgram(_freeArbosState, codeHash, program);
        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: false);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            codeHash.ToCommitment()
        );

        _context.WithCaller(account).WithBlockExecutionContext(_genesis.Header);
        byte[] result = implementation!(_context, calldata);

        result.Should().BeEmpty();

        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: true);

        LogEntry basicEventLog = EventsEncoder.BuildLogEntryFromEvent(ArbWasmCache.UpdateProgramCache, ArbWasmCache.Address, account, codeHash.ToCommitment(), true);
        _context.EventLogs.Should().BeEquivalentTo([basicEventLog]);
    }

    [Test]
    public void CacheProgram_ValidAddress_CachesProgram()
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_cacheProgramId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription functionAbi = ArbWasmCacheParser.PrecompileFunctionDescription[_cacheProgramId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");
        _freeArbosState.Programs.CacheManagersStorage.Add(account);

        // Insert DeployCounter contract into world state as well as arbos state
        (_, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(_worldState);
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(_worldState, repository);

        StylusParams stylusParams = _freeArbosState.Programs.GetParams();
        ulong programAgeSeconds = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays) - 1; // make program not expired
        ushort programVersion = stylusParams.StylusVersion;
        bool programCached = false;
        Program program = new(programVersion, 0, 0, 0, 0, 0, programAgeSeconds, programCached);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        SetProgram(_freeArbosState, codeHash, program);
        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: false);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            contract
        );

        _context.WithCaller(account).WithBlockExecutionContext(_genesis.Header);
        byte[] result = implementation!(_context, calldata);

        result.Should().BeEmpty();

        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: true);

        LogEntry basicEventLog = EventsEncoder.BuildLogEntryFromEvent(ArbWasmCache.UpdateProgramCache, ArbWasmCache.Address, account, codeHash.ToCommitment(), true);
        _context.EventLogs.Should().BeEquivalentTo([basicEventLog]);
    }

    [Test]
    public void CacheProgram_ProgramVersionDifferentFromStylusVersion_ThrowsProgramNeedsUpgradeSolidityError()
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_cacheProgramId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription functionAbi = ArbWasmCacheParser.PrecompileFunctionDescription[_cacheProgramId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");
        _freeArbosState.Programs.CacheManagersStorage.Add(account);

        // Insert DeployCounter contract into world state as well as arbos state
        (_, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(_worldState);
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(_worldState, repository);

        StylusParams stylusParams = _freeArbosState.Programs.GetParams();
        ushort programVersion = (ushort)(stylusParams.StylusVersion + 1); // make program version different from stylus version
        Program program = new(programVersion, 0, 0, 0, 0, 0, 0, false);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        SetProgram(_freeArbosState, codeHash, program);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            contract
        );

        _context.WithCaller(account).WithBlockExecutionContext(_genesis.Header);
        Action action = () => implementation!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbWasm.ProgramNeedsUpgradeError(programVersion, stylusParams.StylusVersion);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CacheProgram_ProgramIsExpired_ThrowsProgramExpiredSolidityError()
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_cacheProgramId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription functionAbi = ArbWasmCacheParser.PrecompileFunctionDescription[_cacheProgramId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");
        _freeArbosState.Programs.CacheManagersStorage.Add(account);

        // Insert DeployCounter contract into world state as well as arbos state
        (_, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(_worldState);
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(_worldState, repository);

        StylusParams stylusParams = _freeArbosState.Programs.GetParams();
        ushort programVersion = stylusParams.StylusVersion;
        Program program = new(programVersion, 0, 0, 0, ActivatedAtHours: 0, 0, 0, false);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        SetProgram(_freeArbosState, codeHash, program);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            contract
        );

        // Make block header timestamp bigger than the program's expiry time
        // That time is relative to the program's `ActivatedAtHours` field, currently set to 0
        ulong programAgeSeconds = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays) + 1;
        _genesis.Header.Timestamp = ArbitrumTime.StartTime + programAgeSeconds;
        _context.WithCaller(account).WithBlockExecutionContext(_genesis.Header);
        Action action = () => implementation!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbWasm.ProgramExpiredError(programAgeSeconds);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CacheProgram_ProgramIsAlreadyCached_ReturnsSuccessWithNoEvent()
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_cacheProgramId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription functionAbi = ArbWasmCacheParser.PrecompileFunctionDescription[_cacheProgramId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");
        _freeArbosState.Programs.CacheManagersStorage.Add(account);

        // Insert DeployCounter contract into world state as well as arbos state
        (_, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(_worldState);
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(_worldState, repository);

        StylusParams stylusParams = _freeArbosState.Programs.GetParams();
        ulong programAgeSeconds = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays) - 1; // make program not expired
        ushort programVersion = stylusParams.StylusVersion;
        bool programCached = true;
        Program program = new(programVersion, 0, 0, 0, 0, 0, programAgeSeconds, programCached);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        SetProgram(_freeArbosState, codeHash, program);
        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: true);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            contract
        );

        _context.WithCaller(account).WithBlockExecutionContext(_genesis.Header);
        byte[] result = implementation!(_context, calldata);

        result.Should().BeEmpty();
        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: true);
        _context.EventLogs.Should().BeEmpty();
    }

    [Test]
    public void EvictProgram_ValidCodehash_EvictsProgramFromCache()
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_evictProgramId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription functionAbi = ArbWasmCacheParser.PrecompileFunctionDescription[_evictProgramId].AbiFunctionDescription;

        Address account = new("0x0000000000000000000000000000000000000123");
        _freeArbosState.Programs.CacheManagersStorage.Add(account);

        // Insert DeployCounter contract into world state as well as arbos state
        (_, ICodeInfoRepository repository) = DeployTestsContract.CreateTestPrograms(_worldState);
        (_, Address contract, _) = DeployTestsContract.DeployCounterContract(_worldState, repository);

        StylusParams stylusParams = _freeArbosState.Programs.GetParams();
        ulong programAgeSeconds = ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays) - 1; // make program not expired
        ushort programVersion = stylusParams.StylusVersion;
        bool programCached = true; // make it cached from the start
        Program program = new(programVersion, 0, 0, 0, 0, 0, programAgeSeconds, programCached);

        ValueHash256 codeHash = _worldState.GetCodeHash(contract);
        SetProgram(_freeArbosState, codeHash, program);
        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: true);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            codeHash.ToCommitment()
        );

        _context.WithCaller(account).WithBlockExecutionContext(_genesis.Header);
        byte[] result = implementation!(_context, calldata);

        result.Should().BeEmpty();

        AssertProgramCached(_freeArbosState, codeHash, shouldBeCached: false);

        LogEntry basicEventLog = EventsEncoder.BuildLogEntryFromEvent(ArbWasmCache.UpdateProgramCache, ArbWasmCache.Address, account, codeHash.ToCommitment(), false);
        _context.EventLogs.Should().BeEquivalentTo([basicEventLog]);
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void CodehashIsCached_Always_ReturnsResult(bool shouldBeCached)
    {
        bool exists = ArbWasmCacheParser.PrecompileImplementation.TryGetValue(_codehashIsCachedId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbWasmCacheParser.PrecompileFunctionDescription[_codehashIsCachedId].AbiFunctionDescription;

        Program program = new(0, 0, 0, 0, 0, 0, 0, Cached: shouldBeCached);
        Hash256 testCodeHash = new("0xabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd");
        SetProgram(_freeArbosState, testCodeHash, program);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            testCodeHash
        );

        byte[] result = implementation!(_context, calldata);

        UInt256 expectedResult = shouldBeCached ? UInt256.One : UInt256.Zero;
        result.Should().BeEquivalentTo(expectedResult.ToBigEndian());
    }

    private static void AssertProgramCached(ArbosState arbosState, ValueHash256 codeHash, bool shouldBeCached)
    {
        ValueHash256 programData = arbosState.Programs.ProgramsStorage.Get(codeHash);
        Program resultingProgram = GetProgramFromData(programData);

        resultingProgram.Cached.Should().Be(shouldBeCached);
    }

    private static Program GetProgramFromData(ValueHash256 programData, ulong timestamp = 0)
    {
        ReadOnlySpan<byte> data = programData.Bytes;

        ushort version = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort initCost = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort cachedCost = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        ushort footprint = ArbitrumBinaryReader.ReadUShortOrFail(ref data);
        uint activatedAtHours = ArbitrumBinaryReader.ReadUIntFrom24OrFail(ref data);
        uint asmEstimateKb = ArbitrumBinaryReader.ReadUIntFrom24OrFail(ref data);
        bool cached = ArbitrumBinaryReader.ReadBoolOrFail(ref data);

        ulong ageSeconds = ArbitrumTime.HoursToAgeSeconds(timestamp, activatedAtHours);

        return new Program(version, initCost, cachedCost, footprint, activatedAtHours, asmEstimateKb, ageSeconds, cached);
    }

    private static void SetProgram(ArbosState arbosState, in ValueHash256 codeHash, Program program)
    {
        Span<byte> data = stackalloc byte[32];

        BinaryPrimitives.WriteUInt16BigEndian(data, program.Version);
        BinaryPrimitives.WriteUInt16BigEndian(data[2..], program.InitCost);
        BinaryPrimitives.WriteUInt16BigEndian(data[4..], program.CachedCost);
        BinaryPrimitives.WriteUInt16BigEndian(data[6..], program.Footprint);
        WriteUInt24BigEndian(data[8..], program.ActivatedAtHours);
        WriteUInt24BigEndian(data[11..], program.AsmEstimateKb);
        WriteBool(data[14..], program.Cached);

        arbosState.Programs.ProgramsStorage.Set(codeHash, new ValueHash256(data));
    }

    private record Program(
       ushort Version,
       ushort InitCost,
       ushort CachedCost,
       ushort Footprint,
       uint ActivatedAtHours,
       uint AsmEstimateKb,
       ulong AgeSeconds,
       bool Cached);
}
