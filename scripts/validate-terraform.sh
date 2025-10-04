#!/bin/bash
# Terraform Infrastructure Validation Script
# Validates Terraform configuration and IONOS Cloud setup
#
# Usage: ./validate-terraform.sh [environment]
#   environment: dev, staging, production (default: dev)

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TERRAFORM_DIR="$PROJECT_ROOT/terraform"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Counters
CHECKS_PASSED=0
CHECKS_FAILED=0
CHECKS_WARNING=0

# Functions
print_header() {
    echo ""
    echo -e "${BLUE}================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}================================================${NC}"
    echo ""
}

check_pass() {
    echo -e "${GREEN}✓ $1${NC}"
    ((CHECKS_PASSED++))
}

check_fail() {
    echo -e "${RED}✗ $1${NC}"
    ((CHECKS_FAILED++))
}

check_warn() {
    echo -e "${YELLOW}⚠ $1${NC}"
    ((CHECKS_WARNING++))
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# Parse environment
ENVIRONMENT="${1:-dev}"

if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    echo -e "${RED}Invalid environment: $ENVIRONMENT${NC}"
    echo "Valid environments: dev, staging, production"
    exit 1
fi

VAR_FILE="environments/${ENVIRONMENT}.tfvars"

print_header "Terraform Infrastructure Validation - $ENVIRONMENT"

# Check 1: Terraform installed
echo "Checking prerequisites..."
if command -v terraform &> /dev/null; then
    TF_VERSION=$(terraform version -json | grep -o '"version":"[^"]*' | cut -d'"' -f4)
    check_pass "Terraform installed: $TF_VERSION"
    
    # Check version
    REQUIRED_VERSION="1.5.0"
    if printf '%s\n' "$REQUIRED_VERSION" "$TF_VERSION" | sort -V -C; then
        check_pass "Terraform version meets requirements (>= $REQUIRED_VERSION)"
    else
        check_warn "Terraform version $TF_VERSION may be too old (recommended >= $REQUIRED_VERSION)"
    fi
else
    check_fail "Terraform is not installed"
    echo ""
    echo "Install from: https://www.terraform.io/downloads"
fi
echo ""

# Check 2: IONOS credentials
echo "Checking IONOS Cloud credentials..."
if [ -n "$IONOS_TOKEN" ]; then
    check_pass "IONOS_TOKEN environment variable set"
    
    # Test API connectivity
    response=$(curl -s -w "\n%{http_code}" -H "Authorization: Bearer $IONOS_TOKEN" \
        -H "Content-Type: application/json" \
        https://api.ionos.com/cloudapi/v6/ 2>/dev/null || echo "000")
    
    http_code=$(echo "$response" | tail -n1)
    
    if [ "$http_code" = "200" ]; then
        check_pass "IONOS Cloud API connection successful"
    elif [ "$http_code" = "401" ]; then
        check_fail "IONOS API authentication failed (invalid token)"
    else
        check_warn "Could not verify IONOS API connectivity (HTTP $http_code)"
    fi
    
elif [ -n "$IONOS_USERNAME" ] && [ -n "$IONOS_PASSWORD" ]; then
    check_pass "IONOS_USERNAME and IONOS_PASSWORD set"
    check_warn "Consider using IONOS_TOKEN instead of username/password for better security"
else
    check_fail "No IONOS credentials found"
    echo ""
    print_info "Set IONOS_TOKEN (recommended):"
    echo "  export IONOS_TOKEN=\"your-token\""
    echo ""
    print_info "Or set IONOS_USERNAME and IONOS_PASSWORD:"
    echo "  export IONOS_USERNAME=\"your-username\""
    echo "  export IONOS_PASSWORD=\"your-password\""
fi
echo ""

# Check 3: Terraform directory structure
echo "Checking Terraform directory structure..."
cd "$TERRAFORM_DIR"

required_files=(
    "main.tf"
    "variables.tf"
    "outputs.tf"
    "$VAR_FILE"
)

for file in "${required_files[@]}"; do
    if [ -f "$file" ]; then
        check_pass "Found: $file"
    else
        check_fail "Missing: $file"
    fi
done
echo ""

# Check 4: Module structure
echo "Checking Terraform modules..."
modules=(
    "modules/datacenter"
    "modules/kubernetes"
    "modules/registry"
    "modules/storage"
    "modules/networking"
)

for module in "${modules[@]}"; do
    if [ -d "$module" ]; then
        check_pass "Module exists: $module"
        
        # Check module files
        if [ -f "$module/main.tf" ] && [ -f "$module/variables.tf" ] && [ -f "$module/outputs.tf" ]; then
            check_pass "Module $module has required files"
        else
            check_warn "Module $module missing some required files"
        fi
    else
        check_fail "Module missing: $module"
    fi
done
echo ""

# Check 5: Terraform formatting
echo "Checking Terraform formatting..."
if command -v terraform &> /dev/null; then
    if terraform fmt -check -recursive > /dev/null 2>&1; then
        check_pass "Terraform files are properly formatted"
    else
        check_warn "Some Terraform files need formatting (run: terraform fmt -recursive)"
    fi
else
    check_warn "Cannot check formatting (Terraform not installed)"
fi
echo ""

# Check 6: Terraform validation
echo "Checking Terraform configuration validity..."
if command -v terraform &> /dev/null; then
    # Initialize if needed
    if [ ! -d ".terraform" ]; then
        print_info "Initializing Terraform..."
        if terraform init > /dev/null 2>&1; then
            check_pass "Terraform initialized successfully"
        else
            check_warn "Terraform initialization had warnings"
            echo ""
            print_info "Initialization output:"
            terraform init
        fi
    else
        check_pass "Terraform already initialized"
    fi
    
    # Validate
    if terraform validate > /dev/null 2>&1; then
        check_pass "Terraform configuration is valid"
    else
        check_fail "Terraform validation failed"
        echo ""
        print_info "Validation errors:"
        terraform validate
    fi
else
    check_warn "Cannot validate (Terraform not installed)"
fi
echo ""

# Check 7: Environment-specific configuration
echo "Checking environment configuration: $ENVIRONMENT"
if [ -f "$VAR_FILE" ]; then
    check_pass "Environment file exists: $VAR_FILE"
    
    # Parse key settings
    if grep -q "environment.*=.*\"$ENVIRONMENT\"" "$VAR_FILE"; then
        check_pass "Environment variable correctly set"
    else
        check_warn "Environment variable may not match file name"
    fi
    
    # Check for common settings
    settings=(
        "datacenter_name"
        "cluster_name"
        "node_count"
        "registry_name"
    )
    
    for setting in "${settings[@]}"; do
        if grep -q "^[[:space:]]*$setting[[:space:]]*=" "$VAR_FILE"; then
            check_pass "Setting configured: $setting"
        else
            check_warn "Setting not found: $setting (may use default)"
        fi
    done
else
    check_fail "Environment file missing: $VAR_FILE"
fi
echo ""

# Check 8: GitHub Actions workflow
echo "Checking GitHub Actions workflow..."
WORKFLOW_FILE="$PROJECT_ROOT/.github/workflows/terraform-infrastructure.yml"
if [ -f "$WORKFLOW_FILE" ]; then
    check_pass "Terraform workflow file exists"
    
    # Check for required steps
    if grep -q "terraform init" "$WORKFLOW_FILE"; then
        check_pass "Workflow includes terraform init"
    fi
    
    if grep -q "terraform plan" "$WORKFLOW_FILE"; then
        check_pass "Workflow includes terraform plan"
    fi
    
    if grep -q "terraform apply" "$WORKFLOW_FILE"; then
        check_pass "Workflow includes terraform apply"
    fi
else
    check_warn "GitHub Actions workflow not found"
fi
echo ""

# Summary
print_header "Validation Summary"

TOTAL_CHECKS=$((CHECKS_PASSED + CHECKS_FAILED + CHECKS_WARNING))

echo -e "${GREEN}Passed:  $CHECKS_PASSED${NC}"
echo -e "${RED}Failed:  $CHECKS_FAILED${NC}"
echo -e "${YELLOW}Warnings: $CHECKS_WARNING${NC}"
echo ""
echo "Total checks: $TOTAL_CHECKS"
echo ""

# Recommendations
if [ $CHECKS_FAILED -gt 0 ]; then
    echo -e "${RED}⚠ Validation failed with $CHECKS_FAILED errors${NC}"
    echo ""
    echo "Recommendations:"
    
    if ! command -v terraform &> /dev/null; then
        echo "  1. Install Terraform: https://www.terraform.io/downloads"
    fi
    
    if [ -z "$IONOS_TOKEN" ] && [ -z "$IONOS_USERNAME" ]; then
        echo "  2. Set IONOS Cloud credentials"
    fi
    
    echo "  3. Review failed checks above"
    echo "  4. See docs/IONOS_IAC_GUIDE.md for help"
    echo ""
    exit 1
    
elif [ $CHECKS_WARNING -gt 0 ]; then
    echo -e "${YELLOW}⚠ Validation completed with $CHECKS_WARNING warnings${NC}"
    echo ""
    echo "Your infrastructure configuration is functional but has some warnings."
    echo "Review the warnings above to optimize your setup."
    echo ""
    echo "Next steps:"
    echo "  1. Review warnings above"
    echo "  2. Run: terraform plan -var-file=$VAR_FILE"
    echo "  3. Run: terraform apply -var-file=$VAR_FILE"
    echo ""
    
else
    echo -e "${GREEN}✓ All validation checks passed!${NC}"
    echo ""
    echo "Your Terraform infrastructure setup is ready to use."
    echo ""
    echo "Next steps:"
    echo "  1. Review the plan:"
    echo "     terraform plan -var-file=$VAR_FILE"
    echo ""
    echo "  2. Apply infrastructure:"
    echo "     terraform apply -var-file=$VAR_FILE"
    echo ""
    echo "  3. Or use the helper script:"
    echo "     ./scripts/manage-infrastructure.sh apply $ENVIRONMENT"
    echo ""
fi

# Quick start guide
if [ $CHECKS_FAILED -eq 0 ]; then
    echo "Quick start guide:"
    echo "  ./scripts/manage-infrastructure.sh plan $ENVIRONMENT"
    echo "  ./scripts/manage-infrastructure.sh apply $ENVIRONMENT"
    echo "  ./scripts/manage-infrastructure.sh kubeconfig $ENVIRONMENT"
    echo ""
    echo "Documentation:"
    echo "  - Quick Start: docs/IONOS_IAC_QUICKSTART.md"
    echo "  - Full Guide:  docs/IONOS_IAC_GUIDE.md"
    echo "  - Modules:     terraform/README.md"
fi

exit $CHECKS_FAILED
