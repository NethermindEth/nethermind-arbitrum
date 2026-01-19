#!/bin/bash

# Test Naming Convention Checker
# Validates that test methods follow the pattern: SystemUnderTest_StateUnderTest_ExpectedBehavior
#
# Convention requirements:
#   - At least 3 parts separated by underscores
#   - Each part should be in PascalCase (starting with uppercase)
#   - Pattern: {SystemUnderTest}_{Condition/State}_{ExpectedBehavior}
#   - Example: ArbInfo_GetVersion_ReturnsCorrectVersion

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_DIR="${PROJECT_ROOT}/src/Nethermind.Arbitrum.Test"
MIN_PARTS=3

# Counters
total_tests=0
valid_tests=0
invalid_tests=0

# Arrays to store results
declare -a invalid_test_list

# Function to check if a string is valid PascalCase or starts with digit followed by PascalCase
# Examples: "GetBalance", "3Nodes", "100Blocks" are all valid
is_valid_part() {
    # Must not be empty
    [ -z "$1" ] && return 1
    # Allow: starts with uppercase letter OR starts with digit(s) followed by uppercase letter
    [[ "$1" =~ ^[A-Z] ]] || [[ "$1" =~ ^[0-9]+[A-Z] ]]
}

# Function to validate test name
validate_test_name() {
    local test_name="$1"
    local file="$2"
    local line_num="$3"

    # Split by underscore
    IFS='_' read -ra parts <<< "$test_name"
    local num_parts=${#parts[@]}

    # Check minimum parts
    if [ "$num_parts" -lt "$MIN_PARTS" ]; then
        return 1
    fi

    # Check each part is valid (PascalCase or numeric prefix)
    for part in "${parts[@]}"; do
        if ! is_valid_part "$part"; then
            return 1
        fi
    done

    return 0
}

# Function to extract test methods from a file
extract_tests_from_file() {
    local file="$1"
    local in_test_block=false
    local line_num=0

    while IFS= read -r line || [[ -n "$line" ]]; do
        ((line_num++))

        # Check for [Test] or [Test(...)] attribute
        if [[ "$line" =~ \[Test(\(.*\))?\] ]]; then
            in_test_block=true
            continue
        fi

        # Check for [TestCase] attribute (parameterized tests)
        if [[ "$line" =~ \[TestCase(\(.*\))?\] ]]; then
            in_test_block=true
            continue
        fi

        # If we're in a test block, look for the method definition
        if [ "$in_test_block" = true ]; then
            # Match public/private/protected async? void/Task MethodName(
            if [[ "$line" =~ (public|private|protected|internal)[[:space:]]+(static[[:space:]]+)?(async[[:space:]]+)?(void|Task)[[:space:]]+([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*\( ]]; then
                local method_name="${BASH_REMATCH[5]}"
                ((total_tests++))

                if validate_test_name "$method_name" "$file" "$line_num"; then
                    ((valid_tests++))
                else
                    ((invalid_tests++))
                    local relative_file="${file#$PROJECT_ROOT/}"
                    invalid_test_list+=("${relative_file}:${line_num}: ${method_name}")
                fi
                in_test_block=false
            # Reset if we hit another attribute or empty line
            elif [[ "$line" =~ ^\s*\[ ]] || [[ -z "${line// }" ]]; then
                : # Continue looking
            elif [[ ! "$line" =~ ^\s*$ ]] && [[ ! "$line" =~ ^\s*// ]]; then
                # If it's not whitespace or comment, and not a method, reset
                in_test_block=false
            fi
        fi
    done < "$file"
}

# Function to print usage
print_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -h, --help     Show this help message"
    echo "  -v, --verbose  Show all tests (valid and invalid)"
    echo "  -q, --quiet    Only output errors (for CI)"
    echo "  --fix          Show suggestions for fixing invalid names"
    echo ""
    echo "Test Naming Convention:"
    echo "  Pattern: SystemUnderTest_StateUnderTest_ExpectedBehavior"
    echo "  Example: ArbInfo_GetVersion_ReturnsCorrectVersion"
    echo ""
    echo "Requirements:"
    echo "  - At least 3 parts separated by underscores"
    echo "  - Each part must start with uppercase (PascalCase)"
}

# Parse arguments
VERBOSE=false
QUIET=false
SHOW_FIX=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            print_usage
            exit 0
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -q|--quiet)
            QUIET=true
            shift
            ;;
        --fix)
            SHOW_FIX=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            print_usage
            exit 1
            ;;
    esac
done

# Check if test directory exists
if [ ! -d "$TEST_DIR" ]; then
    echo -e "${RED}Error: Test directory not found: $TEST_DIR${NC}"
    exit 1
fi

# Header
if [ "$QUIET" = false ]; then
    echo "=========================================="
    echo "Test Naming Convention Checker"
    echo "=========================================="
    echo ""
    echo "Pattern: SystemUnderTest_StateUnderTest_ExpectedBehavior"
    echo "Example: ArbInfo_GetVersion_ReturnsCorrectVersion"
    echo ""
    echo "Scanning: $TEST_DIR"
    echo ""
fi

# Find and process all test files
while IFS= read -r -d '' file; do
    extract_tests_from_file "$file"
done < <(find "$TEST_DIR" -name "*Tests.cs" -type f -print0)

# Output results
if [ "$QUIET" = false ]; then
    echo "=========================================="
    echo "Results"
    echo "=========================================="
    echo ""
fi

if [ ${#invalid_test_list[@]} -gt 0 ]; then
    if [ "$QUIET" = false ]; then
        echo -e "${RED}Invalid test names found:${NC}"
        echo ""
    fi

    for entry in "${invalid_test_list[@]}"; do
        echo -e "${RED}✗${NC} $entry"

        if [ "$SHOW_FIX" = true ]; then
            # Extract method name and suggest fix
            method_name=$(echo "$entry" | sed 's/.*: //')
            echo -e "  ${YELLOW}Suggestion: Rename to follow pattern: SystemUnderTest_Condition_ExpectedBehavior${NC}"
            echo ""
        fi
    done

    if [ "$QUIET" = false ]; then
        echo ""
    fi
fi

# Summary
if [ "$QUIET" = false ]; then
    echo "=========================================="
    echo "Summary"
    echo "=========================================="
    echo ""
    echo "Total tests:   $total_tests"
    echo -e "Valid tests:   ${GREEN}$valid_tests${NC}"
    echo -e "Invalid tests: ${RED}$invalid_tests${NC}"
    echo ""
fi

# Exit with error if there are invalid tests
if [ "$invalid_tests" -gt 0 ]; then
    if [ "$QUIET" = false ]; then
        echo -e "${RED}FAILED: $invalid_tests test(s) do not follow the naming convention.${NC}"
        echo ""
        echo "Please rename the test methods to follow the pattern:"
        echo "  SystemUnderTest_StateUnderTest_ExpectedBehavior"
        echo ""
    fi
    exit 1
else
    if [ "$QUIET" = false ]; then
        echo -e "${GREEN}PASSED: All tests follow the naming convention.${NC}"
    fi
    exit 0
fi
