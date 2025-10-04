variable "datacenter_id" {
  description = "ID of the data center"
  type        = string
}

variable "lan_name" {
  description = "Name of the LAN"
  type        = string
}

variable "lan_public" {
  description = "Whether the LAN is public"
  type        = bool
  default     = true
}
