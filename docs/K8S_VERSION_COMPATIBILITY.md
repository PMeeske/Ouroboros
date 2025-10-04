# Kubernetes Version Compatibility for IONOS Cloud

## Current Configuration

**Version**: 1.29 (Updated January 2025)

All environments now use Kubernetes 1.29:
- Development: `terraform/environments/dev.tfvars`
- Staging: `terraform/environments/staging.tfvars`
- Production: `terraform/environments/production.tfvars`

## Version History

| Date | Version | Reason |
|------|---------|--------|
| January 2025 | 1.29 | Upgraded from 1.28 for better IONOS support and security |
| Pre-2025 | 1.28 | Initial configuration |

## Supported Versions

### IONOS Cloud Kubernetes Support (2025)

IONOS Cloud typically supports the last 3-4 minor Kubernetes versions:

| Version | Release Date | IONOS Support | Recommendation |
|---------|-------------|---------------|----------------|
| 1.32 | Dec 2024 | ✅ Supported | ⚠️ Test in dev first |
| 1.31 | Aug 2024 | ✅ Supported | ✅ Safe for all environments |
| 1.30 | Apr 2024 | ✅ Supported | ✅ Safe for all environments |
| **1.29** | Dec 2023 | ✅ Supported | ✅ **Current choice** |
| 1.28 | Aug 2023 | ⚠️ Deprecated | ❌ Upgrade required |

## Version Selection Rationale

### Why Kubernetes 1.29?

1. **Stability**: Released December 2023, well-tested in production
2. **Support Window**: ~14 months of upstream support
3. **IONOS Compatibility**: Fully supported by IONOS Cloud
4. **Security**: Receives regular security patches
5. **Feature Set**: Includes modern Kubernetes features without bleeding edge risk

### When to Upgrade

Upgrade to a newer version when:
- Current version approaches end-of-life (< 6 months remaining)
- Security vulnerabilities require newer version
- New features are needed for applications
- IONOS recommends or requires upgrade

Recommended upgrade cadence: **Quarterly review, upgrade annually**

## Compatibility Validation

### Terraform Validation

The configuration includes built-in validation:

```hcl
validation {
  condition     = can(regex("^1\\.(2[9-9]|3[0-9])(\\.\\d+)?$", var.k8s_version))
  error_message = "Kubernetes version must be 1.29 or higher..."
}
```

This prevents accidental use of deprecated versions.

### Manifest API Versions

All Kubernetes manifests use stable API versions:

| Resource Type | API Version | K8s Compatibility |
|--------------|-------------|-------------------|
| Deployment | apps/v1 | 1.9+ (stable) |
| StatefulSet | apps/v1 | 1.9+ (stable) |
| Service | v1 | 1.0+ (stable) |
| ConfigMap | v1 | 1.2+ (stable) |
| Secret | v1 | 1.0+ (stable) |
| Namespace | v1 | 1.0+ (stable) |
| Ingress | networking.k8s.io/v1 | 1.19+ (stable) |

**Result**: All manifests compatible with Kubernetes 1.19+, no issues with 1.29+

### Application Compatibility

| Component | Deployment Type | K8s Features Used | Min K8s Version |
|-----------|----------------|-------------------|-----------------|
| Ollama | Deployment | Standard pods, volumes | 1.9+ |
| Qdrant | StatefulSet | Persistent volumes | 1.9+ |
| WebAPI | Deployment | Standard pods, services | 1.9+ |
| Jaeger | Deployment | Standard pods | 1.9+ |

**Result**: No version-specific features, compatible with all supported versions

## Upgrade Procedures

### Pre-Upgrade Checklist

- [ ] Review [IONOS Cloud release notes](https://docs.ionos.com/cloud/managed-services/managed-kubernetes/release-notes)
- [ ] Check for deprecated API versions in manifests
- [ ] Review application dependencies for compatibility
- [ ] Ensure backup and rollback plan exists
- [ ] Schedule maintenance window
- [ ] Notify stakeholders

### Upgrade Steps

#### 1. Test in Development

```bash
# Update dev environment
vim terraform/environments/dev.tfvars
# Change: k8s_version = "1.30"

# Apply changes
cd terraform
terraform plan -var-file=environments/dev.tfvars
terraform apply -var-file=environments/dev.tfvars

# Verify cluster
kubectl get nodes
kubectl get pods -A
```

#### 2. Validate Application

```bash
# Deploy application
kubectl apply -f k8s/

# Test functionality
# - Check all pods are running
# - Test API endpoints
# - Verify data persistence
# - Check logs for errors
```

#### 3. Upgrade Staging

Repeat steps for staging environment with `staging.tfvars`

#### 4. Upgrade Production

```bash
# Update production environment
vim terraform/environments/production.tfvars
# Change: k8s_version = "1.30"

# Plan with extra caution
terraform plan -var-file=environments/production.tfvars

# Review plan carefully
# Apply during maintenance window
terraform apply -var-file=environments/production.tfvars
```

### Rollback Procedure

If issues occur:

```bash
# Revert to previous version
vim terraform/environments/[env].tfvars
# Change back to previous version

# Apply rollback
terraform apply -var-file=environments/[env].tfvars
```

**Note**: IONOS may not support downgrading. Test thoroughly before production upgrade.

## Version Validation Script

To check current Kubernetes version across all environments:

```bash
#!/bin/bash
# Check configured Kubernetes versions

echo "=== Kubernetes Version Check ==="
echo ""

echo "Default (terraform/variables.tf):"
grep 'default.*=' terraform/variables.tf | grep -A 1 'k8s_version'

echo ""
echo "Development:"
grep 'k8s_version' terraform/environments/dev.tfvars

echo ""
echo "Staging:"
grep 'k8s_version' terraform/environments/staging.tfvars

echo ""
echo "Production:"
grep 'k8s_version' terraform/environments/production.tfvars

echo ""
echo "=== Manifest API Versions ==="
grep -h "apiVersion:" k8s/*.yaml | sort | uniq -c
```

## Troubleshooting

### Issue: Terraform validation fails

```
Error: Kubernetes version must be 1.29 or higher
```

**Solution**: Update `k8s_version` in environment file to 1.29 or newer

### Issue: Cluster upgrade fails

**Symptoms**: Terraform apply fails during version change

**Possible Causes**:
1. Invalid version number
2. IONOS doesn't support requested version
3. Cluster in invalid state

**Solutions**:
1. Check [IONOS supported versions](https://docs.ionos.com/cloud/managed-services/managed-kubernetes)
2. Ensure version is valid format (e.g., "1.29", "1.29.5")
3. Check cluster status in IONOS console
4. Review Terraform state for conflicts

### Issue: Pods fail after upgrade

**Symptoms**: Pods in CrashLoopBackOff or ImagePullBackOff

**Possible Causes**:
1. API version deprecation
2. RBAC changes
3. Resource limits changes

**Solutions**:
1. Check pod logs: `kubectl logs <pod-name>`
2. Describe pod: `kubectl describe pod <pod-name>`
3. Review Kubernetes changelog for breaking changes
4. Update manifests if API versions deprecated

## Resources

### Official Documentation
- [IONOS Managed Kubernetes](https://docs.ionos.com/cloud/managed-services/managed-kubernetes)
- [Kubernetes Release Notes](https://kubernetes.io/releases/)
- [Kubernetes Deprecation Policy](https://kubernetes.io/docs/reference/using-api/deprecation-policy/)

### Internal Documentation
- [IONOS IAC Guide](./IONOS_IAC_GUIDE.md)
- [IONOS Deployment Guide](./IONOS_DEPLOYMENT_GUIDE.md)
- [Infrastructure Dependencies](./INFRASTRUCTURE_DEPENDENCIES.md)

## Maintenance Schedule

| Activity | Frequency | Responsible |
|----------|-----------|-------------|
| Review IONOS K8s versions | Monthly | DevOps Team |
| Check for security updates | Weekly | Security Team |
| Plan version upgrade | Quarterly | DevOps Team |
| Execute upgrade (dev) | As needed | DevOps Team |
| Execute upgrade (staging) | As needed | DevOps Team |
| Execute upgrade (production) | Annually | DevOps Team + Approval |

## Change Log

### 2025-01-XX
- **Action**: Upgraded from Kubernetes 1.28 to 1.29
- **Reason**: Version 1.28 approaching EOL, improved security and IONOS support
- **Environments**: All (dev, staging, production)
- **Validation**: Added Terraform validation for minimum version 1.29
- **Testing**: Manifests validated for compatibility with 1.29+

---

**Last Updated**: January 2025  
**Next Review**: April 2025  
**Maintainer**: DevOps Team
