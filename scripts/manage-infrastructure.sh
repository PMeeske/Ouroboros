#!/bin/bash
# Helper script to manage IONOS infrastructure using Terraform
# This script wraps common Terraform commands for easier infrastructure management
#
# Prerequisites:
# - Terraform installed (>= 1.5.0)
# - IONOS Cloud credentials configured (IONOS_TOKEN or IONOS_USERNAME/IONOS_PASSWORD)
#
# Usage: ./manage-infrastructure.sh [command] [environment]
#   command: init, plan, apply, destroy, output
#   environment: dev, staging, production (default: dev)
#
# Examples:
#   ./manage-infrastructure.sh plan dev
#   ./manage-infrastructure.sh apply production
#   ./manage-infrastructure.sh output staging

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

# Functions
print_header() {
    echo -e "${BLUE}================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}================================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

check_prerequisites() {
    print_header "Checking Prerequisites"
    
    # Check Terraform
    if ! command -v terraform &> /dev/null; then
        print_error "Terraform is not installed"
        echo "Install from: https://www.terraform.io/downloads"
        exit 1
    fi
    
    local tf_version=$(terraform version -json | grep -o '"version":"[^"]*' | cut -d'"' -f4)
    print_success "Terraform installed: $tf_version"
    
    # Check IONOS credentials
    if [ -n "$IONOS_TOKEN" ]; then
        print_success "IONOS_TOKEN environment variable set"
    elif [ -n "$IONOS_USERNAME" ] && [ -n "$IONOS_PASSWORD" ]; then
        print_success "IONOS_USERNAME and IONOS_PASSWORD set"
    else
        print_error "IONOS credentials not found"
        echo ""
        echo "Set IONOS_TOKEN (recommended):"
        echo "  export IONOS_TOKEN=\"your-token\""
        echo ""
        echo "Or set IONOS_USERNAME and IONOS_PASSWORD:"
        echo "  export IONOS_USERNAME=\"your-username\""
        echo "  export IONOS_PASSWORD=\"your-password\""
        exit 1
    fi
    
    echo ""
}

show_usage() {
    cat << EOF
Ouroboros Infrastructure Management Script

Usage: $0 [command] [environment]

Commands:
  init        Initialize Terraform
  plan        Show infrastructure changes
  apply       Apply infrastructure changes
  destroy     Destroy infrastructure
  output      Show Terraform outputs
  kubeconfig  Save kubeconfig file
  help        Show this help message

Environments:
  dev         Development environment (minimal resources)
  staging     Staging environment (medium resources)
  production  Production environment (full resources)

Examples:
  $0 plan dev
  $0 apply production
  $0 output staging
  $0 kubeconfig production
  $0 destroy dev

Environment Variables:
  IONOS_TOKEN       IONOS API token (recommended)
  IONOS_USERNAME    IONOS username (alternative)
  IONOS_PASSWORD    IONOS password (alternative)
  AUTO_APPROVE      Auto-approve apply/destroy (yes/no, default: no)

EOF
}

# Parse arguments
COMMAND="${1:-help}"
ENVIRONMENT="${2:-dev}"
AUTO_APPROVE="${AUTO_APPROVE:-no}"

if [ "$COMMAND" == "help" ] || [ "$COMMAND" == "-h" ] || [ "$COMMAND" == "--help" ]; then
    show_usage
    exit 0
fi

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    print_error "Invalid environment: $ENVIRONMENT"
    echo "Valid environments: dev, staging, production"
    exit 1
fi

VAR_FILE="environments/${ENVIRONMENT}.tfvars"

# Check prerequisites
check_prerequisites

# Change to Terraform directory
cd "$TERRAFORM_DIR"

# Execute command
case "$COMMAND" in
    init)
        print_header "Initializing Terraform"
        terraform init
        print_success "Terraform initialized"
        ;;
        
    plan)
        print_header "Planning Infrastructure Changes - $ENVIRONMENT"
        terraform plan -var-file="$VAR_FILE"
        ;;
        
    apply)
        print_header "Applying Infrastructure Changes - $ENVIRONMENT"
        
        if [ "$AUTO_APPROVE" == "yes" ]; then
            terraform apply -var-file="$VAR_FILE" -auto-approve
        else
            terraform apply -var-file="$VAR_FILE"
        fi
        
        print_success "Infrastructure applied successfully"
        echo ""
        print_info "Get kubeconfig: $0 kubeconfig $ENVIRONMENT"
        ;;
        
    destroy)
        print_header "Destroying Infrastructure - $ENVIRONMENT"
        print_warning "This will DELETE all infrastructure resources!"
        
        if [ "$ENVIRONMENT" == "production" ]; then
            print_warning "You are about to destroy PRODUCTION infrastructure!"
            echo ""
            read -p "Type 'DELETE PRODUCTION' to confirm: " confirm
            if [ "$confirm" != "DELETE PRODUCTION" ]; then
                print_error "Destruction cancelled"
                exit 1
            fi
        fi
        
        if [ "$AUTO_APPROVE" == "yes" ]; then
            terraform destroy -var-file="$VAR_FILE" -auto-approve
        else
            terraform destroy -var-file="$VAR_FILE"
        fi
        
        print_success "Infrastructure destroyed"
        ;;
        
    output)
        print_header "Terraform Outputs - $ENVIRONMENT"
        terraform output
        ;;
        
    kubeconfig)
        print_header "Saving Kubeconfig - $ENVIRONMENT"
        
        KUBECONFIG_FILE="$TERRAFORM_DIR/kubeconfig-${ENVIRONMENT}.yaml"
        
        terraform output -raw k8s_kubeconfig > "$KUBECONFIG_FILE" 2>/dev/null || {
            print_error "Failed to get kubeconfig"
            print_info "Run 'terraform apply' first to create the infrastructure"
            exit 1
        }
        
        print_success "Kubeconfig saved: $KUBECONFIG_FILE"
        echo ""
        print_info "To use this kubeconfig:"
        echo "  export KUBECONFIG=$KUBECONFIG_FILE"
        echo "  kubectl get nodes"
        ;;
        
    *)
        print_error "Unknown command: $COMMAND"
        echo ""
        show_usage
        exit 1
        ;;
esac

echo ""
print_success "Operation completed successfully"
