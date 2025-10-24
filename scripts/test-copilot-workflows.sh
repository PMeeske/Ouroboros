#!/bin/bash
# Test script to validate Copilot Development Loop workflows

# Don't exit on first error - we want to see all test results
set +e

echo "🧪 Testing Copilot Development Loop Workflows"
echo "=============================================="
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKFLOWS_DIR="$PROJECT_ROOT/.github/workflows"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counter
PASSED=0
FAILED=0

# Function to print test result
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

echo "Test 1: Validate YAML Syntax"
echo "----------------------------"
for workflow in "$WORKFLOWS_DIR"/copilot-*.yml; do
    if [ -f "$workflow" ]; then
        workflow_name=$(basename "$workflow")
        if python3 -c "import yaml; yaml.safe_load(open('$workflow'))" 2>/dev/null; then
            print_result "YAML syntax: $workflow_name" 0
        else
            print_result "YAML syntax: $workflow_name" 1
        fi
    fi
done
echo ""

echo "Test 2: Check Required Files Exist"
echo "----------------------------------"
required_files=(
    ".github/workflows/copilot-code-review.yml"
    ".github/workflows/copilot-issue-assistant.yml"
    ".github/workflows/copilot-continuous-improvement.yml"
    ".github/COPILOT_QUICKSTART.md"
    ".github/workflows/README_COPILOT.md"
    "docs/COPILOT_DEVELOPMENT_LOOP.md"
)

for file in "${required_files[@]}"; do
    if [ -f "$PROJECT_ROOT/$file" ]; then
        print_result "File exists: $file" 0
    else
        print_result "File exists: $file" 1
    fi
done
echo ""

echo "Test 3: Check Workflow Triggers"
echo "-------------------------------"
# Check code review workflow
if grep -q "pull_request:" "$WORKFLOWS_DIR/copilot-code-review.yml" 2>/dev/null; then
    print_result "Code review triggers on PR" 0
else
    print_result "Code review triggers on PR" 1
fi

# Check issue assistant workflow
if grep -q "issues:" "$WORKFLOWS_DIR/copilot-issue-assistant.yml" 2>/dev/null; then
    print_result "Issue assistant triggers on issues" 0
else
    print_result "Issue assistant triggers on issues" 1
fi

# Check continuous improvement workflow
if grep -q "schedule:" "$WORKFLOWS_DIR/copilot-continuous-improvement.yml" 2>/dev/null; then
    print_result "Continuous improvement has schedule" 0
else
    print_result "Continuous improvement has schedule" 1
fi

# Check automated development cycle workflow
if grep -q "schedule:" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Automated development cycle has schedule" 0
else
    print_result "Automated development cycle has schedule" 1
fi
echo ""

echo "Test 4: Check Workflow Permissions"
echo "----------------------------------"
workflows=("copilot-code-review.yml" "copilot-issue-assistant.yml" "copilot-continuous-improvement.yml")

for workflow in "${workflows[@]}"; do
    workflow_path="$WORKFLOWS_DIR/$workflow"
    
    # Check for permissions section
    if grep -q "permissions:" "$workflow_path" 2>/dev/null; then
        print_result "Permissions defined: $workflow" 0
        
        # Check for required permissions
        if grep -q "pull-requests: write\|issues: write" "$workflow_path" 2>/dev/null; then
            print_result "Write permissions configured: $workflow" 0
        else
            print_result "Write permissions configured: $workflow" 1
        fi
    else
        print_result "Permissions defined: $workflow" 1
    fi
done
echo ""

echo "Test 5: Check Documentation Links"
echo "---------------------------------"
# Check if documentation files reference each other
if grep -q "COPILOT_DEVELOPMENT_LOOP.md" "$PROJECT_ROOT/README.md" 2>/dev/null; then
    print_result "README references Copilot docs" 0
else
    print_result "README references Copilot docs" 1
fi

if grep -q "copilot-instructions.md" "$PROJECT_ROOT/.github/COPILOT_QUICKSTART.md" 2>/dev/null; then
    print_result "Quick start references instructions" 0
else
    print_result "Quick start references instructions" 1
fi

if grep -q "COPILOT_DEVELOPMENT_LOOP" "$PROJECT_ROOT/docs/README.md" 2>/dev/null; then
    print_result "Docs index includes Copilot guide" 0
else
    print_result "Docs index includes Copilot guide" 1
fi
echo ""

echo "Test 6: Check Workflow Job Names"
echo "--------------------------------"
# Check that jobs have descriptive names
workflows=("copilot-code-review.yml" "copilot-issue-assistant.yml" "copilot-continuous-improvement.yml")

for workflow in "${workflows[@]}"; do
    workflow_path="$WORKFLOWS_DIR/$workflow"
    
    if grep -q "name:.*Code Review\|name:.*Issue\|name:.*Improvement" "$workflow_path" 2>/dev/null; then
        print_result "Job names are descriptive: $workflow" 0
    else
        print_result "Job names are descriptive: $workflow" 1
    fi
done
echo ""

echo "Test 7: Simulate Workflow Pattern Checks"
echo "----------------------------------------"
# Test that the patterns we're checking for actually exist in the codebase
cd "$PROJECT_ROOT"

# Check for Result<T> usage
if find src -name "*.cs" -type f -exec grep -q "Result<" {} \; 2>/dev/null; then
    print_result "Codebase uses Result<T> pattern" 0
else
    print_result "Codebase uses Result<T> pattern" 1
fi

# Check for Option<T> usage
if find src -name "*.cs" -type f -exec grep -q "Option<" {} \; 2>/dev/null; then
    print_result "Codebase uses Option<T> pattern" 0
else
    print_result "Codebase uses Option<T> pattern" 1
fi

# Check for async/await patterns
if find src -name "*.cs" -type f -exec grep -q "async Task\|await " {} \; 2>/dev/null; then
    print_result "Codebase uses async/await" 0
else
    print_result "Codebase uses async/await" 1
fi
echo ""

echo "Test 8: Check Unassigned Issues Assignment Feature"
echo "--------------------------------------------------"
# Check that the automated development cycle includes the new job
if grep -q "assign-copilot-to-unassigned:" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Unassigned issues assignment job exists" 0
else
    print_result "Unassigned issues assignment job exists" 1
fi

# Check that workflow has the new input parameter
if grep -q "assign_unassigned:" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow has assign_unassigned input" 0
else
    print_result "Workflow has assign_unassigned input" 1
fi

# Check that the job scans for unassigned issues
if grep -q "listForRepo" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Job scans repository issues" 0
else
    print_result "Job scans repository issues" 1
fi

# Check that the job assigns copilot
if grep -q "addAssignees" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Job assigns copilot to issues" 0
else
    print_result "Job assigns copilot to issues" 1
fi
echo ""

echo "Test 9: Check Playwright Integration"
echo "------------------------------------"
# Check that Playwright script exists
if [ -f "$PROJECT_ROOT/.github/scripts/assign-copilot-via-ui.js" ]; then
    print_result "Playwright assignment script exists" 0
else
    print_result "Playwright assignment script exists" 1
fi

# Check that package.json exists for Playwright dependencies
if [ -f "$PROJECT_ROOT/.github/scripts/package.json" ]; then
    print_result "Playwright package.json exists" 0
else
    print_result "Playwright package.json exists" 1
fi

# Check that workflow uses Playwright
if grep -q "assign-copilot-via-ui.js" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow uses Playwright script" 0
else
    print_result "Workflow uses Playwright script" 1
fi

# Check that workflow has Playwright setup steps
if grep -q "Setup Node.js for Playwright" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow has Playwright setup" 0
else
    print_result "Workflow has Playwright setup" 1
fi

# Check that workflow installs Playwright
if grep -q "playwright install" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow installs Playwright browsers" 0
else
    print_result "Workflow installs Playwright browsers" 1
fi

# Check that workflow has fallback to API
if grep -q "Fallback - Assign via API" "$WORKFLOWS_DIR/copilot-automated-development-cycle.yml" 2>/dev/null; then
    print_result "Workflow has API fallback mechanism" 0
else
    print_result "Workflow has API fallback mechanism" 1
fi

# Check that .gitignore excludes node_modules
if grep -q "node_modules" "$PROJECT_ROOT/.gitignore" 2>/dev/null; then
    print_result ".gitignore excludes node_modules" 0
else
    print_result ".gitignore excludes node_modules" 1
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
    echo -e "${YELLOW}⚠️  Some tests failed. Please review the failures above.${NC}"
    exit 1
else
    echo -e "${GREEN}✓ All tests passed! Copilot workflows are ready.${NC}"
    exit 0
fi
