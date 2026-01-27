---
paths: src/Nethermind.Arbitrum/**/*.cs
---
# C# Code Style for Nethermind Arbitrum

## Type Declarations

**ALWAYS use explicit types. NEVER use `var`.**

```csharp
// ✅ CORRECT
Address testAccount = new("0x123...");
UInt256 balance = UInt256.Zero;
List<Transaction> transactions = new();
IWorldState worldState = TestWorldStateFactory.CreateForTest();

// ❌ WRONG - DO NOT USE
var testAccount = new Address("0x123...");
var balance = UInt256.Zero;
var transactions = new List<Transaction>();
var worldState = TestWorldStateFactory.CreateForTest();
```

**Always use explicit access modifiers.**

```csharp
// ✅ CORRECT
public class MyClass
{
    private readonly ILogger _logger;
    public string Name { get; }
}

// ❌ WRONG - missing access modifiers
class MyClass
{
    readonly ILogger _logger;
    string Name { get; }
}
```

## Member Ordering (CRITICAL)

Members MUST appear in this exact order:

```csharp
public class ExampleClass
{
    // 1. CONSTANTS (const)
    private const int MaxRetries = 3;
    public const string DefaultName = "Unknown";

    // 2. STATIC FIELDS
    private static readonly ILogger s_logger = NullLogger.Instance;
    public static int InstanceCount;

    // 3. INSTANCE FIELDS
    private readonly IBlockTree _blockTree;
    private readonly IWorldState _worldState;
    private int _counter;

    // 4. CONSTRUCTORS
    public ExampleClass(IBlockTree blockTree, IWorldState worldState)
    {
        _blockTree = blockTree;
        _worldState = worldState;
    }

    // 5. PROPERTIES
    public string Name { get; }
    public int Counter => _counter;

    // 6. METHODS
    public void DoSomething() { }
    private void HelperMethod() { }
}
```

## Access Modifier Ordering Within Each Section

Within each member category, order by access level:
1. `public`
2. `internal`
3. `protected`
4. `private`

```csharp
// 3. INSTANCE FIELDS - ordered by access
public readonly Address ContractAddress;      // public first
internal readonly int BatchSize;              // internal second
protected readonly ILogger Logger;            // protected third
private readonly IBlockTree _blockTree;       // private last
```

## Braces and Single-Line Blocks

**Skip braces for single-line blocks:**

```csharp
// ✅ CORRECT - no braces for single statement
if (condition)
    DoSomething();

foreach (Transaction tx in transactions)
    ProcessTransaction(tx);

// ❌ WRONG - unnecessary braces
if (condition)
{
    DoSomething();
}
```

**But use braces for multi-line:**

```csharp
// ✅ CORRECT - braces needed for multiple statements
if (condition)
{
    DoSomething();
    DoSomethingElse();
}
```

## Performance Guidelines
- Use `Span<T>` and `Memory<T>` for buffer operations
- Pass large structs by `in` or `ref` reference
- Avoid allocations in hot paths
- Use big-endian ABI encoding by default for Arbitrum

## Code Organization
- Comment only to explain "why", not "what"
- Prefer .NET XML documentation format over inline comments for public APIs
- Avoid static imports (`using static`)
- Remove unused usings

## General Principles
- Change only code related to the feature being implemented
- Ensure code builds without new warnings
