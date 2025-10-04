output "cluster_id" {
  description = "ID of the Kubernetes cluster"
  value       = ionoscloud_k8s_cluster.main.id
}

output "cluster_name" {
  description = "Name of the Kubernetes cluster"
  value       = ionoscloud_k8s_cluster.main.name
}

output "k8s_version" {
  description = "Kubernetes version"
  value       = ionoscloud_k8s_cluster.main.k8s_version
}

output "node_pool_id" {
  description = "ID of the node pool"
  value       = ionoscloud_k8s_node_pool.main.id
}

output "node_pool_name" {
  description = "Name of the node pool"
  value       = ionoscloud_k8s_node_pool.main.name
}

output "kubeconfig" {
  description = "Kubeconfig for the cluster"
  value       = data.ionoscloud_k8s_cluster.main.kube_config
  sensitive   = true
}

output "api_server" {
  description = "API server endpoint"
  value       = data.ionoscloud_k8s_cluster.main.config
}

output "public_ips" {
  description = "Public IPs assigned to node pool"
  value       = ionoscloud_k8s_node_pool.main.public_ips
}

output "api_subnet_allow_list" {
  description = "Allowed subnets for API access"
  value       = ionoscloud_k8s_cluster.main.api_subnet_allow_list
}
