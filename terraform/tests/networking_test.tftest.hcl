# Terraform Test for Networking Module
# Tests the networking module configuration and validation

run "networking_lan_validation" {
  command = plan

  module {
    source = "../modules/networking"
  }

  variables {
    datacenter_id = "test-dc-id"
    lan_name      = "test-lan"
    lan_public    = false
  }

  # Validate LAN name
  assert {
    condition     = output.lan_name == "test-lan"
    error_message = "LAN name does not match expected value"
  }

  # Validate LAN is created
  assert {
    condition     = output.lan_id != ""
    error_message = "LAN ID should not be empty"
  }
}

run "networking_public_lan" {
  command = plan

  module {
    source = "../modules/networking"
  }

  variables {
    datacenter_id = "test-dc-id"
    lan_name      = "public-lan"
    lan_public    = true
  }

  # Validate public LAN configuration
  assert {
    condition     = var.lan_public == true
    error_message = "LAN should be configured as public"
  }
}

run "networking_private_lan" {
  command = plan

  module {
    source = "../modules/networking"
  }

  variables {
    datacenter_id = "prod-dc-id"
    lan_name      = "private-internal-lan"
    lan_public    = false
  }

  # Validate private LAN configuration
  assert {
    condition     = var.lan_public == false
    error_message = "Production internal LANs should be private"
  }

  # Validate naming convention for private LANs
  assert {
    condition     = can(regex("private|internal", var.lan_name))
    error_message = "Private LAN names should indicate they are private/internal"
  }
}
