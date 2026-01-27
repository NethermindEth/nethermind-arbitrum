# Task 01: Span-Based ABI Decoder

> **Status**: TODO
> **Priority**: HIGH - Eliminates allocations in precompile hot path

## Metadata
- **Target**: `src/Nethermind.Arbitrum/Precompiles/Abi/SpanAbiDecoder.cs` (new)
- **Affected Files**: All `*Parser.cs` files in Precompiles/Parser/
- **Type**: Performance Optimization
- **Dependencies**: Task 00 (benchmarks)
- **Estimated Impact**: ~32-64 bytes saved per precompile call

## Problem Statement

Current precompile ABI decoding allocates on every call:

```csharp
// src/Nethermind.Arbitrum/Precompiles/Parser/ArbInfoParser.cs:31-37
private static byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
{
    object[] decoded = PrecompileAbiEncoder.Instance.Decode(
        AbiEncodingStyle.None,
        PrecompileFunctionDescription[_getBalanceId].AbiFunctionDescription.GetCallInfo().Signature,
        inputData.ToArray()  // <-- ALLOCATION: copies entire input
    );
    Address account = (Address)decoded[0];  // <-- ALLOCATION: boxed in object[]
    // ...
}
```

**Allocations per call:**
- `inputData.ToArray()`: 36+ bytes (20 for address + ABI padding)
- `object[]`: 24+ bytes for array + boxing overhead
- Total: ~64 bytes per precompile call

## Solution

Create zero-allocation span-based decoder that reads ABI-encoded data directly:

```csharp
// src/Nethermind.Arbitrum/Precompiles/Abi/SpanAbiDecoder.cs
public static class SpanAbiDecoder
{
    /// <summary>
    /// Decode ABI-encoded address (32 bytes, right-aligned in last 20 bytes)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Address DecodeAddress(ReadOnlySpan<byte> input)
    {
        // ABI: address is 32 bytes, with 12 zero-padding bytes + 20 address bytes
        if (input.Length < 32)
            ThrowInsufficientData();
        return new Address(input.Slice(12, 20));
    }

    /// <summary>
    /// Decode ABI-encoded address, advancing the span
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Address DecodeAddress(ref ReadOnlySpan<byte> input)
    {
        Address result = DecodeAddress(input);
        input = input.Slice(32);
        return result;
    }

    /// <summary>
    /// Decode ABI-encoded uint256
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt256 DecodeUInt256(ReadOnlySpan<byte> input)
    {
        if (input.Length < 32)
            ThrowInsufficientData();
        return new UInt256(input.Slice(0, 32), isBigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt256 DecodeUInt256(ref ReadOnlySpan<byte> input)
    {
        UInt256 result = DecodeUInt256(input);
        input = input.Slice(32);
        return result;
    }

    /// <summary>
    /// Decode ABI-encoded uint64
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong DecodeUInt64(ReadOnlySpan<byte> input)
    {
        if (input.Length < 32)
            ThrowInsufficientData();
        // uint64 is right-aligned in 32 bytes
        return BinaryPrimitives.ReadUInt64BigEndian(input.Slice(24, 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong DecodeUInt64(ref ReadOnlySpan<byte> input)
    {
        ulong result = DecodeUInt64(input);
        input = input.Slice(32);
        return result;
    }

    /// <summary>
    /// Decode ABI-encoded bool (uint8 in 32 bytes)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DecodeBool(ReadOnlySpan<byte> input)
    {
        if (input.Length < 32)
            ThrowInsufficientData();
        return input[31] != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DecodeBool(ref ReadOnlySpan<byte> input)
    {
        bool result = DecodeBool(input);
        input = input.Slice(32);
        return result;
    }

    /// <summary>
    /// Decode ABI-encoded dynamic bytes
    /// </summary>
    public static ReadOnlySpan<byte> DecodeBytes(ReadOnlySpan<byte> input, int offset)
    {
        // Dynamic bytes: offset points to (length, data)
        ReadOnlySpan<byte> dataSection = input.Slice(offset);
        int length = (int)DecodeUInt64(dataSection);
        return dataSection.Slice(32, length);
    }

    [DoesNotReturn]
    private static void ThrowInsufficientData()
    {
        throw new AbiDecodingException("Insufficient data for ABI decoding");
    }
}
```

## Migration Pattern

### Before (Current)

```csharp
private static byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
{
    object[] decoded = PrecompileAbiEncoder.Instance.Decode(
        AbiEncodingStyle.None,
        PrecompileFunctionDescription[_getBalanceId].AbiFunctionDescription.GetCallInfo().Signature,
        inputData.ToArray()
    );
    Address account = (Address)decoded[0];
    return ArbInfo.GetBalance(context, account).ToBigEndian();
}
```

### After (Optimized)

```csharp
private static byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
{
    Address account = SpanAbiDecoder.DecodeAddress(inputData);
    return ArbInfo.GetBalance(context, account).ToBigEndian();
}
```

## Files to Update

| File | Methods | Complexity |
|------|---------|------------|
| `ArbInfoParser.cs` | GetBalance, GetCode | Simple |
| `ArbSysParser.cs` | Multiple | Medium |
| `ArbGasInfoParser.cs` | Multiple | Medium |
| `ArbOwnerParser.cs` | Multiple | Medium |
| `ArbRetryableTxParser.cs` | Multiple | Complex (dynamic bytes) |
| `ArbAddressTableParser.cs` | Multiple | Medium |
| `ArbAggregatorParser.cs` | Multiple | Simple |
| `ArbWasmParser.cs` | Multiple | Complex |

## Benchmark Verification

### Before Task

```bash
./scripts/run-benchmarks.sh --filter "*AbiDecoding*"
```

Expected baseline:
```
| Method                | Mean     | Allocated |
|-----------------------|----------|-----------|
| GetBalance_Current    | 125 ns   | 64 B      |
| GetCode_Current       | 180 ns   | 96 B      |
```

### After Task

```bash
./scripts/run-benchmarks.sh --filter "*AbiDecoding*"
```

Expected improvement:
```
| Method                | Mean     | Allocated |
|-----------------------|----------|-----------|
| GetBalance_Span       | 45 ns    | 0 B       |
| GetCode_Span          | 85 ns    | 0 B       |
```

## Acceptance Criteria

- [ ] `SpanAbiDecoder` class created with all common types
- [ ] At least 3 parsers migrated (ArbInfo, ArbSys, ArbGasInfo)
- [ ] Benchmarks show 0 allocations for migrated methods
- [ ] All existing tests pass
- [ ] No new warnings
- [ ] Benchmark comparison saved in PR description

## Rollback Plan

If issues arise:
1. Keep old methods as `*_Legacy` suffix
2. New span methods call through for debugging
3. Feature flag to switch between implementations (if needed)

## Canonical Reference

Similar pattern used in:
- `Nethermind.Core/Extensions/SpanExtensions.cs`
- `Nethermind.Serialization.Rlp/RlpStream.cs`
