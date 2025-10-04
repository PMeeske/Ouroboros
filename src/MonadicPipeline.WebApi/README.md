# MonadicPipeline Web API

ASP.NET Core Web API providing a Kubernetes-friendly remoting layer for the MonadicPipeline system. This Web API exposes the same functionality as the CLI application through REST endpoints, making it ideal for containerized and cloud-native deployments.

## Features

- 🚀 **REST API Endpoints** - HTTP-based access to pipeline functionality
- 🏥 **Health Checks** - Kubernetes-ready liveness and readiness probes
- 📊 **OpenAPI/Swagger** - Interactive API documentation
- 🔄 **Horizontal Scaling** - Stateless design for multi-instance deployment
- 🎯 **Minimal API** - Built with ASP.NET Core 8.0 minimal APIs
- 🛡️ **Production Ready** - Includes logging, CORS, and error handling

## Quick Start

### Local Development

```bash
cd src/MonadicPipeline.WebApi
dotnet run
```

The API will be available at `http://localhost:5000` (or as configured in `launchSettings.json`).

Access Swagger UI at: `http://localhost:5000/swagger`

### Docker

Build and run with Docker:

```bash
# Build the image
docker build -f Dockerfile.webapi -t monadic-pipeline-webapi:latest .

# Run the container
docker run -p 8080:8080 monadic-pipeline-webapi:latest
```

### Docker Compose

Run the entire stack including Web API, Ollama, Qdrant, and Jaeger:

```bash
docker-compose up -d monadic-pipeline-webapi
```

Access the Web API at: `http://localhost:8080`

## API Endpoints

### System Endpoints

#### Root
```http
GET /
```
Returns service information and available endpoints.

#### Health Check
```http
GET /health
```
Kubernetes liveness probe endpoint.

#### Readiness Check
```http
GET /ready
```
Kubernetes readiness probe endpoint.

### AI Pipeline Endpoints

#### Ask Question
```http
POST /api/ask
Content-Type: application/json

{
  "question": "What is functional programming?",
  "useRag": false,
  "model": "llama3",
  "temperature": 0.7,
  "maxTokens": 2048
}
```

Response:
```json
{
  "success": true,
  "data": {
    "answer": "Functional programming is...",
    "model": "llama3"
  },
  "error": null,
  "executionTimeMs": 1234
}
```

#### Execute Pipeline DSL
```http
POST /api/pipeline
Content-Type: application/json

{
  "dsl": "SetTopic('AI') | UseDraft | UseCritique | UseImprove",
  "model": "llama3",
  "debug": false,
  "temperature": 0.7,
  "maxTokens": 2048
}
```

Response:
```json
{
  "success": true,
  "data": {
    "result": "Final pipeline output...",
    "finalState": "Completed"
  },
  "error": null,
  "executionTimeMs": 5678
}
```

## Configuration

### Environment Variables

- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Production)
- `ASPNETCORE_URLS` - Bind URLs (default: `http://+:8080`)
- `PIPELINE__LlmProvider__OllamaEndpoint` - Ollama endpoint URL
- `PIPELINE__VectorStore__Type` - Vector store type (InMemory/Qdrant)
- `PIPELINE__VectorStore__ConnectionString` - Vector store connection string

### Remote Endpoints

The Web API supports remote AI endpoints (Ollama Cloud, OpenAI-compatible):

```json
{
  "question": "Hello world",
  "endpoint": "https://api.ollama.com",
  "apiKey": "your-api-key"
}
```

## Kubernetes Deployment

Deploy to Kubernetes using the provided manifests:

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Deploy secrets and config
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml

# Deploy Web API
kubectl apply -f k8s/webapi-deployment.yaml

# Verify deployment
kubectl get pods -n monadic-pipeline
kubectl get svc -n monadic-pipeline
```

### Ingress

The Web API includes an Ingress configuration for external access:

```bash
# Add to /etc/hosts
echo "127.0.0.1 monadic-pipeline.local" | sudo tee -a /etc/hosts

# Access the API
curl http://monadic-pipeline.local/health
```

## Scaling

The Web API is stateless and can be horizontally scaled:

```bash
# Scale to 5 replicas
kubectl scale deployment monadic-pipeline-webapi --replicas=5 -n monadic-pipeline

# Verify scaling
kubectl get pods -n monadic-pipeline -l app=monadic-pipeline-webapi
```

## Monitoring

### Health Checks

- **Liveness**: `GET /health` - Checks if the service is running
- **Readiness**: `GET /ready` - Checks if the service is ready to accept traffic

### Observability

The Web API integrates with OpenTelemetry for distributed tracing:

- View traces in Jaeger UI: `http://localhost:16686`
- Configure endpoint via `PIPELINE__Observability__OpenTelemetryEndpoint`

## Architecture

The Web API follows a clean architecture pattern:

```
MonadicPipeline.WebApi/
├── Models/              # DTOs and request/response models
│   ├── AskRequest.cs
│   ├── PipelineRequest.cs
│   └── ApiResponse.cs
├── Services/            # Business logic layer
│   └── PipelineService.cs
├── Program.cs           # Minimal API configuration
└── GlobalUsings.cs      # Global imports
```

### Design Principles

- **Stateless** - No server-side session state
- **Immutable** - Follows functional programming principles from MonadicPipeline core
- **Composable** - Reuses CLI logic through shared service layer
- **Observable** - Structured logging and distributed tracing

## Development

### Build

```bash
dotnet build src/MonadicPipeline.WebApi/MonadicPipeline.WebApi.csproj
```

### Test

```bash
# Run with test profile
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MonadicPipeline.WebApi

# Test endpoints
curl -X POST http://localhost:5000/api/ask \
  -H "Content-Type: application/json" \
  -d '{"question": "Hello world"}'
```

### Debug

Use the provided `launchSettings.json` profiles:

- **Development** - Detailed logging and debugging
- **Production** - Optimized for deployment

**Note**: Swagger UI is now available in all environments at `/swagger`.

## Comparison: CLI vs Web API

Both the CLI and Web API provide the same core functionality as remoting layers:

| Feature | CLI | Web API |
|---------|-----|---------|
| Deployment | Container/VM | Container/Kubernetes |
| Access | Command line | REST API |
| Scaling | Single instance | Horizontal scaling |
| Health Checks | Process monitoring | HTTP endpoints |
| Use Case | Batch jobs, scripts | Web apps, microservices |
| Integration | Shell scripts | HTTP clients |

## Related Documentation

- [Main README](../../README.md) - Project overview
- [Deployment Guide](../../DEPLOYMENT.md) - Deployment instructions
- [Configuration Guide](../../CONFIGURATION_AND_SECURITY.md) - Configuration details
- [CLI Documentation](../MonadicPipeline.CLI/README.md) - CLI usage

## License

Copyright © 2025 - See main project LICENSE file.
