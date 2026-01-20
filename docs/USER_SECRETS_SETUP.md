# User Secrets Setup for Ouroboros Test Projects

This guide explains how to configure user secrets for accessing the Ollama DeepSeek cloud endpoint API in both local development and CI/CD environments.

## Table of Contents

- [Overview](#overview)
- [Local Development Setup](#local-development-setup)
- [CI/CD Configuration](#cicd-configuration)
- [Configuration Structure](#configuration-structure)
- [Test Projects](#test-projects)
- [Usage in Tests](#usage-in-tests)
- [Troubleshooting](#troubleshooting)

## Overview

User secrets provide a secure way to store sensitive configuration data like API keys:

- **Local Development**: Uses .NET user secrets stored outside the project directory
- **CI/CD**: Uses GitHub Actions repository secrets mapped to environment variables
- **Configuration Priority**: Environment variables > User secrets > appsettings.Test.json > appsettings.json

All test projects are configured with:
- Unique UserSecretsId for isolation
- Microsoft.Extensions.Configuration packages for flexible configuration
- Support for JSON files, user secrets, and environment variables

## Local Development Setup

### Step 1: Initialize User Secrets

Initialize user secrets for each test project you'll be working with:

```bash
# Ouroboros.Tests.Shared (shared test infrastructure)
dotnet user-secrets init --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj

# Ouroboros.Tests (main test suite)
dotnet user-secrets init --project src/Ouroboros.Tests/Ouroboros.Tests.csproj

# Ouroboros.Tests.UnitTests (unit tests)
dotnet user-secrets init --project src/Ouroboros.Tests.UnitTests/Ouroboros.Tests.UnitTests.csproj

# Ouroboros.Tests.Integration (integration tests)
dotnet user-secrets init --project src/Ouroboros.Tests.Integration/Ouroboros.Tests.Integration.csproj

# Ouroboros.Tests.Bdd (BDD/SpecFlow tests)
dotnet user-secrets init --project src/Ouroboros.Tests.Bdd/Ouroboros.Tests.Bdd.csproj

# Ouroboros.Android.Tests (Android UI tests)
dotnet user-secrets init --project src/Ouroboros.Android.Tests/Ouroboros.Android.Tests.csproj
```

### Step 2: Set API Key

Set the Ollama DeepSeek API key for each project:

```bash
# For Ouroboros.Tests.Shared
dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj

# For Ouroboros.Tests
dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Tests/Ouroboros.Tests.csproj

# For Ouroboros.Tests.UnitTests
dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Tests.UnitTests/Ouroboros.Tests.UnitTests.csproj

# For Ouroboros.Tests.Integration
dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Tests.Integration/Ouroboros.Tests.Integration.csproj

# For Ouroboros.Tests.Bdd
dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Tests.Bdd/Ouroboros.Tests.Bdd.csproj

# For Ouroboros.Android.Tests
dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Android.Tests/Ouroboros.Android.Tests.csproj
```

### Step 3: Verify Configuration

List the secrets to verify they were set correctly:

```bash
dotnet user-secrets list --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj
```

Expected output:
```
Ollama:ApiKey = your-api-key-here
```

### Step 4: Optional - Set Additional Configuration

You can also override other settings:

```bash
# Override the endpoint (if using a different DeepSeek instance)
dotnet user-secrets set "Ollama:DeepSeekCloudEndpoint" "https://custom-api.deepseek.com/v1" --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj

# Override the default chat model
dotnet user-secrets set "Pipeline:LlmProvider:DefaultChatModel" "deepseek-v3:custom" --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj
```

## CI/CD Configuration

### GitHub Actions Repository Secrets

1. Navigate to your GitHub repository
2. Go to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add the following secret:
   - **Name**: `OLLAMA_DEEPSEEK_API_KEY`
   - **Value**: Your DeepSeek API key

### GitHub Actions Workflow Configuration

Add the following environment variable mapping to your workflow file (`.github/workflows/test.yml`):

```yaml
name: Test Suite

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    env:
      # Map GitHub secret to environment variable
      # The double underscore (__) syntax is used for nested configuration keys
      Ollama__ApiKey: ${{ secrets.OLLAMA_DEEPSEEK_API_KEY }}
      Ollama__DeepSeekCloudEndpoint: "https://api.deepseek.com/v1"
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Run Unit Tests
        run: dotnet test src/Ouroboros.Tests.UnitTests --no-build --configuration Release --verbosity normal
      
      - name: Run Integration Tests
        run: dotnet test src/Ouroboros.Tests.Integration --no-build --configuration Release --verbosity normal
      
      - name: Run BDD Tests
        run: dotnet test src/Ouroboros.Tests.Bdd --no-build --configuration Release --verbosity normal
```

### Environment Variable Naming Convention

.NET Configuration uses the **double underscore (`__`)** syntax to represent nested configuration keys:

- `Ollama:ApiKey` → `Ollama__ApiKey`
- `Ollama:DeepSeekCloudEndpoint` → `Ollama__DeepSeekCloudEndpoint`
- `Pipeline:LlmProvider:DefaultChatModel` → `Pipeline__LlmProvider__DefaultChatModel`

## Configuration Structure

### appsettings.Test.json

Located at the repository root (`/home/runner/work/Ouroboros/Ouroboros/appsettings.Test.json`):

```json
{
  "Ollama": {
    "DeepSeekCloudEndpoint": "https://api.deepseek.com/v1",
    "ApiKey": ""
  },
  "Pipeline": {
    "LlmProvider": {
      "DefaultProvider": "Ollama",
      "OllamaEndpoint": "https://api.deepseek.com/v1",
      "DefaultChatModel": "deepseek-v3.1:671b-cloud"
    }
  }
}
```

**Note**: The `ApiKey` field is intentionally empty. Actual keys should be stored in user secrets (local) or environment variables (CI/CD).

### Configuration Keys

| Key | Description | Default Value |
|-----|-------------|---------------|
| `Ollama:ApiKey` | DeepSeek API key (secret) | `""` |
| `Ollama:DeepSeekCloudEndpoint` | API endpoint URL | `https://api.deepseek.com/v1` |
| `Pipeline:LlmProvider:DefaultProvider` | LLM provider name | `Ollama` |
| `Pipeline:LlmProvider:OllamaEndpoint` | Ollama endpoint URL | `https://api.deepseek.com/v1` |
| `Pipeline:LlmProvider:DefaultChatModel` | Default chat model | `deepseek-v3.1:671b-cloud` |

## Test Projects

All test projects are configured with user secrets support:

| Project | UserSecretsId | Purpose |
|---------|--------------|---------|
| `Ouroboros.Tests.Shared` | `ouroboros-tests-shared` | Shared test infrastructure |
| `Ouroboros.Tests` | `ouroboros-tests` | Main test suite |
| `Ouroboros.Tests.UnitTests` | `ouroboros-tests-unit` | Unit tests |
| `Ouroboros.Tests.Integration` | `ouroboros-tests-integration` | Integration tests |
| `Ouroboros.Tests.Bdd` | `ouroboros-tests-bdd` | BDD/SpecFlow tests |
| `Ouroboros.Android.Tests` | `ouroboros-tests-android` | Android UI tests |

Each project has:
- Unique `UserSecretsId` for isolation
- Configuration packages (UserSecrets, Json, EnvironmentVariables)
- Access to the centralized `TestConfiguration` helper

## Usage in Tests

### Using TestConfiguration Helper

The `TestConfiguration` class (in `Ouroboros.Tests.Shared`) provides centralized configuration management:

```csharp
using Ouroboros.Tests.Shared.Configuration;
using Microsoft.Extensions.Configuration;

public class MyIntegrationTest
{
    private readonly IConfiguration _configuration;

    public MyIntegrationTest()
    {
        // Build configuration with all sources
        _configuration = TestConfiguration.BuildConfiguration();
    }

    [Fact]
    public async Task TestWithOllamaApi()
    {
        // Retrieve configuration values
        var apiKey = _configuration["Ollama:ApiKey"];
        var endpoint = _configuration["Ollama:DeepSeekCloudEndpoint"];
        var model = _configuration["Pipeline:LlmProvider:DefaultChatModel"];

        // Use in your test
        var client = new OllamaClient(endpoint, apiKey);
        var response = await client.GenerateAsync(model, "Test prompt");

        Assert.NotNull(response);
    }
}
```

### Configuration Priority

The `TestConfiguration.BuildConfiguration()` method loads configuration in this order (highest priority first):

1. **Environment Variables** (highest priority - CI/CD)
2. **User Secrets** (local development)
3. **appsettings.Test.json** (test-specific defaults)
4. **appsettings.json** (general defaults, lowest priority)

### Manual Configuration

If you prefer manual configuration:

```csharp
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Test.json", optional: true)
    .AddUserSecrets<MyTestClass>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["Ollama:ApiKey"];
```

## Troubleshooting

### Issue: API Key Not Found

**Symptoms**: Tests fail with "API key not configured" or `null` API key.

**Solutions**:

1. **Local Development**:
   ```bash
   # Verify secrets are set
   dotnet user-secrets list --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj
   
   # If empty, set the API key
   dotnet user-secrets set "Ollama:ApiKey" "your-api-key-here" --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj
   ```

2. **CI/CD**:
   - Verify the GitHub Actions secret `OLLAMA_DEEPSEEK_API_KEY` is set in repository settings
   - Check that the workflow file maps the secret to `Ollama__ApiKey` environment variable
   - Review workflow logs for configuration loading errors

### Issue: Configuration Not Loading

**Symptoms**: Configuration values are always defaults, even with secrets set.

**Solutions**:

1. Ensure you're using `TestConfiguration.BuildConfiguration()` or manually adding all configuration sources
2. Check that the project has `<UserSecretsId>` in the `.csproj` file
3. Verify configuration packages are installed:
   - Microsoft.Extensions.Configuration.UserSecrets
   - Microsoft.Extensions.Configuration.Json
   - Microsoft.Extensions.Configuration.EnvironmentVariables

### Issue: Wrong Configuration Priority

**Symptoms**: Environment variable doesn't override user secret.

**Solution**: Configuration sources are loaded in the order they're added to `ConfigurationBuilder`. The **last source added has the highest priority**. Our setup loads environment variables last, so they override everything else.

### Issue: UserSecretsId Conflict

**Symptoms**: Secrets from one project appear in another.

**Solution**: Each test project has a unique `UserSecretsId`. If you need to share secrets across projects, either:
- Use the same `UserSecretsId` (not recommended)
- Set secrets for each project individually
- Use environment variables instead

### Viewing User Secrets Location

User secrets are stored at:

- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **Linux/macOS**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

You can manually edit these files if needed, but using `dotnet user-secrets` is recommended.

### Clearing User Secrets

To remove all secrets for a project:

```bash
dotnet user-secrets clear --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj
```

To remove a specific secret:

```bash
dotnet user-secrets remove "Ollama:ApiKey" --project src/Ouroboros.Tests.Shared/Ouroboros.Tests.Shared.csproj
```

## Security Best Practices

1. **Never commit API keys** to source control
2. **Never log API keys** in test output or error messages
3. **Rotate API keys** regularly
4. **Use separate API keys** for development, testing, and production
5. **Limit API key permissions** to the minimum required scope
6. **Monitor API usage** for unusual activity

## Additional Resources

- [.NET User Secrets Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [GitHub Actions Secrets Documentation](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [Microsoft.Extensions.Configuration Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [DeepSeek API Documentation](https://platform.deepseek.com/docs)

## Support

For issues or questions:
1. Check this documentation first
2. Review the troubleshooting section
3. Open an issue in the repository with:
   - Description of the problem
   - Steps to reproduce
   - Environment (local vs CI/CD)
   - Configuration code snippet (without secrets!)
