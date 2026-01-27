# Claude Code Hooks

This directory contains hooks that execute at various points during Claude Code's operation.

## Available Hooks

### `pre-tool-use.sh`
**Trigger**: Before tool execution (Edit, Write, Grep, Glob, etc.)

**Purpose**: Suggests appropriate agents based on:
- File paths being edited (precompiles, ArbOS, execution)
- User message keywords (implement, validate, check, etc.)
- Tool being used

**Examples**:
- Editing `Precompiles/*.cs` → Suggests Precompile-Validator
- User says "implement feature" → Suggests checking Nitro first
- User mentions "state root" → Suggests State-Root-Debugger

### `session-start.sh`
**Trigger**: When a new Claude Code session starts

**Purpose**: Displays workspace information:
- Available repositories
- Configured agents
- Quick slash commands
- Usage reminders

## Hook Configuration

Hooks are automatically detected by Claude Code when placed in `.claude/hooks/` directory with execute permissions.

### Environment Variables Available

Hooks receive context via environment variables:
- `CLAUDE_PROJECT_DIR`: Project root directory
- `CLAUDE_TOOL_NAME`: Name of tool being executed (in pre-tool-use)
- `CLAUDE_FILE_PATH`: File path (for Edit/Write/Read operations)
- `CLAUDE_USER_MESSAGE`: User's message text
- `hook_event_name`: Name of the hook event

### Output Format

Hooks communicate with Claude Code via JSON on stdout:

```json
{
  "systemMessage": "Message to display to user or add as context"
}
```

## Creating New Hooks

Supported hook types:
- `pre-tool-use.sh` - Before tool execution
- `post-tool-use.sh` - After tool execution
- `user-prompt-submit.sh` - When user submits message
- `session-start.sh` - Session initialization
- `session-end.sh` - Session cleanup
- `stop.sh` - When conversation stops
- `subagent-stop.sh` - When subagent completes
- `pre-compact.sh` - Before conversation compaction

### Hook Template

```bash
#!/bin/bash
# Hook description

# Get context
VAR="${CLAUDE_VARIABLE:-default}"

# Your logic here
if [[ condition ]]; then
    echo '{"systemMessage": "Your message"}'
    exit 0
fi

# No action needed
exit 0
```

### Best Practices

1. **Exit Codes**:
   - `0`: Success (continue operation)
   - Non-zero: Block operation (use sparingly)

2. **Performance**:
   - Keep hooks fast (< 100ms)
   - Avoid network calls
   - Cache results if needed

3. **Messages**:
   - Be concise and actionable
   - Use emojis for visual distinction
   - Provide specific agent/command suggestions

4. **Safety**:
   - Don't modify project files
   - Don't make destructive changes
   - Test hooks thoroughly before committing

## Disabling Hooks

To temporarily disable all hooks:
```bash
# In settings.local.json
{
  "disableAllHooks": true
}
```

To disable specific hook:
```bash
# Rename or remove execute permission
chmod -x .claude/hooks/pre-tool-use.sh
```

## Debugging Hooks

Enable verbose output:
```bash
# Set in environment or settings
export ANTHROPIC_LOG=debug
```

Test hook manually:
```bash
cd .claude/hooks
CLAUDE_PROJECT_DIR=$(pwd) ./pre-tool-use.sh
```

## Integration with Agent Orchestration

These hooks work together with the agent system to provide:
- **Proactive agent suggestions**: Hook suggests agent → User/Claude invokes → Agent executes
- **File-based triggers**: Editing specific files auto-suggests validators
- **Keyword detection**: User message analysis for agent routing
- **Context injection**: Hooks add context for better agent selection

### Example Flow

```
1. User: "I want to implement ArbSys precompile"
2. pre-tool-use hook: Detects "implement" + "precompile"
3. Hook outputs: "Before implementing, check Nitro via Nitro-Source-Reader"
4. Claude sees suggestion → Invokes Task-Orchestrator
5. Task-Orchestrator → Routes to Nitro-Source-Reader
6. User gets Nitro reference → Implements
7. User edits Precompiles/ArbSys.cs
8. pre-tool-use hook: Detects precompile file edit
9. Hook outputs: "After editing, validate with Precompile-Validator"
10. Claude → Invokes Precompile-Validator
11. Validation complete → Report to user
```

## Troubleshooting

**Hook not executing?**
- Check execute permissions: `ls -l .claude/hooks/`
- Verify bash is available: `which bash`
- Check for syntax errors: `bash -n hook-name.sh`

**Hook output not visible?**
- Ensure valid JSON output
- Check quotes are properly escaped
- Verify exit code is 0

**Hook too slow?**
- Profile with `time ./hook-name.sh`
- Remove heavy operations
- Consider caching strategies

## Resources

- [Claude Code Hooks Documentation](https://docs.claude.com/claude-code/hooks)
- [Hook Examples](https://github.com/anthropics/claude-code-examples)
- Project: `.claude/project.yaml` for agent routing configuration
- Workspace: `.claude/workspace.yaml` for workspace-level settings
