# MonadicPipeline Deployment Examples

This directory contains deployment scripts and configuration files for various deployment scenarios.

## Scripts

### `deploy-local.sh`
Publishes the application to a local directory for manual deployment or systemd service installation.

```bash
./scripts/deploy-local.sh ./publish
```

### `deploy-docker.sh`
Automated Docker Compose deployment with all dependencies.

```bash
./scripts/deploy-docker.sh production
```

### `deploy-k8s.sh`
Automated Kubernetes deployment using kubectl.

```bash
./scripts/deploy-k8s.sh monadic-pipeline
```

## Service Files

### `monadic-pipeline.service`
Systemd service unit file for running MonadicPipeline as a Linux service.

**Installation:**
```bash
# 1. Publish application
./scripts/deploy-local.sh /opt/monadic-pipeline

# 2. Create service user
sudo useradd -r -s /bin/false monadic

# 3. Set permissions
sudo chown -R monadic:monadic /opt/monadic-pipeline
sudo chmod +x /opt/monadic-pipeline/LangChainPipeline

# 4. Install service
sudo cp scripts/monadic-pipeline.service /etc/systemd/system/

# 5. Enable and start
sudo systemctl daemon-reload
sudo systemctl enable monadic-pipeline
sudo systemctl start monadic-pipeline

# 6. Check status
sudo systemctl status monadic-pipeline
sudo journalctl -u monadic-pipeline -f
```

## Windows Service

For Windows deployments, use the Windows Service wrapper:

```powershell
# Publish application
dotnet publish src/MonadicPipeline.CLI/MonadicPipeline.CLI.csproj -c Release -o C:\MonadicPipeline

# Install as Windows Service using sc.exe
sc.exe create MonadicPipeline binPath= "C:\MonadicPipeline\LangChainPipeline.exe" start= auto
sc.exe start MonadicPipeline
```

Or use [NSSM (Non-Sucking Service Manager)](https://nssm.cc/):
```powershell
nssm install MonadicPipeline "C:\MonadicPipeline\LangChainPipeline.exe"
nssm start MonadicPipeline
```

## Azure Deployment

For Azure App Service deployment:

```bash
# Login to Azure
az login

# Create resource group
az group create --name monadic-pipeline-rg --location eastus

# Create App Service plan
az appservice plan create --name monadic-pipeline-plan --resource-group monadic-pipeline-rg --is-linux --sku B1

# Create web app
az webapp create --name monadic-pipeline --resource-group monadic-pipeline-rg --plan monadic-pipeline-plan --runtime "DOTNETCORE:8.0"

# Deploy application
az webapp deploy --name monadic-pipeline --resource-group monadic-pipeline-rg --src-path ./publish.zip --type zip
```

## AWS Deployment

For AWS ECS deployment, see [DEPLOYMENT.md](../DEPLOYMENT.md) for detailed instructions on:
- Creating ECR repository
- Pushing Docker image
- Creating ECS task definition
- Creating ECS service

## See Also

- [DEPLOYMENT.md](../DEPLOYMENT.md) - Comprehensive deployment guide
- [CONFIGURATION_AND_SECURITY.md](../CONFIGURATION_AND_SECURITY.md) - Configuration reference
- [README.md](../README.md) - Project overview
