# Development Environment Configuration
# Minimal resource allocation for cost optimization

datacenter_name = "monadic-pipeline-dev"
location        = "de/fra"

cluster_name = "monadic-pipeline-dev"
k8s_version  = "1.30.14"  # Stable version for IONOS Cloud (1.29 no longer supported)

# Smaller node pool for development
node_pool_name = "dev-pool"
node_count     = 2
cores_count    = 2
ram_size       = 8192 # 8 GB
storage_size   = 50
storage_type   = "HDD" # Use HDD for cost savings

# Container registry
registry_name     = "monadic-dev"
registry_location = "de/fra"

# Minimal storage volumes
volumes = [
  {
    name         = "qdrant-data-dev"
    size         = 20
    type         = "HDD"
    licence_type = "OTHER"
  },
  {
    name         = "ollama-models-dev"
    size         = 50
    type         = "HDD"
    licence_type = "OTHER"
  }
]

environment = "dev"

tags = {
  Project     = "Ouroboros"
  Environment = "Development"
  ManagedBy   = "Terraform"
  Repository  = "PMeeske/Ouroboros"
}
