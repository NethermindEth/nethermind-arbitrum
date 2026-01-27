---
name: workspace-status
description: Check overall health and sync status of the dual-client workspace
---

You are checking the health and sync status of the Arbitrum dual-client workspace.

## Task

Provide a comprehensive status report of both repositories and their synchronization.

## Steps

1. **Check both repository status**:
   ```bash
   # Nitro status
   cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
   echo "=== Nitro Repository ==="
   git status
   git branch --show-current
   git log --oneline -5

   # Nethermind status
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   echo "=== Nethermind Repository ==="
   git status
   git branch --show-current
   git log --oneline -5
   ```

2. **Check for uncommitted changes**:
   - List modified files in both repos
   - Check for untracked files
   - Note any stashed changes

3. **Run sync check** (abbreviated):
   ```bash
   cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
   git log --oneline --since="7 days ago" -- precompiles/ arbos/ | head -10
   ```

4. **Check test status**:
   ```bash
   # Nethermind test list
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   dotnet test --list-tests | grep -E "Precompile|ArbOS" | wc -l
   ```

5. **Check workspace configuration**:
   - Verify `.claude/workspace.yaml` exists
   - Check `.claude/project.yaml` is workspace-aware
   - Confirm all agents are available
   - List available commands

6. **Generate status report**:
   ```markdown
   ## Arbitrum Dual-Client Workspace Status
   **Report Date**: [timestamp]
   **User**: ${USER}
   **Workspace**: Nethermind (C#) + Nitro (Go)

   ---

   ### Repository Health

   #### Nitro (Source of Truth)
   - **Path**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro`
   - **Branch**: `master` (or current branch)
   - **Status**: ✅ Clean | ⚠️ Uncommitted changes | ❌ Issues
   - **Last Commit**: [hash] - [message] (X days ago)
   - **Uncommitted Files**: [count] files
     - [list if any]

   #### Nethermind (Implementation)
   - **Path**: `/Users/daniilankusin/RiderProjects/nethermind-arbitrum`
   - **Branch**: `daniil/feature/version-based-precompile-registration` (or current)
   - **Status**: ✅ Clean | ⚠️ Uncommitted changes | ❌ Issues
   - **Last Commit**: [hash] - [message] (X days ago)
   - **Uncommitted Files**: [count] files
     - [list if any]

   ---

   ### Synchronization Status

   **Last Sync Check**: [timestamp or "Not run recently"]

   **Nitro Updates (Last 7 Days)**:
   - ✅ No protocol-affecting changes
     OR
   - ⚠️ 2 precompile updates need Nethermind implementation:
     - `ArbWasm.extendProgramExpiry` (commit: abc123)
     - `ArbInfo.getVersion` parameter change (commit: def456)

   **Recommendation**: Run `/sync-check` for detailed analysis

   ---

   ### Test Coverage

   **Nethermind Tests**:
   - Total test count: [number]
   - Precompile tests: [number]
   - ArbOS tests: [number]
   - Recent failures: [count] (0 is good)

   **Recent Test Runs**:
   - Last run: [timestamp]
   - Status: ✅ All passing | ⚠️ Some failures | ❌ Build errors

   **Recommendation**: Run `dotnet test` to verify current status

   ---

   ### Workspace Configuration

   **Configuration Files**:
   - ✅ `.claude/workspace.yaml` - Exists and valid
   - ✅ `.claude/project.yaml` - Workspace-aware
   - ✅ `.claudeignore` - Configured for both repos

   **Agents Available**:
   - ✅ Repo-Navigator (project-level)
   - ✅ Nethermind-Builder (project-level)
   - ✅ Nethermind-Tester (project-level)
   - ✅ Nethermind-Docs (project-level)
   - ✅ Nitro-Docs (project-level)
   - ✅ Cross-Repo-Validator (workspace-level)
   - ✅ Nitro-Source-Reader (workspace-level)
   - ✅ Implementation-Mapper (workspace-level)
   - ✅ Precompile-Validator (workspace-level)
   - ✅ State-Root-Debugger (workspace-level)

   **Commands Available**:
   - ✅ `/compare-impl` - Compare implementations
   - ✅ `/nitro-lookup` - Lookup Nitro source
   - ✅ `/sync-check` - Check for updates
   - ✅ `/validate-precompile` - Validate precompiles
   - ✅ `/state-root-debug` - Debug mismatches
   - ✅ `/workspace-status` - This command

   ---

   ### Action Items

   **Immediate**:
   - [List any urgent issues]
   - Example: "Commit or stash Nethermind changes"
   - Example: "Run `/sync-check` - Nitro has 2 new protocol updates"

   **This Week**:
   - [List medium priority items]
   - Example: "Implement pending Nitro updates"
   - Example: "Run full test suite"

   **Future**:
   - [List low priority items]
   - Example: "Consider optimization from Nitro commit xyz"

   ---

   ### Recommendations

   ✅ **Workspace is healthy** - No immediate action needed
     OR
   ⚠️ **Action required**:
   1. [Priority 1 action]
   2. [Priority 2 action]
   3. [Priority 3 action]

   ### Quick Actions
   ```bash
   # Commit Nethermind changes
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   git add .
   git commit -m "your message"

   # Check sync status
   /sync-check

   # Run tests
   dotnet test

   # Compare implementation
   /compare-impl [component]
   ```

   ---
   **Next Status Check**: [Recommend frequency, e.g., "1 week from now" or "After next Nitro release"]
   ```

7. **Provide recommendations** based on findings:
   - If out of sync: "Run `/sync-check` immediately"
   - If uncommitted changes: "Commit or stash changes"
   - If tests failing: "Investigate test failures"
   - If clean: "Workspace is healthy"

8. **Suggest frequency**:
   - Run weekly for active development
   - Run after Nitro releases
   - Run before Nethermind releases
   - Run when issues occur

## Status Indicators

- ✅ **GREEN**: All good, no action needed
- ⚠️ **YELLOW**: Attention needed, non-critical
- ❌ **RED**: Critical issue, immediate action required

## Remember
- Workspace health is important for productivity
- Regular checks prevent issues
- Sync status ensures implementations match
- Early detection prevents state root mismatches
