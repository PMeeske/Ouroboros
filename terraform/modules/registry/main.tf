# Container Registry Module
# Creates and manages an IONOS Container Registry

terraform {
  required_providers {
    ionoscloud = {
      source  = "ionos-cloud/ionoscloud"
      version = "~> 6.7.0"
    }
  }
}

resource "ionoscloud_container_registry" "main" {
  name     = var.registry_name
  location = var.location

  garbage_collection_schedule {
    days = var.garbage_collection_schedule.days
    time = var.garbage_collection_schedule.time
  }

  features {
    vulnerability_scanning = var.features.vulnerability_scanning
  }
}

# Create a registry token for authentication
resource "ionoscloud_container_registry_token" "main" {
  registry_id = ionoscloud_container_registry.main.id
  name        = "${var.registry_name}-token"

  scopes {
    actions = ["pull", "push", "delete"]
    name    = "*"
    type    = "repository"
  }

  expiry_date = var.token_expiry_date
  status      = "enabled"
}
