# External Accessibility Validation for IONOS Infrastructure

## Overview

This document describes the new external accessibility validation capabilities added to the Ouroboros Terraform infrastructure.

## What Was Added

### 1. Enhanced Terraform Outputs

#### Kubernetes Module (`terraform/modules/kubernetes/outputs.tf`)

Added the following outputs to expose external accessibility information:

- **`public_ips`**: Public IP addresses assigned to the Kubernetes node pool
- **`api_subnet_allow_list`**: List of allowed subnets for Kubernetes API access
- **`cluster_state`**: Current state of the Kubernetes cluster (ACTIVE/INACTIVE/etc.)
- **`node_pool_state`**: Current state of the node pool (ACTIVE/INACTIVE/etc.)

#### Main Outputs (`terraform/outputs.tf`)

Added comprehensive outputs at the root level:

- **`lan_name`**: Name of the created LAN
- **`lan_public`**: Whether the LAN is public or private
- **`k8s_public_ips`**: Public IPs of Kubernetes nodes for external access
- **`k8s_api_subnet_allow_list`**: Allowed subnets for Kubernetes API access
- **`k8s_cluster_state`**: Current state of the Kubernetes cluster
- **`k8s_node_pool_state`**: Current state of the node pool
- **`external_access_info`**: A comprehensive summary object containing all external access information

### 2. External Access Validation Script

**Location**: `scripts/check-external-access.sh`

A comprehensive script that validates the external accessibility of deployed IONOS infrastructure.

## Usage

### Checking External Accessibility

After deploying infrastructure with Terraform, run:

```bash
./scripts/check-external-access.sh [environment]
```

**Examples:**

```bash
# Check development environment
./scripts/check-external-access.sh dev

# Check production infrastructure
./scripts/check-external-access.sh production
```

### What the Script Checks

The script performs the following validation checks:

1. **Prerequisites**
   - Terraform installation
   - curl availability (for connectivity tests)
   - kubectl availability (for Kubernetes tests)

2. **Terraform State**
   - Verifies that Terraform state exists
   - Confirms infrastructure is deployed

3. **Container Registry**
   - Registry hostname and location
   - HTTP connectivity test (if curl is available)
   - Authentication requirements

4. **Kubernetes Cluster**
   - Cluster name and state (should be ACTIVE)
   - Node pool state (should be ACTIVE)
   - Public IP addresses assigned to nodes
   - IP reachability tests

5. **API Access Configuration**
   - Subnet allow list for API access
   - Security configuration review

6. **Network Configuration**
   - LAN public/private status
   - Recommendations for external access

7. **Kubeconfig and kubectl Access**
   - Kubeconfig file existence
   - Cluster connectivity via kubectl
   - Active node count

### Output Interpretation

The script provides color-coded output:

- **✓ Green**: Check passed successfully
- **⚠ Yellow**: Warning - may require attention
- **✗ Red**: Check failed - action required

### Exit Codes

- **0**: All checks passed (or only warnings)
- **1**: One or more critical checks failed

## Complete Workflow

### 1. Deploy Infrastructure

```bash
# Initialize Terraform
./scripts/manage-infrastructure.sh init

# Plan deployment
./scripts/manage-infrastructure.sh plan dev

# Apply infrastructure
./scripts/manage-infrastructure.sh apply dev

# Get kubeconfig
./scripts/manage-infrastructure.sh kubeconfig dev
```

### 2. Validate External Accessibility

```bash
# Check external access
./scripts/check-external-access.sh dev
```

### 3. Review Terraform Outputs

You can also view all outputs directly:

```bash
cd terraform
terraform output

# Or get specific outputs
terraform output external_access_info
terraform output k8s_public_ips
terraform output registry_hostname
```

### 4. Access the Infrastructure

Once validation passes:

```bash
# Use kubeconfig
export KUBECONFIG=terraform/kubeconfig-dev.yaml
kubectl get nodes

# Check cluster info
kubectl cluster-info

# Deploy applications
kubectl apply -f k8s/
```

## Common Scenarios

### Private LAN Configuration

If the LAN is private (not public):

```
⚠ LAN is private: monadic-pipeline-lan-dev
ℹ Private LANs require VPN or bastion host for external access
```

**Solution**: This is expected for secure environments. Set up:
- VPN connection to IONOS datacenter
- Bastion host for SSH access
- IONOS Cloud VPN service

### No Public IPs

If no public IPs are assigned:

```
⚠ No public IPs assigned to nodes
ℹ Nodes may be using private IPs only
```

**Solution**: 
- For development: This is acceptable if you're using VPN/bastion access
- For production: Consider adding public IPs for external LoadBalancer services
- Update `terraform/environments/*.tfvars` to include public IPs if needed

### Cluster Not Active

If cluster state is not ACTIVE:

```
⚠ Cluster State: PROVISIONING (not ACTIVE)
```

**Solution**: Wait for provisioning to complete. This can take 5-15 minutes.

```bash
# Check again after a few minutes
./scripts/check-external-access.sh dev
```

### Registry Not Accessible

If registry connectivity test fails:

```
⚠ Registry may not be accessible (HTTP check failed)
ℹ This might be expected if registry requires authentication
```

**Solution**: This is usually expected. The registry requires authentication:

```bash
# Login to registry
docker login <registry-hostname>

# Test with proper credentials
curl -u username:password https://<registry-hostname>
```

## Troubleshooting

### No Terraform State Found

```
✗ No Terraform state found
ℹ Infrastructure may not be deployed yet.
ℹ Run: ./scripts/manage-infrastructure.sh apply dev
```

**Solution**: Deploy the infrastructure first using the management script.

### Cannot Access Cluster via kubectl

```
⚠ Cannot access cluster via kubectl (may require network access)
ℹ Try: export KUBECONFIG=terraform/kubeconfig-dev.yaml && kubectl get nodes
```

**Possible causes**:
1. Kubeconfig not exported: `export KUBECONFIG=terraform/kubeconfig-dev.yaml`
2. Network access blocked: Check firewall rules and API subnet allow list
3. Cluster not ready: Wait for cluster to become ACTIVE

### Failed to Get Terraform Outputs

```
✗ Failed to get Terraform outputs
ℹ Try running: terraform refresh -var-file=environments/dev.tfvars
```

**Solution**: Refresh the Terraform state:

```bash
cd terraform
terraform refresh -var-file=environments/dev.tfvars
```

## Integration with CI/CD

The validation script can be integrated into CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Validate External Access
  run: |
    ./scripts/check-external-access.sh production
  env:
    IONOS_TOKEN: ${{ secrets.IONOS_TOKEN }}
```

## Best Practices

1. **Run After Every Deployment**: Always validate external access after applying infrastructure changes
2. **Document Custom Configurations**: If you use private LANs or custom network setups, document the access procedures
3. **Security First**: Review the API subnet allow list to ensure only authorized networks can access the cluster
4. **Monitor State**: Use the state outputs to monitor cluster and node pool health
5. **Automate Validation**: Include the validation script in deployment pipelines

## Related Documentation

- [Terraform IaC Guide](IONOS_IAC_GUIDE.md)
- [Terraform README](../terraform/README.md)
- [IONOS Deployment Guide](IONOS_DEPLOYMENT_GUIDE.md)
- [Scripts README](../scripts/README.md)

## Summary

The new external accessibility validation features provide:

1. **Comprehensive Outputs**: All necessary information to understand external access configuration
2. **Automated Validation**: Script-based checking of accessibility
3. **Clear Feedback**: Color-coded output with actionable recommendations
4. **Troubleshooting Guidance**: Built-in help for common issues

These additions ensure that infrastructure is not only deployed but also properly accessible for application deployment and operations.
