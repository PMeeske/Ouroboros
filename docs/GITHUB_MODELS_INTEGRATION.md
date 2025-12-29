# GitHub Models Integration Guide

This guide explains how to use Ouroboros with GitHub Models API, which provides access to various state-of-the-art AI models including GPT-4o, o1-preview, Claude 3.5 Sonnet, Llama 3.1, Mistral, and more.

## Overview

GitHub Models is a hosted AI service that provides access to multiple leading AI models through an OpenAI-compatible API. It uses GitHub Personal Access Tokens (PAT) for authentication, making it easy to integrate with your existing GitHub workflow.

### Supported Models

GitHub Models provides access to:

- **OpenAI Models**: `gpt-4o`, `gpt-4o-mini`, `o1-preview`, `o1-mini`
- **Anthropic Models**: `claude-3-5-sonnet`, `claude-3-haiku`, `claude-3-opus`
- **Meta Models**: `llama-3.1-70b-instruct`, `llama-3.1-405b-instruct`
- **Mistral Models**: `mistral-large`, `mistral-small`, `mistral-nemo`
- **Cohere Models**: `command-r`, `command-r-plus`
- **Other Models**: `phi-3-medium`, `phi-3-mini`

## Quick Start

### 1. Obtain a GitHub Personal Access Token

To use GitHub Models, you need a GitHub Personal Access Token:

1. Go to [GitHub Settings > Developer settings > Personal access tokens > Tokens (classic)](https://github.com/settings/tokens)
2. Click "Generate new token (classic)"
3. Give your token a descriptive name (e.g., "Ouroboros GitHub Models")
4. Select the required scopes:
   - For public repositories: No additional scopes needed
   - For private repositories: Select `repo` scope
5. Click "Generate token"
6. **Important**: Copy the token immediately - you won't be able to see it again!

### 2. Set Up Environment Variables

The simplest way to configure GitHub Models is through environment variables:

```bash
# Set your GitHub token
export GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"

# Optional: Override the default endpoint
export GITHUB_MODELS_ENDPOINT="https://models.inference.ai.azure.com"

# Optional: Set endpoint type explicitly
export CHAT_ENDPOINT_TYPE="github-models"
```

Alternatively, you can use `GITHUB_MODELS_TOKEN` instead of `GITHUB_TOKEN`.

### 3. Using with CLI

Once configured, you can use GitHub Models with any Ouroboros CLI command:

#### Ask Command

```bash
cd src/Ouroboros.CLI

# Basic question with auto-detection (detects GitHub Models from endpoint)
export GITHUB_TOKEN="ghp_xxx"
export CHAT_ENDPOINT="https://models.inference.ai.azure.com"
dotnet run -- ask -q "Explain functional programming in simple terms" --model gpt-4o

# Explicit endpoint type
dotnet run -- ask -q "What is category theory?" \
  --endpoint "https://models.inference.ai.azure.com" \
  --api-key "ghp_xxx" \
  --endpoint-type github-models \
  --model gpt-4o

# Using Claude 3.5 Sonnet
dotnet run -- ask -q "Write a Python function to calculate Fibonacci numbers" \
  --endpoint-type github-models \
  --model claude-3-5-sonnet

# Using Llama 3.1 70B
dotnet run -- ask -q "Explain monads in functional programming" \
  --endpoint-type github-models \
  --model llama-3.1-70b-instruct
```

#### Pipeline Command

```bash
# Run a pipeline with GitHub Models
export GITHUB_TOKEN="ghp_xxx"
export CHAT_ENDPOINT="https://models.inference.ai.azure.com"

dotnet run -- pipeline \
  -d "SetTopic('AI Ethics') | UseDraft | UseCritique" \
  --model gpt-4o \
  --endpoint-type github-models

# With custom settings
dotnet run -- pipeline \
  -d "SetTopic('Category Theory') | UseDraft" \
  --model o1-preview \
  --endpoint-type github-models \
  --temperature 0.7 \
  --max-tokens 2000
```

#### Orchestrator Command

```bash
# Use orchestrator with GitHub Models
dotnet run -- orchestrator \
  --goal "Explain the concept of monads in functional programming" \
  --endpoint-type github-models \
  --model gpt-4o

# With Claude
dotnet run -- orchestrator \
  --goal "Design a scalable microservices architecture" \
  --endpoint-type github-models \
  --model claude-3-5-sonnet
```

#### MeTTa Command

```bash
# Use MeTTa orchestrator with GitHub Models
dotnet run -- metta \
  --goal "Plan a data pipeline for real-time analytics" \
  --endpoint-type github-models \
  --model gpt-4o-mini
```

## Programmatic Usage

### Basic Usage

```csharp
using LangChainPipeline.Providers;

// Create a GitHub Models chat model
var chatModel = new GitHubModelsChatModel(
    githubToken: "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    model: "gpt-4o"
);

// Generate a response
string response = await chatModel.GenerateTextAsync(
    "Explain category theory in simple terms"
);

Console.WriteLine(response);

// Dispose when done
chatModel.Dispose();
```

### Using Environment Variables

```csharp
using LangChainPipeline.Providers;

// Create from environment (reads GITHUB_TOKEN or GITHUB_MODELS_TOKEN)
var chatModel = GitHubModelsChatModel.FromEnvironment("gpt-4o");

string response = await chatModel.GenerateTextAsync("What is a monad?");
Console.WriteLine(response);
```

### Custom Endpoint and Settings

```csharp
using LangChainPipeline.Providers;

// Custom settings
var settings = new ChatRuntimeSettings(
    temperature: 0.7,
    maxTokens: 1000,
    timeoutSeconds: 60,
    stream: false
);

// Create with custom endpoint
var chatModel = new GitHubModelsChatModel(
    githubToken: Environment.GetEnvironmentVariable("GITHUB_TOKEN")!,
    model: "claude-3-5-sonnet",
    endpoint: "https://models.inference.ai.azure.com",
    settings: settings
);

string response = await chatModel.GenerateTextAsync(
    "Write a functional programming tutorial"
);

Console.WriteLine(response);
chatModel.Dispose();
```

### Streaming Responses

```csharp
using LangChainPipeline.Providers;
using System.Reactive.Linq;

var chatModel = GitHubModelsChatModel.FromEnvironment("gpt-4o");

// Stream the response token by token
IObservable<string> stream = chatModel.StreamReasoningContent(
    "Explain the principles of functional programming"
);

await stream.ForEachAsync(token => 
{
    Console.Write(token);
});

Console.WriteLine(); // New line after streaming completes
chatModel.Dispose();
```

### Integration with ToolAwareChatModel

```csharp
using LangChainPipeline.Providers;
using LangChainPipeline.Tools;

// Create GitHub Models provider
var githubModels = new GitHubModelsChatModel(
    githubToken: Environment.GetEnvironmentVariable("GITHUB_TOKEN")!,
    model: "gpt-4o"
);

// Create tool registry
var tools = new ToolRegistry()
    .WithFunction("echo", "Echo back input", (string s) => s)
    .WithFunction("uppercase", "Convert to uppercase", (string s) => s.ToUpperInvariant());

// Wrap with tool awareness
var toolAwareChatModel = new ToolAwareChatModel(githubModels, tools);

// Now the model can use tools
string response = await toolAwareChatModel.GenerateTextAsync(
    "Use the uppercase tool to convert 'hello world' to uppercase"
);

Console.WriteLine(response);
```

## Configuration Options

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MODEL_TOKEN` | Primary token for GitHub Models API (highest priority) | None |
| `GITHUB_TOKEN` | GitHub Personal Access Token | Falls back to MODEL_TOKEN |
| `GITHUB_MODELS_TOKEN` | Alternative to GITHUB_TOKEN | Falls back to GITHUB_TOKEN |
| `GITHUB_MODELS_ENDPOINT` | API endpoint URL | `https://models.inference.ai.azure.com` |
| `CHAT_ENDPOINT` | Generic endpoint (works with all providers) | None |
| `CHAT_API_KEY` | Generic API key (works with all providers) | None |
| `CHAT_ENDPOINT_TYPE` | Force endpoint type | `auto` (auto-detects) |

### CLI Options

| Option | Description | Default |
|--------|-------------|---------|
| `--endpoint` | API endpoint URL | From env vars |
| `--api-key` | GitHub Personal Access Token | From env vars |
| `--endpoint-type` | Force endpoint type: `github-models`, `auto`, etc. | `auto` |
| `--model` | Model name (e.g., `gpt-4o`, `claude-3-5-sonnet`) | Varies by command |
| `--temperature` | Sampling temperature (0.0-2.0) | 0.7 |
| `--max-tokens` | Maximum tokens in response | 512 |
| `--timeout-seconds` | HTTP request timeout | 60 |

### Runtime Settings

```csharp
var settings = new ChatRuntimeSettings(
    temperature: 0.7,      // Controls randomness (0.0 = deterministic, 2.0 = very random)
    maxTokens: 1000,       // Maximum tokens in response
    timeoutSeconds: 60,    // HTTP timeout
    stream: true           // Enable streaming
);
```

## Model Selection Guide

### For Code Generation and Technical Tasks
- **Best**: `gpt-4o`, `claude-3-5-sonnet`
- **Fast/Cost-effective**: `gpt-4o-mini`, `claude-3-haiku`

### For Reasoning and Problem-Solving
- **Best**: `o1-preview`, `gpt-4o`, `claude-3-opus`
- **Fast**: `o1-mini`, `gpt-4o-mini`

### For Long-Form Content
- **Best**: `claude-3-5-sonnet`, `claude-3-opus`
- **Alternative**: `gpt-4o`, `mistral-large`

### For Open Source Models
- **Best**: `llama-3.1-405b-instruct`
- **Balanced**: `llama-3.1-70b-instruct`
- **Fast**: `phi-3-medium`, `mistral-nemo`

## Advanced Features

### Retry Policy and Error Handling

The GitHub Models provider includes built-in retry logic using Polly:

- Automatically retries on rate limiting (HTTP 429)
- Automatically retries on server errors (HTTP 5xx)
- Uses exponential backoff (2^retryCount seconds)
- Maximum 3 retry attempts

```csharp
// The retry policy is automatic - no configuration needed
var chatModel = GitHubModelsChatModel.FromEnvironment("gpt-4o");

// Will automatically retry on transient failures
string response = await chatModel.GenerateTextAsync("Your prompt here");
```

### Auto-Detection

The system automatically detects GitHub Models when:

1. The endpoint URL contains `models.inference.ai.azure.com`
2. The endpoint URL contains `github`
3. The `--endpoint-type` is set to `github-models` or `github`

```bash
# These all use GitHub Models automatically:
export CHAT_ENDPOINT="https://models.inference.ai.azure.com"
export GITHUB_TOKEN="ghp_xxx"

dotnet run -- ask -q "Hello" --model gpt-4o
# Auto-detects GitHub Models from endpoint URL

dotnet run -- ask -q "Hello" --endpoint-type github-models --model gpt-4o
# Explicit endpoint type

dotnet run -- ask -q "Hello" --endpoint-type github --model gpt-4o
# Shorthand also works
```

### Using with .env File

For persistent configuration, create a `.env` file in your project root:

```bash
# .env
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
CHAT_ENDPOINT=https://models.inference.ai.azure.com
CHAT_ENDPOINT_TYPE=github-models
```

Then use a tool like `dotenv` to load it before running commands.

## Troubleshooting

### Authentication Errors

**Problem**: `401 Unauthorized` or authentication failures

**Solutions**:
1. Verify your GitHub token is valid and not expired
2. Ensure the token has the required scopes
3. Check that the token is correctly set in the environment variable
4. Make sure there are no extra spaces or quotes around the token

```bash
# Verify token is set correctly
echo $GITHUB_TOKEN | wc -c  # Should be around 40+ characters

# Test with explicit token
dotnet run -- ask -q "Test" --api-key "ghp_xxx" --endpoint-type github-models --model gpt-4o
```

### Rate Limiting

**Problem**: `429 Too Many Requests` errors

**Solutions**:
1. The provider automatically retries with exponential backoff
2. If you still hit limits, add delays between requests
3. Consider using a different model with higher rate limits
4. Check GitHub's rate limit documentation

### Model Not Found

**Problem**: `404 Not Found` or model not available

**Solutions**:
1. Verify the model name is correct (case-sensitive)
2. Check if the model is available in your region
3. Ensure you have access to the model
4. Try with a known model like `gpt-4o` or `gpt-4o-mini`

### Timeout Errors

**Problem**: Requests timing out

**Solutions**:
1. Increase timeout value: `--timeout-seconds 120`
2. Try a faster model like `gpt-4o-mini`
3. Reduce `--max-tokens` value
4. Check your network connection

```bash
# Increase timeout for long requests
dotnet run -- ask -q "Long prompt..." \
  --endpoint-type github-models \
  --model gpt-4o \
  --timeout-seconds 180 \
  --max-tokens 2000
```

## Comparison with Other Providers

| Feature | GitHub Models | Ollama Cloud | OpenAI | LiteLLM |
|---------|--------------|--------------|--------|---------|
| Authentication | GitHub PAT | API Key | API Key | API Key |
| Endpoint | `models.inference.ai.azure.com` | `api.ollama.com` | `api.openai.com` | Custom |
| API Format | OpenAI-compatible | Ollama native | OpenAI native | OpenAI-compatible |
| Models | Multiple providers | Ollama models | OpenAI models | Any LLM |
| Streaming | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| Free Tier | Limited | Yes | No | Depends |

## Best Practices

1. **Use Environment Variables**: Store tokens in environment variables, never in code
2. **Model Selection**: Choose the right model for your use case to balance cost and quality
3. **Error Handling**: Always handle potential errors and implement fallbacks
4. **Rate Limiting**: Be mindful of rate limits, especially in production
5. **Token Management**: Rotate tokens regularly and revoke unused ones
6. **Streaming**: Use streaming for better user experience with long responses
7. **Testing**: Test with smaller models (`gpt-4o-mini`) before using premium models

## Examples

### Complete Example with Error Handling

```csharp
using LangChainPipeline.Providers;

try
{
    var chatModel = GitHubModelsChatModel.FromEnvironment("gpt-4o");
    
    var settings = new ChatRuntimeSettings(
        temperature: 0.7,
        maxTokens: 500,
        timeoutSeconds: 60,
        stream: false
    );
    
    string response = await chatModel.GenerateTextAsync(
        "Explain monads in functional programming"
    );
    
    Console.WriteLine($"Response: {response}");
    chatModel.Dispose();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    Console.Error.WriteLine("Make sure GITHUB_TOKEN is set in environment variables");
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
    Console.Error.WriteLine("Check your internet connection and endpoint URL");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
}
```

### Multi-Model Pipeline

```csharp
using LangChainPipeline.Providers;

// Use different models for different tasks
var draftModel = new GitHubModelsChatModel(
    githubToken: Environment.GetEnvironmentVariable("GITHUB_TOKEN")!,
    model: "gpt-4o-mini"  // Fast model for drafts
);

var critiqueModel = new GitHubModelsChatModel(
    githubToken: Environment.GetEnvironmentVariable("GITHUB_TOKEN")!,
    model: "gpt-4o"  // Premium model for critique
);

// Generate draft
string draft = await draftModel.GenerateTextAsync(
    "Write an introduction to functional programming"
);

// Critique the draft
string critique = await critiqueModel.GenerateTextAsync(
    $"Critique this introduction and suggest improvements:\n\n{draft}"
);

Console.WriteLine("Draft:");
Console.WriteLine(draft);
Console.WriteLine("\nCritique:");
Console.WriteLine(critique);

draftModel.Dispose();
critiqueModel.Dispose();
```

## Additional Resources

- [GitHub Models Documentation](https://docs.github.com/en/github-models)
- [GitHub PAT Documentation](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
- [Ouroboros Documentation](../README.md)

## Support

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Review the [GitHub Models status page](https://githubstatus.com)
3. Open an issue on the [Ouroboros GitHub repository](https://github.com/PMeeske/Ouroboros/issues)
4. Check existing issues for similar problems

## Security Considerations

1. **Token Security**:
   - Never commit tokens to version control
   - Use environment variables or secure vaults
   - Rotate tokens regularly
   - Revoke tokens when no longer needed

2. **Network Security**:
   - Always use HTTPS (default)
   - Be cautious with proxy settings
   - Validate endpoint URLs

3. **Data Privacy**:
   - Be aware that prompts are sent to GitHub's servers
   - Don't send sensitive or private data
   - Review GitHub's privacy policy and terms of service

4. **Access Control**:
   - Limit token scopes to minimum required
   - Use separate tokens for different applications
   - Monitor token usage through GitHub settings
