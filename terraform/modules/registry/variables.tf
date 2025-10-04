variable "registry_name" {
  description = "Name of the container registry"
  type        = string
}

variable "location" {
  description = "Location of the registry"
  type        = string
}

variable "garbage_collection_schedule" {
  description = "Schedule for garbage collection"
  type = object({
    days = list(string)
    time = string
  })
}

variable "features" {
  description = "Registry features"
  type = object({
    vulnerability_scanning = bool
  })
  default = {
    vulnerability_scanning = true
  }
}

variable "token_expiry_date" {
  description = "Expiry date for registry token (YYYY-MM-DDTHH:MM:SSZ)"
  type        = string
  default     = null
}
