# Infrastructure Incident Runbook

## Quick Reference Guide

**For detailed procedures, see**: [terraform/README.md](../terraform/README.md)

This runbook provides quick access to common infrastructure incidents and their resolutions.

## Emergency Contacts

- **IONOS Cloud Support**: support@ionos.com
- **IONOS Emergency Hotline**: +49 721 17 407 117
- **IONOS Status Page**: https://status.ionos.com/

## Severity Levels

| Level | Response Time | Impact |
|-------|---------------|--------|
| **P0 - Critical** | < 15 min | Complete outage |
| **P1 - High** | < 1 hour | Major degradation |
| **P2 - Medium** | < 4 hours | Partial degradation |
| **P3 - Low** | < 24 hours | Minor issues |

## Quick Diagnosis Commands

```bash
# Check overall infrastructure status
./scripts/check-external-access.sh production

# Check Kubernetes cluster
kubectl get nodes
kubectl get pods --all-namespaces
kubectl top nodes

# Check Terraform state
cd terraform
terraform state list
terraform output

# Check IONOS API connectivity
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/
```

## Common Incidents - Quick Actions

### 🔴 P0: Cluster Unreachable

**Quick Fix**:
```bash
# Regenerate kubeconfig
cd terraform
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
export KUBECONFIG=./kubeconfig.yaml
kubectl get nodes
```

If fails → Check IONOS status page → Contact IONOS support

---

### 🔴 P0: Complete Infrastructure Down

**Emergency Recovery**:
```bash
# 1. Check IONOS status
curl https://status.ionos.com/

# 2. Verify credentials
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/

# 3. If credentials OK, check state
cd terraform
terraform state pull > emergency-backup.json

# 4. Plan recovery
terraform plan -var-file=environments/production.tfvars

# 5. If infrastructure missing, apply
terraform apply -var-file=environments/production.tfvars
```

Contact IONOS support if issue persists.

---

### 🟡 P1: Pods Failing (ImagePullBackOff)

**Quick Fix**:
```bash
# Check pod status
kubectl describe pod <pod-name>

# Recreate registry secret
kubectl delete secret ionos-registry
kubectl create secret docker-registry ionos-registry \
  --docker-server=registry.ionos.com \
  --docker-username=<username> \
  --docker-password=<token>

# Restart deployment
kubectl rollout restart deployment/monadic-pipeline
```

---

### 🟡 P1: Pods Failing (CrashLoopBackOff)

**Quick Fix**:
```bash
# Check logs
kubectl logs <pod-name> --previous

# Common causes:
# - Config error → Check ConfigMap/Secret
# - Resource limits → Scale up or adjust limits
# - Application bug → Rollback deployment

# Rollback if needed
kubectl rollout undo deployment/monadic-pipeline
kubectl rollout status deployment/monadic-pipeline
```

---

### 🟡 P1: High Resource Usage / OOMKilled

**Quick Fix**:
```bash
# Check resource usage
kubectl top nodes
kubectl top pods --all-namespaces

# Immediate: Scale up nodes
cd terraform
# Edit production.tfvars: node_count = 5
terraform apply -var-file=environments/production.tfvars

# Or scale down pods temporarily
kubectl scale deployment/resource-heavy-app --replicas=1
```

---

### 🟢 P2: Terraform State Locked

**Quick Fix**:
```bash
# 1. Check if terraform is running
ps aux | grep terraform

# 2. Wait 10 minutes for auto-unlock

# 3. If still locked, force unlock (CAUTION)
terraform force-unlock <lock-id>
```

---

### 🟢 P2: Certificate/Token Expired

**Quick Fix**:
```bash
# For kubeconfig expiry
cd terraform
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
export KUBECONFIG=./kubeconfig.yaml

# For registry token expiry
# 1. Regenerate in IONOS Console OR
cd terraform
terraform apply -var-file=environments/production.tfvars \
  -target=module.registry.ionoscloud_container_registry_token.main

# 2. Update Kubernetes secret
kubectl create secret docker-registry ionos-registry \
  --docker-server=registry.ionos.com \
  --docker-username=<username> \
  --docker-password=<new-token> \
  --dry-run=client -o yaml | kubectl apply -f -
```

---

## Rollback Procedures

### Application Rollback (Quick)

```bash
# Rollback to previous version
kubectl rollout undo deployment/monadic-pipeline

# Rollback to specific revision
kubectl rollout undo deployment/monadic-pipeline --to-revision=2

# Monitor
kubectl rollout status deployment/monadic-pipeline
```

### Infrastructure Rollback

```bash
# 1. Find backup state
ls -lh ~/terraform-backups/

# 2. Restore state
cd terraform
terraform state push ~/terraform-backups/terraform-state-YYYYMMDD.json

# 3. Apply
terraform apply -var-file=environments/production.tfvars
```

### Git-based Rollback

```bash
# 1. Find last good commit
git log --oneline terraform/

# 2. Checkout previous version
git checkout <commit-hash> -- terraform/

# 3. Apply
cd terraform
terraform apply -var-file=environments/production.tfvars
```

---

## Disaster Recovery

### Restore Infrastructure from Scratch

**Time estimate**: 2-4 hours

```bash
# 1. Clone repository
git clone https://github.com/PMeeske/MonadicPipeline.git
cd MonadicPipeline/terraform

# 2. Initialize Terraform
terraform init

# 3. Deploy infrastructure
terraform apply -var-file=environments/production.tfvars

# 4. Get kubeconfig
terraform output -raw k8s_kubeconfig > kubeconfig.yaml
export KUBECONFIG=./kubeconfig.yaml

# 5. Deploy application
cd ..
./scripts/deploy-ionos.sh monadic-pipeline production

# 6. Restore data (if using Velero)
velero restore create --from-backup latest-backup
```

### Restore from Velero Backup

```bash
# 1. List available backups
velero backup get

# 2. Restore from backup
velero restore create --from-backup <backup-name>

# 3. Monitor restore
velero restore describe <restore-name>
velero restore logs <restore-name>
```

---

## Health Checks

### Infrastructure Health

```bash
# Run comprehensive check
./scripts/check-external-access.sh production

# Individual checks
terraform output k8s_public_ips
terraform output registry_hostname
terraform output deployment_summary
```

### Application Health

```bash
# Check pods
kubectl get pods -n default

# Check deployments
kubectl get deployments

# Check services
kubectl get services

# Test application endpoint
curl -I https://your-app.com/health
```

### Resource Health

```bash
# Node resources
kubectl top nodes

# Pod resources
kubectl top pods --all-namespaces

# Storage
kubectl get pvc
kubectl get pv
```

---

## Monitoring Queries

### Recent Pod Failures

```bash
kubectl get events --all-namespaces --sort-by='.lastTimestamp' | grep -i error
```

### Resource Pressure

```bash
kubectl describe nodes | grep -A 5 "Allocated resources"
```

### Failed Pods

```bash
kubectl get pods --all-namespaces --field-selector status.phase!=Running,status.phase!=Succeeded
```

---

## Useful Commands Reference

### Terraform

```bash
# Validate configuration
terraform validate

# Plan changes
terraform plan -var-file=environments/production.tfvars

# Apply changes
terraform apply -var-file=environments/production.tfvars

# Show state
terraform state list
terraform state show <resource>

# Import existing resource
terraform import <resource> <id>
```

### Kubernetes

```bash
# Get resources
kubectl get nodes
kubectl get pods --all-namespaces
kubectl get deployments
kubectl get services

# Describe resources
kubectl describe pod <pod-name>
kubectl describe node <node-name>

# Logs
kubectl logs <pod-name>
kubectl logs <pod-name> --previous
kubectl logs -f <pod-name>

# Scale
kubectl scale deployment/<name> --replicas=3

# Restart
kubectl rollout restart deployment/<name>

# Rollback
kubectl rollout undo deployment/<name>
```

### IONOS Cloud API

```bash
# Test connectivity
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/

# List datacenters
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/datacenters

# Check K8s clusters
curl -H "Authorization: Bearer $IONOS_TOKEN" \
  https://api.ionos.com/cloudapi/v6/k8s
```

---

## Incident Response Workflow

```
1. DETECT
   ↓
2. ASSESS SEVERITY (P0/P1/P2/P3)
   ↓
3. NOTIFY (if P0 or P1)
   ↓
4. DIAGNOSE (use Quick Diagnosis Commands)
   ↓
5. MITIGATE (use Quick Actions above)
   ↓
6. RESOLVE
   ↓
7. VERIFY (run Health Checks)
   ↓
8. DOCUMENT
   ↓
9. POSTMORTEM (for P0/P1)
```

---

## Postmortem Template

After resolving P0 or P1 incidents:

```markdown
# Incident Postmortem - <Date>

## Summary
Brief description of the incident

## Timeline
- **Start**: <timestamp>
- **Detection**: <timestamp>
- **Mitigation**: <timestamp>
- **Resolution**: <timestamp>
- **Duration**: X hours

## Impact
- Services affected: 
- Users affected:
- Data loss: Yes/No

## Root Cause
What caused the incident

## Resolution
What was done to resolve it

## Preventive Measures
- [ ] Action item 1
- [ ] Action item 2
- [ ] Action item 3

## Lessons Learned
What we learned from this incident
```

---

## Additional Resources

- **Full Documentation**: [terraform/README.md](../terraform/README.md)
- **Quick Start**: [docs/IONOS_IAC_QUICKSTART.md](IONOS_IAC_QUICKSTART.md)
- **Deployment Guide**: [docs/IONOS_DEPLOYMENT_GUIDE.md](IONOS_DEPLOYMENT_GUIDE.md)
- **IONOS Cloud Docs**: https://api.ionos.com/docs/
- **Terraform Provider**: https://registry.terraform.io/providers/ionos-cloud/ionoscloud/latest/docs

---

**Last Updated**: January 2025  
**Version**: 1.0
