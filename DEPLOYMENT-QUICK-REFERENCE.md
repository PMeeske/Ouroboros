# MonadicPipeline Deployment - Quick Reference

## Quick Start Commands

### Local Development
```bash
# Build and run
dotnet build
cd src/MonadicPipeline.CLI
dotnet run -- --help

# With Ollama
ollama serve &
dotnet run -- ask -q "Explain functional programming"
```

### Docker Deployment
```bash
# Production
./scripts/deploy-docker.sh production

# Development
./scripts/deploy-docker.sh development

# Stop
docker-compose down
```

### Kubernetes Deployment
```bash
# Deploy
./scripts/deploy-k8s.sh

# Check status
kubectl get all -n monadic-pipeline

# Verify Web API
kubectl get service monadic-pipeline-webapi-service -n monadic-pipeline
kubectl get pods -n monadic-pipeline -l app=monadic-pipeline-webapi

# View logs
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline

# Delete
kubectl delete namespace monadic-pipeline
```

### Local/Systemd Deployment
```bash
# Publish
./scripts/deploy-local.sh /opt/monadic-pipeline

# Install service
sudo cp scripts/monadic-pipeline.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable monadic-pipeline
sudo systemctl start monadic-pipeline

# Check status
sudo systemctl status monadic-pipeline
```

## Service Endpoints

| Service | Local | Docker | Kubernetes |
|---------|-------|--------|------------|
| Web API | http://localhost:8080 | http://localhost:8080 | Port-forward required |
| Ollama | http://localhost:11434 | http://localhost:11434 | Port-forward required |
| Qdrant | http://localhost:6333 | http://localhost:6333 | Port-forward required |
| Jaeger UI | http://localhost:16686 | http://localhost:16686 | Port-forward required |

## Common Commands

### Docker Commands
```bash
# Build image
docker build -t monadic-pipeline:latest .

# Run CLI
docker run -it --rm monadic-pipeline:latest --help

# View logs
docker-compose logs -f

# Restart service
docker-compose restart monadic-pipeline

# Clean up
docker-compose down -v
```

### Kubernetes Commands
```bash
# Port forwarding
kubectl port-forward -n monadic-pipeline service/monadic-pipeline-webapi-service 8080:80
kubectl port-forward -n monadic-pipeline service/jaeger-ui 16686:16686
kubectl port-forward -n monadic-pipeline service/qdrant-service 6333:6333

# Execute command
kubectl exec -it deployment/monadic-pipeline -n monadic-pipeline -- dotnet LangChainPipeline.dll --help

# Scale
kubectl scale deployment/monadic-pipeline --replicas=3 -n monadic-pipeline

# Update
kubectl set image deployment/monadic-pipeline monadic-pipeline=monadic-pipeline:v2.0.0 -n monadic-pipeline

# Rollback
kubectl rollout undo deployment/monadic-pipeline -n monadic-pipeline
```

### Local Deployment Commands
```bash
# Check service status
sudo systemctl status monadic-pipeline

# View logs
sudo journalctl -u monadic-pipeline -f

# Restart service
sudo systemctl restart monadic-pipeline

# Stop service
sudo systemctl stop monadic-pipeline
```

## Configuration

### Environment Variables
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Production

# Override configuration
export PIPELINE__LlmProvider__OllamaEndpoint=http://custom-ollama:11434
export PIPELINE__Execution__MaxTurns=10
export PIPELINE__Observability__MinimumLogLevel=Debug
```

### Configuration Files
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- `.env` - Environment variables (Docker Compose)

## Troubleshooting

### Ollama Not Running
```bash
# Check if Ollama is accessible
curl http://localhost:11434/api/tags

# Pull models
ollama pull llama3
ollama pull nomic-embed-text
```

### Connection Issues
```bash
# Docker - check network
docker network ls
docker network inspect pipeline-network

# Kubernetes - check pod networking
kubectl get pods -n monadic-pipeline -o wide
kubectl describe pod <pod-name> -n monadic-pipeline
```

### Memory Issues
```bash
# Docker - increase memory limit
docker-compose down
# Edit docker-compose.yml to increase resource limits
docker-compose up -d

# Kubernetes - check resource usage
kubectl top pods -n monadic-pipeline
kubectl describe pod <pod-name> -n monadic-pipeline
```

### Web API Service Issues
```bash
# Check Web API service
kubectl get service monadic-pipeline-webapi-service -n monadic-pipeline

# Check Ingress
kubectl get ingress monadic-pipeline-webapi-ingress -n monadic-pipeline

# Check Web API pods
kubectl get pods -n monadic-pipeline -l app=monadic-pipeline-webapi

# View Web API logs
kubectl logs -f deployment/monadic-pipeline-webapi -n monadic-pipeline
```

### View Logs
```bash
# Docker
docker-compose logs -f monadic-pipeline
ls -l logs/

# Kubernetes
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline
kubectl logs <pod-name> -n monadic-pipeline --previous

# Systemd
sudo journalctl -u monadic-pipeline -f
```

## Security Notes

⚠️ **Before Production Deployment:**

1. Update secrets in `k8s/secrets.yaml`
2. Use external secret management (Azure Key Vault, AWS Secrets Manager)
3. Enable TLS/SSL for all services
4. Configure authentication and authorization
5. Set resource limits
6. Enable security scanning
7. Review firewall rules

## Additional Resources

- [DEPLOYMENT.md](DEPLOYMENT.md) - Complete deployment guide
- [CONFIGURATION_AND_SECURITY.md](CONFIGURATION_AND_SECURITY.md) - Configuration reference
- [README.md](README.md) - Project overview

---

For more details, see the complete [Deployment Guide](DEPLOYMENT.md).
