---
applyTo:
  - "src/Nethermind.Arbitrum.Test/**/*.cs"
  - "**/Test/**/*.cs"
  - "**/*Tests.cs"
  - "**/*Test.cs"
---

# Test Code Review Instructions

## Test Naming Convention (CRITICAL - ALWAYS ENFORCE)

**Pattern: `MethodName_Condition_ExpectedResult`**

Every test method MUST have exactly THREE parts separated by underscores:
1. Method/feature being tested
2. State/condition/input
3. Expected behavior/result

### CORRECT Examples (approve these patterns)
```csharp
GetBalance_ValidAccountAndEnoughGas_ReturnsBalance
GetBalance_InsufficientGas_ThrowsOutOfGasException
GetBalance_NonExistentAccount_ReturnsZero
DigestMessage_ValidMessage_ProducesBlock
ParseInput_MalformedData_ThrowsDecodingException
```

### WRONG Examples (REJECT and request fix)
```csharp
// Missing condition part
GetBalance_ReturnsBalance           // REJECT: missing condition
TestGetBalance                      // REJECT: not following pattern

// Informal/verbose language
GetBalance_DoesntHaveEnoughBalance_Fails    // REJECT: use "InsufficientBalance"
GetBalance_WhenAccountDoesNotExist_Returns0 // REJECT: use "NonExistentAccount"

// Extra prefixes
Should_GetBalance_WhenValid_ReturnBalance   // REJECT: no "Should" prefix
Test_GetBalance_Returns_Value               // REJECT: no "Test" prefix

// Vague naming
BalanceTest                         // REJECT: not descriptive
TestMethod1                         // REJECT: meaningless name
```

### Naming Guidelines

**For conditions, use concise terms:**
- `ValidInput`, `InvalidInput`, `MalformedData`
- `EnoughGas`, `InsufficientGas`, `ZeroGas`
- `ExistingAccount`, `NonExistentAccount`
- `EmptyArray`, `NullParameter`, `ZeroValue`

**For results, use action verbs:**
- `ReturnsValue`, `ReturnsZero`, `ReturnsNull`, `ReturnsEmpty`
- `ThrowsException`, `ThrowsArgumentException`
- `ProducesBlock`, `UpdatesState`, `EmitsEvent`
- `Succeeds`, `Fails`

## Test Structure (AAA Pattern)

Require clear Arrange/Act/Assert sections:

```csharp
[Test]
public void GetBalance_ValidAccount_ReturnsBalance()
{
    Address account = new("0x123...");
    UInt256 expectedBalance = 1000;

    UInt256 result = sut.GetBalance(account);

    Assert.That(result, Is.EqualTo(expectedBalance));
}
```

## Assertion Rules (CRITICAL)

**REJECT** these assertion patterns:
```csharp
// WRONG - no range comparisons
Assert.That(value, Is.GreaterThan(0));
Assert.That(value, Is.LessThan(100));
Assert.That(value, Is.AtLeast(1));

// CORRECT - exact values only
Assert.That(value, Is.EqualTo(42));
Assert.That(value, Is.EqualTo(expectedValue));
```

**Flag** tests with:
- Multiple unrelated assertions
- Branching logic (if/switch)
- Shared mutable state between tests
- Dependencies on test execution order

## Test Organization

**Flag** these patterns:
- `[SetUp]` or `[TearDown]` methods (prefer self-contained tests)
- Private helper methods (should be in test infrastructure)
- Test classes with dependencies on other test classes

## Type Declarations in Tests

Same as production code - **REJECT** `var` keyword:

```csharp
// CORRECT
IWorldState worldState = TestWorldStateFactory.CreateForTest();
Address account = new("0x123...");

// WRONG
var worldState = TestWorldStateFactory.CreateForTest();
var account = new Address("0x123...");
```
