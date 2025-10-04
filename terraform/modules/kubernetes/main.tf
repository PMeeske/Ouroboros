# Kubernetes Cluster Module
# Creates and manages an IONOS Managed Kubernetes cluster with node pool

terraform {
  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }
}

resource "ionoscloud_k8s_cluster" "main" {
  name        = var.cluster_name
  k8s_version = var.k8s_version

  maintenance_window {
    day_of_the_week = var.maintenance_day
    time            = var.maintenance_time
  }

  api_subnet_allow_list = var.api_subnet_allow_list

  dynamic "s3_buckets" {
    for_each = var.s3_buckets
    content {
      name = s3_buckets.value.name
    }
  }
}

resource "ionoscloud_k8s_node_pool" "main" {
  k8s_cluster_id = ionoscloud_k8s_cluster.main.id
  datacenter_id  = var.datacenter_id
  name           = var.node_pool_name
  k8s_version    = var.k8s_version

  cpu_family        = var.cpu_family
  availability_zone = var.availability_zone
  storage_type      = var.storage_type
  node_count        = var.node_count

  cores_count  = var.cores_count
  ram_size     = var.ram_size
  storage_size = var.storage_size

  auto_scaling {
    min_node_count = var.min_node_count
    max_node_count = var.max_node_count
  }

  maintenance_window {
    day_of_the_week = var.maintenance_day
    time            = var.maintenance_time
  }

  labels      = var.labels
  annotations = var.annotations

  public_ips = var.public_ips
}

# Data source to retrieve kubeconfig
data "ionoscloud_k8s_cluster" "main" {
  id = ionoscloud_k8s_cluster.main.id

  depends_on = [ionoscloud_k8s_cluster.main]
}
