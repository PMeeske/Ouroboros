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
  value       = ionoscloud_k8s_cluster.main.kube_config
  sensitive   = true
}

output "api_server" {
  description = "API server endpoint"
  value       = ionoscloud_k8s_cluster.main.viable_node_pool_versions
}
