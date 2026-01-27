# Task 05: Source-Generated JSON Serialization

> **Status**: TODO
> **Priority**: LOW - Startup time and allocation improvement

## Metadata
- **Target**: New file `src/Nethermind.Arbitrum/Serialization/ArbitrumJsonContext.cs`
- **Affected Files**: DTOs in `Data/`, `Config/`
- **Type**: Performance Optimization
- **Dependencies**: None
- **Estimated Impact**: Faster JSON serialization, reduced allocations

## Problem Statement

Current JSON serialization uses reflection-based System.Text.Json:

```csharp
// src/Nethermind.Arbitrum/Modules/ArbitrumRpcModule.cs:575-579
private bool TryDeserializeChainConfig(ReadOnlySpan<byte> bytes, out ChainConfig? chainConfig)
{
    try
    {
        chainConfig = JsonSerializer.Deserialize<ChainConfig>(bytes);  // Reflection-based
        return chainConfig != null;
    }
    // ...
}
```

**Problems:**
1. Reflection overhead on every serialization
2. Slower startup (first serialization triggers JIT)
3. More allocations for reflection metadata
4. No compile-time validation of serializable types

## Solution

Use .NET Source Generators for compile-time serialization:

### Step 1: Create JSON Source Generation Context

```csharp
// src/Nethermind.Arbitrum/Serialization/ArbitrumJsonContext.cs
using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Modules;

namespace Nethermind.Arbitrum.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ChainConfig))]
[JsonSerializable(typeof(ArbitrumChainParams))]
[JsonSerializable(typeof(DigestMessageParameters))]
[JsonSerializable(typeof(DigestInitMessage))]
[JsonSerializable(typeof(MessageWithMetadata))]
[JsonSerializable(typeof(MessageResult))]
[JsonSerializable(typeof(SetFinalityDataParams))]
[JsonSerializable(typeof(SetConsensusSyncDataParams))]
[JsonSerializable(typeof(ReorgParameters))]
[JsonSerializable(typeof(MaintenanceStatus))]
[JsonSerializable(typeof(ArbitrumFinalityData))]
[JsonSerializable(typeof(L1IncomingMessage))]
[JsonSerializable(typeof(L1IncomingMessageHeader))]
internal partial class ArbitrumJsonContext : JsonSerializerContext
{
}
```

### Step 2: Update Serialization Calls

```csharp
// Before (reflection-based)
ChainConfig? config = JsonSerializer.Deserialize<ChainConfig>(bytes);

// After (source-generated)
ChainConfig? config = JsonSerializer.Deserialize(bytes, ArbitrumJsonContext.Default.ChainConfig);
```

### Step 3: Update All Serialization Sites

```csharp
// ArbitrumRpcModule.cs
private bool TryDeserializeChainConfig(ReadOnlySpan<byte> bytes, out ChainConfig? chainConfig)
{
    try
    {
        chainConfig = JsonSerializer.Deserialize(bytes, ArbitrumJsonContext.Default.ChainConfig);
        return chainConfig != null;
    }
    catch (JsonException exception)
    {
        Logger.Error("Failed to deserialize ChainConfig from bytes.", exception);
        chainConfig = null;
        return false;
    }
}

// For serialization
string json = JsonSerializer.Serialize(result, ArbitrumJsonContext.Default.MessageResult);
```

### Step 4: Add Project Configuration

```xml
<!-- Nethermind.Arbitrum.csproj -->
<PropertyGroup>
    <!-- Enable source generators -->
    <EnablePreviewFeatures>true</EnablePreviewFeatures>
</PropertyGroup>

<ItemGroup>
    <!-- Already included by default in .NET 8+ -->
</ItemGroup>
```

## Types to Include

| Type | Usage | Priority |
|------|-------|----------|
| `ChainConfig` | Genesis deserialization | High |
| `ArbitrumChainParams` | Nested in ChainConfig | High |
| `DigestMessageParameters` | RPC input | High |
| `MessageWithMetadata` | RPC input | High |
| `MessageResult` | RPC output | High |
| `SetFinalityDataParams` | RPC input | Medium |
| `MaintenanceStatus` | RPC output | Medium |
| `L1IncomingMessage` | Internal | Medium |

## Benchmark Verification

```csharp
[MemoryDiagnoser]
public class JsonSerializationBenchmarks
{
    private byte[] _chainConfigJson = null!;
    private ChainConfig _chainConfig = null!;

    [GlobalSetup]
    public void Setup()
    {
        _chainConfig = CreateTestChainConfig();
        _chainConfigJson = JsonSerializer.SerializeToUtf8Bytes(_chainConfig);
    }

    [Benchmark(Baseline = true)]
    public ChainConfig? Deserialize_Reflection()
    {
        return JsonSerializer.Deserialize<ChainConfig>(_chainConfigJson);
    }

    [Benchmark]
    public ChainConfig? Deserialize_SourceGen()
    {
        return JsonSerializer.Deserialize(_chainConfigJson, ArbitrumJsonContext.Default.ChainConfig);
    }

    [Benchmark]
    public byte[] Serialize_Reflection()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_chainConfig);
    }

    [Benchmark]
    public byte[] Serialize_SourceGen()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_chainConfig, ArbitrumJsonContext.Default.ChainConfig);
    }
}
```

Expected results:
```
| Method                    | Mean     | Allocated |
|---------------------------|----------|-----------|
| Deserialize_Reflection    | 850 ns   | 512 B     |
| Deserialize_SourceGen     | 450 ns   | 256 B     |
| Serialize_Reflection      | 620 ns   | 384 B     |
| Serialize_SourceGen       | 380 ns   | 192 B     |
```

## Files to Update

| File | Changes |
|------|---------|
| `ArbitrumRpcModule.cs` | Use source-gen context |
| `ArbitrumBlockTreeInitializer.cs` | Use source-gen context |
| `ArbitrumRpcTestBlockchain.cs` | Use source-gen context |
| New: `ArbitrumJsonContext.cs` | Create source gen context |

## Acceptance Criteria

- [ ] `ArbitrumJsonContext` created with all key types
- [ ] At least 3 serialization sites updated
- [ ] Benchmarks show improvement
- [ ] All existing tests pass
- [ ] No new warnings
- [ ] Startup time not regressed

## Edge Cases

1. **Polymorphic types**: May need `[JsonDerivedType]` attributes
2. **Custom converters**: Existing converters still work with source gen
3. **Nullable types**: Source gen handles nullable correctly
4. **Private setters**: May need `[JsonInclude]` attribute

## Rollback Plan

Source-gen is additive - can mix reflection and source-gen:

```csharp
// Use source-gen where available, reflection as fallback
ChainConfig? config = ArbitrumJsonContext.Default.ChainConfig is not null
    ? JsonSerializer.Deserialize(bytes, ArbitrumJsonContext.Default.ChainConfig)
    : JsonSerializer.Deserialize<ChainConfig>(bytes);
```

## Future Enhancements

1. **Full coverage**: Add all serializable types to context
2. **Options sharing**: Share JsonSerializerOptions with Nethermind core
3. **AOT compatibility**: Source-gen enables Native AOT compilation
