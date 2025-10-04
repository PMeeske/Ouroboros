# Data Center Module
# Creates and manages an IONOS Cloud data center

terraform {
  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }
}

resource "ionoscloud_datacenter" "main" {
  name        = var.datacenter_name
  location    = var.location
  description = var.description
}
