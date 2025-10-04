# End-to-End Deployment Example with Terraform IaC

This example demonstrates a complete end-to-end deployment of MonadicPipeline using Terraform Infrastructure as Code.

## Scenario

Deploy MonadicPipeline to IONOS Cloud with:
- Production-grade infrastructure
- Automated provisioning
- Complete observability stack
- Multi-environment support

## Prerequisites

- IONOS Cloud account with API credentials
- Terraform >= 1.5.0 installed
- kubectl installed
- Docker installed (for local testing)

## Step-by-Step Walkthrough

### Step 1: Setup Credentials (2 minutes)

```bash
# Generate API token from IONOS Cloud Console
# Go to: https://dcd.ionos.com → User Menu → API Credentials

# Set environment variable
export IONOS_TOKEN="your-api-token-here"

# Verify connectivity
curl -H "Authorization: Bearer $IONOS_TOKEN" https://api.ionos.com/cloudapi/v6/
```

### Step 2: Validate Setup (1 minute)

```bash
# Clone repository
git clone https://github.com/PMeeske/MonadicPipeline.git
cd MonadicPipeline

# Validate Terraform configuration
./scripts/validate-terraform.sh production

# Expected output:
# ✓ Terraform installed
# ✓ IONOS Cloud API connection successful
# ✓ All required files present
# ✓ Configuration valid
```

### Step 3: Review Configuration (2 minutes)

```bash
# View production configuration
cat terraform/environments/production.tfvars

# Key settings:
# - datacenter_name: monadic-pipeline-prod
# - cluster_name: monadic-pipeline-cluster
# - node_count: 3 (autoscaling 2-5)
# - cores_count: 4 per node
# - ram_size: 16384 MB (16 GB) per node
# - storage_type: SSD
```

### Step 4: Preview Infrastructure (2 minutes)

```bash
# Initialize Terraform
./scripts/manage-infrastructure.sh init

# Preview what will be created
./scripts/manage-infrastructure.sh plan production

# Expected resources:
# + ionoscloud_datacenter.main
# + ionoscloud_k8s_cluster.main
# + ionoscloud_k8s_node_pool.main
# + ionoscloud_container_registry.main
# + ionoscloud_container_registry_token.main
# + ionoscloud_volume.volumes["qdrant-data"]
# + ionoscloud_volume.volumes["ollama-models"]
# + ionoscloud_lan.main
#
# Total: 8 resources to create
```

### Step 5: Provision Infrastructure (10-15 minutes)

```bash
# Apply infrastructure
./scripts/manage-infrastructure.sh apply production

# Terraform will:
# 1. Create data center in Frankfurt
# 2. Provision Kubernetes cluster (takes ~10 minutes)
# 3. Create container registry
# 4. Provision storage volumes
# 5. Configure networking

# Wait for completion...
# ✓ Infrastructure applied successfully
```

### Step 6: Configure kubectl (1 minute)

```bash
# Save kubeconfig
./scripts/manage-infrastructure.sh kubeconfig production

# Configure kubectl
export KUBECONFIG=./terraform/kubeconfig-production.yaml

# Verify cluster access
kubectl get nodes

# Expected output:
# NAME                            STATUS   ROLES    AGE   VERSION
# k8s-production-pool-node-1      Ready    <none>   5m    v1.28.x
# k8s-production-pool-node-2      Ready    <none>   5m    v1.28.x
# k8s-production-pool-node-3      Ready    <none>   5m    v1.28.x
```

### Step 7: Get Registry Credentials (1 minute)

```bash
# Get registry hostname
REGISTRY_HOSTNAME=$(cd terraform && terraform output -raw registry_hostname)
echo $REGISTRY_HOSTNAME
# Output: adaptive-systems.cr.de-fra.ionos.com

# Get registry credentials (save to environment)
cd terraform
terraform output -json registry_token_credentials > /tmp/registry-creds.json

# Extract username and password
REGISTRY_USER=$(cat /tmp/registry-creds.json | jq -r '.username')
REGISTRY_PASS=$(cat /tmp/registry-creds.json | jq -r '.password')

# Set for deployment script
export IONOS_REGISTRY=$REGISTRY_HOSTNAME
export IONOS_USERNAME=$REGISTRY_USER
export IONOS_PASSWORD=$REGISTRY_PASS

# Clean up credentials file
rm /tmp/registry-creds.json
cd ..
```

### Step 8: Deploy Application (5 minutes)

```bash
# Deploy MonadicPipeline to the cluster
./scripts/deploy-ionos.sh monadic-pipeline

# The script will:
# 1. Authenticate with container registry
# 2. Build Docker images (CLI and WebAPI)
# 3. Push images to IONOS registry
# 4. Create Kubernetes namespace
# 5. Configure registry pull secrets
# 6. Deploy Ollama (LLM runtime)
# 7. Deploy Qdrant (vector database)
# 8. Deploy Jaeger (tracing)
# 9. Deploy MonadicPipeline CLI
# 10. Deploy MonadicPipeline Web API

# Wait for deployments...
# ✓ All components deployed successfully
```

### Step 9: Verify Deployment (2 minutes)

```bash
# Check pod status
kubectl get pods -n monadic-pipeline

# Expected output:
# NAME                                       READY   STATUS    RESTARTS   AGE
# ollama-xxxxx                               1/1     Running   0          3m
# qdrant-xxxxx                               1/1     Running   0          3m
# jaeger-xxxxx                               1/1     Running   0          3m
# monadic-pipeline-xxxxx                     1/1     Running   0          2m
# monadic-pipeline-webapi-xxxxx              1/1     Running   0          2m

# Check services
kubectl get svc -n monadic-pipeline

# Test Web API
kubectl port-forward svc/monadic-pipeline-webapi 8080:80 -n monadic-pipeline &
curl http://localhost:8080/health

# Expected: {"status":"healthy"}
```

### Step 10: Access Application (1 minute)

```bash
# Get LoadBalancer IP (if configured)
kubectl get svc monadic-pipeline-webapi -n monadic-pipeline

# Or use port-forwarding for testing
kubectl port-forward svc/monadic-pipeline-webapi 8080:80 -n monadic-pipeline

# Access Web API
open http://localhost:8080/swagger
```

## Infrastructure Overview

### Resources Created

```
IONOS Cloud (de/fra)
├── Data Center: monadic-pipeline-prod
├── Kubernetes Cluster: monadic-pipeline-cluster
│   ├── Control Plane (managed by IONOS)
│   └── Node Pool: production-pool
│       ├── Node 1: 4 cores, 16GB RAM, 100GB SSD
│       ├── Node 2: 4 cores, 16GB RAM, 100GB SSD
│       └── Node 3: 4 cores, 16GB RAM, 100GB SSD
├── Container Registry: adaptive-systems.cr.de-fra.ionos.com
│   ├── Images: monadic-pipeline:latest
│   ├── Images: monadic-pipeline-webapi:latest
│   └── Token: adaptive-systems-token
├── Storage Volumes
│   ├── qdrant-data: 50GB SSD
│   └── ollama-models: 100GB SSD
└── Network
    └── LAN: monadic-pipeline-lan (public)
```

### Application Architecture

```
Kubernetes Cluster (monadic-pipeline namespace)
├── Ollama (LLM Runtime)
│   ├── Service: ollama:11434
│   └── Models: llama3, codellama, etc.
├── Qdrant (Vector Database)
│   ├── Service: qdrant:6333
│   └── Storage: qdrant-data PVC (50GB)
├── Jaeger (Distributed Tracing)
│   ├── Service: jaeger:16686 (UI)
│   └── Service: jaeger:14268 (collector)
├── MonadicPipeline CLI
│   └── Job: Batch processing
└── MonadicPipeline Web API
    ├── Service: monadic-pipeline-webapi:80
    ├── Deployment: 2 replicas
    └── Endpoints:
        ├── /health
        ├── /swagger
        └── /api/pipeline/*
```

## Cost Breakdown

### Production Environment

**Infrastructure**:
- Kubernetes nodes: 3x (4 cores, 16GB) = €120-150/month
- Container registry: €10-15/month
- Storage: 150GB SSD = €15-20/month
- Networking: €5-10/month

**Total**: ~€150-250/month

**Cost Optimization Tips**:
1. Enable autoscaling (scale down to 2 nodes during low usage)
2. Use HDD for non-critical storage
3. Configure garbage collection for registry
4. Set up alerts for resource usage

## Multi-Environment Strategy

### Development

```bash
# Deploy dev environment (minimal resources)
./scripts/manage-infrastructure.sh apply dev

# Cost: ~€50-80/month
# Use case: Feature development, testing
```

### Staging

```bash
# Deploy staging environment (medium resources)
./scripts/manage-infrastructure.sh apply staging

# Cost: ~€100-150/month
# Use case: Pre-production validation, QA testing
```

### Production

```bash
# Deploy production environment (full resources)
./scripts/manage-infrastructure.sh apply production

# Cost: ~€150-250/month
# Use case: Live workloads, customer-facing services
```

## Maintenance Tasks

### Upgrading Kubernetes

```bash
# Edit production.tfvars
vim terraform/environments/production.tfvars

# Change k8s_version
k8s_version = "1.29"  # from 1.28

# Apply upgrade
./scripts/manage-infrastructure.sh apply production

# Verify
kubectl version
```

### Scaling Nodes

```bash
# Edit production.tfvars
vim terraform/environments/production.tfvars

# Change node_count
node_count = 5  # from 3

# Apply scaling
./scripts/manage-infrastructure.sh apply production

# Verify
kubectl get nodes
```

### Adding Storage

```bash
# Edit production.tfvars
vim terraform/environments/production.tfvars

# Add new volume
volumes = [
  # ... existing volumes ...
  {
    name         = "new-storage"
    size         = 100
    type         = "SSD"
    licence_type = "OTHER"
  }
]

# Apply changes
./scripts/manage-infrastructure.sh apply production
```

## Disaster Recovery

### Backup Infrastructure State

```bash
# Backup Terraform state
cp terraform/terraform.tfstate terraform/terraform.tfstate.backup.$(date +%Y%m%d)

# Or use remote backend (recommended)
# See: docs/IONOS_IAC_GUIDE.md#state-management
```

### Recreate Infrastructure

```bash
# If infrastructure is lost, simply re-apply
./scripts/manage-infrastructure.sh apply production

# Terraform will recreate all resources
# Application data should be backed up separately
```

### Application Data Backup

```bash
# Backup Qdrant data
kubectl exec -n monadic-pipeline qdrant-xxxxx -- tar czf /tmp/qdrant-backup.tar.gz /qdrant/storage
kubectl cp monadic-pipeline/qdrant-xxxxx:/tmp/qdrant-backup.tar.gz ./qdrant-backup.tar.gz

# Backup Ollama models (optional - can re-download)
kubectl exec -n monadic-pipeline ollama-xxxxx -- tar czf /tmp/ollama-backup.tar.gz /root/.ollama
kubectl cp monadic-pipeline/ollama-xxxxx:/tmp/ollama-backup.tar.gz ./ollama-backup.tar.gz
```

## Cleanup

### Destroy Infrastructure

```bash
# Warning: This will delete all resources!

# Development (safe to destroy)
./scripts/manage-infrastructure.sh destroy dev

# Production (requires confirmation)
./scripts/manage-infrastructure.sh destroy production
# Type: DELETE PRODUCTION
```

## Troubleshooting

### Issue: Cluster creation fails

**Solution**: Check IONOS Cloud quota
```bash
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/contracts/resources
```

### Issue: Nodes not ready

**Solution**: Check node status
```bash
kubectl describe nodes
kubectl get events -n kube-system
```

### Issue: Pods in ImagePullBackOff

**Solution**: Verify registry credentials
```bash
kubectl get secret ionos-registry-secret -n monadic-pipeline -o yaml
kubectl describe pod <pod-name> -n monadic-pipeline
```

## Next Steps

1. **Configure monitoring**: Set up Prometheus/Grafana
2. **Set up alerts**: Configure alerting for critical metrics
3. **Implement CI/CD**: Use GitHub Actions workflow
4. **Configure DNS**: Set up custom domain
5. **Enable SSL**: Configure SSL certificates

## Resources

- [IONOS IaC Guide](docs/IONOS_IAC_GUIDE.md)
- [Terraform README](terraform/README.md)
- [IONOS Deployment Guide](docs/IONOS_DEPLOYMENT_GUIDE.md)
- [Scripts Documentation](scripts/README.md)

---

**Estimated Total Time**: 30-40 minutes for first deployment  
**Estimated Cost**: €150-250/month for production environment  
**Support**: See documentation or create GitHub issue
