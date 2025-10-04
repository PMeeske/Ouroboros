# Infrastructure Migration and Change Management Guide

This guide provides comprehensive procedures for safely migrating infrastructure changes across the C#, Kubernetes, and Terraform stack.

## Table of Contents

1. [Overview](#overview)
2. [Change Management Principles](#change-management-principles)
3. [Pre-Migration Checklist](#pre-migration-checklist)
4. [Migration Scenarios](#migration-scenarios)
5. [Rollback Procedures](#rollback-procedures)
6. [Testing and Validation](#testing-and-validation)
7. [Common Migration Patterns](#common-migration-patterns)

## Overview

Infrastructure changes in MonadicPipeline span multiple layers:
- **C# Application Layer**: Configuration, code, dependencies
- **Kubernetes Layer**: Manifests, services, deployments
- **Terraform Layer**: Cloud infrastructure, networking, storage

Changes must be coordinated across all layers to maintain consistency and avoid breakage.

## Change Management Principles

### 1. Test in Lower Environments First

```
Development → Staging → Production
```

**Never** apply changes directly to production without testing in staging.

### 2. Understand Dependency Impact

Before making any change, understand:
- **Upstream dependencies**: What relies on this component?
- **Downstream dependencies**: What does this component rely on?
- **Cross-layer impacts**: How does this affect other infrastructure layers?

### 3. Maintain Backward Compatibility

When possible, make changes backward-compatible:
- Add new features before removing old ones
- Use feature flags for gradual rollout
- Maintain old endpoints during migration

### 4. Document Everything

Every infrastructure change should be documented:
- Why the change is needed
- What components are affected
- How to verify the change
- How to rollback if needed

## Pre-Migration Checklist

Before making any infrastructure change:

```bash
# 1. Validate current infrastructure
./scripts/validate-infrastructure-dependencies.sh

# 2. Backup current state
cd terraform
terraform state pull > backup-$(date +%Y%m%d-%H%M%S).tfstate

# 3. Review documentation
# Read relevant sections in:
# - docs/INFRASTRUCTURE_DEPENDENCIES.md
# - docs/TERRAFORM_K8S_INTEGRATION.md
# - docs/ENVIRONMENT_INFRASTRUCTURE_MAPPING.md

# 4. Create migration plan
# Document in migration-plan.md:
# - Components affected
# - Order of operations
# - Rollback plan
# - Validation steps

# 5. Notify team
# Create PR with detailed description
# Schedule maintenance window if needed
```

## Migration Scenarios

### Scenario 1: Changing C# Configuration

**Impact**: Application behavior, service connections

**Example**: Change Ollama endpoint from localhost to cluster service

#### Steps

1. **Update Configuration Files**

```bash
# Update appsettings.Production.json
# Change: "OllamaEndpoint": "http://localhost:11434"
# To: "OllamaEndpoint": "http://ollama-service:11434"
```

2. **Update Kubernetes ConfigMap**

```bash
# Update k8s/configmap.yaml to match
# Ensure consistency between C# config and K8s config
```

3. **Test in Development**

```bash
# Set environment variable for local testing
export PIPELINE__LlmProvider__OllamaEndpoint="http://ollama-service:11434"

# Run application
dotnet run --project src/MonadicPipeline.CLI
```

4. **Deploy to Staging**

```bash
# Apply updated ConfigMap
kubectl apply -f k8s/configmap.yaml -n monadic-pipeline-staging

# Restart pods to pick up new config
kubectl rollout restart deployment/monadic-pipeline -n monadic-pipeline-staging
kubectl rollout restart deployment/monadic-pipeline-webapi -n monadic-pipeline-staging

# Verify
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline-staging
```

5. **Deploy to Production**

```bash
# Same steps as staging but in production namespace
kubectl apply -f k8s/configmap.yaml -n monadic-pipeline
kubectl rollout restart deployment/monadic-pipeline -n monadic-pipeline
kubectl rollout restart deployment/monadic-pipeline-webapi -n monadic-pipeline

# Monitor for issues
kubectl get pods -n monadic-pipeline -w
```

#### Validation

```bash
# Check pod logs for connection success
kubectl logs deployment/monadic-pipeline -n monadic-pipeline | grep "Ollama"

# Test API endpoint
curl http://monadic-pipeline-webapi-service/health

# Run end-to-end test
kubectl run test-pod --rm -it --image=curlimages/curl --restart=Never -- \
  curl http://monadic-pipeline-webapi-service/api/pipeline/test
```

#### Rollback

```bash
# Revert ConfigMap
git checkout HEAD~1 k8s/configmap.yaml
kubectl apply -f k8s/configmap.yaml -n monadic-pipeline

# Restart pods
kubectl rollout restart deployment/monadic-pipeline -n monadic-pipeline
```

### Scenario 2: Scaling Kubernetes Resources

**Impact**: Resource allocation, pod distribution

**Example**: Increase WebAPI replicas from 2 to 4

#### Steps

1. **Check Current Capacity**

```bash
# Check node resources
kubectl top nodes

# Check current pod resource usage
kubectl top pods -n monadic-pipeline

# Verify we have capacity for 2 more replicas
# Each replica needs: 512Mi request, 2Gi limit
# Required additional: 1Gi request, 4Gi limit
```

2. **Update Kubernetes Manifest**

```yaml
# k8s/webapi-deployment.cloud.yaml
spec:
  replicas: 4  # Changed from 2
```

3. **Apply to Staging**

```bash
kubectl apply -f k8s/webapi-deployment.cloud.yaml -n monadic-pipeline-staging

# Watch rollout
kubectl rollout status deployment/monadic-pipeline-webapi -n monadic-pipeline-staging

# Verify all replicas are running
kubectl get pods -n monadic-pipeline-staging -l app=monadic-pipeline-webapi
```

4. **Validate Load Balancing**

```bash
# Test that traffic is distributed
for i in {1..10}; do
  curl http://monadic-pipeline-webapi-service/api/health
done

# Check pod logs to see which pods served requests
kubectl logs -l app=monadic-pipeline-webapi -n monadic-pipeline-staging --tail=1
```

5. **Apply to Production**

```bash
kubectl apply -f k8s/webapi-deployment.cloud.yaml -n monadic-pipeline
kubectl rollout status deployment/monadic-pipeline-webapi -n monadic-pipeline
```

#### Validation

```bash
# Verify 4 replicas running
kubectl get pods -n monadic-pipeline -l app=monadic-pipeline-webapi

# Check resource usage
kubectl top pods -n monadic-pipeline -l app=monadic-pipeline-webapi

# Monitor for OOM or CPU throttling
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp' | grep -i "oom\|throttle"
```

#### Rollback

```bash
# Scale back to 2 replicas
kubectl scale deployment monadic-pipeline-webapi --replicas=2 -n monadic-pipeline

# Or apply previous manifest
git checkout HEAD~1 k8s/webapi-deployment.cloud.yaml
kubectl apply -f k8s/webapi-deployment.cloud.yaml -n monadic-pipeline
```

### Scenario 3: Increasing Terraform Node Count

**Impact**: Cluster capacity, cost, HA

**Example**: Increase production nodes from 3 to 5

#### Steps

1. **Update Terraform Variables**

```hcl
# terraform/environments/production.tfvars
node_count = 5  # Changed from 3
```

2. **Plan Infrastructure Change**

```bash
cd terraform
terraform init
terraform plan -var-file=environments/production.tfvars -out=plan.tfplan

# Review plan output carefully
# Should show: +2 nodes
# No other unexpected changes
```

3. **Apply During Maintenance Window**

```bash
# Notify team
# Schedule: Low-traffic period

terraform apply plan.tfplan

# Monitor IONOS console for node provisioning
# Typically takes 10-15 minutes
```

4. **Verify New Nodes in Kubernetes**

```bash
# Get kubeconfig (may need refresh)
terraform output -raw k8s_kubeconfig > kubeconfig.prod.yaml
export KUBECONFIG=./kubeconfig.prod.yaml

# Verify 5 nodes
kubectl get nodes

# Check all nodes are Ready
kubectl get nodes -o wide

# Verify node labels and taints
kubectl describe nodes | grep -E "Name:|Taints:|Labels:"
```

5. **Redistribute Pods (Optional)**

```bash
# Kubernetes will automatically schedule new pods on new nodes
# To rebalance existing pods, can do rolling restart
kubectl rollout restart deployment -n monadic-pipeline

# Or use descheduler (if installed)
# kubectl create job --from=cronjob/descheduler manual-deschedule
```

#### Validation

```bash
# Check pod distribution
kubectl get pods -n monadic-pipeline -o wide | awk '{print $7}' | sort | uniq -c

# Should see pods distributed across all 5 nodes

# Check cluster capacity
kubectl describe nodes | grep -A 5 "Allocated resources"

# Monitor for a few hours
kubectl top nodes
```

#### Rollback

```bash
# If issues arise, scale back down
# Edit terraform/environments/production.tfvars
node_count = 3

# Plan and apply
terraform plan -var-file=environments/production.tfvars
terraform apply -var-file=environments/production.tfvars

# Kubernetes will drain and remove nodes
# Pods will be rescheduled on remaining nodes
```

### Scenario 4: Adding New Service Dependency

**Impact**: Application architecture, service mesh, configuration

**Example**: Add Redis caching layer

#### Steps

1. **Update Terraform (if needed)**

```hcl
# If Redis requires persistent storage
# terraform/variables.tf
volumes = [
  # ... existing volumes
  {
    name         = "redis-data"
    size         = 20
    type         = "SSD"
    licence_type = "OTHER"
  }
]
```

2. **Create Kubernetes Manifests**

```yaml
# k8s/redis.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
  namespace: monadic-pipeline
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
        - name: redis
          image: redis:7-alpine
          ports:
            - containerPort: 6379
          volumeMounts:
            - name: redis-data
              mountPath: /data
      volumes:
        - name: redis-data
          persistentVolumeClaim:
            claimName: redis-storage
---
apiVersion: v1
kind: Service
metadata:
  name: redis-service
  namespace: monadic-pipeline
spec:
  selector:
    app: redis
  ports:
    - port: 6379
      targetPort: 6379
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: redis-storage
  namespace: monadic-pipeline
spec:
  accessModes:
    - ReadWriteOnce
  storageClassName: ionos-enterprise-ssd
  resources:
    requests:
      storage: 20Gi
```

3. **Update C# Configuration**

```json
// appsettings.Production.json
{
  "Pipeline": {
    // ... existing config
    "Cache": {
      "Type": "Redis",
      "ConnectionString": "redis-service:6379",
      "DefaultExpiration": 3600
    }
  }
}
```

4. **Update Application Code**

```csharp
// src/MonadicPipeline.Core/Configuration/PipelineConfiguration.cs
public class CacheConfiguration
{
    public string Type { get; set; } = "InMemory";
    public string? ConnectionString { get; set; }
    public int DefaultExpiration { get; set; } = 3600;
}

// Add to PipelineConfiguration
public CacheConfiguration Cache { get; set; } = new();
```

5. **Deploy to Staging**

```bash
# Apply Terraform changes (if any)
cd terraform
terraform apply -var-file=environments/staging.tfvars

# Deploy Redis to Kubernetes
kubectl apply -f k8s/redis.yaml -n monadic-pipeline-staging

# Wait for Redis to be ready
kubectl wait --for=condition=Ready pod -l app=redis -n monadic-pipeline-staging --timeout=60s

# Update ConfigMap with new config
kubectl apply -f k8s/configmap.yaml -n monadic-pipeline-staging

# Deploy updated application
# Build new image with Redis client
docker build -t adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:staging .
docker push adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:staging

# Restart deployment
kubectl rollout restart deployment/monadic-pipeline -n monadic-pipeline-staging
```

6. **Test Integration**

```bash
# Check Redis connectivity
kubectl run redis-test --rm -it --image=redis:7-alpine --restart=Never -- \
  redis-cli -h redis-service ping

# Should return: PONG

# Check application logs
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline-staging | grep -i redis

# Run integration test
# Test that caching is working
```

7. **Deploy to Production**

```bash
# Same steps as staging but in production namespace/environment
```

#### Validation

```bash
# Verify Redis is running
kubectl get pods -n monadic-pipeline -l app=redis

# Check Redis metrics
kubectl exec -it deployment/redis -n monadic-pipeline -- redis-cli INFO stats

# Monitor cache hit rate in application logs
kubectl logs deployment/monadic-pipeline -n monadic-pipeline | grep "cache hit"

# Check for any errors
kubectl logs deployment/monadic-pipeline -n monadic-pipeline | grep -i "redis.*error"
```

#### Rollback

```bash
# Remove Redis
kubectl delete -f k8s/redis.yaml -n monadic-pipeline

# Revert C# configuration
git checkout HEAD~1 appsettings.Production.json
kubectl apply -f k8s/configmap.yaml -n monadic-pipeline

# Revert application code (redeploy previous image)
kubectl set image deployment/monadic-pipeline \
  monadic-pipeline=adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:previous-tag \
  -n monadic-pipeline

# If Terraform storage was added, destroy it
cd terraform
terraform destroy -target=module.storage.ionoscloud_volume.redis-data
```

### Scenario 5: Changing Storage Size

**Impact**: Data persistence, cost, downtime

**Example**: Increase Qdrant storage from 50GB to 100GB

#### Steps

1. **Update Terraform Variables**

```hcl
# terraform/environments/production.tfvars
volumes = [
  {
    name         = "qdrant-data"
    size         = 100  # Changed from 50
    type         = "SSD"
    licence_type = "OTHER"
  },
  # ... other volumes
]
```

2. **Plan Storage Expansion**

```bash
cd terraform
terraform plan -var-file=environments/production.tfvars

# Note: Storage expansion is usually non-disruptive
# But verify in IONOS documentation
```

3. **Backup Data First**

```bash
# Create snapshot of Qdrant data before expansion
kubectl exec deployment/qdrant -n monadic-pipeline -- \
  tar czf /tmp/qdrant-backup.tar.gz /qdrant/storage

# Copy backup out
kubectl cp monadic-pipeline/qdrant-pod:/tmp/qdrant-backup.tar.gz \
  ./qdrant-backup-$(date +%Y%m%d).tar.gz
```

4. **Apply Terraform Changes**

```bash
terraform apply -var-file=environments/production.tfvars

# Volume expansion happens automatically
# No pod restart needed in most cases
```

5. **Update Kubernetes PVC**

```yaml
# k8s/qdrant.yaml
# Update PVC size to match Terraform
spec:
  resources:
    requests:
      storage: 100Gi  # Changed from 50Gi
```

```bash
kubectl apply -f k8s/qdrant.yaml -n monadic-pipeline

# Kubernetes will expand PVC if StorageClass supports it
```

6. **Verify Expansion**

```bash
# Check PVC size
kubectl get pvc qdrant-storage -n monadic-pipeline

# Check actual volume size in pod
kubectl exec deployment/qdrant -n monadic-pipeline -- df -h /qdrant/storage

# Should show 100GB
```

#### Validation

```bash
# Verify Qdrant is still accessible
kubectl exec deployment/qdrant -n monadic-pipeline -- \
  curl -X GET http://localhost:6333/collections

# Test write operation
kubectl exec deployment/monadic-pipeline -n monadic-pipeline -- \
  curl -X POST http://qdrant-service:6333/collections/test/points \
  -H "Content-Type: application/json" \
  -d '{"points": [{"id": 1, "vector": [0.1, 0.2], "payload": {"test": true}}]}'

# Check application logs for any storage errors
kubectl logs deployment/monadic-pipeline -n monadic-pipeline | grep -i "storage\|qdrant"
```

#### Rollback

**Warning**: Storage downsizing is risky and may cause data loss.

If expansion causes issues:

```bash
# Restore from backup
kubectl exec deployment/qdrant -n monadic-pipeline -- \
  tar xzf /tmp/qdrant-backup.tar.gz -C /

# DO NOT downsize storage unless absolutely necessary
# Data loss may occur

# If downsize is required:
# 1. Backup all data
# 2. Delete PVC
# 3. Recreate with smaller size
# 4. Restore data
# (This causes downtime)
```

## Rollback Procedures

### General Rollback Strategy

```
1. Identify issue
   ↓
2. Stop making changes
   ↓
3. Assess impact
   ↓
4. Execute rollback (layer by layer)
   ↓
5. Verify system health
   ↓
6. Post-mortem analysis
```

### Layer-by-Layer Rollback

#### Terraform Rollback

```bash
# Option 1: Revert to previous state (if backed up)
terraform state push backup-<timestamp>.tfstate

# Option 2: Target destroy specific resources
terraform destroy -target=module.kubernetes.ionoscloud_k8s_node_pool.pool2

# Option 3: Apply previous configuration
git checkout <previous-commit> terraform/
terraform apply -var-file=environments/production.tfvars
```

#### Kubernetes Rollback

```bash
# Rollback deployment
kubectl rollout undo deployment/monadic-pipeline -n monadic-pipeline

# Rollback to specific revision
kubectl rollout history deployment/monadic-pipeline -n monadic-pipeline
kubectl rollout undo deployment/monadic-pipeline --to-revision=<N> -n monadic-pipeline

# Revert manifests
git checkout <previous-commit> k8s/
kubectl apply -f k8s/ -n monadic-pipeline
```

#### Application Rollback

```bash
# Redeploy previous container image
kubectl set image deployment/monadic-pipeline \
  monadic-pipeline=adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:<previous-tag> \
  -n monadic-pipeline

# Or use rollout undo
kubectl rollout undo deployment/monadic-pipeline -n monadic-pipeline
```

### Emergency Rollback

If production is severely impacted:

```bash
# 1. Immediately scale down affected deployments
kubectl scale deployment <affected-deployment> --replicas=0 -n monadic-pipeline

# 2. Scale up last known good deployment
# (Keep old deployment around for emergencies)
kubectl scale deployment <old-deployment> --replicas=2 -n monadic-pipeline

# 3. Switch service selector to old deployment
kubectl patch service <service-name> -n monadic-pipeline \
  -p '{"spec":{"selector":{"version":"old"}}}'

# 4. Fix issues in affected deployment offline

# 5. Gradually switch back
kubectl scale deployment <affected-deployment> --replicas=2 -n monadic-pipeline
kubectl scale deployment <old-deployment> --replicas=0 -n monadic-pipeline
```

## Testing and Validation

### Pre-Deployment Testing

```bash
# 1. Syntax validation
./scripts/validate-infrastructure-dependencies.sh

# 2. Dry-run
terraform plan -var-file=environments/production.tfvars
kubectl apply --dry-run=client -f k8s/

# 3. Staging validation
# Deploy to staging first
# Run smoke tests
# Monitor for 24 hours

# 4. Load testing (if major change)
# Use k6, locust, or similar
# Ensure performance is acceptable
```

### Post-Deployment Validation

```bash
# 1. Health checks
kubectl get pods -n monadic-pipeline
kubectl get all -n monadic-pipeline

# 2. Log monitoring
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline --tail=100

# 3. Metrics
kubectl top nodes
kubectl top pods -n monadic-pipeline

# 4. End-to-end test
curl http://monadic-pipeline-webapi-service/health
curl -X POST http://monadic-pipeline-webapi-service/api/pipeline/test

# 5. Monitor for errors
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp' | tail -20
```

## Common Migration Patterns

### Pattern 1: Blue-Green Deployment

Deploy new version alongside old version, then switch traffic:

```yaml
# Old deployment (blue)
metadata:
  name: monadic-pipeline-blue
  labels:
    version: blue

# New deployment (green)
metadata:
  name: monadic-pipeline-green
  labels:
    version: green

# Service switches between blue and green
selector:
  app: monadic-pipeline
  version: blue  # Switch to green when ready
```

### Pattern 2: Canary Deployment

Gradually shift traffic to new version:

```bash
# Step 1: Deploy canary (10% traffic)
kubectl apply -f deployment-canary.yaml
kubectl scale deployment monadic-pipeline-canary --replicas=1

# Step 2: Monitor metrics
# If good, increase to 50%
kubectl scale deployment monadic-pipeline-canary --replicas=2
kubectl scale deployment monadic-pipeline-stable --replicas=2

# Step 3: Full rollout
kubectl scale deployment monadic-pipeline-canary --replicas=4
kubectl scale deployment monadic-pipeline-stable --replicas=0
```

### Pattern 3: Feature Flags

Use configuration to enable features gradually:

```json
{
  "Features": {
    "UseRedisCache": false,  // Enable gradually
    "EnableNewAlgorithm": false,
    "MaxConcurrentRequests": 10
  }
}
```

```csharp
// In application code
if (config.Features.UseRedisCache)
{
    // Use Redis
}
else
{
    // Use in-memory cache
}
```

## Best Practices

1. **Always test in staging first**
2. **Make one change at a time**
3. **Document your changes**
4. **Have a rollback plan**
5. **Monitor during and after changes**
6. **Communicate with team**
7. **Schedule changes during low-traffic periods**
8. **Backup before major changes**
9. **Use version control for all infrastructure**
10. **Validate at every step**

## Conclusion

Successful infrastructure migration requires:
- ✅ Careful planning
- ✅ Layer-by-layer coordination
- ✅ Thorough testing
- ✅ Clear rollback procedures
- ✅ Continuous monitoring

Always prioritize safety and system stability over speed of change.

---

**Version**: 1.0.0
**Last Updated**: 2025-01-XX
**Maintained By**: Infrastructure Team
