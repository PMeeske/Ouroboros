# Application Configuration Module
# Validates and provides application-specific infrastructure configuration

This module bridges the gap between C# application requirements and Terraform infrastructure provisioning.

## Purpose

- Validates that Terraform-provisioned resources meet C# application requirements
- Provides helper outputs for Kubernetes configuration
- Calculates optimal node sizing based on application needs
- Maps C# configuration paths to infrastructure endpoints

## Usage

```hcl
module "app_config" {
  source = "./modules/app-config"
  
  # From K8s deployment specs
  app_replica_count    = 2
  app_memory_limit_mb  = 2048
  app_cpu_limit_cores  = 1
  
  # From main Terraform variables
  node_count  = var.node_count
  cores_count = var.cores_count
  ram_size    = var.ram_size
  volumes     = var.volumes
}
```

## Outputs

### `app_config`
Application configuration requirements extracted from C# appsettings.

### `recommended_node_config`
Recommended node configuration based on application resource needs.

### `resource_validation`
Validation flags indicating if current infrastructure meets requirements:
- `sufficient_nodes`: Are there enough nodes?
- `sufficient_cores`: Do nodes have enough CPU cores?
- `ollama_storage_sufficient`: Is Ollama storage adequate?
- `qdrant_storage_sufficient`: Is Qdrant storage adequate?

### `k8s_configmap_data`
Service endpoint URLs for Kubernetes ConfigMap:
- `ollama_endpoint`: http://ollama-service:11434
- `qdrant_endpoint`: http://qdrant-service:6333
- `jaeger_endpoint`: http://jaeger-collector:4317

### `k8s_service_endpoints`
Kubernetes service configuration with C# config paths:
- Maps service names to C# configuration paths
- Provides port numbers for each service

## Integration with C# Application

This module maps infrastructure to C# `appsettings.Production.json`:

```json
{
  "Pipeline": {
    "LlmProvider": {
      "OllamaEndpoint": "http://ollama-service:11434"  // From k8s_configmap_data.ollama_endpoint
    },
    "VectorStore": {
      "ConnectionString": "http://qdrant-service:6333"  // From k8s_configmap_data.qdrant_endpoint
    },
    "Observability": {
      "OpenTelemetryEndpoint": "http://jaeger-collector:4317"  // From k8s_configmap_data.jaeger_endpoint
    }
  }
}
```

## Resource Calculation

The module calculates total resource requirements:

```
Total Memory = App Pods + Services + System Overhead
Total CPU = App Pods + Services + System Overhead

Minimum Nodes = ceil(Total Memory / RAM per Node)
Minimum Cores = ceil(Total CPU)
```

## Validation

After applying Terraform, check validation output:

```bash
terraform output app_config.resource_validation

# Expected output:
# {
#   "sufficient_nodes" = true
#   "sufficient_cores" = true
#   "ollama_storage_sufficient" = true
#   "qdrant_storage_sufficient" = true
# }
```

If any validation fails, increase node resources in your environment tfvars file.

## Example Integration

In your main.tf:

```hcl
module "app_config" {
  source = "./modules/app-config"
  
  app_replica_count    = 2
  app_memory_limit_mb  = 2048
  app_cpu_limit_cores  = 1
  
  node_count  = var.node_count
  cores_count = var.cores_count
  ram_size    = var.ram_size
  volumes     = var.volumes
}

# Use validation to ensure infrastructure is adequate
output "infrastructure_ready" {
  value = alltrue([
    module.app_config.resource_validation.sufficient_nodes,
    module.app_config.resource_validation.sufficient_cores,
    module.app_config.resource_validation.ollama_storage_sufficient,
    module.app_config.resource_validation.qdrant_storage_sufficient
  ])
}
```

## Dependencies

- C# Application: Ouroboros (appsettings.Production.json)
- Kubernetes: Deployment manifests (deployment.cloud.yaml)
- Terraform: Main infrastructure variables

## Maintenance

When updating C# application requirements:
1. Update `app_config` locals in this module
2. Run `terraform plan` to see impact
3. Adjust environment tfvars if validation fails
4. Apply changes to infrastructure
