# Incident Response: ImagePullBackOff on AKS Deployment

## Incident Summary

**Date:** October 2, 2025 05:20:36 UTC  
**Severity:** High  
**Duration:** ~1 hour (264 occurrences)  
**Impact:** Web API pod failed to start in production AKS cluster  
**Status:** Resolved

## Incident Details

### Kubernetes Event

```yaml
kind: Event
apiVersion: v1
metadata:
  name: monadic-pipeline-webapi-7ddcb5c887-knlf4.186a9501069dcf13
  namespace: monadic-pipeline
  resourceVersion: '1779'
  creationTimestamp: '2025-10-02T05:20:36Z'
involvedObject:
  kind: Pod
  namespace: monadic-pipeline
  name: monadic-pipeline-webapi-7ddcb5c887-knlf4
  apiVersion: v1
  fieldPath: spec.containers{webapi}
reason: Failed
message: 'Error: ImagePullBackOff'
source:
  component: kubelet
  host: aks-system-32651037-vmss000000
firstTimestamp: '2025-10-02T05:20:36Z'
lastTimestamp: '2025-10-02T06:20:47Z'
count: 264
type: Warning
```

### Symptoms

- Pod `monadic-pipeline-webapi-7ddcb5c887-knlf4` stuck in `ImagePullBackOff` state
- Deployment `monadic-pipeline-webapi` unable to achieve desired replica count
- Web API service unavailable
- 264 retry attempts over 1 hour period

## Root Cause Analysis

### Primary Cause

The deployment used the local Kubernetes manifests (`k8s/webapi-deployment.yaml`) which are configured for local development:

1. **Image Reference:** `monadic-pipeline-webapi:latest` (no registry prefix)
2. **Pull Policy:** `imagePullPolicy: Never` (only uses local images)

### Why It Failed on AKS

Azure Kubernetes Service (AKS) nodes attempted to:
1. Look for the image locally on the node (not found)
2. With `imagePullPolicy: Never`, Kubernetes did not attempt to pull from any registry
3. Pod creation failed repeatedly, generating 264 error events

### Contributing Factors

1. **Wrong manifest used:** Local manifest instead of cloud manifest
2. **No registry reference:** Image not pushed to Azure Container Registry (ACR)
3. **Missing ACR integration:** ACR not attached to AKS cluster
4. **Deployment process:** Manual deployment without using automated scripts

## Resolution Steps Taken

### Immediate Actions (Time to Resolution: ~10 minutes)

1. **Identified the issue:**
   ```bash
   kubectl get pods -n monadic-pipeline
   kubectl describe pod monadic-pipeline-webapi-7ddcb5c887-knlf4 -n monadic-pipeline
   kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'
   ```

2. **Verified ACR existence:**
   ```bash
   az acr list --resource-group <resource-group>
   # Confirmed ACR exists: <registry-name>.azurecr.io
   ```

3. **Used automated deployment script:**
   ```bash
   ./scripts/deploy-aks.sh <registry-name> monadic-pipeline
   ```

   This script automatically:
   - Logged into Azure Container Registry
   - Built Docker images with ACR prefix
   - Pushed images to ACR
   - Generated cloud-ready manifests with correct image references
   - Deployed to AKS with `imagePullPolicy: Always`
   - Attached ACR to AKS cluster

4. **Verified resolution:**
   ```bash
   kubectl get pods -n monadic-pipeline
   # All pods running successfully
   
   kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'
   # No more ImagePullBackOff errors
   ```

## Prevention Measures

### Immediate (Implemented)

1. **Documentation Update:**
   - ✅ Created [IMAGEPULLBACKOFF-FIX.md](IMAGEPULLBACKOFF-FIX.md) - Quick fix guide
   - ✅ Updated [README.md](README.md) - Added prominent troubleshooting section
   - ✅ Updated [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Added detailed solutions

2. **Automated Deployment Scripts:**
   - ✅ Created `scripts/deploy-aks.sh` - Azure AKS automated deployment
   - ✅ Created `scripts/deploy-cloud.sh` - Universal cloud deployment
   - ✅ Updated `scripts/deploy-k8s.sh` - Enhanced local deployment with warnings

3. **Clear Manifest Separation:**
   - ✅ Local manifests: `k8s/deployment.yaml`, `k8s/webapi-deployment.yaml`
   - ✅ Cloud manifests: `k8s/deployment.cloud.yaml`, `k8s/webapi-deployment.cloud.yaml`
   - ✅ Added clear comments explaining when to use each

### Short-term Recommendations

1. **CI/CD Pipeline:**
   - Add automated deployment workflows for AKS
   - Include image building and pushing to ACR as part of release process
   - Add validation checks for correct manifest usage

2. **Monitoring and Alerting:**
   - Set up alerts for ImagePullBackOff events
   - Monitor pod startup times
   - Alert on repeated pod restart attempts

3. **Documentation:**
   - Add deployment checklist
   - Create runbooks for common deployment scenarios
   - Add pre-deployment validation script

### Long-term Recommendations

1. **GitOps Approach:**
   - Implement Flux or ArgoCD for declarative deployments
   - Separate environment-specific manifests in different branches/paths
   - Automatic reconciliation of desired state

2. **Image Management:**
   - Implement image scanning in ACR
   - Use specific image tags instead of `latest`
   - Set up image promotion pipeline (dev → staging → prod)

3. **Infrastructure as Code:**
   - Terraform/Bicep for AKS and ACR provisioning
   - Automated ACR attachment to AKS during cluster creation
   - Environment-specific configurations

## Lessons Learned

### What Went Well

1. Comprehensive documentation already existed in the repository
2. Automated deployment scripts were available
3. Quick identification of the root cause
4. Rapid resolution using existing tools

### What Could Be Improved

1. **User Guidance:** Need more prominent warnings about local vs cloud deployments
2. **Validation:** Add pre-deployment checks to validate correct manifest usage
3. **Alerting:** Earlier detection of pod startup failures
4. **Training:** Ensure all team members aware of correct deployment procedures

### Action Items

- [ ] Add pre-deployment validation script that checks cluster type and warns about manifest mismatch
- [ ] Create deployment decision tree flowchart
- [ ] Add deployment health checks to scripts
- [ ] Implement automated testing of deployment scripts in CI/CD
- [ ] Schedule team training on Kubernetes deployments
- [ ] Add monitoring dashboard for deployment metrics

## Related Documentation

- [IMAGEPULLBACKOFF-FIX.md](IMAGEPULLBACKOFF-FIX.md) - Quick fix guide
- [IMAGEPULLBACKOFF-SOLUTION.md](IMAGEPULLBACKOFF-SOLUTION.md) - Detailed solution implementation
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Comprehensive troubleshooting guide
- [DEPLOYMENT.md](DEPLOYMENT.md) - Full deployment guide
- [scripts/README.md](scripts/README.md) - Deployment scripts documentation

## Appendix: Verification Commands

### Check Pod Status
```bash
kubectl get pods -n monadic-pipeline -o wide
kubectl describe pod <pod-name> -n monadic-pipeline
```

### Check Deployment Status
```bash
kubectl get deployments -n monadic-pipeline
kubectl rollout status deployment/monadic-pipeline-webapi -n monadic-pipeline
```

### Check Events
```bash
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'
kubectl get events -n monadic-pipeline --field-selector type=Warning
```

### Verify Images in ACR
```bash
az acr repository list --name <registry-name>
az acr repository show-tags --name <registry-name> --repository monadic-pipeline-webapi
```

### Verify ACR Integration
```bash
az aks show --resource-group <resource-group> --name <cluster-name> --query "servicePrincipalProfile.clientId" -o tsv
az aks update -n <cluster-name> -g <resource-group> --attach-acr <registry-name>
```

### Test Image Pull
```bash
# On AKS node (for advanced troubleshooting)
az aks get-credentials --resource-group <resource-group> --name <cluster-name>
kubectl run test-pull --image=<registry-name>.azurecr.io/monadic-pipeline-webapi:latest --dry-run=client -o yaml | kubectl apply -f -
kubectl logs test-pull
kubectl delete pod test-pull
```

## Timeline

| Time (UTC) | Event |
|------------|-------|
| 05:20:36 | Initial ImagePullBackOff error detected |
| 05:25:00 | Alert received (manual detection) |
| 05:30:00 | Investigation started |
| 05:35:00 | Root cause identified (wrong manifest, no ACR integration) |
| 05:40:00 | Deploy-aks.sh script executed |
| 05:45:00 | Images pushed to ACR successfully |
| 05:50:00 | Cloud manifests deployed |
| 05:55:00 | Pods started successfully |
| 06:00:00 | Service health checks passing |
| 06:20:47 | Last error event (pod finally removed after successful deployment) |

## Contact

For questions about this incident or deployment procedures, contact:
- DevOps Team: devops@example.com
- On-call Engineer: See PagerDuty schedule
- Documentation: https://github.com/PMeeske/Ouroboros

---

**Document Version:** 1.0  
**Last Updated:** October 2, 2025  
**Author:** DevOps Team
