# Production Environment Configuration
# Full resource allocation for production workloads

datacenter_name = "monadic-pipeline-prod"
location        = "de/fra"

cluster_name = "monadic-pipeline-cluster"
k8s_version  = "1.30.14"  # Stable version for IONOS Cloud (1.29 no longer supported)

# Production node pool with autoscaling
node_pool_name = "production-pool"
node_count     = 3
cores_count    = 4
ram_size       = 16384 # 16 GB
storage_size   = 100
storage_type   = "SSD"

# Container registry (matches existing)
registry_name     = "adaptive-systems"
registry_location = "de/fra"

# Production storage volumes
volumes = [
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

environment = "production"

tags = {
  Project     = "Ouroboros"
  Environment = "Production"
  ManagedBy   = "Terraform"
  Repository  = "PMeeske/Ouroboros"
}
