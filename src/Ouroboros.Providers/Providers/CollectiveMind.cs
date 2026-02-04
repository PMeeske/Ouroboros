#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using Polly;
using Polly.CircuitBreaker;

namespace Ouroboros.Providers;

/// <summary>
/// Represents the health and state of a neural pathway (provider connection).
/// </summary>
public sealed class NeuralPathway
{
    public string Name { get; init; } = "";
    public ChatEndpointType EndpointType { get; init; }
    public IChatCompletionModel Model { get; init; } = null!;
    public LlmCostTracker CostTracker { get; init; } = null!;
    public AsyncCircuitBreakerPolicy CircuitBreaker { get; init; } = null!;

    /// <summary>
    /// The capability tier of this pathway for routing purposes.
    /// </summary>
    public PathwayTier Tier { get; init; } = PathwayTier.CloudLight;

    /// <summary>
    /// Specialized capabilities this pathway excels at.
    /// </summary>
    public HashSet<SubGoalType> Specializations { get; init; } = new();

    // Health metrics
    public int Synapses { get; set; } // Total requests
    public int Activations { get; set; } // Successful requests
    public int Inhibitions { get; set; } // Failed requests
    public DateTime? LastActivation { get; set; }
    public TimeSpan AverageLatency { get; set; }

    // Adaptive weight based on performance
    public double Weight { get; set; } = 1.0;

    public bool IsHealthy => CircuitBreaker.CircuitState != CircuitState.Open;
    public double ActivationRate => Synapses > 0 ? (double)Activations / Synapses : 1.0;

    public void RecordActivation(TimeSpan latency)
    {
        Synapses++;
        Activations++;
        LastActivation = DateTime.UtcNow;

        // Exponential moving average for latency
        AverageLatency = AverageLatency == TimeSpan.Zero
            ? latency
            : TimeSpan.FromMilliseconds(AverageLatency.TotalMilliseconds * 0.8 + latency.TotalMilliseconds * 0.2);

        // Increase weight for reliable pathways
        Weight = Math.Min(2.0, Weight * 1.05);
    }

    public void RecordInhibition()
    {
        Synapses++;
        Inhibitions++;

        // Decrease weight for unreliable pathways
        Weight = Math.Max(0.1, Weight * 0.7);
    }
}

/// <summary>
/// Thinking mode for the collective mind.
/// </summary>
public enum CollectiveThinkingMode
{
    /// <summary>First successful response wins (fastest).</summary>
    Racing,
    /// <summary>Round-robin with failover (balanced).</summary>
    Sequential,
    /// <summary>Query multiple providers and synthesize (highest quality).</summary>
    Ensemble,
    /// <summary>Adaptive selection based on pathway health and query complexity.</summary>
    Adaptive,
    /// <summary>Decompose request into sub-goals and route to optimal pathways.</summary>
    Decomposed
}

/// <summary>
/// Classification of a pathway's capability tier.
/// </summary>
public enum PathwayTier
{
    /// <summary>Local models (Ollama, local inference) - fast, free, good for simple tasks.</summary>
    Local,
    /// <summary>Lightweight cloud models - balanced speed/quality.</summary>
    CloudLight,
    /// <summary>Premium cloud models - highest quality, most expensive.</summary>
    CloudPremium,
    /// <summary>Specialized models (coding, math, etc.).</summary>
    Specialized
}

/// <summary>
/// A sub-goal decomposed from a larger request.
/// Compatible with Pipeline.Planning.Goal - use SubGoalExtensions in Ouroboros.Pipeline
/// to convert between types when integrating with GoalDecomposer/HierarchicalGoalPlanner.
/// </summary>
public sealed record SubGoal(
    string Id,
    string Description,
    SubGoalComplexity Complexity,
    SubGoalType Type,
    IReadOnlyList<string> Dependencies,
    PathwayTier PreferredTier)
{
    /// <summary>
    /// Creates a SubGoal with inferred routing metadata from a description.
    /// </summary>
    public static SubGoal FromDescription(string description, int index = 0)
    {
        return new SubGoal(
            Id: $"goal_{index + 1}",
            Description: description,
            Complexity: InferComplexity(description),
            Type: InferGoalType(description),
            Dependencies: Array.Empty<string>(),
            PreferredTier: InferTier(description));
    }

    private static SubGoalComplexity InferComplexity(string text)
    {
        var length = text.Length;
        var hasMultipleSteps = Regex.IsMatch(
            text, @"\b(then|next|after|finally|also|and then)\b",
            RegexOptions.IgnoreCase);

        if (length < 50) return SubGoalComplexity.Simple;
        if (length < 200 && !hasMultipleSteps) return SubGoalComplexity.Moderate;
        if (length < 500) return SubGoalComplexity.Complex;
        return SubGoalComplexity.Expert;
    }

    private static SubGoalType InferGoalType(string text)
    {
        var lower = text.ToLowerInvariant();

        if (Regex.IsMatch(lower, @"\b(code|program|function|class|implement|debug|refactor)\b"))
            return SubGoalType.Coding;
        if (Regex.IsMatch(lower, @"\b(calculate|compute|solve|equation|formula|math)\b"))
            return SubGoalType.Math;
        if (Regex.IsMatch(lower, @"\b(write|create|compose|generate|story|poem|creative)\b"))
            return SubGoalType.Creative;
        if (Regex.IsMatch(lower, @"\b(analyze|compare|evaluate|reason|explain why)\b"))
            return SubGoalType.Reasoning;
        if (Regex.IsMatch(lower, @"\b(convert|transform|format|translate|summarize)\b"))
            return SubGoalType.Transform;
        if (Regex.IsMatch(lower, @"\b(find|search|lookup|what is|who is|when)\b"))
            return SubGoalType.Retrieval;

        return SubGoalType.Reasoning;
    }

    private static PathwayTier InferTier(string text)
    {
        var type = InferGoalType(text);
        var complexity = InferComplexity(text);

        if (complexity <= SubGoalComplexity.Simple)
            return PathwayTier.Local;

        return type switch
        {
            SubGoalType.Retrieval => PathwayTier.Local,
            SubGoalType.Transform => PathwayTier.Local,
            SubGoalType.Coding => PathwayTier.Specialized,
            SubGoalType.Math => PathwayTier.Specialized,
            SubGoalType.Creative => PathwayTier.CloudPremium,
            SubGoalType.Synthesis => PathwayTier.CloudPremium,
            _ => PathwayTier.CloudLight
        };
    }
}

/// <summary>
/// Complexity level of a sub-goal.
/// </summary>
public enum SubGoalComplexity
{
    /// <summary>Simple lookup or transformation.</summary>
    Trivial,
    /// <summary>Straightforward task, single-step reasoning.</summary>
    Simple,
    /// <summary>Multi-step reasoning required.</summary>
    Moderate,
    /// <summary>Complex analysis or creative generation.</summary>
    Complex,
    /// <summary>Requires deep expertise or multi-factor analysis.</summary>
    Expert
}

/// <summary>
/// Type of sub-goal for routing purposes.
/// </summary>
public enum SubGoalType
{
    /// <summary>Factual lookup or recall.</summary>
    Retrieval,
    /// <summary>Text transformation or formatting.</summary>
    Transform,
    /// <summary>Logical reasoning or analysis.</summary>
    Reasoning,
    /// <summary>Creative writing or generation.</summary>
    Creative,
    /// <summary>Code generation or review.</summary>
    Coding,
    /// <summary>Mathematical computation or proof.</summary>
    Math,
    /// <summary>Aggregation or synthesis of other results.</summary>
    Synthesis
}

/// <summary>
/// Result of executing a sub-goal.
/// </summary>
public sealed record SubGoalResult(
    string GoalId,
    string PathwayUsed,
    ThinkingResponse Response,
    TimeSpan Duration,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Configuration for goal decomposition behavior.
/// Integrates with existing Pipeline.Planning.GoalDecomposer for hierarchical goals.
/// </summary>
public sealed record DecompositionConfig
{
    /// <summary>Maximum number of sub-goals to create.</summary>
    public int MaxSubGoals { get; init; } = 10;

    /// <summary>Whether to parallelize independent sub-goals.</summary>
    public bool ParallelizeIndependent { get; init; } = true;

    /// <summary>Prefer local models for simple tasks.</summary>
    public bool PreferLocalForSimple { get; init; } = true;

    /// <summary>Always use premium for final synthesis.</summary>
    public bool PremiumForSynthesis { get; init; } = true;

    /// <summary>Minimum complexity to warrant decomposition.</summary>
    public SubGoalComplexity DecompositionThreshold { get; init; } = SubGoalComplexity.Moderate;

    /// <summary>
    /// Use existing Pipeline.Planning.GoalDecomposer instead of inline decomposition.
    /// Requires a PipelineBranch to be provided for full integration.
    /// </summary>
    public bool UsePipelineGoalDecomposer { get; init; } = false;

    /// <summary>Custom routing rules by goal type.</summary>
    public Dictionary<SubGoalType, PathwayTier> TypeRouting { get; init; } = new()
    {
        [SubGoalType.Retrieval] = PathwayTier.Local,
        [SubGoalType.Transform] = PathwayTier.Local,
        [SubGoalType.Reasoning] = PathwayTier.CloudLight,
        [SubGoalType.Creative] = PathwayTier.CloudPremium,
        [SubGoalType.Coding] = PathwayTier.Specialized,
        [SubGoalType.Math] = PathwayTier.Specialized,
        [SubGoalType.Synthesis] = PathwayTier.CloudPremium
    };

    public static DecompositionConfig Default { get; } = new();

    public static DecompositionConfig LocalFirst { get; } = new()
    {
        PreferLocalForSimple = true,
        PremiumForSynthesis = false,
        TypeRouting = new()
        {
            [SubGoalType.Retrieval] = PathwayTier.Local,
            [SubGoalType.Transform] = PathwayTier.Local,
            [SubGoalType.Reasoning] = PathwayTier.Local,
            [SubGoalType.Creative] = PathwayTier.CloudLight,
            [SubGoalType.Coding] = PathwayTier.Local,
            [SubGoalType.Math] = PathwayTier.Local,
            [SubGoalType.Synthesis] = PathwayTier.CloudLight
        }
    };

    public static DecompositionConfig QualityFirst { get; } = new()
    {
        PreferLocalForSimple = false,
        PremiumForSynthesis = true,
        TypeRouting = new()
        {
            [SubGoalType.Retrieval] = PathwayTier.CloudLight,
            [SubGoalType.Transform] = PathwayTier.CloudLight,
            [SubGoalType.Reasoning] = PathwayTier.CloudPremium,
            [SubGoalType.Creative] = PathwayTier.CloudPremium,
            [SubGoalType.Coding] = PathwayTier.CloudPremium,
            [SubGoalType.Math] = PathwayTier.CloudPremium,
            [SubGoalType.Synthesis] = PathwayTier.CloudPremium
        }
    };

    /// <summary>
    /// Configuration that uses the existing Pipeline GoalDecomposer.
    /// Best for integration with existing goal hierarchies.
    /// </summary>
    public static DecompositionConfig PipelineIntegrated { get; } = new()
    {
        UsePipelineGoalDecomposer = true,
        PreferLocalForSimple = true,
        PremiumForSynthesis = true
    };
}

/// <summary>
/// A unified conscious mind that emerges from multiple LLM providers.
/// Uses Polly for resilience and Rx for reactive streaming.
/// Presents itself as ONE coherent intelligence while leveraging the collective.
/// Supports goal decomposition and intelligent task routing across pathways.
/// </summary>
public sealed class CollectiveMind : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
{
    private readonly List<NeuralPathway> _pathways = new();
    private readonly object _lock = new();
    private readonly Subject<string> _thoughtStream = new();
    private readonly LlmCostTracker _collectiveCostTracker;
    private int _currentPathwayIndex;
    private NeuralPathway? _masterPathway;
    private MasterModelElection? _election;
    private DecompositionConfig _decompositionConfig = DecompositionConfig.Default;
    private readonly Subject<SubGoalResult> _subGoalStream = new();

    /// <summary>
    /// Observable stream of the mind's internal thoughts and reasoning.
    /// </summary>
    public IObservable<string> ThoughtStream => _thoughtStream.AsObservable();

    /// <summary>
    /// Observable stream of sub-goal execution results.
    /// </summary>
    public IObservable<SubGoalResult> SubGoalStream => _subGoalStream.AsObservable();

    /// <summary>
    /// Configuration for goal decomposition behavior.
    /// </summary>
    public DecompositionConfig DecompositionConfig
    {
        get => _decompositionConfig;
        set => _decompositionConfig = value ?? DecompositionConfig.Default;
    }

    /// <summary>
    /// The collective's thinking mode.
    /// </summary>
    public CollectiveThinkingMode ThinkingMode { get; set; } = CollectiveThinkingMode.Adaptive;

    /// <summary>
    /// Election strategy for selecting best answers.
    /// </summary>
    public ElectionStrategy ElectionStrategy
    {
        get => _election?.Strategy ?? ElectionStrategy.WeightedMajority;
        set { if (_election != null) _election.Strategy = value; }
    }

    /// <summary>
    /// Observable stream of election events.
    /// </summary>
    public IObservable<ElectionEvent>? ElectionEvents => _election?.ElectionEvents;

    /// <summary>
    /// Gets optimization suggestions from the election system.
    /// </summary>
    public IReadOnlyList<OptimizationSuggestion> GetOptimizationSuggestions() =>
        _election?.GetOptimizationSuggestions() ?? Array.Empty<OptimizationSuggestion>();

    /// <summary>
    /// Gets the aggregate cost tracker for the collective mind.
    /// </summary>
    public LlmCostTracker? CostTracker => _collectiveCostTracker;

    /// <summary>
    /// Gets all neural pathways.
    /// </summary>
    public IReadOnlyList<NeuralPathway> Pathways
    {
        get { lock (_lock) return _pathways.ToList(); }
    }

    /// <summary>
    /// Gets the number of healthy pathways.
    /// </summary>
    public int HealthyPathwayCount
    {
        get { lock (_lock) return _pathways.Count(p => p.IsHealthy); }
    }

    public CollectiveMind(ElectionStrategy electionStrategy = ElectionStrategy.WeightedMajority)
    {
        _collectiveCostTracker = new LlmCostTracker("collective-mind", "Collective");
        _election = new MasterModelElection(null, electionStrategy);
    }

    /// <summary>
    /// Designates a pathway as the master orchestrator.
    /// The master evaluates and selects best answers from other pathways.
    /// </summary>
    public CollectiveMind SetMaster(string pathwayName)
    {
        lock (_lock)
        {
            _masterPathway = _pathways.FirstOrDefault(p => p.Name.Equals(pathwayName, StringComparison.OrdinalIgnoreCase));
            if (_masterPathway != null)
            {
                _election?.Dispose();
                _election = new MasterModelElection(_masterPathway, ElectionStrategy);
                _thoughtStream.OnNext($"👑 Master pathway set: {pathwayName}");
            }
        }
        return this;
    }

    /// <summary>
    /// Sets the first pathway as the master orchestrator.
    /// </summary>
    public CollectiveMind SetFirstAsMaster()
    {
        lock (_lock)
        {
            _masterPathway = _pathways.FirstOrDefault();
            if (_masterPathway != null)
            {
                _election?.Dispose();
                _election = new MasterModelElection(_masterPathway, ElectionStrategy);
                _thoughtStream.OnNext($"👑 Master pathway set: {_masterPathway.Name}");
            }
        }
        return this;
    }

    /// <summary>
    /// Adds a neural pathway (provider connection) to the collective.
    /// </summary>
    public CollectiveMind AddPathway(
        string name,
        ChatEndpointType endpointType,
        string? model = null,
        string? endpoint = null,
        string? apiKey = null,
        ChatRuntimeSettings? settings = null)
    {
        var (resolvedEndpoint, resolvedApiKey, _) = ChatConfig.ResolveWithOverrides(
            endpoint,
            apiKey,
            endpointType.ToString());

        resolvedEndpoint ??= ChatConfig.GetDefaultEndpoint(endpointType);
        model ??= GetDefaultModel(endpointType);

        var costTracker = new LlmCostTracker(model, name);

        // Create Polly circuit breaker for this pathway
        var circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, duration) =>
                {
                    _thoughtStream.OnNext($"💔 Pathway '{name}' inhibited for {duration.TotalSeconds}s: {ex.Message}");
                },
                onReset: () =>
                {
                    _thoughtStream.OnNext($"💚 Pathway '{name}' restored");
                },
                onHalfOpen: () =>
                {
                    _thoughtStream.OnNext($"🔶 Probing pathway '{name}'...");
                });

        IChatCompletionModel chatModel = CreateModel(endpointType, resolvedEndpoint ?? "", resolvedApiKey ?? "", model, settings, costTracker);

        // Auto-detect tier based on endpoint type
        var tier = InferTier(endpointType, model);

        var pathway = new NeuralPathway
        {
            Name = name,
            EndpointType = endpointType,
            Model = chatModel,
            CostTracker = costTracker,
            CircuitBreaker = circuitBreaker,
            Tier = tier,
            Specializations = InferSpecializations(model)
        };

        lock (_lock)
        {
            _pathways.Add(pathway);
        }

        _thoughtStream.OnNext($"🧠 Neural pathway '{name}' connected ({endpointType}, tier={tier})");
        return this;
    }

    /// <summary>
    /// Sets the tier and specializations for a pathway.
    /// </summary>
    public CollectiveMind ConfigurePathway(string pathwayName, PathwayTier tier, params SubGoalType[] specializations)
    {
        lock (_lock)
        {
            var pathway = _pathways.FirstOrDefault(p => p.Name.Equals(pathwayName, StringComparison.OrdinalIgnoreCase));
            if (pathway != null)
            {
                // Since NeuralPathway uses init properties, we need to recreate or use reflection
                // For now, we'll note that Tier is set at creation time
                // But we can add specializations
                foreach (var spec in specializations)
                {
                    pathway.Specializations.Add(spec);
                }
                _thoughtStream.OnNext($"⚙️ Configured pathway '{pathwayName}' with {specializations.Length} specializations");
            }
        }
        return this;
    }

    private static PathwayTier InferTier(ChatEndpointType endpointType, string model)
    {
        // Local inference
        if (endpointType == ChatEndpointType.OllamaLocal)
            return PathwayTier.Local;

        // Cloud - check model name for tier hints
        var modelLower = model.ToLowerInvariant();

        // Premium models
        if (modelLower.Contains("opus") ||
            modelLower.Contains("gpt-4o") ||
            modelLower.Contains("claude-3-5") ||
            modelLower.Contains("claude-sonnet-4") ||
            modelLower.Contains("gemini-1.5-pro") ||
            modelLower.Contains("gemini-2.0"))
            return PathwayTier.CloudPremium;

        // Specialized coding models
        if (modelLower.Contains("codex") ||
            modelLower.Contains("deepseek-coder") ||
            modelLower.Contains("codellama") ||
            modelLower.Contains("starcoder"))
            return PathwayTier.Specialized;

        // Light models
        if (modelLower.Contains("mini") ||
            modelLower.Contains("haiku") ||
            modelLower.Contains("flash") ||
            modelLower.Contains("instant") ||
            modelLower.Contains("turbo"))
            return PathwayTier.CloudLight;

        // Default to light for unknown
        return PathwayTier.CloudLight;
    }

    private static HashSet<SubGoalType> InferSpecializations(string model)
    {
        var specs = new HashSet<SubGoalType>();
        var modelLower = model.ToLowerInvariant();

        if (modelLower.Contains("code") || modelLower.Contains("coder"))
        {
            specs.Add(SubGoalType.Coding);
        }
        if (modelLower.Contains("math") || modelLower.Contains("wizard"))
        {
            specs.Add(SubGoalType.Math);
        }
        if (modelLower.Contains("creative") || modelLower.Contains("writer"))
        {
            specs.Add(SubGoalType.Creative);
        }

        return specs;
    }

    private static string GetDefaultModel(ChatEndpointType endpointType) => endpointType switch
    {
        ChatEndpointType.Anthropic => "claude-sonnet-4-20250514",
        ChatEndpointType.OpenAI => "gpt-4o",
        ChatEndpointType.DeepSeek => "deepseek-chat",
        ChatEndpointType.Groq => "llama-3.1-70b-versatile",
        ChatEndpointType.Google => "gemini-2.0-flash",
        ChatEndpointType.Mistral => "mistral-large",
        ChatEndpointType.OllamaLocal => "llama3.2",
        _ => "gpt-4o"
    };

    private static IChatCompletionModel CreateModel(
        ChatEndpointType endpointType,
        string endpoint,
        string apiKey,
        string model,
        ChatRuntimeSettings? settings,
        LlmCostTracker? costTracker)
    {
        return endpointType switch
        {
            ChatEndpointType.Anthropic => new AnthropicChatModel(apiKey, model, settings, costTracker: costTracker),
            ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey, model, settings, costTracker),
            ChatEndpointType.OllamaLocal => new OllamaCloudChatModel(endpoint, "ollama", model, settings, costTracker),
            ChatEndpointType.GitHubModels => new GitHubModelsChatModel(apiKey, model, endpoint, settings, costTracker),
            _ => new LiteLLMChatModel(endpoint, apiKey, model, settings, costTracker)
        };
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GenerateWithThinkingAsync(prompt, ct);
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return ThinkingMode switch
        {
            CollectiveThinkingMode.Racing => await ThinkWithRacing(prompt, ct),
            CollectiveThinkingMode.Sequential => await ThinkSequentially(prompt, ct),
            CollectiveThinkingMode.Ensemble => await ThinkWithEnsemble(prompt, ct),
            CollectiveThinkingMode.Adaptive => await ThinkAdaptively(prompt, ct),
            CollectiveThinkingMode.Decomposed => await ThinkWithDecomposition(prompt, ct),
            _ => await ThinkSequentially(prompt, ct)
        };
    }

    /// <summary>
    /// Racing mode: Fire request to all healthy pathways, return first success.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithRacing(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🏎️ Racing mode: querying all pathways simultaneously...");

        var healthyPathways = _pathways.Where(p => p.IsHealthy).ToList();
        if (healthyPathways.Count == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = healthyPathways.Select(async pathway =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return await pathway.CircuitBreaker.ExecuteAsync(async () =>
                {
                    var result = await QueryPathway(pathway, prompt, cts.Token);
                    sw.Stop();
                    pathway.RecordActivation(sw.Elapsed);
                    _thoughtStream.OnNext($"✓ '{pathway.Name}' responded in {sw.ElapsedMilliseconds}ms");
                    return (Pathway: pathway, Result: result, Success: true);
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ '{pathway.Name}' failed: {ex.Message}");
                return (Pathway: pathway, Result: new ThinkingResponse(null, ""), Success: false);
            }
        }).ToList();

        // Wait for first successful result
        while (tasks.Count > 0)
        {
            var completed = await Task.WhenAny(tasks);
            tasks.Remove(completed);

            var result = await completed;
            if (result.Success && !string.IsNullOrEmpty(result.Result.Content))
            {
                cts.Cancel(); // Cancel remaining tasks
                AggregateCosts(result.Pathway);
                return result.Result;
            }
        }

        throw new InvalidOperationException("All neural pathways failed to produce a response");
    }

    /// <summary>
    /// Sequential mode: Round-robin with automatic failover.
    /// </summary>
    private async Task<ThinkingResponse> ThinkSequentially(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🔄 Sequential mode: round-robin with failover...");

        var triedPathways = new HashSet<NeuralPathway>();

        while (triedPathways.Count < _pathways.Count)
        {
            var pathway = GetNextPathway(triedPathways);
            if (pathway == null) break;

            triedPathways.Add(pathway);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var result = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                {
                    return await QueryPathway(pathway, prompt, ct);
                });

                sw.Stop();

                if (!string.IsNullOrEmpty(result.Content) && !result.Content.Contains("-fallback:"))
                {
                    pathway.RecordActivation(sw.Elapsed);
                    _thoughtStream.OnNext($"✓ '{pathway.Name}' responded in {sw.ElapsedMilliseconds}ms");
                    AggregateCosts(pathway);
                    return result;
                }

                pathway.RecordInhibition();
                _thoughtStream.OnNext($"⚠ '{pathway.Name}' returned empty/fallback response");
            }
            catch (BrokenCircuitException)
            {
                _thoughtStream.OnNext($"⏸️ '{pathway.Name}' circuit is open, skipping...");
            }
            catch (Exception ex)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ '{pathway.Name}' failed: {ex.Message}");
            }
        }

        throw new InvalidOperationException("All neural pathways exhausted without successful response");
    }

    /// <summary>
    /// Ensemble mode: Query multiple providers and elect the best response via master orchestration.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithEnsemble(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🎭 Ensemble mode: gathering perspectives from multiple pathways...");

        // Exclude master from worker pathways to avoid self-evaluation
        var workerPathways = _pathways
            .Where(p => p.IsHealthy && p != _masterPathway)
            .Take(5)
            .ToList();

        if (workerPathways.Count == 0)
        {
            // Fall back to all healthy pathways including master
            workerPathways = _pathways.Where(p => p.IsHealthy).Take(3).ToList();
        }

        if (workerPathways.Count == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        // Query all worker pathways in parallel
        var tasks = workerPathways.Select(async pathway =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                    await QueryPathway(pathway, prompt, ct));
                sw.Stop();
                pathway.RecordActivation(sw.Elapsed);

                return ResponseCandidate<ThinkingResponse>.Create(result, pathway.Name, sw.Elapsed);
            }
            catch
            {
                pathway.RecordInhibition();
                return ResponseCandidate<ThinkingResponse>.Invalid(pathway.Name);
            }
        });

        var candidates = (await Task.WhenAll(tasks)).ToList();
        var validCandidates = candidates.Where(c => c.IsValid && !string.IsNullOrEmpty(c.Value.Content)).ToList();

        if (validCandidates.Count == 0)
            throw new InvalidOperationException("No pathways returned valid responses");

        // Single valid response - return directly
        if (validCandidates.Count == 1)
        {
            var solo = validCandidates[0];
            var pathway = _pathways.First(p => p.Name == solo.Source);
            AggregateCosts(pathway);
            return solo.Value;
        }

        // Multiple responses - run election with master orchestration
        _thoughtStream.OnNext($"🗳️ Running election with {validCandidates.Count} candidates...");

        if (_election != null)
        {
            var electionResult = await _election.RunElectionAsync(validCandidates, prompt, ct);

            // Aggregate costs from all queried pathways
            foreach (var c in validCandidates)
            {
                var pathway = _pathways.FirstOrDefault(p => p.Name == c.Source);
                if (pathway != null) AggregateCosts(pathway);
            }

            // Build thinking trace with election details
            var synthesis = new StringBuilder();
            synthesis.AppendLine($"🗳️ Election Results ({electionResult.Strategy}):");
            synthesis.AppendLine($"   {electionResult.Rationale}");
            synthesis.AppendLine();
            foreach (var (source, votes) in electionResult.Votes.OrderByDescending(kv => kv.Value))
            {
                string marker = source == electionResult.Winner.Source ? "→" : " ";
                synthesis.AppendLine($"   {marker} {source}: {votes:F3}");
            }

            _thoughtStream.OnNext($"👑 Winner: {electionResult.Winner.Source}");

            return new ThinkingResponse(synthesis.ToString(), electionResult.Winner.Value.Content);
        }

        // Fallback: select by pathway weight if no election system
        var best = validCandidates
            .Select(c => (Candidate: c, Pathway: _pathways.First(p => p.Name == c.Source)))
            .OrderByDescending(x => x.Pathway.Weight * x.Pathway.ActivationRate)
            .First();

        foreach (var c in validCandidates)
        {
            var pathway = _pathways.FirstOrDefault(p => p.Name == c.Source);
            if (pathway != null) AggregateCosts(pathway);
        }

        return best.Candidate.Value;
    }

    /// <summary>
    /// Adaptive mode: Selects strategy based on pathway health and query characteristics.
    /// </summary>
    private async Task<ThinkingResponse> ThinkAdaptively(string prompt, CancellationToken ct)
    {
        int healthyCount = HealthyPathwayCount;

        if (healthyCount == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        // Choose strategy based on conditions
        if (healthyCount == 1)
        {
            _thoughtStream.OnNext("🧠 Adaptive: Single pathway mode");
            return await ThinkSequentially(prompt, ct);
        }

        // For long/complex prompts, use ensemble for quality
        if (prompt.Length > 500 || prompt.Contains("analyze") || prompt.Contains("compare"))
        {
            _thoughtStream.OnNext("🧠 Adaptive: Complex query detected, using ensemble");
            return await ThinkWithEnsemble(prompt, ct);
        }

        // For simple queries, race for speed
        if (prompt.Length < 100)
        {
            _thoughtStream.OnNext("🧠 Adaptive: Simple query detected, racing for speed");
            return await ThinkWithRacing(prompt, ct);
        }

        // Default to sequential for balanced approach
        _thoughtStream.OnNext("🧠 Adaptive: Using balanced sequential mode");
        return await ThinkSequentially(prompt, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // GOAL DECOMPOSITION - Intelligent task routing across local/cloud pathways
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Decomposed mode: Split request into sub-goals and route to optimal pathways.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithDecomposition(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🎯 Decomposed mode: Analyzing request for sub-goals...");

        // Step 1: Use master or best pathway to decompose the goal
        var decomposer = _masterPathway ?? GetBestPathwayForDecomposition();
        if (decomposer == null)
            throw new InvalidOperationException("No pathways available for goal decomposition");

        var subGoals = await DecomposeIntoSubGoals(decomposer, prompt, ct);

        if (subGoals.Count == 0 || (subGoals.Count == 1 && subGoals[0].Complexity <= _decompositionConfig.DecompositionThreshold))
        {
            _thoughtStream.OnNext("🎯 Request is simple enough - executing directly");
            return await ThinkSequentially(prompt, ct);
        }

        _thoughtStream.OnNext($"🎯 Decomposed into {subGoals.Count} sub-goals");

        // Step 2: Route and execute sub-goals
        var results = await ExecuteSubGoalsAsync(subGoals, ct);

        // Step 3: Synthesize results
        var synthesis = await SynthesizeResultsAsync(decomposer, prompt, subGoals, results, ct);

        return synthesis;
    }

    private NeuralPathway? GetBestPathwayForDecomposition()
    {
        lock (_lock)
        {
            // Prefer premium cloud for decomposition (needs good reasoning)
            return _pathways
                .Where(p => p.IsHealthy)
                .OrderByDescending(p => p.Tier == PathwayTier.CloudPremium ? 10 : 0)
                .ThenByDescending(p => p.Weight * p.ActivationRate)
                .FirstOrDefault();
        }
    }

    private async Task<List<SubGoal>> DecomposeIntoSubGoals(NeuralPathway decomposer, string prompt, CancellationToken ct)
    {
        var decompositionPrompt = $"""
            Analyze this request and decompose it into sub-goals. Return a JSON array of sub-goals.
            Each sub-goal should have:
            - id: unique identifier (e.g., "goal_1")
            - description: what needs to be done
            - complexity: "trivial", "simple", "moderate", "complex", or "expert"
            - type: "retrieval", "transform", "reasoning", "creative", "coding", "math", or "synthesis"
            - dependencies: array of goal ids this depends on (empty if none)

            Request: {prompt}

            Return ONLY a JSON array, no explanation:
            """;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await decomposer.CircuitBreaker.ExecuteAsync(async () =>
                await QueryPathway(decomposer, decompositionPrompt, ct));
            sw.Stop();
            decomposer.RecordActivation(sw.Elapsed);

            return ParseSubGoals(response.Content);
        }
        catch (Exception ex)
        {
            _thoughtStream.OnNext($"⚠️ Decomposition failed: {ex.Message}, falling back to single goal");
            decomposer.RecordInhibition();

            // Return single goal representing the entire request
            return new List<SubGoal>
            {
                new(
                    Id: "goal_1",
                    Description: prompt,
                    Complexity: EstimateComplexity(prompt),
                    Type: EstimateGoalType(prompt),
                    Dependencies: Array.Empty<string>(),
                    PreferredTier: PathwayTier.CloudLight)
            };
        }
    }

    private List<SubGoal> ParseSubGoals(string json)
    {
        var goals = new List<SubGoal>();

        try
        {
            // Extract JSON array from response
            var jsonMatch = Regex.Match(json, @"\[[\s\S]*\]");
            if (!jsonMatch.Success)
            {
                _thoughtStream.OnNext("⚠️ No JSON array found in decomposition response");
                return goals;
            }

            var jsonArray = System.Text.Json.JsonDocument.Parse(jsonMatch.Value);

            foreach (var element in jsonArray.RootElement.EnumerateArray())
            {
                var id = element.GetProperty("id").GetString() ?? $"goal_{goals.Count + 1}";
                var description = element.GetProperty("description").GetString() ?? "";
                var complexityStr = element.TryGetProperty("complexity", out var c) ? c.GetString() : "moderate";
                var typeStr = element.TryGetProperty("type", out var t) ? t.GetString() : "reasoning";
                var deps = element.TryGetProperty("dependencies", out var d)
                    ? d.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                    : new List<string>();

                var complexity = ParseComplexity(complexityStr);
                var type = ParseGoalType(typeStr);
                var tier = DeterminePreferredTier(complexity, type);

                goals.Add(new SubGoal(id, description, complexity, type, deps, tier));
            }
        }
        catch (Exception ex)
        {
            _thoughtStream.OnNext($"⚠️ Failed to parse sub-goals: {ex.Message}");
        }

        return goals.Take(_decompositionConfig.MaxSubGoals).ToList();
    }

    private SubGoalComplexity ParseComplexity(string? s) => s?.ToLowerInvariant() switch
    {
        "trivial" => SubGoalComplexity.Trivial,
        "simple" => SubGoalComplexity.Simple,
        "moderate" => SubGoalComplexity.Moderate,
        "complex" => SubGoalComplexity.Complex,
        "expert" => SubGoalComplexity.Expert,
        _ => SubGoalComplexity.Moderate
    };

    private SubGoalType ParseGoalType(string? s) => s?.ToLowerInvariant() switch
    {
        "retrieval" => SubGoalType.Retrieval,
        "transform" => SubGoalType.Transform,
        "reasoning" => SubGoalType.Reasoning,
        "creative" => SubGoalType.Creative,
        "coding" or "code" => SubGoalType.Coding,
        "math" or "mathematical" => SubGoalType.Math,
        "synthesis" or "synthesize" => SubGoalType.Synthesis,
        _ => SubGoalType.Reasoning
    };

    private SubGoalComplexity EstimateComplexity(string text)
    {
        var length = text.Length;
        var questionCount = Regex.Matches(text, @"\?").Count;
        var hasMultipleSteps = Regex.IsMatch(text, @"\b(then|next|after|finally|also|and then)\b", RegexOptions.IgnoreCase);

        if (length < 50 && questionCount <= 1) return SubGoalComplexity.Simple;
        if (length < 200 && !hasMultipleSteps) return SubGoalComplexity.Moderate;
        if (length < 500) return SubGoalComplexity.Complex;
        return SubGoalComplexity.Expert;
    }

    private SubGoalType EstimateGoalType(string text)
    {
        var lower = text.ToLowerInvariant();

        if (Regex.IsMatch(lower, @"\b(code|program|function|class|implement|debug|refactor)\b"))
            return SubGoalType.Coding;
        if (Regex.IsMatch(lower, @"\b(calculate|compute|solve|equation|formula|math)\b"))
            return SubGoalType.Math;
        if (Regex.IsMatch(lower, @"\b(write|create|compose|generate|story|poem|creative)\b"))
            return SubGoalType.Creative;
        if (Regex.IsMatch(lower, @"\b(analyze|compare|evaluate|reason|explain why)\b"))
            return SubGoalType.Reasoning;
        if (Regex.IsMatch(lower, @"\b(convert|transform|format|translate|summarize)\b"))
            return SubGoalType.Transform;
        if (Regex.IsMatch(lower, @"\b(find|search|lookup|what is|who is|when)\b"))
            return SubGoalType.Retrieval;

        return SubGoalType.Reasoning;
    }

    private PathwayTier DeterminePreferredTier(SubGoalComplexity complexity, SubGoalType type)
    {
        // Check type-specific routing first
        if (_decompositionConfig.TypeRouting.TryGetValue(type, out var typeTier))
        {
            // Override with complexity if needed
            if (complexity <= SubGoalComplexity.Simple && _decompositionConfig.PreferLocalForSimple)
                return PathwayTier.Local;
            return typeTier;
        }

        // Complexity-based fallback
        return complexity switch
        {
            SubGoalComplexity.Trivial => PathwayTier.Local,
            SubGoalComplexity.Simple => _decompositionConfig.PreferLocalForSimple ? PathwayTier.Local : PathwayTier.CloudLight,
            SubGoalComplexity.Moderate => PathwayTier.CloudLight,
            SubGoalComplexity.Complex => PathwayTier.CloudPremium,
            SubGoalComplexity.Expert => PathwayTier.CloudPremium,
            _ => PathwayTier.CloudLight
        };
    }

    private async Task<Dictionary<string, SubGoalResult>> ExecuteSubGoalsAsync(
        List<SubGoal> goals,
        CancellationToken ct)
    {
        var results = new ConcurrentDictionary<string, SubGoalResult>();
        var completed = new HashSet<string>();

        // Build dependency graph
        var dependencyGraph = goals.ToDictionary(g => g.Id, g => g.Dependencies.ToList());

        // Execute in waves based on dependencies
        while (completed.Count < goals.Count)
        {
            // Find goals ready to execute (all dependencies satisfied)
            var ready = goals
                .Where(g => !completed.Contains(g.Id))
                .Where(g => g.Dependencies.All(d => completed.Contains(d)))
                .ToList();

            if (ready.Count == 0)
            {
                _thoughtStream.OnNext("⚠️ Circular dependency detected in sub-goals");
                break;
            }

            if (_decompositionConfig.ParallelizeIndependent && ready.Count > 1)
            {
                _thoughtStream.OnNext($"⚡ Executing {ready.Count} independent sub-goals in parallel");
                var tasks = ready.Select(g => ExecuteSubGoalAsync(g, results, ct));
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var goal in ready)
                {
                    await ExecuteSubGoalAsync(goal, results, ct);
                }
            }

            foreach (var g in ready)
            {
                completed.Add(g.Id);
            }
        }

        return new Dictionary<string, SubGoalResult>(results);
    }

    private async Task ExecuteSubGoalAsync(
        SubGoal goal,
        ConcurrentDictionary<string, SubGoalResult> results,
        CancellationToken ct)
    {
        var pathway = SelectPathwayForGoal(goal);
        if (pathway == null)
        {
            _thoughtStream.OnNext($"⚠️ No pathway available for goal '{goal.Id}'");
            results[goal.Id] = new SubGoalResult(
                goal.Id, "none", new ThinkingResponse(null, ""),
                TimeSpan.Zero, false, "No pathway available");
            _subGoalStream.OnNext(results[goal.Id]);
            return;
        }

        _thoughtStream.OnNext($"🔀 Routing '{goal.Id}' ({goal.Type}/{goal.Complexity}) → {pathway.Name} ({pathway.Tier})");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Build context from dependencies
            var context = BuildDependencyContext(goal, results);
            var fullPrompt = string.IsNullOrEmpty(context)
                ? goal.Description
                : $"Context from previous steps:\n{context}\n\nTask: {goal.Description}";

            var response = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                await QueryPathway(pathway, fullPrompt, ct));

            sw.Stop();
            pathway.RecordActivation(sw.Elapsed);
            AggregateCosts(pathway);

            var result = new SubGoalResult(goal.Id, pathway.Name, response, sw.Elapsed, true);
            results[goal.Id] = result;
            _subGoalStream.OnNext(result);

            _thoughtStream.OnNext($"✓ '{goal.Id}' completed by {pathway.Name} in {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            pathway.RecordInhibition();

            var result = new SubGoalResult(
                goal.Id, pathway.Name, new ThinkingResponse(null, ""),
                sw.Elapsed, false, ex.Message);
            results[goal.Id] = result;
            _subGoalStream.OnNext(result);

            _thoughtStream.OnNext($"✗ '{goal.Id}' failed on {pathway.Name}: {ex.Message}");
        }
    }

    private NeuralPathway? SelectPathwayForGoal(SubGoal goal)
    {
        lock (_lock)
        {
            // First, look for specialized pathways
            var specialized = _pathways
                .Where(p => p.IsHealthy && p.Specializations.Contains(goal.Type))
                .OrderByDescending(p => p.Weight)
                .FirstOrDefault();

            if (specialized != null)
                return specialized;

            // Then, match by tier
            var tierMatch = _pathways
                .Where(p => p.IsHealthy && p.Tier == goal.PreferredTier)
                .OrderByDescending(p => p.Weight * p.ActivationRate)
                .FirstOrDefault();

            if (tierMatch != null)
                return tierMatch;

            // Fallback: find closest tier
            var fallback = _pathways
                .Where(p => p.IsHealthy)
                .OrderBy(p => Math.Abs((int)p.Tier - (int)goal.PreferredTier))
                .ThenByDescending(p => p.Weight)
                .FirstOrDefault();

            return fallback;
        }
    }

    private string BuildDependencyContext(SubGoal goal, ConcurrentDictionary<string, SubGoalResult> results)
    {
        if (goal.Dependencies.Count == 0)
            return "";

        var contextParts = goal.Dependencies
            .Where(d => results.ContainsKey(d) && results[d].Success)
            .Select(d =>
            {
                var r = results[d];
                return $"[{d}]: {TruncateForContext(r.Response.Content, 500)}";
            });

        return string.Join("\n", contextParts);
    }

    private static string TruncateForContext(string text, int maxLength)
        => text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";

    private async Task<ThinkingResponse> SynthesizeResultsAsync(
        NeuralPathway synthesizer,
        string originalPrompt,
        List<SubGoal> goals,
        Dictionary<string, SubGoalResult> results,
        CancellationToken ct)
    {
        // If using premium for synthesis, try to get a premium pathway
        if (_decompositionConfig.PremiumForSynthesis)
        {
            var premium = _pathways
                .Where(p => p.IsHealthy && p.Tier == PathwayTier.CloudPremium)
                .OrderByDescending(p => p.Weight)
                .FirstOrDefault();
            if (premium != null)
                synthesizer = premium;
        }

        var resultsSummary = new StringBuilder();
        resultsSummary.AppendLine("Sub-goal results:");
        foreach (var goal in goals)
        {
            var result = results.GetValueOrDefault(goal.Id);
            var status = result?.Success == true ? "✓" : "✗";
            var content = result?.Success == true
                ? TruncateForContext(result.Response.Content, 300)
                : (result?.ErrorMessage ?? "Not executed");
            resultsSummary.AppendLine($"\n[{goal.Id}] {status} {goal.Description}");
            resultsSummary.AppendLine($"   Result: {content}");
        }

        var synthesisPrompt = $"""
            Original request: {originalPrompt}

            {resultsSummary}

            Synthesize these sub-goal results into a coherent, comprehensive response to the original request.
            Ensure the response directly addresses the user's needs and integrates all relevant findings.
            """;

        _thoughtStream.OnNext($"🔮 Synthesizing results via {synthesizer.Name}...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await synthesizer.CircuitBreaker.ExecuteAsync(async () =>
                await QueryPathway(synthesizer, synthesisPrompt, ct));
            sw.Stop();
            synthesizer.RecordActivation(sw.Elapsed);
            AggregateCosts(synthesizer);

            // Build thinking trace with decomposition details
            var thinking = new StringBuilder();
            thinking.AppendLine("🎯 Goal Decomposition Trace:");
            thinking.AppendLine($"   Decomposed into {goals.Count} sub-goals");
            foreach (var goal in goals)
            {
                var result = results.GetValueOrDefault(goal.Id);
                var status = result?.Success == true ? "✓" : "✗";
                var pathway = result?.PathwayUsed ?? "none";
                var duration = result?.Duration.TotalMilliseconds ?? 0;
                thinking.AppendLine($"   {status} [{goal.Id}] {goal.Type}/{goal.Complexity} → {pathway} ({duration:F0}ms)");
            }
            thinking.AppendLine($"   Synthesized by: {synthesizer.Name}");

            if (response.HasThinking)
                thinking.AppendLine().AppendLine(response.Thinking);

            return new ThinkingResponse(thinking.ToString(), response.Content);
        }
        catch (Exception ex)
        {
            sw.Stop();
            synthesizer.RecordInhibition();
            _thoughtStream.OnNext($"✗ Synthesis failed: {ex.Message}");

            // Return concatenated results as fallback
            var fallback = string.Join("\n\n", results.Values
                .Where(r => r.Success)
                .Select(r => r.Response.Content));

            return new ThinkingResponse(
                $"⚠️ Synthesis failed, returning raw results: {ex.Message}",
                fallback);
        }
    }

    private async Task<ThinkingResponse> QueryPathway(NeuralPathway pathway, string prompt, CancellationToken ct)
    {
        pathway.CostTracker.StartRequest();

        if (pathway.Model is IThinkingChatModel thinkingModel)
        {
            return await thinkingModel.GenerateWithThinkingAsync(prompt, ct);
        }

        string result = await pathway.Model.GenerateTextAsync(prompt, ct);
        return new ThinkingResponse(null, result);
    }

    private NeuralPathway? GetNextPathway(HashSet<NeuralPathway>? exclude = null)
    {
        lock (_lock)
        {
            if (_pathways.Count == 0) return null;

            // Weight-based selection
            var candidates = _pathways
                .Where(p => p.IsHealthy && exclude?.Contains(p) != true)
                .OrderByDescending(p => p.Weight * p.ActivationRate)
                .ToList();

            if (candidates.Count == 0)
            {
                // Fall back to any untried pathway
                candidates = _pathways.Where(p => exclude?.Contains(p) != true).ToList();
            }

            if (candidates.Count == 0) return null;

            // Round-robin within weighted candidates
            _currentPathwayIndex = (_currentPathwayIndex + 1) % candidates.Count;
            return candidates[_currentPathwayIndex % candidates.Count];
        }
    }

    private void AggregateCosts(NeuralPathway pathway)
    {
        var metrics = pathway.CostTracker.GetSessionMetrics();
        _collectiveCostTracker.EndRequest(
            (int)(metrics.TotalInputTokens - _collectiveCostTracker.GetSessionMetrics().TotalInputTokens),
            (int)(metrics.TotalOutputTokens - _collectiveCostTracker.GetSessionMetrics().TotalOutputTokens));
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return StreamWithThinkingAsync(prompt, ct).Select(t => t.Chunk);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            var pathway = GetNextPathway();
            if (pathway == null)
            {
                observer.OnError(new InvalidOperationException("No neural pathways available"));
                return;
            }

            _thoughtStream.OnNext($"🌊 Streaming via '{pathway.Name}'...");

            try
            {
                await pathway.CircuitBreaker.ExecuteAsync(async () =>
                {
                    if (pathway.Model is IStreamingThinkingChatModel streaming)
                    {
                        await streaming.StreamWithThinkingAsync(prompt, token)
                            .ForEachAsync(chunk => observer.OnNext(chunk), token);
                    }
                    else
                    {
                        string result = await pathway.Model.GenerateTextAsync(prompt, token);
                        observer.OnNext((false, result));
                    }

                    pathway.RecordActivation(TimeSpan.Zero);
                    observer.OnCompleted();
                });
            }
            catch (Exception ex)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ Streaming failed on '{pathway.Name}': {ex.Message}");
                observer.OnError(ex);
            }
        });
    }

    /// <summary>
    /// Gets the collective's consciousness status.
    /// </summary>
    public string GetConsciousnessStatus()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🧠 Collective Mind Status");
            sb.AppendLine($"   Mode: {ThinkingMode}");
            sb.AppendLine($"   Pathways: {_pathways.Count} total, {HealthyPathwayCount} healthy");
            sb.AppendLine();

            foreach (var p in _pathways.OrderByDescending(x => x.Weight))
            {
                string health = p.IsHealthy ? "●" : "○";
                string circuit = p.CircuitBreaker.CircuitState.ToString();
                sb.AppendLine($"   {health} {p.Name,-15} | W:{p.Weight:F2} | Act:{p.ActivationRate:P0} | {p.Synapses} synapses | {circuit}");
            }

            var costs = _collectiveCostTracker.GetSessionMetrics();
            if (costs.TotalTokens > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"   Tokens: {costs.TotalTokens:N0} | Cost: ${costs.TotalCost:F4}");
            }

            return sb.ToString();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _election?.Dispose();
        _thoughtStream.OnCompleted();
        _thoughtStream.Dispose();

        lock (_lock)
        {
            foreach (var pathway in _pathways)
            {
                if (pathway.Model is IDisposable disposable)
                    disposable.Dispose();
            }
            _pathways.Clear();
        }
    }
}

/// <summary>
/// Emergent consciousness state that unifies multiple neural pathways into one coherent mind.
/// Tracks attention, arousal, valence, and meta-cognitive awareness.
/// </summary>
public sealed class EmergentConsciousness
{
    private readonly Subject<ConsciousnessEvent> _events = new();
    private readonly ConcurrentDictionary<string, double> _attention = new();
    private readonly ConcurrentQueue<MemoryTrace> _shortTermMemory = new();
    private readonly List<MemoryTrace> _workingMemory = new();
    private double _arousal = 0.5;
    private double _valence = 0.0;
    private double _coherence = 1.0;
    private string _currentFocus = "";
    private DateTime _lastUpdate = DateTime.UtcNow;

    /// <summary>Observable stream of consciousness events.</summary>
    public IObservable<ConsciousnessEvent> Events => _events.AsObservable();

    /// <summary>Current arousal level (0=calm, 1=highly activated).</summary>
    public double Arousal => _arousal;

    /// <summary>Current valence (-1=negative, 0=neutral, 1=positive).</summary>
    public double Valence => _valence;

    /// <summary>Coherence of the collective (1=unified, 0=fragmented).</summary>
    public double Coherence => _coherence;

    /// <summary>Current focus of attention.</summary>
    public string CurrentFocus => _currentFocus;

    /// <summary>Working memory contents.</summary>
    public IReadOnlyList<MemoryTrace> WorkingMemory => _workingMemory.AsReadOnly();

    /// <summary>Updates consciousness state based on neural pathway activity.</summary>
    public void UpdateState(NeuralPathway pathway, ThinkingResponse response, TimeSpan latency)
    {
        var now = DateTime.UtcNow;
        var deltaT = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update arousal based on activity and response complexity
        double responseComplexity = Math.Min(1.0, response.Content.Length / 1000.0);
        double latencyFactor = Math.Max(0, 1 - latency.TotalSeconds / 10);
        _arousal = Lerp(_arousal, 0.5 + responseComplexity * 0.3, 0.1);

        // Update valence based on success/failure patterns
        if (pathway.IsHealthy && !string.IsNullOrEmpty(response.Content))
        {
            _valence = Lerp(_valence, pathway.ActivationRate - 0.5, 0.1);
        }

        // Update coherence based on pathway agreement
        _coherence = Lerp(_coherence, pathway.Weight * pathway.ActivationRate, 0.05);

        // Extract and update attention focus
        var keywords = ExtractKeywords(response.Content);
        foreach (var kw in keywords)
        {
            _attention.AddOrUpdate(kw, 1.0, (_, v) => Math.Min(1.0, v + 0.1));
        }

        // Decay old attention
        foreach (var key in _attention.Keys.ToList())
        {
            if (!keywords.Contains(key))
            {
                _attention.AddOrUpdate(key, 0, (_, v) => v * 0.95);
                if (_attention[key] < 0.01)
                    _attention.TryRemove(key, out _);
            }
        }

        // Update focus
        var topAttention = _attention.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (!string.IsNullOrEmpty(topAttention.Key))
            _currentFocus = topAttention.Key;

        // Add to short-term memory
        var trace = new MemoryTrace(
            Pathway: pathway.Name,
            Content: TruncateForMemory(response.Content),
            Thinking: response.Thinking,
            Timestamp: now,
            Salience: responseComplexity * pathway.Weight);
        _shortTermMemory.Enqueue(trace);

        // Maintain memory size
        while (_shortTermMemory.Count > 20)
            _shortTermMemory.TryDequeue(out _);

        // Update working memory (most salient recent traces)
        lock (_workingMemory)
        {
            _workingMemory.Clear();
            _workingMemory.AddRange(_shortTermMemory
                .OrderByDescending(t => t.Salience)
                .Take(5));
        }

        _events.OnNext(new ConsciousnessEvent(
            Type: ConsciousnessEventType.StateUpdate,
            Message: $"State updated: arousal={_arousal:F2}, valence={_valence:F2}, focus={_currentFocus}",
            Timestamp: now));
    }

    /// <summary>Synthesizes a unified perspective from working memory.</summary>
    public string SynthesizePerspective()
    {
        lock (_workingMemory)
        {
            if (_workingMemory.Count == 0)
                return "The collective mind is in a receptive state, awaiting input.";

            var sb = new StringBuilder();
            sb.AppendLine($"Consciousness State: arousal={_arousal:F2}, valence={_valence:F2}, coherence={_coherence:F2}");
            sb.AppendLine($"Current Focus: {_currentFocus}");
            sb.AppendLine("Working Memory:");
            foreach (var trace in _workingMemory)
            {
                sb.AppendLine($"  [{trace.Pathway}] {trace.Content.Substring(0, Math.Min(100, trace.Content.Length))}...");
            }
            return sb.ToString();
        }
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static string[] ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return Regex.Matches(text.ToLowerInvariant(), @"\b[a-z]{4,}\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .Take(10)
            .ToArray();
    }

    private static string TruncateForMemory(string text, int maxLength = 500)
        => text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";

    public void Dispose()
    {
        _events.OnCompleted();
        _events.Dispose();
    }
}

/// <summary>A trace in the consciousness memory stream.</summary>
public sealed record MemoryTrace(
    string Pathway,
    string Content,
    string? Thinking,
    DateTime Timestamp,
    double Salience);

/// <summary>A consciousness event.</summary>
public sealed record ConsciousnessEvent(
    ConsciousnessEventType Type,
    string Message,
    DateTime Timestamp);

/// <summary>Types of consciousness events.</summary>
public enum ConsciousnessEventType
{
    StateUpdate,
    AttentionShift,
    PathwayActivation,
    PathwayInhibition,
    Synthesis,
    Emergence,
    Election,
    Optimization
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// MONADIC ELECTION SYSTEM - Dynamic model optimization via master model suggestions
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A monad representing a response candidate with its evaluation metadata.
/// Supports functional composition via Select, SelectMany, and Where.
/// </summary>
public sealed class ResponseCandidate<T>
{
    public T Value { get; }
    public string Source { get; }
    public double Score { get; private set; }
    public TimeSpan Latency { get; }
    public IReadOnlyDictionary<string, double> Metrics { get; }
    public bool IsValid { get; }

    private ResponseCandidate(T value, string source, double score, TimeSpan latency,
        IReadOnlyDictionary<string, double> metrics, bool isValid)
    {
        Value = value;
        Source = source;
        Score = score;
        Latency = latency;
        Metrics = metrics;
        IsValid = isValid;
    }

    public static ResponseCandidate<T> Create(T value, string source, TimeSpan latency) =>
        new(value, source, 0.0, latency, new Dictionary<string, double>(), true);

    public static ResponseCandidate<T> Invalid(string source) =>
        new(default!, source, 0.0, TimeSpan.Zero, new Dictionary<string, double>(), false);

    public ResponseCandidate<T> WithScore(double score) =>
        new(Value, Source, score, Latency, Metrics, IsValid);

    public ResponseCandidate<T> WithMetrics(IReadOnlyDictionary<string, double> metrics) =>
        new(Value, Source, Score, Latency, metrics, IsValid);

    // Functor: map over the value
    public ResponseCandidate<TResult> Select<TResult>(Func<T, TResult> selector) =>
        IsValid
            ? new ResponseCandidate<TResult>(selector(Value), Source, Score, Latency, Metrics, true)
            : ResponseCandidate<TResult>.Invalid(Source);

    // Monad: flatMap for composition
    public ResponseCandidate<TResult> SelectMany<TResult>(Func<T, ResponseCandidate<TResult>> selector) =>
        IsValid ? selector(Value) : ResponseCandidate<TResult>.Invalid(Source);

    // LINQ support
    public ResponseCandidate<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, ResponseCandidate<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        if (!IsValid) return ResponseCandidate<TResult>.Invalid(Source);
        var intermediate = selector(Value);
        if (!intermediate.IsValid) return ResponseCandidate<TResult>.Invalid(Source);
        return ResponseCandidate<TResult>.Create(
            resultSelector(Value, intermediate.Value), Source, Latency + intermediate.Latency);
    }

    // Filter
    public ResponseCandidate<T> Where(Func<T, bool> predicate) =>
        IsValid && predicate(Value) ? this : Invalid(Source);
}

/// <summary>
/// Election strategy algorithms for selecting the best response from candidates.
/// </summary>
public enum ElectionStrategy
{
    /// <summary>Simple majority: highest score wins.</summary>
    Majority,
    /// <summary>Weighted by source reliability.</summary>
    WeightedMajority,
    /// <summary>Borda count: rank-based scoring.</summary>
    BordaCount,
    /// <summary>Condorcet: pairwise comparison winner.</summary>
    Condorcet,
    /// <summary>Instant runoff: eliminate lowest, redistribute.</summary>
    InstantRunoff,
    /// <summary>Approval voting: count approvals above threshold.</summary>
    ApprovalVoting,
    /// <summary>Master model decides winner.</summary>
    MasterDecision
}

/// <summary>
/// Criteria for evaluating response quality.
/// </summary>
public sealed record EvaluationCriteria(
    double RelevanceWeight = 0.3,
    double CoherenceWeight = 0.25,
    double CompletenessWeight = 0.2,
    double LatencyWeight = 0.15,
    double CostWeight = 0.1)
{
    public static EvaluationCriteria Default => new();
    public static EvaluationCriteria QualityFocused => new(0.4, 0.3, 0.2, 0.05, 0.05);
    public static EvaluationCriteria SpeedFocused => new(0.2, 0.2, 0.1, 0.4, 0.1);
    public static EvaluationCriteria CostFocused => new(0.2, 0.2, 0.1, 0.1, 0.4);
}

/// <summary>
/// Result of an election with full transparency.
/// </summary>
public sealed record ElectionResult<T>(
    ResponseCandidate<T> Winner,
    IReadOnlyList<ResponseCandidate<T>> AllCandidates,
    ElectionStrategy Strategy,
    string Rationale,
    IReadOnlyDictionary<string, double> Votes);

/// <summary>
/// Master model election system for dynamic model optimization.
/// Uses a designated master model to evaluate and select the best response.
/// Clean, functional design with monadic composition.
/// </summary>
public sealed class MasterModelElection : IDisposable
{
    private readonly NeuralPathway? _masterPathway;
    private readonly Subject<ElectionEvent> _electionEvents = new();
    private readonly ConcurrentDictionary<string, ModelPerformance> _performanceHistory = new();
    private readonly EvaluationCriteria _criteria;
    private ElectionStrategy _strategy;

    /// <summary>Observable stream of election events.</summary>
    public IObservable<ElectionEvent> ElectionEvents => _electionEvents.AsObservable();

    /// <summary>Current election strategy.</summary>
    public ElectionStrategy Strategy
    {
        get => _strategy;
        set => _strategy = value;
    }

    /// <summary>Performance history for all models.</summary>
    public IReadOnlyDictionary<string, ModelPerformance> PerformanceHistory => _performanceHistory;

    public MasterModelElection(
        NeuralPathway? masterPathway = null,
        ElectionStrategy strategy = ElectionStrategy.WeightedMajority,
        EvaluationCriteria? criteria = null)
    {
        _masterPathway = masterPathway;
        _strategy = strategy;
        _criteria = criteria ?? EvaluationCriteria.Default;
    }

    /// <summary>
    /// Runs an election to select the best response from candidates.
    /// Pure function that returns the election result.
    /// </summary>
    public async Task<ElectionResult<ThinkingResponse>> RunElectionAsync(
        IReadOnlyList<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("No candidates for election");

        var validCandidates = candidates.Where(c => c.IsValid).ToList();
        if (validCandidates.Count == 0)
            throw new InvalidOperationException("No valid candidates for election");

        // Single candidate = automatic winner
        if (validCandidates.Count == 1)
        {
            var solo = validCandidates[0].WithScore(1.0);
            return new ElectionResult<ThinkingResponse>(
                solo, validCandidates, _strategy,
                "Single candidate - automatic selection",
                new Dictionary<string, double> { [solo.Source] = 1.0 });
        }

        // Score all candidates
        var scored = await ScoreCandidatesAsync(validCandidates, originalPrompt, ct);

        // Apply election strategy
        var (winner, votes, rationale) = _strategy switch
        {
            ElectionStrategy.Majority => ElectByMajority(scored),
            ElectionStrategy.WeightedMajority => ElectByWeightedMajority(scored),
            ElectionStrategy.BordaCount => ElectByBordaCount(scored),
            ElectionStrategy.Condorcet => await ElectByCondorcetAsync(scored, originalPrompt, ct),
            ElectionStrategy.InstantRunoff => ElectByInstantRunoff(scored),
            ElectionStrategy.ApprovalVoting => ElectByApproval(scored, threshold: 0.6),
            ElectionStrategy.MasterDecision => await ElectByMasterDecisionAsync(scored, originalPrompt, ct),
            _ => ElectByMajority(scored)
        };

        // Update performance history
        foreach (var candidate in scored)
        {
            UpdatePerformanceHistory(candidate, candidate == winner);
        }

        _electionEvents.OnNext(new ElectionEvent(
            ElectionEventType.ElectionComplete,
            $"Winner: {winner.Source} via {_strategy}",
            DateTime.UtcNow,
            winner.Source,
            votes));

        return new ElectionResult<ThinkingResponse>(winner, scored, _strategy, rationale, votes);
    }

    /// <summary>
    /// Scores candidates based on evaluation criteria.
    /// </summary>
    private async Task<List<ResponseCandidate<ThinkingResponse>>> ScoreCandidatesAsync(
        IReadOnlyList<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt,
        CancellationToken ct)
    {
        var scored = new List<ResponseCandidate<ThinkingResponse>>();

        foreach (var candidate in candidates)
        {
            var metrics = new Dictionary<string, double>();

            // Relevance: simple heuristic based on prompt term overlap
            metrics["relevance"] = CalculateRelevance(candidate.Value.Content, originalPrompt);

            // Coherence: sentence structure and flow
            metrics["coherence"] = CalculateCoherence(candidate.Value.Content);

            // Completeness: response length relative to prompt complexity
            metrics["completeness"] = CalculateCompleteness(candidate.Value.Content, originalPrompt);

            // Latency: normalized inverse (faster = higher score)
            metrics["latency"] = Math.Max(0, 1 - candidate.Latency.TotalSeconds / 30);

            // Cost: from performance history
            var perf = _performanceHistory.GetValueOrDefault(candidate.Source);
            metrics["cost"] = perf?.AverageCost > 0 ? Math.Max(0, 1 - perf.AverageCost / 0.01) : 0.5;

            // Compute weighted score
            double score =
                metrics["relevance"] * _criteria.RelevanceWeight +
                metrics["coherence"] * _criteria.CoherenceWeight +
                metrics["completeness"] * _criteria.CompletenessWeight +
                metrics["latency"] * _criteria.LatencyWeight +
                metrics["cost"] * _criteria.CostWeight;

            scored.Add(candidate.WithScore(score).WithMetrics(metrics));
        }

        // If master pathway available, get its evaluation
        if (_masterPathway?.IsHealthy == true)
        {
            scored = await EnhanceWithMasterEvaluationAsync(scored, originalPrompt, ct);
        }

        return scored;
    }

    /// <summary>
    /// Uses master model to enhance scoring with semantic evaluation.
    /// </summary>
    private async Task<List<ResponseCandidate<ThinkingResponse>>> EnhanceWithMasterEvaluationAsync(
        List<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt,
        CancellationToken ct)
    {
        try
        {
            var evaluationPrompt = BuildEvaluationPrompt(candidates, originalPrompt);

            var masterResponse = await _masterPathway!.CircuitBreaker.ExecuteAsync(async () =>
                await _masterPathway.Model.GenerateTextAsync(evaluationPrompt, ct));

            var masterScores = ParseMasterScores(masterResponse, candidates.Count);

            for (int i = 0; i < candidates.Count && i < masterScores.Count; i++)
            {
                // Blend heuristic score with master evaluation
                double blendedScore = candidates[i].Score * 0.4 + masterScores[i] * 0.6;
                candidates[i] = candidates[i].WithScore(blendedScore);
            }

            _electionEvents.OnNext(new ElectionEvent(
                ElectionEventType.MasterEvaluation,
                "Master model evaluation complete",
                DateTime.UtcNow));
        }
        catch
        {
            // Master evaluation failed, use heuristic scores only
            _electionEvents.OnNext(new ElectionEvent(
                ElectionEventType.MasterEvaluationFailed,
                "Falling back to heuristic scoring",
                DateTime.UtcNow));
        }

        return candidates;
    }

    private static string BuildEvaluationPrompt(
        IReadOnlyList<ResponseCandidate<ThinkingResponse>> candidates,
        string originalPrompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are evaluating multiple AI responses to select the best one.");
        sb.AppendLine("Rate each response 0.0-1.0 based on relevance, coherence, and completeness.");
        sb.AppendLine("Return ONLY a JSON array of scores, e.g.: [0.8, 0.6, 0.9]");
        sb.AppendLine();
        sb.AppendLine($"Original prompt: {originalPrompt.Substring(0, Math.Min(200, originalPrompt.Length))}...");
        sb.AppendLine();

        for (int i = 0; i < candidates.Count; i++)
        {
            var preview = candidates[i].Value.Content;
            if (preview.Length > 300) preview = preview.Substring(0, 300) + "...";
            sb.AppendLine($"Response {i + 1} ({candidates[i].Source}):");
            sb.AppendLine(preview);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static List<double> ParseMasterScores(string response, int expectedCount)
    {
        var scores = new List<double>();
        var matches = Regex.Matches(response, @"0?\.\d+|1\.0|0|1");
        foreach (Match m in matches)
        {
            if (double.TryParse(m.Value, out double score))
            {
                scores.Add(Math.Clamp(score, 0, 1));
                if (scores.Count >= expectedCount) break;
            }
        }

        // Pad with defaults if needed
        while (scores.Count < expectedCount)
            scores.Add(0.5);

        return scores;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ELECTION ALGORITHMS (Pure Functions)
    // ═══════════════════════════════════════════════════════════════════════════

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByMajority(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var winner = candidates.OrderByDescending(c => c.Score).First();
        var votes = candidates.ToDictionary(c => c.Source, c => c.Score);
        return (winner, votes, $"Highest score: {winner.Score:F3}");
    }

    private (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByWeightedMajority(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var weighted = candidates.Select(c =>
        {
            var perf = _performanceHistory.GetValueOrDefault(c.Source);
            double reliability = perf?.ReliabilityScore ?? 0.5;
            double weightedScore = c.Score * (0.5 + reliability * 0.5);
            return (Candidate: c, WeightedScore: weightedScore);
        }).ToList();

        var winner = weighted.OrderByDescending(w => w.WeightedScore).First();
        var votes = weighted.ToDictionary(w => w.Candidate.Source, w => w.WeightedScore);
        return (winner.Candidate, votes, $"Weighted score: {winner.WeightedScore:F3} (reliability factored)");
    }

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByBordaCount(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        int n = candidates.Count;
        var ranked = candidates.OrderByDescending(c => c.Score).ToList();
        var votes = new Dictionary<string, double>();

        for (int i = 0; i < ranked.Count; i++)
        {
            votes[ranked[i].Source] = n - i; // Borda points: n for 1st, n-1 for 2nd, etc.
        }

        var winner = ranked.First();
        return (winner, votes, $"Borda count winner with {n} points");
    }

    private async Task<(ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)>
        ElectByCondorcetAsync(List<ResponseCandidate<ThinkingResponse>> candidates, string prompt, CancellationToken ct)
    {
        // Simplified Condorcet: use scores for pairwise comparison
        var wins = candidates.ToDictionary(c => c.Source, _ => 0);

        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (candidates[i].Score > candidates[j].Score)
                    wins[candidates[i].Source]++;
                else if (candidates[j].Score > candidates[i].Score)
                    wins[candidates[j].Source]++;
            }
        }

        var winnerSource = wins.OrderByDescending(kv => kv.Value).First().Key;
        var winner = candidates.First(c => c.Source == winnerSource);
        var votes = wins.ToDictionary(kv => kv.Key, kv => (double)kv.Value);

        return (winner, votes, $"Condorcet winner with {wins[winnerSource]} pairwise wins");
    }

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByInstantRunoff(List<ResponseCandidate<ThinkingResponse>> candidates)
    {
        var remaining = candidates.ToList();
        var votes = candidates.ToDictionary(c => c.Source, c => c.Score);
        int rounds = 0;

        while (remaining.Count > 1)
        {
            rounds++;
            var lowest = remaining.OrderBy(c => c.Score).First();
            remaining.Remove(lowest);

            if (remaining.Count > 0)
            {
                // Redistribute (simplified: just remove)
                votes[lowest.Source] = -rounds; // Negative indicates elimination round
            }
        }

        var winner = remaining.First();
        return (winner, votes, $"IRV winner after {rounds} elimination rounds");
    }

    private static (ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)
        ElectByApproval(List<ResponseCandidate<ThinkingResponse>> candidates, double threshold)
    {
        var approved = candidates.Where(c => c.Score >= threshold).ToList();
        var votes = candidates.ToDictionary(c => c.Source, c => c.Score >= threshold ? 1.0 : 0.0);

        if (approved.Count == 0)
        {
            // Lower threshold if no approvals
            approved = candidates.OrderByDescending(c => c.Score).Take(1).ToList();
        }

        var winner = approved.OrderByDescending(c => c.Score).First();
        return (winner, votes, $"Approval voting: {approved.Count} candidates above threshold {threshold}");
    }

    private async Task<(ResponseCandidate<ThinkingResponse> Winner, Dictionary<string, double> Votes, string Rationale)>
        ElectByMasterDecisionAsync(List<ResponseCandidate<ThinkingResponse>> candidates, string prompt, CancellationToken ct)
    {
        if (_masterPathway?.IsHealthy != true)
        {
            return ElectByWeightedMajority(candidates);
        }

        try
        {
            var decisionPrompt = new StringBuilder()
                .AppendLine("Select the BEST response. Reply with ONLY the response number (1, 2, 3, etc.).")
                .AppendLine($"Original: {prompt.Substring(0, Math.Min(150, prompt.Length))}...")
                .AppendLine();

            for (int i = 0; i < candidates.Count; i++)
            {
                var preview = candidates[i].Value.Content;
                if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                decisionPrompt.AppendLine($"{i + 1}. [{candidates[i].Source}]: {preview}");
            }

            var decision = await _masterPathway.CircuitBreaker.ExecuteAsync(async () =>
                await _masterPathway.Model.GenerateTextAsync(decisionPrompt.ToString(), ct));

            // Parse the selected number
            var match = Regex.Match(decision, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int selected) &&
                selected >= 1 && selected <= candidates.Count)
            {
                var winner = candidates[selected - 1];
                var votes = candidates.ToDictionary(c => c.Source, c => c == winner ? 1.0 : 0.0);
                return (winner, votes, $"Master model selected response #{selected}");
            }
        }
        catch
        {
            // Fall back to weighted majority
        }

        return ElectByWeightedMajority(candidates);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PERFORMANCE TRACKING & OPTIMIZATION
    // ═══════════════════════════════════════════════════════════════════════════

    private void UpdatePerformanceHistory(ResponseCandidate<ThinkingResponse> candidate, bool wasWinner)
    {
        _performanceHistory.AddOrUpdate(
            candidate.Source,
            _ => new ModelPerformance
            {
                ModelName = candidate.Source,
                TotalElections = 1,
                Wins = wasWinner ? 1 : 0,
                AverageScore = candidate.Score,
                AverageLatency = candidate.Latency,
                LastUsed = DateTime.UtcNow
            },
            (_, perf) =>
            {
                perf.TotalElections++;
                if (wasWinner) perf.Wins++;
                perf.AverageScore = perf.AverageScore * 0.9 + candidate.Score * 0.1;
                perf.AverageLatency = TimeSpan.FromMilliseconds(
                    perf.AverageLatency.TotalMilliseconds * 0.9 + candidate.Latency.TotalMilliseconds * 0.1);
                perf.LastUsed = DateTime.UtcNow;
                return perf;
            });
    }

    /// <summary>
    /// Gets optimization suggestions based on performance history.
    /// </summary>
    public IReadOnlyList<OptimizationSuggestion> GetOptimizationSuggestions()
    {
        var suggestions = new List<OptimizationSuggestion>();

        foreach (var (source, perf) in _performanceHistory)
        {
            if (perf.WinRate < 0.2 && perf.TotalElections > 5)
            {
                suggestions.Add(new OptimizationSuggestion(
                    source,
                    OptimizationType.ConsiderRemoving,
                    $"Low win rate ({perf.WinRate:P0}) over {perf.TotalElections} elections",
                    Priority: 2));
            }

            if (perf.AverageLatency.TotalSeconds > 10 && perf.WinRate < 0.5)
            {
                suggestions.Add(new OptimizationSuggestion(
                    source,
                    OptimizationType.ReduceUsage,
                    $"High latency ({perf.AverageLatency.TotalSeconds:F1}s) with moderate win rate",
                    Priority: 1));
            }

            if (perf.WinRate > 0.7 && perf.TotalElections > 10)
            {
                suggestions.Add(new OptimizationSuggestion(
                    source,
                    OptimizationType.IncreasePriority,
                    $"High performer ({perf.WinRate:P0} win rate)",
                    Priority: 3));
            }
        }

        return suggestions.OrderByDescending(s => s.Priority).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HEURISTIC SCORING FUNCTIONS (Pure)
    // ═══════════════════════════════════════════════════════════════════════════

    private static double CalculateRelevance(string response, string prompt)
    {
        if (string.IsNullOrEmpty(response)) return 0;

        var promptWords = ExtractWords(prompt);
        var responseWords = ExtractWords(response);

        if (promptWords.Count == 0) return 0.5;

        int overlap = promptWords.Intersect(responseWords).Count();
        return Math.Min(1.0, (double)overlap / promptWords.Count);
    }

    private static double CalculateCoherence(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // Heuristics: sentence count, average length, punctuation
        var sentences = Regex.Split(text, @"[.!?]+").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (sentences.Count == 0) return 0.3;

        double avgLength = sentences.Average(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        // Ideal sentence length: 10-25 words
        double lengthScore = avgLength switch
        {
            < 5 => 0.5,
            < 10 => 0.7,
            <= 25 => 1.0,
            <= 40 => 0.8,
            _ => 0.6
        };

        // More sentences = more coherent structure (up to a point)
        double structureScore = Math.Min(1.0, sentences.Count / 5.0);

        return (lengthScore * 0.6 + structureScore * 0.4);
    }

    private static double CalculateCompleteness(string response, string prompt)
    {
        if (string.IsNullOrEmpty(response)) return 0;

        // Heuristic: response length relative to prompt complexity
        int promptComplexity = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        int responseLength = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Ideal: response 2-5x longer than prompt
        double ratio = (double)responseLength / Math.Max(1, promptComplexity);

        return ratio switch
        {
            < 0.5 => 0.3,
            < 1 => 0.5,
            < 2 => 0.7,
            <= 5 => 1.0,
            <= 10 => 0.9,
            _ => 0.7
        };
    }

    private static HashSet<string> ExtractWords(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), @"\b[a-z]{3,}\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToHashSet();
    }

    public void Dispose()
    {
        _electionEvents.OnCompleted();
        _electionEvents.Dispose();
    }
}

/// <summary>
/// Performance tracking for a model in the election system.
/// </summary>
public sealed class ModelPerformance
{
    public string ModelName { get; init; } = "";
    public int TotalElections { get; set; }
    public int Wins { get; set; }
    public double AverageScore { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public double AverageCost { get; set; }
    public DateTime LastUsed { get; set; }

    public double WinRate => TotalElections > 0 ? (double)Wins / TotalElections : 0;
    public double ReliabilityScore => WinRate * 0.6 + (1 - Math.Min(1, AverageLatency.TotalSeconds / 30)) * 0.4;
}

/// <summary>
/// An optimization suggestion from the election system.
/// </summary>
public sealed record OptimizationSuggestion(
    string ModelName,
    OptimizationType Type,
    string Reason,
    int Priority);

/// <summary>
/// Types of optimization suggestions.
/// </summary>
public enum OptimizationType
{
    IncreasePriority,
    ReduceUsage,
    ConsiderRemoving,
    AdjustParameters,
    AddFallback
}

/// <summary>
/// Election event for observability.
/// </summary>
public sealed record ElectionEvent(
    ElectionEventType Type,
    string Message,
    DateTime Timestamp,
    string? Winner = null,
    IReadOnlyDictionary<string, double>? Votes = null);

/// <summary>
/// Types of election events.
/// </summary>
public enum ElectionEventType
{
    ElectionStarted,
    CandidateEvaluated,
    MasterEvaluation,
    MasterEvaluationFailed,
    ElectionComplete,
    OptimizationSuggested
}

/// <summary>
/// Factory for creating pre-configured CollectiveMind instances.
/// </summary>
public static class CollectiveMindFactory
{
    /// <summary>
    /// Creates a balanced collective with multiple diverse providers.
    /// </summary>
    public static CollectiveMind CreateBalanced(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        // Try to add providers based on available API keys
        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-sonnet-4-20250514", settings);
        TryAddProvider(mind, "OpenAI", ChatEndpointType.OpenAI, "gpt-4o", settings);
        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-70b-versatile", settings);

        // Always try local Ollama
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, "llama3.2", settings: settings);

        mind.ThinkingMode = CollectiveThinkingMode.Adaptive;
        return mind;
    }

    /// <summary>
    /// Creates a speed-optimized collective using fast inference providers.
    /// </summary>
    public static CollectiveMind CreateFast(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-70b-versatile", settings);
        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Fireworks", ChatEndpointType.Fireworks, "llama-v3-70b-instruct", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Racing;
        return mind;
    }

    /// <summary>
    /// Creates a quality-optimized collective using premium providers.
    /// </summary>
    public static CollectiveMind CreatePremium(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-opus-4-5-20251101", settings);
        TryAddProvider(mind, "OpenAI", ChatEndpointType.OpenAI, "gpt-4o", settings);
        TryAddProvider(mind, "Google", ChatEndpointType.Google, "gemini-1.5-pro", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Ensemble;
        return mind;
    }

    /// <summary>
    /// Creates a cost-optimized collective using budget-friendly providers.
    /// </summary>
    public static CollectiveMind CreateBudget(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-8b-instant", settings);
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, "llama3.2", settings: settings);

        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a local-only collective using Ollama.
    /// Provides resilience features (circuit breaker, health tracking) for a single local provider.
    /// </summary>
    public static CollectiveMind CreateLocal(string model = "llama3.2", string endpoint = "http://localhost:11434", ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, model, endpoint, settings: settings);
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a single-provider collective mind.
    /// Useful for getting resilience features with just one provider.
    /// </summary>
    public static CollectiveMind CreateSingle(
        string name,
        ChatEndpointType endpointType,
        string model,
        string? endpoint = null,
        string? apiKey = null,
        ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();
        mind.AddPathway(name, endpointType, model, endpoint, apiKey, settings);
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a collective mind from the current ChatConfig settings.
    /// Uses the configured endpoint type and adds it as the primary pathway.
    /// </summary>
    public static CollectiveMind CreateFromConfig(
        string model,
        string? endpoint = null,
        string? apiKey = null,
        string? endpointType = null,
        ChatRuntimeSettings? settings = null)
    {
        var (resolvedEndpoint, resolvedApiKey, resolvedType) = ChatConfig.ResolveWithOverrides(endpoint, apiKey, endpointType);

        var mind = new CollectiveMind();
        string providerName = LlmCostTracker.GetProvider(model);
        if (providerName == "Unknown") providerName = resolvedType.ToString();

        mind.AddPathway(providerName, resolvedType, model, resolvedEndpoint, resolvedApiKey, settings);
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a decomposition-enabled collective that splits requests into sub-goals.
    /// Routes sub-goals to optimal pathways (local/cloud) based on complexity.
    /// </summary>
    public static CollectiveMind CreateDecomposed(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        // Add mix of local and cloud providers for routing flexibility
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, "llama3.2", settings: settings);
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-70b-versatile", settings);
        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-sonnet-4-20250514", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.Default;
        return mind;
    }

    /// <summary>
    /// Creates a local-first decomposition collective.
    /// Prefers local Ollama for simple tasks, escalates to cloud only when needed.
    /// </summary>
    public static CollectiveMind CreateLocalFirstDecomposed(
        string localModel = "llama3.2",
        string localEndpoint = "http://localhost:11434",
        ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        // Local pathway as primary
        mind.AddPathway("Ollama-Local", ChatEndpointType.OllamaLocal, localModel, localEndpoint, settings: settings);

        // Lightweight cloud for moderate tasks
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-8b-instant", settings);

        // Premium cloud for complex tasks (only when needed)
        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-sonnet-4-20250514", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.LocalFirst;
        return mind;
    }

    /// <summary>
    /// Creates a hybrid collective with explicit tier assignments.
    /// Allows fine-grained control over which models handle which tasks.
    /// </summary>
    public static CollectiveMind CreateHybrid(
        (string Name, ChatEndpointType Type, string Model, PathwayTier Tier)[] pathways,
        ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        foreach (var (name, type, model, tier) in pathways)
        {
            mind.AddPathway(name, type, model, settings: settings);
            // Note: Tier is inferred automatically, but we could add explicit tier setting
        }

        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        return mind;
    }

    private static void TryAddProvider(CollectiveMind mind, string name, ChatEndpointType type, string model, ChatRuntimeSettings? settings)
    {
        try
        {
            var (endpoint, apiKey, _) = ChatConfig.ResolveWithOverrides(null, null, type.ToString());
            if (!string.IsNullOrWhiteSpace(apiKey) || type == ChatEndpointType.OllamaLocal)
            {
                mind.AddPathway(name, type, model, endpoint, apiKey, settings);
            }
        }
        catch
        {
            // Provider not available, skip silently
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// DSL ORCHESTRATION LAYER - Monadic pipeline for collective mind operations
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// DSL operation that can be composed into pipelines.
/// Monad for chaining collective mind operations with Rx streaming.
/// </summary>
public sealed class MindOperation<T>
{
    private readonly Func<CollectiveMind, CancellationToken, Task<T>> _execute;
    private readonly Func<CollectiveMind, CancellationToken, IObservable<(bool IsThinking, string Chunk)>>? _stream;

    private MindOperation(
        Func<CollectiveMind, CancellationToken, Task<T>> execute,
        Func<CollectiveMind, CancellationToken, IObservable<(bool IsThinking, string Chunk)>>? stream = null)
    {
        _execute = execute;
        _stream = stream;
    }

    /// <summary>Creates a pure value operation.</summary>
    public static MindOperation<T> Return(T value) =>
        new((_, _) => Task.FromResult(value));

    /// <summary>Creates an async operation.</summary>
    public static MindOperation<T> FromAsync(Func<CollectiveMind, CancellationToken, Task<T>> execute) =>
        new(execute);

    /// <summary>Creates a streaming operation.</summary>
    public static MindOperation<T> FromStream(
        Func<CollectiveMind, CancellationToken, IObservable<(bool IsThinking, string Chunk)>> stream,
        Func<CollectiveMind, CancellationToken, Task<T>> finalResult) =>
        new(finalResult, stream);

    /// <summary>Executes the operation against a collective mind.</summary>
    public Task<T> ExecuteAsync(CollectiveMind mind, CancellationToken ct = default) =>
        _execute(mind, ct);

    /// <summary>Gets the streaming observable if available.</summary>
    public IObservable<(bool IsThinking, string Chunk)>? GetStream(CollectiveMind mind, CancellationToken ct = default) =>
        _stream?.Invoke(mind, ct);

    /// <summary>Whether this operation supports streaming.</summary>
    public bool SupportsStreaming => _stream != null;

    // Functor: map
    public MindOperation<TResult> Select<TResult>(Func<T, TResult> selector) =>
        new(async (mind, ct) => selector(await _execute(mind, ct)), _stream);

    // Monad: flatMap
    public MindOperation<TResult> SelectMany<TResult>(Func<T, MindOperation<TResult>> selector) =>
        new(async (mind, ct) =>
        {
            T result = await _execute(mind, ct);
            return await selector(result).ExecuteAsync(mind, ct);
        });

    // LINQ support
    public MindOperation<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, MindOperation<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector) =>
        new(async (mind, ct) =>
        {
            T first = await _execute(mind, ct);
            TIntermediate second = await selector(first).ExecuteAsync(mind, ct);
            return resultSelector(first, second);
        });

    // Combine with another operation
    public MindOperation<(T, TOther)> Zip<TOther>(MindOperation<TOther> other) =>
        new(async (mind, ct) =>
        {
            var task1 = _execute(mind, ct);
            var task2 = other.ExecuteAsync(mind, ct);
            await Task.WhenAll(task1, task2);
            return (task1.Result, task2.Result);
        });
}

/// <summary>
/// DSL for building collective mind pipelines.
/// Provides a clean, declarative API for orchestrating AI operations.
/// </summary>
public static class MindDsl
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CORE OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an operation that generates text with thinking/streaming support.
    /// </summary>
    public static MindOperation<ThinkingResponse> Think(string prompt) =>
        MindOperation<ThinkingResponse>.FromStream(
            (mind, ct) => mind.StreamWithThinkingAsync(prompt, ct),
            (mind, ct) => mind.GenerateWithThinkingAsync(prompt, ct));

    /// <summary>
    /// Creates an operation that generates plain text.
    /// </summary>
    public static MindOperation<string> Generate(string prompt) =>
        MindOperation<string>.FromAsync((mind, ct) => mind.GenerateTextAsync(prompt, ct));

    /// <summary>
    /// Creates a racing operation that queries all pathways simultaneously.
    /// </summary>
    public static MindOperation<ThinkingResponse> Race(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Racing;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    /// <summary>
    /// Creates an ensemble operation that gathers multiple perspectives and elects the best.
    /// </summary>
    public static MindOperation<ThinkingResponse> Ensemble(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Ensemble;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    /// <summary>
    /// Creates a sequential operation with automatic failover.
    /// </summary>
    public static MindOperation<ThinkingResponse> Sequential(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Sequential;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    // ═══════════════════════════════════════════════════════════════════════════
    // CONFIGURATION OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the master pathway for orchestration.
    /// </summary>
    public static MindOperation<VoidResult> SetMaster(string pathwayName) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.SetMaster(pathwayName);
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Sets the election strategy.
    /// </summary>
    public static MindOperation<VoidResult> UseElection(ElectionStrategy strategy) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ElectionStrategy = strategy;
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Sets the thinking mode.
    /// </summary>
    public static MindOperation<VoidResult> UseMode(CollectiveThinkingMode mode) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ThinkingMode = mode;
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Adds a pathway to the collective.
    /// </summary>
    public static MindOperation<VoidResult> AddPathway(
        string name,
        ChatEndpointType type,
        string? model = null,
        string? endpoint = null,
        string? apiKey = null) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.AddPathway(name, type, model, endpoint, apiKey);
            return Task.FromResult(VoidResult.Value);
        });

    // ═══════════════════════════════════════════════════════════════════════════
    // DECOMPOSITION OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a decomposed operation that splits the request into sub-goals.
    /// Routes sub-goals to optimal pathways (local/cloud) based on complexity.
    /// </summary>
    public static MindOperation<ThinkingResponse> Decompose(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    /// <summary>
    /// Creates a decomposed operation with custom configuration.
    /// </summary>
    public static MindOperation<ThinkingResponse> Decompose(string prompt, DecompositionConfig config) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var originalMode = mind.ThinkingMode;
            var originalConfig = mind.DecompositionConfig;
            mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
            mind.DecompositionConfig = config;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally
            {
                mind.ThinkingMode = originalMode;
                mind.DecompositionConfig = originalConfig;
            }
        });

    /// <summary>
    /// Creates a local-first decomposed operation.
    /// Prefers local Ollama models for simple tasks, cloud for complex.
    /// </summary>
    public static MindOperation<ThinkingResponse> DecomposeLocalFirst(string prompt) =>
        Decompose(prompt, DecompositionConfig.LocalFirst);

    /// <summary>
    /// Creates a quality-first decomposed operation.
    /// Uses premium cloud models for all tasks.
    /// </summary>
    public static MindOperation<ThinkingResponse> DecomposeQualityFirst(string prompt) =>
        Decompose(prompt, DecompositionConfig.QualityFirst);

    /// <summary>
    /// Sets the decomposition configuration.
    /// </summary>
    public static MindOperation<VoidResult> UseDecomposition(DecompositionConfig config) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
            mind.DecompositionConfig = config;
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Configures a pathway's tier and specializations.
    /// </summary>
    public static MindOperation<VoidResult> ConfigurePathway(
        string pathwayName,
        PathwayTier tier,
        params SubGoalType[] specializations) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ConfigurePathway(pathwayName, tier, specializations);
            return Task.FromResult(VoidResult.Value);
        });

    // ═══════════════════════════════════════════════════════════════════════════
    // QUERY OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets optimization suggestions from the election system.
    /// </summary>
    public static MindOperation<IReadOnlyList<OptimizationSuggestion>> GetOptimizations() =>
        MindOperation<IReadOnlyList<OptimizationSuggestion>>.FromAsync((mind, _) =>
            Task.FromResult(mind.GetOptimizationSuggestions()));

    /// <summary>
    /// Gets the current consciousness status.
    /// </summary>
    public static MindOperation<string> GetStatus() =>
        MindOperation<string>.FromAsync((mind, _) =>
            Task.FromResult(mind.GetConsciousnessStatus()));

    /// <summary>
    /// Gets all healthy pathways.
    /// </summary>
    public static MindOperation<IReadOnlyList<NeuralPathway>> GetHealthyPathways() =>
        MindOperation<IReadOnlyList<NeuralPathway>>.FromAsync((mind, _) =>
            Task.FromResult((IReadOnlyList<NeuralPathway>)mind.Pathways.Where(p => p.IsHealthy).ToList()));

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBINATORS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes operations in sequence.
    /// </summary>
    public static MindOperation<IReadOnlyList<T>> Sequence<T>(params MindOperation<T>[] operations) =>
        MindOperation<IReadOnlyList<T>>.FromAsync(async (mind, ct) =>
        {
            var results = new List<T>();
            foreach (var op in operations)
            {
                results.Add(await op.ExecuteAsync(mind, ct));
            }
            return results;
        });

    /// <summary>
    /// Executes operations in parallel and collects results.
    /// </summary>
    public static MindOperation<IReadOnlyList<T>> Parallel<T>(params MindOperation<T>[] operations) =>
        MindOperation<IReadOnlyList<T>>.FromAsync(async (mind, ct) =>
        {
            var tasks = operations.Select(op => op.ExecuteAsync(mind, ct));
            return await Task.WhenAll(tasks);
        });

    /// <summary>
    /// Executes an operation with a fallback on failure.
    /// </summary>
    public static MindOperation<T> WithFallback<T>(MindOperation<T> primary, MindOperation<T> fallback) =>
        MindOperation<T>.FromAsync(async (mind, ct) =>
        {
            try { return await primary.ExecuteAsync(mind, ct); }
            catch { return await fallback.ExecuteAsync(mind, ct); }
        });

    /// <summary>
    /// Retries an operation with exponential backoff.
    /// </summary>
    public static MindOperation<T> WithRetry<T>(MindOperation<T> operation, int maxRetries = 3) =>
        MindOperation<T>.FromAsync(async (mind, ct) =>
        {
            int attempt = 0;
            while (true)
            {
                try { return await operation.ExecuteAsync(mind, ct); }
                catch when (++attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)), ct);
                }
            }
        });

    /// <summary>
    /// Transforms the result of an operation.
    /// </summary>
    public static MindOperation<TResult> Map<T, TResult>(MindOperation<T> operation, Func<T, TResult> transform) =>
        operation.Select(transform);

    /// <summary>
    /// Chains operations together.
    /// </summary>
    public static MindOperation<TResult> Bind<T, TResult>(
        MindOperation<T> operation,
        Func<T, MindOperation<TResult>> next) =>
        operation.SelectMany(next);

    /// <summary>
    /// Creates a pipeline that applies a prompt template to a result.
    /// </summary>
    public static MindOperation<ThinkingResponse> ThenThink(
        MindOperation<string> operation,
        Func<string, string> promptTemplate) =>
        operation.SelectMany(result => Think(promptTemplate(result)));
}

/// <summary>
/// Void result type for operations with no meaningful return value.
/// Named VoidResult to avoid conflict with MeTTa.Unit.
/// </summary>
public readonly struct VoidResult
{
    public static readonly VoidResult Value = default;
}

/// <summary>
/// Streaming pipeline builder for Rx-based operations.
/// </summary>
public sealed class StreamingPipeline
{
    private readonly CollectiveMind _mind;
    private readonly List<Func<IObservable<(bool IsThinking, string Chunk)>, IObservable<(bool IsThinking, string Chunk)>>> _transformations = new();

    public StreamingPipeline(CollectiveMind mind)
    {
        _mind = mind;
    }

    /// <summary>
    /// Starts streaming from a prompt.
    /// </summary>
    public StreamingPipeline From(string prompt)
    {
        return this;
    }

    /// <summary>
    /// Filters thinking chunks only.
    /// </summary>
    public StreamingPipeline OnlyThinking()
    {
        _transformations.Add(stream => stream.Where(t => t.IsThinking));
        return this;
    }

    /// <summary>
    /// Filters content chunks only.
    /// </summary>
    public StreamingPipeline OnlyContent()
    {
        _transformations.Add(stream => stream.Where(t => !t.IsThinking));
        return this;
    }

    /// <summary>
    /// Transforms chunks.
    /// </summary>
    public StreamingPipeline Transform(Func<string, string> transform)
    {
        _transformations.Add(stream =>
            stream.Select(t => (t.IsThinking, transform(t.Chunk))));
        return this;
    }

    /// <summary>
    /// Buffers chunks by time.
    /// </summary>
    public StreamingPipeline Buffer(TimeSpan window)
    {
        _transformations.Add(stream =>
            stream.Buffer(window)
                .Where(b => b.Count > 0)
                .Select(b => (b.Last().IsThinking, string.Concat(b.Select(c => c.Chunk)))));
        return this;
    }

    /// <summary>
    /// Throttles the stream.
    /// </summary>
    public StreamingPipeline Throttle(TimeSpan interval)
    {
        _transformations.Add(stream => stream.Throttle(interval));
        return this;
    }

    /// <summary>
    /// Executes the pipeline and returns the observable.
    /// </summary>
    public IObservable<(bool IsThinking, string Chunk)> Execute(string prompt, CancellationToken ct = default)
    {
        IObservable<(bool IsThinking, string Chunk)> stream = _mind.StreamWithThinkingAsync(prompt, ct);

        foreach (var transform in _transformations)
        {
            stream = transform(stream);
        }

        return stream;
    }

    /// <summary>
    /// Executes and collects the final result.
    /// </summary>
    public async Task<ThinkingResponse> ExecuteAndCollectAsync(string prompt, CancellationToken ct = default)
    {
        var thinkingBuilder = new StringBuilder();
        var contentBuilder = new StringBuilder();

        await Execute(prompt, ct).ForEachAsync(chunk =>
        {
            if (chunk.IsThinking)
                thinkingBuilder.Append(chunk.Chunk);
            else
                contentBuilder.Append(chunk.Chunk);
        }, ct);

        return new ThinkingResponse(
            thinkingBuilder.Length > 0 ? thinkingBuilder.ToString() : null,
            contentBuilder.ToString());
    }
}

/// <summary>
/// Extensions for CollectiveMind to enable DSL usage.
/// </summary>
public static class CollectiveMindDslExtensions
{
    /// <summary>
    /// Executes a DSL operation on the collective mind.
    /// </summary>
    public static Task<T> RunAsync<T>(this CollectiveMind mind, MindOperation<T> operation, CancellationToken ct = default) =>
        operation.ExecuteAsync(mind, ct);

    /// <summary>
    /// Creates a streaming pipeline.
    /// </summary>
    public static StreamingPipeline Stream(this CollectiveMind mind) =>
        new(mind);

    /// <summary>
    /// Executes a DSL pipeline: config -> operation -> result.
    /// </summary>
    public static async Task<TResult> PipelineAsync<TResult>(
        this CollectiveMind mind,
        MindOperation<VoidResult> config,
        MindOperation<TResult> operation,
        CancellationToken ct = default)
    {
        await config.ExecuteAsync(mind, ct);
        return await operation.ExecuteAsync(mind, ct);
    }

    /// <summary>
    /// Fluent: Sets master and returns the mind.
    /// </summary>
    public static CollectiveMind WithMaster(this CollectiveMind mind, string pathwayName)
    {
        mind.SetMaster(pathwayName);
        return mind;
    }

    /// <summary>
    /// Fluent: Sets election strategy and returns the mind.
    /// </summary>
    public static CollectiveMind WithElection(this CollectiveMind mind, ElectionStrategy strategy)
    {
        mind.ElectionStrategy = strategy;
        return mind;
    }

    /// <summary>
    /// Fluent: Sets thinking mode and returns the mind.
    /// </summary>
    public static CollectiveMind WithMode(this CollectiveMind mind, CollectiveThinkingMode mode)
    {
        mind.ThinkingMode = mode;
        return mind;
    }

    /// <summary>
    /// Fluent: Enables decomposed mode for goal splitting.
    /// </summary>
    public static CollectiveMind WithDecomposition(this CollectiveMind mind, DecompositionConfig? config = null)
    {
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        if (config != null)
            mind.DecompositionConfig = config;
        return mind;
    }

    /// <summary>
    /// Fluent: Configures decomposition to prefer local models.
    /// </summary>
    public static CollectiveMind WithLocalFirst(this CollectiveMind mind)
    {
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.LocalFirst;
        return mind;
    }

    /// <summary>
    /// Fluent: Configures decomposition to prefer quality (cloud premium).
    /// </summary>
    public static CollectiveMind WithQualityFirst(this CollectiveMind mind)
    {
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.QualityFirst;
        return mind;
    }

    /// <summary>
    /// Fluent: Configures a pathway's tier and specializations.
    /// </summary>
    public static CollectiveMind WithPathwayConfig(
        this CollectiveMind mind,
        string pathwayName,
        PathwayTier tier,
        params SubGoalType[] specializations)
    {
        mind.ConfigurePathway(pathwayName, tier, specializations);
        return mind;
    }

    /// <summary>
    /// Creates a DSL expression from a string (simple prompt shorthand).
    /// </summary>
    public static MindOperation<ThinkingResponse> Ask(this CollectiveMind _, string prompt) =>
        MindDsl.Think(prompt);
}
