# Architecture Optimization Tasks

This directory contains atomic implementation tasks for architecture optimizations in nethermind-arbitrum based on 2025-2026 best practices analysis.

## Overview

These tasks focus on:
- **Performance**: Reduce allocations, improve hot-path efficiency
- **Maintainability**: Better separation of concerns, cleaner APIs
- **Testability**: Improved test infrastructure
- **Modern Patterns**: Source generators, Span-based APIs

## Measurement Strategy

**CRITICAL**: Every task MUST have measurable before/after metrics. Use:

1. **BenchmarkDotNet** - For micro-benchmarks (allocation, throughput)
2. **Existing Test Suite** - Ensure no regressions (all tests pass)
3. **Memory Profiler** - Track GC pressure reduction
4. **Custom Metrics** - Task-specific measurements

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project src/Nethermind.Arbitrum.Benchmarks

# Run specific benchmark
dotnet run -c Release --project src/Nethermind.Arbitrum.Benchmarks -- --filter "*AbiDecoder*"

# Export results for comparison
dotnet run -c Release --project src/Nethermind.Arbitrum.Benchmarks -- --exporters json
```

### Comparing Results

```bash
# Before changes: save baseline
dotnet run -c Release -- --job baseline --exporters json

# After changes: compare
dotnet run -c Release -- --job current --baseline baseline.json
```

## Task Index

### Phase 0: Infrastructure (BLOCKING)

| Task | Description | Impact | Status |
|------|-------------|--------|--------|
| [00](00-benchmark-infrastructure.md) | BenchmarkDotNet setup + baseline metrics | Required | TODO |

### Phase 1: High-Impact Performance (Plugin)

| Task | Description | Allocations Saved | Status |
|------|-------------|-------------------|--------|
| [01](01-span-abi-decoder.md) | Span-based ABI decoder | ~32-64 bytes/precompile call | TODO |
| [02](02-precompile-dispatch.md) | Interface-based dispatch | Runtime type checks | TODO |

### Phase 2: Architecture Improvements (Plugin)

| Task | Description | Benefit | Status |
|------|-------------|---------|--------|
| [03](03-rpc-cqrs-extraction.md) | CQRS-style handler extraction | Testability, SoC | TODO |
| [04](04-result-pattern.md) | Consistent Result<T> pattern | Exception reduction | TODO |

### Phase 3: Modern .NET Features (Plugin)

| Task | Description | Benefit | Status |
|------|-------------|---------|--------|
| [05](05-source-gen-serialization.md) | Source-generated JSON | Startup time, allocations | TODO |
| [06](06-test-builder-pattern.md) | Test infrastructure builder | Test maintainability | TODO |

## Dependency Graph

```
Phase 0: Infrastructure (BLOCKING)
┌─────────────────────────────────────────────────────────────────────────────┐
│  00-BENCHMARK-INFRA ──► Create benchmark project + baseline measurements    │
│  (MUST DO FIRST)        Enables before/after comparison for all tasks       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
Phase 1: High-Impact Performance
┌─────────────────────────────────────────────────────────────────────────────┐
│  01-SPAN-ABI ──────────► 02-PRECOMPILE-DISPATCH                             │
│  (independent)           (independent)                                      │
│                                                                             │
│  Both can be done in parallel, both reduce hot-path overhead                │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
Phase 2: Architecture Improvements
┌─────────────────────────────────────────────────────────────────────────────┐
│  03-RPC-CQRS ──────────► 04-RESULT-PATTERN                                  │
│  (independent)           (can use handlers from 03)                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
Phase 3: Modern .NET Features
┌─────────────────────────────────────────────────────────────────────────────┐
│  05-SOURCE-GEN ────────► 06-TEST-BUILDER                                    │
│  (independent)           (independent)                                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Success Criteria

Each task must demonstrate:

1. **Measurable Improvement**: Benchmark shows improvement (or no regression)
2. **All Tests Pass**: `dotnet test src/Nethermind.Arbitrum.Test` succeeds
3. **Build Clean**: No new warnings
4. **Code Review**: Follows existing patterns and style guide

## Key Files Reference

### Precompiles (Tasks 01, 02)
- `src/Nethermind.Arbitrum/Precompiles/PrecompileHelper.cs`
- `src/Nethermind.Arbitrum/Precompiles/Parser/*.cs`
- `src/Nethermind.Arbitrum/Precompiles/Abi/PrecompileAbiEncoder.cs`

### RPC Module (Tasks 03, 04)
- `src/Nethermind.Arbitrum/Modules/ArbitrumRpcModule.cs`
- `src/Nethermind.Arbitrum/Modules/IArbitrumRpcModule.cs`

### Serialization (Task 05)
- `src/Nethermind.Arbitrum/Data/*.cs`
- `src/Nethermind.Arbitrum/Config/*.cs`

### Tests (Task 06)
- `src/Nethermind.Arbitrum.Test/Infrastructure/ArbitrumRpcTestBlockchain.cs`
