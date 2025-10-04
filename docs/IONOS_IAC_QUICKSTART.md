# IONOS Infrastructure as Code - Quick Start Guide

Get your MonadicPipeline infrastructure up and running in 5 minutes!

## Prerequisites (1 minute)

1. **IONOS Cloud Account**: [Sign up](https://cloud.ionos.com) if you don't have one
2. **Terraform**: Install it
   ```bash
   # macOS
   brew install terraform
   
   # Linux
   wget https://releases.hashicorp.com/terraform/1.5.0/terraform_1.5.0_linux_amd64.zip
   unzip terraform_1.5.0_linux_amd64.zip
   sudo mv terraform /usr/local/bin/
   ```

3. **IONOS API Token**: Generate from [IONOS Cloud Console](https://dcd.ionos.com)
   - Go to User Menu → API Credentials
   - Click "Generate Token"
   - Copy the token (you won't see it again!)

## Quick Deployment (4 minutes)

### Method 1: Using the Helper Script (Easiest)

```bash
# Step 1: Set your IONOS token
export IONOS_TOKEN="your-token-here"

# Step 2: Initialize Terraform
./scripts/manage-infrastructure.sh init

# Step 3: Preview what will be created
./scripts/manage-infrastructure.sh plan dev

# Step 4: Create the infrastructure
./scripts/manage-infrastructure.sh apply dev

# Step 5: Get the kubeconfig
./scripts/manage-infrastructure.sh kubeconfig dev

# Step 6: Use kubectl
export KUBECONFIG=./terraform/kubeconfig-dev.yaml
kubectl get nodes
```

### Method 2: Using Terraform Directly

```bash
# Step 1: Set your IONOS token
export IONOS_TOKEN="your-token-here"

# Step 2: Go to terraform directory
cd terraform

# Step 3: Initialize
terraform init

# Step 4: Create infrastructure
terraform apply -var-file=environments/dev.tfvars

# Step 5: Get kubeconfig
terraform output -raw k8s_kubeconfig > kubeconfig-dev.yaml
export KUBECONFIG=./kubeconfig-dev.yaml
kubectl get nodes
```

## What Gets Created

Your infrastructure includes:

✅ **Data Center**: Virtual data center in Frankfurt (`de/fra`)  
✅ **Kubernetes Cluster**: 2-node cluster with 8GB RAM per node  
✅ **Container Registry**: Private Docker registry  
✅ **Storage Volumes**: 70GB total (HDD for dev)  
✅ **Virtual Network**: LAN for cluster networking  

**Estimated Cost**: €50-80/month for dev environment

## Next Steps

### Deploy the Application

```bash
# Step 1: Configure kubectl
export KUBECONFIG=./terraform/kubeconfig-dev.yaml

# Step 2: Deploy MonadicPipeline
./scripts/deploy-ionos.sh monadic-pipeline

# Step 3: Check deployment
kubectl get pods -n monadic-pipeline
```

### View Infrastructure Details

```bash
# All outputs
./scripts/manage-infrastructure.sh output dev

# Or with Terraform directly
cd terraform
terraform output

# Get registry hostname
terraform output registry_hostname
```

### Upgrade to Production

When ready for production:

```bash
# Review production configuration
cat terraform/environments/production.tfvars

# Apply production infrastructure
./scripts/manage-infrastructure.sh apply production

# Get production kubeconfig
./scripts/manage-infrastructure.sh kubeconfig production
```

## Common Tasks

### Scale Kubernetes Nodes

Edit `terraform/environments/dev.tfvars`:
```hcl
node_count = 3  # Change from 2 to 3
```

Apply changes:
```bash
./scripts/manage-infrastructure.sh apply dev
```

### Upgrade Kubernetes Version

Edit `terraform/environments/dev.tfvars`:
```hcl
k8s_version = "1.29"  # Upgrade from 1.28
```

Apply changes:
```bash
./scripts/manage-infrastructure.sh apply dev
```

### Clean Up (Destroy Infrastructure)

```bash
./scripts/manage-infrastructure.sh destroy dev
```

## Troubleshooting

### "Terraform not found"

```bash
# macOS
brew install terraform

# Linux
wget https://releases.hashicorp.com/terraform/1.5.0/terraform_1.5.0_linux_amd64.zip
unzip terraform_1.5.0_linux_amd64.zip
sudo mv terraform /usr/local/bin/
```

### "Authentication failed"

```bash
# Check if token is set
echo $IONOS_TOKEN

# Verify token works
curl -H "Authorization: Bearer $IONOS_TOKEN" https://api.ionos.com/cloudapi/v6/
```

### "Resource already exists"

If you have existing manual infrastructure, import it:

```bash
cd terraform
terraform import module.datacenter.ionoscloud_datacenter.main <datacenter-id>
```

See [Migration Guide](IONOS_IAC_GUIDE.md#migration-from-manual-setup) for details.

## GitHub Actions Automation

### Setup Secrets

1. Go to your GitHub repository
2. Navigate to Settings → Secrets and variables → Actions
3. Add secret: `IONOS_ADMIN_TOKEN` with your IONOS token

### Run Workflow

1. Go to Actions tab
2. Select "Terraform Infrastructure" workflow
3. Click "Run workflow"
4. Choose environment and action

## Getting Help

- **Full Guide**: [IONOS IaC Guide](IONOS_IAC_GUIDE.md)
- **Terraform Docs**: [terraform/README.md](../terraform/README.md)
- **IONOS Support**: [https://www.ionos.com/help](https://www.ionos.com/help)
- **Issues**: [GitHub Issues](https://github.com/PMeeske/MonadicPipeline/issues)

## Environment Comparison

| Feature | Dev | Staging | Production |
|---------|-----|---------|------------|
| Nodes | 2 | 2 | 3 (autoscaling) |
| CPU/RAM | 2 cores, 8GB | 4 cores, 16GB | 4 cores, 16GB |
| Storage | HDD | SSD | SSD |
| Cost/month | €50-80 | €100-150 | €150-250 |
| Use Case | Development | Pre-prod testing | Live workloads |

---

**Ready to deploy?** Start with: `./scripts/manage-infrastructure.sh apply dev`

**Questions?** See the [full documentation](IONOS_IAC_GUIDE.md)
