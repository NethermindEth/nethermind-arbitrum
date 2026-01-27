---
name: debug-mode
description: Enter structured Debugger Mode for systematic issue investigation
argument-hint: [describe the issue or error you're seeing]
---

# Debugger Mode - Systematic Issue Investigation

**Issue to investigate**: ${1:describe the error, unexpected behavior, or problem}

## Debugger Mode Protocol

When entering Debugger Mode, follow this exact sequence:

### Step 1: Hypothesis Generation

Reflect on **5-7 different possible sources** of the problem. Consider:

- **Gas/computation issues**: Incorrect gas calculations, order of operations
- **State management**: Storage offsets, world state handling, commit timing
- **ABI encoding/decoding**: Big-endian vs little-endian, padding, type conversions
- **Control flow**: Missing branches, incorrect conditions, exception handling
- **Data structures**: Initialization, null references, collection boundaries
- **Cross-repo divergence**: C# behavior differs from Go source of truth
- **Configuration/setup**: Missing parameters, incorrect defaults

### Step 2: Hypothesis Refinement

Distill the 5-7 possibilities down to **1-2 most likely sources** based on:
- Error message content
- File locations involved
- Timing/sequence of failure
- Similar past issues

### Step 3: Diagnostic Logging

Before implementing any fix, add **strategic logs** to:
- Validate assumptions about data flow
- Track transformation of data structures through execution
- Identify exact divergence point

```csharp
// Example diagnostic pattern for Nethermind
_logger.Debug("DEBUG: {Method} - Input: {Input}, State: {State}", methodName, input, relevantState);
```

### Step 4: Log Collection

Request or collect relevant logs:
- Application logs from the failing scenario
- Nitro comparison logs (if running comparison mode)
- Test output with verbose flags

For Nethermind Arbitrum tests, use:
```bash
dotnet test src/Nethermind.Arbitrum.Test --filter "TestName" -v detailed
```

### Step 5: Deep Analysis

Based on collected data, perform comprehensive analysis:
- Compare actual vs expected values at each logged point
- Trace the data flow from input to failure
- Identify the exact line/operation where divergence occurs
- Cross-reference with Nitro implementation if relevant

### Step 6: Iterative Investigation

If source is still unclear:
- Add more targeted logs
- Narrow the scope based on previous findings
- Consider using State-Root-Debugger for state divergence issues
- Consider using Cross-Repo-Validator for implementation mismatches

### Step 7: Fix Implementation

Once root cause is identified:
1. Propose the fix with explanation
2. Reference the exact Nitro behavior (if applicable)
3. Ask for approval before making changes
4. After fix: Request approval to remove diagnostic logs

## Agent Integration

For complex debugging, invoke specialized agents:

**State Root Issues**:
```
/state-root-debug [block number or transaction]
```

**Implementation Mismatch**:
```
/compare-impl [component name]
```

**Nitro Reference Check**:
```
/nitro-lookup [feature or function name]
```

## Output Format

Present findings as:

```markdown
## Debugging Summary

### Issue
[Brief description of the problem]

### Hypotheses Considered
1. [Most likely] - [reason]
2. [Second most likely] - [reason]
3-7. [Other considered sources]

### Investigation
[What was checked and what was found]

### Root Cause
[Exact cause identified with file:line reference]

### Fix
[Proposed solution]

### Verification
[How to verify the fix works]
```

## Begin Debugger Mode

Analyze the issue described above and follow the Debugger Mode Protocol systematically.

Start with Step 1: Generate 5-7 hypotheses for what could be causing: **${1}**
