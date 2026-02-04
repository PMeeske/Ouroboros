# GitHub Actions Quick Reference Guide

## 📌 Pinned Action Versions (Copy-Paste Ready)

Use these for any new workflows or updates:

```yaml
# Core Actions
actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11  # v4.1.1
actions/setup-dotnet@4d6c8fcf3c8f7a60068d26b594648e99df24cee3  # v4.0.0
actions/cache@ab5e6d0c87105b4c9c2047343972218f562e4319  # v4.0.1
actions/upload-artifact@5d5d22a31266ced268874388b861e4b58bb5c2f3  # v4.3.1
actions/download-artifact@87c55149d96e628cc2ef7e6fc2aab372015aec85  # v4.1.8

# Reliability
nick-fields/retry@7152eba30c6575329ac0576536151aca5a72780e  # v3.0.0

# Docker
docker/login-action@e92390c5fb421da1463c202d546fed0ec5c39f20  # v3.1.0
docker/setup-buildx-action@2b51285047da1547ffb1b2203d8be4c0af6b1f20  # v3.2.0
docker/build-push-action@2cdde995de11925a030ce8070c3d77a52ffcf1c0  # v5.3.0

# Infrastructure
hashicorp/setup-terraform@a1502cd9e758c50496cc9ac5308c4843bcd56d36  # v3.0.0
azure/setup-kubectl@901a10e89ea615cf61f57ac05cecdf23e7de06d8  # v3.2

# GitHub
actions/github-script@60a0d83039c74a4aee543508d2ffcb1c3799cdea  # v7.0.1

# Testing & Coverage
EnricoMi/publish-unit-test-result-action@30eadd5010312f995f0d3b3cff7fe2984f69409e  # v2.16.1
irongut/CodeCoverageSummary@51cc3a756ddcd398d447c044c02cb6aa83fdae95  # v1.3.0
marocchino/sticky-pull-request-comment@331f8f5b4215f0445d3c07b4967662a32a2d3e31  # v2.9.0
codecov/codecov-action@54bcd8715eee62d40e33596ef5e8f0f48dbbccab  # v4.1.0
```

## 🔧 Standard Patterns

### .NET Workflow Template

```yaml
name: .NET Workflow

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  workflow_dispatch:

permissions:
  contents: read
  pull-requests: write
  checks: write

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

env:
  DOTNET_VERSION: '10.0.x'
  DOTNET_CLI_TELEMETRY_OPTOUT: '1'
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1'
  DOTNET_NOLOGO: '1'

jobs:
  build-and-test:
    name: Build and Test
    runs-on: ubuntu-latest
    timeout-minutes: 30
    
    steps:
    - name: Checkout code
      uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11  # v4.1.1
      with:
        fetch-depth: 0
    
    - name: Setup .NET
      uses: actions/setup-dotnet@4d6c8fcf3c8f7a60068d26b594648e99df24cee3  # v4.0.0
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Cache NuGet packages
      uses: actions/cache@ab5e6d0c87105b4c9c2047343972218f562e4319  # v4.0.1
      with:
        path: ~/.nuget/packages
        key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}
        restore-keys: |
          ${{ runner.os }}-nuget-
    
    - name: Restore dependencies
      uses: nick-fields/retry@7152eba30c6575329ac0576536151aca5a72780e  # v3.0.0
      with:
        timeout_minutes: 10
        max_attempts: 3
        retry_wait_seconds: 30
        command: dotnet restore --verbosity normal
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
      timeout-minutes: 15
    
    - name: Test
      uses: nick-fields/retry@7152eba30c6575329ac0576536151aca5a72780e  # v3.0.0
      with:
        timeout_minutes: 20
        max_attempts: 2
        retry_wait_seconds: 10
        command: dotnet test --configuration Release --no-build --verbosity normal
```

### Network Operation with Retry

```yaml
- name: Network Operation
  uses: nick-fields/retry@7152eba30c6575329ac0576536151aca5a72780e  # v3.0.0
  with:
    timeout_minutes: 10
    max_attempts: 3
    retry_wait_seconds: 30
    command: |
      # Your network command here
      curl -fsSL https://example.com/install.sh | sh
```

### Docker Build with Caching

```yaml
- name: Set up Docker Buildx
  uses: docker/setup-buildx-action@2b51285047da1547ffb1b2203d8be4c0af6b1f20  # v3.2.0

- name: Login to Container Registry
  uses: docker/login-action@e92390c5fb421da1463c202d546fed0ec5c39f20  # v3.1.0
  with:
    registry: ${{ env.REGISTRY }}
    username: ${{ secrets.REGISTRY_USERNAME }}
    password: ${{ secrets.REGISTRY_PASSWORD }}
  timeout-minutes: 5

- name: Build and push Docker image
  uses: docker/build-push-action@2cdde995de11925a030ce8070c3d77a52ffcf1c0  # v5.3.0
  with:
    context: .
    file: ./Dockerfile
    push: true
    tags: |
      ${{ env.REGISTRY }}/myapp:latest
      ${{ env.REGISTRY }}/myapp:${{ github.sha }}
    cache-from: type=gha
    cache-to: type=gha,mode=max
  timeout-minutes: 30
```

### Artifact Upload/Download

```yaml
# Upload
- name: Upload test results
  uses: actions/upload-artifact@5d5d22a31266ced268874388b861e4b58bb5c2f3  # v4.3.1
  if: always()
  continue-on-error: true
  with:
    name: test-results
    path: TestResults/*.trx
    retention-days: 30

# Download
- name: Download test results
  uses: actions/download-artifact@87c55149d96e628cc2ef7e6fc2aab372015aec85  # v4.1.8
  with:
    name: test-results
    path: TestResults
```

### Job Summary

```yaml
- name: Generate summary
  if: always()
  run: |
    echo "## 📊 Workflow Summary" >> $GITHUB_STEP_SUMMARY
    echo "" >> $GITHUB_STEP_SUMMARY
    echo "### Environment" >> $GITHUB_STEP_SUMMARY
    echo "- **Runner**: ${{ runner.os }}" >> $GITHUB_STEP_SUMMARY
    echo "- **.NET Version**: ${{ env.DOTNET_VERSION }}" >> $GITHUB_STEP_SUMMARY
    echo "- **Status**: ✅ Success" >> $GITHUB_STEP_SUMMARY
```

## 📋 Timeouts Reference

| Operation | Recommended Timeout |
|-----------|---------------------|
| Checkout | None (not supported) |
| .NET Setup | Default |
| NuGet restore | 10 minutes |
| Build | 15 minutes |
| Unit tests | 15 minutes |
| Integration tests | 30 minutes |
| Mutation testing | 120 minutes |
| Docker build | 30 minutes |
| Docker push | 15 minutes |
| Terraform init | 10 minutes |
| Terraform plan | 20 minutes |
| Terraform apply | 30 minutes |
| MAUI workload install | 15 minutes |
| Model downloads | 20 minutes |

## 🎯 Common Mistakes to Avoid

### ❌ DON'T DO THIS

```yaml
# Unpinned actions
- uses: actions/checkout@v4

# No retry for network operations
- run: dotnet restore

# No timeout
- run: dotnet build

# Timeout on checkout
- name: Checkout code
  uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11  # v4.1.1
  timeout-minutes: 5  # ❌ Not supported

# Overly broad permissions
permissions: write-all

# Hard-coded values
dotnet-version: '10.0.x'  # Should be in env

# Missing continue-on-error for optional steps
- name: Upload artifact
  uses: actions/upload-artifact@5d5d22a31266ced268874388b861e4b58bb5c2f3  # v4.3.1
  # Missing: continue-on-error: true
```

### ✅ DO THIS INSTEAD

```yaml
# Pinned actions
- uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11  # v4.1.1

# Retry for network operations
- name: Restore dependencies
  uses: nick-fields/retry@7152eba30c6575329ac0576536151aca5a72780e  # v3.0.0
  with:
    timeout_minutes: 10
    max_attempts: 3
    command: dotnet restore

# Timeout on operation
- name: Build
  run: dotnet build
  timeout-minutes: 15

# No timeout on checkout (correct)
- name: Checkout code
  uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11  # v4.1.1

# Minimal permissions
permissions:
  contents: read
  pull-requests: write

# Environment variable
env:
  DOTNET_VERSION: '10.0.x'

# Continue-on-error for optional steps
- name: Upload artifact
  uses: actions/upload-artifact@5d5d22a31266ced268874388b861e4b58bb5c2f3  # v4.3.1
  if: always()
  continue-on-error: true
```

## 🔍 Debugging Tips

### View Workflow Logs
```bash
# Using GitHub CLI
gh run list --workflow=dotnet-coverage.yml
gh run view <run-id> --log
```

### Test Workflow Locally
```bash
# Using act (https://github.com/nektos/act)
act -j test-coverage
```

### Validate Workflow Syntax
```bash
# Using actionlint
actionlint .github/workflows/*.yml
```

## 📞 Support

### Questions?
- Check the full summary: `WORKFLOW_REVISION_SUMMARY.md`
- GitHub Actions docs: https://docs.github.com/actions
- Open an issue with label `workflow-help`

### Need to Update an Action?
1. Find the release on GitHub
2. Get the commit SHA
3. Update with `uses: action@<SHA>  # v<VERSION>`
4. Test thoroughly

---

**Last Updated:** December 2024  
**Status:** ✅ Production Ready
