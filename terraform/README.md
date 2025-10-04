# MonadicPipeline Infrastructure as Code (Terraform)

This directory contains Terraform configuration for provisioning and managing MonadicPipeline infrastructure on IONOS Cloud.

## Overview

The infrastructure is organized into modular components:

- **Data Center**: Virtual data center for resource organization
- **Kubernetes**: Managed Kubernetes Service (MKS) with autoscaling node pools
- **Container Registry**: Private Docker registry for container images
- **Storage**: Persistent volumes for stateful applications
- **Networking**: Virtual LANs and network configuration

## Prerequisites

1. **Terraform**: Install Terraform >= 1.5.0
   ```bash
   # macOS
   brew install terraform

   # Linux
   wget https://releases.hashicorp.com/terraform/1.5.0/terraform_1.5.0_linux_amd64.zip
   unzip terraform_1.5.0_linux_amd64.zip
   sudo mv terraform /usr/local/bin/
   ```

2. **IONOS Cloud Account**: Sign up at [cloud.ionos.com](https://cloud.ionos.com)

3. **IONOS Cloud Credentials**: Set up authentication
   ```bash
   # Option 1: Using username/password
   export IONOS_USERNAME="your-username"
   export IONOS_PASSWORD="your-password"

   # Option 2: Using API token (recommended)
   export IONOS_TOKEN="your-api-token"
   ```

## Directory Structure

```
terraform/
├── main.tf                      # Main infrastructure orchestration
├── variables.tf                 # Variable definitions
├── outputs.tf                   # Output definitions
├── README.md                    # This file
├── modules/                     # Reusable infrastructure modules
│   ├── datacenter/             # Data center module
│   ├── kubernetes/             # Kubernetes cluster module
│   ├── registry/               # Container registry module
│   ├── storage/                # Storage volumes module
│   └── networking/             # Networking module
└── environments/               # Environment-specific configurations
    ├── dev.tfvars              # Development environment
    ├── staging.tfvars          # Staging environment
    └── production.tfvars       # Production environment
```

## Quick Start

### 1. Initialize Terraform

```bash
cd terraform
terraform init
```

### 2. Review Planned Changes

```bash
# For development environment
terraform plan -var-file=environments/dev.tfvars

# For staging environment
terraform plan -var-file=environments/staging.tfvars

# For production environment
terraform plan -var-file=environments/production.tfvars
```

### 3. Apply Infrastructure

```bash
# Development
terraform apply -var-file=environments/dev.tfvars

# Staging
terraform apply -var-file=environments/staging.tfvars

# Production
terraform apply -var-file=environments/production.tfvars
```

### 4. Get Outputs

```bash
# View all outputs
terraform output

# Get specific output (e.g., kubeconfig)
terraform output -raw k8s_kubeconfig > kubeconfig.yaml

# Get registry hostname
terraform output registry_hostname

# Get external access information
terraform output external_access_info
```

### 5. Validate External Accessibility (NEW)

After deploying infrastructure, validate that it's accessible from outside:

```bash
# From the project root
./scripts/check-external-access.sh dev

# Or for production
./scripts/check-external-access.sh production
```

This script checks:
- Container registry accessibility
- Kubernetes cluster state (ACTIVE/INACTIVE)
- Public IP assignments
- Network configuration
- kubectl connectivity

See [External Access Validation Guide](../docs/EXTERNAL_ACCESS_VALIDATION.md) for details.

## Available Outputs

After deploying infrastructure, Terraform provides the following outputs:

### Core Infrastructure Outputs

- **`datacenter_id`**: ID of the created data center
- **`datacenter_name`**: Name of the data center
- **`k8s_cluster_id`**: Kubernetes cluster ID
- **`k8s_cluster_name`**: Kubernetes cluster name
- **`k8s_kubeconfig`**: Kubeconfig for cluster access (sensitive)
- **`k8s_node_pool_id`**: Node pool ID
- **`registry_id`**: Container registry ID
- **`registry_hostname`**: Registry hostname (e.g., `myregistry.cr.de-fra.ionos.com`)
- **`registry_location`**: Registry location

### External Accessibility Outputs (NEW)

These outputs help verify and troubleshoot external accessibility:

- **`k8s_public_ips`**: Public IP addresses assigned to Kubernetes nodes
- **`k8s_api_subnet_allow_list`**: Allowed subnets for Kubernetes API access
- **`k8s_cluster_state`**: Current cluster state (ACTIVE, PROVISIONING, etc.)
- **`k8s_node_pool_state`**: Current node pool state (ACTIVE, PROVISIONING, etc.)
- **`lan_id`**: LAN ID
- **`lan_name`**: LAN name
- **`lan_public`**: Whether LAN is public or private

### Consolidated Outputs

- **`deployment_summary`**: Summary of all deployed resources
- **`external_access_info`**: Comprehensive external access information including:
  - Registry hostname and location
  - Kubernetes public IPs
  - API access configuration
  - LAN public status
  - Cluster and node pool states

**Example usage:**

```bash
# Get all outputs
terraform output

# Get external access information
terraform output external_access_info

# Get public IPs
terraform output k8s_public_ips

# Get kubeconfig
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
```

## Environment Configurations

### Development (`dev.tfvars`)
- **Purpose**: Local development and testing
- **Resources**: Minimal (2 nodes, 8GB RAM, HDD storage)
- **Cost**: ~€50-80/month
- **Use case**: Feature development, integration testing

### Staging (`staging.tfvars`)
- **Purpose**: Pre-production validation
- **Resources**: Medium (2 nodes, 16GB RAM, SSD storage)
- **Cost**: ~€100-150/month
- **Use case**: QA testing, performance validation

### Production (`production.tfvars`)
- **Purpose**: Production workloads
- **Resources**: Full (3 nodes, 16GB RAM, SSD storage, autoscaling)
- **Cost**: ~€150-250/month
- **Use case**: Live applications, customer-facing services

## Module Documentation

### Data Center Module

Creates an IONOS virtual data center for resource organization.

**Resources**:
- `ionoscloud_datacenter`: Virtual data center

**Variables**:
- `datacenter_name`: Name of the data center
- `location`: Physical location (e.g., `de/fra`, `de/txl`, `us/las`)
- `description`: Description of the data center

### Kubernetes Module

Provisions a managed Kubernetes cluster with autoscaling node pool.

**Resources**:
- `ionoscloud_k8s_cluster`: Kubernetes cluster
- `ionoscloud_k8s_node_pool`: Worker node pool

**Variables**:
- `cluster_name`: Name of the cluster
- `k8s_version`: Kubernetes version
- `node_count`: Number of worker nodes
- `cores_count`: CPU cores per node
- `ram_size`: RAM in MB per node
- `storage_size`: Disk size in GB per node

**Features**:
- Autoscaling (configurable min/max nodes)
- Maintenance windows
- Multiple availability zones

### Container Registry Module

Creates a private container registry with authentication tokens.

**Resources**:
- `ionoscloud_container_registry`: Registry instance
- `ionoscloud_container_registry_token`: Authentication token

**Variables**:
- `registry_name`: Name of the registry
- `location`: Registry location
- `garbage_collection_schedule`: Automated cleanup schedule

**Features**:
- Vulnerability scanning
- Garbage collection
- Token-based authentication

### Storage Module

Provisions persistent volumes for stateful applications.

**Resources**:
- `ionoscloud_volume`: Storage volumes

**Variables**:
- `volumes`: List of volume configurations

**Supported Storage Types**:
- `SSD`: High-performance storage
- `HDD`: Cost-effective storage

### Networking Module

Configures virtual networks and LANs.

**Resources**:
- `ionoscloud_lan`: Virtual LAN

**Variables**:
- `lan_name`: Name of the LAN
- `lan_public`: Public or private LAN

## State Management

### Local State (Default)

By default, Terraform stores state locally in `terraform.tfstate`. This is suitable for development but not recommended for production.

### Remote State (Recommended for Production)

Configure S3-compatible backend for team collaboration:

```hcl
terraform {
  backend "s3" {
    bucket   = "monadic-pipeline-terraform-state"
    key      = "ionos/terraform.tfstate"
    region   = "de"
    endpoint = "https://s3-eu-central-1.ionoscloud.com"
    
    skip_credentials_validation = true
    skip_region_validation      = true
    skip_metadata_api_check     = true
  }
}
```

Initialize with remote backend:
```bash
terraform init -backend-config="access_key=$S3_ACCESS_KEY" \
               -backend-config="secret_key=$S3_SECRET_KEY"
```

## CI/CD Integration

### GitHub Actions

The infrastructure can be managed automatically via GitHub Actions. See `.github/workflows/ionos-deploy.yml` for the workflow configuration.

**Required Secrets**:
- `IONOS_USERNAME` or `IONOS_TOKEN`: IONOS Cloud API credentials
- `IONOS_PASSWORD`: (if using username/password auth)
- `TF_STATE_ACCESS_KEY`: S3 access key for state storage
- `TF_STATE_SECRET_KEY`: S3 secret key for state storage

**Workflow triggers**:
- Manual trigger via GitHub UI
- Automated on infrastructure changes (optional)

### Applying Infrastructure via CI/CD

```yaml
- name: Terraform Apply
  env:
    IONOS_TOKEN: ${{ secrets.IONOS_ADMIN_TOKEN }}
  run: |
    cd terraform
    terraform init
    terraform apply -var-file=environments/production.tfvars -auto-approve
```

## Managing Infrastructure

### Updating Resources

1. Modify the appropriate `.tfvars` file or module
2. Review changes: `terraform plan -var-file=environments/<env>.tfvars`
3. Apply changes: `terraform apply -var-file=environments/<env>.tfvars`

### Scaling Kubernetes Nodes

Edit the environment file:
```hcl
node_count = 5  # Increase from 3 to 5
```

Apply changes:
```bash
terraform apply -var-file=environments/production.tfvars
```

### Upgrading Kubernetes Version

Edit the environment file:
```hcl
k8s_version = "1.29"  # Upgrade from 1.28
```

Apply changes:
```bash
terraform apply -var-file=environments/production.tfvars
```

### Destroying Infrastructure

**Warning**: This will delete all resources!

```bash
# Development
terraform destroy -var-file=environments/dev.tfvars

# Production (requires confirmation)
terraform destroy -var-file=environments/production.tfvars
```

## Cost Optimization

### Development Environment
- Use HDD instead of SSD: ~30% cost savings
- Reduce node count to 2
- Use smaller instance sizes

### Autoscaling
Enable autoscaling to scale down during low usage:
```hcl
auto_scaling {
  min_node_count = 2
  max_node_count = 5
}
```

### Garbage Collection
Configure registry garbage collection to clean up unused images:
```hcl
garbage_collection_schedule {
  days = ["Sunday"]
  time = "02:00:00"
}
```

## Troubleshooting

### Authentication Issues

```bash
# Verify credentials
curl -u "$IONOS_USERNAME:$IONOS_PASSWORD" https://api.ionos.com/cloudapi/v6/

# Or with token
curl -H "Authorization: Bearer $IONOS_TOKEN" https://api.ionos.com/cloudapi/v6/
```

### Terraform State Issues

```bash
# Refresh state
terraform refresh -var-file=environments/production.tfvars

# Force unlock (if state is locked)
terraform force-unlock <lock-id>
```

### Resource Conflicts

If resources already exist manually:
```bash
# Import existing resource
terraform import module.datacenter.ionoscloud_datacenter.main <datacenter-id>
```

## Security Best Practices

1. **Never commit credentials**: Use environment variables or secret management
2. **Use API tokens**: Preferred over username/password
3. **Enable vulnerability scanning**: In container registry
4. **Restrict API access**: Use `api_subnet_allow_list` for Kubernetes
5. **Rotate tokens regularly**: Set expiry dates for registry tokens
6. **Use remote state**: With encryption and access controls
7. **Apply least privilege**: Use IONOS IAM for role-based access

## Migration from Manual Setup

If you have existing IONOS infrastructure:

1. **Inventory existing resources**: Document current setup
2. **Import resources**: Use `terraform import` for each resource
3. **Validate state**: Run `terraform plan` to check for drift
4. **Gradually transition**: Migrate one environment at a time

Example import:
```bash
terraform import module.kubernetes.ionoscloud_k8s_cluster.main <cluster-id>
terraform import module.registry.ionoscloud_container_registry.main <registry-id>
```

## Related Documentation

- [IONOS Cloud API Documentation](https://api.ionos.com/docs/)
- [IONOS Terraform Provider](https://registry.terraform.io/providers/ionos-cloud/ionoscloud/latest/docs)
- [MonadicPipeline Deployment Guide](../docs/IONOS_DEPLOYMENT_GUIDE.md)
- [GitHub Actions Workflow](../.github/workflows/ionos-deploy.yml)

## Support

For issues or questions:
- **IONOS Cloud Support**: [https://www.ionos.com/help](https://www.ionos.com/help)
- **Terraform Issues**: [GitHub Issues](https://github.com/PMeeske/MonadicPipeline/issues)
- **Documentation**: See `/docs` directory

## Contributing

When adding new infrastructure:

1. Create a new module in `modules/`
2. Update `main.tf` to include the module
3. Add variables to `variables.tf`
4. Add outputs to `outputs.tf`
5. Update environment files as needed
6. Document changes in this README

---

**Last Updated**: January 2025  
**Terraform Version**: >= 1.5.0  
**IONOS Provider Version**: ~> 6.7.0
