# GitHub Actions Workflows

This directory contains GitHub Actions workflows for automated CI/CD of MonadicPipeline.

## Active Workflows

### IONOS Cloud Deployment (`ionos-deploy.yml`)

**Status**: ✅ Active (Primary deployment target)

Automatically builds, tests, and deploys MonadicPipeline to IONOS Cloud Kubernetes infrastructure.

**Triggers**:
- Push to `main` branch
- Manual trigger via GitHub Actions UI

**Jobs**:
1. **test**: Runs xUnit tests
2. **build-and-push**: Builds Docker images and pushes to IONOS Container Registry
3. **deploy**: Deploys to IONOS Kubernetes cluster

**Required Secrets**:
- `IONOS_REGISTRY_USERNAME`: IONOS Container Registry username
- `IONOS_REGISTRY_PASSWORD`: IONOS Container Registry password
- `IONOS_KUBECONFIG`: Base64-encoded kubeconfig file

**Optional Variables**:
- `IONOS_REGISTRY`: Registry URL (default: `registry.ionos.com`)
- `IONOS_PROJECT`: Project name (default: `adaptive-systems`)

See [IONOS Deployment Guide](../../docs/IONOS_DEPLOYMENT_GUIDE.md) for detailed setup instructions.

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

3. **Push to main branch** or manually trigger the workflow

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

## Related Documentation

- [IONOS Deployment Guide](../../docs/IONOS_DEPLOYMENT_GUIDE.md)
- [General Deployment Guide](../../DEPLOYMENT.md)
- [Troubleshooting Guide](../../TROUBLESHOOTING.md)
