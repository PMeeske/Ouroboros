# Application Configuration Module for Terraform
# This module bridges Terraform infrastructure with C# application configuration

terraform {
  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }
}

# Local values derived from application configuration
locals {
  # Parse C# appsettings requirements
  app_config = {
    # LLM Provider requirements
    llm_provider = {
      endpoint_protocol = "http"
      endpoint_port     = 11434
      service_name      = "ollama-service"
      min_memory_mb     = 4096  # Minimum for LLM models
      min_cpu_cores     = 2     # Minimum for inference
    }
    
    # Vector Store requirements
    vector_store = {
      endpoint_protocol = "http"
      endpoint_port     = 6333
      service_name      = "qdrant-service"
      min_storage_gb    = 50    # From Terraform volumes
      min_memory_mb     = 2048
      min_cpu_cores     = 0.5
    }
    
    # Observability requirements
    observability = {
      endpoint_protocol = "http"
      jaeger_port       = 4317
      service_name      = "jaeger-collector"
      min_memory_mb     = 1024
      min_cpu_cores     = 0.5
    }
  }
  
  # Calculate required node resources based on app requirements
  total_required_resources = {
    # Application itself
    app_memory_mb = var.app_replica_count * var.app_memory_limit_mb
    app_cpu_cores = var.app_replica_count * var.app_cpu_limit_cores
    
    # Dependencies (Ollama, Qdrant, Jaeger)
    services_memory_mb = (
      local.app_config.llm_provider.min_memory_mb +
      local.app_config.vector_store.min_memory_mb +
      local.app_config.observability.min_memory_mb
    )
    services_cpu_cores = (
      local.app_config.llm_provider.min_cpu_cores +
      local.app_config.vector_store.min_cpu_cores +
      local.app_config.observability.min_cpu_cores
    )
    
    # System overhead (K8s, system processes)
    system_memory_mb = 2048
    system_cpu_cores = 0.5
  }
  
  # Recommended node configuration
  recommended_node_config = {
    min_nodes = ceil(
      (local.total_required_resources.app_memory_mb +
       local.total_required_resources.services_memory_mb +
       local.total_required_resources.system_memory_mb) / var.ram_size
    )
    min_cores_per_node = ceil(
      (local.total_required_resources.app_cpu_cores +
       local.total_required_resources.services_cpu_cores +
       local.total_required_resources.system_cpu_cores)
    )
  }
}

# Variables from application requirements
variable "app_replica_count" {
  description = "Number of application replicas (from K8s deployment)"
  type        = number
  default     = 2
}

variable "app_memory_limit_mb" {
  description = "Memory limit per application pod in MB (from K8s resources.limits.memory)"
  type        = number
  default     = 2048  # 2Gi from deployment.cloud.yaml
}

variable "app_cpu_limit_cores" {
  description = "CPU limit per application pod in cores (from K8s resources.limits.cpu)"
  type        = number
  default     = 1  # 1000m from deployment.cloud.yaml
}

variable "ram_size" {
  description = "RAM size per node in MB (from main variables)"
  type        = number
  default     = 16384  # 16GB
}

variable "node_count" {
  description = "Number of nodes in the cluster"
  type        = number
  default     = 3
}

variable "cores_count" {
  description = "Number of CPU cores per node"
  type        = number
  default     = 4
}

variable "volumes" {
  description = "Storage volumes configuration"
  type = list(object({
    name = string
    size = number
    type = string
  }))
  default = [
    { name = "qdrant-data", size = 50, type = "SSD" },
    { name = "ollama-models", size = 100, type = "SSD" }
  ]
}

# Outputs for validation and K8s configuration
output "app_config" {
  description = "Application configuration requirements"
  value       = local.app_config
}

output "recommended_node_config" {
  description = "Recommended node configuration based on app requirements"
  value       = local.recommended_node_config
}

output "total_required_resources" {
  description = "Total resources required by application and services"
  value       = local.total_required_resources
}

# Validation checks
output "resource_validation" {
  description = "Validation of resource allocation"
  value = {
    sufficient_nodes = var.node_count >= local.recommended_node_config.min_nodes
    sufficient_cores = var.cores_count >= local.recommended_node_config.min_cores_per_node
    ollama_storage_sufficient = var.volumes[1].size >= local.app_config.llm_provider.min_memory_mb / 1024
    qdrant_storage_sufficient = var.volumes[0].size >= local.app_config.vector_store.min_storage_gb
  }
}

# ConfigMap generation helper
output "k8s_configmap_data" {
  description = "Data for Kubernetes ConfigMap generation"
  value = {
    ollama_endpoint = format(
      "%s://%s:%d",
      local.app_config.llm_provider.endpoint_protocol,
      local.app_config.llm_provider.service_name,
      local.app_config.llm_provider.endpoint_port
    )
    qdrant_endpoint = format(
      "%s://%s:%d",
      local.app_config.vector_store.endpoint_protocol,
      local.app_config.vector_store.service_name,
      local.app_config.vector_store.endpoint_port
    )
    jaeger_endpoint = format(
      "%s://%s:%d",
      local.app_config.observability.endpoint_protocol,
      local.app_config.observability.service_name,
      local.app_config.observability.jaeger_port
    )
  }
}

# Service discovery configuration
output "k8s_service_endpoints" {
  description = "Kubernetes service endpoints for application configuration"
  value = {
    llm_provider = {
      service_name = local.app_config.llm_provider.service_name
      port         = local.app_config.llm_provider.endpoint_port
      config_path  = "Pipeline:LlmProvider:OllamaEndpoint"
    }
    vector_store = {
      service_name = local.app_config.vector_store.service_name
      port         = local.app_config.vector_store.endpoint_port
      config_path  = "Pipeline:VectorStore:ConnectionString"
    }
    observability = {
      service_name = local.app_config.observability.service_name
      port         = local.app_config.observability.jaeger_port
      config_path  = "Pipeline:Observability:OpenTelemetryEndpoint"
    }
  }
}
