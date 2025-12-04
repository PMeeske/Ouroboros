#!/bin/bash
# Diagnostic script to check IONOS deployment errors for Ouroboros Web API
# This script helps identify and troubleshoot deployment issues on IONOS Cloud
#
# Usage: ./check-ionos-deployment.sh [namespace]
#   namespace: Kubernetes namespace (default: monadic-pipeline)
#
# Examples:
#   ./check-ionos-deployment.sh
#   ./check-ionos-deployment.sh monadic-pipeline

# Note: We don't use 'set -e' here because we want to continue diagnostics even if some checks fail
set -uo pipefail

NAMESPACE="${1:-monadic-pipeline}"
WEBAPI_DEPLOYMENT="monadic-pipeline-webapi"
WEBAPI_SERVICE="monadic-pipeline-webapi-service"

echo "================================================"
echo "IONOS Web API Deployment Diagnostics"
echo "================================================"
echo ""
echo "Namespace: $NAMESPACE"
echo "Deployment: $WEBAPI_DEPLOYMENT"
echo ""

# Check prerequisites
if ! command -v kubectl &> /dev/null; then
    echo "❌ Error: kubectl is not installed"
    exit 1
fi

# Check cluster connection
if ! kubectl cluster-info &> /dev/null; then
    echo "❌ Error: Cannot connect to Kubernetes cluster"
    echo "Please configure kubectl for your IONOS cluster"
    exit 1
fi

echo "✓ Connected to cluster: $(kubectl config current-context)"
echo ""

# Check if namespace exists
if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
    echo "❌ Error: Namespace '$NAMESPACE' does not exist"
    echo ""
    echo "Create namespace with:"
    echo "  kubectl create namespace $NAMESPACE"
    exit 1
fi

echo "================================================"
echo "1. Deployment Status"
echo "================================================"
echo ""

# Check if deployment exists
if ! kubectl get deployment "$WEBAPI_DEPLOYMENT" -n "$NAMESPACE" &> /dev/null; then
    echo "❌ Deployment '$WEBAPI_DEPLOYMENT' not found in namespace '$NAMESPACE'"
    echo ""
    echo "Deploy Web API with:"
    echo "  ./scripts/deploy-ionos.sh $NAMESPACE"
    exit 1
fi

# Get deployment status
kubectl get deployment "$WEBAPI_DEPLOYMENT" -n "$NAMESPACE"
echo ""

# Check replica status
DESIRED=$(kubectl get deployment "$WEBAPI_DEPLOYMENT" -n "$NAMESPACE" -o jsonpath='{.spec.replicas}')
READY=$(kubectl get deployment "$WEBAPI_DEPLOYMENT" -n "$NAMESPACE" -o jsonpath='{.status.readyReplicas}')
AVAILABLE=$(kubectl get deployment "$WEBAPI_DEPLOYMENT" -n "$NAMESPACE" -o jsonpath='{.status.availableReplicas}')

echo "Replicas:"
echo "  Desired: $DESIRED"
echo "  Ready: ${READY:-0}"
echo "  Available: ${AVAILABLE:-0}"
echo ""

if [ "${READY:-0}" -eq "$DESIRED" ] && [ "${AVAILABLE:-0}" -eq "$DESIRED" ]; then
    echo "✅ Deployment is healthy - all replicas are ready"
else
    echo "⚠️  Deployment has issues - not all replicas are ready"
fi

echo ""
echo "================================================"
echo "2. Pod Status"
echo "================================================"
echo ""

# Get pods for the deployment
PODS=$(kubectl get pods -n "$NAMESPACE" -l app=monadic-pipeline-webapi -o jsonpath='{.items[*].metadata.name}')

if [ -z "$PODS" ]; then
    echo "❌ No pods found for deployment '$WEBAPI_DEPLOYMENT'"
    echo ""
    echo "Check deployment events:"
    kubectl describe deployment "$WEBAPI_DEPLOYMENT" -n "$NAMESPACE"
    exit 1
fi

# Display pod status
kubectl get pods -n "$NAMESPACE" -l app=monadic-pipeline-webapi
echo ""

# Check each pod status
HAS_ERRORS=false
for POD in $PODS; do
    POD_STATUS=$(kubectl get pod "$POD" -n "$NAMESPACE" -o jsonpath='{.status.phase}')
    
    if [ "$POD_STATUS" != "Running" ]; then
        HAS_ERRORS=true
        echo "⚠️  Pod $POD is in state: $POD_STATUS"
        echo ""
        
        # Get pod conditions
        echo "Pod conditions:"
        kubectl get pod "$POD" -n "$NAMESPACE" -o jsonpath='{range .status.conditions[*]}{.type}{"\t"}{.status}{"\t"}{.reason}{"\t"}{.message}{"\n"}{end}' | column -t
        echo ""
    fi
done

if [ "$HAS_ERRORS" = false ]; then
    echo "✅ All pods are running"
fi

echo ""
echo "================================================"
echo "3. Recent Events (Last 10 minutes)"
echo "================================================"
echo ""

# Get recent events related to the deployment
kubectl get events -n "$NAMESPACE" \
    --sort-by='.lastTimestamp' \
    --field-selector involvedObject.name="$WEBAPI_DEPLOYMENT" \
    2>/dev/null || echo "No deployment events found"

echo ""

# Get recent events for pods
echo "Pod events:"
for POD in $PODS; do
    echo ""
    echo "Events for pod $POD:"
    kubectl get events -n "$NAMESPACE" \
        --sort-by='.lastTimestamp' \
        --field-selector involvedObject.name="$POD" \
        2>/dev/null | tail -20 || echo "  No events found"
done

echo ""
echo "================================================"
echo "4. Common Error Detection"
echo "================================================"
echo ""

ERROR_FOUND=false

# Check for ImagePullBackOff
IMAGE_PULL_ERRORS=$(kubectl get pods -n "$NAMESPACE" -l app=monadic-pipeline-webapi -o jsonpath='{range .items[*]}{.status.containerStatuses[*].state.waiting.reason}{"\n"}{end}' | grep -c "ImagePullBackOff\|ErrImagePull" || true)

if [ "$IMAGE_PULL_ERRORS" -gt 0 ]; then
    ERROR_FOUND=true
    echo "❌ ImagePullBackOff Error Detected"
    echo ""
    echo "This usually means:"
    echo "  1. Image doesn't exist in the registry"
    echo "  2. Registry authentication failed (check ionos-registry-secret)"
    echo "  3. Image name is incorrect"
    echo ""
    echo "Solutions:"
    echo "  1. Verify registry secret exists:"
    echo "     kubectl get secret ionos-registry-secret -n $NAMESPACE"
    echo ""
    echo "  2. Re-create registry secret:"
    echo "     kubectl delete secret ionos-registry-secret -n $NAMESPACE"
    echo "     kubectl create secret docker-registry ionos-registry-secret \\"
    echo "       --docker-server=adaptive-systems.cr.de-fra.ionos.com \\"
    echo "       --docker-username=<username> \\"
    echo "       --docker-password=<password> \\"
    echo "       --namespace=$NAMESPACE"
    echo ""
    echo "  3. Verify images were pushed to registry:"
    echo "     ./scripts/deploy-ionos.sh $NAMESPACE"
    echo ""
fi

# Check for CrashLoopBackOff
CRASH_LOOP_ERRORS=$(kubectl get pods -n "$NAMESPACE" -l app=monadic-pipeline-webapi -o jsonpath='{range .items[*]}{.status.containerStatuses[*].state.waiting.reason}{"\n"}{end}' | grep -c "CrashLoopBackOff" || true)

if [ "$CRASH_LOOP_ERRORS" -gt 0 ]; then
    ERROR_FOUND=true
    echo "❌ CrashLoopBackOff Error Detected"
    echo ""
    echo "The container is starting but then crashing. Check logs below."
    echo ""
fi

# Check for pending PVCs
PENDING_PVC=$(kubectl get pvc -n "$NAMESPACE" -o jsonpath='{range .items[?(@.status.phase=="Pending")]}{.metadata.name}{"\n"}{end}' 2>/dev/null || true)

if [ -n "$PENDING_PVC" ]; then
    ERROR_FOUND=true
    echo "❌ Pending Persistent Volume Claims"
    echo ""
    echo "The following PVCs are pending:"
    echo "$PENDING_PVC"
    echo ""
    echo "Solutions:"
    echo "  1. Check storage class availability:"
    echo "     kubectl get storageclass"
    echo ""
    echo "  2. Verify IONOS storage class exists:"
    echo "     kubectl describe storageclass ionos-enterprise-ssd"
    echo ""
    echo "  3. Check IONOS Cloud Console for storage provisioning errors"
    echo ""
fi

# Check service
if kubectl get service "$WEBAPI_SERVICE" -n "$NAMESPACE" &> /dev/null; then
    SERVICE_TYPE=$(kubectl get service "$WEBAPI_SERVICE" -n "$NAMESPACE" -o jsonpath='{.spec.type}')
    EXTERNAL_IP=$(kubectl get service "$WEBAPI_SERVICE" -n "$NAMESPACE" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "<pending>")
    
    if [ "$SERVICE_TYPE" = "LoadBalancer" ] && [ "$EXTERNAL_IP" = "<pending>" ]; then
        echo "⚠️  LoadBalancer External IP is pending"
        echo ""
        echo "This is normal for the first 5-10 minutes after deployment."
        echo "If it persists:"
        echo "  1. Check IONOS Cloud Console for load balancer creation status"
        echo "  2. Verify you haven't exceeded IONOS load balancer quota"
        echo "  3. Check IONOS Cloud firewall rules"
        echo ""
    fi
else
    echo "⚠️  Service '$WEBAPI_SERVICE' not found"
    echo ""
fi

if [ "$ERROR_FOUND" = false ]; then
    echo "✅ No common errors detected"
fi

echo ""
echo "================================================"
echo "5. Container Logs (Last 50 lines)"
echo "================================================"
echo ""

for POD in $PODS; do
    POD_STATUS=$(kubectl get pod "$POD" -n "$NAMESPACE" -o jsonpath='{.status.phase}')
    echo "Logs from pod $POD (Status: $POD_STATUS):"
    echo "----------------------------------------"
    kubectl logs "$POD" -n "$NAMESPACE" --tail=50 2>&1 || echo "  Unable to retrieve logs"
    echo ""
done

echo ""
echo "================================================"
echo "6. Resource Status"
echo "================================================"
echo ""

echo "Services:"
kubectl get services -n "$NAMESPACE" -l app=monadic-pipeline-webapi
echo ""

echo "ConfigMaps:"
kubectl get configmaps -n "$NAMESPACE" | grep -E "NAME|monadic" || echo "  No ConfigMaps found"
echo ""

echo "Secrets:"
kubectl get secrets -n "$NAMESPACE" | grep -E "NAME|ionos-registry-secret|monadic" || echo "  No relevant secrets found"
echo ""

echo "Storage Classes:"
kubectl get storageclass 2>&1 | head -10 || echo "  Unable to list storage classes"
echo ""

# Check IONOS storage class specifically
if kubectl get storageclass ionos-enterprise-ssd &> /dev/null; then
    echo "✅ IONOS storage class 'ionos-enterprise-ssd' is available"
else
    echo "⚠️  IONOS storage class 'ionos-enterprise-ssd' not found"
    echo "This may cause PVC provisioning issues"
fi
echo ""

echo "================================================"
echo "7. Recommended Actions"
echo "================================================"
echo ""

if [ "${READY:-0}" -eq "$DESIRED" ]; then
    echo "✅ Deployment is healthy!"
    echo ""
    echo "Access your Web API:"
    echo "  Port-forward: kubectl port-forward -n $NAMESPACE service/$WEBAPI_SERVICE 8080:80"
    echo "  Then open: http://localhost:8080"
    echo ""
    if [ "$SERVICE_TYPE" = "LoadBalancer" ] && [ "$EXTERNAL_IP" != "<pending>" ]; then
        echo "  Or access via LoadBalancer: http://$EXTERNAL_IP"
        echo ""
    fi
else
    echo "⚠️  Deployment needs attention"
    echo ""
    echo "Troubleshooting steps:"
    echo "  1. Review errors detected above"
    echo "  2. Check pod logs: kubectl logs -f deployment/$WEBAPI_DEPLOYMENT -n $NAMESPACE"
    echo "  3. Describe pod: kubectl describe pod <pod-name> -n $NAMESPACE"
    echo "  4. Check events: kubectl get events -n $NAMESPACE --sort-by='.lastTimestamp'"
    echo ""
    echo "Common fixes:"
    echo "  - ImagePullBackOff: Re-run ./scripts/deploy-ionos.sh $NAMESPACE"
    echo "  - CrashLoopBackOff: Check application logs and configuration"
    echo "  - Pending PVC: Verify storage class configuration"
    echo ""
    echo "Full documentation:"
    echo "  - docs/IONOS_DEPLOYMENT_GUIDE.md"
    echo "  - TROUBLESHOOTING.md"
fi

echo ""
echo "================================================"
echo "Diagnostics Complete"
echo "================================================"
echo ""
