# GitHub Actions Workflow Testing Guide

This document describes how to test the GitHub Actions workflows in this repository and explains known issues and workarounds.

## Overview

The Ouroboros repository contains several GitHub Actions workflows for:
- **dotnet-coverage.yml**: .NET test coverage analysis
- **mutation-testing.yml**: Mutation testing with Stryker
- **ollama-integration-test.yml**: Integration tests with Ollama LLM
- **android-build.yml**: Android MAUI app building
- **terraform-infrastructure.yml**: Terraform infrastructure management
- **terraform-tests.yml**: Terraform configuration validation
- **ionos-deploy.yml**: Deployment to IONOS Cloud

## Prerequisites for Testing

### Required Tools
- [actionlint](https://github.com/rhysd/actionlint) - Workflow syntax validation
- [act](https://github.com/nektos/act) - Local workflow execution (optional)
- .NET 10.0 SDK
- Docker (for local testing)

### Required Secrets (Optional Features)
Some workflows require secrets to be configured. They are designed to gracefully skip when secrets are missing:

#### Email Notifications (Android Build)
- `SMTP_SERVER` - SMTP server address
- `SMTP_PORT` - SMTP server port
- `SMTP_USERNAME` - SMTP authentication username
- `SMTP_PASSWORD` - SMTP authentication password
- `SMTP_FROM_EMAIL` - From email address
- `NOTIFICATION_EMAIL` - Recipient email address

#### IONOS Cloud Deployment
- `IONOS_ADMIN_TOKEN` or `IONOS_ADMIN_USERNAME`/`IONOS_ADMIN_PASSWORD` - IONOS Cloud API credentials
- `IONOS_REGISTRY_USERNAME` - IONOS Container Registry username
- `IONOS_REGISTRY_PASSWORD` - IONOS Container Registry password
- `IONOS_KUBECONFIG` - Kubernetes configuration file

## Testing Workflows

### 1. Syntax Validation

Validate all workflow files for syntax errors:

```bash
# Install actionlint
curl -L https://github.com/rhysd/actionlint/releases/download/v1.6.26/actionlint_1.6.26_linux_amd64.tar.gz | tar xz

# Validate workflows
./actionlint .github/workflows/*.yml
```

Expected output: Only shellcheck style warnings (safe to ignore)

### 2. Local Build Testing

Test the build process locally before pushing:

```bash
# Restore dependencies
dotnet restore

# Build main projects (excluding tests due to NuGet vulnerability warning)
for project in \
  src/Ouroboros.Agent/Ouroboros.Agent.csproj \
  src/Ouroboros.CLI/Ouroboros.CLI.csproj \
  src/Ouroboros.Core/Ouroboros.Core.csproj \
  src/Ouroboros.Domain/Ouroboros.Domain.csproj \
  src/Ouroboros.Examples/Ouroboros.Examples.csproj \
  src/Ouroboros.Pipeline/Ouroboros.Pipeline.csproj \
  src/Ouroboros.Providers/Ouroboros.Providers.csproj \
  src/Ouroboros.Tools/Ouroboros.Tools.csproj \
  src/Ouroboros.WebApi/Ouroboros.WebApi.csproj
do
  dotnet build --configuration Release --no-restore "$project"
done

# Test projects
dotnet test --configuration Release \
  src/Ouroboros.Tests/Ouroboros.Tests.csproj
```

### 3. Workflow-Specific Testing

#### Testing dotnet-coverage.yml
```bash
# Trigger manually via GitHub Actions UI
# Or wait for push to main/develop with changes to:
# - src/**
# - tests/**
# - **.csproj
# - .github/workflows/dotnet-coverage.yml
```

#### Testing android-build.yml
```bash
# Check if Android project exists
ls -la src/Ouroboros.Android/

# Check if test project exists
ls -la src/Ouroboros.Android.Tests/

# Run smoke tests locally
cd src/Ouroboros.Android.Tests
dotnet restore
dotnet test --filter "Category=SmokeTests" --verbosity normal

# Trigger via push to develop branch or workflow_dispatch
```

**New Features (2025-12-31):**
- ✅ Automated smoke tests run before APK distribution
- ✅ Build metadata generation (version, commit, date)
- ✅ QR code generation for mobile downloads
- ✅ Enhanced email notifications with testing guidance
- ✅ Comprehensive tester documentation
- 📖 See [Android Testing Guide](../../docs/ANDROID_TESTING_GUIDE.md) for full details

#### Testing terraform-tests.yml
```bash
# Validate Terraform locally
cd terraform
terraform init -backend=false
terraform validate

# Run test script
cd tests
./run-tests.sh
```

#### Testing ollama-integration-test.yml
**Note**: This workflow requires significant resources and is best tested in GitHub Actions environment.

Local testing requires:
```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull required models (large downloads)
ollama pull llama3:8b
ollama pull nomic-embed-text

# Build and run CLI
cd src/Ouroboros.CLI
dotnet run -- ask -q "What is 2+2?" --model "llama3:8b"
```

## Known Issues and Workarounds

### Issue 1: NuGet Vulnerability Warning (NU1903)

**Problem**: The test project (`Ouroboros.Tests.csproj`) has `TreatWarningsAsErrors=true` and fails on NuGet vulnerability warning NU1903 for `Microsoft.Build.Tasks.Core` 17.7.2.

**Cause**: This is a transitive dependency vulnerability that will be fixed when LangChain or other dependencies update.

**Workflow Workaround**: 
- Build main projects separately (excluding tests)
- Run `dotnet test` which builds the test project with the warning
- Continue on test failure with a warning message

**Local Workaround**:
```bash
# Temporarily disable TreatWarningsAsErrors in Ouroboros.Tests.csproj
# Or wait for package updates to resolve the vulnerability
```

### Issue 2: Android Build Requires MAUI Workload

**Problem**: Building Android apps requires the MAUI Android workload which takes time to install.

**Workflow Solution**: 
- Check if Android project exists before attempting build
- Use retry logic for workload installation
- Cache NuGet packages to speed up subsequent builds

### Issue 3: IONOS Deployment Requires Multiple Secrets

**Problem**: Full deployment requires IONOS Cloud credentials, registry credentials, and Kubernetes config.

**Workflow Solution**:
- All IONOS-related steps are now conditional
- Workflows gracefully skip deployment when credentials are missing
- Helpful messages explain what secrets are needed

## Workflow Testing Checklist

Before merging workflow changes, verify:

- [x] ✅ Syntax validation passes (actionlint)
- [x] ✅ Workflows don't fail on missing optional secrets
- [x] ✅ Build steps work with current project structure
- [x] ✅ Test steps handle NuGet vulnerability warnings
- [x] ✅ Conditional logic works (Android project check, IONOS credential check)
- [x] ✅ Error messages are clear and actionable
- [x] ✅ Artifacts are uploaded correctly
- [x] ✅ Summary messages are informative

## Continuous Improvement

### Adding New Workflows

When adding new workflows:
1. Validate syntax with actionlint
2. Make external dependencies optional with proper checks
3. Add clear error messages for missing configuration
4. Test both success and failure paths
5. Document required secrets in this file
6. Use retry logic for flaky operations (network calls, package restores)

### Monitoring Workflow Performance

Track these metrics:
- **Build Time**: Should be < 5 minutes for main build
- **Test Time**: Should be < 10 minutes for full test suite
- **Cache Hit Rate**: Should be > 80% for NuGet packages
- **Workflow Success Rate**: Should be > 95% for non-flaky failures

## Debugging Failed Workflows

### Enable Debug Logging
Add these secrets to your repository:
- `ACTIONS_STEP_DEBUG=true` - Enables debug logging for all steps
- `ACTIONS_RUNNER_DEBUG=true` - Enables runner diagnostic logging

### Common Failures and Solutions

| Error | Likely Cause | Solution |
|-------|-------------|----------|
| `NU1903: Warning As Error` | NuGet vulnerability in test project | Verify build excludes test project initially |
| `No such file or directory: kubeconfig.yaml` | Missing IONOS_KUBECONFIG secret | Check that deployment step is conditional |
| `MAUI workload installation failed` | Network timeout or package unavailable | Workflow has retry logic, may need to re-run |
| `Module not found` in Terraform | Module directory missing | Verify terraform/modules structure |
| `Cannot connect to Ollama` | Service not started or crashed | Check Ollama service logs in workflow output |

## Support and Feedback

For issues with workflows:
1. Check workflow run logs in GitHub Actions UI
2. Review this testing guide for known issues
3. Open an issue with:
   - Workflow name
   - Run ID and link
   - Error message and relevant logs
   - Steps already attempted to fix

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [actionlint Documentation](https://github.com/rhysd/actionlint)
- [act - Run GitHub Actions locally](https://github.com/nektos/act)
- [.NET CLI Reference](https://learn.microsoft.com/en-us/dotnet/core/tools/)
- [Terraform Testing](https://developer.hashicorp.com/terraform/language/tests)
