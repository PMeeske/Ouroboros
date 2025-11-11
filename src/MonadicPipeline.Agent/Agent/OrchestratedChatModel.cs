// <copyright file="OrchestratedChatModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent;

using System.Diagnostics;

/// <summary>
/// Performance-aware chat model that uses an orchestrator to select
/// optimal models and tools based on prompt analysis and metrics.
/// Implements monadic patterns for consistent error handling.
/// </summary>
public sealed class OrchestratedChatModel : IChatCompletionModel
{
    private readonly IModelOrchestrator orchestrator;
    private readonly bool trackMetrics;

    public OrchestratedChatModel(IModelOrchestrator orchestrator, bool trackMetrics = true)
    {
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.trackMetrics = trackMetrics;
    }

    /// <summary>
    /// Generates text using orchestrator-selected model.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Get orchestrator decision
            var decision = await this.orchestrator.SelectModelAsync(prompt, ct: ct);

            return await decision.Match(
                async selected =>
                {
                    // Execute with selected model
                    var result = await selected.SelectedModel.GenerateTextAsync(prompt, ct);

                    // Track metrics
                    if (this.trackMetrics)
                    {
                        sw.Stop();
                        this.orchestrator.RecordMetric(
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
            if (this.trackMetrics)
            {
                sw.Stop();
                this.orchestrator.RecordMetric("orchestrator", sw.Elapsed.TotalMilliseconds, success: false);
            }

            return $"[orchestrator-exception] {ex.Message}";
        }
    }

    /// <summary>
    /// Generates text with tools using orchestrator recommendations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<(string Text, List<ToolExecution> Tools, OrchestratorDecision? Decision)>
        GenerateWithOrchestratedToolsAsync(string prompt, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Get orchestrator decision
            var decisionResult = await this.orchestrator.SelectModelAsync(prompt, ct: ct);

            return await decisionResult.Match(
                async decision =>
                {
                    // Create tool-aware model with recommended tools
                    var toolAwareModel = new ToolAwareChatModel(
                        decision.SelectedModel,
                        decision.RecommendedTools);

                    // Execute with tools
                    var (text, tools) = await toolAwareModel.GenerateWithToolsAsync(prompt, ct);

                    // Track metrics
                    if (this.trackMetrics)
                    {
                        sw.Stop();
                        this.orchestrator.RecordMetric(
                            decision.ModelName,
                            sw.Elapsed.TotalMilliseconds,
                            success: !string.IsNullOrEmpty(text));

                        // Track tool usage
                        foreach (var tool in tools)
                        {
                            this.orchestrator.RecordMetric(
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
            if (this.trackMetrics)
            {
                sw.Stop();
                this.orchestrator.RecordMetric("orchestrator", sw.Elapsed.TotalMilliseconds, success: false);
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
    private readonly SmartModelOrchestrator orchestrator;
    private readonly List<(ModelCapability, IChatCompletionModel)> models = new();
    private bool trackMetrics = true;

    public OrchestratorBuilder(ToolRegistry baseTools, string fallbackModel = "default")
    {
        this.orchestrator = new SmartModelOrchestrator(baseTools, fallbackModel);
    }

    /// <summary>
    /// Registers a model with its capabilities.
    /// </summary>
    /// <returns></returns>
    public OrchestratorBuilder WithModel(
        string name,
        IChatCompletionModel model,
        ModelType type,
        string[] strengths,
        int maxTokens = 4096,
        double avgCost = 1.0,
        double avgLatencyMs = 1000.0)
    {
        var capability = new ModelCapability(
            name,
            strengths,
            maxTokens,
            avgCost,
            avgLatencyMs,
            type);

        this.models.Add((capability, model));
        return this;
    }

    /// <summary>
    /// Enables or disables performance metric tracking.
    /// </summary>
    /// <returns></returns>
    public OrchestratorBuilder WithMetricTracking(bool enabled)
    {
        this.trackMetrics = enabled;
        return this;
    }

    /// <summary>
    /// Builds the orchestrated chat model.
    /// </summary>
    /// <returns></returns>
    public OrchestratedChatModel Build()
    {
        // Register all models
        foreach (var (capability, model) in this.models)
        {
            this.orchestrator.RegisterModel(capability, model);
        }

        return new OrchestratedChatModel(this.orchestrator, this.trackMetrics);
    }

    /// <summary>
    /// Gets the underlying orchestrator for advanced scenarios.
    /// </summary>
    /// <returns></returns>
    public IModelOrchestrator GetOrchestrator() => this.orchestrator;
}
