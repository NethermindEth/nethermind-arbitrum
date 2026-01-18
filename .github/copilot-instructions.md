# Copilot Code Review Instructions for Nethermind Arbitrum

## Project Context

This is a C# implementation of the Arbitrum execution client as a plugin for Nethermind Ethereum client. Code correctness and style consistency are critical for blockchain consensus.

## Code Style Rules (ENFORCE STRICTLY)

### Type Declarations
- **REJECT** any use of `var` keyword - always require explicit types
- **REJECT** missing access modifiers on class members

```csharp
// CORRECT
Address account = new("0x123...");
List<Transaction> txs = new();

// WRONG - flag these
var account = new Address("0x123...");
var txs = new List<Transaction>();
```

### Member Ordering (CRITICAL)
Flag violations of this order within classes:
1. Constants (`const`)
2. Static fields
3. Instance fields
4. Constructors
5. Properties
6. Methods

### Access Modifier Ordering
Within each member category, order by access level:
1. `public`
2. `internal`
3. `protected`
4. `private`

### Braces
- Single-statement blocks should NOT have braces
- Multi-statement blocks MUST have braces

```csharp
// CORRECT
if (condition)
    DoSomething();

// WRONG
if (condition)
{
    DoSomething();
}
```

## Performance Requirements

Flag these anti-patterns:
- Unnecessary allocations in loops or hot paths
- Large structs passed by value instead of `in` or `ref`
- Missing `Span<T>` or `Memory<T>` for buffer operations
- LINQ in performance-critical code paths
