# IONOS Infrastructure as Code (IaC) Implementation Guide

This document provides a comprehensive guide to the Infrastructure as Code (IaC) implementation for MonadicPipeline on IONOS Cloud using Terraform.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
- [Infrastructure Modules](#infrastructure-modules)
- [Environment Management](#environment-management)
- [CI/CD Integration](#cicd-integration)
- [Operations Guide](#operations-guide)
- [Migration from Manual Setup](#migration-from-manual-setup)
- [Troubleshooting](#troubleshooting)
- [Best Practices](#best-practices)

## Overview

The MonadicPipeline infrastructure is now fully automated using Terraform and the IONOS Cloud provider. This enables:

- **Zero manual infrastructure setup**: All resources provisioned via code
- **Environment parity**: Consistent infrastructure across dev/staging/prod
- **Version control**: Infrastructure changes tracked in Git
- **Reproducibility**: Infrastructure can be recreated anytime
- **Disaster recovery**: Fast recovery from failures
- **Cost optimization**: Right-sized resources per environment

### What This IaC Implementation Manages

✅ **Automated Resources**:
- IONOS Data Centers (virtual data centers for resource organization)
- Kubernetes Clusters (Managed Kubernetes Service with autoscaling)
- Container Registry (private Docker registry with vulnerability scanning)
- Persistent Storage Volumes (SSD/HDD volumes for stateful apps)
- Virtual Networks (LANs for cluster networking)

❌ **Not Yet Automated** (manual for now):
- DNS records (requires DNS provider integration)
- SSL certificates (manual Let's Encrypt setup)
- Load balancer configurations (managed by Kubernetes Ingress)
- RBAC policies (managed via kubectl/Kubernetes manifests)

## Architecture

### Infrastructure Organization

```
IONOS Cloud Account
└── Data Center (de/fra)
    ├── Kubernetes Cluster (MKS)
    │   └── Node Pool (3 nodes, autoscaling)
    ├── Container Registry
    │   └── Registry Token (authentication)
    ├── Storage Volumes
    │   ├── qdrant-data (50GB SSD)
    │   └── ollama-models (100GB SSD)
    └── Virtual Network (LAN)
```

### Module Dependencies

```
main.tf
├── datacenter module (base)
├── kubernetes module → depends on datacenter
├── registry module (independent)
├── storage module → depends on datacenter
└── networking module → depends on datacenter
```

### Resource Naming Convention

```
{project}-{environment}-{resource-type}

Examples:
- monadic-pipeline-prod (data center)
- monadic-pipeline-cluster (Kubernetes cluster)
- production-pool (node pool)
- adaptive-systems (container registry)
```

## Getting Started

### Prerequisites

1. **IONOS Cloud Account**: [Sign up](https://cloud.ionos.com)
2. **Terraform**: Install version >= 1.5.0
3. **IONOS API Credentials**: Generate from IONOS Cloud Console
4. **Git**: For version control

### Initial Setup

#### 1. Install Terraform

```bash
# macOS
brew install terraform

# Linux
wget https://releases.hashicorp.com/terraform/1.5.0/terraform_1.5.0_linux_amd64.zip
unzip terraform_1.5.0_linux_amd64.zip
sudo mv terraform /usr/local/bin/
```

#### 2. Configure IONOS Credentials

```bash
# Generate API token from IONOS Cloud Console:
# https://dcd.ionos.com → User Menu → API Credentials

export IONOS_TOKEN="your-api-token-here"

# OR use username/password (less secure)
export IONOS_USERNAME="your-username"
export IONOS_PASSWORD="your-password"
```

#### 3. Initialize Terraform

```bash
cd terraform
terraform init
```

#### 4. Provision Development Environment

```bash
# Review changes
terraform plan -var-file=environments/dev.tfvars

# Apply infrastructure
terraform apply -var-file=environments/dev.tfvars
```

#### 5. Get Kubeconfig

```bash
# Save kubeconfig for kubectl access
terraform output -raw k8s_kubeconfig > kubeconfig-dev.yaml

# Configure kubectl
export KUBECONFIG=./kubeconfig-dev.yaml
kubectl get nodes
```

## Infrastructure Modules

### Data Center Module

**Purpose**: Creates a virtual data center for resource organization.

**Location**: `terraform/modules/datacenter/`

**Resources Created**:
- IONOS virtual data center

**Key Variables**:
- `datacenter_name`: Name of the data center
- `location`: Physical location (e.g., `de/fra`, `de/txl`, `us/las`)

**Outputs**:
- `datacenter_id`: ID for use in other modules
- `datacenter_name`: Name of the data center

**Usage Example**:
```hcl
module "datacenter" {
  source = "./modules/datacenter"
  
  datacenter_name = "monadic-pipeline-prod"
  location        = "de/fra"
  description     = "Production data center"
}
```

### Kubernetes Module

**Purpose**: Provisions a managed Kubernetes cluster with autoscaling node pool.

**Location**: `terraform/modules/kubernetes/`

**Resources Created**:
- Kubernetes cluster (MKS)
- Node pool with autoscaling

**Key Variables**:
- `cluster_name`: Name of the cluster
- `k8s_version`: Kubernetes version (e.g., "1.28")
- `node_count`: Initial number of nodes
- `cores_count`: CPU cores per node
- `ram_size`: RAM in MB per node
- `storage_size`: Disk size in GB per node

**Outputs**:
- `cluster_id`: Cluster ID
- `kubeconfig`: Kubeconfig for kubectl access
- `node_pool_id`: Node pool ID

**Autoscaling Configuration**:
```hcl
auto_scaling {
  min_node_count = 2
  max_node_count = 5
}
```

### Container Registry Module

**Purpose**: Creates a private Docker registry with authentication.

**Location**: `terraform/modules/registry/`

**Resources Created**:
- Container registry
- Registry authentication token

**Key Variables**:
- `registry_name`: Name of the registry
- `location`: Registry location
- `garbage_collection_schedule`: Automated cleanup schedule

**Features**:
- Vulnerability scanning
- Automated garbage collection
- Token-based authentication

**Outputs**:
- `registry_hostname`: Registry URL (e.g., `adaptive-systems.cr.de-fra.ionos.com`)
- `registry_token_credentials`: Authentication credentials

### Storage Module

**Purpose**: Provisions persistent volumes for stateful applications.

**Location**: `terraform/modules/storage/`

**Resources Created**:
- Multiple storage volumes

**Key Variables**:
- `volumes`: List of volume configurations

**Volume Configuration**:
```hcl
volumes = [
  {
    name         = "qdrant-data"
    size         = 50
    type         = "SSD"
    licence_type = "OTHER"
  }
]
```

**Storage Types**:
- `SSD`: High-performance (recommended for production)
- `HDD`: Cost-effective (suitable for development)

### Networking Module

**Purpose**: Configures virtual networks and LANs.

**Location**: `terraform/modules/networking/`

**Resources Created**:
- Virtual LAN

**Key Variables**:
- `lan_name`: Name of the LAN
- `lan_public`: Public or private LAN

## Environment Management

### Environment Configurations

Three environments are pre-configured:

#### Development (`dev.tfvars`)
- **Purpose**: Feature development and testing
- **Nodes**: 2 nodes
- **Resources**: 2 cores, 8GB RAM, 50GB HDD per node
- **Storage**: HDD (cost-effective)
- **Estimated Cost**: €50-80/month

#### Staging (`staging.tfvars`)
- **Purpose**: Pre-production validation
- **Nodes**: 2 nodes
- **Resources**: 4 cores, 16GB RAM, 100GB SSD per node
- **Storage**: SSD (production-like)
- **Estimated Cost**: €100-150/month

#### Production (`production.tfvars`)
- **Purpose**: Live production workloads
- **Nodes**: 3 nodes (autoscaling 2-5)
- **Resources**: 4 cores, 16GB RAM, 100GB SSD per node
- **Storage**: SSD (high performance)
- **Estimated Cost**: €150-250/month

### Switching Environments

```bash
# Development
terraform plan -var-file=environments/dev.tfvars
terraform apply -var-file=environments/dev.tfvars

# Staging
terraform plan -var-file=environments/staging.tfvars
terraform apply -var-file=environments/staging.tfvars

# Production
terraform plan -var-file=environments/production.tfvars
terraform apply -var-file=environments/production.tfvars
```

### Creating Custom Environments

1. Copy an existing environment file:
   ```bash
   cp environments/production.tfvars environments/qa.tfvars
   ```

2. Modify the configuration:
   ```hcl
   datacenter_name = "monadic-pipeline-qa"
   cluster_name    = "monadic-pipeline-qa"
   environment     = "qa"
   ```

3. Apply the configuration:
   ```bash
   terraform apply -var-file=environments/qa.tfvars
   ```

## CI/CD Integration

### GitHub Actions Workflow

A dedicated workflow manages infrastructure: `.github/workflows/terraform-infrastructure.yml`

#### Manual Trigger

Use GitHub Actions UI:
1. Go to **Actions** → **Terraform Infrastructure**
2. Click **Run workflow**
3. Select:
   - **Environment**: dev, staging, or production
   - **Action**: plan, apply, or destroy
   - **Auto-approve**: true/false

#### Automated Trigger

The workflow automatically runs on pushes to `main` that modify:
- `terraform/**` files
- `.github/workflows/terraform-infrastructure.yml`

### Required GitHub Secrets

Configure these secrets in your GitHub repository:

```
IONOS_ADMIN_TOKEN       # IONOS API token (recommended)
IONOS_ADMIN_USERNAME    # IONOS username (alternative)
IONOS_ADMIN_PASSWORD    # IONOS password (alternative)
```

### Integration with Deployment Workflow

The main deployment workflow (`.github/workflows/ionos-deploy.yml`) now references the Terraform setup:

```yaml
- name: Ensure infrastructure prerequisites
  run: |
    echo "✅ Infrastructure as Code (IaC) available:"
    echo "   - Terraform configurations: terraform/"
    echo "   - Automated provisioning workflow"
```

## Operations Guide

### Common Tasks

#### Viewing Current Infrastructure

```bash
cd terraform
terraform show
```

#### Getting Outputs

```bash
# All outputs
terraform output

# Specific output
terraform output registry_hostname
terraform output k8s_cluster_id

# Save kubeconfig
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
```

#### Scaling Nodes

Edit environment file:
```hcl
node_count = 5  # Increase nodes
```

Apply changes:
```bash
terraform apply -var-file=environments/production.tfvars
```

#### Upgrading Kubernetes

Edit environment file:
```hcl
k8s_version = "1.29"  # Upgrade version
```

Apply changes:
```bash
terraform apply -var-file=environments/production.tfvars
```

#### Adding Storage Volumes

Edit environment file:
```hcl
volumes = [
  {
    name         = "qdrant-data"
    size         = 50
    type         = "SSD"
    licence_type = "OTHER"
  },
  {
    name         = "new-volume"
    size         = 100
    type         = "SSD"
    licence_type = "OTHER"
  }
]
```

Apply changes:
```bash
terraform apply -var-file=environments/production.tfvars
```

#### Destroying Infrastructure

**Warning**: This deletes all resources!

```bash
# Development (safe to destroy)
terraform destroy -var-file=environments/dev.tfvars

# Production (requires confirmation)
terraform destroy -var-file=environments/production.tfvars
```

## Migration from Manual Setup

If you have existing IONOS infrastructure created manually:

### Step 1: Inventory Resources

Document all existing resources:
- Data center ID
- Kubernetes cluster ID
- Container registry ID
- Volume IDs

### Step 2: Import Resources

Import existing resources into Terraform state:

```bash
# Data center
terraform import module.datacenter.ionoscloud_datacenter.main <datacenter-id>

# Kubernetes cluster
terraform import module.kubernetes.ionoscloud_k8s_cluster.main <cluster-id>

# Node pool
terraform import module.kubernetes.ionoscloud_k8s_node_pool.main <nodepool-id>

# Container registry
terraform import module.registry.ionoscloud_container_registry.main <registry-id>

# Volumes
terraform import module.storage.ionoscloud_volume.volumes[\"qdrant-data\"] <volume-id>
```

### Step 3: Validate State

```bash
# Check for drift
terraform plan -var-file=environments/production.tfvars

# Should show "No changes" if import was successful
```

### Step 4: Gradual Transition

- Start with non-production environments (dev, staging)
- Test infrastructure changes in dev first
- Migrate production last

## Troubleshooting

### Authentication Errors

**Problem**: `Error: authentication failed`

**Solution**:
```bash
# Verify credentials
curl -H "Authorization: Bearer $IONOS_TOKEN" https://api.ionos.com/cloudapi/v6/

# Check token is set
echo $IONOS_TOKEN
```

### State Lock Issues

**Problem**: `Error: state locked`

**Solution**:
```bash
# Force unlock (use with caution)
terraform force-unlock <lock-id>
```

### Resource Already Exists

**Problem**: `Error: resource already exists`

**Solution**:
```bash
# Import the existing resource
terraform import module.<module>.<resource> <resource-id>
```

### Validation Errors

**Problem**: `Error: invalid configuration`

**Solution**:
```bash
# Validate configuration
terraform validate

# Check formatting
terraform fmt -check -recursive
```

## Best Practices

### Security

1. **Never commit credentials**: Use environment variables or GitHub Secrets
2. **Use API tokens**: Preferred over username/password
3. **Rotate tokens regularly**: Set expiry dates
4. **Enable vulnerability scanning**: In container registry
5. **Use remote state**: For production (S3-compatible backend)
6. **Restrict API access**: Use `api_subnet_allow_list`

### Cost Optimization

1. **Right-size environments**: Dev uses HDD, fewer nodes
2. **Enable autoscaling**: Scale down during low usage
3. **Use garbage collection**: Clean up unused container images
4. **Monitor costs**: Regular review of resource usage
5. **Destroy unused environments**: Delete dev/staging when not needed

### Infrastructure Management

1. **Version control everything**: Commit all Terraform changes
2. **Use workspaces or separate state**: Per environment
3. **Plan before apply**: Always review changes
4. **Document changes**: Update this guide with modifications
5. **Test in dev first**: Never test in production
6. **Use modules**: Keep configuration DRY
7. **Tag resources**: For organization and cost tracking

### Disaster Recovery

1. **State backup**: Store state remotely with versioning
2. **Export kubeconfig**: Save kubeconfig regularly
3. **Document procedures**: Keep runbooks updated
4. **Test recovery**: Practice infrastructure recreation
5. **Monitor alerts**: Set up infrastructure monitoring

## Related Documentation

- [Terraform README](../terraform/README.md) - Detailed module documentation
- [IONOS Deployment Guide](IONOS_DEPLOYMENT_GUIDE.md) - Application deployment
- [IONOS Cloud API Docs](https://api.ionos.com/docs/) - API reference
- [Terraform IONOS Provider](https://registry.terraform.io/providers/ionos-cloud/ionoscloud/latest/docs)

## Support and Contributing

### Getting Help

- **IONOS Support**: [https://www.ionos.com/help](https://www.ionos.com/help)
- **Terraform Issues**: GitHub Issues
- **Documentation**: This guide and module READMEs

### Contributing

When adding new infrastructure:

1. Create feature branch
2. Add/modify modules in `terraform/modules/`
3. Update `main.tf`, `variables.tf`, `outputs.tf`
4. Update environment files
5. Document changes in this guide
6. Test in dev environment
7. Create pull request

---

**Last Updated**: January 2025  
**Author**: MonadicPipeline Team  
**Terraform Version**: >= 1.5.0  
**IONOS Provider Version**: ~> 6.7.0
