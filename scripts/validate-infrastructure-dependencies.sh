#!/bin/bash
# Infrastructure Dependency Validation Script
# Validates that all infrastructure dependencies are correctly configured

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
TERRAFORM_DIR="./terraform"
K8S_DIR="./k8s"
APPSETTINGS_FILE="./appsettings.Production.json"
VALIDATION_PASSED=true

echo "═══════════════════════════════════════════════════════════════"
echo "  Infrastructure Dependency Validation"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Function to print success
print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

# Function to print error
print_error() {
    echo -e "${RED}✗${NC} $1"
    VALIDATION_PASSED=false
}

# Function to print warning
print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Function to print section header
print_section() {
    echo ""
    echo "─────────────────────────────────────────────────────────────"
    echo "  $1"
    echo "─────────────────────────────────────────────────────────────"
}

# 1. Validate Terraform Configuration
print_section "1. Terraform Configuration"

if [ ! -d "$TERRAFORM_DIR" ]; then
    print_error "Terraform directory not found: $TERRAFORM_DIR"
else
    print_success "Terraform directory exists"
    
    # Check Terraform is installed
    if command -v terraform &> /dev/null; then
        TERRAFORM_VERSION=$(terraform version -json | jq -r '.terraform_version')
        print_success "Terraform installed (version: $TERRAFORM_VERSION)"
    else
        print_error "Terraform not installed"
    fi
    
    # Check terraform files exist
    if [ -f "$TERRAFORM_DIR/main.tf" ]; then
        print_success "main.tf exists"
    else
        print_error "main.tf not found"
    fi
    
    # Check environment files
    for env in dev staging production; do
        if [ -f "$TERRAFORM_DIR/environments/${env}.tfvars" ]; then
            print_success "Environment file exists: ${env}.tfvars"
        else
            print_warning "Environment file missing: ${env}.tfvars"
        fi
    done
    
    # Validate Terraform configuration
    if command -v terraform &> /dev/null; then
        cd "$TERRAFORM_DIR"
        if terraform init -backend=false &> /dev/null; then
            print_success "Terraform initialization successful"
            
            if terraform validate &> /dev/null; then
                print_success "Terraform configuration valid"
            else
                print_error "Terraform validation failed"
                terraform validate
            fi
        else
            print_error "Terraform initialization failed"
        fi
        cd ..
    fi
fi

# 2. Validate Kubernetes Manifests
print_section "2. Kubernetes Manifests"

if [ ! -d "$K8S_DIR" ]; then
    print_error "Kubernetes directory not found: $K8S_DIR"
else
    print_success "Kubernetes directory exists"
    
    # Check required manifests
    REQUIRED_MANIFESTS=(
        "namespace.yaml"
        "configmap.yaml"
        "secrets.yaml"
        "deployment.cloud.yaml"
        "webapi-deployment.cloud.yaml"
        "ollama.yaml"
        "qdrant.yaml"
    )
    
    for manifest in "${REQUIRED_MANIFESTS[@]}"; do
        if [ -f "$K8S_DIR/$manifest" ]; then
            print_success "Manifest exists: $manifest"
        else
            print_error "Manifest missing: $manifest"
        fi
    done
    
    # Validate K8s manifests if kubectl is available
    if command -v kubectl &> /dev/null; then
        print_success "kubectl installed"
        
        for manifest in "${REQUIRED_MANIFESTS[@]}"; do
            if [ -f "$K8S_DIR/$manifest" ]; then
                if kubectl apply --dry-run=client -f "$K8S_DIR/$manifest" &> /dev/null; then
                    print_success "Manifest valid: $manifest"
                else
                    print_error "Manifest validation failed: $manifest"
                fi
            fi
        done
    else
        print_warning "kubectl not installed, skipping manifest validation"
    fi
fi

# 3. Validate C# Configuration
print_section "3. C# Application Configuration"

if [ ! -f "$APPSETTINGS_FILE" ]; then
    print_error "appsettings.Production.json not found"
else
    print_success "appsettings.Production.json exists"
    
    # Check if jq is available for JSON parsing
    if command -v jq &> /dev/null; then
        print_success "jq available for JSON parsing"
        
        # Validate JSON structure
        if jq empty "$APPSETTINGS_FILE" 2> /dev/null; then
            print_success "Valid JSON format"
        else
            print_error "Invalid JSON format"
        fi
        
        # Check required configuration paths
        OLLAMA_ENDPOINT=$(jq -r '.Pipeline.LlmProvider.OllamaEndpoint // empty' "$APPSETTINGS_FILE")
        VECTOR_STORE=$(jq -r '.Pipeline.VectorStore.Type // empty' "$APPSETTINGS_FILE")
        
        if [ -n "$OLLAMA_ENDPOINT" ]; then
            print_success "LlmProvider.OllamaEndpoint configured: $OLLAMA_ENDPOINT"
        else
            print_error "LlmProvider.OllamaEndpoint not configured"
        fi
        
        if [ -n "$VECTOR_STORE" ]; then
            print_success "VectorStore.Type configured: $VECTOR_STORE"
        else
            print_error "VectorStore.Type not configured"
        fi
    else
        print_warning "jq not installed, skipping JSON validation"
    fi
fi

# 4. Validate Configuration Consistency
print_section "4. Configuration Consistency"

# Check Ollama endpoint consistency
if command -v jq &> /dev/null && [ -f "$APPSETTINGS_FILE" ] && [ -f "$K8S_DIR/configmap.yaml" ]; then
    CS_OLLAMA=$(jq -r '.Pipeline.LlmProvider.OllamaEndpoint // empty' "$APPSETTINGS_FILE")
    K8S_OLLAMA=$(grep -A 1 "OllamaEndpoint" "$K8S_DIR/configmap.yaml" | tail -1 | sed 's/.*: "\(.*\)".*/\1/' | xargs)
    
    if [ "$CS_OLLAMA" == "$K8S_OLLAMA" ]; then
        print_success "Ollama endpoint consistent between C# and K8s"
    else
        print_warning "Ollama endpoint mismatch:"
        echo "    C# config: $CS_OLLAMA"
        echo "    K8s ConfigMap: $K8S_OLLAMA"
    fi
fi

# 5. Validate Docker Configuration
print_section "5. Docker Configuration"

DOCKERFILES=("Dockerfile" "Dockerfile.webapi")
for dockerfile in "${DOCKERFILES[@]}"; do
    if [ -f "$dockerfile" ]; then
        print_success "Dockerfile exists: $dockerfile"
        
        # Validate Dockerfile syntax
        if docker build -f "$dockerfile" --target build -t test:validation . &> /dev/null; then
            print_success "Dockerfile build test passed: $dockerfile"
        else
            print_warning "Dockerfile build test failed: $dockerfile (may need dependencies)"
        fi
    else
        print_error "Dockerfile missing: $dockerfile"
    fi
done

# Check docker-compose files
COMPOSE_FILES=("docker-compose.yml" "docker-compose.dev.yml")
for compose in "${COMPOSE_FILES[@]}"; do
    if [ -f "$compose" ]; then
        print_success "Docker Compose file exists: $compose"
        
        # Validate compose file syntax if docker-compose is available
        if command -v docker-compose &> /dev/null; then
            if docker-compose -f "$compose" config &> /dev/null; then
                print_success "Docker Compose file valid: $compose"
            else
                print_error "Docker Compose file invalid: $compose"
            fi
        fi
    else
        print_error "Docker Compose file missing: $compose"
    fi
done

# 6. Validate Resource Requirements
print_section "6. Resource Requirements"

# Check if we can extract resource requirements
if [ -f "$K8S_DIR/deployment.cloud.yaml" ]; then
    # Extract resource limits from deployment
    if grep -q "limits:" "$K8S_DIR/deployment.cloud.yaml"; then
        print_success "Resource limits defined in deployment"
        
        # Show resource configuration
        echo "    Resource configuration:"
        grep -A 2 "limits:" "$K8S_DIR/deployment.cloud.yaml" | head -3
    else
        print_warning "No resource limits defined in deployment"
    fi
fi

# Check Terraform node sizing
if [ -f "$TERRAFORM_DIR/variables.tf" ]; then
    if grep -q "cores_count" "$TERRAFORM_DIR/variables.tf"; then
        print_success "Node sizing variables defined in Terraform"
    else
        print_error "Node sizing variables missing in Terraform"
    fi
fi

# 7. Validate Storage Configuration
print_section "7. Storage Configuration"

# Check Terraform storage volumes
if [ -f "$TERRAFORM_DIR/variables.tf" ]; then
    if grep -q "volumes" "$TERRAFORM_DIR/variables.tf"; then
        print_success "Storage volumes defined in Terraform"
        
        # Check for qdrant and ollama volumes
        if grep -q "qdrant-data" "$TERRAFORM_DIR/variables.tf"; then
            print_success "Qdrant storage volume defined"
        else
            print_warning "Qdrant storage volume not found"
        fi
        
        if grep -q "ollama-models" "$TERRAFORM_DIR/variables.tf"; then
            print_success "Ollama models storage volume defined"
        else
            print_warning "Ollama models storage volume not found"
        fi
    else
        print_error "Storage volumes not defined in Terraform"
    fi
fi

# Check K8s PVCs
if [ -f "$K8S_DIR/qdrant.yaml" ]; then
    if grep -q "PersistentVolumeClaim\|volumeClaimTemplates" "$K8S_DIR/qdrant.yaml"; then
        print_success "Qdrant PVC defined in Kubernetes"
    else
        print_warning "Qdrant PVC not found"
    fi
fi

if [ -f "$K8S_DIR/ollama.yaml" ]; then
    if grep -q "PersistentVolumeClaim\|volumeClaimTemplates" "$K8S_DIR/ollama.yaml"; then
        print_success "Ollama PVC defined in Kubernetes"
    else
        print_warning "Ollama PVC not found"
    fi
fi

# 8. Validate Network Configuration
print_section "8. Network Configuration"

# Check Terraform networking module
if [ -d "$TERRAFORM_DIR/modules/networking" ]; then
    print_success "Terraform networking module exists"
    
    if [ -f "$TERRAFORM_DIR/modules/networking/main.tf" ]; then
        print_success "Networking module main.tf exists"
    else
        print_error "Networking module main.tf missing"
    fi
else
    print_error "Terraform networking module missing"
fi

# Check K8s services
if [ -f "$K8S_DIR/deployment.cloud.yaml" ]; then
    if grep -q "kind: Service" "$K8S_DIR/deployment.cloud.yaml"; then
        print_success "Kubernetes services defined"
    else
        print_warning "No services found in deployment manifest"
    fi
fi

# 9. Validate Security Configuration
print_section "9. Security Configuration"

# Check secrets file
if [ -f "$K8S_DIR/secrets.yaml" ]; then
    print_success "Kubernetes secrets file exists"
    
    # Check for important secrets
    if grep -q "openai-api-key" "$K8S_DIR/secrets.yaml"; then
        print_success "OpenAI API key placeholder defined"
    fi
    
    if grep -q "vector-store-connection-string" "$K8S_DIR/secrets.yaml"; then
        print_success "Vector store connection string defined"
    fi
    
    # Warn about placeholder values
    if grep -q "your-openai-api-key-here" "$K8S_DIR/secrets.yaml"; then
        print_warning "Secrets contain placeholder values - update before deployment"
    fi
else
    print_error "Kubernetes secrets file missing"
fi

# Check Terraform registry token
if [ -f "$TERRAFORM_DIR/modules/registry/main.tf" ]; then
    if grep -q "ionoscloud_container_registry_token" "$TERRAFORM_DIR/modules/registry/main.tf"; then
        print_success "Container registry token configured in Terraform"
    else
        print_warning "Container registry token not found in Terraform"
    fi
fi

# 10. Validate CI/CD Configuration
print_section "10. CI/CD Configuration"

# Check GitHub workflows
if [ -d ".github/workflows" ]; then
    print_success "GitHub workflows directory exists"
    
    WORKFLOWS=("terraform-infrastructure.yml" "ionos-deploy.yml")
    for workflow in "${WORKFLOWS[@]}"; do
        if [ -f ".github/workflows/$workflow" ]; then
            print_success "Workflow exists: $workflow"
        else
            print_warning "Workflow missing: $workflow"
        fi
    done
else
    print_warning "No GitHub workflows found"
fi

# Summary
print_section "Validation Summary"

if [ "$VALIDATION_PASSED" = true ]; then
    echo -e "${GREEN}✓ All critical validations passed${NC}"
    echo ""
    echo "Infrastructure dependencies are correctly configured."
    echo "You can proceed with deployment."
    exit 0
else
    echo -e "${RED}✗ Some validations failed${NC}"
    echo ""
    echo "Please review the errors above and fix them before deployment."
    echo "See docs/INFRASTRUCTURE_DEPENDENCIES.md for detailed guidance."
    exit 1
fi
