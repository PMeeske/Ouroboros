# Variable definitions for MonadicPipeline IONOS Infrastructure

# Data Center Configuration
variable "datacenter_name" {
  description = "Name of the IONOS data center"
  type        = string
  default     = "monadic-pipeline-dc"
}

variable "location" {
  description = "IONOS data center location (e.g., de/fra, de/txl, us/las)"
  type        = string
  default     = "de/fra"
}

variable "datacenter_description" {
  description = "Description of the data center"
  type        = string
  default     = "MonadicPipeline infrastructure data center"
}

# Kubernetes Cluster Configuration
variable "cluster_name" {
  description = "Name of the Kubernetes cluster"
  type        = string
  default     = "monadic-pipeline-cluster"
}

variable "k8s_version" {
  description = "Kubernetes version"
  type        = string
  default     = "1.33.3"  # Updated from 1.28 - See docs/K8S_VERSION_COMPATIBILITY.md
  
  # Version Selection Guidelines:
  # - Use 1.29+ for best IONOS support and security updates
  # - Kubernetes 1.28 is approaching EOL (October 2024)
  # - All manifests compatible with 1.28+ (tested with 1.29, 1.30, 1.31)
  # - Supported versions: 1.29, 1.30, 1.31 (as of January 2025)
  # - Update this version at least once per quarter
  
  validation {
    condition     = can(regex("^1\\.(2[9-9]|3[0-9])(\\.\\d+)?$", var.k8s_version))
    error_message = "Kubernetes version must be 1.29 or higher. Version 1.28 is deprecated. Use 1.29, 1.30, or 1.31 for IONOS compatibility."
  }
}

variable "maintenance_day" {
  description = "Day of the week for maintenance window"
  type        = string
  default     = "Sunday"
}

variable "maintenance_time" {
  description = "Time of day for maintenance window (HH:MM:SS format)"
  type        = string
  default     = "03:00:00"
}

# Node Pool Configuration
variable "node_pool_name" {
  description = "Name of the default node pool"
  type        = string
  default     = "default-pool"
}

variable "node_count" {
  description = "Number of nodes in the pool"
  type        = number
  default     = 3
}

variable "cpu_family" {
  description = "CPU family for nodes"
  type        = string
  default     = "AMD_OPTERON"
}

variable "cores_count" {
  description = "Number of CPU cores per node"
  type        = number
  default     = 4
}

variable "ram_size" {
  description = "RAM size in MB per node"
  type        = number
  default     = 16384 # 16 GB
}

variable "storage_size" {
  description = "Storage size in GB per node"
  type        = number
  default     = 100
}

variable "storage_type" {
  description = "Storage type (HDD or SSD)"
  type        = string
  default     = "SSD"
}

variable "availability_zone" {
  description = "Availability zone for the node pool"
  type        = string
  default     = "AUTO"
}

# Container Registry Configuration
variable "registry_name" {
  description = "Name of the container registry"
  type        = string
  default     = "adaptive-systems"
}

variable "registry_location" {
  description = "Location of the container registry"
  type        = string
  default     = "de/fra"
}

variable "garbage_collection_schedule" {
  description = "Cron schedule for garbage collection (time format: HH:MM:SS+TZ, e.g., 02:00:00+00:00)"
  type = object({
    days = list(string)
    time = string
  })
  default = {
    days = ["Sunday"]
    time = "02:00:00+00:00"
  }
}

variable "registry_features" {
  description = "Registry features configuration"
  type = object({
    vulnerability_scanning = bool
  })
  default = {
    vulnerability_scanning = true
  }
}

# Storage Configuration
variable "volumes" {
  description = "List of volumes to create"
  type = list(object({
    name         = string
    size         = number
    type         = string
    licence_type = string
    image_alias  = optional(string)
  }))
  default = [
    {
      name         = "qdrant-data"
      size         = 50
      type         = "SSD"
      licence_type = "OTHER"
    },
    {
      name         = "ollama-models"
      size         = 100
      type         = "SSD"
      licence_type = "OTHER"
    }
  ]
}

# Networking Configuration
variable "lan_name" {
  description = "Name of the LAN"
  type        = string
  default     = "monadic-pipeline-lan"
}

variable "lan_public" {
  description = "Whether the LAN is public"
  type        = bool
  default     = true
}

# Environment
variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  default     = "production"
}

# Tags
variable "tags" {
  description = "Tags to apply to resources"
  type        = map(string)
  default = {
    Project    = "MonadicPipeline"
    ManagedBy  = "Terraform"
    Repository = "PMeeske/MonadicPipeline"
  }
}
