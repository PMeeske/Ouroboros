// <copyright file="ConsolidatedMind.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Steps;
using Ouroboros.Providers.DeepSeek;

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Result of a ConsolidatedMind execution.
/// </summary>
/// <param name="Response">The generated response.</param>
/// <param name="ThinkingContent">Optional thinking/reasoning content if available.</param>
/// <param name="UsedRoles">Roles that contributed to this response.</param>
/// <param name="ExecutionTimeMs">Total execution time in milliseconds.</param>
/// <param name="WasVerified">Whether the response was verified.</param>
/// <param name="Confidence">Overall confidence in the response.</param>
public sealed record MindResponse(
    string Response,
    string? ThinkingContent,
    SpecializedRole[] UsedRoles,
    double ExecutionTimeMs,
    bool WasVerified,
    double Confidence);

/// <summary>
/// Configuration for the ConsolidatedMind.
/// </summary>
/// <param name="EnableThinking">Whether to use thinking mode when appropriate.</param>
/// <param name="EnableVerification">Whether to verify complex responses.</param>
/// <param name="EnableParallelExecution">Whether to run sub-models in parallel when possible.</param>
/// <param name="MaxParallelism">Maximum parallel model executions.</param>
/// <param name="DefaultTimeout">Default timeout for model calls.</param>
/// <param name="FallbackOnError">Whether to fallback to alternative models on error.</param>
public sealed record MindConfig(
    bool EnableThinking = true,
    bool EnableVerification = true,
    bool EnableParallelExecution = true,
    int MaxParallelism = 3,
    TimeSpan? DefaultTimeout = null,
    bool FallbackOnError = true)
{
    /// <summary>
    /// Creates a minimal configuration for resource-constrained environments.
    /// </summary>
    public static MindConfig Minimal() => new(
        EnableThinking: false,
        EnableVerification: false,
        EnableParallelExecution: false,
        MaxParallelism: 1,
        FallbackOnError: true);

    /// <summary>
    /// Creates a high-quality configuration for production use.
    /// </summary>
    public static MindConfig HighQuality() => new(
        EnableThinking: true,
        EnableVerification: true,
        EnableParallelExecution: true,
        MaxParallelism: 4,
        DefaultTimeout: TimeSpan.FromMinutes(5),
        FallbackOnError: true);
}

/// <summary>
/// The ConsolidatedMind is a central orchestrator that coordinates multiple specialized AI models
/// to handle complex tasks. It analyzes incoming requests, routes them to appropriate specialists,
/// and synthesizes responses using functional composition patterns.
///
/// This implements a "Society of Mind" architecture where each specialist contributes
/// its expertise, coordinated by a meta-cognitive controller.
/// </summary>
public sealed class ConsolidatedMind : IChatCompletionModel, IDisposable
{
    private readonly ConcurrentDictionary<SpecializedRole, SpecializedModel> _specialists = new();
    private readonly MindConfig _config;
    private readonly ToolRegistry? _tools;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new ConsolidatedMind with the specified configuration.
    /// </summary>
    /// <param name="config">Configuration for the mind.</param>
    /// <param name="tools">Optional tool registry for tool-enabled specialists.</param>
    public ConsolidatedMind(MindConfig? config = null, ToolRegistry? tools = null)
    {
        _config = config ?? new MindConfig();
        _tools = tools;
    }

    /// <summary>
    /// Registers a specialized model for a specific role.
    /// </summary>
    /// <param name="specialist">The specialist to register.</param>
    public void RegisterSpecialist(SpecializedModel specialist)
    {
        ArgumentNullException.ThrowIfNull(specialist);
        _specialists[specialist.Role] = specialist;

        // Initialize metrics
        _metrics.TryAdd(specialist.ModelName, new PerformanceMetrics(
            specialist.ModelName,
            ExecutionCount: 0,
            AverageLatencyMs: specialist.AverageLatencyMs,
            SuccessRate: 1.0,
            LastUsed: DateTime.UtcNow,
            CustomMetrics: new Dictionary<string, double>()));
    }

    /// <summary>
    /// Registers multiple specialists at once.
    /// </summary>
    /// <param name="specialists">The specialists to register.</param>
    public void RegisterSpecialists(IEnumerable<SpecializedModel> specialists)
    {
        foreach (var specialist in specialists)
        {
            RegisterSpecialist(specialist);
        }
    }

    /// <summary>
    /// Creates and registers specialists from Ollama Cloud configurations.
    /// </summary>
    /// <param name="configs">Model configurations.</param>
    /// <param name="endpoint">Ollama Cloud endpoint.</param>
    /// <param name="apiKey">Ollama Cloud API key.</param>
    public void RegisterFromConfigs(
        IEnumerable<SpecializedModelConfig> configs,
        string endpoint,
        string apiKey)
    {
        foreach (var config in configs)
        {
            var model = new OllamaCloudChatModel(
                config.Endpoint ?? endpoint,
                apiKey,
                config.OllamaModel,
                new ChatRuntimeSettings { Temperature = (float)config.Temperature });

            var specialist = new SpecializedModel(
                config.Role,
                model,
                config.OllamaModel,
                config.Capabilities ?? Array.Empty<string>(),
                config.Priority,
                config.MaxTokens);

            RegisterSpecialist(specialist);
        }
    }

    /// <summary>
    /// Sets up the ConsolidatedMind with default Ollama Cloud models from environment variables.
    /// </summary>
    /// <param name="useHighQuality">Whether to use high-quality (larger) models.</param>
    public void SetupFromEnvironment(bool useHighQuality = false)
    {
        string endpoint = OllamaCloudDefaults.GetEndpoint();
        string? apiKey = Environment.GetEnvironmentVariable(OllamaCloudDefaults.ApiKeyEnvVar);

        // API key is optional for local mode
        bool isCloud = OllamaCloudDefaults.IsCloudMode();
        if (isCloud && string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                $"Environment variable {OllamaCloudDefaults.ApiKeyEnvVar} is not set. " +
                "Set it to your Ollama Cloud API key, or use local mode.");
        }

        var configs = useHighQuality
            ? OllamaCloudDefaults.GetHighQualityConfigs()
            : OllamaCloudDefaults.GetAllDefaultConfigs();

        RegisterFromConfigs(configs, endpoint, apiKey);
    }

    /// <summary>
    /// Sets up a minimal ConsolidatedMind with only essential specialists.
    /// </summary>
    public void SetupMinimalFromEnvironment()
    {
        string endpoint = OllamaCloudDefaults.GetEndpoint();
        string? apiKey = Environment.GetEnvironmentVariable(OllamaCloudDefaults.ApiKeyEnvVar);

        bool isCloud = OllamaCloudDefaults.IsCloudMode();
        if (isCloud && string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                $"Environment variable {OllamaCloudDefaults.ApiKeyEnvVar} is not set.");
        }

        RegisterFromConfigs(OllamaCloudDefaults.GetMinimalConfigs(), endpoint, apiKey);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await ProcessAsync(prompt, ct);
        return response.Response;
    }

    /// <summary>
    /// Processes a prompt with full analysis and routing.
    /// </summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mind's response with metadata.</returns>
    public async Task<MindResponse> ProcessAsync(string prompt, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var usedRoles = new List<SpecializedRole>();

        // Analyze the task
        TaskAnalysis analysis = TaskAnalyzer.Analyze(prompt);

        // Get the primary specialist
        if (!_specialists.TryGetValue(analysis.PrimaryRole, out var primarySpecialist))
        {
            // Fallback to QuickResponse or any available specialist
            primarySpecialist = _specialists.Values.FirstOrDefault()
                ?? throw new InvalidOperationException("No specialists registered in ConsolidatedMind");
        }
        usedRoles.Add(primarySpecialist.Role);

        string? thinkingContent = null;
        string response;

        try
        {
            // Execute with thinking if appropriate
            if (_config.EnableThinking && analysis.RequiresThinking &&
                primarySpecialist.Model is IThinkingChatModel thinkingModel)
            {
                var thinkingResponse = await thinkingModel.GenerateWithThinkingAsync(prompt, ct);
                response = thinkingResponse.Content;
                thinkingContent = thinkingResponse.Thinking;
            }
            else
            {
                response = await primarySpecialist.Model.GenerateTextAsync(prompt, ct);
            }

            // Verify if needed
            bool wasVerified = false;
            if (_config.EnableVerification && analysis.RequiresVerification)
            {
                var verificationResult = await VerifyResponseAsync(prompt, response, ct);
                wasVerified = true;
                if (!verificationResult.IsValid && _config.FallbackOnError)
                {
                    // Try to improve the response
                    response = await RefineResponseAsync(prompt, response, verificationResult.Feedback, ct);
                }
                usedRoles.Add(SpecializedRole.Verifier);
            }

            // Update metrics
            UpdateMetrics(primarySpecialist.ModelName, stopwatch.ElapsedMilliseconds, true);

            return new MindResponse(
                response,
                thinkingContent,
                usedRoles.ToArray(),
                stopwatch.ElapsedMilliseconds,
                wasVerified,
                analysis.Confidence);
        }
        catch (Exception ex) when (_config.FallbackOnError)
        {
            // Try fallback to another specialist
            UpdateMetrics(primarySpecialist.ModelName, stopwatch.ElapsedMilliseconds, false);

            var fallback = GetFallbackSpecialist(analysis.PrimaryRole);
            if (fallback != null)
            {
                response = await fallback.Model.GenerateTextAsync(prompt, ct);
                usedRoles.Add(fallback.Role);

                return new MindResponse(
                    response,
                    null,
                    usedRoles.ToArray(),
                    stopwatch.ElapsedMilliseconds,
                    WasVerified: false,
                    Confidence: analysis.Confidence * 0.7); // Reduced confidence for fallback
            }

            throw new InvalidOperationException($"Primary specialist failed and no fallback available: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a complex task by decomposing it and coordinating multiple specialists.
    /// </summary>
    /// <param name="prompt">The complex prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Synthesized response from multiple specialists.</returns>
    public async Task<MindResponse> ProcessComplexAsync(string prompt, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var usedRoles = new List<SpecializedRole>();

        // Use planner to decompose the task
        if (!_specialists.TryGetValue(SpecializedRole.Planner, out var planner))
        {
            // Fall back to simple processing if no planner
            return await ProcessAsync(prompt, ct);
        }

        usedRoles.Add(SpecializedRole.Planner);

        string planPrompt = $@"Decompose this task into clear sub-tasks that can be handled by specialists.
Output a numbered list of sub-tasks, each on a new line.

Task: {prompt}

Sub-tasks:";

        string plan = await planner.Model.GenerateTextAsync(planPrompt, ct);

        // Parse sub-tasks (simple line-based parsing)
        var subTasks = plan.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '-', ' '))
            .Where(line => line.Length > 5)
            .ToList();

        if (subTasks.Count == 0)
        {
            // Planning didn't produce sub-tasks, fall back to simple processing
            return await ProcessAsync(prompt, ct);
        }

        // Execute sub-tasks (optionally in parallel)
        var subResults = new List<(string Task, string Result, SpecializedRole Role)>();

        if (_config.EnableParallelExecution && subTasks.Count > 1)
        {
            var semaphore = new SemaphoreSlim(_config.MaxParallelism);
            var tasks = subTasks.Select(async subTask =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var subAnalysis = TaskAnalyzer.Analyze(subTask);
                    var specialist = _specialists.GetValueOrDefault(subAnalysis.PrimaryRole)
                        ?? _specialists.Values.First();

                    var result = await specialist.Model.GenerateTextAsync(subTask, ct);
                    return (subTask, result, specialist.Role);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            subResults.AddRange(results);
        }
        else
        {
            foreach (var subTask in subTasks)
            {
                var subAnalysis = TaskAnalyzer.Analyze(subTask);
                var specialist = _specialists.GetValueOrDefault(subAnalysis.PrimaryRole)
                    ?? _specialists.Values.First();

                var result = await specialist.Model.GenerateTextAsync(subTask, ct);
                subResults.Add((subTask, result, specialist.Role));
                usedRoles.Add(specialist.Role);
            }
        }

        usedRoles.AddRange(subResults.Select(r => r.Role).Distinct());

        // Synthesize final response
        if (_specialists.TryGetValue(SpecializedRole.Synthesizer, out var synthesizer))
        {
            usedRoles.Add(SpecializedRole.Synthesizer);

            string synthesisPrompt = $@"Synthesize a coherent response from these sub-task results.

Original task: {prompt}

Sub-task results:
{string.Join("\n\n", subResults.Select((r, i) => $"[{i + 1}] {r.Task}\nResult: {r.Result}"))}

Synthesized response:";

            var finalResponse = await synthesizer.Model.GenerateTextAsync(synthesisPrompt, ct);

            return new MindResponse(
                finalResponse,
                ThinkingContent: null,
                usedRoles.Distinct().ToArray(),
                stopwatch.ElapsedMilliseconds,
                WasVerified: false,
                Confidence: 0.8);
        }

        // No synthesizer - just concatenate results
        var combinedResponse = string.Join("\n\n", subResults.Select(r => r.Result));
        return new MindResponse(
            combinedResponse,
            ThinkingContent: null,
            usedRoles.Distinct().ToArray(),
            stopwatch.ElapsedMilliseconds,
            WasVerified: false,
            Confidence: 0.6);
    }

    /// <summary>
    /// Creates a Kleisli arrow for pipeline integration.
    /// </summary>
    /// <returns>A step that can be composed into pipelines.</returns>
    public Step<string, MindResponse> ToStep()
    {
        return async prompt => await ProcessAsync(prompt);
    }

    /// <summary>
    /// Creates a pipeline-compatible arrow for PipelineBranch processing.
    /// </summary>
    /// <param name="promptBuilder">Function to build prompt from branch.</param>
    /// <param name="responseHandler">Function to handle response and update branch.</param>
    /// <returns>A step for pipeline composition.</returns>
    public Step<PipelineBranch, PipelineBranch> ToBranchStep(
        Func<PipelineBranch, string> promptBuilder,
        Func<PipelineBranch, MindResponse, PipelineBranch> responseHandler)
    {
        return async branch =>
        {
            string prompt = promptBuilder(branch);
            var response = await ProcessAsync(prompt);
            return responseHandler(branch, response);
        };
    }

    /// <summary>
    /// Gets all registered specialists.
    /// </summary>
    public IReadOnlyDictionary<SpecializedRole, SpecializedModel> Specialists =>
        new Dictionary<SpecializedRole, SpecializedModel>(_specialists);

    /// <summary>
    /// Gets performance metrics for all models.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> Metrics =>
        new Dictionary<string, PerformanceMetrics>(_metrics);

    private async Task<(bool IsValid, string Feedback)> VerifyResponseAsync(
        string originalPrompt,
        string response,
        CancellationToken ct)
    {
        if (!_specialists.TryGetValue(SpecializedRole.Verifier, out var verifier))
        {
            return (true, string.Empty); // No verifier available
        }

        string verifyPrompt = $@"Verify this response for accuracy and completeness.

Original question: {originalPrompt}

Response to verify: {response}

Is this response accurate and complete? Reply with:
VALID: [reason]
or
INVALID: [specific issues and suggestions]";

        var verificationResult = await verifier.Model.GenerateTextAsync(verifyPrompt, ct);

        bool isValid = verificationResult.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);
        return (isValid, verificationResult);
    }

    private async Task<string> RefineResponseAsync(
        string originalPrompt,
        string originalResponse,
        string feedback,
        CancellationToken ct)
    {
        // Get any available specialist for refinement
        var refiner = _specialists.GetValueOrDefault(SpecializedRole.DeepReasoning)
            ?? _specialists.GetValueOrDefault(SpecializedRole.Analyst)
            ?? _specialists.Values.FirstOrDefault();

        if (refiner == null)
            return originalResponse;

        string refinePrompt = $@"Improve this response based on the verification feedback.

Original question: {originalPrompt}

Original response: {originalResponse}

Feedback: {feedback}

Improved response:";

        return await refiner.Model.GenerateTextAsync(refinePrompt, ct);
    }

    private SpecializedModel? GetFallbackSpecialist(SpecializedRole failedRole)
    {
        // Define fallback chains
        var fallbackChain = failedRole switch
        {
            SpecializedRole.DeepReasoning => new[] { SpecializedRole.Analyst, SpecializedRole.QuickResponse },
            SpecializedRole.CodeExpert => new[] { SpecializedRole.DeepReasoning, SpecializedRole.QuickResponse },
            SpecializedRole.Mathematical => new[] { SpecializedRole.DeepReasoning, SpecializedRole.CodeExpert },
            SpecializedRole.Creative => new[] { SpecializedRole.QuickResponse, SpecializedRole.DeepReasoning },
            SpecializedRole.Planner => new[] { SpecializedRole.DeepReasoning, SpecializedRole.Analyst },
            _ => new[] { SpecializedRole.QuickResponse, SpecializedRole.DeepReasoning }
        };

        foreach (var role in fallbackChain)
        {
            if (_specialists.TryGetValue(role, out var specialist))
            {
                return specialist;
            }
        }

        return _specialists.Values.FirstOrDefault();
    }

    private void UpdateMetrics(string modelName, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            modelName,
            _ => new PerformanceMetrics(
                modelName,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            (_, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newLatency = (existing.AverageLatencyMs * existing.ExecutionCount + latencyMs) / newCount;
                double newSuccessRate = (existing.SuccessRate * existing.ExecutionCount + (success ? 1.0 : 0.0)) / newCount;

                return existing with
                {
                    ExecutionCount = newCount,
                    AverageLatencyMs = newLatency,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow
                };
            });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        foreach (var specialist in _specialists.Values)
        {
            if (specialist.Model is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _specialists.Clear();
        _disposed = true;
    }
}
