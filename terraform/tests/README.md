# Terraform Tests

This directory contains automated tests for the Ouroboros Terraform infrastructure.

## Overview

The test suite includes:

- **Unit Tests**: Individual module validation tests
  - `datacenter_test.tftest.hcl` - Data center module tests
  - `kubernetes_test.tftest.hcl` - Kubernetes cluster module tests
  - `registry_test.tftest.hcl` - Container registry module tests
  - `storage_test.tftest.hcl` - Storage volumes module tests
  - `networking_test.tftest.hcl` - Networking module tests
  - `app_config_test.tftest.hcl` - Application configuration module tests

- **Integration Tests**: Full infrastructure validation
  - `integration_test.tftest.hcl` - End-to-end infrastructure tests

## Requirements

- Terraform >= 1.5.0
- For native test execution: Terraform >= 1.6.0
- IONOS Cloud credentials (for integration tests that require API access)

## Running Tests

### Run All Tests

```bash
cd terraform/tests
./run-tests.sh
```

### Run Specific Test

```bash
cd terraform/tests
./run-tests.sh datacenter_test
```

### Run with Terraform 1.6+ Native Test Command

If you have Terraform 1.6 or later:

```bash
cd terraform
terraform test
```

## Test Structure

Each test file follows the Terraform test framework format:

```hcl
run "test_name" {
  command = plan

  module {
    source = "../modules/module_name"
  }

  variables {
    # Test variables
  }

  assert {
    condition     = # validation condition
    error_message = "Error message if condition fails"
  }
}
```

## Test Coverage

### Module Tests

- **Datacenter Module**
  - Basic configuration validation
  - Naming convention validation
  - Location validation
  - Minimal configuration test

- **Kubernetes Module**
  - Cluster configuration validation
  - Node pool configuration
  - Production-grade configuration (HA, resource sizing)
  - Version format validation
  - Resource constraints validation

- **Registry Module**
  - Basic registry configuration
  - Vulnerability scanning feature validation
  - Garbage collection schedule validation
  - Location validation

- **Storage Module**
  - Volume configuration validation
  - Multiple volumes support
  - Volume naming uniqueness
  - Storage type validation
  - Size constraints validation

- **Networking Module**
  - LAN configuration validation
  - Public/private LAN validation
  - Naming conventions for security

- **App Config Module**
  - Environment validation
  - Namespace validation
  - Environment-specific configurations

### Integration Tests

- **Development Environment**
  - Full infrastructure setup with dev configurations
  - Module integration validation
  - Output validation

- **Production Environment**
  - Production-grade configurations
  - High availability requirements
  - Security best practices
  - Performance requirements

- **Resource Dependencies**
  - Cross-module dependencies
  - Deployment summary validation

## Continuous Integration

Tests are automatically run in GitHub Actions on:

- Pull requests that modify Terraform files
- Push to main branch with Terraform changes
- Manual workflow dispatch

See `.github/workflows/terraform-tests.yml` for the CI configuration.

## Test Development Guidelines

### Writing New Tests

1. Create a new `.tftest.hcl` file in this directory
2. Follow the naming convention: `<module>_test.tftest.hcl`
3. Include multiple `run` blocks for different scenarios
4. Add assertions to validate expected behavior
5. Test both success and failure cases

### Test Naming Conventions

- Use descriptive names: `module_feature_validation`
- Include the scenario being tested
- Use lowercase with underscores

### Best Practices

1. **Test Independence**: Each test should be independent
2. **Clear Assertions**: Assertions should have clear error messages
3. **Comprehensive Coverage**: Test normal cases, edge cases, and error conditions
4. **Documentation**: Document complex test scenarios
5. **Validation First**: Use `command = plan` for most tests to avoid creating resources

## Troubleshooting

### Tests Fail on Older Terraform Versions

If you're using Terraform < 1.6.0, the native test framework is not available. The test runner will:
- Validate test file syntax
- Skip execution of test assertions
- Still validate module configurations

Consider upgrading to Terraform 1.6+ for full test execution.

### Module Validation Errors

If module validation fails:
1. Ensure all required variables are defined
2. Check module source paths are correct
3. Verify Terraform is initialized: `terraform init`
4. Run `terraform validate` in the module directory

### Test Execution Issues

If specific tests fail:
1. Review the test output for assertion failures
2. Check variable values match expected formats
3. Verify module outputs are correctly defined
4. Run the test in isolation: `./run-tests.sh <test_name>`

## Adding Tests to CI/CD

Tests are integrated into the GitHub Actions workflow. To modify test execution:

1. Edit `.github/workflows/terraform-tests.yml`
2. Adjust test triggers, environments, or parameters
3. Tests run automatically on PR creation and updates

## Local Development

For rapid test development:

```bash
# Watch mode (requires entr or similar)
find . -name "*.tftest.hcl" | entr -c ./run-tests.sh

# Quick validation
terraform validate

# Format test files
terraform fmt -recursive
```

## Resources

- [Terraform Test Framework Documentation](https://developer.hashicorp.com/terraform/language/tests)
- [Terraform Testing Best Practices](https://developer.hashicorp.com/terraform/tutorials/configuration-language/test)
- [IONOS Cloud Provider Documentation](https://registry.terraform.io/providers/ionos-cloud/ionoscloud/latest/docs)
