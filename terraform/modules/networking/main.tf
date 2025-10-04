# Networking Module
# Creates and manages network resources (LANs)

terraform {
  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }
}

resource "ionoscloud_lan" "main" {
  datacenter_id = var.datacenter_id
  name          = var.lan_name
  public        = var.lan_public
}
