#!/bin/bash
# Script to validate IONOS Cloud deployment prerequisites
# This script checks if all requirements are met before deploying to IONOS Cloud
#
# Usage: ./validate-ionos-prerequisites.sh [namespace]
#   namespace: Kubernetes namespace (default: monadic-pipeline)
#
# Examples:
#   ./validate-ionos-prerequisites.sh
#   ./validate-ionos-prerequisites.sh monadic-pipeline

set -uo pipefail

NAMESPACE="${1:-monadic-pipeline}"

echo "================================================"
echo "IONOS Cloud Prerequisites Validation"
echo "================================================"
echo ""
echo "Checking prerequisites for IONOS Cloud deployment..."
echo "Namespace: $NAMESPACE"
echo ""

EXIT_CODE=0

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_pass() {
    echo -e "${GREEN}✓${NC} $1"
}

print_fail() {
    echo -e "${RED}✗${NC} $1"
    EXIT_CODE=1
}

print_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# 1. Check kubectl
echo "1. Checking kubectl..."
if command -v kubectl &> /dev/null; then
    KUBECTL_VERSION=$(kubectl version --client --short 2>/dev/null | grep -oP 'v\d+\.\d+\.\d+' || echo "unknown")
    print_pass "kubectl is installed (version: $KUBECTL_VERSION)"
else
    print_fail "kubectl is not installed"
    echo "   Install from: https://kubernetes.io/docs/tasks/tools/"
fi
echo ""

# 2. Check Docker
echo "2. Checking Docker..."
if command -v docker &> /dev/null; then
    DOCKER_VERSION=$(docker --version 2>/dev/null | grep -oP '\d+\.\d+\.\d+' || echo "unknown")
    print_pass "Docker is installed (version: $DOCKER_VERSION)"
    
    # Check if Docker daemon is running
    if docker info &> /dev/null; then
        print_pass "Docker daemon is running"
    else
        print_warn "Docker daemon is not running"
        echo "   Start Docker daemon before building images"
    fi
else
    print_fail "Docker is not installed"
    echo "   Install from: https://docs.docker.com/get-docker/"
fi
echo ""

# 3. Check cluster connection
echo "3. Checking Kubernetes cluster connection..."
if kubectl cluster-info &> /dev/null; then
    CLUSTER_CONTEXT=$(kubectl config current-context 2>/dev/null || echo "unknown")
    print_pass "Connected to cluster: $CLUSTER_CONTEXT"
    
    # Check if it's an IONOS cluster (heuristic check)
    if echo "$CLUSTER_CONTEXT" | grep -iq "ionos"; then
        print_pass "Cluster appears to be an IONOS Managed Kubernetes cluster"
    else
        print_warn "Cluster context doesn't appear to be IONOS"
        echo "   Current context: $CLUSTER_CONTEXT"
        echo "   Ensure you're connected to your IONOS Managed Kubernetes cluster"
    fi
else
    print_fail "Cannot connect to Kubernetes cluster"
    echo "   Configure kubectl with your IONOS kubeconfig:"
    echo "   1. Download kubeconfig from IONOS Cloud Console (https://dcd.ionos.com)"
    echo "   2. export KUBECONFIG=/path/to/ionos-kubeconfig.yaml"
    echo "   3. kubectl cluster-info"
fi
echo ""

# 4. Check cluster resources
echo "4. Checking cluster resources..."
if kubectl cluster-info &> /dev/null; then
    # Check nodes
    NODE_COUNT=$(kubectl get nodes --no-headers 2>/dev/null | wc -l)
    if [ "$NODE_COUNT" -gt 0 ]; then
        print_pass "Cluster has $NODE_COUNT node(s)"
        
        # Check node resources
        NODE_INFO=$(kubectl top nodes 2>/dev/null || echo "")
        if [ -n "$NODE_INFO" ]; then
            print_pass "Metrics server is available"
        else
            print_warn "Metrics server not available (resource monitoring limited)"
        fi
    else
        print_warn "Unable to detect cluster nodes"
    fi
else
    print_warn "Skipping cluster resource checks (not connected)"
fi
echo ""

# 5. Check IONOS storage class
echo "5. Checking IONOS storage class..."
if kubectl get storageclass ionos-enterprise-ssd &> /dev/null; then
    print_pass "IONOS storage class 'ionos-enterprise-ssd' is available"
    
    # Check if it's default
    IS_DEFAULT=$(kubectl get storageclass ionos-enterprise-ssd -o jsonpath='{.metadata.annotations.storageclass\.kubernetes\.io/is-default-class}' 2>/dev/null)
    if [ "$IS_DEFAULT" = "true" ]; then
        print_pass "ionos-enterprise-ssd is the default storage class"
    fi
else
    print_fail "IONOS storage class 'ionos-enterprise-ssd' not found"
    echo "   Available storage classes:"
    kubectl get storageclass 2>&1 | sed 's/^/   /' || echo "   Unable to list storage classes"
    echo ""
    echo "   This will cause PVC provisioning to fail."
    echo "   Ensure you're connected to an IONOS Managed Kubernetes cluster."
fi
echo ""

# 6. Check namespace
echo "6. Checking namespace..."
if kubectl get namespace "$NAMESPACE" &> /dev/null; then
    print_pass "Namespace '$NAMESPACE' exists"
    
    # Check existing resources
    EXISTING_DEPLOYMENTS=$(kubectl get deployments -n "$NAMESPACE" --no-headers 2>/dev/null | wc -l)
    if [ "$EXISTING_DEPLOYMENTS" -gt 0 ]; then
        print_warn "Namespace already has $EXISTING_DEPLOYMENTS deployment(s)"
        echo "   Existing deployments will be updated"
    fi
else
    print_pass "Namespace '$NAMESPACE' will be created during deployment"
fi
echo ""

# 7. Check registry secret
echo "7. Checking IONOS registry secret..."
if kubectl get namespace "$NAMESPACE" &> /dev/null; then
    if kubectl get secret ionos-registry-secret -n "$NAMESPACE" &> /dev/null; then
        print_pass "Registry secret 'ionos-registry-secret' exists in namespace"
        
        # Check secret data
        SECRET_SERVER=$(kubectl get secret ionos-registry-secret -n "$NAMESPACE" -o jsonpath='{.data.\.dockerconfigjson}' 2>/dev/null | base64 -d 2>/dev/null | grep -oP '"https?://[^"]+' | head -1 | tr -d '"' || echo "")
        if [ -n "$SECRET_SERVER" ]; then
            print_pass "Secret configured for registry: $SECRET_SERVER"
        fi
    else
        print_warn "Registry secret 'ionos-registry-secret' not found"
        echo "   Will be created during deployment"
        echo "   Ensure IONOS_USERNAME and IONOS_PASSWORD are set or provided interactively"
    fi
else
    print_warn "Skipping secret check (namespace doesn't exist yet)"
fi
echo ""

# 8. Check environment variables
echo "8. Checking environment variables..."
if [ -n "${IONOS_USERNAME:-}" ] && [ -n "${IONOS_PASSWORD:-}" ]; then
    print_pass "IONOS_USERNAME and IONOS_PASSWORD are set"
elif [ -n "${IONOS_TOKEN:-}" ]; then
    print_pass "IONOS_TOKEN is set (preferred for CI/CD)"
else
    print_warn "IONOS credentials not set in environment"
    echo "   You'll be prompted for credentials during deployment"
    echo "   Or set: export IONOS_USERNAME=<username>"
    echo "           export IONOS_PASSWORD=<password>"
fi

IONOS_REGISTRY="${IONOS_REGISTRY:-adaptive-systems.cr.de-fra.ionos.com}"
print_pass "Registry URL: $IONOS_REGISTRY"
echo ""

# 9. Check network connectivity
echo "9. Checking network connectivity..."
if curl -s --connect-timeout 5 https://api.ionos.com &> /dev/null; then
    print_pass "Can reach IONOS Cloud API (api.ionos.com)"
else
    print_warn "Cannot reach IONOS Cloud API"
    echo "   This may indicate network connectivity issues"
fi

if curl -s --connect-timeout 5 "https://$IONOS_REGISTRY" &> /dev/null; then
    print_pass "Can reach IONOS Container Registry"
else
    print_warn "Cannot reach IONOS Container Registry"
    echo "   Registry: $IONOS_REGISTRY"
fi
echo ""

# 10. Summary
echo "================================================"
echo "Validation Summary"
echo "================================================"
echo ""

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✅ All critical prerequisites are met!${NC}"
    echo ""
    echo "You can proceed with deployment:"
    echo "  ./scripts/deploy-ionos.sh $NAMESPACE"
    echo ""
    echo "Or run diagnostics after deployment:"
    echo "  ./scripts/check-ionos-deployment.sh $NAMESPACE"
else
    echo -e "${RED}❌ Some critical prerequisites are missing${NC}"
    echo ""
    echo "Please address the issues marked with ✗ above before deploying."
    echo ""
    echo "For detailed setup instructions, see:"
    echo "  docs/IONOS_DEPLOYMENT_GUIDE.md"
fi
echo ""

exit $EXIT_CODE
