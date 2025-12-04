# Outputs for Ouroboros IONOS Infrastructure

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

output "lan_name" {
  description = "Name of the created LAN"
  value       = module.networking.lan_name
}

output "lan_public" {
  description = "Whether the LAN is public"
  value       = module.networking.lan_public
}

# External Accessibility Outputs
output "k8s_public_ips" {
  description = "Public IPs of Kubernetes nodes for external access"
  value       = module.kubernetes.public_ips
}

output "k8s_api_subnet_allow_list" {
  description = "Allowed subnets for Kubernetes API access"
  value       = module.kubernetes.api_subnet_allow_list
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

# External Access Information
output "external_access_info" {
  description = "Information about external accessibility of the infrastructure"
  value = {
    registry_hostname = module.registry.registry_hostname
    registry_location = module.registry.registry_location
    k8s_public_ips    = module.kubernetes.public_ips
    k8s_api_access    = module.kubernetes.api_subnet_allow_list
    lan_public        = module.networking.lan_public
  }
}
