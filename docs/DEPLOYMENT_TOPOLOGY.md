# Infrastructure Deployment Topology

This document provides visual and detailed topological representations of the MonadicPipeline infrastructure across all deployment layers.

## Table of Contents

1. [Complete Stack Topology](#complete-stack-topology)
2. [Terraform Infrastructure Topology](#terraform-infrastructure-topology)
3. [Kubernetes Cluster Topology](#kubernetes-cluster-topology)
4. [Application Service Topology](#application-service-topology)
5. [Network Topology](#network-topology)
6. [Data Flow Topology](#data-flow-topology)
7. [Security Zones](#security-zones)

## Complete Stack Topology

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          IONOS Cloud Platform                                │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                    Data Center (de/fra)                               │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │              Kubernetes Cluster (MKS)                            │  │  │
│  │  │  ┌───────────────────────────────────────────────────────────┐  │  │  │
│  │  │  │  Node 1 (4 cores, 16GB)                                   │  │  │  │
│  │  │  │  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐      │  │  │  │
│  │  │  │  │ WebAPI Pod  │  │ Ollama Pod  │  │ Qdrant Pod   │      │  │  │  │
│  │  │  │  │ (2Gi, 1cpu) │  │ (8Gi, 2cpu) │  │ (2Gi, 1cpu)  │      │  │  │  │
│  │  │  │  └─────────────┘  └─────────────┘  └──────────────┘      │  │  │  │
│  │  │  └───────────────────────────────────────────────────────────┘  │  │  │
│  │  │  ┌───────────────────────────────────────────────────────────┐  │  │  │
│  │  │  │  Node 2 (4 cores, 16GB)                                   │  │  │  │
│  │  │  │  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐      │  │  │  │
│  │  │  │  │ WebAPI Pod  │  │ CLI Pod     │  │ Jaeger Pod   │      │  │  │  │
│  │  │  │  │ (2Gi, 1cpu) │  │ (2Gi, 1cpu) │  │ (1Gi, 0.5cpu)│      │  │  │  │
│  │  │  │  └─────────────┘  └─────────────┘  └──────────────┘      │  │  │  │
│  │  │  └───────────────────────────────────────────────────────────┘  │  │  │
│  │  │  ┌───────────────────────────────────────────────────────────┐  │  │  │
│  │  │  │  Node 3 (4 cores, 16GB) - Standby/Scale                  │  │  │  │
│  │  │  └───────────────────────────────────────────────────────────┘  │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │              Persistent Storage (IONOS SSD)                     │  │  │
│  │  │  ├─ qdrant-data (50GB)                                          │  │  │
│  │  │  └─ ollama-models (100GB)                                       │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │              Virtual Network (LAN)                              │  │  │
│  │  │  ├─ Internal: 10.0.0.0/24                                       │  │  │
│  │  │  └─ Public: External IPs for LoadBalancer                       │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │        Container Registry (adaptive-systems.cr.de-fra.ionos.com)      │  │
│  │  ├─ monadic-pipeline:latest                                           │  │
│  │  ├─ monadic-pipeline-webapi:latest                                    │  │
│  │  └─ Authentication: Registry Token                                    │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘

External Access:
  ├─ LoadBalancer IP → WebAPI Service (Port 80/443)
  ├─ Registry Access → Docker Push/Pull
  └─ kubectl → Kubernetes API (via kubeconfig)
```

## Terraform Infrastructure Topology

### Module Dependency Graph

```
terraform/main.tf
├── module "datacenter"
│   ├── Resource: ionoscloud_datacenter.main
│   └── Outputs:
│       ├── datacenter_id
│       ├── datacenter_name
│       └── datacenter_location
│
├── module "kubernetes" [depends_on: datacenter]
│   ├── Resource: ionoscloud_k8s_cluster.main
│   ├── Resource: ionoscloud_k8s_node_pool.main
│   └── Outputs:
│       ├── cluster_id
│       ├── cluster_name
│       ├── kubeconfig (sensitive)
│       ├── node_pool_id
│       └── public_ips
│
├── module "registry" [independent]
│   ├── Resource: ionoscloud_container_registry.main
│   ├── Resource: ionoscloud_container_registry_token.main
│   └── Outputs:
│       ├── registry_id
│       ├── registry_hostname
│       └── registry_token (sensitive)
│
├── module "networking" [depends_on: datacenter]
│   ├── Resource: ionoscloud_lan.main
│   └── Outputs:
│       ├── lan_id
│       ├── lan_name
│       └── lan_public
│
└── module "app-config" [validation module]
    └── Outputs:
        ├── app_config
        ├── recommended_node_config
        ├── resource_validation
        └── k8s_configmap_data
```

### Resource Provisioning Order

```
Step 1: Data Center Creation
  └─ ionoscloud_datacenter.main

Step 2: Container Registry Creation (parallel)
  └─ ionoscloud_container_registry.main
  └─ ionoscloud_container_registry_token.main

Step 3: Network Resources
  └─ ionoscloud_lan.main (requires datacenter_id)

Step 4: Kubernetes Cluster
  └─ ionoscloud_k8s_cluster.main (requires datacenter_id)

Step 5: Node Pool
  └─ ionoscloud_k8s_node_pool.main (requires cluster_id)

Step 6: Validation
  └─ module.app_config (validates configuration)
```

## Kubernetes Cluster Topology

### Namespace Organization

```
Kubernetes Cluster
├── Namespace: monadic-pipeline
│   ├── ConfigMaps
│   │   └── monadic-pipeline-config
│   │       └── appsettings.Production.json
│   │
│   ├── Secrets
│   │   ├── monadic-pipeline-secrets
│   │   │   ├── openai-api-key
│   │   │   ├── vector-store-connection-string
│   │   │   └── application-insights-connection-string
│   │   └── ionos-registry-secret
│   │       └── .dockerconfigjson
│   │
│   ├── Deployments
│   │   ├── monadic-pipeline (1 replica)
│   │   ├── monadic-pipeline-webapi (2 replicas)
│   │   ├── ollama (1 replica)
│   │   ├── qdrant (1 replica)
│   │   └── jaeger (1 replica)
│   │
│   ├── Services
│   │   ├── monadic-pipeline-service (ClusterIP)
│   │   ├── monadic-pipeline-webapi-service (ClusterIP/LoadBalancer)
│   │   ├── ollama-service (ClusterIP)
│   │   ├── qdrant-service (ClusterIP)
│   │   └── jaeger-collector (ClusterIP)
│   │
│   ├── PersistentVolumeClaims
│   │   ├── qdrant-storage (50Gi, SSD)
│   │   └── ollama-models (100Gi, SSD)
│   │
│   └── Ingress
│       └── monadic-pipeline-webapi-ingress
│
├── Namespace: kube-system
│   └── (System components, IONOS CSI driver, etc.)
│
└── Namespace: default
    └── (Unused for application)
```

### Pod Distribution Strategy

```
Node 1 (Primary):
├── monadic-pipeline-webapi-xxx (replica 1)
├── ollama-yyy
└── qdrant-zzz

Node 2 (Secondary):
├── monadic-pipeline-webapi-aaa (replica 2)
├── monadic-pipeline-bbb
└── jaeger-ccc

Node 3 (Standby):
└── Available for scaling

Anti-affinity ensures:
- WebAPI replicas on different nodes (HA)
- Critical services distributed
- Resource balancing across nodes
```

## Application Service Topology

### Service Communication Flow

```
┌──────────────────────────────────────────────────────────────┐
│                      External User                           │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS/HTTP
                         ▼
┌──────────────────────────────────────────────────────────────┐
│            LoadBalancer / Ingress                            │
│          (monadic-pipeline-webapi-ingress)                   │
└────────────────────────┬─────────────────────────────────────┘
                         │ Port 80
                         ▼
┌──────────────────────────────────────────────────────────────┐
│      Service: monadic-pipeline-webapi-service (ClusterIP)    │
└────────────────────────┬─────────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ WebAPI Pod  │  │ WebAPI Pod  │  │ (Scale out) │
│  Replica 1  │  │  Replica 2  │  │             │
└──────┬──────┘  └──────┬──────┘  └─────────────┘
       │                │
       └────────┬───────┘
                │ Internal calls
                ▼
┌──────────────────────────────────────────────────────────────┐
│        Service: ollama-service (ClusterIP:11434)             │
└────────────────────────┬─────────────────────────────────────┘
                         ▼
                  ┌─────────────┐
                  │ Ollama Pod  │
                  │ (LLM Inference)
                  └──────┬──────┘
                         │
                         │ Model loading
                         ▼
                  ┌─────────────┐
                  │ PVC: ollama │
                  │ (100GB SSD) │
                  └─────────────┘

┌──────────────────────────────────────────────────────────────┐
│      Service: qdrant-service (ClusterIP:6333)                │
└────────────────────────┬─────────────────────────────────────┘
                         ▼
                  ┌─────────────┐
                  │ Qdrant Pod  │
                  │ (Vector DB) │
                  └──────┬──────┘
                         │
                         │ Data persistence
                         ▼
                  ┌─────────────┐
                  │ PVC: qdrant │
                  │ (50GB SSD)  │
                  └─────────────┘

┌──────────────────────────────────────────────────────────────┐
│    Service: jaeger-collector (ClusterIP:4317)                │
└────────────────────────┬─────────────────────────────────────┘
                         ▼
                  ┌─────────────┐
                  │ Jaeger Pod  │
                  │ (Tracing)   │
                  └─────────────┘
```

### C# Application Internal Architecture

```
┌──────────────────────────────────────────────────────────────┐
│              MonadicPipeline Application                     │
├──────────────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────────┐      │
│  │           PipelineConfiguration                    │      │
│  │  ┌──────────────────────────────────────────────┐  │      │
│  │  │ LlmProviderConfiguration                     │  │      │
│  │  │  - OllamaEndpoint: ollama-service:11434     │  │      │
│  │  └──────────────────────────────────────────────┘  │      │
│  │  ┌──────────────────────────────────────────────┐  │      │
│  │  │ VectorStoreConfiguration                     │  │      │
│  │  │  - Type: Qdrant                             │  │      │
│  │  │  - ConnectionString: qdrant-service:6333    │  │      │
│  │  └──────────────────────────────────────────────┘  │      │
│  │  ┌──────────────────────────────────────────────┐  │      │
│  │  │ ObservabilityConfiguration                   │  │      │
│  │  │  - OpenTelemetryEndpoint: jaeger:4317       │  │      │
│  │  └──────────────────────────────────────────────┘  │      │
│  └────────────────────────────────────────────────────┘      │
│                                                                │
│  ┌────────────────────────────────────────────────────┐      │
│  │           Pipeline Execution Layer                 │      │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────┐ │      │
│  │  │ Draft Arrow  │→ │Critique Arrow│→ │Final Spec│ │      │
│  │  └──────────────┘  └──────────────┘  └──────────┘ │      │
│  └────────────────────────────────────────────────────┘      │
│                                                                │
│  ┌────────────────────────────────────────────────────┐      │
│  │           Service Clients                          │      │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────┐ │      │
│  │  │ LLM Client   │  │Vector Client │  │ Tracer   │ │      │
│  │  │ (HTTP)       │  │ (HTTP)       │  │ (gRPC)   │ │      │
│  │  └──────┬───────┘  └──────┬───────┘  └────┬─────┘ │      │
│  └─────────┼──────────────────┼───────────────┼───────┘      │
└────────────┼──────────────────┼───────────────┼──────────────┘
             │                  │               │
             │ DNS              │ DNS           │ DNS
             │ Resolution       │ Resolution    │ Resolution
             ▼                  ▼               ▼
      ollama-service     qdrant-service   jaeger-collector
```

## Network Topology

### Network Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    External Network                         │
│                    (Internet)                               │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           │ IONOS Cloud Edge
                           ▼
┌─────────────────────────────────────────────────────────────┐
│              Public Network (LoadBalancer)                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  LoadBalancer Service                                 │  │
│  │  - External IP: Auto-assigned by IONOS               │  │
│  │  - Ports: 80 (HTTP), 443 (HTTPS)                     │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           │ IONOS LAN (Public)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│            Kubernetes Cluster Network                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Cluster CIDR: 10.244.0.0/16 (Pod Network)           │  │
│  │  Service CIDR: 10.96.0.0/12 (Service Network)        │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  Service Network:                                            │
│  ├─ monadic-pipeline-webapi-service: 10.96.x.x:80          │
│  ├─ ollama-service: 10.96.x.x:11434                         │
│  ├─ qdrant-service: 10.96.x.x:6333                          │
│  └─ jaeger-collector: 10.96.x.x:4317                        │
│                                                              │
│  Pod Network:                                                │
│  ├─ Node 1 Pod CIDR: 10.244.0.0/24                          │
│  ├─ Node 2 Pod CIDR: 10.244.1.0/24                          │
│  └─ Node 3 Pod CIDR: 10.244.2.0/24                          │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           │ IONOS LAN (Internal)
                           ▼
┌─────────────────────────────────────────────────────────────┐
│              Node-to-Node Network                            │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Private LAN: 10.0.0.0/24                            │  │
│  │  ├─ Node 1: 10.0.0.10                               │  │
│  │  ├─ Node 2: 10.0.0.11                               │  │
│  │  └─ Node 3: 10.0.0.12                               │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### DNS Resolution Flow

```
Application requests "ollama-service:11434"
         ↓
    CoreDNS (in kube-system)
         ↓
    Resolves to ClusterIP: 10.96.x.x
         ↓
    kube-proxy (iptables/ipvs)
         ↓
    Routes to Pod IP: 10.244.y.z
         ↓
    Pod receives request
```

## Data Flow Topology

### Request Flow (User → Response)

```
1. External Request
   User Browser/Client
         ↓ HTTPS
   LoadBalancer (IONOS)
         ↓ HTTP
   Ingress Controller
         ↓
   WebAPI Service (ClusterIP)
         ↓
   WebAPI Pod (ASP.NET Core)

2. LLM Inference Request
   WebAPI Pod
         ↓ HTTP POST /api/completion
   DNS: ollama-service
         ↓ Resolves to 10.96.x.x:11434
   Ollama Service (ClusterIP)
         ↓
   Ollama Pod
         ↓ Reads from
   PVC: ollama-models (100GB)
         ↓ Model inference
   Response → WebAPI Pod

3. Vector Search Request
   WebAPI Pod
         ↓ HTTP POST /collections/search
   DNS: qdrant-service
         ↓ Resolves to 10.96.x.x:6333
   Qdrant Service (ClusterIP)
         ↓
   Qdrant Pod
         ↓ Reads/Writes
   PVC: qdrant-data (50GB)
         ↓ Vector search
   Response → WebAPI Pod

4. Telemetry Flow
   WebAPI Pod
         ↓ gRPC traces
   DNS: jaeger-collector
         ↓ Resolves to 10.96.x.x:4317
   Jaeger Collector (ClusterIP)
         ↓
   Jaeger Pod (stores in memory)

5. Response to User
   WebAPI Pod
         ↓ HTTP 200 + JSON
   WebAPI Service
         ↓
   Ingress Controller
         ↓
   LoadBalancer
         ↓ HTTPS
   User Browser/Client
```

### Configuration Flow (Terraform → C#)

```
1. Infrastructure Provisioning
   Terraform Apply
         ↓ Creates
   IONOS Resources
         ↓ Outputs
   registry_hostname, kubeconfig, etc.

2. Kubernetes Configuration
   kubectl apply
         ↓ Creates
   ConfigMap (appsettings.Production.json)
         ↓ Mounts to
   Pod at /app/appsettings.Production.json

3. Application Startup
   C# Application Boot
         ↓ Reads
   appsettings.Production.json
         ↓ Overridden by
   Environment Variables (from K8s)
         ↓ Builds
   PipelineConfiguration object

4. Service Discovery
   C# makes request to "ollama-service:11434"
         ↓ K8s DNS resolves
   ClusterIP: 10.96.x.x
         ↓ kube-proxy routes to
   Ollama Pod IP: 10.244.y.z
```

## Security Zones

### Zone Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       DMZ Zone                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  LoadBalancer (Public Internet facing)               │  │
│  │  - External IP exposed                               │  │
│  │  - TLS termination (future)                          │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │ Ingress rules
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                  Application Zone                            │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  WebAPI Pods (Authenticated endpoints)               │  │
│  │  - API endpoints                                     │  │
│  │  - Health checks                                     │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │ Internal networking
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                  Service Zone                                │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Internal Services (ClusterIP only)                  │  │
│  │  - ollama-service (no external access)              │  │
│  │  - qdrant-service (no external access)              │  │
│  │  - jaeger-collector (no external access)            │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │ Storage access
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    Data Zone                                 │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Persistent Volumes                                   │  │
│  │  - qdrant-data (encrypted at rest by IONOS)         │  │
│  │  - ollama-models (encrypted at rest by IONOS)       │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                  Management Zone                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Kubernetes API Server                               │  │
│  │  - Access via kubeconfig                            │  │
│  │  - RBAC enforced                                     │  │
│  │  - Allowed IPs only (configured in Terraform)       │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Container Registry                                  │  │
│  │  - Registry token authentication                    │  │
│  │  - Image pull only from K8s                         │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Security Boundaries

| Zone | Access Control | Data Classification | Encryption |
|------|---------------|---------------------|------------|
| DMZ | Public Internet | Public | TLS (future) |
| Application | Internal K8s + Authenticated users | Sensitive | In-transit |
| Service | Internal K8s only | Sensitive | In-transit |
| Data | Pods via PVCs | Highly Sensitive | At-rest (IONOS) |
| Management | Authorized admins only | Critical | TLS + RBAC |

## Disaster Recovery Topology

### Backup Flow

```
┌─────────────────────────────────────────────────────────────┐
│                   Production Cluster                         │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Persistent Volumes (qdrant-data, ollama-models)     │  │
│  └────────────────────────┬──────────────────────────────┘  │
└───────────────────────────┼──────────────────────────────────┘
                            │ Volume snapshots (manual/scripted)
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   IONOS Snapshots                            │
│  - Point-in-time backups                                    │
│  - Stored in same datacenter                                │
└───────────────────────────┬──────────────────────────────────┘
                            │ Export (manual)
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Off-site Backup (S3)                       │
│  - Terraform state backup                                   │
│  - Volume exports                                           │
│  - Configuration backups                                    │
└─────────────────────────────────────────────────────────────┘

Recovery Flow:
1. terraform apply (recreate infrastructure)
2. Restore volume snapshots
3. kubectl apply (redeploy applications)
4. Validate services
```

## Conclusion

This topology documentation provides:
- **Visual representation** of all infrastructure layers
- **Clear dependency mapping** between components
- **Network flow understanding** for troubleshooting
- **Security zone awareness** for compliance
- **Recovery planning** guidance

Use this as a reference when:
- Planning infrastructure changes
- Troubleshooting connectivity issues
- Designing security policies
- Implementing disaster recovery
- Onboarding new team members

---

**Version**: 1.0.0
**Last Updated**: 2025-01-XX
**Maintained By**: Infrastructure Team
