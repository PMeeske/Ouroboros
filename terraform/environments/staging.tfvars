# Staging Environment Configuration
# Medium resource allocation for pre-production testing

datacenter_name = "monadic-pipeline-staging"
location        = "de/fra"

cluster_name = "monadic-pipeline-staging"
k8s_version  = "1.30.14"  # Stable version for IONOS Cloud (1.29 no longer supported)

# Medium node pool for staging
node_pool_name = "staging-pool"
node_count     = 2
cores_count    = 4
ram_size       = 16384 # 16 GB
storage_size   = 100
storage_type   = "SSD"

# Container registry
registry_name     = "monadic-staging"
registry_location = "de/fra"

# Medium storage volumes
volumes = [
  {
    name         = "qdrant-data-staging"
    size         = 40
    type         = "SSD"
    licence_type = "OTHER"
  },
  {
    name         = "ollama-models-staging"
    size         = 80
    type         = "SSD"
    licence_type = "OTHER"
  }
]

environment = "staging"

tags = {
  Project     = "Ouroboros"
  Environment = "Staging"
  ManagedBy   = "Terraform"
  Repository  = "PMeeske/Ouroboros"
}
