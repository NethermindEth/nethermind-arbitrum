---
name: plan-mode
description: Enter Planner Mode for systematic implementation planning with clarifying questions
argument-hint: [describe the feature or change you want to implement]
---

# Planner Mode - Systematic Implementation Planning

**Feature/Change Request**: ${1:describe what you want to implement or change}

## Planner Mode Protocol

When entering Planner Mode, follow this structured process:

### Phase 1: Deep Analysis

Before proposing any plan, thoroughly analyze:

1. **Existing Code Analysis**
   - Search for related code in both Nethermind and Nitro repos
   - Identify files that will need modification
   - Understand current architecture and patterns

2. **Scope Mapping**
   - Map full scope of changes needed
   - Identify dependencies between components
   - Note any cross-repo implications (Nitro source of truth)

3. **Risk Assessment**
   - State root implications
   - Gas calculation order changes
   - Breaking changes to existing tests/behavior

### Phase 2: Clarifying Questions

Based on analysis, ask **4-6 targeted clarifying questions** before drafting a plan:

**Categories of questions to consider:**

- **Scope**: "Should this also include X, or just Y?"
- **Behavior**: "When Z happens, should the result be A or B?"
- **Compatibility**: "Do we need to maintain backward compatibility with X?"
- **Testing**: "What level of test coverage do you expect?"
- **Performance**: "Are there specific performance constraints?"
- **Nitro alignment**: "Should this exactly match Nitro, or is deviation acceptable?"

### Phase 3: Draft Comprehensive Plan

After questions are answered, draft a detailed plan including:

```markdown
## Implementation Plan

### Overview
[1-2 sentence summary]

### Phases

#### Phase 1: [Phase Name]
**Files to modify:**
- `path/to/file.cs` - [brief description of changes]

**Changes:**
1. [Specific change with detail]
2. [Next change]

**Validation:**
- [How to verify this phase is complete]

#### Phase 2: [Phase Name]
...

### Testing Strategy
- Unit tests for: [specific components]
- Integration tests: [scenarios]
- Comparison test: [if state root relevant]

### Risks and Mitigations
- Risk: [description]
  Mitigation: [approach]

### Estimated Impact
- Files modified: N
- New files: M
- Test coverage: [expectation]
```

### Phase 4: Approval Request

Present the plan and explicitly ask:

> "Does this plan look correct? Should I proceed with implementation, or would you like me to adjust anything?"

### Phase 5: Implementation (After Approval)

Once approved:
1. Begin implementing phase by phase
2. After each phase, report:
   - What was just completed
   - What the next steps are
   - How many phases remain

### Phase 6: Validation

After implementation complete:
- Run tests: `dotnet test src/Nethermind.Arbitrum.Test`
- If state-relevant: Run comparison test
- Invoke appropriate validator agents

## Agent Integration

**For Nitro reference:**
```
/nitro-lookup [component name]
```

**For implementation mapping:**
Invoke Implementation-Mapper to find C# equivalents of Go code

**For validation after implementation:**
```
/validate-precompile [precompile name]
```
or
```
/compare-impl [component]
```

## Planner Mode Output Template

```markdown
## Analysis Results

### Existing Code Found
- [File 1]: [relevant content]
- [File 2]: [relevant content]

### Full Scope of Changes
1. [Component 1] - [why it needs changes]
2. [Component 2] - [why it needs changes]

## Clarifying Questions

Before I draft a plan, I need to understand:

1. [Question about scope]
2. [Question about behavior]
3. [Question about constraints]
4. [Question about testing expectations]
5. [Additional question if needed]
6. [Additional question if needed]

Please answer these questions so I can create a comprehensive plan tailored to your needs.
```

## Example Planner Mode Session

**User**: "I want to add a new method to ArbOwner precompile"

**Planner Mode Response**:

> ## Analysis
>
> I've examined the existing code:
> - Nitro: `arbos/precompiles/ArbOwner.go` has the canonical implementation
> - Nethermind: `Precompiles/ArbOwner.cs` and `ArbOwnerParser.cs`
>
> ## Clarifying Questions
>
> 1. Which specific method do you want to add?
> 2. Should gas costs exactly match Nitro?
> 3. Does this method modify state, or is it read-only?
> 4. Are there access control requirements (owner-only)?
> 5. Should I include test coverage for error cases?
>
> Once you answer these, I'll draft a detailed implementation plan.

## Begin Planner Mode

Analyze the requested feature/change and begin with Phase 1: Deep Analysis of **${1}**

After analysis, proceed to Phase 2: Ask your clarifying questions.
