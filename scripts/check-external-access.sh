#!/bin/bash
# Script to check external accessibility of IONOS infrastructure
# This script validates that the deployed infrastructure is accessible from outside
#
# Prerequisites:
# - Terraform state exists (infrastructure must be deployed)
# - kubectl installed (for Kubernetes checks)
# - curl installed (for connectivity tests)
#
# Usage: ./check-external-access.sh [environment]
#   environment: dev, staging, production (default: dev)
#
# Examples:
#   ./check-external-access.sh dev
#   ./check-external-access.sh production

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TERRAFORM_DIR="$PROJECT_ROOT/terraform"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Counters
CHECKS_PASSED=0
CHECKS_FAILED=0
CHECKS_WARNING=0

# Functions
print_header() {
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

# Parse arguments
ENVIRONMENT="${1:-dev}"

if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    echo -e "${RED}Invalid environment: $ENVIRONMENT${NC}"
    echo "Valid environments: dev, staging, production"
    exit 1
fi

print_header "External Accessibility Check - $ENVIRONMENT"

# Check prerequisites
echo "Checking prerequisites..."
if ! command -v terraform &> /dev/null; then
    check_fail "Terraform is not installed"
    echo ""
    echo "Install from: https://www.terraform.io/downloads"
    exit 1
fi
check_pass "Terraform is installed"

if ! command -v curl &> /dev/null; then
    check_warn "curl is not installed (some checks will be skipped)"
else
    check_pass "curl is installed"
fi

if ! command -v kubectl &> /dev/null; then
    check_warn "kubectl is not installed (Kubernetes checks will be skipped)"
else
    check_pass "kubectl is installed"
fi
echo ""

# Change to Terraform directory
cd "$TERRAFORM_DIR"

# Check if Terraform state exists
echo "Checking Terraform state..."
if [ ! -f "terraform.tfstate" ] && [ ! -f ".terraform/terraform.tfstate" ]; then
    check_fail "No Terraform state found"
    echo ""
    print_info "Infrastructure may not be deployed yet."
    print_info "Run: ./scripts/manage-infrastructure.sh apply $ENVIRONMENT"
    exit 1
fi
check_pass "Terraform state exists"
echo ""

# Get Terraform outputs
echo "Fetching infrastructure information..."
print_info "Running terraform output to get deployment details..."
echo ""

# Check if outputs are available
if ! terraform output > /dev/null 2>&1; then
    check_fail "Failed to get Terraform outputs"
    echo ""
    print_info "Try running: terraform refresh -var-file=environments/${ENVIRONMENT}.tfvars"
    exit 1
fi

# Get external access information
print_header "External Access Information"

# Registry accessibility
echo "Container Registry:"
REGISTRY_HOSTNAME=$(terraform output -raw registry_hostname 2>/dev/null || echo "N/A")
REGISTRY_LOCATION=$(terraform output -raw registry_location 2>/dev/null || echo "N/A")

if [ "$REGISTRY_HOSTNAME" != "N/A" ]; then
    check_pass "Registry Hostname: $REGISTRY_HOSTNAME"
    check_pass "Registry Location: $REGISTRY_LOCATION"
    
    # Test registry connectivity
    if command -v curl &> /dev/null; then
        echo ""
        print_info "Testing registry connectivity..."
        if curl -s --connect-timeout 5 -o /dev/null -w "%{http_code}" "https://$REGISTRY_HOSTNAME" | grep -q "^[2-4][0-9][0-9]$"; then
            check_pass "Registry is accessible from outside"
        else
            check_warn "Registry may not be accessible (HTTP check failed)"
            print_info "This might be expected if registry requires authentication"
        fi
    fi
else
    check_fail "Registry hostname not available"
fi
echo ""

# Kubernetes cluster accessibility
echo "Kubernetes Cluster:"
K8S_CLUSTER_NAME=$(terraform output -raw k8s_cluster_name 2>/dev/null || echo "N/A")
K8S_CLUSTER_STATE=$(terraform output -raw k8s_cluster_state 2>/dev/null || echo "N/A")
K8S_NODE_POOL_STATE=$(terraform output -raw k8s_node_pool_state 2>/dev/null || echo "N/A")

if [ "$K8S_CLUSTER_NAME" != "N/A" ]; then
    check_pass "Cluster Name: $K8S_CLUSTER_NAME"
    
    if [ "$K8S_CLUSTER_STATE" == "ACTIVE" ]; then
        check_pass "Cluster State: $K8S_CLUSTER_STATE"
    else
        check_warn "Cluster State: $K8S_CLUSTER_STATE (not ACTIVE)"
    fi
    
    if [ "$K8S_NODE_POOL_STATE" == "ACTIVE" ]; then
        check_pass "Node Pool State: $K8S_NODE_POOL_STATE"
    else
        check_warn "Node Pool State: $K8S_NODE_POOL_STATE (not ACTIVE)"
    fi
else
    check_fail "Kubernetes cluster name not available"
fi
echo ""

# Public IPs
echo "Public IP Addresses:"
PUBLIC_IPS=$(terraform output -json k8s_public_ips 2>/dev/null || echo "[]")

if [ "$PUBLIC_IPS" != "[]" ] && [ "$PUBLIC_IPS" != "null" ]; then
    echo "$PUBLIC_IPS" | jq -r '.[]' 2>/dev/null | while read -r ip; do
        if [ -n "$ip" ]; then
            check_pass "Node Public IP: $ip"
            
            # Test IP reachability
            if command -v curl &> /dev/null; then
                if ping -c 1 -W 2 "$ip" &> /dev/null; then
                    check_pass "IP $ip is reachable"
                else
                    check_warn "IP $ip is not responding to ping (may be expected)"
                fi
            fi
        fi
    done
else
    check_warn "No public IPs assigned to nodes"
    print_info "Nodes may be using private IPs only"
fi
echo ""

# API access configuration
echo "Kubernetes API Access:"
API_ALLOW_LIST=$(terraform output -json k8s_api_subnet_allow_list 2>/dev/null || echo "[]")

if [ "$API_ALLOW_LIST" != "[]" ] && [ "$API_ALLOW_LIST" != "null" ]; then
    echo "$API_ALLOW_LIST" | jq -r '.[]' 2>/dev/null | while read -r subnet; do
        if [ -n "$subnet" ]; then
            check_pass "Allowed subnet: $subnet"
        fi
    done
else
    check_warn "No API subnet allow list configured"
    print_info "API access may be restricted or open to all (check IONOS console)"
fi
echo ""

# Network configuration
echo "Network Configuration:"
LAN_PUBLIC=$(terraform output -raw lan_public 2>/dev/null || echo "N/A")
LAN_NAME=$(terraform output -raw lan_name 2>/dev/null || echo "N/A")

if [ "$LAN_PUBLIC" == "true" ]; then
    check_pass "LAN is public: $LAN_NAME"
elif [ "$LAN_PUBLIC" == "false" ]; then
    check_warn "LAN is private: $LAN_NAME"
    print_info "Private LANs require VPN or bastion host for external access"
else
    check_fail "LAN public status not available"
fi
echo ""

# Kubeconfig check
echo "Kubernetes Access Configuration:"
KUBECONFIG_FILE="$TERRAFORM_DIR/kubeconfig-${ENVIRONMENT}.yaml"

if [ -f "$KUBECONFIG_FILE" ]; then
    check_pass "Kubeconfig file exists: $KUBECONFIG_FILE"
    
    # Test kubectl access if kubectl is installed
    if command -v kubectl &> /dev/null; then
        print_info "Testing kubectl access..."
        if KUBECONFIG="$KUBECONFIG_FILE" kubectl cluster-info &> /dev/null; then
            check_pass "Kubernetes cluster is accessible via kubectl"
            
            # Get node information
            NODE_COUNT=$(KUBECONFIG="$KUBECONFIG_FILE" kubectl get nodes --no-headers 2>/dev/null | wc -l)
            if [ "$NODE_COUNT" -gt 0 ]; then
                check_pass "Cluster has $NODE_COUNT active node(s)"
            else
                check_warn "No active nodes found in cluster"
            fi
        else
            check_warn "Cannot access cluster via kubectl (may require network access)"
            print_info "Try: export KUBECONFIG=$KUBECONFIG_FILE && kubectl get nodes"
        fi
    fi
else
    check_warn "Kubeconfig file not found"
    print_info "Generate it with: ./scripts/manage-infrastructure.sh kubeconfig $ENVIRONMENT"
fi
echo ""

# Summary
print_header "Check Summary"
echo -e "${GREEN}Passed:  $CHECKS_PASSED${NC}"
echo -e "${YELLOW}Warnings: $CHECKS_WARNING${NC}"
echo -e "${RED}Failed:  $CHECKS_FAILED${NC}"
echo ""

if [ $CHECKS_FAILED -gt 0 ]; then
    print_info "Some checks failed. Review the errors above."
    echo ""
    echo "Common issues:"
    echo "  - Infrastructure not deployed: Run ./scripts/manage-infrastructure.sh apply $ENVIRONMENT"
    echo "  - Network restrictions: Check IONOS firewall and security group settings"
    echo "  - Cluster not ready: Wait for cluster and node pool to reach ACTIVE state"
    exit 1
elif [ $CHECKS_WARNING -gt 0 ]; then
    print_info "All critical checks passed, but there are warnings."
    echo ""
    echo "Recommendations:"
    echo "  - Review network configuration for optimal external access"
    echo "  - Consider configuring public IPs if external access is needed"
    echo "  - Check API subnet allow list for security best practices"
    exit 0
else
    print_header "Success!"
    echo "Infrastructure is properly configured for external access."
    echo ""
    print_info "Next steps:"
    echo "  - Deploy applications: kubectl apply -f k8s/"
    echo "  - Configure ingress for external access: See docs/IONOS_DEPLOYMENT_GUIDE.md"
    echo "  - Set up monitoring and logging"
    exit 0
fi
