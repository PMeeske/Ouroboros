# Environment-Specific Infrastructure Mapping

This document provides detailed mappings between C# application configuration, Kubernetes resources, and Terraform infrastructure for each environment.

## Environment Overview

| Environment | Purpose | Terraform | Kubernetes | C# Config |
|-------------|---------|-----------|------------|-----------|
| **Development** | Local development | Not used | Optional (local) | appsettings.Development.json |
| **Staging** | Pre-production testing | IONOS Cloud | IONOS MKS | appsettings.Production.json + staging env vars |
| **Production** | Live system | IONOS Cloud | IONOS MKS | appsettings.Production.json |

## Development Environment

### Infrastructure Setup

**Deployment Method**: Docker Compose (no Terraform/Kubernetes)

**File**: `docker-compose.dev.yml`

### C# Configuration

**File**: `appsettings.Development.json`

```json
{
  "Pipeline": {
    "LlmProvider": {
      "OllamaEndpoint": "http://localhost:11434"
    },
    "VectorStore": {
      "Type": "InMemory"  // No Qdrant needed
    },
    "Execution": {
      "EnableDebugOutput": true
    },
    "Observability": {
      "MinimumLogLevel": "Debug",
      "EnableMetrics": false,
      "EnableTracing": false
    }
  }
}
```

### Dependencies

| Service | Provider | Endpoint | Notes |
|---------|----------|----------|-------|
| Ollama | Docker Compose | localhost:11434 | Local container |
| Vector Store | In-Memory | N/A | No persistence |
| Jaeger | Not used | N/A | Optional |

### Resource Requirements

- **CPU**: 2 cores minimum
- **Memory**: 8GB minimum
- **Storage**: 20GB for Ollama models

### Deployment Commands

```bash
# Start development environment
docker-compose -f docker-compose.dev.yml up -d

# Run application
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/Ouroboros.CLI

# Or use the CLI in container
docker exec -it monadic-pipeline dotnet LangChainPipeline.dll ask -q "Test"
```

### Configuration Override

```bash
# Override LLM endpoint
export PIPELINE__LlmProvider__OllamaEndpoint=http://custom-ollama:11434

# Enable debug logging
export PIPELINE__Observability__MinimumLogLevel=Debug
```

### Terraform Mapping

**Status**: ❌ Not applicable (local development)

### Kubernetes Mapping

**Status**: ⚠️ Optional (can use local Kubernetes for testing)

If using local Kubernetes:
```bash
kubectl apply -f k8s/deployment.yaml  # Use local manifest, not cloud manifest
```

## Staging Environment

### Infrastructure Setup

**Deployment Method**: Terraform + Kubernetes (IONOS Cloud)

**Terraform Configuration**: `terraform/environments/staging.tfvars`

```hcl
# Staging Environment - Moderate Resources
datacenter_name = "monadic-pipeline-staging"
location        = "de/fra"

cluster_name = "monadic-pipeline-staging-cluster"
k8s_version  = "1.28"

# Staging node pool
node_pool_name = "staging-pool"
node_count     = 2         # Reduced from production
cores_count    = 3         # Reduced from production
ram_size       = 12288     # 12 GB (reduced from 16GB)
storage_size   = 80        # Reduced from 100GB
storage_type   = "SSD"

# Container registry (shared with production)
registry_name     = "adaptive-systems"
registry_location = "de/fra"

# Staging storage volumes
volumes = [
  {
    name         = "qdrant-data-staging"
    size         = 30      # Smaller than production
    type         = "SSD"
    licence_type = "OTHER"
  },
  {
    name         = "ollama-models-staging"
    size         = 50      # Smaller than production
    type         = "SSD"
    licence_type = "OTHER"
  }
]

environment = "staging"
```

### C# Configuration

**Base**: `appsettings.Production.json` (same as production)

**Overrides via Environment Variables**:

```yaml
# In k8s/deployment.cloud.yaml (staging namespace)
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Staging"
  - name: PIPELINE__LlmProvider__OllamaEndpoint
    value: "http://ollama-service.monadic-pipeline-staging:11434"
  - name: PIPELINE__VectorStore__ConnectionString
    value: "http://qdrant-service.monadic-pipeline-staging:6333"
  - name: PIPELINE__Observability__EnableMetrics
    value: "true"
  - name: PIPELINE__Observability__MinimumLogLevel
    value: "Information"  # Less verbose than dev, more than prod
```

### Terraform to C# Mapping

| Terraform Resource | Output | K8s Resource | C# Config Path |
|-------------------|--------|--------------|----------------|
| `module.kubernetes` | `k8s_kubeconfig` | kubectl context | N/A (infrastructure) |
| `module.registry` | `registry_hostname` | Image URL | N/A (build-time) |
| `module.datacenter` | `datacenter_id` | Annotations | N/A (metadata) |
| `var.volumes[0]` | N/A | PVC: qdrant-data-staging | `Pipeline:VectorStore` (indirect) |
| `var.volumes[1]` | N/A | PVC: ollama-models-staging | `Pipeline:LlmProvider` (indirect) |

### Kubernetes Resources

**Namespace**: `monadic-pipeline-staging`

```bash
# Create staging namespace
kubectl create namespace monadic-pipeline-staging

# Apply staging manifests
kubectl apply -f k8s/namespace.yaml -n monadic-pipeline-staging
kubectl apply -f k8s/configmap.yaml -n monadic-pipeline-staging
kubectl apply -f k8s/secrets.yaml -n monadic-pipeline-staging
kubectl apply -f k8s/ollama.yaml -n monadic-pipeline-staging
kubectl apply -f k8s/qdrant.yaml -n monadic-pipeline-staging
kubectl apply -f k8s/deployment.cloud.yaml -n monadic-pipeline-staging
```

### Resource Allocation

**Terraform Node Allocation**:
- 2 nodes × 3 cores × 12GB = 6 cores, 24GB total cluster capacity

**Kubernetes Resource Requests** (per pod):
```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "250m"
  limits:
    memory: "2Gi"
    cpu: "1000m"
```

**Application Deployment**:
- Ouroboros: 1 replica (staging has less traffic)
- Ollama: 1 replica (2 cores, 6GB)
- Qdrant: 1 replica (0.5 cores, 2GB)
- Jaeger: 1 replica (0.5 cores, 1GB)

**Total**: ~3-4 cores, ~10GB RAM (fits in 2 nodes)

### Deployment Workflow

```bash
# 1. Apply Terraform infrastructure
cd terraform
terraform init
terraform apply -var-file=environments/staging.tfvars

# 2. Configure kubectl
terraform output -raw k8s_kubeconfig > ../kubeconfig.staging.yaml
export KUBECONFIG=../kubeconfig.staging.yaml

# 3. Build and push images
REGISTRY_URL=$(terraform output -raw registry_hostname)
docker build -t ${REGISTRY_URL}/monadic-pipeline:staging .
docker push ${REGISTRY_URL}/monadic-pipeline:staging

# 4. Create secrets
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=$REGISTRY_URL \
  --docker-username=<user> \
  --docker-password=$(terraform output -raw registry_token) \
  --namespace=monadic-pipeline-staging

# 5. Deploy to Kubernetes
kubectl apply -f k8s/ -n monadic-pipeline-staging

# 6. Verify deployment
kubectl get all -n monadic-pipeline-staging
```

### Testing

```bash
# Port-forward to WebAPI
kubectl port-forward svc/monadic-pipeline-webapi-service 8080:80 -n monadic-pipeline-staging

# Test endpoint
curl http://localhost:8080/health

# View logs
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline-staging
```

## Production Environment

### Infrastructure Setup

**Deployment Method**: Terraform + Kubernetes (IONOS Cloud)

**Terraform Configuration**: `terraform/environments/production.tfvars`

```hcl
# Production Environment - Full Resources
datacenter_name = "monadic-pipeline-prod"
location        = "de/fra"

cluster_name = "monadic-pipeline-cluster"
k8s_version  = "1.28"

# Production node pool with capacity for growth
node_pool_name = "production-pool"
node_count     = 3         # High availability
cores_count    = 4         # Full performance
ram_size       = 16384     # 16 GB
storage_size   = 100       # Ample storage
storage_type   = "SSD"     # Fast I/O

# Container registry
registry_name     = "adaptive-systems"
registry_location = "de/fra"

# Production storage volumes
volumes = [
  {
    name         = "qdrant-data"
    size         = 50      # Production data
    type         = "SSD"
    licence_type = "OTHER"
  },
  {
    name         = "ollama-models"
    size         = 100     # Multiple models
    type         = "SSD"
    licence_type = "OTHER"
  }
]

environment = "production"
```

### C# Configuration

**File**: `appsettings.Production.json`

```json
{
  "Pipeline": {
    "LlmProvider": {
      "DefaultProvider": "Ollama",
      "OllamaEndpoint": "http://ollama-service:11434",
      "RequestTimeoutSeconds": 180
    },
    "VectorStore": {
      "Type": "Qdrant",
      "ConnectionString": "${VECTOR_STORE_CONNECTION_STRING}",
      "BatchSize": 200
    },
    "Execution": {
      "MaxTurns": 5,
      "MaxParallelToolExecutions": 10,
      "EnableDebugOutput": false
    },
    "Observability": {
      "EnableStructuredLogging": true,
      "MinimumLogLevel": "Warning",
      "EnableMetrics": true,
      "EnableTracing": true,
      "ApplicationInsightsConnectionString": "${APPLICATION_INSIGHTS_CONNECTION_STRING}"
    }
  }
}
```

### Terraform to C# Complete Mapping

```
┌─────────────────────────────────────────────────────────────┐
│ Terraform Infrastructure (terraform/environments/production.tfvars) │
└──────────────────────┬──────────────────────────────────────┘
                       │
      ┌────────────────┼────────────────┐
      │                │                │
      ▼                ▼                ▼
┌──────────┐    ┌──────────┐    ┌──────────┐
│Datacenter│    │ K8s      │    │Container │
│          │    │ Cluster  │    │ Registry │
└─────┬────┘    └─────┬────┘    └─────┬────┘
      │               │               │
      │         ┌─────┴─────┐        │
      │         │           │        │
      ▼         ▼           ▼        ▼
┌──────────────────────────────────────────┐
│ Kubernetes Resources (k8s/*.yaml)        │
├──────────────────────────────────────────┤
│ - Namespace: monadic-pipeline            │
│ - ConfigMap: pipeline-config             │
│ - Secrets: credentials                   │
│ - Services: ollama, qdrant, jaeger       │
│ - Deployments: app, services             │
│ - PVCs: storage volumes                  │
└──────────────────┬───────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────┐
│ C# Application (appsettings.Production.json) │
├──────────────────────────────────────────┤
│ Pipeline:LlmProvider:OllamaEndpoint      │
│   → http://ollama-service:11434          │
│ Pipeline:VectorStore:ConnectionString    │
│   → http://qdrant-service:6333           │
│ Pipeline:Observability:OpenTelemetryEndpoint │
│   → http://jaeger-collector:4317         │
└──────────────────────────────────────────┘
```

### Resource Allocation

**Terraform Node Allocation**:
- 3 nodes × 4 cores × 16GB = 12 cores, 48GB total cluster capacity

**Kubernetes Resource Requests** (production):
```yaml
# Application (2 replicas for HA)
resources:
  requests:
    memory: "512Mi"   # 2 × 512Mi = 1Gi
    cpu: "250m"       # 2 × 250m = 500m
  limits:
    memory: "2Gi"     # 2 × 2Gi = 4Gi
    cpu: "1000m"      # 2 × 1000m = 2000m (2 cores)
```

**Total Production Allocation**:
- Ouroboros: 2 replicas (2 cores, 4GB)
- Ouroboros WebAPI: 2 replicas (2 cores, 4GB)
- Ollama: 1 replica (2 cores, 8GB)
- Qdrant: 1 replica (1 core, 2GB)
- Jaeger: 1 replica (0.5 cores, 1GB)
- System/K8s: ~1.5 cores, 3GB

**Total**: ~9 cores, ~22GB (fits comfortably in 3×4core/16GB nodes)

### High Availability Configuration

**Terraform**:
```hcl
node_count = 3  # Allows for 1 node failure
availability_zone = "AUTO"  # IONOS distributes across AZs
```

**Kubernetes**:
```yaml
# In deployment.cloud.yaml
replicas: 2  # For HA

# Pod anti-affinity (to be added)
affinity:
  podAntiAffinity:
    preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          labelSelector:
            matchExpressions:
              - key: app
                operator: In
                values:
                  - monadic-pipeline
          topologyKey: kubernetes.io/hostname
```

### Monitoring and Observability

**Terraform** (infrastructure metrics):
- IONOS Cloud Console provides node/cluster metrics

**Kubernetes** (application metrics):
```yaml
# From appsettings.Production.json
env:
  - name: PIPELINE__Observability__EnableMetrics
    value: "true"
  - name: PIPELINE__Observability__EnableTracing
    value: "true"
  - name: PIPELINE__Observability__OpenTelemetryEndpoint
    value: "http://jaeger-collector:4317"
```

**C# Application** (structured logging):
- Logs sent to stdout
- Captured by Kubernetes
- Viewable via `kubectl logs`

### Deployment Workflow (Production)

```bash
# 1. Review and plan infrastructure changes
cd terraform
terraform init
terraform plan -var-file=environments/production.tfvars

# 2. Apply with approval
terraform apply -var-file=environments/production.tfvars

# 3. Extract outputs
terraform output -raw k8s_kubeconfig > ../kubeconfig.prod.yaml
REGISTRY_URL=$(terraform output -raw registry_hostname)

# 4. Build production images with version tags
VERSION=$(git describe --tags --always)
docker build -t ${REGISTRY_URL}/monadic-pipeline:${VERSION} .
docker build -t ${REGISTRY_URL}/monadic-pipeline:latest .
docker push ${REGISTRY_URL}/monadic-pipeline:${VERSION}
docker push ${REGISTRY_URL}/monadic-pipeline:latest

# 5. Configure kubectl
export KUBECONFIG=./kubeconfig.prod.yaml

# 6. Apply Kubernetes manifests
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml  # After updating with production secrets
kubectl apply -f k8s/ollama.yaml
kubectl apply -f k8s/qdrant.yaml
kubectl apply -f k8s/jaeger.yaml
kubectl apply -f k8s/deployment.cloud.yaml
kubectl apply -f k8s/webapi-deployment.cloud.yaml

# 7. Verify deployment
kubectl get all -n monadic-pipeline
kubectl rollout status deployment/monadic-pipeline -n monadic-pipeline
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline

# 8. Run health checks
kubectl run -it --rm healthcheck --image=curlimages/curl --restart=Never -- \
  curl http://monadic-pipeline-webapi-service/health
```

### Rollback Procedure

```bash
# If deployment fails, rollback to previous version
kubectl rollout undo deployment/monadic-pipeline -n monadic-pipeline
kubectl rollout undo deployment/monadic-pipeline-webapi -n monadic-pipeline

# Or rollback to specific revision
kubectl rollout history deployment/monadic-pipeline -n monadic-pipeline
kubectl rollout undo deployment/monadic-pipeline --to-revision=<number> -n monadic-pipeline
```

## Cross-Environment Comparison

### Resource Sizing

| Resource | Development | Staging | Production |
|----------|-------------|---------|------------|
| **Nodes** | 0 (local) | 2 | 3 |
| **Cores/Node** | 2 (local) | 3 | 4 |
| **RAM/Node** | 8GB (local) | 12GB | 16GB |
| **Total Cores** | 2 | 6 | 12 |
| **Total RAM** | 8GB | 24GB | 48GB |
| **Qdrant Storage** | N/A | 30GB | 50GB |
| **Ollama Storage** | 20GB (local) | 50GB | 100GB |

### Application Configuration

| Config Path | Development | Staging | Production |
|-------------|-------------|---------|------------|
| `LlmProvider:OllamaEndpoint` | localhost:11434 | ollama-service:11434 | ollama-service:11434 |
| `VectorStore:Type` | InMemory | Qdrant | Qdrant |
| `VectorStore:ConnectionString` | N/A | qdrant-service:6333 | qdrant-service:6333 |
| `Execution:EnableDebugOutput` | true | false | false |
| `Observability:MinimumLogLevel` | Debug | Information | Warning |
| `Observability:EnableMetrics` | false | true | true |
| `Observability:EnableTracing` | false | true | true |

### Deployment Method

| Aspect | Development | Staging | Production |
|--------|-------------|---------|------------|
| **Infrastructure** | Docker Compose | Terraform + IONOS | Terraform + IONOS |
| **Orchestration** | Docker | Kubernetes | Kubernetes |
| **Registry** | Local | IONOS Registry | IONOS Registry |
| **Secrets** | .env file | K8s Secrets | K8s Secrets + External Vault |
| **Monitoring** | Console logs | Basic metrics | Full observability stack |
| **HA** | No | Limited (2 nodes) | Yes (3 nodes) |
| **Auto-scaling** | No | Manual | Potential (HPA) |

## Configuration Best Practices

### Environment Variable Naming

Follow hierarchical naming for C# configuration:

```bash
# Correct
export PIPELINE__LlmProvider__OllamaEndpoint="http://ollama:11434"
export PIPELINE__VectorStore__ConnectionString="http://qdrant:6333"

# Incorrect (won't bind to C# config)
export OLLAMA_ENDPOINT="http://ollama:11434"
```

### Secret Management

**Development**:
```bash
# Use .env file or user secrets
dotnet user-secrets set "Pipeline:OpenAiApiKey" "sk-..."
```

**Staging/Production**:
```yaml
# Use Kubernetes secrets
apiVersion: v1
kind: Secret
metadata:
  name: monadic-pipeline-secrets
stringData:
  openai-api-key: "sk-..."
  vector-store-connection-string: "http://qdrant-service:6333"
```

### Infrastructure Changes

**Process**:
1. Update environment tfvars file
2. Run `terraform plan` to preview
3. Test in dev/staging first
4. Apply to production during maintenance window
5. Verify with health checks

### Monitoring Changes

After infrastructure changes:

```bash
# Check node status
kubectl get nodes

# Check pod status
kubectl get pods -n monadic-pipeline

# Check resource usage
kubectl top nodes
kubectl top pods -n monadic-pipeline

# Check application logs
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline --tail=100
```

## Troubleshooting by Environment

### Development Issues

**Issue**: Ollama not responding
```bash
# Check container
docker ps | grep ollama
docker logs ollama

# Restart
docker-compose -f docker-compose.dev.yml restart ollama
```

### Staging Issues

**Issue**: Pods not starting
```bash
# Check events
kubectl get events -n monadic-pipeline-staging --sort-by='.lastTimestamp'

# Check pod details
kubectl describe pod <pod-name> -n monadic-pipeline-staging

# Check logs
kubectl logs <pod-name> -n monadic-pipeline-staging
```

### Production Issues

**Issue**: High memory usage
```bash
# Check node resources
kubectl top nodes

# Identify heavy pods
kubectl top pods -n monadic-pipeline --sort-by=memory

# Scale down if needed
kubectl scale deployment monadic-pipeline --replicas=1 -n monadic-pipeline

# Review Terraform node sizing
cd terraform
terraform output deployment_summary
```

## Conclusion

This environment mapping ensures:
- ✅ Consistent configuration across environments
- ✅ Proper resource allocation at each level
- ✅ Clear dependency paths from C# to infrastructure
- ✅ Smooth progression from dev → staging → production

Always test infrastructure changes in lower environments before applying to production.

---

**Version**: 1.0.0
**Last Updated**: 2025-01-XX
**Maintained By**: Infrastructure Team
