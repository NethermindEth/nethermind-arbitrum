# Task 02: Interface-Based Precompile Dispatch

> **Status**: TODO
> **Priority**: MEDIUM - Removes runtime type checks in precompile dispatch

## Metadata
- **Target**: `src/Nethermind.Arbitrum/Precompiles/PrecompileHelper.cs`
- **Affected Files**: All `*Parser.cs` files, `IArbitrumPrecompile.cs`
- **Type**: Performance + Architecture
- **Dependencies**: Task 00 (benchmarks)
- **Estimated Impact**: Eliminates runtime `is` type checks for 15+ precompiles

## Problem Statement

Current dispatch uses runtime type checking via switch expression:

```csharp
// src/Nethermind.Arbitrum/Precompiles/PrecompileHelper.cs:22-42
public static bool TryCheckMethodVisibility(IArbitrumPrecompile precompile, ...)
    => precompile switch
    {
        _ when precompile is ArbInfoParser _ => CheckMethodVisibility<ArbInfoParser>(...),
        _ when precompile is ArbRetryableTxParser _ => CheckMethodVisibility<ArbRetryableTxParser>(...),
        _ when precompile is ArbOwnerParser _ => CheckMethodVisibility<ArbOwnerParser>(...),
        // ... 12+ more cases
        _ => throw new ArgumentException($"CheckMethodVisibility is not registered for precompile: {precompile.GetType()}")
    };
```

**Problems:**
1. O(n) type checks in worst case (last precompile requires all checks)
2. Runtime reflection overhead for `is` operator
3. Maintenance burden: new precompiles require manual registration
4. Throws exception instead of returning false for unknown precompiles

## Solution

Move dispatch logic into the interface itself:

### Option A: Instance Method Dispatch (Recommended)

```csharp
// IArbitrumPrecompile.cs
public interface IArbitrumPrecompile
{
    static abstract Address Address { get; }
    static abstract ulong AvailableFromArbosVersion { get; }

    // NEW: Instance method for dispatch
    bool TryCheckMethodVisibility(
        ArbitrumPrecompileExecutionContext context,
        ILogger logger,
        ref ReadOnlySpan<byte> calldata,
        out bool shouldRevert,
        out PrecompileHandler? methodToExecute);
}

// IArbitrumPrecompile<TSelf>.cs - Keep existing static abstract pattern
public interface IArbitrumPrecompile<TSelf> : IArbitrumPrecompile
    where TSelf : IArbitrumPrecompile<TSelf>
{
    static abstract IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
    static abstract FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    // Default implementation using static members
    bool IArbitrumPrecompile.TryCheckMethodVisibility(
        ArbitrumPrecompileExecutionContext context,
        ILogger logger,
        ref ReadOnlySpan<byte> calldata,
        out bool shouldRevert,
        out PrecompileHandler? methodToExecute)
    {
        return PrecompileHelper.CheckMethodVisibility<TSelf>(
            context, logger, ref calldata, out shouldRevert, out methodToExecute);
    }
}
```

### Option B: Dictionary-Based Dispatch (Alternative)

```csharp
// PrecompileRegistry.cs
public static class PrecompileRegistry
{
    private static readonly FrozenDictionary<Address, Func<ArbitrumPrecompileExecutionContext, ILogger, ReadOnlySpan<byte>, (bool, bool, PrecompileHandler?)>> _dispatchers;

    static PrecompileRegistry()
    {
        Dictionary<Address, Func<...>> dispatchers = new()
        {
            [ArbInfo.Address] = (ctx, log, data) => CheckMethodVisibility<ArbInfoParser>(ctx, log, data),
            [ArbSys.Address] = (ctx, log, data) => CheckMethodVisibility<ArbSysParser>(ctx, log, data),
            // Auto-generated or reflection-based at startup
        };
        _dispatchers = dispatchers.ToFrozenDictionary();
    }

    public static bool TryDispatch(Address address, ArbitrumPrecompileExecutionContext context, ...)
    {
        return _dispatchers.TryGetValue(address, out var dispatcher)
            && dispatcher(context, logger, calldata).Item1;
    }
}
```

## Implementation Steps

### Step 1: Add instance method to interface

```csharp
// Update IArbitrumPrecompile.cs
public interface IArbitrumPrecompile
{
    static abstract Address Address { get; }
    static abstract ulong AvailableFromArbosVersion { get; }

    bool TryCheckMethodVisibility(
        ArbitrumPrecompileExecutionContext context,
        ILogger logger,
        ref ReadOnlySpan<byte> calldata,
        out bool shouldRevert,
        [NotNullWhen(true)] out PrecompileHandler? methodToExecute);
}
```

### Step 2: Update IArbitrumPrecompile<TSelf> with default implementation

```csharp
public interface IArbitrumPrecompile<TSelf> : IArbitrumPrecompile
    where TSelf : IArbitrumPrecompile<TSelf>
{
    static abstract IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
    static abstract FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    // Default interface implementation
    bool IArbitrumPrecompile.TryCheckMethodVisibility(
        ArbitrumPrecompileExecutionContext context,
        ILogger logger,
        ref ReadOnlySpan<byte> calldata,
        out bool shouldRevert,
        [NotNullWhen(true)] out PrecompileHandler? methodToExecute)
    {
        return PrecompileHelper.CheckMethodVisibility<TSelf>(
            context, logger, ref calldata, out shouldRevert, out methodToExecute);
    }
}
```

### Step 3: Simplify PrecompileHelper

```csharp
// PrecompileHelper.cs - AFTER
public static class PrecompileHelper
{
    // Remove the switch expression entirely
    // CheckMethodVisibility<T> remains for the actual logic

    public static bool CheckMethodVisibility<T>(...) where T : IArbitrumPrecompile<T>
    {
        // Existing logic unchanged
    }
}
```

### Step 4: Update call sites

```csharp
// Before
if (PrecompileHelper.TryCheckMethodVisibility(precompile, context, logger, ref calldata, out shouldRevert, out handler))

// After
if (precompile.TryCheckMethodVisibility(context, logger, ref calldata, out shouldRevert, out handler))
```

## Benchmark Verification

### Before Task

```bash
./scripts/run-benchmarks.sh --filter "*PrecompileDispatch*"
```

Expected baseline:
```
| Method                     | Mean     | Allocated |
|----------------------------|----------|-----------|
| Dispatch_ArbInfo (best)    | 15 ns    | 0 B       |
| Dispatch_ArbBls (worst)    | 180 ns   | 0 B       |
| Dispatch_AllPrecompiles    | 1.2 μs   | 0 B       |
```

### After Task

```bash
./scripts/run-benchmarks.sh --filter "*PrecompileDispatch*"
```

Expected improvement:
```
| Method                     | Mean     | Allocated |
|----------------------------|----------|-----------|
| Dispatch_ArbInfo           | 12 ns    | 0 B       |
| Dispatch_ArbBls            | 12 ns    | 0 B       |
| Dispatch_AllPrecompiles    | 180 ns   | 0 B       |
```

## Files to Update

| File | Changes |
|------|---------|
| `IArbitrumPrecompile.cs` | Add instance method |
| `IArbitrumPrecompile<T>.cs` | Add default implementation |
| `PrecompileHelper.cs` | Remove switch expression |
| All `*Parser.cs` (15 files) | No changes needed (interface default handles it) |

## Acceptance Criteria

- [ ] Interface updated with instance method
- [ ] Default implementation in generic interface
- [ ] Switch expression in PrecompileHelper removed
- [ ] Benchmarks show consistent O(1) dispatch time
- [ ] All existing tests pass
- [ ] No new warnings

## Edge Cases

1. **Unknown precompile**: Return false instead of throwing
2. **Null precompile**: Handle at call site (precondition check)
3. **Future precompiles**: Just implement interface, no registration needed

## Rollback Plan

Keep the switch expression as fallback:

```csharp
// If interface method fails for some reason
public static bool TryCheckMethodVisibility(IArbitrumPrecompile precompile, ...)
{
    // Try interface dispatch first
    if (precompile.TryCheckMethodVisibility(context, logger, ref calldata, out shouldRevert, out handler))
        return true;

    // Fallback to switch (should never reach here)
    return LegacySwitch(precompile, context, logger, ref calldata, out shouldRevert, out handler);
}
```
