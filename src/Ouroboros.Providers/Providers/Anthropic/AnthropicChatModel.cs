#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text;
using System.Reactive.Linq;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace Ouroboros.Providers;

/// <summary>
/// Chat model adapter for Anthropic Claude API using the Anthropic.SDK package.
/// Supports streaming, extended thinking, and all Claude models.
/// </summary>
public sealed class AnthropicChatModel : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly int? _thinkingBudgetTokens;
    private readonly LlmCostTracker? _costTracker;

    /// <summary>
    /// Gets the cost tracker for this model instance.
    /// </summary>
    public LlmCostTracker? CostTracker => _costTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicChatModel"/> class.
    /// </summary>
    /// <param name="apiKey">Anthropic API key (sk-ant-...).</param>
    /// <param name="model">Model name (e.g., claude-opus-4-5-20251101, claude-sonnet-4-20250514).</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <param name="thinkingBudgetTokens">Optional token budget for extended thinking (requires compatible model).</param>
    /// <param name="costTracker">Optional cost tracker for monitoring usage and costs.</param>
    public AnthropicChatModel(string apiKey, string model, ChatRuntimeSettings? settings = null, int? thinkingBudgetTokens = null, LlmCostTracker? costTracker = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new AnthropicClient(apiKey);
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        _thinkingBudgetTokens = thinkingBudgetTokens;
        _costTracker = costTracker ?? new LlmCostTracker(model, "Anthropic");
    }

    /// <summary>
    /// Creates an AnthropicChatModel from environment variables.
    /// Uses ANTHROPIC_API_KEY environment variable.
    /// </summary>
    /// <param name="model">Model name.</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <param name="thinkingBudgetTokens">Optional token budget for extended thinking.</param>
    /// <param name="costTracker">Optional cost tracker for monitoring usage and costs.</param>
    /// <returns>Configured AnthropicChatModel instance.</returns>
    public static AnthropicChatModel FromEnvironment(string model, ChatRuntimeSettings? settings = null, int? thinkingBudgetTokens = null, LlmCostTracker? costTracker = null)
    {
        string? apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set");
        }

        return new AnthropicChatModel(apiKey, model, settings, thinkingBudgetTokens, costTracker);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        ThinkingResponse response = await GenerateWithThinkingAsync(prompt, ct);
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        _costTracker?.StartRequest();
        try
        {
            string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

            List<Message> messages = new()
            {
                new Message(RoleType.User, finalPrompt)
            };

            MessageParameters parameters = new()
            {
                Messages = messages,
                MaxTokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 4096,
                Model = _model,
                Stream = false
            };

            // Add thinking parameters if budget is specified
            if (_thinkingBudgetTokens.HasValue && _thinkingBudgetTokens.Value > 0)
            {
                parameters.Thinking = new ThinkingParameters
                {
                    BudgetTokens = _thinkingBudgetTokens.Value
                };
            }

            MessageResponse result = await _client.Messages.GetClaudeMessageAsync(parameters, ct).ConfigureAwait(false);

            string? thinking = null;
            StringBuilder contentBuilder = new();
            int inputTokens = result.Usage?.InputTokens ?? 0;
            int outputTokens = result.Usage?.OutputTokens ?? 0;

            // Record cost tracking
            _costTracker?.EndRequest(inputTokens, outputTokens);

            // Process content blocks - handle both text and thinking blocks
            if (result.Content != null)
            {
                // Extract text content
                foreach (TextContent textBlock in result.Content.OfType<TextContent>())
                {
                    contentBuilder.Append(textBlock.Text);
                }

                // Extract thinking content
                foreach (ThinkingContent thinkingBlock in result.Content.OfType<ThinkingContent>())
                {
                    thinking = thinkingBlock.Thinking;
                }
            }

            string content = contentBuilder.ToString();

            // If we have explicit thinking content, return structured response
            if (!string.IsNullOrEmpty(thinking))
            {
                return new ThinkingResponse(thinking, content, inputTokens, outputTokens);
            }

            // Otherwise, try to parse thinking tags from content
            if (!string.IsNullOrEmpty(content))
            {
                ThinkingResponse parsed = ThinkingResponse.FromRawText(content);
                return parsed with { ThinkingTokens = inputTokens, ContentTokens = outputTokens };
            }

            return new ThinkingResponse(null, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AnthropicChatModel] Error: {ex.GetType().Name}: {ex.Message}");
        }

        return new ThinkingResponse(null, $"[anthropic-fallback:{_model}] {prompt}");
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

                List<Message> messages = new()
                {
                    new Message(RoleType.User, finalPrompt)
                };

                MessageParameters parameters = new()
                {
                    Messages = messages,
                    MaxTokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 4096,
                    Model = _model,
                    Stream = true
                };

                // Add thinking parameters if budget is specified
                if (_thinkingBudgetTokens.HasValue && _thinkingBudgetTokens.Value > 0)
                {
                    parameters.Thinking = new ThinkingParameters
                    {
                        BudgetTokens = _thinkingBudgetTokens.Value
                    };
                }

                await foreach (MessageResponse streamEvent in _client.Messages.StreamClaudeMessageAsync(parameters, token).ConfigureAwait(false))
                {
                    // Handle text delta
                    if (streamEvent.Delta?.Text != null)
                    {
                        observer.OnNext((false, streamEvent.Delta.Text));
                    }
                    // Handle thinking delta (if supported by model)
                    else if (streamEvent.Delta?.Thinking != null)
                    {
                        observer.OnNext((true, streamEvent.Delta.Thinking));
                    }
                }

                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        // Flatten the thinking stream to emit all chunks
        return StreamWithThinkingAsync(prompt, ct).Select(tuple => tuple.Chunk);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
