#!/bin/bash
# Test script for Playwright authentication handling

set +e  # Don't exit on error, we want to see all test results

echo "🧪 Testing Playwright Authentication Handling"
echo "=============================================="
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

PASSED=0
FAILED=0

print_result() {
    local test_name=$1
    local result=$2
    
    if [ "$result" -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $test_name"
        ((PASSED++))
    else
        echo -e "${RED}✗${NC} $test_name"
        ((FAILED++))
    fi
}

echo "Test 1: Authentication Documentation"
echo "------------------------------------"

# Check that script has authentication requirements documented
if grep -q "GITHUB_COOKIE_USER_SESSION - GitHub user_session cookie (REQUIRED for browser auth)" "$SCRIPT_DIR/assign-copilot-via-ui.js" 2>/dev/null; then
    print_result "Script documents cookie requirement" 0
else
    print_result "Script documents cookie requirement" 1
fi

# Check that script has instructions to obtain cookie
if grep -q "To obtain the cookie:" "$SCRIPT_DIR/assign-copilot-via-ui.js" 2>/dev/null; then
    print_result "Script provides cookie instructions" 0
else
    print_result "Script provides cookie instructions" 1
fi

# Check that script documents PAT token limitation
if grep -q "GitHub PAT tokens CANNOT be used for browser authentication" "$SCRIPT_DIR/assign-copilot-via-ui.js" 2>/dev/null; then
    print_result "Script documents PAT token limitation" 0
else
    print_result "Script documents PAT token limitation" 1
fi

echo ""

echo "Test 2: Script Authentication Check"
echo "-----------------------------------"

# Check that script validates authentication at startup
if grep -q "if (!githubCookie)" "$SCRIPT_DIR/assign-copilot-via-ui.js" 2>/dev/null; then
    print_result "Script checks for session cookie" 0
else
    print_result "Script checks for session cookie" 1
fi

# Check that script exits early without cookie
if grep -q "Skipping Playwright UI automation - will use API fallback instead" "$SCRIPT_DIR/assign-copilot-via-ui.js" 2>/dev/null; then
    print_result "Script exits early without cookie" 0
else
    print_result "Script exits early without cookie" 1
fi

# Check that script provides helpful message
if grep -q "To enable browser authentication, provide GITHUB_COOKIE_USER_SESSION:" "$SCRIPT_DIR/assign-copilot-via-ui.js" 2>/dev/null; then
    print_result "Script provides helpful setup message" 0
else
    print_result "Script provides helpful setup message" 1
fi

echo ""

echo "Test 3: Workflow Configuration"
echo "------------------------------"

# Check workflow passes cookie as env var
if grep -q "GITHUB_COOKIE_USER_SESSION: \${{ secrets.GITHUB_COOKIE_USER_SESSION }}" "$SCRIPT_DIR/../workflows/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow passes cookie secret" 0
else
    print_result "Workflow passes cookie secret" 1
fi

# Check workflow has informational messages
if grep -q "Playwright UI automation requires GITHUB_COOKIE_USER_SESSION secret" "$SCRIPT_DIR/../workflows/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow has informational messages" 0
else
    print_result "Workflow has informational messages" 1
fi

echo ""

echo "Test 4: Run Script Without Cookie"
echo "---------------------------------"

# Test that script exits gracefully without cookie
cd "$SCRIPT_DIR"
output=$(GITHUB_TOKEN="test_token" node assign-copilot-via-ui.js test repo 1 copilot 2>&1)
exit_code=$?

if echo "$output" | grep -q "GITHUB_COOKIE_USER_SESSION not provided"; then
    print_result "Script logs missing cookie" 0
else
    print_result "Script logs missing cookie" 1
fi

if echo "$output" | grep -q "No GitHub session cookie available"; then
    print_result "Script detects missing authentication" 0
else
    print_result "Script detects missing authentication" 1
fi

if echo "$output" | grep -q "Skipping Playwright UI automation"; then
    print_result "Script skips automation without cookie" 0
else
    print_result "Script skips automation without cookie" 1
fi

if [ $exit_code -ne 0 ]; then
    print_result "Script exits with error code" 0
else
    print_result "Script exits with error code" 1
fi

echo ""

echo "Test 5: Documentation Updates"
echo "-----------------------------"

PROJECT_ROOT="$SCRIPT_DIR/../.."

# Check main documentation mentions authentication requirement
if grep -q "GITHUB_COOKIE_USER_SESSION" "$PROJECT_ROOT/docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md" 2>/dev/null; then
    print_result "Playwright docs mention cookie requirement" 0
else
    print_result "Playwright docs mention cookie requirement" 1
fi

# Check docs explain how to obtain cookie
if grep -q "How to obtain a session cookie" "$PROJECT_ROOT/docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md" 2>/dev/null; then
    print_result "Docs explain how to obtain cookie" 0
else
    print_result "Docs explain how to obtain cookie" 1
fi

# Check docs explain API fallback is default
if grep -q "Default Behavior" "$PROJECT_ROOT/docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md" 2>/dev/null; then
    print_result "Docs explain default behavior" 0
else
    print_result "Docs explain default behavior" 1
fi

# Check automated dev cycle docs mention optional nature
if grep -q "Optional" "$PROJECT_ROOT/docs/AUTOMATED_DEVELOPMENT_CYCLE.md" 2>/dev/null; then
    print_result "Dev cycle docs note Playwright is optional" 0
else
    print_result "Dev cycle docs note Playwright is optional" 1
fi

echo ""

# Summary
echo "=============================================="
echo "Test Results Summary"
echo "=============================================="
echo -e "${GREEN}Passed: $PASSED${NC}"
if [ $FAILED -gt 0 ]; then
    echo -e "${RED}Failed: $FAILED${NC}"
else
    echo -e "${GREEN}Failed: $FAILED${NC}"
fi
echo ""

if [ $FAILED -gt 0 ]; then
    echo -e "${YELLOW}⚠️  Some authentication tests failed.${NC}"
    exit 1
else
    echo -e "${GREEN}✓ All authentication tests passed!${NC}"
    exit 0
fi
