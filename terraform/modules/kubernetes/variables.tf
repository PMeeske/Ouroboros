variable "cluster_name" {
  description = "Name of the Kubernetes cluster"
  type        = string
}

variable "k8s_version" {
  description = "Kubernetes version (must be 1.30+ for IONOS compatibility)"
  type        = string
  
  validation {
    condition     = can(regex("^1\\.(3[0-9]|[4-9][0-9])(\\.\\d+)?$", var.k8s_version))
    error_message = "Kubernetes version must be 1.30 or higher for IONOS Cloud. Supported versions: 1.30, 1.31, 1.32, 1.33+"
  }
}

variable "maintenance_day" {
  description = "Day of the week for maintenance window"
  type        = string
  default     = "Sunday"
}

variable "maintenance_time" {
  description = "Time for maintenance window"
  type        = string
  default     = "03:00:00"
}

variable "api_subnet_allow_list" {
  description = "List of allowed subnets for Kubernetes API access"
  type        = list(string)
  default     = []
}

variable "s3_buckets" {
  description = "S3 buckets for cluster backups"
  type = list(object({
    name = string
  }))
  default = []
}

variable "datacenter_id" {
  description = "ID of the data center"
  type        = string
}

variable "node_pool_name" {
  description = "Name of the node pool"
  type        = string
}

variable "cpu_family" {
  description = "CPU family for nodes (INTEL_SKYLAKE, INTEL_XEON, AMD_EPYC)"
  type        = string
  default     = "INTEL_SKYLAKE"
}

variable "availability_zone" {
  description = "Availability zone"
  type        = string
  default     = "AUTO"
}

variable "storage_type" {
  description = "Storage type (HDD or SSD)"
  type        = string
  default     = "SSD"
}

variable "node_count" {
  description = "Number of nodes"
  type        = number
  default     = 3
}

variable "cores_count" {
  description = "Number of CPU cores per node"
  type        = number
  default     = 4
}

variable "ram_size" {
  description = "RAM size in MB per node"
  type        = number
  default     = 16384
}

variable "storage_size" {
  description = "Storage size in GB per node"
  type        = number
  default     = 100
}

variable "min_node_count" {
  description = "Minimum number of nodes for autoscaling"
  type        = number
  default     = 2
}

variable "max_node_count" {
  description = "Maximum number of nodes for autoscaling"
  type        = number
  default     = 5
}

variable "labels" {
  description = "Labels for the node pool"
  type        = map(string)
  default     = {}
}

variable "annotations" {
  description = "Annotations for the node pool"
  type        = map(string)
  default     = {}
}

variable "public_ips" {
  description = "Public IPs for node pool"
  type        = list(string)
  default     = []
}
