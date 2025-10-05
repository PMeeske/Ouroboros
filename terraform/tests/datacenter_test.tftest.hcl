# Terraform Test for Datacenter Module
# Tests the datacenter module configuration and validation

run "datacenter_validation" {
  command = plan

  module {
    source = "../modules/datacenter"
  }

  variables {
    datacenter_name = "test-datacenter"
    location        = "de/fra"
    description     = "Test datacenter for validation"
  }

  # Validate that datacenter name is set correctly
  assert {
    condition     = output.datacenter_name == "test-datacenter"
    error_message = "Datacenter name does not match expected value"
  }

  # Validate that location is set correctly
  assert {
    condition     = output.datacenter_location == "de/fra"
    error_message = "Datacenter location does not match expected value"
  }
}

run "datacenter_with_minimal_config" {
  command = plan

  module {
    source = "../modules/datacenter"
  }

  variables {
    datacenter_name = "minimal-dc"
    location        = "us/las"
    description     = "Minimal datacenter configuration"
  }

  # Validate basic resource creation
  assert {
    condition     = output.datacenter_id != ""
    error_message = "Datacenter ID should not be empty"
  }
}

run "datacenter_naming_validation" {
  command = plan

  module {
    source = "../modules/datacenter"
  }

  variables {
    datacenter_name = "valid-name-123"
    location        = "de/txl"
    description     = "Testing valid naming"
  }

  # Validate name format
  assert {
    condition     = can(regex("^[a-z0-9-]+$", var.datacenter_name))
    error_message = "Datacenter name should only contain lowercase letters, numbers, and hyphens"
  }
}
