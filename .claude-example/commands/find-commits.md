---
name: find-commits
description: Search Nitro and Geth repos for commits related to a feature or bug
argument-hint: <feature, keyword, or file path>
---

Search git history in upstream repositories for commits related to the specified feature.

## Search Request: $ARGUMENTS

## Repositories to Search

1. **Nitro (Go)**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro`
   - Upstream: `https://github.com/OffchainLabs/nitro`

2. **go-ethereum (Arbitrum fork)**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/go-ethereum`
   - Origin: `https://github.com/OffchainLabs/go-ethereum`

## Search Strategy

### Step 1: Determine Search Type
Based on the request, choose appropriate search method:

| Request Type | Git Command |
|--------------|-------------|
| Feature name | `git log --grep="<keyword>"` |
| Function/code | `git log -S "<code>"` |
| File history | `git log --follow -- <file>` |
| Recent changes | `git log --since="6 months ago" -- <path>` |

### Step 2: Execute Searches

**In Nitro repo**:
```bash
cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
git log --all --oneline --grep="$ARGUMENTS" | head -20
# or
git log --all --oneline -S "$ARGUMENTS" -- <relevant-path> | head -20
```

**In go-ethereum submodule** (if EVM/gas related):
```bash
cd /Users/daniilankusin/GolandProjects/arbitrum-nitro/go-ethereum
git log --all --oneline --grep="$ARGUMENTS" | head -20
```

### Step 3: Get Commit Details
For important commits found:
```bash
git show <commit_hash> --stat
git show <commit_hash> -- <specific_file>
```

### Step 4: Search GitHub (if local search insufficient)
- Use `mcp__github__search_code` for code patterns
- Use `mcp__github__search_issues` for PRs: `"repo:OffchainLabs/nitro $ARGUMENTS is:pr"`

## Expected Output

```markdown
## Commit Search: $ARGUMENTS

### Search Summary
- Keywords: [what was searched]
- Paths: [directories searched]
- Commits found: [count]

### Nitro Repository

| Commit | Date | Message |
|--------|------|---------|
| abc123 | YYYY-MM-DD | Message summary |

**Key Commit**: `abc123`
[Full commit message]
Files changed: [list]

### go-ethereum Repository
[If applicable]

### Related PRs
- PR #123: [Title](URL) - [Summary of discussion]

### Implementation Insights
- [Key finding 1]
- [Key finding 2]

### Recommended Next Steps
1. Read specific commit for implementation details
2. Use @Nitro-Source-Reader to understand current code
3. Check if changes need porting to Nethermind
```

## Common Searches

| Search | Example |
|--------|---------|
| ArbOS feature | `/find-commits arbos50` |
| Precompile | `/find-commits ArbGasInfo` |
| Gas/pricing | `/find-commits l1pricing` |
| Stylus | `/find-commits stylus wasm` |
| Specific file | `/find-commits arbos/l1pricing/l1pricing.go` |
| Bug fix | `/find-commits fix overflow` |

## Remember
- Nitro commit messages often explain design rationale
- PRs contain discussion and alternative approaches considered
- Recent commits may indicate bug fixes to port
- Use specific paths to narrow results
