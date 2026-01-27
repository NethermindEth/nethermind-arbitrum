---
paths: src/Nethermind.Arbitrum.Test/**/*.cs
---
# Testing Guidelines for Nethermind Arbitrum

## Naming Convention (CRITICAL)

**Pattern: `MethodName_Condition_ExpectedResult`**

The test name MUST have THREE parts separated by underscores:
1. **Method/Feature being tested** - What you're testing
2. **State/Condition** - The input condition or state
3. **Expected behavior** - What should happen

### ✅ CORRECT Examples

```csharp
// Method_Condition_Result pattern
GetBalance_PositiveBalanceAndEnoughGas_ReturnsBalance
GetBalance_NotEnoughGas_ThrowsOutOfGasException
GetBalance_NonExistentAccount_ReturnsZero
GetCode_ExistingContractAndEnoughGas_ReturnsCode
GetCode_NotEnoughGas_ThrowsOutOfGasException
DigestMessage_ValidMessage_ProducesBlock
DigestMessage_InvalidIndex_ReturnsError
SetFinalityData_ValidData_UpdatesFinality
ParseInput_ValidAbiEncodedData_DecodesCorrectly
ParseInput_MalformedData_ThrowsDecodingException
```

### ❌ WRONG Examples - DO NOT USE

```csharp
// Too vague - missing condition
GetBalance_ReturnsBalance                    // ❌ Missing condition
TestGetBalance                               // ❌ Not following pattern

// Missing parts
GetBalance_ShouldWork                        // ❌ Missing condition, vague result
BalanceTest                                  // ❌ Not following pattern at all

// Informal language
GetBalance_DoesntHaveEnoughBalance_Fails     // ❌ Use "InsufficientBalance" not "DoesntHaveEnoughBalance"
GetBalance_WhenAccountDoesNotExist_Returns0  // ❌ Use "NonExistentAccount" not "WhenAccountDoesNotExist"

// Extra words
Should_GetBalance_WhenValid_ReturnCorrectBalance  // ❌ Don't add "Should" prefix
Test_GetBalance_Returns_Value                     // ❌ Don't add "Test" prefix
```

### Naming Guidelines

**For conditions, use concise descriptive terms:**
- `ValidInput`, `InvalidInput`, `MalformedData`
- `EnoughGas`, `NotEnoughGas`, `InsufficientGas`
- `ExistingAccount`, `NonExistentAccount`
- `EmptyArray`, `NullParameter`, `ZeroValue`

**For expected results, use action verbs:**
- `Returns{Type}`, `ReturnsZero`, `ReturnsNull`, `ReturnsEmpty`
- `ThrowsException`, `Throws{ExceptionType}`
- `ProducesBlock`, `UpdatesState`, `EmitsEvent`
- `Succeeds`, `Fails` (only when appropriate)

## Structure

Follow AAA (Arrange, Act, Assert) pattern with clear sections:

```csharp
[Test]
public void GetBalance_PositiveBalanceAndEnoughGas_ReturnsBalance()
{
    // Arrange - Set up test data and dependencies
    IWorldState worldState = TestWorldStateFactory.CreateForTest();
    Address testAccount = new("0x0000000000000000000000000000000000000123");
    UInt256 expectedBalance = 456;
    worldState.CreateAccount(testAccount, expectedBalance);
    worldState.Commit(London.Instance);

    ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
    PrecompileTestContextBuilder context = new(worldState, gasSupplied);

    // Act - Execute the code under test
    UInt256 balance = ArbInfo.GetBalance(context, testAccount);

    // Assert - Verify the result (single assert section)
    Assert.That(balance, Is.EqualTo(expectedBalance));
    Assert.That(context.GasLeft, Is.EqualTo(1));
}
```

## Assertion Rules

**DO NOT** use `greater than` or `less than` comparisons. Tests must have EXACT expected values:

```csharp
// ✅ CORRECT - exact values
Assert.That(gasUsed, Is.EqualTo(21000UL));
Assert.That(balance, Is.EqualTo(expectedBalance));
Assert.That(result.Length, Is.EqualTo(32));

// ❌ WRONG - do not use
Assert.That(gasUsed, Is.GreaterThan(0));           // ❌
Assert.That(balance, Is.LessThanOrEqualTo(1000));  // ❌
Assert.That(result.Length, Is.AtLeast(1));         // ❌
```

## Test Organization
- Avoid dependencies between test classes
- Minimize dependencies between test methods
- Extract shared logic to test infrastructure classes, not private helper methods
- Avoid `[SetUp]` and `[TearDown]` - keep tests self-contained

## Test Priority
- Prefer integration tests using `ArbitrumRpcTestBlockchain` over unit tests
- Cover new code with both unit and integration tests
- Prioritize integration tests when feasible

## Debugging Test Failures
- When test failure occurs, investigate root cause instead of blindly fixing tests
- After a few attempts to fix tests, consult with a human developer
