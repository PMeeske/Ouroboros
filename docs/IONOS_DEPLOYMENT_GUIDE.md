# IONOS Cloud Deployment Guide for MonadicPipeline

This guide provides detailed instructions for deploying MonadicPipeline to IONOS Cloud Kubernetes infrastructure.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [IONOS Cloud Setup](#ionos-cloud-setup)
- [Container Registry Configuration](#container-registry-configuration)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Storage Configuration](#storage-configuration)
- [Networking and Ingress](#networking-and-ingress)
- [Monitoring and Logging](#monitoring-and-logging)
- [Troubleshooting](#troubleshooting)

## Overview

IONOS Cloud provides enterprise-grade Kubernetes infrastructure with:
- **Managed Kubernetes Service (MKS)**: Fully managed Kubernetes clusters
- **Container Registry**: Private Docker registry for secure image storage
- **Enterprise Storage**: SSD and HDD storage classes for persistent volumes
- **Load Balancers**: Integrated load balancing for external access
- **Network Security**: VPC, firewalls, and security groups

**Adaptive Systems Inc.** has chosen IONOS Cloud for its cost-effectiveness, European data sovereignty, and enterprise features.

## Prerequisites

Before deploying to IONOS Cloud, ensure you have:

1. **IONOS Cloud Account**: Sign up at [cloud.ionos.com](https://cloud.ionos.com)
2. **IONOS Cloud CLI**: Install the IONOS CLI tool
   ```bash
   # Install via pip
   pip install ionosctl
   
   # Configure credentials
   ionosctl config setup
   ```

3. **kubectl**: Kubernetes command-line tool
   ```bash
   # macOS
   brew install kubectl
   
   # Linux
   curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
   sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
   ```

4. **Docker**: For building and pushing images
   ```bash
   # macOS
   brew install docker
   
   # Linux - follow Docker documentation
   ```

5. **Git**: For cloning the repository
   ```bash
   git clone https://github.com/PMeeske/MonadicPipeline.git
   cd MonadicPipeline
   ```

## IONOS Cloud Setup

### 1. Create Kubernetes Cluster

Using IONOS Cloud Console:

1. Navigate to **Compute** → **Managed Kubernetes**
2. Click **Create Cluster**
3. Configure cluster:
   - **Name**: `monadic-pipeline-cluster`
   - **Kubernetes Version**: Latest stable (1.28+)
   - **Location**: Select preferred data center (Frankfurt, Berlin, etc.)
   - **Node Pools**:
     - **Name**: `default-pool`
     - **Node Count**: 3
     - **CPU**: 4 cores
     - **RAM**: 16 GB
     - **Storage**: 100 GB SSD

4. Click **Create**

Using IONOS CLI:

```bash
ionosctl k8s cluster create \
  --name monadic-pipeline-cluster \
  --k8s-version 1.28 \
  --location de/fra
```

### 2. Download kubeconfig

After cluster creation:

```bash
# Using IONOS Console
# Navigate to Cluster → Download kubeconfig

# Using CLI
ionosctl k8s kubeconfig get --cluster-id <cluster-id> > ~/.kube/ionos-config

# Set KUBECONFIG
export KUBECONFIG=~/.kube/ionos-config

# Verify connection
kubectl cluster-info
kubectl get nodes
```

## Container Registry Configuration

### 1. Enable IONOS Container Registry

```bash
# Create registry (via IONOS Console or CLI)
# Registry URL format: registry.ionos.com/<your-project-name>

# For Adaptive Systems Inc.:
IONOS_REGISTRY="registry.ionos.com/adaptive-systems"
```

### 2. Authenticate Docker

```bash
# Login to IONOS Container Registry
docker login registry.ionos.com

# Enter credentials when prompted
Username: <your-ionos-username>
Password: <your-ionos-password>
```

### 3. Set Environment Variables

```bash
export IONOS_REGISTRY="registry.ionos.com"
export IONOS_PROJECT="adaptive-systems"
export IONOS_USERNAME="<your-username>"
export IONOS_PASSWORD="<your-password>"
```

## Kubernetes Deployment

### Automated Deployment (Recommended)

Use the provided deployment script:

```bash
# Set environment variables
export IONOS_REGISTRY="registry.ionos.com"
export IONOS_PROJECT="adaptive-systems"
export IONOS_USERNAME="<your-username>"
export IONOS_PASSWORD="<your-password>"

# Run deployment script
./scripts/deploy-ionos.sh monadic-pipeline
```

The script will:
1. Authenticate with IONOS Container Registry
2. Build Docker images
3. Push images to IONOS registry
4. Create Kubernetes namespace
5. Configure registry pull secrets
6. Deploy all components
7. Wait for deployments to be ready

### Manual Deployment

If you prefer manual deployment:

#### Step 1: Build and Push Images

```bash
REGISTRY="registry.ionos.com/adaptive-systems"

# Build CLI image
docker build -t ${REGISTRY}/monadic-pipeline:latest -f Dockerfile .
docker push ${REGISTRY}/monadic-pipeline:latest

# Build WebAPI image
docker build -t ${REGISTRY}/monadic-pipeline-webapi:latest -f Dockerfile.webapi .
docker push ${REGISTRY}/monadic-pipeline-webapi:latest
```

#### Step 2: Create Namespace and Secrets

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Create registry pull secret
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=registry.ionos.com \
  --docker-username=<your-username> \
  --docker-password=<your-password> \
  --namespace=monadic-pipeline

# Apply other secrets
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
```

#### Step 3: Update Manifests

Update `k8s/deployment.cloud.yaml` and `k8s/webapi-deployment.cloud.yaml`:

```yaml
# Change image references
image: registry.ionos.com/adaptive-systems/monadic-pipeline:latest

# Uncomment imagePullSecrets
imagePullSecrets:
  - name: ionos-registry-secret
```

Update `k8s/qdrant.yaml` and `k8s/ollama.yaml`:

```yaml
# Change storage class
storageClassName: ionos-enterprise-ssd
```

#### Step 4: Deploy Components

```bash
# Deploy dependencies
kubectl apply -f k8s/ollama.yaml
kubectl apply -f k8s/qdrant.yaml
kubectl apply -f k8s/jaeger.yaml

# Deploy applications
kubectl apply -f k8s/deployment.cloud.yaml
kubectl apply -f k8s/webapi-deployment.cloud.yaml
```

#### Step 5: Verify Deployment

```bash
# Check deployments
kubectl get deployments -n monadic-pipeline

# Check pods
kubectl get pods -n monadic-pipeline

# Check services
kubectl get services -n monadic-pipeline

# Check persistent volumes
kubectl get pvc -n monadic-pipeline
```

## Storage Configuration

### IONOS Storage Classes

IONOS Cloud provides two storage classes:

1. **ionos-enterprise-ssd**: High-performance SSD storage
   - Best for: Qdrant vector database, Ollama models
   - Performance: High IOPS, low latency
   - Cost: Premium

2. **ionos-enterprise-hdd**: Cost-effective HDD storage
   - Best for: Logs, backups, archives
   - Performance: Standard IOPS
   - Cost: Economical

### Storage Recommendations

```yaml
# Qdrant (requires fast I/O)
storageClassName: ionos-enterprise-ssd
storage: 10Gi

# Ollama (model storage)
storageClassName: ionos-enterprise-ssd
storage: 20Gi

# Application logs (if needed)
storageClassName: ionos-enterprise-hdd
storage: 5Gi
```

### Verify Storage

```bash
# List storage classes
kubectl get storageclass

# Check PVC status
kubectl get pvc -n monadic-pipeline

# Describe PVC for details
kubectl describe pvc qdrant-pvc -n monadic-pipeline
```

## Networking and Ingress

### LoadBalancer Configuration

To expose the Web API externally using IONOS LoadBalancer:

```bash
# Patch service to use LoadBalancer
kubectl patch service monadic-pipeline-webapi-service \
  -n monadic-pipeline \
  -p '{"spec":{"type":"LoadBalancer"}}'

# Get external IP
kubectl get service monadic-pipeline-webapi-service -n monadic-pipeline
```

### Ingress Configuration

For production deployments with SSL/TLS:

#### Step 1: Install NGINX Ingress Controller

```bash
# Add NGINX Ingress Helm repository
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

# Install NGINX Ingress
helm install nginx-ingress ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace
```

#### Step 2: Install cert-manager (for SSL)

```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Create Let's Encrypt ClusterIssuer
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@adaptive-systems.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
```

#### Step 3: Configure Ingress

Update `k8s/webapi-deployment.cloud.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: monadic-pipeline-webapi-ingress
  namespace: monadic-pipeline
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
    - hosts:
        - monadic-pipeline.yourdomain.com
      secretName: monadic-pipeline-tls
  rules:
    - host: monadic-pipeline.yourdomain.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: monadic-pipeline-webapi-service
                port:
                  number: 80
```

Apply:

```bash
kubectl apply -f k8s/webapi-deployment.cloud.yaml
```

### DNS Configuration

Point your domain to the Ingress LoadBalancer IP:

```bash
# Get LoadBalancer IP
kubectl get ingress monadic-pipeline-webapi-ingress -n monadic-pipeline

# Add DNS A record
# monadic-pipeline.yourdomain.com -> <EXTERNAL-IP>
```

## Monitoring and Logging

### Access Logs

```bash
# View Web API logs
kubectl logs -f deployment/monadic-pipeline-webapi -n monadic-pipeline

# View CLI logs
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline

# View Ollama logs
kubectl logs -f deployment/ollama -n monadic-pipeline

# View Qdrant logs
kubectl logs -f deployment/qdrant -n monadic-pipeline
```

### Port Forwarding (Development)

```bash
# Forward Web API port
kubectl port-forward -n monadic-pipeline service/monadic-pipeline-webapi-service 8080:80

# Access at http://localhost:8080

# Forward Qdrant dashboard
kubectl port-forward -n monadic-pipeline service/qdrant-service 6333:6333

# Access at http://localhost:6333/dashboard
```

### Jaeger Tracing

```bash
# Forward Jaeger UI
kubectl port-forward -n monadic-pipeline service/jaeger-query 16686:16686

# Access at http://localhost:16686
```

## Troubleshooting

### Common Issues

#### 1. ImagePullBackOff

**Symptoms**: Pods stuck in `ImagePullBackOff` state

**Solution**:
```bash
# Verify registry secret
kubectl get secret ionos-registry-secret -n monadic-pipeline

# Re-create if necessary
kubectl delete secret ionos-registry-secret -n monadic-pipeline
kubectl create secret docker-registry ionos-registry-secret \
  --docker-server=registry.ionos.com \
  --docker-username=<username> \
  --docker-password=<password> \
  --namespace=monadic-pipeline

# Restart deployment
kubectl rollout restart deployment/monadic-pipeline-webapi -n monadic-pipeline
```

#### 2. Storage Provisioning Issues

**Symptoms**: PVC stuck in `Pending` state

**Solution**:
```bash
# Check storage class availability
kubectl get storageclass

# Verify IONOS storage class exists
kubectl describe storageclass ionos-enterprise-ssd

# If not available, contact IONOS support or use default storage class
```

#### 3. LoadBalancer Not Getting External IP

**Symptoms**: Service stuck in `<pending>` for EXTERNAL-IP

**Solution**:
```bash
# Check IONOS Cloud Console for load balancer creation
# It may take 5-10 minutes

# Verify service configuration
kubectl describe service monadic-pipeline-webapi-service -n monadic-pipeline

# Check IONOS Cloud limits (load balancer quota)
```

#### 4. Connection Issues

**Symptoms**: Cannot connect to services

**Solution**:
```bash
# Check firewall rules in IONOS Cloud Console
# Ensure port 80/443 is open for LoadBalancer

# Verify network policies
kubectl get networkpolicies -n monadic-pipeline

# Test connectivity from within cluster
kubectl run -it --rm debug --image=curlimages/curl --restart=Never -- sh
# Inside pod:
curl http://monadic-pipeline-webapi-service.monadic-pipeline.svc.cluster.local
```

### Health Checks

```bash
# Check all resources
kubectl get all -n monadic-pipeline

# Check resource usage
kubectl top nodes
kubectl top pods -n monadic-pipeline

# Check events
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'
```

### Cleanup

To remove the deployment:

```bash
# Delete namespace (removes all resources)
kubectl delete namespace monadic-pipeline

# Or delete individual components
kubectl delete -f k8s/webapi-deployment.cloud.yaml
kubectl delete -f k8s/deployment.cloud.yaml
kubectl delete -f k8s/qdrant.yaml
kubectl delete -f k8s/ollama.yaml
kubectl delete -f k8s/jaeger.yaml
```

## Cost Optimization

### Tips for Reducing IONOS Cloud Costs

1. **Use HDD for non-critical storage**
   ```yaml
   storageClassName: ionos-enterprise-hdd  # Instead of SSD where possible
   ```

2. **Scale down during off-hours**
   ```bash
   # Scale down replicas
   kubectl scale deployment monadic-pipeline-webapi --replicas=1 -n monadic-pipeline
   ```

3. **Use node autoscaling**
   ```bash
   # Configure cluster autoscaler (IONOS MKS feature)
   ```

4. **Monitor resource usage**
   ```bash
   kubectl top pods -n monadic-pipeline
   # Adjust resource requests/limits accordingly
   ```

## Security Best Practices

1. **Use private registry**: Store images in IONOS private registry
2. **Enable RBAC**: Configure Role-Based Access Control
3. **Network policies**: Restrict pod-to-pod communication
4. **Secrets management**: Use Kubernetes secrets or external secret managers
5. **SSL/TLS**: Enable HTTPS with cert-manager
6. **Regular updates**: Keep Kubernetes and images up to date

## Support

For IONOS Cloud specific issues:
- **Documentation**: [cloud.ionos.com/docs](https://cloud.ionos.com/docs)
- **Support**: IONOS Cloud Support Portal
- **Community**: IONOS Cloud Community Forum

For MonadicPipeline issues:
- **GitHub Issues**: [github.com/PMeeske/MonadicPipeline/issues](https://github.com/PMeeske/MonadicPipeline/issues)
- **Documentation**: See project README and other docs

---

**Adaptive Systems Inc.** - Enterprise AI Pipeline Solutions
