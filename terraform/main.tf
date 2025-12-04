# Ouroboros Infrastructure as Code for IONOS Cloud
# This file orchestrates all infrastructure resources for Ouroboros deployment

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }

  # Backend configuration for state management
  # Uncomment and configure for production use
  # backend "s3" {
  #   bucket = "monadic-pipeline-terraform-state"
  #   key    = "ionos/terraform.tfstate"
  #   region = "de"
  #   endpoint = "https://s3-eu-central-1.ionoscloud.com"
  # }
}

# IONOS Cloud Provider Configuration
provider "ionoscloud" {
  # Authentication via environment variables:
  # - IONOS_USERNAME and IONOS_PASSWORD
  # - OR IONOS_TOKEN
  # Set via GitHub Secrets or local environment
}

# Data Center Module
module "datacenter" {
  source = "./modules/datacenter"

  datacenter_name = var.datacenter_name
  location        = var.location
  description     = var.datacenter_description
}

# Kubernetes Cluster Module
module "kubernetes" {
  source = "./modules/kubernetes"

  cluster_name     = var.cluster_name
  k8s_version      = var.k8s_version
  maintenance_day  = var.maintenance_day
  maintenance_time = var.maintenance_time

  node_pool_name    = var.node_pool_name
  node_count        = var.node_count
  cpu_family        = var.cpu_family
  cores_count       = var.cores_count
  ram_size          = var.ram_size
  storage_size      = var.storage_size
  storage_type      = var.storage_type
  availability_zone = var.availability_zone

  datacenter_id = module.datacenter.datacenter_id

  depends_on = [module.datacenter]
}

# Container Registry Module
module "registry" {
  source = "./modules/registry"

  registry_name               = var.registry_name
  location                    = var.registry_location
  garbage_collection_schedule = var.garbage_collection_schedule
  features                    = var.registry_features
}

# Storage Module (for persistent volumes)
# Note: For Kubernetes workloads, use Kubernetes PersistentVolumeClaims instead
# This module is for creating standalone volumes attached to servers
# Commented out as it's not needed for MKS (Managed Kubernetes Service)
# module "storage" {
#   source = "./modules/storage"
#
#   datacenter_id = module.datacenter.datacenter_id
#   volumes       = var.volumes
#
#   depends_on = [module.datacenter]
# }

# Networking Module
module "networking" {
  source = "./modules/networking"

  datacenter_id = module.datacenter.datacenter_id
  lan_name      = var.lan_name
  lan_public    = var.lan_public

  depends_on = [module.datacenter]
}
