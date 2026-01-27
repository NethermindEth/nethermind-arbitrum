# Task 04: Consistent Result Pattern

> **Status**: TODO
> **Priority**: LOW - Code quality improvement, reduces exceptions

## Metadata
- **Target**: Methods using try-catch for expected errors
- **Affected Files**: Various converters and validators
- **Type**: Code Quality
- **Dependencies**: None
- **Estimated Impact**: Cleaner error handling, fewer exception allocations

## Problem Statement

Some methods use exceptions for expected error cases:

```csharp
// src/Nethermind.Arbitrum/Modules/ArbitrumRpcModule.cs:225-235
public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
{
    try
    {
        long blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, specHelper);
        return ResultWrapper<long>.Success(blockNumber);
    }
    catch (OverflowException)  // <-- Exception for expected case
    {
        return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);
    }
}
```

**Problems:**
1. Exception allocation for expected error path
2. Inconsistent error handling patterns across codebase
3. Stack trace capture overhead even when ignored
4. Harder to reason about control flow

## Solution

Use `TryX` pattern consistently:

### Step 1: Add TryX Methods to Converters

```csharp
// Math/MessageBlockConverter.cs
public static class MessageBlockConverter
{
    // Existing throwing method (keep for backward compat)
    public static long MessageIndexToBlockNumber(ulong messageIndex, IArbitrumSpecHelper specHelper)
    {
        if (!TryMessageIndexToBlockNumber(messageIndex, specHelper, out long blockNumber))
            throw new OverflowException($"Message index {messageIndex} overflows block number");
        return blockNumber;
    }

    // NEW: Non-throwing method
    public static bool TryMessageIndexToBlockNumber(
        ulong messageIndex,
        IArbitrumSpecHelper specHelper,
        out long blockNumber)
    {
        blockNumber = 0;

        ulong genesisBlockNum = specHelper.GenesisBlockNum;
        ulong sum = genesisBlockNum + messageIndex;

        // Check for overflow
        if (sum < genesisBlockNum || sum < messageIndex)
            return false;

        // Check for long overflow
        if (sum > long.MaxValue)
            return false;

        blockNumber = (long)sum;
        return true;
    }

    // Existing throwing method
    public static ulong BlockNumberToMessageIndex(ulong blockNumber, IArbitrumSpecHelper specHelper)
    {
        if (!TryBlockNumberToMessageIndex(blockNumber, specHelper, out ulong messageIndex))
            throw new ArgumentOutOfRangeException(nameof(blockNumber));
        return messageIndex;
    }

    // NEW: Non-throwing method
    public static bool TryBlockNumberToMessageIndex(
        ulong blockNumber,
        IArbitrumSpecHelper specHelper,
        out ulong messageIndex)
    {
        messageIndex = 0;
        ulong genesis = specHelper.GenesisBlockNum;

        if (blockNumber < genesis)
            return false;

        messageIndex = blockNumber - genesis;
        return true;
    }
}
```

### Step 2: Update Callers

```csharp
// ArbitrumRpcModule.cs - AFTER
public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
{
    if (!MessageBlockConverter.TryMessageIndexToBlockNumber(messageIndex, specHelper, out long blockNumber))
        return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);

    return ResultWrapper<long>.Success(blockNumber);
}

public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
{
    if (!MessageBlockConverter.TryBlockNumberToMessageIndex(blockNumber, specHelper, out ulong messageIndex))
    {
        ulong genesis = specHelper.GenesisBlockNum;
        return ResultWrapper<ulong>.Fail($"blockNumber {blockNumber} < genesis {genesis}");
    }

    return ResultWrapper<ulong>.Success(messageIndex);
}
```

### Step 3: Create Result<T> Helper (Optional)

```csharp
// Core/Result.cs
public readonly struct Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);

    // Implicit conversion to ResultWrapper for RPC methods
    public static implicit operator ResultWrapper<T>(Result<T> result)
        => result.IsSuccess
            ? ResultWrapper<T>.Success(result.Value!)
            : ResultWrapper<T>.Fail(result.Error!);

    // Pattern matching support
    public void Deconstruct(out bool isSuccess, out T? value, out string? error)
    {
        isSuccess = IsSuccess;
        value = Value;
        error = Error;
    }
}
```

## Files to Update

| File | Method | Change |
|------|--------|--------|
| `MessageBlockConverter.cs` | MessageIndexToBlockNumber | Add TryX variant |
| `MessageBlockConverter.cs` | BlockNumberToMessageIndex | Add TryX variant |
| `ArbitrumRpcModule.cs` | MessageIndexToBlockNumber | Use TryX |
| `ArbitrumRpcModule.cs` | BlockNumberToMessageIndex | Use TryX |
| `ArbitrumBlockHeaderInfo.cs` | Deserialize | Consider TryX |

## Benchmark Verification

### Micro-benchmark for Error Path

```csharp
[MemoryDiagnoser]
public class ResultPatternBenchmarks
{
    private readonly IArbitrumSpecHelper _specHelper = ...;
    private readonly ulong _overflowIndex = ulong.MaxValue;

    [Benchmark(Baseline = true)]
    public ResultWrapper<long> TryCatch_Overflow()
    {
        try
        {
            long result = MessageBlockConverter.MessageIndexToBlockNumber(_overflowIndex, _specHelper);
            return ResultWrapper<long>.Success(result);
        }
        catch (OverflowException)
        {
            return ResultWrapper<long>.Fail("Overflow");
        }
    }

    [Benchmark]
    public ResultWrapper<long> TryPattern_Overflow()
    {
        if (!MessageBlockConverter.TryMessageIndexToBlockNumber(_overflowIndex, _specHelper, out long result))
            return ResultWrapper<long>.Fail("Overflow");
        return ResultWrapper<long>.Success(result);
    }
}
```

Expected results:
```
| Method              | Mean       | Allocated |
|---------------------|------------|-----------|
| TryCatch_Overflow   | 2,500 ns   | 120 B     |
| TryPattern_Overflow | 5 ns       | 0 B       |
```

## Acceptance Criteria

- [ ] `TryMessageIndexToBlockNumber` added
- [ ] `TryBlockNumberToMessageIndex` added
- [ ] RPC methods updated to use TryX pattern
- [ ] No exception allocations for expected error paths
- [ ] All existing tests pass
- [ ] Benchmark shows improvement for error path

## Edge Cases

1. **Backward compatibility**: Keep throwing methods for existing callers
2. **Null handling**: TryX methods set `out` to default on failure
3. **Thread safety**: Stateless conversion, no issues

## Rollback Plan

TryX methods are additive - old code continues to work. If issues:

```csharp
// Revert callers to try-catch, TryX methods remain
public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
{
    try
    {
        long blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, specHelper);
        return ResultWrapper<long>.Success(blockNumber);
    }
    catch (OverflowException)
    {
        return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);
    }
}
```
