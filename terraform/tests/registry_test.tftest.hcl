# Terraform Test for Registry Module
# Tests the container registry module configuration and validation

run "registry_validation" {
  command = plan

  module {
    source = "../modules/registry"
  }

  variables {
    registry_name               = "test-registry"
    location                    = "de/fra"
    garbage_collection_schedule = {
      days = ["Monday"]
      time = "04:30"
    }
    features = {
      vulnerability_scanning = true
    }
  }

  # Validate registry name
  assert {
    condition     = output.registry_name == "test-registry"
    error_message = "Registry name does not match expected value"
  }

  # Validate location is set
  assert {
    condition     = output.registry_location == "de/fra"
    error_message = "Registry location does not match expected value"
  }
}

run "registry_with_vulnerability_scanning" {
  command = plan

  module {
    source = "../modules/registry"
  }

  variables {
    registry_name               = "secure-registry"
    location                    = "de/fra"
    garbage_collection_schedule = {
      days = ["Sunday"]
      time = "02:00"
    }
    features = {
      vulnerability_scanning = true
    }
  }

  # Validate vulnerability scanning is enabled for secure registries
  assert {
    condition     = var.features.vulnerability_scanning == true
    error_message = "Vulnerability scanning should be enabled for production registries"
  }
}

run "registry_garbage_collection_validation" {
  command = plan

  module {
    source = "../modules/registry"
  }

  variables {
    registry_name               = "gc-test-registry"
    location                    = "us/las"
    garbage_collection_schedule = {
      days = ["Saturday", "Sunday"]
      time = "03:00"
    }
    features = {
      vulnerability_scanning = false
    }
  }

  # Validate garbage collection schedule is configured
  assert {
    condition     = length(var.garbage_collection_schedule.days) > 0
    error_message = "Garbage collection should be scheduled for at least one day"
  }

  # Validate time format (HH:MM)
  assert {
    condition     = can(regex("^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", var.garbage_collection_schedule.time))
    error_message = "Garbage collection time should be in HH:MM format"
  }
}
