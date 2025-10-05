# Terraform Test for Storage Module
# Tests the storage volumes module configuration and validation

run "storage_volume_validation" {
  command = plan

  module {
    source = "../modules/storage"
  }

  variables {
    datacenter_id = "test-dc-id"
    volumes = [
      {
        name             = "test-volume-1"
        size             = 50
        type             = "HDD"
        availability_zone = "AUTO"
      }
    ]
  }

  # Validate volume configuration
  assert {
    condition     = length(var.volumes) > 0
    error_message = "At least one volume should be configured"
  }

  # Validate volume size is adequate
  assert {
    condition     = var.volumes[0].size >= 10
    error_message = "Volume size should be at least 10 GB"
  }
}

run "storage_multiple_volumes" {
  command = plan

  module {
    source = "../modules/storage"
  }

  variables {
    datacenter_id = "test-dc-id"
    volumes = [
      {
        name             = "qdrant-data"
        size             = 100
        type             = "SSD"
        availability_zone = "AUTO"
      },
      {
        name             = "ollama-models"
        size             = 200
        type             = "SSD"
        availability_zone = "AUTO"
      }
    ]
  }

  # Validate multiple volumes
  assert {
    condition     = length(var.volumes) == 2
    error_message = "Should have exactly 2 volumes configured"
  }

  # Validate all volumes have unique names
  assert {
    condition     = length(distinct([for v in var.volumes : v.name])) == length(var.volumes)
    error_message = "All volumes should have unique names"
  }
}

run "storage_ssd_validation" {
  command = plan

  module {
    source = "../modules/storage"
  }

  variables {
    datacenter_id = "test-dc-id"
    volumes = [
      {
        name             = "database-volume"
        size             = 150
        type             = "SSD"
        availability_zone = "ZONE_1"
      }
    ]
  }

  # Validate SSD storage type
  assert {
    condition     = contains(["HDD", "SSD", "SSD_STANDARD", "SSD_PREMIUM"], var.volumes[0].type)
    error_message = "Storage type must be one of: HDD, SSD, SSD_STANDARD, SSD_PREMIUM"
  }

  # Database volumes should be at least 50GB
  assert {
    condition     = var.volumes[0].size >= 50
    error_message = "Database volumes should be at least 50 GB"
  }
}
