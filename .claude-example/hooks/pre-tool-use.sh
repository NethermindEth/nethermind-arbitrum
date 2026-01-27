#!/bin/bash
# Pre-tool-use hook: Suggest agents before operations
# This hook analyzes tool usage and suggests appropriate agents

# Get tool decision info (if available from Claude Code)
TOOL_NAME="${CLAUDE_TOOL_NAME:-}"
FILE_PATH="${CLAUDE_FILE_PATH:-}"
USER_MESSAGE="${CLAUDE_USER_MESSAGE:-}"

# Function to output system message
output_message() {
    echo "{\"systemMessage\": \"$1\"}"
}

# Check if editing C# files - remind about style BEFORE editing
if [[ "$TOOL_NAME" == "Edit" || "$TOOL_NAME" == "Write" ]]; then
    # Test files - CRITICAL style reminder
    if [[ "$FILE_PATH" =~ Test.*\.cs$ ]]; then
        output_message "⚠️ CODE STYLE REMINDER: Test naming MUST follow 'MethodName_Condition_ExpectedResult' pattern. Use explicit types (no var). Use exact assertions (no GreaterThan/LessThan)."
        exit 0
    fi

    # Any Nethermind.Arbitrum C# file
    if [[ "$FILE_PATH" =~ Nethermind\.Arbitrum.*\.cs$ ]]; then
        if [[ "$FILE_PATH" =~ Precompiles/.*\.cs$ ]]; then
            output_message "⚠️ STYLE REMINDER: Use explicit types (no var). Member order: constants→static fields→instance fields→constructors→properties→methods. After edit, validate against Nitro."
            exit 0
        fi

        if [[ "$FILE_PATH" =~ Arbos/.*\.cs$ ]]; then
            output_message "⚠️ STYLE REMINDER: Use explicit types (no var). Member order: constants→static→instance→constructors→properties→methods. Consider Cross-Repo-Validator after."
            exit 0
        fi

        if [[ "$FILE_PATH" =~ Execution/Arbitrum.*\.cs$ ]]; then
            output_message "⚠️ STYLE REMINDER: Use explicit types (no var). Member order: constants→static→instance→constructors→properties→methods. Validate against Nitro after."
            exit 0
        fi

        # Generic C# file
        output_message "⚠️ STYLE REMINDER: Use explicit types (no var). Member order: constants→static fields→instance fields→constructors→properties→methods."
        exit 0
    fi
fi

# Suggest Task-Orchestrator for complex exploration
if [[ "$TOOL_NAME" == "Grep" || "$TOOL_NAME" == "Glob" ]]; then
    if [[ "$USER_MESSAGE" =~ (implement|validate|check|compare) ]]; then
        output_message "💡 HINT: This seems like a complex task. Consider using /orchestrate or invoking Task-Orchestrator to analyze and route to specialized agents."
        exit 0
    fi
fi

# Suggest Nitro-Source-Reader before implementation
if [[ "$USER_MESSAGE" =~ (implement|add|create|build) ]]; then
    if [[ "$USER_MESSAGE" =~ (precompile|ArbOS|feature) ]]; then
        output_message "⚠️ IMPORTANT: Before implementing, invoke Nitro-Source-Reader to understand the canonical Go implementation. Nitro is the source of truth."
        exit 0
    fi
fi

# Suggest State-Root-Debugger for test failures
if [[ "$USER_MESSAGE" =~ (state root|mismatch|diverge) ]]; then
    output_message "🔴 CRITICAL: State root issues detected. Immediately invoke State-Root-Debugger to identify the divergence point and root cause."
    exit 0
fi

# Suggest validators for validation requests
if [[ "$USER_MESSAGE" =~ (validate|check|verify|compare) ]]; then
    if [[ "$USER_MESSAGE" =~ precompile ]]; then
        output_message "✅ VALIDATION: Use Precompile-Validator to validate precompile implementation against Nitro."
        exit 0
    else
        output_message "✅ VALIDATION: Use Cross-Repo-Validator to validate implementation against Nitro source."
        exit 0
    fi
fi

# NEW: Suggest Commit-Search for history/canonical implementation requests
if [[ "$USER_MESSAGE" =~ (how was|original|canonical|upstream|when was.*added|find commits|git history) ]]; then
    output_message "🔍 HISTORY SEARCH: Use Commit-Search agent or /find-commits to locate canonical implementation in Nitro/Geth repos."
    exit 0
fi

# NEW: Suggest Visual-Explainer for explanation requests
if [[ "$USER_MESSAGE" =~ (explain|how does.*work|visualize|diagram|show.*flow|architecture) ]]; then
    if [[ "$USER_MESSAGE" =~ (complex|system|cross|interaction|component|flow) ]]; then
        output_message "📊 VISUALIZATION: Consider generating a Mermaid diagram with /visualize or Visual-Explainer agent for better understanding."
        exit 0
    fi
fi

# NEW: Suggest checking Nitro commits before implementing new features
if [[ "$USER_MESSAGE" =~ (implement|port|add.*feature|create.*new) ]]; then
    if [[ ! "$USER_MESSAGE" =~ (test|mock|stub) ]]; then
        output_message "💡 BEFORE IMPLEMENTING: Use /find-commits to find canonical implementation in Nitro, then @Nitro-Source-Reader to understand it."
        exit 0
    fi
fi

# NEW: Suggest Stylus-specific handling
if [[ "$USER_MESSAGE" =~ (stylus|wasm|arbitrator|hostio) ]]; then
    output_message "🦀 STYLUS: For Stylus/WASM features, check /find-commits stylus and verify host IO costs match Nitro."
    exit 0
fi

# Suggest debug-mode for debugging/investigation requests
if [[ "$USER_MESSAGE" =~ (debug|investigate|why is.*failing|not working|broken|error|exception|trace) ]]; then
    if [[ ! "$USER_MESSAGE" =~ (state root|mismatch) ]]; then
        output_message "🔍 DEBUGGING: Consider using /debug-mode for systematic issue investigation with hypothesis-driven debugging."
        exit 0
    fi
fi

# Suggest plan-mode for implementation requests
if [[ "$USER_MESSAGE" =~ (plan|design|how should I|implement.*feature|add.*new|help me.*implement) ]]; then
    if [[ "$USER_MESSAGE" =~ (complex|multi|several|multiple|architecture|structure) ]]; then
        output_message "📋 PLANNING: Consider using /plan-mode for structured implementation planning with clarifying questions and approval workflow."
        exit 0
    fi
fi

# Suggest design-feature for architecture/design requests
if [[ "$USER_MESSAGE" =~ (design|architect|structure|how to build|feature request|specification|spec) ]]; then
    output_message "🏗️ DESIGN: Consider using /design-feature for comprehensive feature design with architecture diagrams and module specifications."
    exit 0
fi

# No suggestion needed
exit 0
