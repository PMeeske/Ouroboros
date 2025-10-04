# Outputs for MonadicPipeline IONOS Infrastructure

# Data Center Outputs
output "datacenter_id" {
  description = "ID of the created data center"
  value       = module.datacenter.datacenter_id
}

output "datacenter_name" {
  description = "Name of the created data center"
  value       = module.datacenter.datacenter_name
}

# Kubernetes Outputs
output "k8s_cluster_id" {
  description = "ID of the Kubernetes cluster"
  value       = module.kubernetes.cluster_id
}

output "k8s_cluster_name" {
  description = "Name of the Kubernetes cluster"
  value       = module.kubernetes.cluster_name
}

output "k8s_kubeconfig" {
  description = "Kubeconfig for accessing the Kubernetes cluster"
  value       = module.kubernetes.kubeconfig
  sensitive   = true
}

output "k8s_node_pool_id" {
  description = "ID of the default node pool"
  value       = module.kubernetes.node_pool_id
}

# Container Registry Outputs
output "registry_id" {
  description = "ID of the container registry"
  value       = module.registry.registry_id
}

output "registry_hostname" {
  description = "Hostname of the container registry"
  value       = module.registry.registry_hostname
}

output "registry_location" {
  description = "Location of the container registry"
  value       = module.registry.registry_location
}

# Storage Outputs
# Commented out as storage module is disabled
# For Kubernetes workloads, use PersistentVolumeClaims instead
# output "volume_ids" {
#   description = "IDs of created volumes"
#   value       = module.storage.volume_ids
# }

# Networking Outputs
output "lan_id" {
  description = "ID of the created LAN"
  value       = module.networking.lan_id
}

# Summary Output
output "deployment_summary" {
  description = "Summary of deployed infrastructure"
  value = {
    datacenter  = module.datacenter.datacenter_name
    location    = var.location
    k8s_cluster = module.kubernetes.cluster_name
    k8s_version = var.k8s_version
    node_count  = var.node_count
    registry    = module.registry.registry_hostname
    environment = var.environment
  }
}
