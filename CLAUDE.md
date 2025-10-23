# Nethermind Arbitrum

This project is an implementation of the Arbitrum execution client in C# as a plugin for the Nethermind Ethereum client.

## Development Commands

```bash
# Clean the project
dotnet clean src/Nethermind.Arbitrum.slnx

# Build the project
dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj

# Run tests (use --filter to run specific tests)
dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj
```

## Business Context

Arbitrum is a Layer 2 scaling solution for Ethereum that uses optimistic rollups to increase transaction throughput and reduce fees. Arbitrum already has execution and consensus client implementation in Go (Nitro). OffchainLabs is supporting development of a Nethermind-based execution client to provide more diversity and choice in the Ethereum ecosystem. Also, Nethermind's client is known for its performance and modularity, making it a strong candidate for Arbitrum's execution layer.

## Project Structure

Source code of Nethermind client is connected to the plugin repository via git submodule located at `/src/Nethermind`. The main Arbitrum plugin code is located in `/src/Nethermind.Arbitrum/` and its tests are in `/src/Nethermind.Arbitrum.Test/`.
As an AI, you can also have access to Nitro source code located in `../arbitrum-nitro` directory. If access is not available, request it from the user.

## Key Components

### RPC Modules

RPC module enables Nitro (consensus layer) to communicate with Nethermind (execution layer).

**Key Methods:**
- `DigestInitMessage` - initialize genesis
- `DigestMessage` - process transactions and produce block
- `SetFinalityData` - update finality information
- Conversions: `MessageIndexToBlockNumber`, `BlockNumberToMessageIndex`

**Locations:**
- Interface: `../arbitrum-nitro/execution/interface.go` (Go, source of truth)
- Implementation: `/src/Nethermind.Arbitrum/Modules/ArbitrumRpcModule.cs` (C#)

### ArbOS

What is ArbOS
- Arbitrum Operating System - manages L2 state and chain behavior
- Handles L1/L2 pricing, retryables, address tables, merkle accumulator
- Version-gated features (e.g., Stylus available from v30+)

Source code located in `/src/Nethermind.Arbitrum/Arbos/` in Nethermind, and `../arbitrum-nitro/arbos/` in Nitro (Go, source of truth).

### Precompiles

Precompiles are special smart contracts at fixed addresses that provide system-level functionality. They are implemented natively in the execution client for performance and security.

#### Nitro (Go) - Source of Truth
- **Generated Bindings**: `../arbitrum-nitro/solgen/go/localgen/localgen.go` contains ABI JSON strings, look for a `var {precompile-name}MetaData = &bind.MetaData{...}` pattern.
- **Implementation**: Go files in `../arbitrum-nitro/precompiles/Arb*.go`
- **Registration**: Reflection-based in `../arbitrum-nitro/precompiles/precompile.go:Precompiles()`

#### Nethermind (C#) - Implementation
- **Pattern**: Two-file system per precompile
  1. **Implementation file** (`ArbXxx.cs`) - business logic
  2. **Parser file** (`ArbXxxParser.cs`) - ABI encoding/decoding
- **Location**: `/src/Nethermind.Arbitrum/Precompiles/`
- **Registration**: Manual in `PrecompileHelper.cs`

### Stylus/WASM

Stylus is a WebAssembly (WASM) based smart contract platform for Arbitrum, allowing developers to write contracts in languages that compile to WASM.
Stylus contracts are executed through a WASM runtime source code of which is located in Nitro repository at `../arbitrum-nitro/arbitrator`. Go code interacts with WASM runtime native libraries via abstractions located in `../arbitrum-nitro/arbos/programs`.
Nethermind implements Stylus support by integrating with the Nitro WASM runtime through interop calls. C# code for Stylus support is located in `/src/Nethermind.Arbitrum/Arbos/Stylus` and `/src/Nethermind.Arbitrum/Stylus`.

## Development Guidelines

### Before Implementation

* Conduct thorough analysis of the feature requirements
* Review related code in both Nethermind and Nitro repositories
* Create a detailed implementation plan outlining steps and components involved

### During Implementation

* Strictly follow the established code organization, patterns and style
* Code of the Nethermind client `./src/Nethermind` should be considered as read-only
* Change only code related to the feature being implemented
* Pay attention to performance optimizations and memory allocations
  * Use `Span<T>` and `Memory<T>` where applicable
  * Pass large structs by reference
  * Avoid unnecessary allocations in hot paths
* Cover new code with unit and integration tests (prioritize integration tests)
* When test failure occurs, investigate root cause instead of blindly fixing tests
* After a few attempts to fix tests, consult with a human developer if the issue persists

### After Implementation

* Review code for adherence to guidelines
* Ensure code builds successfully without new warnings (ignore unrelated warnings)
* Ensure all tests from `src/Nethermind.Arbitrum.Test` are passing

### Code Organization and Style

* Comment only when necessary to explain "why", not "what"; or when the code is not self-explanatory
* Prefer .NET XML documentation format over inline comments for type members
* Always specify types explicitly instead of using `var`
* Ensure proper identifiers order: constants, static fields, instance fields, constructors, properties, methods
* Ensure proper access modifiers order: `public`, `internal`, `protected`, `private`
* Skip braces for single-line blocks
* Avoid static imports and unused usings

### Testing Guidelines

* Strictly follow the following naming convention for test methods:
  * SystemUnderTest_StateUnderTest_ExpectedBehavior
  * Example: `ArbInfo_GetVersion_ReturnsCorrectVersion`
* Avoid dependencies between test classes
* Avoid `[SetUp]` and `[TearDown]` methods and do your best to keep tests isolated
* Prefer integration tests using `ArbitrumRpcTestBlockchain` over unit tests when possible
