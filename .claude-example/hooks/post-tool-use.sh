#!/bin/bash
# Post-tool-use hook: Suggest validation and style checks after edits
# This hook analyzes completed tool operations and suggests follow-up actions

# Get context from Claude Code
TOOL_NAME="${CLAUDE_TOOL_NAME:-}"
FILE_PATH="${CLAUDE_FILE_PATH:-}"
TOOL_RESULT="${CLAUDE_TOOL_RESULT:-success}"

# Function to output system message
output_message() {
    echo "{\"systemMessage\": \"$1\"}"
}

# Only trigger after successful Edit or Write operations
if [[ "$TOOL_RESULT" == "success" ]]; then
    if [[ "$TOOL_NAME" == "Edit" || "$TOOL_NAME" == "Write" ]]; then

        # Test files edited - CRITICAL style check
        if [[ "$FILE_PATH" =~ Test.*\.cs$ ]]; then
            output_message "📋 STYLE CHECK: Test file modified. Verify: (1) Test names follow 'MethodName_Condition_ExpectedResult' pattern (2) No 'var' keyword used (3) Exact assertions (no GreaterThan/LessThan). Run tests with 'dotnet test'."
            exit 0
        fi

        # Any C# file in Arbitrum - remind about style
        if [[ "$FILE_PATH" =~ Nethermind\.Arbitrum.*\.cs$ ]]; then
            # Precompile files - additional validation
            if [[ "$FILE_PATH" =~ Precompiles/.*\.cs$ ]]; then
                output_message "📋 STYLE CHECK: Verify member ordering (constants→static fields→instance fields→constructors→properties→methods) and no 'var' usage. Also validate against Nitro with /validate-precompile."
                exit 0
            fi

            # ArbOS files edited
            if [[ "$FILE_PATH" =~ Arbos/.*\.cs$ ]]; then
                output_message "📋 STYLE CHECK: Verify member ordering and no 'var' usage. Also validate against Nitro with Cross-Repo-Validator."
                exit 0
            fi

            # Execution files edited
            if [[ "$FILE_PATH" =~ Execution/.*\.cs$ ]]; then
                output_message "📋 STYLE CHECK: Verify member ordering and no 'var' usage. Also validate against Nitro with Cross-Repo-Validator."
                exit 0
            fi

            # EVM instruction files edited
            if [[ "$FILE_PATH" =~ Evm/.*\.cs$ ]]; then
                output_message "📋 STYLE CHECK: Verify member ordering and no 'var' usage. Consider running tests with @Nethermind-Tester."
                exit 0
            fi

            # Generic C# file reminder
            output_message "📋 STYLE CHECK: Verify (1) No 'var' keyword (2) Member ordering: constants→static→instance→constructors→properties→methods (3) Access modifiers ordered: public→internal→protected→private."
            exit 0
        fi
    fi
fi

# No suggestion needed
exit 0
