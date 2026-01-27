# Task 00: Benchmark Infrastructure

> **Status**: TODO
> **Priority**: BLOCKING - All other tasks depend on this

## Metadata
- **Target**: New project `src/Nethermind.Arbitrum.Benchmarks/`
- **Type**: Infrastructure
- **Dependencies**: None
- **Estimated Impact**: Enables measurement for all optimization tasks

## Description

Create BenchmarkDotNet infrastructure to measure performance before and after each optimization task. This is CRITICAL for ensuring optimizations actually help.

## Deliverables

1. New benchmark project with BenchmarkDotNet
2. Baseline benchmarks for key hot paths
3. Scripts to run and compare benchmarks
4. CI integration (optional)

## Implementation

### 1. Create Benchmark Project

```bash
dotnet new console -n Nethermind.Arbitrum.Benchmarks -o src/Nethermind.Arbitrum.Benchmarks
cd src/Nethermind.Arbitrum.Benchmarks
dotnet add package BenchmarkDotNet
dotnet add reference ../Nethermind.Arbitrum/Nethermind.Arbitrum.csproj
```

### 2. Project Structure

```
src/Nethermind.Arbitrum.Benchmarks/
├── Nethermind.Arbitrum.Benchmarks.csproj
├── Program.cs
├── Precompiles/
│   ├── AbiDecodingBenchmarks.cs      # Task 01 target
│   └── PrecompileDispatchBenchmarks.cs # Task 02 target
├── Rpc/
│   └── RpcModuleBenchmarks.cs         # Task 03 target
└── Serialization/
    └── JsonSerializationBenchmarks.cs  # Task 05 target
```

### 3. Core Benchmark Classes

```csharp
// Program.cs
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// Precompiles/AbiDecodingBenchmarks.cs
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class AbiDecodingBenchmarks
{
    private byte[] _getBalanceCalldata = null!;
    private byte[] _getCodeCalldata = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Prepare realistic calldata
        Address testAddress = new("0x1234567890123456789012345678901234567890");
        _getBalanceCalldata = PrepareGetBalanceCalldata(testAddress);
        _getCodeCalldata = PrepareGetCodeCalldata(testAddress);
    }

    [Benchmark(Baseline = true)]
    public object CurrentDecoding_GetBalance()
    {
        // Current implementation with ToArray()
        return PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            ArbInfoSignatures.GetBalance,
            _getBalanceCalldata
        );
    }

    [Benchmark]
    public Address SpanDecoding_GetBalance()
    {
        // New span-based implementation (after Task 01)
        ReadOnlySpan<byte> data = _getBalanceCalldata;
        return SpanAbiDecoder.DecodeAddress(ref data);
    }
}

// Precompiles/PrecompileDispatchBenchmarks.cs
[MemoryDiagnoser]
public class PrecompileDispatchBenchmarks
{
    private IArbitrumPrecompile[] _precompiles = null!;
    private ArbitrumPrecompileExecutionContext _context;
    private byte[] _calldata = null!;

    [GlobalSetup]
    public void Setup()
    {
        _precompiles = new IArbitrumPrecompile[]
        {
            ArbInfoParser.Instance,
            ArbSysParser.Instance,
            ArbGasInfoParser.Instance,
            // ... all precompiles
        };
    }

    [Benchmark(Baseline = true)]
    public bool CurrentDispatch_PatternMatch()
    {
        // Current switch-based pattern matching
        foreach (IArbitrumPrecompile precompile in _precompiles)
        {
            ReadOnlySpan<byte> data = _calldata;
            PrecompileHelper.TryCheckMethodVisibility(precompile, _context, ...);
        }
        return true;
    }

    [Benchmark]
    public bool InterfaceDispatch()
    {
        // New interface-based dispatch (after Task 02)
        foreach (IArbitrumPrecompile precompile in _precompiles)
        {
            precompile.CheckMethodVisibility(_context, ...);
        }
        return true;
    }
}
```

### 4. Benchmark Scripts

Create `scripts/run-benchmarks.sh`:

```bash
#!/bin/bash
set -e

BENCHMARK_DIR="src/Nethermind.Arbitrum.Benchmarks"
RESULTS_DIR="benchmark-results"

mkdir -p "$RESULTS_DIR"

# Save current commit
COMMIT=$(git rev-parse --short HEAD)
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Run benchmarks
dotnet run -c Release --project "$BENCHMARK_DIR" -- \
    --exporters json csv \
    --artifacts "$RESULTS_DIR/${TIMESTAMP}_${COMMIT}"

echo "Results saved to: $RESULTS_DIR/${TIMESTAMP}_${COMMIT}"
```

Create `scripts/compare-benchmarks.sh`:

```bash
#!/bin/bash
set -e

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 <baseline-dir> <current-dir>"
    exit 1
fi

BASELINE="$1"
CURRENT="$2"

echo "Comparing benchmarks:"
echo "  Baseline: $BASELINE"
echo "  Current:  $CURRENT"

# Use dotnet-benchmark-compare tool or custom comparison
dotnet tool run benchmark-compare "$BASELINE" "$CURRENT"
```

### 5. Baseline Metrics to Capture

| Benchmark | Key Metric | Target |
|-----------|------------|--------|
| AbiDecoding_GetBalance | Allocated bytes | 0 (currently ~64) |
| AbiDecoding_GetCode | Allocated bytes | 0 (currently ~96+) |
| PrecompileDispatch | ns/op | < current |
| PrecompileDispatch | Allocated bytes | 0 |
| JsonSerialization_ChainConfig | Allocated bytes | Reduce by 50% |
| RpcModule_DigestMessage | ns/op | No regression |

## Acceptance Criteria

- [ ] `src/Nethermind.Arbitrum.Benchmarks` project created and builds
- [ ] BenchmarkDotNet configured with MemoryDiagnoser
- [ ] Baseline benchmarks for precompile ABI decoding
- [ ] Baseline benchmarks for precompile dispatch
- [ ] `scripts/run-benchmarks.sh` works
- [ ] Baseline results saved in `benchmark-results/baseline/`
- [ ] README documents how to run and compare

## Usage by Claude Code

After this task, every optimization task should:

1. **Before changes**: Run `./scripts/run-benchmarks.sh` and save baseline
2. **After changes**: Run benchmarks again
3. **Compare**: Use `./scripts/compare-benchmarks.sh baseline current`
4. **Verify**: Confirm improvement or no regression

## Example Workflow

```bash
# Before making changes
git checkout main
./scripts/run-benchmarks.sh  # saves to benchmark-results/baseline

# After making changes on feature branch
git checkout feature/span-abi-decoder
./scripts/run-benchmarks.sh  # saves to benchmark-results/current

# Compare
./scripts/compare-benchmarks.sh benchmark-results/baseline benchmark-results/current

# Expected output:
# AbiDecoding_GetBalance:
#   Baseline: 64 bytes allocated, 125 ns
#   Current:  0 bytes allocated, 45 ns
#   Improvement: -100% allocations, -64% time
```

## Verification Command

```bash
# Quick verification that benchmarks run
dotnet run -c Release --project src/Nethermind.Arbitrum.Benchmarks -- --list flat

# Should output list of all benchmark methods
```
