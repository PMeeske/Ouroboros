# Terraform Tests Quick Reference

## Quick Start

```bash
# Run all tests
cd terraform/tests && ./run-tests.sh

# Run specific test
cd terraform/tests && ./run-tests.sh datacenter_test

# View test documentation
cat terraform/tests/README.md
```

## Test Files

| File | Purpose | Test Count |
|------|---------|------------|
| `datacenter_test.tftest.hcl` | Datacenter module validation | 3 |
| `kubernetes_test.tftest.hcl` | Kubernetes cluster validation | 3 |
| `registry_test.tftest.hcl` | Container registry validation | 3 |
| `storage_test.tftest.hcl` | Storage volumes validation | 3 |
| `networking_test.tftest.hcl` | Network configuration validation | 3 |
| `app_config_test.tftest.hcl` | App configuration validation | 3 |
| `integration_test.tftest.hcl` | Full stack integration tests | 3 |

## Common Commands

```bash
# Initialize and run tests
cd terraform
terraform init
cd tests
./run-tests.sh

# Check test syntax
cd terraform/tests
grep -l "run " *.tftest.hcl

# Run tests in CI/CD
# Triggered automatically on PR or push to main
# Manual trigger: GitHub Actions → Terraform Tests → Run workflow

# With Terraform 1.6+ (native test support)
cd terraform
terraform test

# View test results
cd terraform/tests
./run-tests.sh | tee test-results.log
```

## Test Patterns

### Basic Test Structure
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
    condition     = # validation
    error_message = "Error if condition fails"
  }
}
```

### Validation Tests
- Configuration validation
- Naming conventions
- Resource constraints
- Security requirements

### Integration Tests
- Multi-module orchestration
- Environment-specific configs
- Output validation
- Dependency verification

## CI/CD Workflow

**Workflow**: `.github/workflows/terraform-tests.yml`

**Triggers**:
- Pull requests (terraform/** changes)
- Push to main (terraform/** changes)
- Manual dispatch

**Jobs**:
1. Validate configuration
2. Test modules (parallel)
3. Test environments (parallel)
4. Run test suite
5. Security scan
6. Generate summary

## Expected Test Results

| Environment | Expected Result |
|-------------|-----------------|
| Without credentials | 14 passed, 11 skipped |
| With credentials | 17 passed, 7 skipped |
| Terraform 1.6+ | All tests executable |
| Terraform 1.5.0 | Validation mode only |

## Troubleshooting

### "Missing credentials" error
```bash
export IONOS_TOKEN="your-token"
# or
export IONOS_USERNAME="user"
export IONOS_PASSWORD="pass"
```

### "Module not found" error
```bash
cd terraform
terraform init
```

### "Terraform not installed"
```bash
# See: https://www.terraform.io/downloads
brew install terraform  # macOS
# or
wget https://releases.hashicorp.com/terraform/1.5.0/terraform_1.5.0_linux_amd64.zip
```

## Documentation

- **Test Documentation**: `terraform/tests/README.md`
- **Terraform README**: `terraform/README.md`
- **Implementation Summary**: `TERRAFORM_TESTS_SUMMARY.md`

## Test Coverage

- ✅ All 6 modules have unit tests
- ✅ Integration tests for full stack
- ✅ Environment-specific validations
- ✅ Production requirements validated
- ✅ Security best practices checked

## Adding New Tests

1. Create `new_module_test.tftest.hcl`
2. Add `run` blocks for test scenarios
3. Include assertions with clear error messages
4. Test locally: `./run-tests.sh new_module_test`
5. Commit and push (CI/CD runs automatically)

## Useful Links

- [Terraform Test Framework](https://developer.hashicorp.com/terraform/language/tests)
- [IONOS Provider Docs](https://registry.terraform.io/providers/ionos-cloud/ionoscloud/latest/docs)
- [GitHub Actions Workflow](.github/workflows/terraform-tests.yml)
