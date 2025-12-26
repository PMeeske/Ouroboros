#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Orchestrated Chat Model
// Performance-aware wrapper that uses orchestrator for
// intelligent model and tool selection
// ==========================================================

using System.Diagnostics;

namespace Ouroboros.Agent;

/// <summary>
/// Performance-aware chat model that uses an orchestrator to select
/// optimal models and tools based on prompt analysis and metrics.
/// Implements monadic patterns for consistent error handling.
/// </summary>
public sealed class OrchestratedChatModel : IChatCompletionModel
{
    private readonly IModelOrchestrator _orchestrator;
    private readonly bool _trackMetrics;

    public OrchestratedChatModel(IModelOrchestrator orchestrator, bool trackMetrics = true)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _trackMetrics = trackMetrics;
    }

    /// <summary>
    /// Generates text using orchestrator-selected model.
    /// </summary>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Get orchestrator decision
            Result<OrchestratorDecision, string> decision = await _orchestrator.SelectModelAsync(prompt, ct: ct);

            return await decision.Match(
                async selected =>
                {
                    // Execute with selected model
                    string result = await selected.SelectedModel.GenerateTextAsync(prompt, ct);

                    // Track metrics
                    if (_trackMetrics)
                    {
                        sw.Stop();
                        _orchestrator.RecordMetric(
                            selected.ModelName,
                            sw.Elapsed.TotalMilliseconds,
                            success: !string.IsNullOrEmpty(result));
                    }

                    return result;
                },
                error => Task.FromResult($"[orchestrator-error] {error}"));
        }
        catch (Exception ex)
        {
            if (_trackMetrics)
            {
                sw.Stop();
                _orchestrator.RecordMetric("orchestrator", sw.Elapsed.TotalMilliseconds, success: false);
            }
            return $"[orchestrator-exception] {ex.Message}";
        }
    }

    /// <summary>
    /// Generates text with tools using orchestrator recommendations.
    /// </summary>
    public async Task<(string Text, List<ToolExecution> Tools, OrchestratorDecision? Decision)>
        GenerateWithOrchestratedToolsAsync(string prompt, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Get orchestrator decision
            Result<OrchestratorDecision, string> decisionResult = await _orchestrator.SelectModelAsync(prompt, ct: ct);

            return await decisionResult.Match(
                async decision =>
                {
                    // Create tool-aware model with recommended tools
                    ToolAwareChatModel toolAwareModel = new ToolAwareChatModel(
                        decision.SelectedModel,
                        decision.RecommendedTools);

                    // Execute with tools
                    (string text, List<ToolExecution> tools) = await toolAwareModel.GenerateWithToolsAsync(prompt, ct);

                    // Track metrics
                    if (_trackMetrics)
                    {
                        sw.Stop();
                        _orchestrator.RecordMetric(
                            decision.ModelName,
                            sw.Elapsed.TotalMilliseconds,
                            success: !string.IsNullOrEmpty(text));

                        // Track tool usage
                        foreach (ToolExecution tool in tools)
                        {
                            _orchestrator.RecordMetric(
                                $"tool_{tool.ToolName}",
                                0, // Tool execution time tracked separately
                                success: true);
                        }
                    }

                    return (text, tools, (OrchestratorDecision?)decision);
                },
                error => Task.FromResult<(string, List<ToolExecution>, OrchestratorDecision?)>(
                    ($"[orchestrator-error] {error}", new List<ToolExecution>(), null)));
        }
        catch (Exception ex)
        {
            if (_trackMetrics)
            {
                sw.Stop();
                _orchestrator.RecordMetric("orchestrator", sw.Elapsed.TotalMilliseconds, success: false);
            }
            return ($"[orchestrator-exception] {ex.Message}", new List<ToolExecution>(), null);
        }
    }
}

/// <summary>
/// Builder for creating orchestrated chat models with fluent configuration.
/// </summary>
public sealed class OrchestratorBuilder
{
    private readonly SmartModelOrchestrator _orchestrator;
    private readonly List<(ModelCapability, IChatCompletionModel)> _models = new();
    private bool _trackMetrics = true;

    public OrchestratorBuilder(ToolRegistry baseTools, string fallbackModel = "default")
    {
        _orchestrator = new SmartModelOrchestrator(baseTools, fallbackModel);
    }

    /// <summary>
    /// Registers a model with its capabilities.
    /// </summary>
    public OrchestratorBuilder WithModel(
        string name,
        IChatCompletionModel model,
        ModelType type,
        string[] strengths,
        int maxTokens = 4096,
        double avgCost = 1.0,
        double avgLatencyMs = 1000.0)
    {
        ModelCapability capability = new ModelCapability(
            name,
            strengths,
            maxTokens,
            avgCost,
            avgLatencyMs,
            type);

        _models.Add((capability, model));
        return this;
    }

    /// <summary>
    /// Enables or disables performance metric tracking.
    /// </summary>
    public OrchestratorBuilder WithMetricTracking(bool enabled)
    {
        _trackMetrics = enabled;
        return this;
    }

    /// <summary>
    /// Builds the orchestrated chat model.
    /// </summary>
    public OrchestratedChatModel Build()
    {
        // Register all models
        foreach ((ModelCapability capability, IChatCompletionModel model) in _models)
        {
            _orchestrator.RegisterModel(capability, model);
        }

        return new OrchestratedChatModel(_orchestrator, _trackMetrics);
    }

    /// <summary>
    /// Gets the underlying orchestrator for advanced scenarios.
    /// </summary>
    public IModelOrchestrator GetOrchestrator() => _orchestrator;
}
