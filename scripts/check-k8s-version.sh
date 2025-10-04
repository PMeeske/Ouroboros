#!/bin/bash
# Kubernetes Version Compatibility Check Script
# Validates Kubernetes version configuration across all environments

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TERRAFORM_DIR="$PROJECT_ROOT/terraform"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Counters
CHECKS_PASSED=0
CHECKS_FAILED=0
CHECKS_WARNING=0

print_header() {
    echo -e "${BLUE}=== $1 ===${NC}"
}

check_pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((CHECKS_PASSED++))
}

check_fail() {
    echo -e "${RED}✗${NC} $1"
    ((CHECKS_FAILED++))
}

check_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
    ((CHECKS_WARNING++))
}

validate_version() {
    local version=$1
    local min_major=1
    local min_minor=29
    
    # Extract major and minor version
    if [[ $version =~ ^([0-9]+)\.([0-9]+) ]]; then
        major="${BASH_REMATCH[1]}"
        minor="${BASH_REMATCH[2]}"
        
        if [ "$major" -gt "$min_major" ] || ([ "$major" -eq "$min_major" ] && [ "$minor" -ge "$min_minor" ]); then
            return 0
        fi
    fi
    return 1
}

echo ""
print_header "Kubernetes Version Compatibility Check"
echo ""

# Check 1: Terraform directory exists
if [ -d "$TERRAFORM_DIR" ]; then
    check_pass "Terraform directory found"
else
    check_fail "Terraform directory not found"
    exit 1
fi

# Check 2: Main variables.tf
echo ""
print_header "Main Configuration"
VAR_FILE="$TERRAFORM_DIR/variables.tf"

if [ -f "$VAR_FILE" ]; then
    check_pass "variables.tf exists"
    
    # Extract default version
    default_version=$(grep -A 1 'variable "k8s_version"' "$VAR_FILE" | grep 'default' | sed 's/.*"\(.*\)".*/\1/')
    
    if [ -n "$default_version" ]; then
        echo "  Default version: $default_version"
        
        if validate_version "$default_version"; then
            check_pass "Default version is 1.29+ (recommended)"
        else
            check_fail "Default version is below 1.29 (deprecated)"
        fi
    else
        check_warn "Could not extract default version"
    fi
    
    # Check for validation block
    if grep -q "validation {" "$VAR_FILE"; then
        check_pass "Validation block present"
    else
        check_warn "No validation block (consider adding)"
    fi
else
    check_fail "variables.tf not found"
fi

# Check 3: Environment configurations
echo ""
print_header "Environment Configurations"

environments=("dev" "staging" "production")

for env in "${environments[@]}"; do
    env_file="$TERRAFORM_DIR/environments/${env}.tfvars"
    
    echo ""
    echo "Checking $env environment..."
    
    if [ -f "$env_file" ]; then
        check_pass "$env.tfvars exists"
        
        # Extract version
        env_version=$(grep 'k8s_version' "$env_file" | sed 's/.*"\(.*\)".*/\1/')
        
        if [ -n "$env_version" ]; then
            echo "  Version: $env_version"
            
            if validate_version "$env_version"; then
                check_pass "$env version is 1.29+ (recommended)"
            else
                check_fail "$env version is below 1.29 (deprecated)"
            fi
        else
            check_fail "Could not extract version from $env"
        fi
    else
        check_warn "$env.tfvars not found"
    fi
done

# Check 4: Module configuration
echo ""
print_header "Kubernetes Module Configuration"

MODULE_VAR_FILE="$TERRAFORM_DIR/modules/kubernetes/variables.tf"

if [ -f "$MODULE_VAR_FILE" ]; then
    check_pass "Module variables.tf exists"
    
    if grep -q "validation {" "$MODULE_VAR_FILE"; then
        check_pass "Module has validation block"
    else
        check_warn "Module has no validation block"
    fi
else
    check_warn "Module variables.tf not found"
fi

# Check 5: Kubernetes manifests API versions
echo ""
print_header "Kubernetes Manifests Compatibility"

K8S_DIR="$PROJECT_ROOT/k8s"

if [ -d "$K8S_DIR" ]; then
    check_pass "Kubernetes manifests directory exists"
    
    # Check for deprecated API versions
    deprecated_apis=(
        "extensions/v1beta1"
        "apps/v1beta1"
        "apps/v1beta2"
        "networking.k8s.io/v1beta1"
    )
    
    deprecated_found=false
    for api in "${deprecated_apis[@]}"; do
        if grep -r "apiVersion: $api" "$K8S_DIR" >/dev/null 2>&1; then
            check_fail "Deprecated API version found: $api"
            deprecated_found=true
        fi
    done
    
    if [ "$deprecated_found" = false ]; then
        check_pass "No deprecated API versions in manifests"
    fi
    
    # Check current API versions
    echo "  API versions in use:"
    grep -h "apiVersion:" "$K8S_DIR"/*.yaml 2>/dev/null | sort | uniq | while read -r line; do
        echo "    $line"
    done
else
    check_warn "Kubernetes manifests directory not found"
fi

# Check 6: Documentation
echo ""
print_header "Documentation"

DOCS_DIR="$PROJECT_ROOT/docs"

if [ -f "$DOCS_DIR/K8S_VERSION_COMPATIBILITY.md" ]; then
    check_pass "K8S_VERSION_COMPATIBILITY.md exists"
else
    check_warn "K8S_VERSION_COMPATIBILITY.md not found"
fi

if grep -r "k8s.*version\|kubernetes.*version" "$DOCS_DIR"/*.md >/dev/null 2>&1; then
    check_pass "Documentation mentions Kubernetes versions"
else
    check_warn "Limited version documentation found"
fi

# Summary
echo ""
print_header "Summary"
echo ""

TOTAL_CHECKS=$((CHECKS_PASSED + CHECKS_FAILED + CHECKS_WARNING))

echo -e "${GREEN}Passed:   $CHECKS_PASSED${NC}"
echo -e "${RED}Failed:   $CHECKS_FAILED${NC}"
echo -e "${YELLOW}Warnings: $CHECKS_WARNING${NC}"
echo ""
echo "Total checks: $TOTAL_CHECKS"
echo ""

# Recommendations
if [ $CHECKS_FAILED -gt 0 ]; then
    echo -e "${RED}⚠ Validation failed with $CHECKS_FAILED errors${NC}"
    echo ""
    echo "Recommended actions:"
    echo "  1. Update Kubernetes versions to 1.29 or higher"
    echo "  2. Fix deprecated API versions in manifests"
    echo "  3. Add validation blocks to Terraform variables"
    echo "  4. Review docs/K8S_VERSION_COMPATIBILITY.md"
    echo ""
    exit 1
elif [ $CHECKS_WARNING -gt 0 ]; then
    echo -e "${YELLOW}⚠ Validation passed with $CHECKS_WARNING warnings${NC}"
    echo ""
    echo "Consider:"
    echo "  1. Review warnings above"
    echo "  2. Update documentation if needed"
    echo "  3. Add missing validation blocks"
    echo ""
    exit 0
else
    echo -e "${GREEN}✓ All checks passed!${NC}"
    echo ""
    echo "Kubernetes version configuration is valid and up to date."
    echo ""
    exit 0
fi
