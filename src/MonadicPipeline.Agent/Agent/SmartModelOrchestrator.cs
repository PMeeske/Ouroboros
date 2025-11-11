// <copyright file="SmartModelOrchestrator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

/// <summary>
/// Smart orchestrator that analyzes prompts and selects optimal models and tools
/// based on use case classification and performance metrics.
/// Implements functional programming patterns with monadic error handling.
/// </summary>
public sealed class SmartModelOrchestrator : IModelOrchestrator
{
    private readonly ConcurrentDictionary<string, IChatCompletionModel> models = new();
    private readonly ConcurrentDictionary<string, ModelCapability> capabilities = new();
    private readonly ConcurrentDictionary<string, PerformanceMetrics> metrics = new();
    private readonly ToolRegistry baseTools;
    private readonly string fallbackModel;

    public SmartModelOrchestrator(ToolRegistry baseTools, string fallbackModel = "default")
    {
        this.baseTools = baseTools ?? throw new ArgumentNullException(nameof(baseTools));
        this.fallbackModel = fallbackModel;
    }

    /// <summary>
    /// Registers a model with its capabilities and the actual model instance.
    /// </summary>
    public void RegisterModel(ModelCapability capability, IChatCompletionModel model)
    {
        if (capability is null)
        {
            throw new ArgumentNullException(nameof(capability));
        }

        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        this.capabilities[capability.ModelName] = capability;
        this.models[capability.ModelName] = model;

        // Initialize metrics if not present
        this.metrics.TryAdd(capability.ModelName, new PerformanceMetrics(
            capability.ModelName,
            ExecutionCount: 0,
            AverageLatencyMs: capability.AverageLatencyMs,
            SuccessRate: 1.0,
            LastUsed: DateTime.UtcNow,
            CustomMetrics: new Dictionary<string, double>()));
    }

    /// <summary>
    /// Registers a model with its capabilities for orchestration.
    /// </summary>
    public void RegisterModel(ModelCapability capability)
    {
        if (capability is null)
        {
            throw new ArgumentNullException(nameof(capability));
        }

        this.capabilities[capability.ModelName] = capability;

        // Initialize metrics if not present
        this.metrics.TryAdd(capability.ModelName, new PerformanceMetrics(
            capability.ModelName,
            ExecutionCount: 0,
            AverageLatencyMs: capability.AverageLatencyMs,
            SuccessRate: 1.0,
            LastUsed: DateTime.UtcNow,
            CustomMetrics: new Dictionary<string, double>()));
    }

    /// <summary>
    /// Analyzes prompt and selects optimal model with recommended tools.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<OrchestratorDecision, string>> SelectModelAsync(
        string prompt,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result<OrchestratorDecision, string>.Failure("Prompt cannot be empty");
        }

        // Classify the use case
        var useCase = this.ClassifyUseCase(prompt);

        // Find best matching model
        var modelResult = await this.SelectBestModelAsync(useCase, context, ct);

        return modelResult.Match(
            model => Result<OrchestratorDecision, string>.Success(model),
            error => Result<OrchestratorDecision, string>.Failure(error));
    }

    /// <summary>
    /// Classifies a prompt into a specific use case.
    /// </summary>
    /// <returns></returns>
    public UseCase ClassifyUseCase(string prompt)
    {
        var lowerPrompt = prompt.ToLowerInvariant();

        // Code generation patterns
        if (Regex.IsMatch(lowerPrompt, @"\b(code|implement|function|class|method|debug|fix|refactor)\b"))
        {
            var complexity = this.EstimateComplexity(prompt);
            return new UseCase(
                UseCaseType.CodeGeneration,
                complexity,
                new[] { "code", "syntax", "debugging" },
                PerformanceWeight: 0.7,
                CostWeight: 0.3);
        }

        // Reasoning patterns
        if (Regex.IsMatch(lowerPrompt, @"\b(analyze|reason|explain|why|how|cause|logic|deduce)\b"))
        {
            var complexity = this.EstimateComplexity(prompt);
            return new UseCase(
                UseCaseType.Reasoning,
                complexity,
                new[] { "reasoning", "analysis", "logic" },
                PerformanceWeight: 0.5,
                CostWeight: 0.5);
        }

        // Creative patterns
        if (Regex.IsMatch(lowerPrompt, @"\b(create|generate|write|story|poem|creative|imagine)\b"))
        {
            return new UseCase(
                UseCaseType.Creative,
                this.EstimateComplexity(prompt),
                new[] { "creative", "generation" },
                PerformanceWeight: 0.4,
                CostWeight: 0.6);
        }

        // Summarization patterns
        if (Regex.IsMatch(lowerPrompt, @"\b(summarize|brief|tldr|overview|condense)\b") || prompt.Length > 1000)
        {
            return new UseCase(
                UseCaseType.Summarization,
                this.EstimateComplexity(prompt),
                new[] { "summarization", "compression" },
                PerformanceWeight: 0.8,
                CostWeight: 0.2);
        }

        // Tool use patterns
        if (Regex.IsMatch(lowerPrompt, @"\[TOOL:|use.*tool|invoke|execute"))
        {
            return new UseCase(
                UseCaseType.ToolUse,
                this.EstimateComplexity(prompt),
                new[] { "tool-use", "function-calling" },
                PerformanceWeight: 0.6,
                CostWeight: 0.4);
        }

        // Default: conversation
        return new UseCase(
            UseCaseType.Conversation,
            this.EstimateComplexity(prompt),
            new[] { "general", "conversation" },
            PerformanceWeight: 0.6,
            CostWeight: 0.4);
    }

    /// <summary>
    /// Selects the best model based on use case and performance metrics.
    /// </summary>
    private async Task<Result<OrchestratorDecision, string>> SelectBestModelAsync(
        UseCase useCase,
        Dictionary<string, object>? context,
        CancellationToken ct)
    {
        if (this.capabilities.IsEmpty)
        {
            return Result<OrchestratorDecision, string>.Failure(
                "No models registered with orchestrator");
        }

        // Score each model
        var scoredModels = this.capabilities.Values
            .Select(cap => new
            {
                Capability = cap,
                Score = this.ScoreModel(cap, useCase),
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scoredModels.Count == 0)
        {
            return Result<OrchestratorDecision, string>.Failure(
                "No suitable models found for use case");
        }

        var best = scoredModels.First();

        // Get model instance or create fallback
        if (!this.models.TryGetValue(best.Capability.ModelName, out var model))
        {
            // If model not registered, try fallback
            if (this.models.TryGetValue(this.fallbackModel, out var fallback))
            {
                model = fallback;
            }
            else
            {
                return Result<OrchestratorDecision, string>.Failure(
                    $"Model '{best.Capability.ModelName}' not registered and no fallback available");
            }
        }

        // Select appropriate tools for the use case
        var recommendedTools = this.SelectToolsForUseCase(useCase);

        var decision = new OrchestratorDecision(
            SelectedModel: model,
            ModelName: best.Capability.ModelName,
            Reason: this.GenerateSelectionReason(best.Capability, useCase, best.Score),
            RecommendedTools: recommendedTools,
            ConfidenceScore: best.Score);

        return await Task.FromResult(
            Result<OrchestratorDecision, string>.Success(decision));
    }

    /// <summary>
    /// Scores a model against a use case considering performance metrics.
    /// </summary>
    private double ScoreModel(ModelCapability capability, UseCase useCase)
    {
        double score = 0.0;

        // Type matching
        var typeScore = useCase.Type switch
        {
            UseCaseType.CodeGeneration => capability.Type == ModelType.Code ? 1.0 : 0.3,
            UseCaseType.Reasoning => capability.Type == ModelType.Reasoning ? 1.0 : 0.4,
            UseCaseType.Creative => capability.Type == ModelType.Creative ? 1.0 : 0.5,
            UseCaseType.Summarization => capability.Type == ModelType.Summary ? 1.0 : 0.5,
            UseCaseType.Analysis => capability.Type == ModelType.Analysis ? 1.0 : 0.4,
            _ => capability.Type == ModelType.General ? 1.0 : 0.6,
        };
        score += typeScore * 0.4;

        // Capability matching
        var capabilityScore = useCase.RequiredCapabilities
            .Count(req => capability.Strengths.Any(s =>
                s.Contains(req, StringComparison.OrdinalIgnoreCase)))
            / (double)Math.Max(1, useCase.RequiredCapabilities.Length);
        score += capabilityScore * 0.3;

        // Performance metrics
        if (this.metrics.TryGetValue(capability.ModelName, out var metrics))
        {
            var performanceScore = metrics.SuccessRate *
                (1.0 - Math.Min(metrics.AverageLatencyMs / 10000.0, 0.9));
            score += performanceScore * useCase.PerformanceWeight * 0.3;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>
    /// Selects tools appropriate for the use case.
    /// </summary>
    private ToolRegistry SelectToolsForUseCase(UseCase useCase)
    {
        // Start with base tools
        var tools = this.baseTools;

        // Add specialized tools based on use case
        return useCase.Type switch
        {
            UseCaseType.CodeGeneration => tools, // Code tools would be added here
            UseCaseType.Reasoning => tools,
            UseCaseType.Creative => tools,
            UseCaseType.Summarization => tools,
            UseCaseType.ToolUse => tools,
            _ => tools,
        };
    }

    /// <summary>
    /// Generates human-readable reason for model selection.
    /// </summary>
    private string GenerateSelectionReason(
        ModelCapability capability,
        UseCase useCase,
        double score)
    {
        var reasons = new List<string>
        {
            $"Use case: {useCase.Type}",
            $"Model type: {capability.Type}",
            $"Confidence: {score:P0}",
        };

        if (this.metrics.TryGetValue(capability.ModelName, out var metrics))
        {
            reasons.Add($"Success rate: {metrics.SuccessRate:P0}");
            reasons.Add($"Avg latency: {metrics.AverageLatencyMs:F0}ms");
        }

        return string.Join(", ", reasons);
    }

    /// <summary>
    /// Estimates complexity of a prompt.
    /// </summary>
    private int EstimateComplexity(string prompt)
    {
        var length = prompt.Length;
        var sentences = prompt.Split('.', '!', '?').Length;
        var technicalTerms = Regex.Matches(
            prompt,
            @"\b(algorithm|architecture|implement|optimize|performance|system)\b",
            RegexOptions.IgnoreCase).Count;

        return (length / 100) + sentences + (technicalTerms * 2);
    }

    /// <summary>
    /// Records performance metrics for model execution.
    /// </summary>
    public void RecordMetric(string resourceName, double latencyMs, bool success)
    {
        this.metrics.AddOrUpdate(
            resourceName,

            // Add new
            _ => new PerformanceMetrics(
                resourceName,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),

            // Update existing
            (_, existing) =>
            {
                var newCount = existing.ExecutionCount + 1;
                var newAvgLatency = ((existing.AverageLatencyMs * existing.ExecutionCount) + latencyMs) / newCount;
                var newSuccessRate = ((existing.SuccessRate * existing.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;

                return new PerformanceMetrics(
                    resourceName,
                    ExecutionCount: newCount,
                    AverageLatencyMs: newAvgLatency,
                    SuccessRate: newSuccessRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: existing.CustomMetrics);
            });
    }

    /// <summary>
    /// Gets all current performance metrics.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        => new Dictionary<string, PerformanceMetrics>(this.metrics);
}
