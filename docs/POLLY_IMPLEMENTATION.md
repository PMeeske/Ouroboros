# Polly Implementation for Exponential Backoff

## Overview

This document describes the implementation of Polly retry policies with exponential backoff for handling rate limiting and server errors in HTTP client models.

## Implementation Details

### Package Dependencies

- **Polly**: 8.5.0
- **Polly.Extensions.Http**: 3.0.0

Added to `Ouroboros.Providers.csproj`.

### Retry Policy Configuration

All HTTP client models now use a consistent Polly `AsyncRetryPolicy<HttpResponseMessage>` with the following configuration:

```csharp
_retryPolicy = Policy
    .HandleResult<HttpResponseMessage>(r => 
        (int)r.StatusCode == 429 || // Too Many Requests
        (int)r.StatusCode >= 500)   // Server errors
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            Console.WriteLine($"[ModelName] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
        });
```

### Backoff Schedule

The exponential backoff follows this schedule:

| Retry Attempt | Wait Time | Cumulative Wait |
|---------------|-----------|-----------------|
| 1st retry     | 2 seconds | 2 seconds       |
| 2nd retry     | 4 seconds | 6 seconds       |
| 3rd retry     | 8 seconds | 14 seconds      |

### HTTP Status Codes Handled

**Retried:**
- `429` - Too Many Requests (rate limiting)
- `500` - Internal Server Error
- `502` - Bad Gateway
- `503` - Service Unavailable
- `504` - Gateway Timeout
- All other 5xx server errors

**Not Retried:**
- `2xx` - Success responses
- `4xx` - Client errors (except 429)
  - `400` - Bad Request
  - `401` - Unauthorized
  - `403` - Forbidden
  - `404` - Not Found
  - etc.

## Updated Models

### Chat Completion Models

1. **HttpOpenAiCompatibleChatModel**
   - Endpoint: `/v1/responses`
   - Used for: OpenAI-compatible API endpoints
   - Method: `GenerateTextAsync()`

2. **LiteLLMChatModel**
   - Endpoint: `/v1/chat/completions`
   - Used for: LiteLLM proxy endpoints
   - Methods:
     - `GenerateTextAsync()` - Standard completion
     - `StreamReasoningContent()` - Streaming with reactive extensions

3. **OllamaCloudChatModel**
   - Endpoint: `/api/generate`
   - Used for: Ollama Cloud API
   - Method: `GenerateTextAsync()`

### Embedding Models

4. **LiteLLMEmbeddingModel**
   - Endpoint: `/v1/embeddings`
   - Used for: LiteLLM proxy embedding endpoints
   - Method: `CreateEmbeddingsAsync()`

5. **OllamaCloudEmbeddingModel**
   - Endpoint: `/api/embeddings`
   - Used for: Ollama Cloud embedding API
   - Method: `CreateEmbeddingsAsync()`

## Changes from Previous Implementation

### Before (Basic Throttling)

```csharp
private static readonly SemaphoreSlim _rateLimiter = new(1, 1);
private static DateTime _lastRequestTime = DateTime.MinValue;
private static readonly TimeSpan _throttleDelay = TimeSpan.FromSeconds(2);

private static async Task ThrottleRequestAsync(CancellationToken ct)
{
    await _rateLimiter.WaitAsync(ct);
    try
    {
        TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
        if (timeSinceLastRequest < _throttleDelay)
        {
            await Task.Delay(_throttleDelay - timeSinceLastRequest, ct);
        }
        _lastRequestTime = DateTime.UtcNow;
    }
    finally
    {
        _rateLimiter.Release();
    }
}
```

**Limitations:**
- Fixed delay between requests
- No retry on failures
- Global lock across all instances
- No handling of 429 or 5xx errors

### After (Polly Exponential Backoff)

```csharp
private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
{
    return await _client.PostAsync(endpoint, payload, ct).ConfigureAwait(false);
}).ConfigureAwait(false);
```

**Benefits:**
- Automatic retry on rate limits and server errors
- Exponential backoff reduces server load
- Per-instance policy (no global locks)
- Configurable and testable
- Industry-standard resilience pattern
- Detailed logging of retry attempts

## Usage Example

```csharp
// Create a LiteLLM client with automatic retry
var model = new LiteLLMChatModel(
    endpoint: "https://api.example.com",
    apiKey: "your-api-key",
    model: "gpt-4"
);

// Calls automatically retry on 429 or 5xx with exponential backoff
string response = await model.GenerateTextAsync("Hello, world!");
```

## Logging

Retry attempts are logged to console with the following format:

```
[LiteLLMChatModel] Retry 1 after 2s due to TooManyRequests
[LiteLLMChatModel] Retry 2 after 4s due to ServiceUnavailable
```

This helps with:
- Debugging rate limit issues
- Monitoring API health
- Identifying problematic endpoints

## Testing

The implementation includes unit tests in `PollyRetryTests.cs` that verify:

1. Exponential backoff calculation (2^n seconds)
2. Retry behavior on 429 status codes
3. Retry behavior on 5xx status codes
4. No retry on 4xx client errors (except 429)
5. No retry on 2xx success responses
6. Total retry duration calculation

## Configuration

The retry policy can be customized by modifying the policy configuration in each model's constructor:

- **Retry Count**: Change `retryCount: 3` to desired value
- **Backoff Formula**: Modify `Math.Pow(2, retryAttempt)` for different backoff
- **Status Codes**: Adjust the `HandleResult` predicate

## Performance Considerations

- **Maximum Wait Time**: With 3 retries, maximum total wait is 14 seconds
- **Request Overhead**: Minimal - policy evaluation is fast
- **Memory**: Per-instance policy has negligible memory footprint
- **Concurrency**: No global locks, supports parallel requests

## Future Enhancements

Potential improvements for future consideration:

1. **Circuit Breaker**: Add circuit breaker pattern for failing endpoints
2. **Jitter**: Add randomization to backoff to prevent thundering herd
3. **Configuration**: Move retry settings to appsettings.json
4. **Metrics**: Add telemetry for retry statistics
5. **Per-Endpoint Policies**: Different policies for different endpoint types

## References

- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Exponential Backoff and Jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
- [HTTP Status Codes](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status)
