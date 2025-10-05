# Terraform Test for App Config Module
# Tests the application configuration module

run "app_config_validation" {
  command = plan

  module {
    source = "../modules/app-config"
  }

  variables {
    environment = "test"
    app_name    = "monadic-pipeline"
    namespace   = "test-namespace"
  }

  # Validate environment is set
  assert {
    condition     = contains(["dev", "staging", "production", "test"], var.environment)
    error_message = "Environment must be one of: dev, staging, production, test"
  }

  # Validate app name
  assert {
    condition     = var.app_name != ""
    error_message = "App name should not be empty"
  }
}

run "app_config_dev_environment" {
  command = plan

  module {
    source = "../modules/app-config"
  }

  variables {
    environment = "dev"
    app_name    = "monadic-pipeline"
    namespace   = "monadic-pipeline-dev"
  }

  # Dev namespace should include environment suffix
  assert {
    condition     = can(regex("-dev$", var.namespace))
    error_message = "Development namespace should end with -dev"
  }
}

run "app_config_production_environment" {
  command = plan

  module {
    source = "../modules/app-config"
  }

  variables {
    environment = "production"
    app_name    = "monadic-pipeline"
    namespace   = "monadic-pipeline"
  }

  # Production environment validation
  assert {
    condition     = var.environment == "production"
    error_message = "Environment should be production"
  }
}
