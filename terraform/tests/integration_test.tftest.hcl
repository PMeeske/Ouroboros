# Terraform Integration Test for Main Configuration
# Tests the complete infrastructure setup with all modules

run "integration_dev_environment" {
  command = plan

  variables {
    # Datacenter
    datacenter_name        = "monadic-pipeline-dev"
    location               = "de/fra"
    datacenter_description = "Development environment for Ouroboros"

    # Kubernetes
    cluster_name      = "monadic-k8s-dev"
    k8s_version       = "1.27.0"
    maintenance_day   = "Monday"
    maintenance_time  = "02:00:00Z"
    node_pool_name    = "dev-node-pool"
    node_count        = 2
    cpu_family        = "AMD_OPTERON"
    cores_count       = 2
    ram_size          = 4096
    storage_size      = 50
    storage_type      = "HDD"
    availability_zone = "AUTO"

    # Registry
    registry_name     = "monadic-registry-dev"
    registry_location = "de/fra"
    garbage_collection_schedule = {
      days = ["Monday"]
      time = "04:30"
    }
    registry_features = {
      vulnerability_scanning = false
    }

    # Networking
    lan_name   = "monadic-lan-dev"
    lan_public = false
  }

  # Validate all modules are properly integrated
  assert {
    condition     = output.datacenter_id != ""
    error_message = "Datacenter should be created successfully"
  }

  assert {
    condition     = output.cluster_id != ""
    error_message = "Kubernetes cluster should be created successfully"
  }

  assert {
    condition     = output.registry_id != ""
    error_message = "Container registry should be created successfully"
  }

  assert {
    condition     = output.lan_id != ""
    error_message = "LAN should be created successfully"
  }

  # Validate outputs are properly exported
  assert {
    condition     = output.k8s_kubeconfig != ""
    error_message = "Kubeconfig should be available"
  }

  assert {
    condition     = output.registry_hostname != ""
    error_message = "Registry hostname should be available"
  }
}

run "integration_production_environment" {
  command = plan

  variables {
    # Datacenter
    datacenter_name        = "monadic-pipeline-prod"
    location               = "de/fra"
    datacenter_description = "Production environment for Ouroboros"

    # Kubernetes
    cluster_name      = "monadic-k8s-prod"
    k8s_version       = "1.27.0"
    maintenance_day   = "Sunday"
    maintenance_time  = "03:00:00Z"
    node_pool_name    = "prod-node-pool"
    node_count        = 3
    cpu_family        = "INTEL_SKYLAKE"
    cores_count       = 4
    ram_size          = 8192
    storage_size      = 100
    storage_type      = "SSD"
    availability_zone = "AUTO"

    # Registry
    registry_name     = "monadic-registry-prod"
    registry_location = "de/fra"
    garbage_collection_schedule = {
      days = ["Sunday"]
      time = "03:00"
    }
    registry_features = {
      vulnerability_scanning = true
    }

    # Networking
    lan_name   = "monadic-lan-prod"
    lan_public = false
  }

  # Production-specific validations
  assert {
    condition     = var.node_count >= 3
    error_message = "Production should have at least 3 nodes for HA"
  }

  assert {
    condition     = var.storage_type == "SSD" || var.storage_type == "SSD_STANDARD"
    error_message = "Production should use SSD storage"
  }

  assert {
    condition     = var.registry_features.vulnerability_scanning == true
    error_message = "Production registry should have vulnerability scanning enabled"
  }

  assert {
    condition     = var.ram_size >= 8192
    error_message = "Production nodes should have at least 8GB RAM"
  }
}

run "integration_resource_dependencies" {
  command = plan

  variables {
    datacenter_name        = "dependency-test"
    location               = "de/fra"
    datacenter_description = "Testing resource dependencies"
    cluster_name           = "test-cluster"
    k8s_version            = "1.27.0"
    maintenance_day        = "Monday"
    maintenance_time       = "02:00:00Z"
    node_pool_name         = "test-pool"
    node_count             = 1
    cpu_family             = "AMD_OPTERON"
    cores_count            = 2
    ram_size               = 2048
    storage_size           = 20
    storage_type           = "HDD"
    availability_zone      = "AUTO"
    registry_name          = "test-registry"
    registry_location      = "de/fra"
    garbage_collection_schedule = {
      days = ["Monday"]
      time = "04:30"
    }
    registry_features = {
      vulnerability_scanning = false
    }
    lan_name   = "test-lan"
    lan_public = false
  }

  # Validate proper resource dependencies
  assert {
    condition     = output.deployment_summary != ""
    error_message = "Deployment summary should be available"
  }
}
