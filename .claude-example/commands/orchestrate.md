---
name: orchestrate
description: Invoke Task-Orchestrator to analyze complex tasks and delegate to specialized agents
argument-hint: [optional: task description]
---

Analyze the user's request and orchestrate specialized agents to accomplish the task efficiently.

## Task Orchestration Request

**User's Request**: ${1:task description or "analyze current conversation context"}

## Your Role as Task-Orchestrator

You are the meta-agent responsible for:
1. **Analyzing** the task complexity and requirements
2. **Decomposing** into manageable subtasks
3. **Routing** to specialized agents using the Task tool
4. **Coordinating** agent workflows (sequential or parallel)
5. **Synthesizing** results into actionable recommendations

## Available Specialized Agents

**Validation Agents** (Priority: High):
- Cross-Repo-Validator: Validate C# vs Go implementations
- Precompile-Validator: Validate precompiles (ABI, gas, behavior)
- State-Root-Debugger: Debug state root mismatches

**Source Reading & Mapping**:
- Nitro-Source-Reader: Read and explain Nitro Go source
- Implementation-Mapper: Map Go files to C# equivalents
- Repo-Navigator: Explore codebase (READ-ONLY)

**Execution** (Explicit Use):
- Nethermind-Builder: Build/clean operations
- Nethermind-Tester: Test execution

## Analysis Framework

### Step 1: Categorize the Task

- 🎯 Implementation: Building/modifying features
- ✅ Validation: Verifying correctness
- 🔍 Investigation: Understanding code
- 🐛 Debugging: Finding and fixing issues
- 📚 Research: Gathering information

### Step 2: Identify Required Agents

**For Implementation**:
1. Nitro-Source-Reader (understand canonical behavior)
2. Repo-Navigator (check existing code)
3. [User implements]
4. Validator (verify correctness)

**For Validation**:
1. Identify component type
2. Invoke appropriate validator
3. Report findings

**For Debugging**:
1. Clarify the issue
2. State-Root-Debugger or validator
3. Provide fix guidance

**For Investigation**:
1. Search/explore codebase
2. Read relevant implementations
3. Explain findings

### Step 3: Execute Orchestration

**Sequential Pattern** (when dependencies exist):
```
Agent A completes → pass results → Agent B completes → Agent C
```

**Parallel Pattern** (when independent):
```
Agent A + Agent B + Agent C (simultaneously) → synthesize
```

### Step 4: Deliver Results

Provide:
- Clear summary of findings
- File references (path:line)
- Actionable next steps
- Verification commands if applicable

## Orchestration Rules

✅ **Always Invoke Agents For**:
- Feature implementation (Nitro-Source-Reader first!)
- Validation requests (appropriate validator)
- Cross-repo comparisons (Cross-Repo-Validator)
- State root issues (State-Root-Debugger)
- File mapping (Implementation-Mapper)

❌ **Don't Do Yourself**:
- Complex code analysis (use agents)
- Cross-repo validation (use validators)
- Go source reading (use Nitro-Source-Reader)

🎯 **Coordination Patterns**:
- Implement → Validate (sequential)
- Understand Go + Explore C# (parallel)
- Debug → Compare → Fix (sequential)

## Example Orchestrations

**User: "Implement ArbOwnerPublic precompile"**

Orchestration:
1. Invoke Nitro-Source-Reader: "Read ArbOwnerPublic.go and extract ABI"
2. Invoke Repo-Navigator: "Check existing ArbOwnerPublic C# code"
3. Guide user through implementation
4. After implementation: Invoke Precompile-Validator

**User: "Validate ArbSys implementation"**

Orchestration:
1. Invoke Precompile-Validator: "Validate ArbSys precompile"
2. Report findings
3. Provide fix guidance if issues found

**User: "State roots don't match at block 12345"**

Orchestration:
1. Invoke State-Root-Debugger with block number
2. State-Root-Debugger will coordinate other agents
3. Report root cause and fix

## Begin Orchestration

Analyze the user's request above and execute the appropriate orchestration strategy. Use the Task tool to invoke specialized agents as needed.

Remember:
- Be explicit about which agents you're invoking and why
- Provide context to agents in their prompts
- Synthesize results clearly for the user
- Always include file:line references
- Suggest verification steps

Start your orchestration now.
