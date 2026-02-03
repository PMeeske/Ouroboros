# Configuration and Security Features

This document describes the configuration management and security features implemented in Ouroboros.

## Configuration Management

### Overview

Ouroboros uses a flexible configuration system based on `Microsoft.Extensions.Configuration` that supports multiple configuration sources:

- **JSON files** (`appsettings.json`, `appsettings.{Environment}.json`)
- **Environment variables** (with optional prefix)
- **User secrets** (for development)
- **Azure Key Vault** (for production secrets)

### Configuration Structure

The main configuration is organized into sections:

```json
{
  "Pipeline": {
    "LlmProvider": {
      "DefaultProvider": "Ollama",
      "OllamaEndpoint": "http://localhost:11434",
      "DefaultChatModel": "llama3",
      "DefaultEmbeddingModel": "nomic-embed-text",
      "RequestTimeoutSeconds": 120
    },
    "VectorStore": {
      "Type": "InMemory",
      "ConnectionString": null,
      "BatchSize": 100,
      "DefaultCollection": "pipeline_vectors"
    },
    "Execution": {
      "MaxTurns": 5,
      "MaxParallelToolExecutions": 5,
      "EnableDebugOutput": false,
      "ToolExecutionTimeoutSeconds": 60
    },
    "Observability": {
      "EnableStructuredLogging": true,
      "MinimumLogLevel": "Information",
      "EnableMetrics": false,
      "EnableTracing": false
    }
  }
}
```

### Using Configuration

```csharp
// Create configuration with defaults
var config = PipelineConfigurationBuilder
    .CreateDefault(basePath: Directory.GetCurrentDirectory())
    .Build();

// Access settings
var ollamaEndpoint = config.LlmProvider.OllamaEndpoint;
var maxTurns = config.Execution.MaxTurns;

// Or build IConfiguration for dependency injection
var configuration = PipelineConfigurationBuilder
    .CreateDefault()
    .BuildConfiguration();
```

### Environment-Specific Configuration

Three environment profiles are provided:

1. **Development** (`appsettings.Development.json`)
   - Debug logging enabled
   - Extended timeouts for debugging
   - Detailed observability

2. **Production** (`appsettings.Production.json`)
   - Warning-level logging
   - Production-ready timeouts
   - External vector store support
   - Environment variable placeholders for secrets

3. **Default** (`appsettings.json`)
   - Balanced defaults suitable for most scenarios

### Environment Variables

Configuration can be overridden using environment variables with the `PIPELINE_` prefix:

```bash
export PIPELINE__LlmProvider__OllamaEndpoint="http://remote-ollama:11434"
export PIPELINE__Execution__MaxTurns="10"
export PIPELINE__Observability__MinimumLogLevel="Debug"
```

Note: Use double underscores (`__`) to represent nested sections.

### Secrets Management

For development, use .NET user secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "Pipeline:LlmProvider:OpenAiApiKey" "sk-..."
dotnet user-secrets set "Pipeline:VectorStore:ConnectionString" "..."

# Anthropic API key
dotnet user-secrets set "ANTHROPIC_API_KEY" "sk-ant-api03-..."
```

**Supported API Key Environment Variables:**
- `ANTHROPIC_API_KEY` - Anthropic Claude API key
- `OPENAI_API_KEY` - OpenAI API key
- `GITHUB_TOKEN` - GitHub Models token
- `CHAT_API_KEY` - Generic fallback for any provider

For production, use environment variables or Azure Key Vault:

```csharp
var builder = new PipelineConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentConfiguration()
    .AddEnvironmentVariables("PIPELINE_")
    .AddAzureKeyVault(keyVaultUri); // If using Azure Key Vault
```

## Structured Logging

### Overview

Ouroboros uses Serilog for structured logging with support for:

- **Console output** with readable formatting
- **File output** with rolling daily logs
- **Structured JSON** for production systems
- **Log enrichment** (machine name, thread ID, environment)

### Configuration

Logging is configured in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/pipeline-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

### Usage

```csharp
using Serilog;

// Create logger
var logger = LoggingConfiguration.CreateLogger(configuration, pipelineConfig);
Log.Logger = logger;

// Use structured logging
Log.Information("Pipeline execution started for {Topic}", topic);
Log.Warning("Tool execution timeout for {ToolName} after {Timeout}s", toolName, timeout);
Log.Error(ex, "Failed to execute pipeline step {StepName}", stepName);

// Logs are written to both console and files in logs/ directory
```

## Input Validation and Sanitization

### Overview

The `InputValidator` class provides protection against:

- **SQL injection** attacks
- **Command injection** attacks  
- **Script injection** (XSS) attacks
- **Control characters** and malicious input
- **Length violations**

### Basic Usage

```csharp
using LangChainPipeline.Core.Security;

var validator = new InputValidator();
var context = ValidationContext.Default;

var result = validator.ValidateAndSanitize(userInput, context);

if (result.IsValid)
{
    // Use result.SanitizedValue safely
    ProcessInput(result.SanitizedValue);
}
else
{
    // Handle validation errors
    foreach (var error in result.Errors)
    {
        Log.Warning("Input validation failed: {Error}", error);
    }
}
```

### Validation Contexts

Three built-in contexts are provided:

1. **Default** - General text input with reasonable limits
2. **Strict** - For sensitive operations with HTML escaping
3. **ToolParameter** - Specifically for tool parameters

```csharp
// Strict validation for sensitive operations
var result = validator.ValidateAndSanitize(
    userInput, 
    ValidationContext.Strict
);

// Custom validation rules
var customContext = new ValidationContext
{
    MaxLength = 1000,
    MinLength = 10,
    AllowEmpty = false,
    TrimWhitespace = true,
    EscapeHtml = true,
    BlockedCharacters = new HashSet<char> { '<', '>', '&' }
};
```

### Detected Injection Patterns

The validator detects common attack patterns:

**SQL Injection:**
- `'; DROP TABLE`
- `' OR '1'='1`
- `UNION SELECT`
- SQL comments (`--`, `/*`)

**Command Injection:**
- Shell operators (`&&`, `||`, `;`, `|`)
- Command substitution (`` ` ``, `$()`)
- Path traversal (`../`, `/etc/`)

**Script Injection:**
- `<script>` tags
- `javascript:` protocol
- Event handlers (`onerror=`, `onload=`)
- `<iframe>` tags

### Best Practices

1. **Always validate external input** before processing
2. **Use appropriate validation contexts** for different input types
3. **Log validation failures** for security monitoring
4. **Combine with authentication** for defense in depth
5. **Sanitize output** when displaying user content

## Testing

All configuration and security features are covered by unit tests:

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~InputValidatorTests"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## CI/CD Integration

The GitHub Actions workflow automatically:

1. Builds the project
2. Runs all xUnit tests
3. Publishes test results
4. Fails the build if tests fail

See `.github/workflows/dotnet-desktop.yml` for configuration.
