# Environment Detection

The `EnvironmentDetector` utility class provides a centralized way to detect the runtime environment of the MonadicPipeline application.

## Purpose

This utility helps the application determine whether it's running in:
- Local development environment
- Kubernetes cluster
- Production environment
- Staging environment

## Usage

```csharp
using LangChainPipeline.Core;

// Check if running in local development
if (EnvironmentDetector.IsLocalDevelopment())
{
    // Enable debug features, relaxed CORS, etc.
    Console.WriteLine("Running in local development mode");
}

// Check if running in Kubernetes
if (EnvironmentDetector.IsRunningInKubernetes())
{
    // Configure for Kubernetes-specific features
    Console.WriteLine("Running in Kubernetes cluster");
}

// Check if running in production
if (EnvironmentDetector.IsProduction())
{
    // Use production configurations
    Console.WriteLine("Running in production mode");
}

// Get the environment name
var envName = EnvironmentDetector.GetEnvironmentName();
Console.WriteLine($"Environment: {envName ?? "Not set"}");
```

## How It Works

The `EnvironmentDetector` checks multiple indicators to determine the environment:

### Local Development Detection

1. **Environment Variables**: Checks `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT`
   - Returns `true` if set to "Development" or "Local"
   - Returns `false` if set to "Production" or "Staging"

2. **Kubernetes Detection**: Returns `false` if running in Kubernetes

3. **Ollama Endpoint**: Checks `PIPELINE__LlmProvider__OllamaEndpoint`
   - Returns `true` if endpoint contains "localhost" or "127.0.0.1"

4. **Default**: Returns `false` (safe default - assumes production unless proven otherwise)

### Kubernetes Detection

1. **Service Account**: Checks for `/var/run/secrets/kubernetes.io/serviceaccount` directory
2. **Environment Variable**: Checks for `KUBERNETES_SERVICE_HOST` environment variable

### Production/Staging Detection

- Uses `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` variables
- Assumes production if running in Kubernetes without environment set
- Defaults to production as a safe default

## Environment Variables

### Setting the Environment

```bash
# Development (local)
export ASPNETCORE_ENVIRONMENT=Development
dotnet run

# Production
export ASPNETCORE_ENVIRONMENT=Production
dotnet run

# Staging
export ASPNETCORE_ENVIRONMENT=Staging
dotnet run
```

### Kubernetes Configuration

In Kubernetes deployments, set the environment via the deployment manifest:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: monadic-pipeline
spec:
  template:
    spec:
      containers:
      - name: app
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
```

## Examples in the Codebase

### Web API CORS Configuration

The Web API uses `EnvironmentDetector` to configure CORS policies:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (EnvironmentDetector.IsLocalDevelopment())
        {
            // Allow all origins in development
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Restrict origins in production
            policy.WithOrigins("https://yourdomain.com")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});
```

### Configuration Builder

The `PipelineConfigurationBuilder` uses it to determine whether to load user secrets:

```csharp
public PipelineConfigurationBuilder AddUserSecrets<T>(bool optional = true) where T : class
{
    if (EnvironmentDetector.IsLocalDevelopment())
    {
        _configurationBuilder.AddUserSecrets<T>(optional);
    }
    return this;
}
```

### Environment Information Endpoint

The Web API root endpoint exposes environment information:

```bash
curl http://localhost:5015/

{
  "service": "MonadicPipeline Web API",
  "version": "1.0.0",
  "status": "running",
  "environment": {
    "name": "Development",
    "isLocalDevelopment": true,
    "isProduction": false,
    "isStaging": false,
    "isKubernetes": false
  },
  ...
}
```

## Best Practices

1. **Use for Feature Toggles**: Enable/disable features based on environment
2. **Security Configuration**: Adjust security policies (CORS, authentication) based on environment
3. **Logging Levels**: Set appropriate logging verbosity based on environment
4. **External Services**: Choose between local and remote services
5. **Performance Optimizations**: Enable/disable caching, monitoring based on environment

## Testing

The utility includes comprehensive unit tests in `EnvironmentDetectorTests.cs`:

```bash
dotnet test --filter "FullyQualifiedName~EnvironmentDetectorTests"
```

All tests verify:
- Environment variable detection
- Kubernetes detection
- Localhost endpoint detection
- Production/Staging/Development detection
- Default behavior (safe defaults)

## Related Configuration Files

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development-specific configuration
- `appsettings.Production.json` - Production-specific configuration
- `docker-compose.dev.yml` - Local development deployment
- `k8s/deployment.yaml` - Kubernetes deployment configuration
