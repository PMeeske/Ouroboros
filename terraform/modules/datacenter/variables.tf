variable "datacenter_name" {
  description = "Name of the data center"
  type        = string
}

variable "location" {
  description = "Location of the data center"
  type        = string
}

variable "description" {
  description = "Description of the data center"
  type        = string
  default     = "Managed by Terraform"
}
