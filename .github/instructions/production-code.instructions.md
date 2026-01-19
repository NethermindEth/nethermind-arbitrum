---
applyTo:
  - "src/Nethermind.Arbitrum/**/*.cs"
excludeAgent:
  - "coding-agent"
---

# Production Code Review Instructions

## Member Ordering (CRITICAL - ALWAYS CHECK)

Every class MUST have members in this exact order:

```csharp
public class Example
{
    // 1. CONSTANTS (const) - public first, then private
    public const int MaxValue = 100;
    private const string DefaultName = "Unknown";

    // 2. STATIC FIELDS - public first, then private
    public static readonly ILogger SharedLogger;
    private static readonly object s_lock = new();

    // 3. INSTANCE FIELDS - public first, then private
    public readonly Address ContractAddress;
    private readonly IBlockTree _blockTree;
    private int _counter;

    // 4. CONSTRUCTORS
    public Example(IBlockTree blockTree) { }

    // 5. PROPERTIES - public first, then private
    public string Name { get; }
    private int InternalCounter => _counter;

    // 6. METHODS - public first, then private
    public void Process() { }
    private void Helper() { }
}
```

**REJECT** code where:
- Fields appear after constructors
- Properties appear before fields
- Methods appear before properties
- Private members appear before public members in same category

## Type Declarations (CRITICAL)

**REJECT** any use of `var`:

```csharp
// REJECT
var address = new Address("0x123");
var transactions = new List<Transaction>();
var result = GetResult();

// REQUIRE
Address address = new("0x123");
List<Transaction> transactions = new();
MyResultType result = GetResult();
```

## Access Modifiers

**REJECT** missing access modifiers:

```csharp
// REJECT
class MyClass { }
readonly ILogger _logger;

// REQUIRE
public class MyClass { }
private readonly ILogger _logger;
```

## Execution (src/Nethermind.Arbitrum/Execution/**)

Additional checks for execution files:
- Gas deduction order is critical - flag any reordering of gas operations
- Flag changes to refund calculations
- Flag any state modification that could be non-deterministic

## Performance

**Flag** these patterns:
- Allocations inside loops
- LINQ in hot paths (prefer foreach)
- Large structs passed by value
- String concatenation in loops (use StringBuilder)
- Missing `readonly` on fields that could be readonly
