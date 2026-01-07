# Provider Load Balancing

Comprehensive load balancing and provider rotation system to handle rate limiting (HTTP 429), improve reliability, and optimize performance through intelligent provider distribution. Uses **Polly** for resilient retry logic with exponential backoff.

## Overview

The Provider Load Balancing system automatically distributes requests across multiple provider instances, handling rate limits gracefully and maintaining high availability even when individual providers become unavailable. Built on top of Polly's battle-tested resilience patterns.

## Key Features

- ✅ **Automatic Rate Limit Handling**: Detects HTTP 429 errors and applies cooldown periods
- ✅ **Circuit Breaker**: Marks providers unhealthy after consecutive failures
- ✅ **Multiple Strategies**: RoundRobin, WeightedRandom, LeastLatency, AdaptiveHealth
- ✅ **Health Tracking**: Real-time metrics for latency, success rates, and availability
- ✅ **Polly Integration**: Exponential backoff retry policies (1s, 2s, 4s intervals)
- ✅ **Automatic Recovery**: Providers automatically recover after cooldown expires

## Quick Start

### Basic Setup

```csharp
using Ouroboros.Providers.LoadBalancing;

// Create load-balanced chat model
var loadBalancedModel = new LoadBalancedChatModel(
    ProviderRotationStrategy.AdaptiveHealth);

// Register multiple providers
loadBalancedModel.RegisterProvider("provider-1", provider1);
loadBalancedModel.RegisterProvider("provider-2", provider2);
loadBalancedModel.RegisterProvider("provider-3", provider3);

// Use it like any IChatCompletionModel
string response = await loadBalancedModel.GenerateTextAsync("Hello!");
```

### Multi-Provider Configuration

```csharp
// Primary provider (might get rate limited under heavy load)
var primary = new LiteLLMChatModel(
    "https://api.litellm.com",
    "primary-api-key",
    "gpt-4");

// Backup providers
var backup1 = new LiteLLMChatModel(
    "https://api.litellm.com",
    "backup-api-key-1",
    "gpt-4");

var backup2 = new LiteLLMChatModel(
    "https://api.litellm.com",
    "backup-api-key-2",
    "gpt-3.5-turbo");

var loadBalancer = new LoadBalancedChatModel(
    ProviderRotationStrategy.AdaptiveHealth);

loadBalancer.RegisterProvider("primary", primary);
loadBalancer.RegisterProvider("backup-1", backup1);
loadBalancer.RegisterProvider("backup-2", backup2);

// Load balancer automatically fails over when rate limited
for (int i = 0; i < 100; i++)
{
    await loadBalancer.GenerateTextAsync($"Request {i}");
}
```

## Rotation Strategies

### 1. Round Robin
Distributes requests evenly across all healthy providers in sequence.

```csharp
var model = new LoadBalancedChatModel(ProviderRotationStrategy.RoundRobin);
```

**Use Case**: Equal load distribution when all providers have similar characteristics.

### 2. Weighted Random
Probabilistically selects providers based on their health scores.

```csharp
var model = new LoadBalancedChatModel(ProviderRotationStrategy.WeightedRandom);
```

**Use Case**: Gradually shift traffic toward better-performing providers.

### 3. Least Latency
Always selects the provider with the lowest average latency.

```csharp
var model = new LoadBalancedChatModel(ProviderRotationStrategy.LeastLatency);
```

**Use Case**: Performance-critical applications requiring minimum response time.

### 4. Adaptive Health (Recommended)
Selects providers based on composite health score combining success rate (70%) and latency (30%).

```csharp
var model = new LoadBalancedChatModel(ProviderRotationStrategy.AdaptiveHealth);
```

**Use Case**: General purpose load balancing with optimal balance of reliability and performance.

## Rate Limit Handling

The system automatically detects and handles HTTP 429 (Too Many Requests) errors using Polly's resilient retry policies:

1. **Detection**: Recognizes 429 status codes in HTTP exceptions
2. **Cooldown**: Applies 60-second cooldown period to rate-limited provider
3. **Failover**: Immediately routes requests to healthy providers
4. **Polly Retry**: Uses exponential backoff (1s, 2s, 4s) for provider rotation
5. **Recovery**: Automatically restores provider after cooldown expires

```csharp
// Automatic rate limit handling
try
{
    string result = await loadBalancer.GenerateTextAsync(prompt);
    // Success - used healthy provider
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
{
    // Load balancer already handled this internally
    // Request was routed to alternative provider
}
```

## Health Monitoring

### View Health Status

```csharp
var healthStatus = loadBalancer.GetHealthStatus();

foreach (var (providerId, health) in healthStatus)
{
    Console.WriteLine($"{providerId}:");
    Console.WriteLine($"  Healthy: {health.IsHealthy}");
    Console.WriteLine($"  Success Rate: {health.SuccessRate:P0}");
    Console.WriteLine($"  Avg Latency: {health.AverageLatencyMs:F0}ms");
    Console.WriteLine($"  In Cooldown: {health.IsInCooldown}");
    Console.WriteLine($"  Health Score: {health.HealthScore:F2}");
}
```

### Manual Health Management

```csharp
// Manually mark provider unhealthy (e.g., for maintenance)
loadBalancer.MarkProviderUnhealthy("provider-1", TimeSpan.FromMinutes(5));

// Manually restore health
loadBalancer.MarkProviderHealthy("provider-1");
```

## Circuit Breaker

The built-in circuit breaker automatically protects against cascading failures:

- **Threshold**: 3 consecutive failures
- **Action**: Mark provider unhealthy and apply 60-second cooldown
- **Reset**: Success resets consecutive failure counter

```csharp
// Circuit breaker triggers after 3 failures
loadBalancer.RecordExecution("provider-1", 100, false); // Failure 1
loadBalancer.RecordExecution("provider-1", 100, false); // Failure 2
loadBalancer.RecordExecution("provider-1", 100, false); // Failure 3
// Provider-1 now marked unhealthy with cooldown

// Success resets the counter
loadBalancer.RecordExecution("provider-1", 100, true);  // Success
// Consecutive failures reset to 0
```

## Advanced Usage

### Custom Load Balancer

```csharp
// Create custom load balancer with direct access
var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>(
    ProviderRotationStrategy.WeightedRandom);

// Register providers
loadBalancer.RegisterProvider("provider-1", provider1);
loadBalancer.RegisterProvider("provider-2", provider2);

// Select provider manually
var result = await loadBalancer.SelectProviderAsync();
result.Match(
    selection => 
    {
        Console.WriteLine($"Selected: {selection.ProviderId}");
        Console.WriteLine($"Reason: {selection.Reason}");
        return selection.Provider.GenerateTextAsync(prompt);
    },
    error => 
    {
        Console.WriteLine($"Error: {error}");
        return Task.FromResult("[error]");
    });
```

### Metrics Recording

```csharp
// Record custom metrics
loadBalancer.RecordExecution(
    providerId: "provider-1",
    latencyMs: 150.5,
    success: true,
    wasRateLimited: false);
```

## Best Practices

### 1. Register Multiple Providers
Always register at least 2-3 providers to ensure availability during rate limits or failures.

```csharp
// ✅ Good: Multiple providers for redundancy
loadBalancer.RegisterProvider("primary", primary);
loadBalancer.RegisterProvider("backup-1", backup1);
loadBalancer.RegisterProvider("backup-2", backup2);

// ❌ Bad: Single provider defeats the purpose
loadBalancer.RegisterProvider("only-one", provider);
```

### 2. Use Adaptive Health for General Cases
The AdaptiveHealth strategy provides the best balance for most scenarios.

```csharp
// ✅ Recommended
var model = new LoadBalancedChatModel(ProviderRotationStrategy.AdaptiveHealth);
```

### 3. Monitor Health Status
Regularly check health metrics to identify issues early.

```csharp
// Check for unhealthy providers
var unhealthy = loadBalancer.GetHealthStatus()
    .Where(kvp => !kvp.Value.IsHealthy)
    .Select(kvp => kvp.Key)
    .ToList();

if (unhealthy.Any())
{
    Console.WriteLine($"Warning: {unhealthy.Count} unhealthy providers: {string.Join(", ", unhealthy)}");
}
```

### 4. Configure Appropriate API Keys
Use different API keys for each provider to maximize rate limits.

```csharp
// ✅ Good: Separate API keys
RegisterProvider("account-1", new LiteLLMChatModel(endpoint, "api-key-1", model));
RegisterProvider("account-2", new LiteLLMChatModel(endpoint, "api-key-2", model));

// ❌ Bad: Same API key (shared rate limit)
RegisterProvider("dup-1", new LiteLLMChatModel(endpoint, "same-key", model));
RegisterProvider("dup-2", new LiteLLMChatModel(endpoint, "same-key", model));
```

### 5. Handle Exhausted Providers Gracefully
When all providers are exhausted, implement fallback strategies.

```csharp
string result = await loadBalancer.GenerateTextAsync(prompt);

if (result.Contains("[load-balanced-error]"))
{
    // All providers exhausted - implement fallback
    await NotifyAdministrator("All LLM providers unavailable");
    return "I'm temporarily unavailable. Please try again later.";
}
```

## Performance Considerations

### Latency Impact
- Provider selection adds ~1-5ms overhead
- Health checks are performed in-memory (no I/O)
- Metrics use exponential moving averages for efficiency
- Polly retry adds backoff delays: 1s, 2s, 4s (total ~7s on complete failure)

### Memory Usage
- Each provider: ~1KB for health metrics
- Total overhead: ~100KB for 100 providers
- Polly policies: Negligible memory footprint

### Concurrency
- Thread-safe using `ConcurrentDictionary`
- No locking on request path (lock-free reads)
- Selection operations are atomic
- Polly retry policies are thread-safe

## Polly Integration

The load balancer uses **Polly** (version 8.5.0) for resilient retry logic:

### Retry Policy Configuration

```csharp
// Automatic exponential backoff: 1s, 2s, 4s
Policy
    .Handle<HttpRequestException>(ex => IsRateLimitError(ex))
    .Or<InvalidOperationException>(ex => ex.Message.Contains("No healthy providers"))
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));
```

### Handled Scenarios

- **HTTP 429 (Too Many Requests)**: Triggers immediate provider rotation with retry
- **Provider Selection Failure**: Retries provider selection with backoff
- **General Exceptions**: Caught and logged, triggers provider health tracking

### Benefits

- **Battle-tested**: Polly is production-proven across thousands of applications
- **Configurable**: Retry counts and backoff strategies can be customized
- **Observable**: Built-in logging on each retry attempt
- **Composable**: Can be combined with other Polly policies (circuit breaker, timeout)

## Troubleshooting

### All Providers in Cooldown

**Symptom**: `No healthy providers available. All providers are unhealthy or in cooldown.`

**Solutions**:
1. Wait for cooldowns to expire (check `CooldownUntil` property)
2. Register additional backup providers
3. Reduce request rate
4. Check provider API quotas

### Inconsistent Provider Selection

**Symptom**: Same provider selected repeatedly

**Possible Causes**:
- Other providers are unhealthy or in cooldown
- Using LeastLatency strategy (intentionally selects fastest)
- Provider registration issue

**Solutions**:
1. Check health status: `loadBalancer.GetHealthStatus()`
2. Verify all providers are registered correctly
3. Consider using RoundRobin for equal distribution

### High Latency

**Symptom**: Slow response times despite load balancing

**Possible Causes**:
- All providers experiencing high latency
- Network issues
- Insufficient provider capacity

**Solutions**:
1. Use LeastLatency strategy to route to fastest providers
2. Add more providers to distribute load
3. Check provider endpoint health
4. Monitor provider metrics for patterns

## Examples

See [ProviderLoadBalancingExample.cs](../Examples/ProviderLoadBalancingExample.cs) for comprehensive examples including:

- Basic Round Robin setup
- Adaptive Health routing
- Rate limit handling scenarios
- Least Latency optimization
- Custom configuration options

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  LoadBalancedChatModel (IChatCompletionModel)          │
│  - Wraps ProviderLoadBalancer                          │
│  - Handles HTTP 429 detection                          │
│  - Automatic retry with different providers            │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│  ProviderLoadBalancer<T>                               │
│  - Strategy selection (RoundRobin, Adaptive, etc.)     │
│  - Health tracking                                      │
│  - Circuit breaker logic                               │
│  - Metrics aggregation                                 │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│  Provider Pool                                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐            │
│  │Provider 1│  │Provider 2│  │Provider 3│            │
│  └──────────┘  └──────────┘  └──────────┘            │
│  [Healthy]     [Cooldown]    [Healthy]                │
└─────────────────────────────────────────────────────────┘
```

## Related Components

- `IProviderLoadBalancer<T>`: Generic load balancing interface
- `ProviderHealthStatus`: Health metrics and status
- `ProviderRotationStrategy`: Strategy enumeration
- `ProviderSelectionResult<T>`: Selection result with metadata

## License

Licensed under the MIT license. See LICENSE file in the project root.
