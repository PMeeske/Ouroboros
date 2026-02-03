# Anthropic Claude Integration Guide

This guide explains how to configure and use Anthropic Claude models with Ouroboros.

## Prerequisites

1. **Anthropic API Key** - Get one from [Anthropic Console](https://console.anthropic.com/)
2. **.NET 10.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)

## Quick Start

### 1. Store Your API Key Securely

Use .NET User Secrets for development (recommended):

```bash
cd src/Ouroboros.CLI
dotnet user-secrets init
dotnet user-secrets set "ANTHROPIC_API_KEY" "sk-ant-api03-your-key-here"
```

Or use an environment variable:

```bash
# Windows PowerShell
$env:ANTHROPIC_API_KEY = "sk-ant-api03-your-key-here"

# Linux/macOS
export ANTHROPIC_API_KEY="sk-ant-api03-your-key-here"
```

### 2. Run with Claude

```bash
cd src/Ouroboros.CLI

# Basic usage
dotnet run -- ask -q "What is functional programming?" \
  --endpoint-type anthropic \
  --model claude-sonnet-4-20250514

# With cost tracking
dotnet run -- ask -q "Explain monads" \
  --endpoint-type anthropic \
  --model claude-sonnet-4-20250514 \
  --show-costs --cost-summary
```

## Available Models

### Claude 4 Family (Latest)
| Model | Best For | Input Cost | Output Cost |
|-------|----------|------------|-------------|
| `claude-opus-4-20250514` | Complex analysis, research | $15.00/1M | $75.00/1M |
| `claude-sonnet-4-20250514` | Balanced performance | $3.00/1M | $15.00/1M |

### Claude 3.5 Family
| Model | Best For | Input Cost | Output Cost |
|-------|----------|------------|-------------|
| `claude-3-5-sonnet-20241022` | General purpose | $3.00/1M | $15.00/1M |
| `claude-3-5-haiku-20241022` | Fast, lightweight tasks | $0.80/1M | $4.00/1M |

### Claude 3 Family
| Model | Best For | Input Cost | Output Cost |
|-------|----------|------------|-------------|
| `claude-3-opus-20240229` | Complex reasoning | $15.00/1M | $75.00/1M |
| `claude-3-sonnet-20240229` | Balanced | $3.00/1M | $15.00/1M |
| `claude-3-haiku-20240307` | Quick responses | $0.25/1M | $1.25/1M |

## CLI Options

### Endpoint Configuration

```bash
--endpoint-type anthropic    # Use Anthropic API
--model <model-name>         # Specify Claude model
--endpoint <url>             # Custom endpoint (default: https://api.anthropic.com)
```

### Cost Tracking

```bash
--show-costs      # Display cost after each response
--cost-aware      # Inject token efficiency hints into prompts
--cost-summary    # Show session total cost on exit
```

### Example Commands

```bash
# Use Claude Opus for complex tasks
dotnet run -- orchestrator \
  --goal "Analyze this codebase and suggest architectural improvements" \
  --endpoint-type anthropic \
  --model claude-opus-4-20250514 \
  --show-costs

# Use Haiku for quick operations
dotnet run -- ask -q "Summarize this briefly" \
  --endpoint-type anthropic \
  --model claude-3-5-haiku-20241022

# Pipeline with Claude
dotnet run -- pipeline \
  -d "SetTopic('AI Ethics') | UseDraft | UseCritique | UseImprove" \
  --endpoint-type anthropic \
  --model claude-sonnet-4-20250514 \
  --show-costs
```

## Configuration Resolution

The API key is resolved in the following order:

1. **User Secrets** - `ANTHROPIC_API_KEY` from .NET User Secrets
2. **Configuration** - `ANTHROPIC_API_KEY` from appsettings.json
3. **Environment Variable** - `ANTHROPIC_API_KEY` environment variable
4. **Generic Fallback** - `CHAT_API_KEY` environment variable

## Programmatic Usage

```csharp
using LangChainPipeline.Providers.Anthropic;

// Create Anthropic chat model
var config = new ChatConfig
{
    Endpoint = "https://api.anthropic.com",
    EndpointType = ChatEndpointType.Anthropic,
    ModelId = "claude-sonnet-4-20250514"
};

// Set IConfiguration for secrets resolution
ChatConfig.SetConfiguration(configuration);

var chatModel = ChatConfig.CreateChatModel(config);

// Use in pipeline
var result = await chatModel.GenerateAsync(new ChatHistory
{
    Messages = { new ChatMessage(ChatRole.User, "Hello, Claude!") }
});
```

## Cost Tracking Integration

```csharp
using LangChainPipeline.Providers;

// Create cost tracker
var costTracker = new LlmCostTracker();

// Create cost-aware chat model
var chatModel = ChatConfig.CreateChatModel(config);
if (chatModel is ICostAwareChatModel costAware)
{
    costAware.SetCostTracker(costTracker);
}

// After interactions, get cost summary
Console.WriteLine($"Total cost: ${costTracker.TotalCost:F6}");
Console.WriteLine($"Input tokens: {costTracker.TotalInputTokens}");
Console.WriteLine($"Output tokens: {costTracker.TotalOutputTokens}");
```

## Extended Thinking (Beta)

For complex reasoning tasks, Claude supports extended thinking mode:

```csharp
var response = await anthropicModel.GenerateWithExtendedThinkingAsync(
    history,
    budgetTokens: 10000,  // Max thinking tokens
    cancellationToken
);
```

Note: Extended thinking is currently a beta feature and incurs additional token costs.

## Troubleshooting

### "API key not found"

Ensure your API key is properly configured:

```bash
# Verify user secrets
dotnet user-secrets list

# Or check environment variable
echo $ANTHROPIC_API_KEY  # Linux/macOS
$env:ANTHROPIC_API_KEY   # PowerShell
```

### "Model not found"

Verify the model name is correct. Use the exact model identifiers from Anthropic's documentation.

### Rate Limits

Anthropic applies rate limits based on your API tier. If you encounter rate limit errors:
- Reduce request frequency
- Consider using a smaller model for less critical tasks
- Check your usage at [console.anthropic.com](https://console.anthropic.com/)

## Security Best Practices

1. **Never commit API keys** to source control
2. **Use User Secrets** for development
3. **Use environment variables** or **Azure Key Vault** for production
4. **Rotate keys periodically** via the Anthropic Console
5. **Set usage limits** in the Anthropic Console to prevent unexpected costs

## See Also

- [Ouroboros README](../README.md)
- [Configuration and Security](../CONFIGURATION_AND_SECURITY.md)
- [Anthropic API Documentation](https://docs.anthropic.com/)
- [Claude Model Overview](https://docs.anthropic.com/en/docs/models-overview)
