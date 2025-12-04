# Terraform to Kubernetes Integration Guide

This guide provides detailed instructions for integrating Terraform-provisioned infrastructure with Kubernetes deployments for Ouroboros.

## Table of Contents

1. [Overview](#overview)
2. [Integration Architecture](#integration-architecture)
3. [Terraform Outputs for Kubernetes](#terraform-outputs-for-kubernetes)
4. [Automated Integration Workflows](#automated-integration-workflows)
5. [Manual Integration Steps](#manual-integration-steps)
6. [Configuration Injection Patterns](#configuration-injection-patterns)
7. [Storage Class Integration](#storage-class-integration)
8. [Network Integration](#network-integration)
9. [Security Integration](#security-integration)
10. [Validation and Testing](#validation-and-testing)

## Overview

The integration between Terraform and Kubernetes follows this pattern:

```
Terraform (Infrastructure) → Outputs → Kubernetes (Workloads)
```

Terraform provisions the infrastructure and exports values that Kubernetes needs for deployment.

## Integration Architecture

### Data Flow

```
┌──────────────────────────────────────────────────────────────┐
│                     Terraform Layer                          │
│  ┌────────────┐  ┌──────────┐  ┌─────────────┐             │
│  │ Datacenter │→ │ K8s      │→ │ Container   │             │
│  │            │  │ Cluster  │  │ Registry    │             │
│  └────────────┘  └──────────┘  └─────────────┘             │
│                        ↓                ↓                     │
│                   [Outputs]        [Outputs]                 │
└─────────────────────────┬──────────────┬────────────────────┘
                          │              │
                          ↓              ↓
┌──────────────────────────────────────────────────────────────┐
│                   Integration Layer                          │
│  ┌─────────────────┐     ┌──────────────────────┐           │
│  │ Kubeconfig      │     │ Registry Credentials │           │
│  │ (cluster access)│     │ (image pull)         │           │
│  └─────────────────┘     └──────────────────────┘           │
└─────────────────────────┬────────────────────────────────────┘
                          │
                          ↓
┌──────────────────────────────────────────────────────────────┐
│                   Kubernetes Layer                           │
│  ┌──────────┐  ┌──────────┐  ┌────────────┐                │
│  │ Secrets  │  │ Pods     │  │ Services   │                │
│  └──────────┘  └──────────┘  └────────────┘                │
└──────────────────────────────────────────────────────────────┘
```

### Integration Points

| Terraform Resource | Output | Kubernetes Resource | Usage |
|-------------------|--------|---------------------|-------|
| K8s Cluster | `k8s_kubeconfig` | kubectl context | Cluster access |
| K8s Cluster | `k8s_cluster_id` | Annotations/Labels | Resource tracking |
| Container Registry | `registry_hostname` | Image URLs | Container images |
| Container Registry | `registry_token` | imagePullSecret | Authentication |
| Node Pool | `k8s_public_ips` | LoadBalancer | External access |
| LAN | `lan_id` | Network policies | Network segmentation |
| Datacenter | `datacenter_id` | Annotations | Cost tracking |

## Terraform Outputs for Kubernetes

### Required Outputs

**File**: `terraform/outputs.tf`

```hcl
# Kubeconfig for kubectl access
output "k8s_kubeconfig" {
  description = "Kubeconfig for accessing the Kubernetes cluster"
  value       = module.kubernetes.kubeconfig
  sensitive   = true
}

# Registry hostname for image URLs
output "registry_hostname" {
  description = "Hostname of the container registry"
  value       = module.registry.registry_hostname
}

# Registry authentication token
output "registry_token" {
  description = "Authentication token for container registry"
  value       = module.registry.registry_token
  sensitive   = true
}

# Cluster information
output "k8s_cluster_id" {
  description = "ID of the Kubernetes cluster"
  value       = module.kubernetes.cluster_id
}

# Network information
output "k8s_public_ips" {
  description = "Public IPs of Kubernetes nodes"
  value       = module.kubernetes.public_ips
}
```

### Retrieving Outputs

```bash
# Get kubeconfig
terraform output -raw k8s_kubeconfig > kubeconfig.yaml

# Get registry hostname
REGISTRY_URL=$(terraform output -raw registry_hostname)

# Get registry token
REGISTRY_TOKEN=$(terraform output -raw registry_token)

# Get cluster ID
CLUSTER_ID=$(terraform output -raw k8s_cluster_id)
```

## Automated Integration Workflows

### Script-Based Integration

**File**: `scripts/integrate-terraform-k8s.sh` (to be created)

```bash
#!/bin/bash
set -e

TERRAFORM_DIR="./terraform"
K8S_DIR="./k8s"
NAMESPACE="monadic-pipeline"

echo "🔧 Integrating Terraform outputs with Kubernetes..."

# 1. Extract Terraform outputs
cd $TERRAFORM_DIR
REGISTRY_URL=$(terraform output -raw registry_hostname)
REGISTRY_TOKEN=$(terraform output -raw registry_token)
CLUSTER_ID=$(terraform output -raw k8s_cluster_id)
terraform output -raw k8s_kubeconfig > ../kubeconfig.yaml
cd ..

# 2. Configure kubectl
export KUBECONFIG=./kubeconfig.yaml

# 3. Verify cluster access
echo "✓ Testing cluster access..."
kubectl get nodes

# 4. Create namespace if not exists
kubectl create namespace $NAMESPACE --dry-run=client -o yaml | kubectl apply -f -

# 5. Create registry pull secret
echo "✓ Creating registry pull secret..."
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=$REGISTRY_URL \
  --docker-username=<username> \
  --docker-password=$REGISTRY_TOKEN \
  --namespace=$NAMESPACE \
  --dry-run=client -o yaml | kubectl apply -f -

# 6. Update deployment manifests with registry URL
echo "✓ Updating deployment manifests..."
for file in $K8S_DIR/deployment.cloud.yaml $K8S_DIR/webapi-deployment.cloud.yaml; do
  sed "s|REGISTRY_URL|$REGISTRY_URL|g" $file > ${file}.tmp
  mv ${file}.tmp ${file}
done

# 7. Apply Kubernetes manifests
echo "✓ Applying Kubernetes manifests..."
kubectl apply -f $K8S_DIR/namespace.yaml
kubectl apply -f $K8S_DIR/configmap.yaml
kubectl apply -f $K8S_DIR/secrets.yaml

echo "✅ Integration complete!"
echo "Registry URL: $REGISTRY_URL"
echo "Cluster ID: $CLUSTER_ID"
echo "Kubeconfig: ./kubeconfig.yaml"
```

### GitHub Actions Integration

**File**: `.github/workflows/terraform-k8s-integration.yml` (to be created)

```yaml
name: Terraform-Kubernetes Integration

on:
  workflow_run:
    workflows: ["Terraform Infrastructure"]
    types: [completed]
    branches: [main]
  workflow_dispatch:
    inputs:
      environment:
        type: choice
        description: 'Environment to deploy'
        options:
          - dev
          - staging
          - production

jobs:
  integrate:
    runs-on: ubuntu-latest
    environment: ${{ github.event.inputs.environment || 'production' }}
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
        with:
          terraform_wrapper: false
      
      - name: Setup kubectl
        uses: azure/setup-kubectl@v3
        with:
          version: 'v1.28.0'
      
      - name: Extract Terraform Outputs
        env:
          IONOS_TOKEN: ${{ secrets.IONOS_ADMIN_TOKEN }}
        run: |
          cd terraform
          terraform init
          
          # Extract outputs
          terraform output -raw k8s_kubeconfig > $GITHUB_WORKSPACE/kubeconfig.yaml
          echo "REGISTRY_URL=$(terraform output -raw registry_hostname)" >> $GITHUB_ENV
          echo "CLUSTER_ID=$(terraform output -raw k8s_cluster_id)" >> $GITHUB_ENV
          
          # Store registry token securely
          echo "::add-mask::$(terraform output -raw registry_token)"
          echo "REGISTRY_TOKEN=$(terraform output -raw registry_token)" >> $GITHUB_ENV
      
      - name: Configure kubectl
        run: |
          export KUBECONFIG=$GITHUB_WORKSPACE/kubeconfig.yaml
          kubectl get nodes
      
      - name: Create Kubernetes Secrets
        env:
          KUBECONFIG: ${{ github.workspace }}/kubeconfig.yaml
        run: |
          kubectl create namespace monadic-pipeline --dry-run=client -o yaml | kubectl apply -f -
          
          kubectl create secret docker-registry ionos-registry-secret \
            --docker-server=${{ env.REGISTRY_URL }} \
            --docker-username=github-actions \
            --docker-password=${{ env.REGISTRY_TOKEN }} \
            --namespace=monadic-pipeline \
            --dry-run=client -o yaml | kubectl apply -f -
      
      - name: Update Deployment Manifests
        run: |
          sed -i "s|REGISTRY_URL|${{ env.REGISTRY_URL }}|g" k8s/deployment.cloud.yaml
          sed -i "s|REGISTRY_URL|${{ env.REGISTRY_URL }}|g" k8s/webapi-deployment.cloud.yaml
      
      - name: Validate Integration
        env:
          KUBECONFIG: ${{ github.workspace }}/kubeconfig.yaml
        run: |
          echo "✓ Cluster ID: ${{ env.CLUSTER_ID }}"
          echo "✓ Registry URL: ${{ env.REGISTRY_URL }}"
          kubectl get secret ionos-registry-secret -n monadic-pipeline
```

## Manual Integration Steps

### Step 1: Extract Kubeconfig

```bash
cd terraform
terraform output -raw k8s_kubeconfig > ../kubeconfig.yaml

# Set kubectl context
export KUBECONFIG=../kubeconfig.yaml

# Verify access
kubectl get nodes
kubectl cluster-info
```

### Step 2: Create Registry Pull Secret

```bash
# Get registry credentials
REGISTRY_URL=$(terraform output -raw registry_hostname)
REGISTRY_TOKEN=$(terraform output -raw registry_token)

# Create secret
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=$REGISTRY_URL \
  --docker-username=<your-username> \
  --docker-password=$REGISTRY_TOKEN \
  --namespace=monadic-pipeline

# Verify secret
kubectl get secret ionos-registry-secret -n monadic-pipeline
```

### Step 3: Build and Push Images

```bash
# Build images
docker build -t $REGISTRY_URL/monadic-pipeline:latest .
docker build -f Dockerfile.webapi -t $REGISTRY_URL/monadic-pipeline-webapi:latest .

# Login to registry
docker login $REGISTRY_URL -u <username> -p $REGISTRY_TOKEN

# Push images
docker push $REGISTRY_URL/monadic-pipeline:latest
docker push $REGISTRY_URL/monadic-pipeline-webapi:latest
```

### Step 4: Update Deployment Manifests

```bash
# Replace REGISTRY_URL placeholder in deployment files
sed -i "s|REGISTRY_URL|$REGISTRY_URL|g" k8s/deployment.cloud.yaml
sed -i "s|REGISTRY_URL|$REGISTRY_URL|g" k8s/webapi-deployment.cloud.yaml

# Verify changes
grep "image:" k8s/deployment.cloud.yaml
```

### Step 5: Deploy to Kubernetes

```bash
# Apply manifests in order
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/ollama.yaml
kubectl apply -f k8s/qdrant.yaml
kubectl apply -f k8s/jaeger.yaml
kubectl apply -f k8s/deployment.cloud.yaml
kubectl apply -f k8s/webapi-deployment.cloud.yaml

# Verify deployment
kubectl get all -n monadic-pipeline
```

## Configuration Injection Patterns

### Pattern 1: Terraform Output → Environment Variable

**Terraform**:
```hcl
output "registry_hostname" {
  value = ionoscloud_container_registry.main.hostname
}
```

**Kubernetes Deployment**:
```yaml
env:
  - name: CONTAINER_REGISTRY
    value: "adaptive-systems.cr.de-fra.ionos.com"  # From Terraform output
```

**C# Application**:
```csharp
var registry = Environment.GetEnvironmentVariable("CONTAINER_REGISTRY");
```

### Pattern 2: Terraform Output → ConfigMap

**Script**:
```bash
# Create ConfigMap from Terraform outputs
kubectl create configmap terraform-config \
  --from-literal=registry_url=$(terraform output -raw registry_hostname) \
  --from-literal=cluster_id=$(terraform output -raw k8s_cluster_id) \
  --namespace=monadic-pipeline
```

**Kubernetes Deployment**:
```yaml
envFrom:
  - configMapRef:
      name: terraform-config
```

### Pattern 3: Terraform Output → Secret

**Script**:
```bash
# Create Secret from sensitive Terraform outputs
kubectl create secret generic terraform-secrets \
  --from-literal=registry_token=$(terraform output -raw registry_token) \
  --namespace=monadic-pipeline
```

**Kubernetes Deployment**:
```yaml
env:
  - name: REGISTRY_TOKEN
    valueFrom:
      secretKeyRef:
        name: terraform-secrets
        key: registry_token
```

## Storage Class Integration

### IONOS StorageClass Configuration

Kubernetes needs a StorageClass that maps to IONOS volumes provisioned by Terraform.

**File**: `k8s/storageclass.yaml` (to be created)

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: ionos-enterprise-ssd
  annotations:
    # Reference to Terraform-provisioned storage
    terraform.io/storage-type: "SSD"
    terraform.io/datacenter-id: "<from-terraform-output>"
provisioner: cloud.ionos.com/ionos-enterprise-ssd
parameters:
  type: SSD
  # Size is specified in PVC, not StorageClass
reclaimPolicy: Retain
allowVolumeExpansion: true
volumeBindingMode: WaitForFirstConsumer
```

### PVC Using Terraform-Aligned Storage

**File**: `k8s/qdrant.yaml`

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: qdrant-data
  namespace: monadic-pipeline
  annotations:
    # Link to Terraform variable
    terraform.io/volume-name: "qdrant-data"
    terraform.io/volume-size: "50Gi"  # Matches Terraform volumes[0].size
spec:
  accessModes:
    - ReadWriteOnce
  storageClassName: ionos-enterprise-ssd  # Matches Terraform storage_type
  resources:
    requests:
      storage: 50Gi  # Must match Terraform volumes[0].size
```

### Validation Script

```bash
# Verify storage alignment
TERRAFORM_VOLUME_SIZE=$(cd terraform && terraform output -json volumes | jq -r '.[0].size')
K8S_VOLUME_SIZE=$(kubectl get pvc qdrant-data -n monadic-pipeline -o jsonpath='{.spec.resources.requests.storage}' | sed 's/Gi//')

if [ "$TERRAFORM_VOLUME_SIZE" -eq "$K8S_VOLUME_SIZE" ]; then
  echo "✓ Storage sizes aligned"
else
  echo "✗ Storage size mismatch: Terraform=$TERRAFORM_VOLUME_SIZE, K8s=$K8S_VOLUME_SIZE"
fi
```

## Network Integration

### LoadBalancer Service with Public IP

When Terraform provisions a public LAN, Kubernetes can use LoadBalancer services.

**Terraform Output**:
```hcl
output "k8s_public_ips" {
  description = "Public IPs available for LoadBalancer services"
  value       = module.kubernetes.public_ips
}
```

**Kubernetes Service**:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: monadic-pipeline-webapi-external
  namespace: monadic-pipeline
  annotations:
    # Reference to Terraform network
    terraform.io/lan-public: "true"
    service.beta.kubernetes.io/ionos-loadbalancer-public-ip: "auto"
spec:
  type: LoadBalancer
  selector:
    app: monadic-pipeline-webapi
  ports:
    - port: 80
      targetPort: 8080
      protocol: TCP
```

**Verification**:
```bash
# Get external IP assigned by IONOS
EXTERNAL_IP=$(kubectl get svc monadic-pipeline-webapi-external -n monadic-pipeline -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Compare with Terraform outputs
TERRAFORM_IPS=$(cd terraform && terraform output -json k8s_public_ips | jq -r '.[]')

echo "Terraform IPs: $TERRAFORM_IPS"
echo "K8s LoadBalancer IP: $EXTERNAL_IP"
```

## Security Integration

### Registry Authentication Flow

```
1. Terraform creates registry token
   ↓
2. Extract token via terraform output
   ↓
3. Create Kubernetes docker-registry secret
   ↓
4. Reference secret in pod imagePullSecrets
   ↓
5. Kubernetes pulls images using token
```

### Token Rotation Strategy

```bash
# When rotating registry tokens:

# 1. Update Terraform
cd terraform
terraform apply  # This regenerates the token

# 2. Update Kubernetes secret
NEW_TOKEN=$(terraform output -raw registry_token)
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=$(terraform output -raw registry_hostname) \
  --docker-username=<username> \
  --docker-password=$NEW_TOKEN \
  --namespace=monadic-pipeline \
  --dry-run=client -o yaml | kubectl apply -f -

# 3. Restart pods to use new secret
kubectl rollout restart deployment -n monadic-pipeline
```

### RBAC Integration

Link Kubernetes RBAC to Terraform-provisioned resources:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: monadic-pipeline-sa
  namespace: monadic-pipeline
  annotations:
    terraform.io/cluster-id: "<from-terraform-output>"
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: monadic-pipeline-role
  namespace: monadic-pipeline
rules:
  - apiGroups: [""]
    resources: ["pods", "services"]
    verbs: ["get", "list", "watch"]
```

## Validation and Testing

### Integration Validation Checklist

```bash
#!/bin/bash
# File: scripts/validate-terraform-k8s-integration.sh

echo "🔍 Validating Terraform-Kubernetes Integration..."

# 1. Terraform outputs exist
cd terraform
echo "Checking Terraform outputs..."
terraform output k8s_kubeconfig >/dev/null && echo "✓ k8s_kubeconfig" || echo "✗ k8s_kubeconfig missing"
terraform output registry_hostname >/dev/null && echo "✓ registry_hostname" || echo "✗ registry_hostname missing"
terraform output registry_token >/dev/null && echo "✓ registry_token" || echo "✗ registry_token missing"

# 2. Kubectl access works
cd ..
export KUBECONFIG=./kubeconfig.yaml
echo "Testing cluster access..."
kubectl get nodes >/dev/null 2>&1 && echo "✓ Cluster accessible" || echo "✗ Cluster not accessible"

# 3. Registry secret exists
echo "Checking registry secret..."
kubectl get secret ionos-registry-secret -n monadic-pipeline >/dev/null 2>&1 && \
  echo "✓ Registry secret exists" || echo "✗ Registry secret missing"

# 4. Images are accessible
REGISTRY_URL=$(cd terraform && terraform output -raw registry_hostname)
echo "Checking registry access..."
docker login $REGISTRY_URL -u <user> -p $(cd terraform && terraform output -raw registry_token) >/dev/null 2>&1 && \
  echo "✓ Registry accessible" || echo "✗ Registry not accessible"

# 5. Storage classes exist
echo "Checking storage classes..."
kubectl get storageclass ionos-enterprise-ssd >/dev/null 2>&1 && \
  echo "✓ StorageClass exists" || echo "⚠ StorageClass not found (may be auto-provisioned)"

# 6. Network connectivity
echo "Checking network connectivity..."
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup kubernetes.default >/dev/null 2>&1 && \
  echo "✓ DNS working" || echo "✗ DNS not working"

echo "✅ Validation complete!"
```

### End-to-End Integration Test

```bash
#!/bin/bash
# File: scripts/test-e2e-integration.sh

set -e

echo "🧪 Running end-to-end integration test..."

# 1. Provision infrastructure
cd terraform
terraform apply -var-file=environments/dev.tfvars -auto-approve

# 2. Extract outputs
REGISTRY_URL=$(terraform output -raw registry_hostname)
terraform output -raw k8s_kubeconfig > ../kubeconfig.yaml
cd ..

# 3. Configure kubectl
export KUBECONFIG=./kubeconfig.yaml

# 4. Build and push test image
docker build -t $REGISTRY_URL/test-app:latest -f - . <<EOF
FROM busybox
CMD ["echo", "Integration test successful"]
EOF
docker push $REGISTRY_URL/test-app:latest

# 5. Deploy test pod
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: integration-test
  namespace: monadic-pipeline
spec:
  imagePullSecrets:
    - name: ionos-registry-secret
  containers:
    - name: test
      image: $REGISTRY_URL/test-app:latest
  restartPolicy: Never
EOF

# 6. Wait for completion
kubectl wait --for=condition=Ready pod/integration-test -n monadic-pipeline --timeout=60s

# 7. Check logs
kubectl logs integration-test -n monadic-pipeline | grep "Integration test successful"

# 8. Cleanup
kubectl delete pod integration-test -n monadic-pipeline

echo "✅ End-to-end integration test passed!"
```

## Troubleshooting

### Issue: Kubeconfig Not Working

**Symptoms**:
```
Unable to connect to the server: x509: certificate signed by unknown authority
```

**Solution**:
```bash
# Regenerate kubeconfig
cd terraform
terraform output -raw k8s_kubeconfig > ../kubeconfig.yaml

# Verify cluster info
export KUBECONFIG=../kubeconfig.yaml
kubectl cluster-info

# Check certificate validity
kubectl config view --raw -o jsonpath='{.clusters[0].cluster.certificate-authority-data}' | base64 -d | openssl x509 -text
```

### Issue: ImagePullBackOff

**Symptoms**:
```
Failed to pull image "REGISTRY_URL/monadic-pipeline:latest": rpc error: code = Unknown desc = Error response from daemon: unauthorized
```

**Solution**:
```bash
# Verify secret exists and is correct
kubectl get secret ionos-registry-secret -n monadic-pipeline -o yaml

# Recreate secret with correct credentials
kubectl delete secret ionos-registry-secret -n monadic-pipeline
REGISTRY_TOKEN=$(cd terraform && terraform output -raw registry_token)
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=$(cd terraform && terraform output -raw registry_hostname) \
  --docker-username=<username> \
  --docker-password=$REGISTRY_TOKEN \
  --namespace=monadic-pipeline

# Verify image exists in registry
docker login $(cd terraform && terraform output -raw registry_hostname) -u <user> -p $REGISTRY_TOKEN
docker pull $(cd terraform && terraform output -raw registry_hostname)/monadic-pipeline:latest
```

### Issue: Storage Not Mounting

**Symptoms**:
```
MountVolume.SetUp failed for volume "pvc-xxx" : rpc error: code = Internal desc = failed to attach volume
```

**Solution**:
```bash
# Check StorageClass
kubectl get storageclass

# Verify PVC status
kubectl get pvc -n monadic-pipeline

# Check volume in IONOS (via Terraform)
cd terraform
terraform state list | grep volume
terraform show | grep -A 10 ionoscloud_volume

# Verify datacenter ID matches
DATACENTER_ID=$(terraform output -raw datacenter_id)
kubectl get pvc qdrant-data -n monadic-pipeline -o yaml | grep datacenter
```

## Best Practices

1. **Always use Terraform outputs** as the source of truth for infrastructure values
2. **Validate integration** after every Terraform apply
3. **Version control kubeconfig** (encrypted) or regenerate from Terraform outputs
4. **Automate secret rotation** to keep registry credentials fresh
5. **Monitor integration points** with health checks and alerts
6. **Document custom integrations** when adding new Terraform resources
7. **Test in dev/staging** before applying to production

## Conclusion

Proper integration between Terraform and Kubernetes is essential for:
- ✅ Reliable deployments
- ✅ Security (proper secret handling)
- ✅ Maintainability (automated workflows)
- ✅ Disaster recovery (reproducible infrastructure)

Always follow the integration patterns documented here to ensure consistency across environments.

---

**Version**: 1.0.0
**Last Updated**: 2025-01-XX
**Maintained By**: Infrastructure Team
