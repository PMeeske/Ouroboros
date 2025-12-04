# Ouroboros Infrastructure as Code (Terraform)

This directory contains Terraform configuration for provisioning and managing Ouroboros infrastructure on IONOS Cloud.

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
│   ├── networking/             # Networking module
│   └── app-config/             # Application configuration module
├── environments/               # Environment-specific configurations
│   ├── dev.tfvars              # Development environment
│   ├── staging.tfvars          # Staging environment
│   └── production.tfvars       # Production environment
└── tests/                      # Terraform tests
    ├── README.md               # Testing documentation
    ├── run-tests.sh            # Test runner script
    ├── datacenter_test.tftest.hcl
    ├── kubernetes_test.tftest.hcl
    ├── registry_test.tftest.hcl
    ├── storage_test.tftest.hcl
    ├── networking_test.tftest.hcl
    ├── app_config_test.tftest.hcl
    └── integration_test.tftest.hcl
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

## Testing

### Running Terraform Tests

The repository includes a comprehensive test suite for all Terraform modules and configurations.

```bash
# Run all tests
cd terraform/tests
./run-tests.sh

# Run specific test
./run-tests.sh datacenter_test
```

### Test Coverage

The test suite includes:

- **Module Unit Tests**: Validate each module independently
  - Data center configuration
  - Kubernetes cluster setup
  - Container registry configuration
  - Storage volumes
  - Networking setup
  - Application configuration

- **Integration Tests**: Validate complete infrastructure setup
  - Development environment
  - Production environment with HA requirements
  - Resource dependencies

- **Environment Tests**: Validate environment-specific configurations
  - dev.tfvars validation
  - staging.tfvars validation
  - production.tfvars validation

### Continuous Integration

Tests run automatically via GitHub Actions:
- On pull requests that modify Terraform files
- On push to main branch
- Can be triggered manually via workflow dispatch

See `.github/workflows/terraform-tests.yml` for the CI configuration.

### Test Requirements

- Terraform >= 1.5.0 (for validation tests)
- Terraform >= 1.6.0 (for native test execution)

For detailed testing documentation, see [tests/README.md](tests/README.md).

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

**Pros**:
- Simple setup
- No external dependencies
- Fast for single-user development

**Cons**:
- No team collaboration
- No state locking
- Risk of state corruption
- No backup/versioning

### Remote State (Recommended for Production)

Remote state enables team collaboration, state locking, and backup/versioning. Three options are available:

#### Option 1: IONOS S3-Compatible Storage (Recommended)

Best for data sovereignty and integration with IONOS infrastructure.

**Step 1: Create S3 Bucket in IONOS Cloud**
```bash
# Using IONOS Cloud Console or API
# Create bucket: monadic-pipeline-terraform-state
# Region: de (Frankfurt)
```

**Step 2: Configure Backend in main.tf**

Uncomment and configure the backend block in `main.tf`:

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

**Step 3: Initialize with Backend**
```bash
export AWS_ACCESS_KEY_ID="your-ionos-s3-access-key"
export AWS_SECRET_ACCESS_KEY="your-ionos-s3-secret-key"

cd terraform
terraform init -migrate-state
```

**Cost**: ~€5-10/month for state storage

#### Option 2: Terraform Cloud (Free for Small Teams)

Best for teams wanting managed state with UI and additional features.

**Step 1: Create Terraform Cloud Account**
- Sign up at https://app.terraform.io

**Step 2: Configure Backend in main.tf**
```hcl
terraform {
  backend "remote" {
    organization = "your-org-name"
    
    workspaces {
      prefix = "monadic-pipeline-"
    }
  }
}
```

**Step 3: Initialize**
```bash
terraform login
terraform init
```

**Cost**: Free for up to 5 users

#### Option 3: Azure Blob Storage

Best if already using Azure services.

```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "terraform-state"
    storage_account_name = "Ouroborosstate"
    container_name       = "tfstate"
    key                  = "ionos.terraform.tfstate"
  }
}
```

### State Locking

State locking prevents concurrent modifications and state corruption.

**IONOS S3**: Use DynamoDB-compatible table or implement custom locking
**Terraform Cloud**: Built-in state locking
**Azure**: Built-in state locking

### Migrating State to Remote Backend

If you're currently using local state and want to migrate to remote backend:

**Step 1: Backup Current State**
```bash
cd terraform
cp terraform.tfstate terraform.tfstate.backup
cp terraform.tfstate.backup ~/terraform.tfstate.$(date +%Y%m%d-%H%M%S)
```

**Step 2: Configure Remote Backend**

Add or uncomment backend configuration in `main.tf` (see options above).

**Step 3: Migrate State**
```bash
terraform init -migrate-state
```

Terraform will prompt:
```
Do you want to copy existing state to the new backend?
  Pre-existing state was found while migrating the previous "local" backend to the
  newly configured "s3" backend. No existing state was found in the newly
  configured "s3" backend. Do you want to copy this state to the new "s3"
  backend? Enter "yes" to copy and "no" to start with an empty state.

  Enter a value: yes
```

**Step 4: Verify Migration**
```bash
# List state
terraform state list

# Verify outputs
terraform output

# Ensure local state is no longer used
ls -la terraform.tfstate*
```

**Step 5: Clean Up Local State (Optional)**
```bash
# After verifying remote state works
mv terraform.tfstate terraform.tfstate.migrated
mv terraform.tfstate.backup terraform.tfstate.backup.migrated
```

### State Backup and Recovery

**Automated Backups** (for IONOS S3):
```bash
# Enable versioning on S3 bucket
# This keeps historical versions of state files
```

**Manual Backup**:
```bash
# Pull current state
terraform state pull > terraform.tfstate.backup

# Store with timestamp
cp terraform.tfstate.backup ~/backups/terraform-$(date +%Y%m%d-%H%M%S).tfstate
```

**Recovery from Backup**:
```bash
# Push backup state to remote
terraform state push terraform.tfstate.backup

# Or initialize from local backup
cp terraform.tfstate.backup terraform.tfstate
terraform init -migrate-state
```

## CI/CD Integration

### GitHub Actions

The infrastructure can be managed automatically via GitHub Actions. See `.github/workflows/terraform-infrastructure.yml` for the workflow configuration.

#### Setting Up GitHub Actions Secrets

**Step 1: Navigate to Repository Secrets**

Go to: `https://github.com/PMeeske/Ouroboros/settings/secrets/actions`

Or navigate via: Settings → Secrets and variables → Actions → New repository secret

**Step 2: Add IONOS Cloud Credentials**

Choose one of two authentication methods:

**Option A: API Token (Recommended)**
1. Login to IONOS Cloud Console: https://dcd.ionos.com
2. Navigate to: User → API Tokens
3. Generate new token with appropriate permissions
4. Add secret in GitHub:
   - Name: `IONOS_ADMIN_TOKEN`
   - Value: `your-generated-token`

**Option B: Username/Password**
1. Add username secret:
   - Name: `IONOS_ADMIN_USERNAME`
   - Value: `your-ionos-username`
2. Add password secret:
   - Name: `IONOS_ADMIN_PASSWORD`
   - Value: `your-ionos-password`

**Step 3: Add State Storage Credentials (Optional)**

If using remote S3 backend:
1. Get IONOS S3 credentials from Cloud Console
2. Add secrets:
   - Name: `TF_STATE_ACCESS_KEY`
   - Value: `your-s3-access-key`
   - Name: `TF_STATE_SECRET_KEY`
   - Value: `your-s3-secret-key`

**Step 4: Verify Secrets**

The workflow will automatically detect which authentication method to use:
1. Go to Actions tab
2. Select "Terraform Infrastructure" workflow
3. Click "Run workflow"
4. Select environment: `dev`
5. Select action: `plan`
6. Run workflow

If credentials are correct, the plan should succeed.

#### Required Secrets Summary

| Secret Name | Required | Purpose | How to Obtain |
|-------------|----------|---------|---------------|
| `IONOS_ADMIN_TOKEN` | Yes* | IONOS API authentication | IONOS DCD → User → API Tokens |
| `IONOS_ADMIN_USERNAME` | Yes** | Alternative auth | Your IONOS username |
| `IONOS_ADMIN_PASSWORD` | Yes** | Alternative auth | Your IONOS password |
| `TF_STATE_ACCESS_KEY` | Optional | S3 backend access | IONOS Cloud → S3 → Access Keys |
| `TF_STATE_SECRET_KEY` | Optional | S3 backend access | IONOS Cloud → S3 → Access Keys |

\* Required if not using username/password  
\*\* Required if not using token

#### Workflow Triggers

**Manual Trigger**:
1. Go to Actions → Terraform Infrastructure
2. Click "Run workflow"
3. Select environment (dev/staging/production)
4. Select action (plan/apply/destroy)
5. Choose auto-approve (apply/destroy only)

**Automatic Trigger**:
- Triggered on push to `main` branch
- Only when files in `terraform/**` change
- Automatically runs `plan` for dev environment

#### Applying Infrastructure via CI/CD

**Example workflow dispatch**:
```yaml
name: Terraform Apply
on:
  workflow_dispatch:
    inputs:
      environment:
        type: choice
        options: [dev, staging, production]

jobs:
  terraform:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
        
      - name: Terraform Apply
        env:
          IONOS_TOKEN: ${{ secrets.IONOS_ADMIN_TOKEN }}
        run: |
          cd terraform
          terraform init
          terraform apply -var-file=environments/${{ inputs.environment }}.tfvars -auto-approve
```

#### Security Best Practices

1. **Use API Tokens**: Prefer `IONOS_ADMIN_TOKEN` over username/password
2. **Rotate Tokens**: Set expiry dates and rotate regularly
3. **Limit Permissions**: Use service accounts with minimal required permissions
4. **Environment Protection**: Enable environment protection rules for production
5. **Review Plans**: Always review plan output before applying
6. **Audit Logs**: Monitor workflow runs and infrastructure changes

#### Environment Protection Rules

For production safety, configure environment protection:

1. Go to Settings → Environments
2. Create environment: `IONOS`
3. Add protection rules:
   - Required reviewers: Add team members
   - Wait timer: 0 minutes
   - Deployment branches: Only `main`
4. Add environment secrets here instead of repository secrets

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
  time = "02:00:00+00:00"  # Format: HH:MM:SS+TZ (timezone offset required)
}
```

## Cost Estimates and Billing

### Monthly Cost Breakdown by Environment

All costs are estimates in EUR (€) based on IONOS Cloud pricing as of January 2025.

#### Development Environment

| Resource | Specification | Quantity | Unit Cost | Monthly Cost |
|----------|---------------|----------|-----------|--------------|
| **Kubernetes Nodes** | 2 cores, 8GB RAM | 2 nodes | €25/node | €50 |
| **Storage (HDD)** | 50GB per node | 100GB total | €0.04/GB | €4 |
| **Data Center** | Virtual DC | 1 | Included | €0 |
| **Container Registry** | Basic tier | 1 | €10 | €10 |
| **Networking** | LAN + Public IPs | 2 IPs | €2/IP | €4 |
| **Data Transfer** | 100GB egress | Est. | €0.05/GB | €5 |
| **Backup/Snapshots** | Optional | - | - | €0 |
| **Total** | | | | **€73** |

**Cost Optimization Tips for Dev**:
- Use HDD instead of SSD (included above)
- Scale down to 1 node during nights/weekends: Save €25/month
- Use node scheduling to shut down during non-working hours
- Delete unused container images regularly

#### Staging Environment

| Resource | Specification | Quantity | Unit Cost | Monthly Cost |
|----------|---------------|----------|-----------|--------------|
| **Kubernetes Nodes** | 4 cores, 16GB RAM | 2 nodes | €60/node | €120 |
| **Storage (SSD)** | 100GB per node | 200GB total | €0.10/GB | €20 |
| **Data Center** | Virtual DC | 1 | Included | €0 |
| **Container Registry** | Standard tier | 1 | €15 | €15 |
| **Networking** | LAN + Public IPs | 2 IPs | €2/IP | €4 |
| **Data Transfer** | 200GB egress | Est. | €0.05/GB | €10 |
| **Backup/Snapshots** | Weekly backups | 4 snapshots | €0.02/GB | €8 |
| **Total** | | | | **€177** |

**Cost Optimization Tips for Staging**:
- Schedule staging to match business hours
- Share registry with development environment
- Use autoscaling to handle varying loads
- Implement garbage collection for old images

#### Production Environment

| Resource | Specification | Quantity | Unit Cost | Monthly Cost |
|----------|---------------|----------|-----------|--------------|
| **Kubernetes Nodes** | 4 cores, 16GB RAM | 3 nodes | €60/node | €180 |
| **Storage (SSD)** | 100GB per node | 300GB total | €0.10/GB | €30 |
| **Data Center** | Virtual DC | 1 | Included | €0 |
| **Container Registry** | Premium tier | 1 | €20 | €20 |
| **Networking** | LAN + Public IPs | 3 IPs | €2/IP | €6 |
| **Data Transfer** | 500GB egress | Est. | €0.05/GB | €25 |
| **Load Balancer** | Optional | 1 | €15 | €15 |
| **Backup/Snapshots** | Daily backups | 7 snapshots | €0.02/GB | €14 |
| **Monitoring** | Cloud monitoring | Included | €0 | €0 |
| **Total** | | | | **€290** |

**High Availability Configuration** (+€120/month):
- Add 2 more nodes: +€120
- Cross-region backup: +€0
- **Total with HA**: €410/month

### Annual Cost Summary

| Environment | Monthly | Annual | Annual with Commitment* |
|-------------|---------|--------|-------------------------|
| Development | €73 | €876 | €700 (-20%) |
| Staging | €177 | €2,124 | €1,700 (-20%) |
| Production | €290 | €3,480 | €2,784 (-20%) |
| **All Environments** | **€540** | **€6,480** | **€5,184** |

\* IONOS offers discounts for 12-month commitments

### Cost Optimization Strategies

#### 1. Right-Sizing Resources

**Analyze Usage**:
```bash
# Get node utilization
kubectl top nodes

# Get pod resource usage
kubectl top pods --all-namespaces
```

**Adjust Node Sizes**:
- If CPU usage < 50%: Reduce cores
- If memory usage < 50%: Reduce RAM
- Update `*.tfvars` and apply changes

#### 2. Autoscaling Configuration

**Enable Horizontal Pod Autoscaling**:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: monadic-pipeline
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: monadic-pipeline
  minReplicas: 1
  maxReplicas: 5
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

**Configure Node Pool Autoscaling** (in tfvars):
```hcl
min_node_count = 2
max_node_count = 5
```

Potential savings: 30-50% during off-peak hours

#### 3. Storage Optimization

**Use Storage Classes**:
```yaml
# For logs (can use HDD)
storageClassName: ionos-hdd

# For databases (use SSD)
storageClassName: ionos-ssd
```

**Implement Retention Policies**:
- Container images: Keep only last 10 versions
- Logs: Retain for 30 days
- Backups: 7 daily, 4 weekly, 12 monthly

Potential savings: €10-20/month per environment

#### 4. Network Cost Optimization

**Minimize Data Transfer**:
- Use CDN for static assets
- Enable compression
- Cache frequently accessed data
- Keep services in same region

**Monitor Transfer Costs**:
```bash
# Check IONOS Cloud Console for data transfer metrics
# Set up alerts for unusual spikes
```

#### 5. Scheduled Scaling

**Development Environment Scaling Schedule**:
```bash
# Scale down during nights (7 PM - 7 AM)
# Scale down during weekends
# Potential savings: ~40% (€20-30/month)
```

**Implement using Kubernetes CronJobs**:
```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: scale-down-evening
spec:
  schedule: "0 19 * * 1-5"  # 7 PM weekdays
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: scale
            image: bitnami/kubectl
            command:
            - kubectl
            - scale
            - deployment/monadic-pipeline
            - --replicas=0
```

#### 6. Reserved Instances / Committed Use

IONOS offers discounts for long-term commitments:
- **12 months**: 20% discount
- **24 months**: 30% discount
- **36 months**: 40% discount

**When to commit**:
- Production environment (stable, predictable)
- After 3 months of usage validation
- When scaling patterns are understood

### Cost Monitoring and Alerts

**Set Up Budget Alerts**:
1. Monitor monthly spend in IONOS Cloud Console
2. Set up alerts at 50%, 75%, 90% of budget
3. Review detailed billing monthly

**Track Costs by Resource**:
```bash
# Use IONOS Cloud API to track costs
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  "https://api.ionos.com/cloudapi/v6/billing/summary"
```

**Cost Tracking Spreadsheet**:
- Track actual vs. estimated costs
- Identify cost trends
- Plan capacity changes

### Cost Comparison with Alternatives

| Provider | Dev | Staging | Production | Notes |
|----------|-----|---------|------------|-------|
| **IONOS Cloud** | €73 | €177 | €290 | Current pricing |
| AWS EKS | €120 | €250 | €450 | Higher costs, more features |
| Azure AKS | €100 | €220 | €400 | Competitive pricing |
| Google GKE | €110 | €230 | €420 | Premium pricing |
| DigitalOcean | €60 | €140 | €250 | Simpler, fewer features |

**IONOS Advantages**:
- Competitive pricing
- EU data sovereignty
- No hidden costs
- Simple pricing model
- Included monitoring

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

## Disaster Recovery Procedures

### Overview

This section covers disaster recovery scenarios and procedures for IONOS infrastructure managed by Terraform.

**Recovery Time Objective (RTO)**: 2-4 hours  
**Recovery Point Objective (RPO)**: 24 hours (daily backups)

### Backup Strategy

#### 1. Terraform State Backup

**Automated** (with remote backend):
```bash
# Enable S3 bucket versioning for automatic state versioning
# Keeps last 30 versions by default
```

**Manual Backup Schedule**:
```bash
#!/bin/bash
# Run daily via cron: 0 2 * * * /path/to/backup-state.sh

DATE=$(date +%Y%m%d-%H%M%S)
BACKUP_DIR="$HOME/terraform-backups"

mkdir -p "$BACKUP_DIR"

cd terraform
terraform state pull > "$BACKUP_DIR/terraform-state-$DATE.json"

# Keep last 30 backups
find "$BACKUP_DIR" -name "terraform-state-*.json" -mtime +30 -delete

echo "State backed up to: $BACKUP_DIR/terraform-state-$DATE.json"
```

#### 2. Kubernetes Cluster Backup

**Velero Installation**:
```bash
# Install Velero for cluster backup
kubectl create namespace velero

helm repo add vmware-tanzu https://vmware-tanzu.github.io/helm-charts
helm install velero vmware-tanzu/velero \
  --namespace velero \
  --set configuration.provider=aws \
  --set configuration.backupStorageLocation.bucket=monadic-pipeline-backups \
  --set configuration.backupStorageLocation.config.region=de \
  --set configuration.backupStorageLocation.config.s3Url=https://s3-eu-central-1.ionoscloud.com
```

**Backup Schedule**:
```bash
# Create daily backup schedule
velero schedule create daily-backup \
  --schedule="0 2 * * *" \
  --ttl 720h0m0s

# Create pre-upgrade backup
velero backup create pre-upgrade-$(date +%Y%m%d)
```

#### 3. Application Data Backup

**Database Backups** (if using databases):
```bash
# PostgreSQL backup
kubectl exec -n default postgresql-0 -- \
  pg_dump -U postgres mydb > backup-$(date +%Y%m%d).sql

# Upload to S3
aws s3 cp backup-$(date +%Y%m%d).sql \
  s3://monadic-pipeline-backups/database/ \
  --endpoint-url https://s3-eu-central-1.ionoscloud.com
```

**Volume Snapshots**:
```bash
# IONOS Cloud supports volume snapshots
# Schedule via IONOS Cloud Console or API
```

#### 4. Container Registry Backup

Registry is managed by IONOS with automatic backups. Additional backup:
```bash
# Export critical images
docker pull registry.ionos.com/adaptive-systems/monadic-pipeline:latest
docker save -o monadic-pipeline-backup.tar \
  registry.ionos.com/adaptive-systems/monadic-pipeline:latest

# Store in S3
aws s3 cp monadic-pipeline-backup.tar \
  s3://monadic-pipeline-backups/images/ \
  --endpoint-url https://s3-eu-central-1.ionoscloud.com
```

### Disaster Recovery Scenarios

#### Scenario 1: Accidental Infrastructure Deletion

**Detection**:
- Monitoring alerts trigger
- Infrastructure becomes unavailable
- Terraform state shows resources missing

**Recovery Steps**:

1. **Assess Damage**:
```bash
cd terraform
terraform state list  # Check what remains
terraform plan -var-file=environments/production.tfvars
```

2. **Restore from State Backup**:
```bash
# If state was deleted
terraform state pull > current-state.json  # Backup any existing state
terraform state push terraform-state-backup-YYYYMMDD.json
```

3. **Recreate Infrastructure**:
```bash
# Plan shows what needs to be recreated
terraform plan -var-file=environments/production.tfvars

# Apply to recreate
terraform apply -var-file=environments/production.tfvars
```

4. **Restore Application Data**:
```bash
# Restore Kubernetes resources
velero restore create --from-backup daily-backup-20250115020000

# Restore databases
kubectl exec -n default postgresql-0 -- \
  psql -U postgres mydb < backup-20250115.sql
```

**Estimated Recovery Time**: 2-3 hours

#### Scenario 2: Data Center Outage

**Detection**:
- IONOS status page reports outage
- Infrastructure unreachable
- API calls to Frankfurt region fail

**Recovery Steps**:

1. **Verify Outage Scope**:
```bash
# Check IONOS status
curl https://status.ionos.com/

# Test API connectivity
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/
```

2. **Activate DR Site** (if configured):
```bash
# Switch to backup region
cd terraform
terraform apply -var-file=environments/production-dr.tfvars \
  -var="location=de/txl"  # Berlin instead of Frankfurt
```

3. **Wait for IONOS Recovery**:
- IONOS SLA: 99.9% uptime (< 44 minutes downtime/month)
- Monitor status page for updates
- Infrastructure should auto-recover

4. **Verify Services**:
```bash
# Check cluster status
kubectl get nodes
kubectl get pods --all-namespaces

# Run health checks
./scripts/check-external-access.sh production
```

**Estimated Recovery Time**: Depends on IONOS (typically < 1 hour)

#### Scenario 3: Corrupted Terraform State

**Detection**:
- `terraform plan` shows unexpected changes
- `terraform validate` fails
- State file appears corrupted

**Recovery Steps**:

1. **Backup Current State**:
```bash
terraform state pull > corrupted-state.json
```

2. **Restore from Backup**:
```bash
# Use latest good backup
terraform state push terraform-state-backup-YYYYMMDD.json
```

3. **Verify State**:
```bash
terraform state list
terraform plan -var-file=environments/production.tfvars
```

4. **Manual State Repair** (if backup unavailable):
```bash
# Remove corrupted resources
terraform state rm module.corrupted_resource

# Re-import resources
terraform import module.kubernetes.ionoscloud_k8s_cluster.main <cluster-id>

# Verify
terraform plan -var-file=environments/production.tfvars
```

**Estimated Recovery Time**: 30 minutes - 2 hours

#### Scenario 4: Complete Infrastructure Loss

**Worst-case scenario**: Everything lost (state, infrastructure, backups)

**Recovery Steps**:

1. **Recreate from Git**:
```bash
git clone https://github.com/PMeeske/Ouroboros.git
cd Ouroboros/terraform
```

2. **Initialize Terraform**:
```bash
terraform init
```

3. **Deploy Fresh Infrastructure**:
```bash
terraform apply -var-file=environments/production.tfvars
```

4. **Restore Application**:
```bash
# Get kubeconfig
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
export KUBECONFIG=./kubeconfig.yaml

# Deploy application
cd ..
./scripts/deploy-ionos.sh monadic-pipeline production

# Restore data from off-site backups
velero restore create --from-backup off-site-backup-latest
```

**Estimated Recovery Time**: 4-6 hours

### DR Testing

**Monthly DR Test Schedule**:

```bash
#!/bin/bash
# dr-test.sh - Run monthly

echo "=== DR Test $(date) ==="

# 1. Test state backup/restore
echo "Testing state backup..."
terraform state pull > test-state.json
terraform state push test-state.json
echo "✓ State backup working"

# 2. Test infrastructure recreation
echo "Testing infrastructure plan..."
terraform plan -var-file=environments/staging.tfvars > /dev/null
echo "✓ Infrastructure plan works"

# 3. Test Velero backup
echo "Testing Velero backup..."
velero backup create dr-test-$(date +%Y%m%d)
velero backup describe dr-test-$(date +%Y%m%d)
echo "✓ Velero backup working"

# 4. Test application deployment
echo "Testing application deployment..."
kubectl apply -f k8s/deployment.yaml --dry-run=client
echo "✓ Application manifest valid"

echo "=== DR Test Complete ==="
```

### Recovery Validation Checklist

After any recovery:

- [ ] All Kubernetes nodes are Ready
- [ ] All pods are Running
- [ ] Application endpoints are accessible
- [ ] Database connections work
- [ ] External access validated (run `check-external-access.sh`)
- [ ] Monitoring and alerting operational
- [ ] Container registry accessible
- [ ] Application functionality tested
- [ ] Performance metrics normal
- [ ] Logs flowing correctly

## Rollback Procedures

### Overview

Rollback procedures for safely reverting infrastructure or application changes.

### Infrastructure Rollback

#### Rolling Back Terraform Changes

**Scenario**: Recent `terraform apply` caused issues

**Method 1: Revert to Previous State**

```bash
# 1. List state backups
ls -lh ~/terraform-backups/

# 2. Restore previous state
cd terraform
terraform state push ~/terraform-backups/terraform-state-YYYYMMDD-HHMMSS.json

# 3. Verify
terraform plan -var-file=environments/production.tfvars

# 4. Apply to revert infrastructure
terraform apply -var-file=environments/production.tfvars
```

**Method 2: Revert Git Changes**

```bash
# 1. Find the last good commit
git log --oneline terraform/

# 2. Checkout previous version
git checkout <commit-hash> -- terraform/

# 3. Plan changes
cd terraform
terraform plan -var-file=environments/production.tfvars

# 4. Apply rollback
terraform apply -var-file=environments/production.tfvars

# 5. Commit rollback
git add terraform/
git commit -m "Rollback infrastructure to commit <commit-hash>"
git push
```

**Method 3: Manual Resource Modification**

```bash
# For specific resource issues
terraform state list

# Remove problematic resource
terraform state rm module.problematic.resource

# Recreate with previous config
terraform apply -var-file=environments/production.tfvars -target=module.problematic.resource
```

#### Kubernetes Version Rollback

If Kubernetes upgrade fails:

```bash
# 1. Update tfvars to previous version
# Edit environments/production.tfvars
k8s_version = "1.27"  # Revert from 1.28

# 2. Apply rollback
terraform apply -var-file=environments/production.tfvars

# 3. Verify cluster
kubectl version
kubectl get nodes
```

**Note**: Kubernetes version downgrades are not supported. You may need to recreate the cluster.

#### Node Pool Rollback

```bash
# Rollback node pool changes
terraform apply -var-file=environments/production.tfvars \
  -target=module.kubernetes.ionoscloud_k8s_node_pool.main

# Verify nodes
kubectl get nodes
```

### Application Rollback

#### Using Kubernetes Deployment Rollback

```bash
# 1. Check deployment history
kubectl rollout history deployment/monadic-pipeline

# 2. View specific revision
kubectl rollout history deployment/monadic-pipeline --revision=2

# 3. Rollback to previous version
kubectl rollout undo deployment/monadic-pipeline

# 4. Rollback to specific revision
kubectl rollout undo deployment/monadic-pipeline --to-revision=2

# 5. Monitor rollback
kubectl rollout status deployment/monadic-pipeline
```

#### Using Container Registry Tags

```bash
# 1. List available tags
docker images | grep monadic-pipeline

# 2. Deploy previous version
kubectl set image deployment/monadic-pipeline \
  monadic-pipeline=registry.ionos.com/adaptive-systems/monadic-pipeline:v1.2.0

# 3. Verify rollback
kubectl rollout status deployment/monadic-pipeline
kubectl get pods
```

#### Using Helm Rollback (if using Helm)

```bash
# 1. List releases
helm list

# 2. View release history
helm history monadic-pipeline

# 3. Rollback to previous release
helm rollback monadic-pipeline

# 4. Rollback to specific revision
helm rollback monadic-pipeline 3
```

### Database Rollback

#### Schema Rollback

```bash
# If using database migration tools like Flyway or Liquibase

# Flyway rollback
flyway undo -configFiles=flyway.conf

# Or restore from backup
kubectl exec -n default postgresql-0 -- \
  psql -U postgres mydb < backup-before-change.sql
```

#### Data Rollback

```bash
# 1. Stop application
kubectl scale deployment/monadic-pipeline --replicas=0

# 2. Restore database
kubectl exec -n default postgresql-0 -- \
  psql -U postgres -c "DROP DATABASE mydb;"
kubectl exec -n default postgresql-0 -- \
  psql -U postgres -c "CREATE DATABASE mydb;"
kubectl exec -n default postgresql-0 -- \
  psql -U postgres mydb < backup-YYYYMMDD.sql

# 3. Restart application
kubectl scale deployment/monadic-pipeline --replicas=3
```

### Rollback Decision Matrix

| Change Type | Risk Level | Rollback Method | Estimated Time |
|-------------|-----------|-----------------|----------------|
| Application code | Low | Kubernetes rollout | 2-5 minutes |
| Container image | Low | Image tag change | 2-5 minutes |
| Environment variables | Low | ConfigMap/Secret update | 1-2 minutes |
| Infrastructure size | Medium | Terraform revert | 10-20 minutes |
| Kubernetes version | High | May require cluster rebuild | 1-2 hours |
| Database schema | High | Backup restoration | 15-60 minutes |
| Complete infrastructure | Critical | Full DR procedure | 2-4 hours |

### Rollback Validation

After any rollback:

```bash
# 1. Check infrastructure
terraform plan -var-file=environments/production.tfvars
# Output should show "No changes"

# 2. Check application
kubectl get deployments
kubectl get pods
kubectl logs -l app=monadic-pipeline --tail=50

# 3. Test functionality
curl -I https://your-app-endpoint.com/health

# 4. Check monitoring
# Verify metrics return to normal
# Check error rates

# 5. Run integration tests
./scripts/validate-deployment.sh production
```

### Preventing Need for Rollbacks

1. **Always test in dev/staging first**
2. **Use `terraform plan` before `apply`**
3. **Enable approval steps for production**
4. **Implement blue-green deployments**
5. **Use feature flags**
6. **Maintain comprehensive backups**
7. **Document all changes**

## Infrastructure Incident Runbook

### Incident Response Workflow

```
Detect → Assess → Communicate → Mitigate → Resolve → Document
```

### Severity Levels

| Level | Impact | Response Time | Examples |
|-------|--------|---------------|----------|
| **P0 - Critical** | Complete outage | < 15 minutes | Data center down, cluster unreachable |
| **P1 - High** | Major degradation | < 1 hour | Pod crashes, high error rates |
| **P2 - Medium** | Partial degradation | < 4 hours | Slow response times, some errors |
| **P3 - Low** | Minor issues | < 24 hours | Non-critical warnings, minor bugs |

### Common Incidents and Responses

#### Incident 1: Kubernetes Cluster Unreachable

**Symptoms**:
- `kubectl` commands timeout
- Application not responding
- Cannot access Kubernetes API

**Diagnosis**:
```bash
# Check IONOS status
curl https://status.ionos.com/

# Test API connectivity
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/datacenters

# Check cluster status
cd terraform
terraform output k8s_cluster_id

# Use IONOS Cloud Console
# Login to https://dcd.ionos.com
# Navigate to Managed Kubernetes → Your Cluster
```

**Resolution**:
1. Check IONOS Cloud status page
2. Verify network connectivity
3. Check kubeconfig is valid
4. Regenerate kubeconfig if needed:
   ```bash
   terraform output -raw k8s_kubeconfig > kubeconfig.yaml
   export KUBECONFIG=./kubeconfig.yaml
   kubectl get nodes
   ```
5. If cluster is stuck, contact IONOS support
6. If unrecoverable, trigger DR procedure

**Prevention**:
- Set up monitoring with alerts
- Use health check endpoints
- Maintain valid kubeconfig

#### Incident 2: Terraform State Locked

**Symptoms**:
- `terraform plan` fails with lock error
- Error: "Error locking state: Error acquiring the state lock"

**Diagnosis**:
```bash
# Check if another process is running
ps aux | grep terraform

# Check lock info
terraform force-unlock -help
```

**Resolution**:
```bash
# 1. Verify no terraform processes running
ps aux | grep terraform

# 2. Wait 10 minutes for automatic unlock

# 3. If still locked, force unlock (use with caution)
terraform force-unlock <lock-id>

# Lock ID is in the error message
```

**Prevention**:
- Use remote backend with proper locking
- Avoid concurrent terraform runs
- Use `-lock-timeout` parameter:
  ```bash
  terraform apply -lock-timeout=10m -var-file=environments/production.tfvars
  ```

#### Incident 3: Pod Failing to Start

**Symptoms**:
- Pods in CrashLoopBackOff or ImagePullBackOff
- Application unavailable or degraded

**Diagnosis**:
```bash
# Check pod status
kubectl get pods -n default

# Check events
kubectl describe pod <pod-name>

# Check logs
kubectl logs <pod-name> --previous

# Common causes:
# - ImagePullBackOff: Registry auth issue
# - CrashLoopBackOff: Application error
# - Pending: Resource constraints
```

**Resolution**:

For ImagePullBackOff:
```bash
# Verify registry credentials
kubectl get secret -n default

# Recreate registry secret
kubectl create secret docker-registry ionos-registry \
  --docker-server=registry.ionos.com \
  --docker-username=<username> \
  --docker-password=<token>
```

For CrashLoopBackOff:
```bash
# Check application logs
kubectl logs <pod-name> --previous

# Check resources
kubectl top pods
kubectl describe node

# Scale down if needed
kubectl scale deployment/monadic-pipeline --replicas=1
```

For Pending (insufficient resources):
```bash
# Check node resources
kubectl top nodes
kubectl describe nodes

# Scale up nodes (update tfvars)
cd terraform
# Edit environments/production.tfvars: node_count = 4
terraform apply -var-file=environments/production.tfvars
```

**Prevention**:
- Set appropriate resource requests/limits
- Monitor resource usage
- Configure horizontal pod autoscaling
- Use readiness/liveness probes

#### Incident 4: High Resource Usage

**Symptoms**:
- Nodes showing high CPU/memory
- Application slow or unresponsive
- OOMKilled pods

**Diagnosis**:
```bash
# Check node resources
kubectl top nodes

# Check pod resources
kubectl top pods --all-namespaces

# Check resource limits
kubectl describe pod <pod-name> | grep -A 10 Limits

# Check for resource-intensive pods
kubectl top pods --all-namespaces --sort-by=cpu
kubectl top pods --all-namespaces --sort-by=memory
```

**Resolution**:

Immediate:
```bash
# Scale up nodes
cd terraform
# Edit tfvars: node_count = 5
terraform apply -var-file=environments/production.tfvars

# Or scale down pods temporarily
kubectl scale deployment/resource-heavy-app --replicas=1
```

Long-term:
```bash
# Adjust resource requests/limits in deployment
kubectl set resources deployment/monadic-pipeline \
  --limits=cpu=2,memory=4Gi \
  --requests=cpu=1,memory=2Gi

# Enable horizontal pod autoscaling
kubectl autoscale deployment/monadic-pipeline \
  --min=2 --max=10 --cpu-percent=70
```

**Prevention**:
- Set appropriate resource requests/limits
- Monitor resource trends
- Use autoscaling
- Regular capacity planning

#### Incident 5: Certificate/Authentication Expired

**Symptoms**:
- "Unauthorized" errors
- "Certificate has expired" errors
- Cannot push to container registry

**Diagnosis**:
```bash
# Check kubeconfig expiry
kubectl config view --raw

# Check registry token
# Tokens expire after configured TTL

# Test registry access
docker login registry.ionos.com/<registry-name>
```

**Resolution**:

For kubeconfig:
```bash
# Regenerate from Terraform
cd terraform
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
export KUBECONFIG=./kubeconfig.yaml
kubectl get nodes
```

For registry token:
```bash
# Regenerate token via IONOS Cloud Console
# Or recreate via Terraform
cd terraform
terraform apply -var-file=environments/production.tfvars \
  -target=module.registry.ionoscloud_container_registry_token.main

# Update Kubernetes secret
kubectl create secret docker-registry ionos-registry \
  --docker-server=registry.ionos.com \
  --docker-username=<username> \
  --docker-password=<new-token> \
  --dry-run=client -o yaml | kubectl apply -f -
```

**Prevention**:
- Set token expiry to long duration (1 year)
- Monitor token expiry dates
- Automate token rotation
- Set calendar reminders for manual renewals

### Incident Response Checklist

**During Incident**:
- [ ] Acknowledge incident
- [ ] Assess severity
- [ ] Notify stakeholders (if P0 or P1)
- [ ] Start incident timeline/notes
- [ ] Begin diagnosis
- [ ] Implement mitigation
- [ ] Verify resolution
- [ ] Notify stakeholders of resolution

**After Incident**:
- [ ] Document incident in runbook
- [ ] Conduct postmortem (P0, P1)
- [ ] Identify root cause
- [ ] Implement preventive measures
- [ ] Update monitoring/alerts
- [ ] Update documentation
- [ ] Share lessons learned

### Escalation Path

1. **First Response**: On-call engineer
2. **Level 2**: Infrastructure lead
3. **Level 3**: Engineering manager
4. **Level 4**: IONOS Cloud support
5. **Level 5**: Executive team (for major outages)

### Communication Templates

**Incident Notification** (for P0/P1):
```
Subject: [P0/P1] Production Incident - <Brief Description>

We are currently experiencing a [P0/P1] incident affecting <service/component>.

Impact: <Description of user impact>
Start Time: <timestamp>
Current Status: <Investigating/Mitigating/Resolving>
ETA: <Estimated time to resolution>

We will provide updates every 30 minutes until resolved.

Next update: <timestamp>
```

**Resolution Notification**:
```
Subject: [RESOLVED] <Incident description>

The incident affecting <service/component> has been resolved.

Root Cause: <Brief description>
Resolution: <What was done>
Impact Duration: <Start time> to <End time>

A full postmortem will be conducted and shared within 48 hours.
```

### Contact Information

- **IONOS Cloud Support**: support@ionos.com
- **IONOS Emergency**: +49 721 17 407 117
- **IONOS Status Page**: https://status.ionos.com/
- **Internal Escalation**: [Add your team contacts]

### Monitoring and Alerting Setup

```bash
# Set up basic monitoring with kubectl
kubectl top nodes
kubectl top pods --all-namespaces

# Set up Prometheus (optional)
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack

# Configure alerts for:
# - Node CPU > 80%
# - Node Memory > 85%
# - Pod CrashLoopBackOff
# - Deployment replica mismatch
# - Certificate expiration < 30 days
```

## Related Documentation

- [IONOS Cloud API Documentation](https://api.ionos.com/docs/)
- [IONOS Terraform Provider](https://registry.terraform.io/providers/ionos-cloud/ionoscloud/latest/docs)
- [Ouroboros Deployment Guide](../docs/IONOS_DEPLOYMENT_GUIDE.md)
- [GitHub Actions Workflow](../.github/workflows/ionos-deploy.yml)

## Support

For issues or questions:
- **IONOS Cloud Support**: [https://www.ionos.com/help](https://www.ionos.com/help)
- **Terraform Issues**: [GitHub Issues](https://github.com/PMeeske/Ouroboros/issues)
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
**Documentation Version**: 2.0 - Complete IaC Implementation
