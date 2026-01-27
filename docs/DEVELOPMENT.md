# Development Guide

## Prerequisites

- **.NET SDK 10.0+** - [Download](https://dotnet.microsoft.com/download)
- **IDE**: JetBrains Rider (recommended) or Visual Studio 2022
- **Git** with submodule support
- **Docker** (for integration testing)

## Getting Started

### Clone the Repository

```bash
# Clone with submodules (required)
git clone --recursive https://github.com/NethermindEth/nethermind-arbitrum.git
cd nethermind-arbitrum

# If already cloned, initialize submodules
git submodule update --init --recursive
```

### Build

```bash
# Clean build
dotnet clean src/Nethermind.Arbitrum.slnx

# Build plugin
dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj

# Build with release configuration
dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj -c Release
```

### Run Tests

```bash
# Run all tests
dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj

# Run specific test by name
dotnet test --filter "FullyQualifiedName~ArbSys"

# Run with verbose output
dotnet test -v normal
```

## Project Structure

```
nethermind-arbitrum/
├── src/
│   ├── Nethermind/                      # Nethermind submodule (READ-ONLY)
│   ├── Nethermind.Arbitrum/             # Plugin source
│   │   ├── Arbos/                       # ArbOS state management
│   │   │   ├── Storage/                 # State storage implementations
│   │   │   ├── Programs/                # Stylus program execution
│   │   │   └── Stylus/                  # Native WASM interop
│   │   ├── Config/                      # Configuration types
│   │   ├── Core/                        # Core utilities
│   │   ├── Data/                        # Data types, transactions
│   │   ├── Evm/                         # EVM modifications
│   │   ├── Execution/                   # Block/transaction processing
│   │   │   └── Transactions/            # Transaction decoders
│   │   ├── Genesis/                     # Genesis initialization
│   │   ├── Modules/                     # RPC modules
│   │   ├── Precompiles/                 # Precompile implementations
│   │   │   ├── Abi/                     # ABI encoding/decoding
│   │   │   ├── Events/                  # Event encoders
│   │   │   └── Exceptions/              # Precompile exceptions
│   │   ├── Stylus/                      # WASM store management
│   │   ├── Tracing/                     # Debug tracing
│   │   └── Properties/                  # Configs & chainspecs
│   └── Nethermind.Arbitrum.Test/        # Test suite
├── docs/                                # Documentation
├── .claude/                             # AI assistant context
└── CLAUDE.md                            # Project guidelines
```

## Code Style

### Type Declarations

**Always use explicit types. Never use `var`.**

```csharp
// ✅ Correct
Address testAccount = new("0x123...");
UInt256 balance = UInt256.Zero;
List<Transaction> transactions = new();

// ❌ Wrong
var testAccount = new Address("0x123...");
```

### Member Ordering

Members must appear in this order:
1. Constants (`const`)
2. Static fields
3. Instance fields
4. Constructors
5. Properties
6. Methods

Within each category, order by access level: `public`, `internal`, `protected`, `private`.

### Braces

Skip braces for single-line blocks:

```csharp
// ✅ Correct
if (condition)
    DoSomething();

// ❌ Wrong
if (condition)
{
    DoSomething();
}
```

### Performance Guidelines

- Use `Span<T>` and `Memory<T>` for buffer operations
- Pass large structs by `in` or `ref` reference
- Avoid allocations in hot paths
- Use big-endian ABI encoding for Arbitrum compatibility

## Testing Guidelines

### Naming Convention

Use: `SystemUnderTest_StateUnderTest_ExpectedBehavior`

```csharp
// ✅ Good
ArbInfo_GetVersion_ReturnsCorrectVersion()
ArbSys_SendTxToL1_EmitsL2ToL1TxEvent()

// ❌ Bad
TestGetVersion()
Test1()
```

### Test Structure

Follow AAA (Arrange, Act, Assert):

```csharp
[Test]
public void ArbInfo_GetBalance_ReturnsAccountBalance()
{
    // Arrange
    Address account = new("0x1234...");
    UInt256 expectedBalance = 1000;
    // ... setup

    // Act
    UInt256 result = ArbInfo.GetBalance(context, account);

    // Assert
    Assert.That(result, Is.EqualTo(expectedBalance));
}
```

### Test Guidelines

- One assert per test (test failure pinpoints exactly what broke)
- Each test should be independent
- Prefer integration tests using `ArbitrumRpcTestBlockchain`
- Avoid `[SetUp]` and `[TearDown]` - keep tests self-contained
- Extract shared logic into test infrastructure

## Common Development Tasks

### Adding a Precompile Method

1. Add method signature to `ArbXxxParser.cs`:
   ```csharp
   private static readonly FrozenDictionary<int, Func<...>> Methods = new Dictionary<int, Func<...>>
   {
       { 0x12345678, NewMethod }  // Add method selector
   }.ToFrozenDictionary();
   ```

2. Implement business logic in `ArbXxx.cs`:
   ```csharp
   public static (byte[], long) NewMethod(ArbitrumPrecompileExecutionContext ctx, ...)
   {
       // Implementation
   }
   ```

3. Write tests validating against Nitro behavior

4. Validate with Cross-Repo-Validator agent

### Updating ArbOS Version Support

1. Add version constant in `ArbosVersion.cs`
2. Implement version-gated logic with conditional checks
3. Update `ArbosStateVersionProvider`
4. Test with appropriate chainspec

### Working with Nitro

The Nitro repository is the source of truth. When implementing:

1. **Read the Go implementation first**
2. **Match behavior exactly** - any deviation causes state root mismatch
3. **Validate storage offsets** - use Cross-Repo-Validator
4. **Test against Nitro** - run comparison tests

## Debugging

### Local Development

```bash
# Run with local config
dotnet run --project src/Nethermind/src/Nethermind/Nethermind.Runner -- \
  -c arbitrum-local \
  --data-dir ./test-data \
  --log DEBUG
```

### Stylus/WASM Debugging

Set environment variable for verbose WASM output:

```bash
export STYLUS_DEBUG=1
```

### State Root Debugging

Use the State-Root-Debugger agent for binary search debugging:

```
/state-root-debug
```

## Validation Tools

### Cross-Repo Validation

Compare implementation against Nitro:

```
/compare-impl <component>
```

### Precompile Validation

Validate precompile ABI and gas costs:

```
/validate-precompile <precompile-name>
```

## Pull Request Guidelines

Before submitting:

1. **Ensure code compiles** without new warnings
2. **Run tests** and ensure they pass
3. **Follow code style** (see above)
4. **Validate against Nitro** for behavioral changes
5. **Update documentation** if adding new features

## Resources

- [Arbitrum Documentation](https://docs.arbitrum.io/)
- [Nethermind Documentation](https://docs.nethermind.io/)
- [Nitro Source Code](https://github.com/OffchainLabs/nitro)
- [CLAUDE.md](../CLAUDE.md) - Project guidelines

---

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - Component overview
- [RPC-API.md](RPC-API.md) - RPC method reference
- [PRECOMPILES.md](PRECOMPILES.md) - Precompile reference
