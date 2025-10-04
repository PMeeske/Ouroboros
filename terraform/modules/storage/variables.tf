variable "datacenter_id" {
  description = "ID of the data center"
  type        = string
}

variable "volumes" {
  description = "List of volumes to create"
  type = list(object({
    name         = string
    size         = number
    type         = string
    licence_type = string
    image_alias  = optional(string)
  }))
  default = []
}
