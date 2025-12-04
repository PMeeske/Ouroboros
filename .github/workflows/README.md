# GitHub Actions Workflows

This directory contains GitHub Actions workflows for automated CI/CD of Ouroboros.

## 🤖 Copilot Development Loop Workflows

**NEW**: Automated development assistance powered by AI

- [**Copilot Workflows Documentation**](README_COPILOT.md) - Complete guide to AI-assisted workflows
- `copilot-code-review.yml` - Automated PR reviews with functional programming checks
- `copilot-issue-assistant.yml` - Issue analysis and implementation guidance
- `copilot-continuous-improvement.yml` - Weekly code quality reports

See [Copilot Development Loop Guide](../../docs/COPILOT_DEVELOPMENT_LOOP.md) for full documentation.

---

## Active Workflows

### Ollama Integration Tests (`ollama-integration-test.yml`)

**Status**: ✅ Active (Continuous Integration)

End-to-end integration tests with Ollama to validate the reverse engineering pipeline using real LLM models.

**Triggers**:
- Push to `main` branch (when source files change)
- Pull requests to `main` (when source files change)
- Manual trigger via GitHub Actions UI

**Features**:
- Sets up Ollama service on GitHub Actions runner
- Pulls lightweight models (llama3:8b, nomic-embed-text)
- Tests CLI commands (ask, pipeline DSL, reverse engineering)
- Memory-efficient configuration for CI/CD constraints
- 30-minute timeout with concurrent job cancellation

**Test Cases**:
1. Basic Ollama connectivity test
2. Pipeline DSL execution
3. Memory-efficient reverse engineering workflow
4. RAG with embeddings

**Resource Constraints**:
- RAM: 7GB available on GitHub Actions
- Model Size: llama3:8b (~4.7GB)
- Timeout: 30 minutes

---

### IONOS Cloud Deployment (`ionos-deploy.yml`)

**Status**: ✅ Active (Primary deployment target)

Automatically builds, tests, and deploys Ouroboros to IONOS Cloud Kubernetes infrastructure.

**Triggers**:
- Push to `main` branch
- Manual trigger via GitHub Actions UI

**Jobs**:
1. **infrastructure**: Manages IONOS Cloud infrastructure via API (optional)
2. **test**: Runs xUnit tests
3. **build-and-push**: Builds Docker images and pushes to IONOS Container Registry
4. **deploy**: Deploys to IONOS Kubernetes cluster

**Required Secrets**:
- `IONOS_REGISTRY_USERNAME`: IONOS Container Registry username
- `IONOS_REGISTRY_PASSWORD`: IONOS Container Registry password
- `IONOS_KUBECONFIG`: Base64-encoded kubeconfig file

**Optional Secrets** (for infrastructure management):
- `IONOS_ADMIN_USERNAME`: IONOS Cloud API username
- `IONOS_ADMIN_PASSWORD`: IONOS Cloud API password
- `IONOS_ADMIN_TOKEN`: IONOS Cloud API token (preferred over username/password)

**Optional Variables**:
- `IONOS_REGISTRY`: Registry URL (default: `adaptive-systems.cr.de-fra.ionos.com`)

**API Specification**:
- `ionos-api.yaml`: IONOS Cloud API v6 OpenAPI specification for infrastructure management

See [IONOS Deployment Guide](../../docs/IONOS_DEPLOYMENT_GUIDE.md) for detailed setup instructions.

---

### .NET Test Coverage (`dotnet-coverage.yml`)

**Status**: ✅ Active

Runs unit tests with code coverage reporting.

**Triggers**:
- Push to `main` or `develop` branches
- Pull requests
- Manual trigger via GitHub Actions UI

**Features**:
- Runs 224 unit tests (mocked, without Ollama)
- Generates code coverage reports
- Publishes test results and coverage to PRs

---

### Azure AKS Deployment (`azure-deploy.yml`)

**Status**: ⚠️ Legacy (Disabled by default)

Previous Azure Kubernetes Service (AKS) deployment workflow. Kept for reference and can be manually triggered if needed.

**Triggers**:
- Manual trigger only (automatic push trigger is disabled)

**Required Secrets** (for manual use):
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

---

## Quick Setup

### For IONOS Cloud (Recommended)

1. **Set up secrets** in GitHub repository settings:
   ```
   Settings → Secrets and variables → Actions → New repository secret
   ```

2. **Add required secrets**:
   - `IONOS_REGISTRY_USERNAME`
   - `IONOS_REGISTRY_PASSWORD`
   - `IONOS_KUBECONFIG`

3. **Add optional infrastructure management secrets** (if managing infrastructure via API):
   - `IONOS_ADMIN_USERNAME` and `IONOS_ADMIN_PASSWORD` (basic auth)
   - OR `IONOS_ADMIN_TOKEN` (token auth, recommended)

4. **Push to main branch** or manually trigger the workflow

### For Azure AKS (Legacy)

The Azure workflow is disabled by default. To use it:

1. Enable the workflow by uncommenting the push trigger in `azure-deploy.yml`
2. Configure Azure secrets (AZURE_CLIENT_ID, etc.)
3. Update environment variables for your AKS cluster

---

## Workflow Migration

**Date**: January 2025  
**Change**: Migrated from Azure AKS to IONOS Cloud as primary deployment target

**Reasons**:
- Cost-effectiveness
- European data sovereignty
- Enterprise features
- Better storage options (ionos-enterprise-ssd)

**Impact**:
- New deployments use IONOS Cloud infrastructure
- Azure workflow preserved for backward compatibility
- Automated deployments now target IONOS Cloud

---

## API Specifications

### IONOS Cloud API (`ionos-api.yaml`)

OpenAPI 3.0.3 specification for IONOS Cloud API v6. This file documents the complete IONOS Cloud Infrastructure API and is referenced by the `ionos-deploy.yml` workflow for infrastructure management operations.

**API Details**:
- **Version**: 6.0
- **Base URL**: https://api.ionos.com/cloudapi/v6
- **Authentication**: Basic Auth or Token-based
- **Capabilities**: Data centers, Kubernetes, networking, storage, and more

The infrastructure management job in `ionos-deploy.yml` uses this API specification to verify connectivity and enable automated infrastructure provisioning when IONOS admin credentials are configured.

---

## Related Documentation

- [IONOS Deployment Guide](../../docs/IONOS_DEPLOYMENT_GUIDE.md)
- [General Deployment Guide](../../DEPLOYMENT.md)
- [Troubleshooting Guide](../../TROUBLESHOOTING.md)
