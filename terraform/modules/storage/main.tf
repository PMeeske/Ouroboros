# Storage Module
# Creates and manages persistent storage volumes

terraform {
  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }
}

resource "ionoscloud_volume" "volumes" {
  for_each = { for idx, vol in var.volumes : vol.name => vol }

  datacenter_id = var.datacenter_id
  name          = each.value.name
  size          = each.value.size
  type          = each.value.type
  licence_type  = each.value.licence_type

  image_alias = try(each.value.image_alias, null)
}
