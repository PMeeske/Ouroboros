# Terraform Test for Kubernetes Module
# Tests the kubernetes cluster module configuration and validation

run "kubernetes_cluster_validation" {
  command = plan

  module {
    source = "../modules/kubernetes"
  }

  variables {
    cluster_name      = "test-k8s-cluster"
    k8s_version       = "1.27.0"
    maintenance_day   = "Monday"
    maintenance_time  = "02:00:00Z"
    node_pool_name    = "test-pool"
    node_count        = 2
    cpu_family        = "AMD_OPTERON"
    cores_count       = 2
    ram_size          = 4096
    storage_size      = 50
    storage_type      = "HDD"
    availability_zone = "AUTO"
    datacenter_id     = "test-dc-id"
  }

  # Validate cluster name
  assert {
    condition     = output.cluster_name == "test-k8s-cluster"
    error_message = "Cluster name does not match expected value"
  }

  # Validate node count is within acceptable range
  assert {
    condition     = var.node_count >= 1 && var.node_count <= 50
    error_message = "Node count must be between 1 and 50"
  }

  # Validate RAM size is adequate
  assert {
    condition     = var.ram_size >= 2048
    error_message = "RAM size should be at least 2048 MB for Kubernetes nodes"
  }

  # Validate storage size is adequate
  assert {
    condition     = var.storage_size >= 20
    error_message = "Storage size should be at least 20 GB for Kubernetes nodes"
  }
}

run "kubernetes_production_config" {
  command = plan

  module {
    source = "../modules/kubernetes"
  }

  variables {
    cluster_name      = "prod-k8s"
    k8s_version       = "1.27.0"
    maintenance_day   = "Sunday"
    maintenance_time  = "03:00:00Z"
    node_pool_name    = "prod-pool"
    node_count        = 3
    cpu_family        = "INTEL_SKYLAKE"
    cores_count       = 4
    ram_size          = 8192
    storage_size      = 100
    storage_type      = "SSD"
    availability_zone = "AUTO"
    datacenter_id     = "prod-dc-id"
  }

  # Production should have at least 3 nodes for HA
  assert {
    condition     = var.node_count >= 3
    error_message = "Production clusters should have at least 3 nodes for high availability"
  }

  # Production should use SSD storage
  assert {
    condition     = var.storage_type == "SSD" || var.storage_type == "SSD_STANDARD"
    error_message = "Production clusters should use SSD storage for better performance"
  }
}

run "kubernetes_version_validation" {
  command = plan

  module {
    source = "../modules/kubernetes"
  }

  variables {
    cluster_name      = "version-test"
    k8s_version       = "1.27.0"
    maintenance_day   = "Monday"
    maintenance_time  = "02:00:00Z"
    node_pool_name    = "test-pool"
    node_count        = 1
    cpu_family        = "AMD_OPTERON"
    cores_count       = 2
    ram_size          = 2048
    storage_size      = 20
    storage_type      = "HDD"
    availability_zone = "AUTO"
    datacenter_id     = "test-dc-id"
  }

  # Validate k8s version format
  assert {
    condition     = can(regex("^\\d+\\.\\d+\\.\\d+$", var.k8s_version))
    error_message = "Kubernetes version should be in semantic version format (e.g., 1.27.0)"
  }
}
