# Infrastructure Dependencies: C# to Terraform

This document provides a comprehensive mapping of logical and architectural dependencies between the C# application layer and Terraform infrastructure provisioning for MonadicPipeline.

## Table of Contents

1. [Overview](#overview)
2. [Dependency Architecture](#dependency-architecture)
3. [Configuration Dependencies](#configuration-dependencies)
4. [Service Dependencies](#service-dependencies)
5. [Storage Dependencies](#storage-dependencies)
6. [Network Dependencies](#network-dependencies)
7. [Registry Dependencies](#registry-dependencies)
8. [Resource Dependencies](#resource-dependencies)
9. [Security Dependencies](#security-dependencies)
10. [Observability Dependencies](#observability-dependencies)
11. [Deployment Workflow](#deployment-workflow)
12. [Environment-Specific Mappings](#environment-specific-mappings)

## Overview

MonadicPipeline follows a **layered infrastructure approach** where:

1. **Terraform** provisions base cloud infrastructure (IONOS)
2. **Kubernetes** orchestrates container deployments
3. **C# Application** consumes infrastructure services via configuration

### Infrastructure Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    C# Application Layer                      │
│  (MonadicPipeline.CLI, MonadicPipeline.WebApi)              │
│  Configuration: appsettings.json + Environment Variables     │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│              Kubernetes Orchestration Layer                  │
│  Deployments, Services, ConfigMaps, Secrets, PVCs           │
│  Files: k8s/*.yaml                                          │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│            Terraform Infrastructure Layer                    │
│  Data Center, K8s Cluster, Registry, Storage, Network       │
│  Files: terraform/*.tf, terraform/modules/*                 │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                   IONOS Cloud Provider                       │
│  Physical Infrastructure, Networking, Compute, Storage       │
└─────────────────────────────────────────────────────────────┘
```

## Dependency Architecture

### Critical Dependency Paths

1. **C# → K8s → Terraform → IONOS**
   - Application configuration references Kubernetes services
   - Kubernetes requires cluster infrastructure from Terraform
   - Terraform provisions resources on IONOS Cloud

2. **Configuration Flow**
   ```
   appsettings.Production.json
   → Environment Variables (K8s)
   → ConfigMaps/Secrets (K8s)
   → Terraform Outputs (infrastructure endpoints)
   → IONOS Resources
   ```

3. **Service Discovery Flow**
   ```
   C# Application
   → Service Name (DNS)
   → Kubernetes Service
   → Pod Endpoints
   → Node (provisioned by Terraform)
   → IONOS Compute
   ```

## Configuration Dependencies

### C# Configuration Structure

**File**: `appsettings.Production.json`

```json
{
  "Pipeline": {
    "LlmProvider": {
      "OllamaEndpoint": "http://ollama-service:11434"  // K8s Service
    },
    "VectorStore": {
      "Type": "Qdrant",
      "ConnectionString": "http://qdrant-service:6333"  // K8s Service
    },
    "Observability": {
      "OpenTelemetryEndpoint": "http://jaeger-collector:4317"  // K8s Service
    }
  }
}
```

### Kubernetes ConfigMap

**File**: `k8s/configmap.yaml`

Maps to C# configuration but can override values via environment variables.

### Terraform to C# Configuration Mapping

| C# Configuration Path | K8s Resource | Terraform Output | IONOS Resource |
|----------------------|--------------|------------------|----------------|
| `Pipeline:LlmProvider:OllamaEndpoint` | `ollama-service` | N/A (in-cluster) | K8s Node |
| `Pipeline:VectorStore:ConnectionString` | `qdrant-service` | N/A (in-cluster) | K8s Node + PVC |
| Container Image Registry | `imagePullSecrets` | `registry_hostname` | IONOS Registry |
| Persistent Storage | `PersistentVolumeClaim` | K8s StorageClass | IONOS Volume |

### Environment Variable Mapping

**C# → Kubernetes → Terraform**

```bash
# C# reads this environment variable
PIPELINE__LlmProvider__OllamaEndpoint

# Kubernetes sets it in deployment.yaml
env:
  - name: PIPELINE__LlmProvider__OllamaEndpoint
    value: "http://ollama-service:11434"

# Service is deployed on K8s cluster
# K8s cluster is provisioned by Terraform module "kubernetes"
# Which depends on Terraform module "datacenter"
```

## Service Dependencies

### Application Service Requirements

The C# application requires these services at runtime:

| Service | Purpose | C# Configuration | K8s Manifest | Terraform Provision |
|---------|---------|------------------|--------------|---------------------|
| **Ollama** | LLM Inference | `LlmProvider:OllamaEndpoint` | `ollama.yaml` | Indirect (K8s nodes) |
| **Qdrant** | Vector Storage | `VectorStore:ConnectionString` | `qdrant.yaml` | Indirect (K8s nodes + storage) |
| **Jaeger** | Tracing | `Observability:OpenTelemetryEndpoint` | `jaeger.yaml` | Indirect (K8s nodes) |

### Service Deployment Hierarchy

```
1. Terraform provisions Kubernetes cluster
   ├── module "datacenter"
   ├── module "kubernetes" (depends on datacenter)
   └── module "networking" (depends on datacenter)

2. Kubernetes deploys services
   ├── namespace.yaml
   ├── ollama.yaml (requires PVC for models)
   ├── qdrant.yaml (requires PVC for data)
   └── jaeger.yaml

3. C# application connects to services
   ├── Reads configuration
   ├── Resolves K8s DNS names
   └── Establishes connections
```

### Storage Requirements for Services

| Service | Storage Type | Size | Terraform Variable | K8s Resource |
|---------|-------------|------|-------------------|--------------|
| Ollama | Persistent | 100GB | `volumes[1].size` | PVC: ollama-models |
| Qdrant | Persistent | 50GB | `volumes[0].size` | PVC: qdrant-data |
| Application Logs | Ephemeral | N/A | N/A | emptyDir |

## Storage Dependencies

### Terraform Storage Provisioning

**File**: `terraform/variables.tf`

```hcl
variable "volumes" {
  description = "List of volumes to create"
  default = [
    {
      name         = "qdrant-data"
      size         = 50
      type         = "SSD"
      licence_type = "OTHER"
    },
    {
      name         = "ollama-models"
      size         = 100
      type         = "SSD"
      licence_type = "OTHER"
    }
  ]
}
```

### Kubernetes Storage Consumption

**File**: `k8s/qdrant.yaml`, `k8s/ollama.yaml`

Kubernetes uses **StorageClass** provisioned by IONOS CSI driver to dynamically create PVCs.

```yaml
# Example PVC in qdrant.yaml
volumeClaimTemplates:
  - metadata:
      name: qdrant-storage
    spec:
      accessModes: ["ReadWriteOnce"]
      storageClassName: ionos-enterprise-ssd  # Maps to Terraform storage_type
      resources:
        requests:
          storage: 50Gi  # Maps to Terraform volumes[0].size
```

### Storage Dependency Flow

```
Terraform (volumes definition)
  → IONOS Cloud (physical storage)
  → K8s StorageClass (ionos-enterprise-ssd)
  → PVC (qdrant-storage)
  → Pod (qdrant)
  → C# App (via service connection)
```

## Network Dependencies

### Terraform Network Provisioning

**File**: `terraform/modules/networking/main.tf`

```hcl
resource "ionoscloud_lan" "main" {
  datacenter_id = var.datacenter_id
  name          = var.lan_name
  public        = var.lan_public  # true for external access
}
```

### Kubernetes Network Consumption

Kubernetes uses the LAN provisioned by Terraform for:
- Node-to-node communication
- Pod-to-pod communication
- Service networking
- Ingress/LoadBalancer external access

### Network Dependency Chain

```
1. Terraform creates LAN
   └── module "networking"
       └── ionoscloud_lan.main

2. Kubernetes cluster uses LAN
   └── module "kubernetes"
       └── Nodes attached to LAN

3. Kubernetes Services
   └── ClusterIP (internal)
   └── LoadBalancer (external, uses public LAN)

4. C# Application
   └── Connects via service DNS names
```

### Service Accessibility Matrix

| Service | K8s Service Type | Terraform Network | External Access |
|---------|-----------------|-------------------|-----------------|
| monadic-pipeline | ClusterIP | Internal LAN | No (use Ingress) |
| monadic-pipeline-webapi | ClusterIP/LoadBalancer | Public LAN (if LoadBalancer) | Configurable |
| ollama-service | ClusterIP | Internal LAN | No |
| qdrant-service | ClusterIP | Internal LAN | No |
| jaeger-collector | ClusterIP | Internal LAN | No |

## Registry Dependencies

### Terraform Registry Provisioning

**File**: `terraform/modules/registry/main.tf`

```hcl
resource "ionoscloud_container_registry" "main" {
  name     = var.registry_name  # "adaptive-systems"
  location = var.location       # "de/fra"
}

output "registry_hostname" {
  value = ionoscloud_container_registry.main.hostname
  # Example: adaptive-systems.cr.de-fra.ionos.com
}
```

### Docker Image Build & Push

**Application Requirement**: Container images must be built and pushed to registry

```bash
# Registry from Terraform output
REGISTRY_URL=$(terraform output -raw registry_hostname)

# Build images
docker build -t ${REGISTRY_URL}/monadic-pipeline:latest .
docker build -f Dockerfile.webapi -t ${REGISTRY_URL}/monadic-pipeline-webapi:latest .

# Push to registry
docker push ${REGISTRY_URL}/monadic-pipeline:latest
docker push ${REGISTRY_URL}/monadic-pipeline-webapi:latest
```

### Kubernetes Registry Consumption

**File**: `k8s/deployment.cloud.yaml`

```yaml
spec:
  imagePullSecrets:
    - name: ionos-registry-secret  # Created from Terraform registry token
  containers:
    - name: monadic-pipeline
      image: adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:latest
      # ↑ This hostname comes from Terraform output: registry_hostname
```

### Registry Dependency Flow

```
1. Terraform provisions registry
   └── module "registry"
       └── ionoscloud_container_registry.main
       └── ionoscloud_container_registry_token.main

2. Build and push Docker images
   └── Use Terraform output: registry_hostname
   └── Authenticate with registry token

3. Create Kubernetes pull secret
   └── kubectl create secret docker-registry ionos-registry-secret
       --docker-server=$(terraform output -raw registry_hostname)
       --docker-username=...
       --docker-password=$(terraform output -raw registry_token)

4. Kubernetes pulls images
   └── Uses imagePullSecrets
   └── Deploys to pods

5. C# application runs
   └── Inside container from registry
```

## Resource Dependencies

### Terraform Node Sizing

**File**: `terraform/variables.tf`

```hcl
variable "cores_count" {
  description = "Number of CPU cores per node"
  default     = 4
}

variable "ram_size" {
  description = "RAM size in MB per node"
  default     = 16384  # 16 GB
}
```

### Kubernetes Resource Requests

**File**: `k8s/deployment.cloud.yaml`

```yaml
resources:
  requests:
    memory: "512Mi"   # Minimum required
    cpu: "250m"       # 0.25 cores
  limits:
    memory: "2Gi"     # Maximum allowed
    cpu: "1000m"      # 1 core
```

### C# Application Resource Requirements

**Implicit from Configuration**:
- LLM operations (Ollama): Memory-intensive (2-4GB per model)
- Vector operations (Qdrant): CPU and memory intensive
- Tracing (Jaeger): Moderate resources

### Resource Sizing Strategy

```
Terraform Node Size:
  4 cores, 16GB RAM per node
  ↓
Kubernetes Node Capacity:
  ~3.5 cores, ~14GB RAM available (after system overhead)
  ↓
Application Allocation:
  - MonadicPipeline: 1 core, 2GB (2 replicas = 2 cores, 4GB)
  - Ollama: 2 cores, 8GB (model loading)
  - Qdrant: 0.5 cores, 2GB
  - Jaeger: 0.5 cores, 1GB
  - System: 0.5 cores, 1GB
  Total: ~6 cores, 16GB (needs 2 nodes minimum)
```

### Scaling Considerations

| Component | Horizontal Scaling | Terraform Impact |
|-----------|-------------------|------------------|
| C# Application (CLI) | Job-based | Need more nodes |
| C# Application (WebAPI) | Replica scaling | Need more nodes |
| Ollama | Limited (stateful) | Larger nodes or more nodes |
| Qdrant | Limited (stateful) | Larger storage, more nodes for replicas |
| Terraform Node Count | `variable "node_count"` | Increase for more capacity |

## Security Dependencies

### Secret Management Flow

```
1. Terraform creates registry token
   └── ionoscloud_container_registry_token.main
   └── Output: registry_token (sensitive)

2. Store in Kubernetes secret
   └── kubectl create secret generic monadic-pipeline-secrets
       --from-literal=registry-token=$(terraform output -raw registry_token)

3. C# application reads secrets
   └── Environment variables from K8s secrets
   └── Configuration via secretKeyRef in deployment.yaml
```

### Security Dependency Matrix

| Security Asset | Terraform | Kubernetes | C# Application |
|---------------|-----------|------------|----------------|
| Registry Token | Creates | Stores in Secret | Uses via imagePullSecrets |
| Vector DB Connection | N/A | Stores in Secret | Reads from env var |
| OpenAI API Key | N/A | Stores in Secret | Reads from configuration |
| TLS Certificates | N/A (manual) | Stores in Secret | Uses for HTTPS |

### Authentication Chain

```
C# App Authentication:
  → K8s Service Account (RBAC)
  → K8s Secrets (encrypted at rest)
  → External Services (API keys)

Terraform Authentication:
  → IONOS API Token (env var: IONOS_TOKEN)
  → Stored in GitHub Secrets
  → Used by CI/CD workflows
```

## Observability Dependencies

### C# Observability Configuration

**File**: `appsettings.Production.json`

```json
{
  "Observability": {
    "EnableStructuredLogging": true,
    "EnableMetrics": true,
    "EnableTracing": true,
    "OpenTelemetryEndpoint": "http://jaeger-collector:4317"
  }
}
```

### Kubernetes Observability Stack

**File**: `k8s/jaeger.yaml`

Deploys Jaeger for distributed tracing, which the C# app sends traces to.

### Infrastructure Observability

Terraform doesn't directly provision observability infrastructure, but:
- K8s cluster metrics are available via IONOS Cloud Console
- Can provision additional monitoring nodes if needed

### Observability Dependency Flow

```
1. C# App generates telemetry
   └── Logs (Serilog)
   └── Metrics (OpenTelemetry)
   └── Traces (OpenTelemetry)

2. Sends to K8s services
   └── Jaeger (traces)
   └── Prometheus (metrics, if configured)
   └── Stdout (logs, captured by K8s)

3. K8s aggregates data
   └── Logs: kubectl logs
   └── Metrics: K8s metrics API
   └── Traces: Jaeger UI

4. IONOS provides infrastructure metrics
   └── Node CPU/Memory
   └── Network traffic
   └── Storage IOPS
```

## Deployment Workflow

### Complete Deployment Sequence

```bash
# Phase 1: Terraform Infrastructure Provisioning
cd terraform
terraform init
terraform apply -var-file=environments/production.tfvars

# Outputs:
# - registry_hostname: adaptive-systems.cr.de-fra.ionos.com
# - k8s_kubeconfig: <kubeconfig-content>
# - datacenter_id: <uuid>

# Phase 2: Kubernetes Cluster Configuration
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
export KUBECONFIG=./kubeconfig.yaml
kubectl get nodes  # Verify cluster access

# Phase 3: Build and Push Container Images
REGISTRY_URL=$(terraform output -raw registry_hostname)
docker build -t ${REGISTRY_URL}/monadic-pipeline:latest .
docker build -f Dockerfile.webapi -t ${REGISTRY_URL}/monadic-pipeline-webapi:latest .
docker push ${REGISTRY_URL}/monadic-pipeline:latest
docker push ${REGISTRY_URL}/monadic-pipeline-webapi:latest

# Phase 4: Configure Kubernetes Secrets
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=${REGISTRY_URL} \
  --docker-username=<from-terraform> \
  --docker-password=$(terraform output -raw registry_token) \
  --namespace=monadic-pipeline

# Phase 5: Deploy Kubernetes Resources
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/ollama.yaml
kubectl apply -f k8s/qdrant.yaml
kubectl apply -f k8s/jaeger.yaml

# Phase 6: Deploy Application
sed "s|REGISTRY_URL|${REGISTRY_URL}|g" k8s/deployment.cloud.yaml | kubectl apply -f -
sed "s|REGISTRY_URL|${REGISTRY_URL}|g" k8s/webapi-deployment.cloud.yaml | kubectl apply -f -

# Phase 7: Verify Deployment
kubectl get all -n monadic-pipeline
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline
```

### Dependency Order

**Critical**: Terraform → Kubernetes Cluster → Image Registry → K8s Resources → Application

1. ✅ Terraform must complete first
2. ✅ Container images must be pushed to registry
3. ✅ Secrets must be created before deployments
4. ✅ Dependent services (Ollama, Qdrant) before application
5. ✅ Application deployment last

### Automated Deployment Scripts

The repository provides automation for this workflow:

```bash
# Option 1: Terraform + IONOS deployment
./scripts/manage-infrastructure.sh apply production
./scripts/deploy-ionos.sh monadic-pipeline

# Option 2: Manual step-by-step (better for understanding)
# Follow the sequence above
```

## Environment-Specific Mappings

### Development Environment

**Terraform**: `terraform/environments/dev.tfvars`

```hcl
node_count = 1      # Minimal resources
cores_count = 2     # Smaller nodes
ram_size = 8192     # 8GB RAM
```

**C# Configuration**: `appsettings.Development.json`

```json
{
  "Pipeline": {
    "LlmProvider": {
      "OllamaEndpoint": "http://localhost:11434"  // Local Ollama
    },
    "VectorStore": {
      "Type": "InMemory"  // No Qdrant needed
    }
  }
}
```

**Deployment**: Docker Compose (no Terraform/K8s needed)

```bash
docker-compose -f docker-compose.dev.yml up
```

### Staging Environment

**Terraform**: `terraform/environments/staging.tfvars`

```hcl
node_count = 2      # Moderate resources
cores_count = 3     # Medium nodes
ram_size = 12288    # 12GB RAM
```

**C# Configuration**: Same as production but different endpoints

**Deployment**: Kubernetes on IONOS (via Terraform)

### Production Environment

**Terraform**: `terraform/environments/production.tfvars`

```hcl
node_count = 3      # Full resources
cores_count = 4     # Large nodes
ram_size = 16384    # 16GB RAM
storage_type = "SSD"  # Fast storage
```

**C# Configuration**: `appsettings.Production.json`

**Deployment**: Full Kubernetes stack on IONOS (via Terraform)

### Environment Comparison Matrix

| Aspect | Development | Staging | Production |
|--------|-------------|---------|------------|
| **Terraform** | Not used | Used | Used |
| **K8s Cluster** | Local (optional) | IONOS MKS | IONOS MKS |
| **Container Registry** | Local | IONOS Registry | IONOS Registry |
| **Node Count** | N/A | 2 | 3 |
| **Node Size** | N/A | 3 cores, 12GB | 4 cores, 16GB |
| **Storage** | Local volumes | SSD, 50GB+100GB | SSD, 50GB+100GB |
| **C# Vector Store** | InMemory | Qdrant | Qdrant |
| **C# LLM Endpoint** | localhost:11434 | ollama-service:11434 | ollama-service:11434 |
| **Observability** | Debug logs | Metrics + Traces | Full stack |

### Configuration Override Strategy

**Development** (Local):
```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

**Staging** (K8s):
```yaml
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Staging"
```

**Production** (K8s):
```yaml
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Production"
```

C# `PipelineConfigurationBuilder` automatically selects the correct `appsettings.{Environment}.json` file.

## Best Practices

### Infrastructure Changes

1. **Always update Terraform first** before changing K8s resources
2. **Test in dev/staging** before production
3. **Document dependencies** when adding new services
4. **Version control everything** (infrastructure as code)

### Configuration Management

1. **Use environment variables** for environment-specific values
2. **Never hardcode** infrastructure endpoints in C# code
3. **Use Terraform outputs** as source of truth for infrastructure values
4. **Validate configurations** before deployment

### Deployment Safety

1. **Backup Terraform state** before major changes
2. **Use `terraform plan`** to preview changes
3. **Test K8s manifests** with `kubectl apply --dry-run`
4. **Monitor deployments** with health checks
5. **Have rollback plan** ready

### Dependency Updates

When updating infrastructure:

```
1. Update Terraform variables
2. Run terraform plan
3. Apply Terraform changes
4. Update K8s manifests if needed
5. Update C# configuration if needed
6. Test end-to-end
7. Document changes
```

## Troubleshooting

### Common Dependency Issues

**Issue**: Application can't connect to Ollama
```
Check:
1. Is ollama-service deployed? kubectl get svc ollama-service -n monadic-pipeline
2. Is DNS resolving? kubectl run -it --rm debug --image=busybox -- nslookup ollama-service.monadic-pipeline.svc.cluster.local
3. Is configuration correct? Check PIPELINE__LlmProvider__OllamaEndpoint
```

**Issue**: ImagePullBackOff errors
```
Check:
1. Is registry accessible? terraform output registry_hostname
2. Are images pushed? docker images | grep adaptive-systems
3. Is pull secret created? kubectl get secret ionos-registry-secret -n monadic-pipeline
4. Is secret referenced in deployment? Check imagePullSecrets in YAML
```

**Issue**: Persistent volume not mounting
```
Check:
1. Is volume created in Terraform? terraform state list | grep volume
2. Is StorageClass available? kubectl get storageclass
3. Is PVC bound? kubectl get pvc -n monadic-pipeline
4. Are permissions correct? Check volume mount in pod spec
```

## Conclusion

Understanding these dependencies is crucial for:
- **Successful deployments**: Know the correct order of operations
- **Troubleshooting**: Identify where failures occur in the stack
- **Infrastructure evolution**: Plan changes that maintain compatibility
- **Team collaboration**: Clear communication about infrastructure needs

All infrastructure changes should consider impacts across all three layers: Terraform, Kubernetes, and C# Application.

---

**Version**: 1.0.0
**Last Updated**: 2025-01-XX
**Maintained By**: Infrastructure Team
