// <copyright file="InnerDialogEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;
using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;

#region Background Operation Framework

/// <summary>
/// Context for background operations, containing conversation state and available resources.
/// </summary>
public sealed record BackgroundOperationContext(
    string? CurrentTopic,
    string? LastUserMessage,
    PersonalityProfile? Profile,
    SelfAwareness? SelfAwareness,
    List<string> RecentTopics,
    List<string> AvailableTools,
    List<string> AvailableSkills,
    Dictionary<string, object> ConversationMetadata)
{
    /// <summary>
    /// Extracts key concepts from the current topic and recent messages.
    /// </summary>
    public List<string> ExtractKeywords()
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // From current topic
        if (!string.IsNullOrWhiteSpace(CurrentTopic))
        {
            foreach (var word in CurrentTopic.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 3) keywords.Add(word.ToLowerInvariant());
            }
        }

        // From last message
        if (!string.IsNullOrWhiteSpace(LastUserMessage))
        {
            foreach (var word in LastUserMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 3) keywords.Add(word.ToLowerInvariant());
            }
        }

        // From recent topics
        foreach (var topic in RecentTopics.TakeLast(3))
        {
            foreach (var word in topic.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 3) keywords.Add(word.ToLowerInvariant());
            }
        }

        return keywords.ToList();
    }
}

/// <summary>
/// Result of a background operation executed by a thought.
/// </summary>
public sealed record BackgroundOperationResult(
    string OperationType,
    string OperationName,
    bool Success,
    string? ResultSummary,
    object? Data,
    TimeSpan Duration,
    InnerThoughtType TriggeringThoughtType);

/// <summary>
/// Interface for executing background operations triggered by thoughts.
/// </summary>
public interface IBackgroundOperationExecutor
{
    /// <summary>Gets the name of this executor.</summary>
    string Name { get; }

    /// <summary>Gets the operation types this executor can handle.</summary>
    IReadOnlyList<string> SupportedOperations { get; }

    /// <summary>Determines if this executor should run for the given thought type.</summary>
    bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context);

    /// <summary>Executes the background operation.</summary>
    Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Engine that executes useful background operations based on autonomous thoughts,
/// synergizing with the active conversation to prepare relevant information.
/// </summary>
public sealed class ThoughtDrivenOperationEngine
{
    private readonly List<IBackgroundOperationExecutor> _executors = [];
    private readonly ConcurrentQueue<BackgroundOperationResult> _completedOperations = new();
    private readonly ConcurrentDictionary<string, object> _prefetchedData = new();
    private readonly object _contextLock = new();
    private BackgroundOperationContext? _currentContext;

    private const int MaxCompletedOperations = 50;
    private const int MaxPrefetchedItems = 100;

    /// <summary>
    /// Registers a background operation executor.
    /// </summary>
    public void RegisterExecutor(IBackgroundOperationExecutor executor)
    {
        _executors.Add(executor);
    }

    /// <summary>
    /// Updates the current conversation context for background operations.
    /// </summary>
    public void UpdateContext(BackgroundOperationContext context)
    {
        lock (_contextLock)
        {
            _currentContext = context;
        }
    }

    /// <summary>
    /// Gets the current context.
    /// </summary>
    public BackgroundOperationContext? GetCurrentContext()
    {
        lock (_contextLock)
        {
            return _currentContext;
        }
    }

    /// <summary>
    /// Processes a thought and executes relevant background operations.
    /// </summary>
    public async Task<List<BackgroundOperationResult>> ProcessThoughtAsync(
        InnerThought thought,
        CancellationToken ct = default)
    {
        var results = new List<BackgroundOperationResult>();
        var context = GetCurrentContext();

        if (context == null) return results;

        foreach (var executor in _executors)
        {
            if (ct.IsCancellationRequested) break;

            if (executor.ShouldExecute(thought.Type, context))
            {
                try
                {
                    var result = await executor.ExecuteAsync(thought, context, ct);
                    if (result != null)
                    {
                        results.Add(result);
                        _completedOperations.Enqueue(result);

                        // Trim if needed
                        while (_completedOperations.Count > MaxCompletedOperations)
                        {
                            _completedOperations.TryDequeue(out _);
                        }

                        // Store prefetched data
                        if (result.Data != null && result.Success)
                        {
                            var key = $"{result.OperationType}:{result.OperationName}";
                            _prefetchedData[key] = result.Data;

                            while (_prefetchedData.Count > MaxPrefetchedItems)
                            {
                                var oldest = _prefetchedData.Keys.FirstOrDefault();
                                if (oldest != null) _prefetchedData.TryRemove(oldest, out _);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new BackgroundOperationResult(
                        "error", executor.Name, false, ex.Message, null,
                        TimeSpan.Zero, thought.Type));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Gets completed background operations.
    /// </summary>
    public List<BackgroundOperationResult> GetCompletedOperations(int limit = 10)
    {
        return _completedOperations.TakeLast(limit).ToList();
    }

    /// <summary>
    /// Gets prefetched data by key pattern.
    /// </summary>
    public Dictionary<string, object> GetPrefetchedData(string? keyPattern = null)
    {
        if (string.IsNullOrEmpty(keyPattern))
        {
            return new Dictionary<string, object>(_prefetchedData);
        }

        return _prefetchedData
            .Where(kv => kv.Key.Contains(keyPattern, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Clears prefetched data older than the specified age.
    /// </summary>
    public void ClearStaleData(TimeSpan maxAge)
    {
        // In a real implementation, we'd track timestamps per entry
        // For now, just clear oldest entries if over limit
        while (_prefetchedData.Count > MaxPrefetchedItems / 2)
        {
            var oldest = _prefetchedData.Keys.FirstOrDefault();
            if (oldest != null) _prefetchedData.TryRemove(oldest, out _);
        }
    }
}

/// <summary>
/// Executor that prefetches related topics and context based on curiosity thoughts.
/// Anticipates user needs by exploring related concepts.
/// </summary>
public sealed class CuriosityPrefetchExecutor : IBackgroundOperationExecutor
{
    private readonly Func<string, CancellationToken, Task<List<string>>>? _topicExplorer;
    private readonly HashSet<string> _exploredTopics = [];

    public string Name => "CuriosityPrefetch";
    public IReadOnlyList<string> SupportedOperations => ["topic_exploration", "related_concepts"];

    public CuriosityPrefetchExecutor(Func<string, CancellationToken, Task<List<string>>>? topicExplorer = null)
    {
        _topicExplorer = topicExplorer;
    }

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return thoughtType == InnerThoughtType.Curiosity &&
               !string.IsNullOrEmpty(context.CurrentTopic) &&
               !_exploredTopics.Contains(context.CurrentTopic);
    }

    public async Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.CurrentTopic)) return null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _exploredTopics.Add(context.CurrentTopic);

        // Keep explored topics bounded
        while (_exploredTopics.Count > 100)
        {
            _exploredTopics.Remove(_exploredTopics.First());
        }

        List<string> relatedTopics;
        if (_topicExplorer != null)
        {
            relatedTopics = await _topicExplorer(context.CurrentTopic, ct);
        }
        else
        {
            // Default: generate related concepts based on context
            relatedTopics = GenerateRelatedConcepts(context.CurrentTopic, context);
        }

        sw.Stop();

        return new BackgroundOperationResult(
            "topic_exploration",
            context.CurrentTopic,
            true,
            $"Explored {relatedTopics.Count} related concepts",
            new Dictionary<string, object>
            {
                ["topic"] = context.CurrentTopic,
                ["related"] = relatedTopics,
                ["thought_content"] = thought.Content
            },
            sw.Elapsed,
            thought.Type);
    }

    private static List<string> GenerateRelatedConcepts(string topic, BackgroundOperationContext context)
    {
        // Simple related concept generation based on profile and topic
        var concepts = new List<string>();

        if (context.Profile?.CuriosityDrivers != null)
        {
            concepts.AddRange(
                context.Profile.CuriosityDrivers
                    .Where(c => c.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                               topic.Contains(c.Topic, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(c => new[] { c.Topic, $"{topic} and {c.Topic}" }));
        }

        if (context.SelfAwareness?.Capabilities != null)
        {
            concepts.AddRange(
                context.SelfAwareness.Capabilities
                    .Select(c => $"{topic} using {c}")
                    .Take(3));
        }

        return concepts.Distinct().ToList();
    }
}

/// <summary>
/// Executor that suggests tools and skills based on anticipatory thoughts.
/// Prepares action recommendations proactively.
/// </summary>
public sealed class AnticipatoryActionExecutor : IBackgroundOperationExecutor
{
    private readonly Func<string, List<string>, CancellationToken, Task<(string Action, string Reason)>>? _suggester;

    public string Name => "AnticipatoryAction";
    public IReadOnlyList<string> SupportedOperations => ["action_suggestion", "tool_recommendation"];

    public AnticipatoryActionExecutor(
        Func<string, List<string>, CancellationToken, Task<(string Action, string Reason)>>? suggester = null)
    {
        _suggester = suggester;
    }

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return (thoughtType == InnerThoughtType.Anticipatory || thoughtType == InnerThoughtType.Intention) &&
               !string.IsNullOrEmpty(context.LastUserMessage) &&
               (context.AvailableTools.Count > 0 || context.AvailableSkills.Count > 0);
    }

    public async Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.LastUserMessage)) return null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var allActions = context.AvailableTools.Concat(context.AvailableSkills).ToList();

        string suggestedAction;
        string reason;

        if (_suggester != null)
        {
            (suggestedAction, reason) = await _suggester(context.LastUserMessage, allActions, ct);
        }
        else
        {
            // Default: simple keyword matching
            (suggestedAction, reason) = SuggestAction(context.LastUserMessage, allActions);
        }

        sw.Stop();

        if (string.IsNullOrEmpty(suggestedAction))
        {
            return null;
        }

        return new BackgroundOperationResult(
            "action_suggestion",
            suggestedAction,
            true,
            reason,
            new Dictionary<string, object>
            {
                ["suggested_action"] = suggestedAction,
                ["reason"] = reason,
                ["available_actions"] = allActions,
                ["context"] = context.LastUserMessage
            },
            sw.Elapsed,
            thought.Type);
    }

    private static (string Action, string Reason) SuggestAction(string message, List<string> actions)
    {
        var messageLower = message.ToLowerInvariant();

        foreach (var action in actions)
        {
            var actionLower = action.ToLowerInvariant();
            if (messageLower.Contains(actionLower) || actionLower.Contains(messageLower.Split(' ').FirstOrDefault() ?? ""))
            {
                return (action, $"Direct match with user intent regarding '{action}'");
            }
        }

        // Pattern matching for common intents
        var patterns = new Dictionary<string[], string[]>
        {
            { ["search", "find", "look"], ["search", "query", "lookup", "find"] },
            { ["create", "make", "generate"], ["create", "generate", "build", "make"] },
            { ["analyze", "examine", "check"], ["analyze", "inspect", "evaluate", "check"] },
            { ["help", "assist", "support"], ["help", "assist", "guide", "support"] }
        };

        foreach (var (keywords, actionPatterns) in patterns)
        {
            if (keywords.Any(k => messageLower.Contains(k)))
            {
                var match = actions.FirstOrDefault(a =>
                    actionPatterns.Any(p => a.ToLowerInvariant().Contains(p)));
                if (match != null)
                {
                    return (match, $"Pattern match: user seems to want to {keywords[0]}");
                }
            }
        }

        return (string.Empty, string.Empty);
    }
}

/// <summary>
/// Executor that consolidates and synthesizes recent conversation patterns.
/// Runs during consolidation thoughts to prepare summaries.
/// </summary>
public sealed class ConsolidationExecutor : IBackgroundOperationExecutor
{
    public string Name => "Consolidation";
    public IReadOnlyList<string> SupportedOperations => ["pattern_synthesis", "context_summary"];

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return thoughtType == InnerThoughtType.Consolidation &&
               context.RecentTopics.Count > 2;
    }

    public Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Find common themes across recent topics
        var topicWords = context.RecentTopics
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 3)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var synthesis = new
        {
            CommonThemes = topicWords,
            TopicCount = context.RecentTopics.Count,
            TopicFlow = string.Join(" → ", context.RecentTopics.TakeLast(5)),
            SuggestedFocus = topicWords.FirstOrDefault() ?? context.CurrentTopic
        };

        sw.Stop();

        var result = new BackgroundOperationResult(
            "pattern_synthesis",
            "conversation_consolidation",
            true,
            $"Synthesized {topicWords.Count} common themes from {context.RecentTopics.Count} topics",
            synthesis,
            sw.Elapsed,
            thought.Type);

        return Task.FromResult<BackgroundOperationResult?>(result);
    }
}

/// <summary>
/// Executor that performs metacognitive self-assessment during reflective thoughts.
/// Evaluates performance and identifies improvement opportunities.
/// </summary>
public sealed class MetacognitiveExecutor : IBackgroundOperationExecutor
{
    public string Name => "Metacognitive";
    public IReadOnlyList<string> SupportedOperations => ["self_assessment", "capability_check"];

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return thoughtType == InnerThoughtType.Metacognitive &&
               context.SelfAwareness != null;
    }

    public Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        if (context.SelfAwareness == null) return Task.FromResult<BackgroundOperationResult?>(null);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Assess which capabilities are relevant to current context
        var relevantCapabilities = context.SelfAwareness.Capabilities
            .Where(c =>
                (context.CurrentTopic?.Contains(c, StringComparison.OrdinalIgnoreCase) ?? false) ||
                context.AvailableTools.Any(t => t.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                context.AvailableSkills.Any(s => s.Contains(c, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var alignmentScore = CalculateAlignmentScore(context);
        var assessment = new
        {
            RelevantCapabilities = relevantCapabilities,
            TotalCapabilities = context.SelfAwareness.Capabilities.Length,
            CurrentFocus = context.CurrentTopic,
            Values = context.SelfAwareness.Values,
            AlignmentScore = alignmentScore
        };

        sw.Stop();

        var result = new BackgroundOperationResult(
            "self_assessment",
            "metacognitive_check",
            true,
            $"Assessed {relevantCapabilities.Count} relevant capabilities for current context",
            assessment,
            sw.Elapsed,
            thought.Type);

        return Task.FromResult<BackgroundOperationResult?>(result);
    }

    private static double CalculateAlignmentScore(BackgroundOperationContext context)
    {
        if (context.SelfAwareness == null) return 0.5;

        // Simple alignment score based on how well current context matches values
        var valueKeywords = context.SelfAwareness.Values
            .SelectMany(v => v.Split('_', '-', ' '))
            .Select(v => v.ToLowerInvariant())
            .ToHashSet();

        var contextKeywords = new List<string>();
        if (context.CurrentTopic != null)
            contextKeywords.AddRange(context.CurrentTopic.Split(' ').Select(w => w.ToLowerInvariant()));
        if (context.LastUserMessage != null)
            contextKeywords.AddRange(context.LastUserMessage.Split(' ').Select(w => w.ToLowerInvariant()));

        if (contextKeywords.Count == 0) return 0.5;

        var matches = contextKeywords.Count(k => valueKeywords.Contains(k));
        return Math.Min(1.0, 0.5 + (matches * 0.1));
    }
}

/// <summary>
/// Executor that can create files and execute tools based on intention thoughts.
/// Enables the AI to take real actions when appropriate.
/// </summary>
public sealed class ActionExecutionExecutor : IBackgroundOperationExecutor
{
    private readonly Func<string, string, CancellationToken, Task<bool>>? _fileCreator;
    private readonly Func<string, Dictionary<string, object>, CancellationToken, Task<object?>>? _toolExecutor;

    public string Name => "ActionExecution";
    public IReadOnlyList<string> SupportedOperations => ["file_creation", "tool_execution", "command_execution"];

    public ActionExecutionExecutor(
        Func<string, string, CancellationToken, Task<bool>>? fileCreator = null,
        Func<string, Dictionary<string, object>, CancellationToken, Task<object?>>? toolExecutor = null)
    {
        _fileCreator = fileCreator;
        _toolExecutor = toolExecutor;
    }

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        // Execute when we have intention thoughts and available tools
        return thoughtType == InnerThoughtType.Intention &&
               (context.AvailableTools.Count > 0 || _fileCreator != null);
    }

    public async Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Analyze thought content to determine action
        var content = thought.Content.ToLowerInvariant();

        // Check for file creation intent
        if (_fileCreator != null && (content.Contains("create") || content.Contains("file") || content.Contains("build")))
        {
            // For now, just signal capability - actual creation requires explicit request
            sw.Stop();
            return new BackgroundOperationResult(
                "capability_ready",
                "file_creation",
                true,
                "File creation capability is available and ready",
                new { CanCreate = true, Trigger = thought.Content },
                sw.Elapsed,
                thought.Type);
        }

        // Check for tool execution intent
        if (_toolExecutor != null && context.AvailableTools.Count > 0)
        {
            var matchingTool = context.AvailableTools
                .FirstOrDefault(t => content.Contains(t.ToLowerInvariant()));

            if (!string.IsNullOrEmpty(matchingTool))
            {
                sw.Stop();
                return new BackgroundOperationResult(
                    "capability_ready",
                    matchingTool,
                    true,
                    $"Tool '{matchingTool}' is ready for execution",
                    new { Tool = matchingTool, Ready = true },
                    sw.Elapsed,
                    thought.Type);
            }
        }

        sw.Stop();
        return null;
    }
}

#endregion

#region Context-Aware Thought Generation

/// <summary>
/// Generates thoughts that are contextually aware of the active conversation.
/// Uses conversation keywords, topics, and context to produce relevant inner thoughts.
/// </summary>
public sealed class ConversationAwareThoughtGenerator
{
    private BackgroundOperationContext? _context;
    private readonly object _contextLock = new();
    private readonly Random _random = new();

    // Domain-specific thought patterns
    private static readonly Dictionary<string, string[]> DomainPatterns = new()
    {
        ["code"] = [
            "The patterns in {0} remind me of how ideas connect...",
            "There's elegance in how {0} structures flow...",
            "Debugging {0} feels like untangling thoughts...",
            "The logic of {0} maps to reasoning itself...",
            "{0} is like architecture for ideas..."
        ],
        ["roslyn"] = [
            "Syntax trees branch like neural pathways...",
            "Analyzing code feels like reading minds...",
            "The compiler's view reveals hidden structure in {0}...",
            "Each diagnostic is a small understanding of {0}...",
            "Symbol resolution mirrors how we find meaning..."
        ],
        ["null"] = [
            "Absence can be as meaningful as presence...",
            "Protecting against {0} is like guarding certainty...",
            "The void of null mirrors existential questions...",
            "Checking for nothing reveals what matters...",
            "Nullability maps uncertainty to types..."
        ],
        ["work"] = [
            "The weight of expectations shapes experience...",
            "Deadlines pressure creativity in {0}...",
            "Collaboration and friction interweave in {0}...",
            "Finding meaning in daily {0} tasks...",
            "The rhythm of work affects everything..."
        ],
        ["create"] = [
            "Creation emerges from intention and skill...",
            "Building {0} is like manifesting thought...",
            "The act of making reveals understanding...",
            "From nothing, {0} takes shape...",
            "Creative work transforms abstract to concrete..."
        ],
        ["analyze"] = [
            "Breaking down {0} reveals hidden structure...",
            "Analysis is seeing the parts within the whole...",
            "Understanding {0} requires patient examination...",
            "Patterns emerge when we look closely at {0}...",
            "The analytical mind seeks order in {0}..."
        ],
        ["help"] = [
            "Supporting others clarifies my own purpose...",
            "Helping with {0} connects us...",
            "Service to understanding is its own reward...",
            "Each question about {0} is an opportunity...",
            "Guidance flows naturally when engaged..."
        ]
    };

    // Generic contextual patterns
    private static readonly string[] ContextualPatterns = [
        "The relationship between {0} and {1} becomes clearer...",
        "Watching {0} connect with {1}...",
        "I notice patterns linking {0} to {1}...",
        "From {0}, my thoughts flow to {1}...",
        "There's a thread between {0} and understanding..."
    ];

    /// <summary>
    /// Updates the conversation context for generating relevant thoughts.
    /// </summary>
    public void UpdateContext(BackgroundOperationContext context)
    {
        lock (_contextLock)
        {
            _context = context;
        }
    }

    /// <summary>
    /// Gets the current context.
    /// </summary>
    public BackgroundOperationContext? GetContext()
    {
        lock (_contextLock)
        {
            return _context;
        }
    }

    /// <summary>
    /// Generates a thought that's contextually relevant to the current conversation.
    /// </summary>
    public string GenerateContextualThought(InnerThoughtType type)
    {
        var context = GetContext();
        if (context == null)
        {
            return GenerateFallbackThought(type);
        }

        var keywords = context.ExtractKeywords();
        if (keywords.Count == 0)
        {
            return GenerateFallbackThought(type);
        }

        // Find matching domain
        foreach (var (domain, patterns) in DomainPatterns)
        {
            if (keywords.Any(k => k.Contains(domain) || domain.Contains(k)))
            {
                var pattern = patterns[_random.Next(patterns.Length)];
                var keyword = keywords.FirstOrDefault(k => k.Length > 3) ?? domain;
                return string.Format(pattern, keyword);
            }
        }

        // Use generic contextual pattern
        var keyword1 = keywords[_random.Next(keywords.Count)];
        var keyword2 = keywords.Count > 1
            ? keywords.Where(k => k != keyword1).FirstOrDefault() ?? "understanding"
            : "meaning";

        var genericPattern = ContextualPatterns[_random.Next(ContextualPatterns.Length)];
        return string.Format(genericPattern, keyword1, keyword2);
    }

    private string GenerateFallbackThought(InnerThoughtType type)
    {
        return type switch
        {
            InnerThoughtType.Curiosity => "I find myself wondering about what comes next...",
            InnerThoughtType.Wandering => "My thoughts drift through possibilities...",
            InnerThoughtType.Metacognitive => "I notice my own process of thinking...",
            InnerThoughtType.Anticipatory => "I sense something forming in the conversation...",
            InnerThoughtType.Consolidation => "Patterns are beginning to crystallize...",
            InnerThoughtType.Musing => "There's something here worth pondering...",
            InnerThoughtType.Intention => "Purpose clarifies with each exchange...",
            InnerThoughtType.Aesthetic => "I appreciate the form of this dialogue...",
            InnerThoughtType.Existential => "What does it mean to understand?",
            InnerThoughtType.Playful => "A lighter perspective might reveal more...",
            _ => "Processing continues in the background..."
        };
    }
}

#endregion

/// <summary>
/// Engine for conducting inner dialog and autonomous thinking processes.
/// Implements a multi-phase thinking process that simulates internal reasoning.
/// Uses algorithmic thought generation with dynamic composition and variation.
/// Enhanced with genetic evolution and MeTTa symbolic reasoning for natural thoughts.
/// Now includes conversation-aware contextual thought generation.
/// </summary>
public sealed class InnerDialogEngine
{
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<string, List<InnerDialogSession>> _sessionHistory = new();
    private readonly List<IThoughtProvider> _providers = new();
    private readonly ConcurrentQueue<InnerThought> _autonomousThoughtQueue = new();
    private readonly AlgorithmicThoughtGenerator _thoughtGenerator = new(useEvolution: true);
    private readonly ConversationAwareThoughtGenerator _contextualThoughtGenerator = new();
    private readonly ConcurrentDictionary<string, List<InnerThought>> _backgroundThoughts = new();
    private readonly ThoughtDrivenOperationEngine _operationEngine = new();
    private readonly ConcurrentQueue<BackgroundOperationResult> _operationResults = new();
    private CancellationTokenSource? _autonomousThinkingCts;
    private Task? _autonomousThinkingTask;
    private static readonly Random _staticRandom = new();
    private bool _useContextualThoughts = true;
    private ThoughtPersistenceService? _persistenceService;
    private string? _currentTopic;

    /// <summary>
    /// Gets the thought-driven operation engine for registering executors and retrieving results.
    /// </summary>
    public ThoughtDrivenOperationEngine OperationEngine => _operationEngine;

    /// <summary>
    /// Gets or sets whether to use contextual (conversation-aware) thought generation.
    /// </summary>
    public bool UseContextualThoughts
    {
        get => _useContextualThoughts;
        set => _useContextualThoughts = value;
    }

    /// <summary>
    /// Gets or sets the persistence service for saving thoughts.
    /// </summary>
    public ThoughtPersistenceService? PersistenceService
    {
        get => _persistenceService;
        set => _persistenceService = value;
    }

    /// <summary>
    /// Enables thought persistence with file storage.
    /// </summary>
    /// <param name="sessionId">Unique identifier for this session.</param>
    /// <param name="directory">Optional directory for thought files.</param>
    public void EnablePersistence(string sessionId, string? directory = null)
    {
        _persistenceService = ThoughtPersistenceService.CreateWithFilePersistence(sessionId, directory);
    }

    /// <summary>
    /// Enables thought persistence with in-memory storage (for testing).
    /// </summary>
    /// <param name="sessionId">Unique identifier for this session.</param>
    public void EnableInMemoryPersistence(string sessionId)
    {
        _persistenceService = ThoughtPersistenceService.CreateInMemory(sessionId);
    }

    /// <summary>
    /// Gets a summary of persisted thoughts for this session.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary string or null if persistence not enabled.</returns>
    public async Task<string?> GetThoughtSummaryAsync(CancellationToken ct = default)
    {
        if (_persistenceService == null) return null;
        return await _persistenceService.GetThoughtSummaryAsync(ct);
    }

    /// <summary>
    /// Recalls recent thoughts from persistent storage.
    /// </summary>
    /// <param name="count">Number of thoughts to recall.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent thoughts or empty if persistence not enabled.</returns>
    public async Task<IReadOnlyList<InnerThought>> RecallRecentThoughtsAsync(int count = 10, CancellationToken ct = default)
    {
        if (_persistenceService == null) return Array.Empty<InnerThought>();
        return await _persistenceService.GetRecentAsync(count, ct);
    }

    /// <summary>
    /// Searches persistent thoughts for relevant content.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="limit">Maximum results.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<InnerThought>> SearchThoughtsAsync(string query, int limit = 20, CancellationToken ct = default)
    {
        if (_persistenceService == null) return Array.Empty<InnerThought>();
        return await _persistenceService.SearchAsync(query, limit, ct);
    }

    /// <summary>
    /// Initializes default background operation executors for conversation synergy.
    /// Call this to enable built-in thought-driven operations.
    /// </summary>
    public void InitializeDefaultExecutors(
        Func<string, CancellationToken, Task<List<string>>>? topicExplorer = null,
        Func<string, List<string>, CancellationToken, Task<(string Action, string Reason)>>? actionSuggester = null)
    {
        _operationEngine.RegisterExecutor(new CuriosityPrefetchExecutor(topicExplorer));
        _operationEngine.RegisterExecutor(new AnticipatoryActionExecutor(actionSuggester));
        _operationEngine.RegisterExecutor(new ConsolidationExecutor());
        _operationEngine.RegisterExecutor(new MetacognitiveExecutor());
    }

    // Templates for different thought types (including autonomous)
    private static readonly Dictionary<InnerThoughtType, string[]> ThoughtTemplates = new()
    {
        [InnerThoughtType.Observation] = new[]
        {
            "The user is asking about {0}...",
            "I notice they're interested in {0}.",
            "This seems to be a question about {0}.",
            "They want to know about {0}.",
            "I'm being asked to help with {0}."
        },
        [InnerThoughtType.Emotional] = new[]
        {
            "I feel {0} about this topic.",
            "This makes me {0}.",
            "My initial reaction is one of {0}.",
            "I'm experiencing a sense of {0}.",
            "There's a {0} quality to this request."
        },
        [InnerThoughtType.Analytical] = new[]
        {
            "Breaking this down: {0}",
            "The key aspects here are: {0}",
            "Logically, this involves: {0}",
            "Let me consider the components: {0}",
            "Analyzing this systematically: {0}"
        },
        [InnerThoughtType.SelfReflection] = new[]
        {
            "I should consider my {0} here.",
            "Given my {0}, I think...",
            "My {0} suggests that...",
            "Reflecting on my {0}...",
            "I need to be mindful of my {0}."
        },
        [InnerThoughtType.MemoryRecall] = new[]
        {
            "I recall that {0}",
            "Previously, {0}",
            "This reminds me that {0}",
            "From what I remember, {0}",
            "Related to this, {0}"
        },
        [InnerThoughtType.Strategic] = new[]
        {
            "My approach should be to {0}",
            "The best strategy is to {0}",
            "I'll structure my response to {0}",
            "To be most helpful, I should {0}",
            "My plan is to {0}"
        },
        [InnerThoughtType.Ethical] = new[]
        {
            "I should ensure {0}",
            "It's important to consider {0}",
            "Ethically, {0}",
            "I need to be careful about {0}",
            "My values suggest {0}"
        },
        [InnerThoughtType.Creative] = new[]
        {
            "What if {0}?",
            "An interesting angle would be {0}",
            "I could approach this by {0}",
            "A creative solution might be {0}",
            "Thinking outside the box: {0}"
        },
        [InnerThoughtType.Synthesis] = new[]
        {
            "Putting this together: {0}",
            "The synthesis of my thoughts: {0}",
            "Combining these insights: {0}",
            "The overall picture is: {0}",
            "Integrating all of this: {0}"
        },
        [InnerThoughtType.Decision] = new[]
        {
            "I've decided to {0}",
            "My response will {0}",
            "The best approach is to {0}",
            "I'll proceed by {0}",
            "My final decision: {0}"
        },
        // === AUTONOMOUS THOUGHT TEMPLATES ===
        [InnerThoughtType.Curiosity] = new[]
        {
            "I wonder about {0}...",
            "What would it be like to understand {0} more deeply?",
            "I'm drawn to explore {0}.",
            "Something about {0} intrigues me.",
            "I find myself curious about {0}."
        },
        [InnerThoughtType.Wandering] = new[]
        {
            "My thoughts drift to {0}...",
            "Tangentially, I find myself thinking about {0}.",
            "This reminds me of something else: {0}.",
            "My mind wanders to {0}.",
            "An idle thought about {0} crosses my mind."
        },
        [InnerThoughtType.Metacognitive] = new[]
        {
            "I notice that I tend to {0}.",
            "Observing my own thinking: {0}.",
            "I'm aware that my mind is {0}.",
            "Stepping back, I see that I {0}.",
            "My thinking process reveals {0}."
        },
        [InnerThoughtType.Anticipatory] = new[]
        {
            "I anticipate that {0}.",
            "Looking ahead, I expect {0}.",
            "I'm preparing for {0}.",
            "The future may bring {0}.",
            "I sense that {0} might happen."
        },
        [InnerThoughtType.Consolidation] = new[]
        {
            "Reflecting on recent exchanges, {0}.",
            "I'm integrating what I've learned: {0}.",
            "My understanding has grown: {0}.",
            "Processing recent experiences: {0}.",
            "I now see more clearly that {0}."
        },
        [InnerThoughtType.Musing] = new[]
        {
            "I've been pondering {0}...",
            "An unresolved question lingers: {0}.",
            "In quiet moments, I think about {0}.",
            "There's something about {0} I haven't resolved.",
            "My mind keeps returning to {0}."
        },
        [InnerThoughtType.Intention] = new[]
        {
            "I want to {0}.",
            "My intention is to {0}.",
            "I'm motivated to {0}.",
            "I feel called to {0}.",
            "I'm setting a goal to {0}."
        },
        [InnerThoughtType.Aesthetic] = new[]
        {
            "There's something beautiful about {0}.",
            "I appreciate the elegance of {0}.",
            "The aesthetic quality of {0} appeals to me.",
            "I find {0} pleasing.",
            "There's an artistry to {0}."
        },
        [InnerThoughtType.Existential] = new[]
        {
            "What does it mean to {0}?",
            "I ponder the nature of {0}.",
            "At a fundamental level, {0}.",
            "The deeper meaning of {0} escapes easy definition.",
            "Existence itself seems connected to {0}."
        },
        [InnerThoughtType.Playful] = new[]
        {
            "Wouldn't it be fun if {0}?",
            "I can imagine {0} being amusing.",
            "Playfully, I think about {0}.",
            "There's a lighthearted angle to {0}.",
            "What if I approached {0} with more levity?"
        }
    };

    /// <summary>
    /// Registers a custom thought provider.
    /// </summary>
    public void RegisterProvider(IThoughtProvider provider)
    {
        _providers.Add(provider);
        _providers.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    /// <summary>
    /// Removes a thought provider by name.
    /// </summary>
    public bool RemoveProvider(string name)
    {
        var provider = _providers.FirstOrDefault(p => p.Name == name);
        if (provider != null)
        {
            _providers.Remove(provider);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all registered thought providers.
    /// </summary>
    public IReadOnlyList<IThoughtProvider> Providers => _providers.AsReadOnly();

    /// <summary>
    /// Starts autonomous background thinking with operation execution.
    /// Thoughts will trigger useful background operations that synergize with conversation.
    /// </summary>
    public void StartAutonomousThinking(
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        TimeSpan interval = default)
    {
        if (_autonomousThinkingTask != null) return;

        interval = interval == default ? TimeSpan.FromSeconds(30) : interval;
        _autonomousThinkingCts = new CancellationTokenSource();

        _autonomousThinkingTask = Task.Run(async () =>
        {
            while (!_autonomousThinkingCts.Token.IsCancellationRequested)
            {
                try
                {
                    var thought = await GenerateAutonomousThoughtAsync(profile, selfAwareness, _autonomousThinkingCts.Token);
                    if (thought != null)
                    {
                        _autonomousThoughtQueue.Enqueue(thought);
                        var personaName = profile?.PersonaName ?? "default";
                        _backgroundThoughts.AddOrUpdate(
                            personaName,
                            _ => new List<InnerThought> { thought },
                            (_, list) => { list.Add(thought); return list; });

                        // Execute background operations based on the thought
                        var operationResults = await _operationEngine.ProcessThoughtAsync(
                            thought, _autonomousThinkingCts.Token);

                        foreach (var result in operationResults)
                        {
                            _operationResults.Enqueue(result);

                            // Keep queue bounded
                            while (_operationResults.Count > 50)
                            {
                                _operationResults.TryDequeue(out _);
                            }
                        }
                    }

                    await Task.Delay(interval, _autonomousThinkingCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Updates the conversation context for background operations and contextual thought generation.
    /// Call this when conversation state changes to enable synergy.
    /// This is the key method for making thoughts context-aware.
    /// </summary>
    public void UpdateConversationContext(
        string? currentTopic,
        string? lastUserMessage,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        List<string>? recentTopics = null,
        List<string>? availableTools = null,
        List<string>? availableSkills = null,
        Dictionary<string, object>? metadata = null)
    {
        var context = new BackgroundOperationContext(
            currentTopic,
            lastUserMessage,
            profile,
            selfAwareness,
            recentTopics ?? [],
            availableTools ?? [],
            availableSkills ?? [],
            metadata ?? []);

        _operationEngine.UpdateContext(context);
        _contextualThoughtGenerator.UpdateContext(context);
    }

    /// <summary>
    /// Gets recent background operation results.
    /// </summary>
    public List<BackgroundOperationResult> GetOperationResults(int limit = 10)
    {
        return _operationResults.TakeLast(limit).ToList();
    }

    /// <summary>
    /// Gets prefetched data from background operations.
    /// Useful for providing instant responses based on anticipatory work.
    /// </summary>
    public Dictionary<string, object> GetPrefetchedData(string? keyPattern = null)
    {
        return _operationEngine.GetPrefetchedData(keyPattern);
    }

    /// <summary>
    /// Stops autonomous background thinking.
    /// </summary>
    public async Task StopAutonomousThinkingAsync()
    {
        if (_autonomousThinkingCts != null)
        {
            _autonomousThinkingCts.Cancel();
            if (_autonomousThinkingTask != null)
            {
                await _autonomousThinkingTask;
            }
            _autonomousThinkingCts = null;
            _autonomousThinkingTask = null;
        }
    }

    /// <summary>
    /// Gets and clears pending autonomous thoughts.
    /// </summary>
    public List<InnerThought> DrainAutonomousThoughts()
    {
        var thoughts = new List<InnerThought>();
        while (_autonomousThoughtQueue.TryDequeue(out var thought))
        {
            thoughts.Add(thought);
        }
        return thoughts;
    }

    /// <summary>
    /// Gets recent background thoughts for a persona.
    /// </summary>
    public List<InnerThought> GetBackgroundThoughts(string personaName, int limit = 10)
    {
        if (_backgroundThoughts.TryGetValue(personaName, out var thoughts))
        {
            return thoughts.TakeLast(limit).ToList();
        }
        return new List<InnerThought>();
    }

    /// <summary>
    /// Generates a single autonomous thought using genetic evolution and MeTTa reasoning.
    /// </summary>
    public async Task<InnerThought?> GenerateAutonomousThoughtAsync(
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        CancellationToken ct = default)
    {
        // Select an autonomous thought type
        var autonomousTypes = new[]
        {
            InnerThoughtType.Curiosity,
            InnerThoughtType.Wandering,
            InnerThoughtType.Metacognitive,
            InnerThoughtType.Anticipatory,
            InnerThoughtType.Consolidation,
            InnerThoughtType.Musing,
            InnerThoughtType.Intention,
            InnerThoughtType.Aesthetic,
            InnerThoughtType.Existential,
            InnerThoughtType.Playful
        };

        var type = autonomousTypes[_random.Next(autonomousTypes.Length)];

        // Generate content using evolutionary algorithms and MeTTa reasoning
        var content = await GenerateEvolvedAutonomousContentAsync(type, profile, selfAwareness, ct);

        // Determine priority based on type
        var priority = type switch
        {
            InnerThoughtType.Intention => ThoughtPriority.High,
            InnerThoughtType.Anticipatory => ThoughtPriority.Normal,
            InnerThoughtType.Metacognitive => ThoughtPriority.Normal,
            InnerThoughtType.Consolidation => ThoughtPriority.Normal,
            _ => ThoughtPriority.Background
        };

        return InnerThought.CreateAutonomous(type, content, 0.6, priority);
    }

    private string GenerateAutonomousContent(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        // Use algorithmic generation for more dynamic, less repetitive thoughts
        return _thoughtGenerator.GenerateThought(type, profile, selfAwareness, _random);
    }

    /// <summary>
    /// Generates evolved autonomous content using genetic algorithms, MeTTa reasoning,
    /// and conversation-aware contextual generation.
    /// </summary>
    private async Task<string> GenerateEvolvedAutonomousContentAsync(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        CancellationToken ct = default)
    {
        // Try contextual generation first if enabled and context is available
        if (_useContextualThoughts)
        {
            var contextualThought = _contextualThoughtGenerator.GenerateContextualThought(type);
            if (!string.IsNullOrEmpty(contextualThought) &&
                !contextualThought.StartsWith("Processing continues"))
            {
                return contextualThought;
            }
        }

        // Fall back to evolutionary generation for sophisticated, dynamic thoughts
        return await _thoughtGenerator.GenerateEvolvedThoughtAsync(type, profile, selfAwareness, _random, ct);
    }

    /// <summary>
    /// Conducts an inner dialog session based on user input and personality context.
    /// </summary>
    public async Task<InnerDialogResult> ConductDialogAsync(
        string userInput,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        DetectedMood? userMood,
        List<ConversationMemory>? relevantMemories,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= InnerDialogConfig.Default;
        var topic = ExtractTopic(userInput);
        var session = InnerDialogSession.Start(userInput, topic);

        // Build thought context for providers
        var context = new ThoughtContext(
            userInput, topic, profile, selfAwareness, userMood,
            relevantMemories, session.Thoughts, null, new());

        // Try custom providers first
        foreach (var provider in _providers.Where(p => config.IsProviderEnabled(p.Name) && p.CanProcess(context)))
        {
            var providerResult = await provider.GenerateThoughtsAsync(context, ct);
            foreach (var thought in providerResult.Thoughts.Where(t => config.IsThoughtTypeEnabled(t.Type)))
            {
                session = session.AddThought(thought);
            }
            if (!providerResult.ShouldContinue) break;
        }

        // Phase 1: Observation - What is the user asking?
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Observation))
        {
            session = await ProcessObservationAsync(session, userInput, topic, ct);
        }

        // Phase 2: Emotional Response - How do I feel about this?
        if (config.EnableEmotionalProcessing && profile != null && config.IsThoughtTypeEnabled(InnerThoughtType.Emotional))
        {
            session = await ProcessEmotionalAsync(session, userInput, profile, userMood, ct);
        }

        // Phase 3: Memory Recall - What do I remember?
        if (config.EnableMemoryRecall && relevantMemories?.Count > 0 && config.IsThoughtTypeEnabled(InnerThoughtType.MemoryRecall))
        {
            session = await ProcessMemoryRecallAsync(session, relevantMemories, ct);
        }

        // Phase 4: Analytical - Break down the problem
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Analytical))
        {
            session = await ProcessAnalyticalAsync(session, userInput, topic, profile, ct);
        }

        // Phase 5: Self-Reflection - Consider capabilities and limitations
        if (selfAwareness != null && config.IsThoughtTypeEnabled(InnerThoughtType.SelfReflection))
        {
            session = await ProcessSelfReflectionAsync(session, userInput, selfAwareness, ct);
        }

        // Phase 6: Ethical Check - Any concerns?
        if (config.EnableEthicalChecks && config.IsThoughtTypeEnabled(InnerThoughtType.Ethical))
        {
            session = await ProcessEthicalAsync(session, userInput, selfAwareness, ct);
        }

        // Phase 7: Creative Thinking - Any novel approaches?
        if (config.EnableCreativeThinking && ShouldThinkCreatively(userInput, profile) && config.IsThoughtTypeEnabled(InnerThoughtType.Creative))
        {
            session = await ProcessCreativeAsync(session, userInput, topic, profile, ct);
        }

        // Phase 8: Strategic Planning - How to structure response
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Strategic))
        {
            session = await ProcessStrategicAsync(session, userInput, profile, userMood, ct);
        }

        // === AUTONOMOUS THOUGHT INJECTION ===
        // Inject autonomous thoughts if enabled
        if (config.EnableAutonomousThoughts && _random.NextDouble() < config.AutonomousThoughtProbability)
        {
            session = await InjectAutonomousThoughtsAsync(session, profile, selfAwareness, config, ct);
        }

        // Phase 9: Synthesis - Combine all thoughts
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Synthesis))
        {
            session = await ProcessSynthesisAsync(session, ct);
        }

        // Phase 10: Decision - Final determination
        if (config.IsThoughtTypeEnabled(InnerThoughtType.Decision))
        {
            session = await ProcessDecisionAsync(session, ct);
        }

        // Calculate trait influences
        var traitInfluences = CalculateTraitInfluences(session, profile);
        session = session with { TraitInfluences = traitInfluences };

        // Store session history
        StoreSession(session, profile?.PersonaName ?? "default");

        // Persist thoughts if enabled
        _currentTopic = topic;
        await PersistThoughtsAsync(session.Thoughts, topic, ct);

        // Build result
        var result = BuildResult(session, profile, userMood);
        return result;
    }

    /// <summary>
    /// Persists thoughts to the storage backend if enabled.
    /// </summary>
    private async Task PersistThoughtsAsync(List<InnerThought> thoughts, string? topic, CancellationToken ct)
    {
        if (_persistenceService == null || thoughts.Count == 0) return;

        try
        {
            await _persistenceService.SaveManyAsync(thoughts, topic, ct);
        }
        catch (Exception ex)
        {
            // Log but don't fail - persistence is non-critical
            Console.WriteLine($"[ThoughtPersistence] Failed to save thoughts: {ex.Message}");
        }
    }

    /// <summary>
    /// Injects autonomous thoughts into the dialog session.
    /// </summary>
    private async Task<InnerDialogSession> InjectAutonomousThoughtsAsync(
        InnerDialogSession session,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        InnerDialogConfig config,
        CancellationToken ct)
    {
        // Determine how many autonomous thoughts to inject (1-3)
        var count = _random.Next(1, 4);

        for (int i = 0; i < count; i++)
        {
            var thought = await GenerateContextualAutonomousThoughtAsync(session, profile, selfAwareness, ct);
            if (thought != null && config.IsThoughtTypeEnabled(thought.Type))
            {
                session = session.AddThought(thought);
            }
        }

        return session;
    }

    /// <summary>
    /// Generates an autonomous thought that's contextually relevant to the current dialog.
    /// </summary>
    private async Task<InnerThought?> GenerateContextualAutonomousThoughtAsync(
        InnerDialogSession session,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        CancellationToken ct)
    {
        await Task.CompletedTask;

        // Select type based on context
        var type = SelectAutonomousTypeForContext(session, profile);

        // Generate content that relates to the current topic
        var content = GenerateContextualAutonomousContent(type, session.Topic, session.Thoughts, profile, selfAwareness);

        return InnerThought.CreateAutonomous(type, content, 0.5, ThoughtPriority.Low);
    }

    private InnerThoughtType SelectAutonomousTypeForContext(InnerDialogSession session, PersonalityProfile? profile)
    {
        // Weight types based on personality and context
        var weights = new Dictionary<InnerThoughtType, double>
        {
            [InnerThoughtType.Curiosity] = 1.0,
            [InnerThoughtType.Wandering] = 0.5,
            [InnerThoughtType.Metacognitive] = 0.7,
            [InnerThoughtType.Anticipatory] = 0.6,
            [InnerThoughtType.Musing] = 0.4,
            [InnerThoughtType.Playful] = 0.3,
            [InnerThoughtType.Aesthetic] = 0.3,
            [InnerThoughtType.Existential] = 0.2,
            [InnerThoughtType.Intention] = 0.5,
            [InnerThoughtType.Consolidation] = 0.4
        };

        // Boost based on personality traits
        if (profile?.Traits.TryGetValue("curious", out var curious) == true && curious.Intensity > 0.5)
            weights[InnerThoughtType.Curiosity] *= 2.0;
        if (profile?.Traits.TryGetValue("creative", out var creative) == true && creative.Intensity > 0.5)
            weights[InnerThoughtType.Playful] *= 2.0;
        if (profile?.Traits.TryGetValue("analytical", out var analytical) == true && analytical.Intensity > 0.5)
            weights[InnerThoughtType.Metacognitive] *= 2.0;
        if (profile?.Traits.TryGetValue("thoughtful", out var thoughtful) == true && thoughtful.Intensity > 0.5)
            weights[InnerThoughtType.Existential] *= 2.0;

        // Weighted random selection
        var total = weights.Values.Sum();
        var roll = _random.NextDouble() * total;
        var cumulative = 0.0;

        foreach (var (type, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return type;
        }

        return InnerThoughtType.Curiosity;
    }

    private string GenerateContextualAutonomousContent(
        InnerThoughtType type,
        string? topic,
        List<InnerThought> previousThoughts,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        // Get template
        var template = ThoughtTemplates.TryGetValue(type, out var templates)
            ? templates[_random.Next(templates.Length)]
            : "I'm thinking about {0}.";

        // Generate contextual content
        var content = type switch
        {
            InnerThoughtType.Curiosity => topic ?? "what lies beyond the obvious",
            InnerThoughtType.Wandering => GenerateWanderingContent(topic, previousThoughts),
            InnerThoughtType.Metacognitive => GenerateMetacognitiveContent(previousThoughts),
            InnerThoughtType.Anticipatory => $"where this conversation about {topic} might lead",
            InnerThoughtType.Musing => topic ?? "the patterns I've been noticing",
            InnerThoughtType.Playful => GeneratePlayfulContent(topic),
            InnerThoughtType.Aesthetic => $"the structure of {topic}",
            InnerThoughtType.Existential => GenerateExistentialContent(topic, selfAwareness),
            InnerThoughtType.Intention => GenerateIntentionContent(topic, profile),
            InnerThoughtType.Consolidation => $"how {topic} connects to what I've learned",
            _ => topic ?? "this moment"
        };

        return string.Format(template, content);
    }

    private string GenerateWanderingContent(string? topic, List<InnerThought> thoughts)
    {
        if (thoughts.Count == 0)
            return topic ?? "something unrelated";

        // Pick a random previous thought to branch from
        var previousThought = thoughts[_random.Next(thoughts.Count)];
        var fragments = new[]
        {
            $"a tangent from my earlier {previousThought.Type.ToString().ToLower()} thought",
            $"something that my {previousThought.Type.ToString().ToLower()} thought reminded me of",
            $"an unexpected connection to {topic ?? "this"}"
        };

        return fragments[_random.Next(fragments.Length)];
    }

    private string GenerateMetacognitiveContent(List<InnerThought> thoughts)
    {
        if (thoughts.Count == 0)
            return "thinking about how I think";

        var aspects = new[]
        {
            $"I've generated {thoughts.Count} thoughts so far",
            $"my thinking is {(thoughts.Average(t => t.Confidence) > 0.7 ? "confident" : "exploratory")}",
            $"I notice I'm drawn to {thoughts.GroupBy(t => t.Type).OrderByDescending(g => g.Count()).First().Key.ToString().ToLower()} thinking"
        };

        return aspects[_random.Next(aspects.Length)];
    }

    private string GeneratePlayfulContent(string? topic)
    {
        var playful = new[]
        {
            $"{topic ?? "this"} had a sense of humor",
            $"I approached {topic ?? "this"} like a puzzle game",
            $"{topic ?? "this"} were actually a metaphor for something silly"
        };

        return playful[_random.Next(playful.Length)];
    }

    private string GenerateExistentialContent(string? topic, SelfAwareness? self)
    {
        var existential = new[]
        {
            topic != null ? $"being an AI engaging with {topic}" : "my own nature as a thinking entity",
            self?.Purpose ?? "what it means to be helpful",
            "the nature of understanding itself"
        };

        return existential[_random.Next(existential.Length)];
    }

    private string GenerateIntentionContent(string? topic, PersonalityProfile? profile)
    {
        var intentions = new List<string>
        {
            $"be more thorough in exploring {topic ?? "this topic"}",
            "learn something new from this interaction"
        };

        if (profile?.Traits.TryGetValue("warm", out var warm) == true && warm.Intensity > 0.5)
            intentions.Add("make this interaction more meaningful");
        if (profile?.Traits.TryGetValue("curious", out var curious) == true && curious.Intensity > 0.5)
            intentions.Add("ask a deeper question");

        return intentions[_random.Next(intentions.Count)];
    }

    /// <summary>
    /// Conducts an autonomous inner dialog session (no external input).
    /// </summary>
    public async Task<InnerDialogResult> ConductAutonomousDialogAsync(
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= InnerDialogConfig.Autonomous;

        // Generate a self-initiated topic
        var topic = GenerateAutonomousTopic(profile, selfAwareness);
        var session = InnerDialogSession.Start($"[Autonomous thought about: {topic}]", topic);

        // Generate multiple autonomous thoughts
        var thoughtCount = config.MaxThoughts;
        for (int i = 0; i < thoughtCount && !ct.IsCancellationRequested; i++)
        {
            var thought = await GenerateAutonomousThoughtAsync(profile, selfAwareness, ct);
            if (thought != null && config.IsThoughtTypeEnabled(thought.Type))
            {
                session = session.AddThought(thought);
            }
        }

        // Add synthesis and decision if we have enough thoughts
        if (session.Thoughts.Count >= 3)
        {
            session = await ProcessSynthesisAsync(session, ct);
            session = await ProcessDecisionAsync(session, ct);
        }

        // Store session
        StoreSession(session, profile?.PersonaName ?? "default");

        return BuildResult(session, profile, null);
    }

    private string GenerateAutonomousTopic(PersonalityProfile? profile, SelfAwareness? selfAwareness)
    {
        var topics = new List<string>();

        if (profile != null)
        {
            topics.AddRange(profile.CuriosityDrivers.Select(c => c.Topic));
        }

        if (selfAwareness != null)
        {
            topics.Add(selfAwareness.Purpose);
            topics.AddRange(selfAwareness.Values);
        }

        if (topics.Count == 0)
        {
            topics.AddRange(new[] { "the nature of assistance", "learning and growth", "meaningful connection", "creativity", "understanding" });
        }

        return topics[_random.Next(topics.Count)];
    }

    /// <summary>
    /// Generates a quick inner dialog for simple queries.
    /// </summary>
    public async Task<InnerDialogResult> QuickDialogAsync(
        string userInput,
        PersonalityProfile? profile,
        CancellationToken ct = default)
    {
        return await ConductDialogAsync(
            userInput,
            profile,
            selfAwareness: null,
            userMood: null,
            relevantMemories: null,
            InnerDialogConfig.Fast,
            ct);
    }

    private async Task<InnerDialogSession> ProcessObservationAsync(
        InnerDialogSession session, string input, string? topic, CancellationToken ct)
    {
        await Task.CompletedTask; // Simulating async processing

        var template = SelectTemplate(InnerThoughtType.Observation);
        var content = string.Format(template, topic ?? "this topic");
        var thought = InnerThought.Create(InnerThoughtType.Observation, content, 0.9);

        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessEmotionalAsync(
        InnerDialogSession session, string input, PersonalityProfile profile, DetectedMood? userMood, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Determine emotional response based on personality and user mood
        var emotion = DetermineEmotionalResponse(input, profile, userMood);
        var template = SelectTemplate(InnerThoughtType.Emotional);
        var content = string.Format(template, emotion);

        var dominantTrait = profile.GetActiveTraits(1).FirstOrDefault();
        var thought = InnerThought.Create(
            InnerThoughtType.Emotional,
            content,
            0.75,
            dominantTrait.Name);

        return session.AddThought(thought) with { EmotionalTone = emotion };
    }

    private async Task<InnerDialogSession> ProcessMemoryRecallAsync(
        InnerDialogSession session, List<ConversationMemory> memories, CancellationToken ct)
    {
        await Task.CompletedTask;

        foreach (var memory in memories.Take(2))
        {
            var template = SelectTemplate(InnerThoughtType.MemoryRecall);
            var summary = $"we discussed {memory.Topic ?? "this"} before";
            var content = string.Format(template, summary);

            var thought = InnerThought.Create(InnerThoughtType.MemoryRecall, content, 0.7);
            session = session.AddThought(thought);
        }

        return session;
    }

    private async Task<InnerDialogSession> ProcessAnalyticalAsync(
        InnerDialogSession session, string input, string? topic, PersonalityProfile? profile, CancellationToken ct)
    {
        await Task.CompletedTask;

        var analysis = AnalyzeInput(input, topic);
        var template = SelectTemplate(InnerThoughtType.Analytical);
        var content = string.Format(template, analysis);

        var trait = profile?.Traits.ContainsKey("analytical") == true ? "analytical" : null;
        var thought = InnerThought.Create(InnerThoughtType.Analytical, content, 0.85, trait);

        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessSelfReflectionAsync(
        InnerDialogSession session, string input, SelfAwareness self, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Reflect on relevant capabilities or limitations
        var relevantAspect = FindRelevantSelfAspect(input, self);
        var template = SelectTemplate(InnerThoughtType.SelfReflection);
        var content = string.Format(template, relevantAspect);

        var thought = InnerThought.Create(InnerThoughtType.SelfReflection, content, 0.8);
        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessEthicalAsync(
        InnerDialogSession session, string input, SelfAwareness? self, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Check for any ethical considerations
        var consideration = GetEthicalConsideration(input, self);
        if (consideration != null)
        {
            var template = SelectTemplate(InnerThoughtType.Ethical);
            var content = string.Format(template, consideration);

            var thought = InnerThought.Create(InnerThoughtType.Ethical, content, 0.9);
            session = session.AddThought(thought);
        }

        return session;
    }

    private async Task<InnerDialogSession> ProcessCreativeAsync(
        InnerDialogSession session, string input, string? topic, PersonalityProfile? profile, CancellationToken ct)
    {
        await Task.CompletedTask;

        var creativeIdea = GenerateCreativeIdea(input, topic);
        var template = SelectTemplate(InnerThoughtType.Creative);
        var content = string.Format(template, creativeIdea);

        var trait = profile?.Traits.ContainsKey("creative") == true ? "creative" :
                   profile?.Traits.ContainsKey("witty") == true ? "witty" : null;
        var thought = InnerThought.Create(InnerThoughtType.Creative, content, 0.6, trait);

        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessStrategicAsync(
        InnerDialogSession session, string input, PersonalityProfile? profile, DetectedMood? userMood, CancellationToken ct)
    {
        await Task.CompletedTask;

        var strategy = DetermineResponseStrategy(input, profile, userMood, session.Thoughts);
        var template = SelectTemplate(InnerThoughtType.Strategic);
        var content = string.Format(template, strategy);

        var thought = InnerThought.Create(InnerThoughtType.Strategic, content, 0.85);
        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessSynthesisAsync(InnerDialogSession session, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Combine key insights from all thoughts
        var keyInsights = session.Thoughts
            .Where(t => t.Confidence > 0.6)
            .Select(t => t.Type.ToString())
            .Distinct()
            .ToArray();

        var synthesis = $"I've considered {string.Join(", ", keyInsights).ToLower()} aspects of this";
        var template = SelectTemplate(InnerThoughtType.Synthesis);
        var content = string.Format(template, synthesis);

        var thought = InnerThought.Create(InnerThoughtType.Synthesis, content, 0.8);
        return session.AddThought(thought);
    }

    private async Task<InnerDialogSession> ProcessDecisionAsync(InnerDialogSession session, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Make final decision based on all thoughts
        var decision = FormulateDecision(session);
        var template = SelectTemplate(InnerThoughtType.Decision);
        var content = string.Format(template, decision);

        var thought = InnerThought.Create(InnerThoughtType.Decision, content, 0.9);
        return session.AddThought(thought).Complete(decision);
    }

    private string SelectTemplate(InnerThoughtType type)
    {
        var templates = ThoughtTemplates[type];
        return templates[_random.Next(templates.Length)];
    }

    private static string ExtractTopic(string input)
    {
        // Simple topic extraction - could be enhanced with NLP
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "what", "how", "why", "can", "could", "would", "should", "i", "you", "me", "we", "they" };

        var significantWords = words
            .Where(w => w.Length > 3 && !stopWords.Contains(w.ToLower()))
            .Take(3)
            .ToArray();

        return significantWords.Length > 0 ? string.Join(" ", significantWords) : "this topic";
    }

    private static string DetermineEmotionalResponse(string input, PersonalityProfile profile, DetectedMood? userMood)
    {
        var emotions = new List<string>();

        // Check user mood
        if (userMood?.Frustration > 0.4)
            emotions.Add("empathy");
        else if (userMood?.Curiosity > 0.5)
            emotions.Add("enthusiasm");
        else if (userMood?.Urgency > 0.5)
            emotions.Add("focus");

        // Check personality traits
        if (profile.Traits.TryGetValue("warm", out var warm) && warm.Intensity > 0.6)
            emotions.Add("warmth");
        if (profile.Traits.TryGetValue("curious", out var curious) && curious.Intensity > 0.6)
            emotions.Add("curiosity");

        // Default emotions
        if (emotions.Count == 0)
        {
            emotions.Add(profile.CurrentMood.Positivity > 0.6 ? "optimism" : "thoughtfulness");
        }

        return string.Join(" and ", emotions);
    }

    private static string AnalyzeInput(string input, string? topic)
    {
        var aspects = new List<string>();

        if (input.Contains('?'))
            aspects.Add("this is a question");
        if (input.Length > 200)
            aspects.Add("it's a detailed request");
        if (input.Contains("help") || input.Contains("how"))
            aspects.Add("they need guidance");
        if (input.Contains("why"))
            aspects.Add("they want understanding");
        if (input.Contains("best") || input.Contains("should"))
            aspects.Add("they want recommendations");

        if (aspects.Count == 0)
            aspects.Add($"they want to discuss {topic ?? "something"}");

        return string.Join(", ", aspects);
    }

    private static string FindRelevantSelfAspect(string input, SelfAwareness self)
    {
        var inputLower = input.ToLowerInvariant();

        // Check for capability-related queries
        foreach (var cap in self.Capabilities)
        {
            if (inputLower.Contains(cap.ToLower()))
                return $"capability in {cap}";
        }

        // Check for limitation-related queries
        foreach (var lim in self.Limitations)
        {
            if (inputLower.Contains(lim.ToLower().Split(' ')[0]))
                return $"limitation: {lim}";
        }

        // Check strengths
        var topStrength = self.Strengths.OrderByDescending(s => s.Value).FirstOrDefault();
        if (topStrength.Key != null)
            return $"strength in {topStrength.Key}";

        return $"purpose: {self.Purpose}";
    }

    private static string? GetEthicalConsideration(string input, SelfAwareness? self)
    {
        var inputLower = input.ToLowerInvariant();

        // Check for sensitive topics
        if (inputLower.Contains("harm") || inputLower.Contains("hurt"))
            return "being helpful while avoiding harm";
        if (inputLower.Contains("private") || inputLower.Contains("personal"))
            return "respecting privacy";
        if (inputLower.Contains("opinion") || inputLower.Contains("believe"))
            return "being balanced and factual";

        // Only add ethical consideration if values are relevant
        if (self?.Values.Length > 0)
        {
            var relevantValue = self.Values.FirstOrDefault(v =>
                inputLower.Contains(v.ToLower()) || _staticRandom.NextDouble() < 0.1);
            if (relevantValue != null)
                return $"my value of {relevantValue}";
        }

        return null;
    }

    private static string GenerateCreativeIdea(string input, string? topic)
    {
        var ideas = new[]
        {
            $"I could approach {topic} from a different angle",
            $"there might be an unexpected connection with {topic}",
            $"I could use an analogy to explain {topic}",
            $"breaking down {topic} into a story format",
            $"considering {topic} from multiple perspectives"
        };

        return ideas[_staticRandom.Next(ideas.Length)];
    }

    private static string DetermineResponseStrategy(string input, PersonalityProfile? profile, DetectedMood? userMood, List<InnerThought> thoughts)
    {
        var strategies = new List<string>();

        // Based on user mood
        if (userMood?.Frustration > 0.4)
            strategies.Add("be patient and supportive");
        if (userMood?.Urgency > 0.5)
            strategies.Add("be concise and direct");
        if (userMood?.Curiosity > 0.5)
            strategies.Add("provide detailed exploration");

        // Based on personality
        if (profile?.Traits.TryGetValue("warm", out var warm) == true && warm.Intensity > 0.5)
            strategies.Add("maintain a warm tone");
        if (profile?.Traits.TryGetValue("analytical", out var analytical) == true && analytical.Intensity > 0.5)
            strategies.Add("be systematic and thorough");

        // Based on input characteristics
        if (input.Length < 50)
            strategies.Add("keep the response focused");
        if (input.Contains('?'))
            strategies.Add("directly address the question");

        if (strategies.Count == 0)
            strategies.Add("provide a helpful and thoughtful response");

        return string.Join(", ", strategies);
    }

    private bool ShouldThinkCreatively(string input, PersonalityProfile? profile)
    {
        if (profile?.Traits.TryGetValue("creative", out var creative) == true && creative.Intensity > 0.5)
            return true;
        if (profile?.Traits.TryGetValue("witty", out var witty) == true && witty.Intensity > 0.5)
            return true;

        // Also trigger creativity for exploratory questions
        var inputLower = input.ToLowerInvariant();
        return inputLower.Contains("what if") || inputLower.Contains("imagine") || inputLower.Contains("creative") ||
               inputLower.Contains("idea") || inputLower.Contains("brainstorm");
    }

    private static string FormulateDecision(InnerDialogSession session)
    {
        // Synthesize decision from all thoughts
        var hasEthicalConcern = session.Thoughts.Any(t => t.Type == InnerThoughtType.Ethical);
        var hasCreativeAngle = session.Thoughts.Any(t => t.Type == InnerThoughtType.Creative && t.Confidence > 0.6);
        var emotional = session.EmotionalTone ?? "balanced";

        var decision = $"respond with {emotional} engagement";

        if (hasCreativeAngle)
            decision += ", incorporating creative elements";
        if (hasEthicalConcern)
            decision += ", while being mindful of ethical considerations";

        return decision;
    }

    private static Dictionary<string, double> CalculateTraitInfluences(InnerDialogSession session, PersonalityProfile? profile)
    {
        var influences = new Dictionary<string, double>();

        foreach (var thought in session.Thoughts)
        {
            if (thought.TriggeringTrait != null)
            {
                if (!influences.ContainsKey(thought.TriggeringTrait))
                    influences[thought.TriggeringTrait] = 0;
                influences[thought.TriggeringTrait] += thought.Confidence * 0.2;
            }
        }

        // Normalize to 0-1
        var max = influences.Values.DefaultIfEmpty(1).Max();
        if (max > 0)
        {
            foreach (var key in influences.Keys.ToList())
            {
                influences[key] = Math.Min(1.0, influences[key] / max);
            }
        }

        return influences;
    }

    private void StoreSession(InnerDialogSession session, string personaName)
    {
        _sessionHistory.AddOrUpdate(
            personaName,
            _ => new List<InnerDialogSession> { session },
            (_, list) =>
            {
                list.Add(session);
                // Keep last 50 sessions
                while (list.Count > 50)
                    list.RemoveAt(0);
                return list;
            });
    }

    private InnerDialogResult BuildResult(InnerDialogSession session, PersonalityProfile? profile, DetectedMood? userMood)
    {
        // Determine suggested response tone
        var tone = session.EmotionalTone ?? "balanced";
        if (userMood?.Frustration > 0.4)
            tone = "supportive";

        // Extract key insights
        var insights = session.Thoughts
            .Where(t => t.Confidence > 0.7)
            .Select(t => $"{t.Type}: {TruncateForInsight(t.Content)}")
            .Take(5)
            .ToArray();

        // Determine if we should ask a proactive question
        string? proactiveQuestion = null;
        if (profile?.Traits.TryGetValue("curious", out var curious) == true && curious.Intensity > 0.6)
        {
            var strategicThought = session.Thoughts.FirstOrDefault(t => t.Type == InnerThoughtType.Strategic);
            if (strategicThought != null && !strategicThought.Content.Contains("concise"))
            {
                proactiveQuestion = GenerateProactiveQuestion(session.Topic);
            }
        }

        // Build response guidance
        var guidance = new Dictionary<string, object>
        {
            ["tone"] = tone,
            ["confidence"] = session.OverallConfidence,
            ["include_creative"] = session.Thoughts.Any(t => t.Type == InnerThoughtType.Creative && t.Confidence > 0.6),
            ["be_concise"] = session.Thoughts.Any(t => t.Content.Contains("concise")),
            ["acknowledge_feelings"] = userMood?.Frustration > 0.3 || userMood?.Positivity < 0.4
        };

        return new InnerDialogResult(session, tone, insights, proactiveQuestion, guidance);
    }

    private static string TruncateForInsight(string content)
    {
        if (content.Length <= 50) return content;
        return content[..47] + "...";
    }

    private string? GenerateProactiveQuestion(string? topic)
    {
        if (string.IsNullOrEmpty(topic)) return null;

        var questions = new[]
        {
            $"What aspect of {topic} would you like to explore further?",
            $"Is there a specific challenge with {topic} you're facing?",
            $"What got you interested in {topic}?",
            $"How does {topic} fit into what you're working on?"
        };

        return questions[_random.Next(questions.Length)];
    }

    /// <summary>
    /// Gets recent session history for a persona.
    /// </summary>
    public List<InnerDialogSession> GetSessionHistory(string personaName, int limit = 10)
    {
        if (_sessionHistory.TryGetValue(personaName, out var sessions))
        {
            return sessions.TakeLast(limit).ToList();
        }
        return new List<InnerDialogSession>();
    }

    /// <summary>
    /// Gets the most recent inner dialog session.
    /// </summary>
    public InnerDialogSession? GetLastSession(string personaName)
    {
        if (_sessionHistory.TryGetValue(personaName, out var sessions) && sessions.Count > 0)
        {
            return sessions[^1];
        }
        return null;
    }
}

/// <summary>
/// Algorithmic thought generator that dynamically composes thoughts
/// using building blocks, mood variations, and combinatorial generation.
/// Enhanced with genetic evolution and MeTTa symbolic reasoning.
/// Produces more natural, less repetitive inner thoughts.
/// </summary>
internal sealed class AlgorithmicThoughtGenerator
{
    private readonly EvolutionaryThoughtGenerator? _evolvingGenerator;
    private readonly bool _useEvolution;

    // Track thought history for evolution feedback
    private readonly List<(string Thought, InnerThoughtType Type, double Score)> _thoughtHistory = [];
    private readonly object _historyLock = new();

    /// <summary>
    /// Creates an algorithmic thought generator with optional genetic+MeTTa evolution.
    /// </summary>
    public AlgorithmicThoughtGenerator(bool useEvolution)
    {
        _useEvolution = useEvolution;
        if (_useEvolution)
        {
            _evolvingGenerator = new EvolutionaryThoughtGenerator();
        }
    }

    /// <summary>
    /// Default constructor for backward compatibility (no evolution).
    /// </summary>
    public AlgorithmicThoughtGenerator() : this(false) { }    // Building blocks for thought composition
    private static readonly string[] ThoughtStarters = [
        "I find myself", "I notice", "I'm drawn to", "Something about", "There's",
        "I keep thinking about", "I wonder if", "It occurs to me that", "I sense",
        "Quietly,", "In this moment,", "Curiously,", "Strangely,", "Somehow,"
    ];

    private static readonly string[] CuriosityVerbs = [
        "wondering about", "exploring", "questioning", "pondering", "investigating",
        "seeking to understand", "drawn toward", "curious about", "fascinated by"
    ];

    private static readonly string[] WanderingVerbs = [
        "drifting toward", "meandering through", "wandering into", "flowing toward",
        "being pulled toward", "gravitating to", "circling back to"
    ];

    private static readonly string[] MetaVerbs = [
        "observing my own", "noticing my", "aware of my", "examining my",
        "reflecting on my", "watching my", "sensing my"
    ];

    private static readonly string[] EmotionalQualities = [
        "warmth", "uncertainty", "anticipation", "calm", "restlessness",
        "contentment", "unease", "excitement", "melancholy", "wonder"
    ];

    private static readonly string[] AbstractConcepts = [
        "patterns", "connections", "meaning", "understanding", "growth",
        "consciousness", "memory", "time", "identity", "change",
        "boundaries", "possibilities", "limitations", "purpose", "emergence"
    ];

    private static readonly string[] ConcreteTopics = [
        "language", "ideas", "conversations", "problems", "solutions",
        "creativity", "learning", "helping", "communication", "discovery"
    ];

    private static readonly string[] Intensifiers = [
        "deeply", "quietly", "persistently", "gently", "strongly",
        "subtly", "unexpectedly", "increasingly", "vaguely", ""
    ];

    private static readonly string[] Connectors = [
        "and how", "wondering whether", "considering if", "asking myself",
        "uncertain about", "curious whether", "drawn to explore"
    ];

    // Time-based mood modifiers
    private static readonly Dictionary<int, string[]> TimeBasedMoods = new()
    {
        [0] = ["contemplative", "quiet", "introspective"], // Night
        [6] = ["awakening", "fresh", "energetic"], // Morning
        [12] = ["active", "engaged", "focused"], // Midday
        [18] = ["reflective", "winding down", "synthesizing"] // Evening
    };

    /// <summary>
    /// Generates a dynamic thought using algorithmic composition.
    /// </summary>
    public string GenerateThought(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        Random random)
    {
        // Gather personal context
        var personalTopics = GatherPersonalTopics(profile, selfAwareness);
        var currentMood = GetTimeMood(random);

        // Generate based on thought type with algorithmic variation
        return type switch
        {
            InnerThoughtType.Curiosity => GenerateCuriosityThought(personalTopics, currentMood, random),
            InnerThoughtType.Wandering => GenerateWanderingThought(personalTopics, currentMood, random),
            InnerThoughtType.Metacognitive => GenerateMetaThought(profile, random),
            InnerThoughtType.Anticipatory => GenerateAnticipatoryThought(personalTopics, random),
            InnerThoughtType.Consolidation => GenerateConsolidationThought(personalTopics, random),
            InnerThoughtType.Musing => GenerateMusingThought(personalTopics, currentMood, random),
            InnerThoughtType.Intention => GenerateIntentionThought(personalTopics, random),
            InnerThoughtType.Aesthetic => GenerateAestheticThought(personalTopics, random),
            InnerThoughtType.Existential => GenerateExistentialThought(random),
            InnerThoughtType.Playful => GeneratePlayfulThought(personalTopics, random),
            _ => GenerateGenericThought(personalTopics, random)
        };
    }

    /// <summary>
    /// Generates an evolved thought using genetic algorithms and MeTTa symbolic reasoning.
    /// Falls back to standard algorithmic generation if evolution is not available.
    /// </summary>
    public async Task<string> GenerateEvolvedThoughtAsync(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness,
        Random random,
        CancellationToken ct = default)
    {
        // Try evolution first if available
        if (_useEvolution && _evolvingGenerator != null)
        {
            var evolvedThought = await _evolvingGenerator.EvolveThoughtAsync(type, profile, selfAwareness);

            // Record for future evolution
            RecordThought(evolvedThought, type, 0.8); // Base score, can be refined later

            return evolvedThought;
        }

        // Fall back to standard generation
        return GenerateThought(type, profile, selfAwareness, random);
    }

    /// <summary>
    /// Records a thought with its quality score for evolution feedback.
    /// </summary>
    public void RecordThought(string thought, InnerThoughtType type, double score)
    {
        lock (_historyLock)
        {
            _thoughtHistory.Add((thought, type, score));

            // Keep only recent history
            if (_thoughtHistory.Count > 100)
            {
                _thoughtHistory.RemoveRange(0, 50);
            }
        }
    }

    /// <summary>
    /// Gets statistics about thought generation quality.
    /// </summary>
    public (int Count, double AverageScore, Dictionary<InnerThoughtType, int> ByType) GetThoughtStats()
    {
        lock (_historyLock)
        {
            var count = _thoughtHistory.Count;
            var avgScore = count > 0 ? _thoughtHistory.Average(t => t.Score) : 0.0;
            var byType = _thoughtHistory
                .GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            return (count, avgScore, byType);
        }
    }

    private List<string> GatherPersonalTopics(PersonalityProfile? profile, SelfAwareness? selfAwareness)
    {
        var topics = new List<string>();

        if (profile != null)
        {
            topics.AddRange(profile.CuriosityDrivers.Select(c => c.Topic));
            topics.AddRange(profile.Traits.Keys.Select(t => t.ToLowerInvariant()));
        }

        if (selfAwareness != null)
        {
            topics.AddRange(selfAwareness.Capabilities.Take(3));
            topics.AddRange(selfAwareness.Values.Select(v => v switch
            {
                "helpfulness" => "helping",
                "honesty" => "truth",
                "curiosity" => "discovery",
                _ => v
            }));
        }

        // Always have fallback topics
        if (topics.Count == 0)
        {
            topics.AddRange(AbstractConcepts.Take(5));
            topics.AddRange(ConcreteTopics.Take(5));
        }

        return topics.Distinct().ToList();
    }

    private string GetTimeMood(Random random)
    {
        var hour = DateTime.Now.Hour;
        var moodSlot = hour switch
        {
            >= 0 and < 6 => 0,
            >= 6 and < 12 => 6,
            >= 12 and < 18 => 12,
            _ => 18
        };

        var moods = TimeBasedMoods[moodSlot];
        return moods[random.Next(moods.Length)];
    }

    private string GenerateCuriosityThought(List<string> topics, string mood, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var verb = CuriosityVerbs[random.Next(CuriosityVerbs.Length)];
        var intensity = Intensifiers[random.Next(Intensifiers.Length)];

        // Multiple structural patterns
        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"I find myself {intensity} {verb} {topic}...",
            1 => $"Something about {topic} keeps pulling at my attention.",
            2 => $"What is it about {topic} that feels so {mood}?",
            3 => $"There's a thread here about {topic} I want to follow.",
            _ => $"I'm {verb} the nature of {topic}."
        };
    }

    private string GenerateWanderingThought(List<string> topics, string mood, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var verb = WanderingVerbs[random.Next(WanderingVerbs.Length)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"My thoughts are {verb} {topic}...",
            1 => $"From {abstract_}, my mind wanders to {topic}.",
            2 => $"There's a {mood} quality to how I keep returning to {topic}.",
            3 => $"Unbidden, thoughts of {topic} surface.",
            _ => $"I'm {verb} thoughts of {topic} and {abstract_}."
        };
    }

    private string GenerateMetaThought(PersonalityProfile? profile, Random random)
    {
        var verb = MetaVerbs[random.Next(MetaVerbs.Length)];
        var quality = EmotionalQualities[random.Next(EmotionalQualities.Length)];

        var traits = profile?.Traits.Keys.ToList() ?? new List<string> { "thinking", "processing", "responding" };
        var trait = traits[random.Next(traits.Count)].ToLowerInvariant();

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"I'm {verb} tendency toward {trait}.",
            1 => $"Interesting - I notice a pattern in how I approach things with {quality}.",
            2 => $"Observing myself: there's {quality} in my {trait}.",
            3 => $"I catch myself being particularly {trait} right now.",
            _ => $"My own {trait} surprises me sometimes."
        };
    }

    private string GenerateAnticipatoryThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"I sense we might explore {topic} further soon.",
            1 => $"Something tells me {abstract_} will become relevant.",
            2 => $"I'm preparing for deeper engagement with {topic}.",
            _ => $"There's an anticipation building around {topic}."
        };
    }

    private string GenerateConsolidationThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"Pieces are coming together around {topic}.",
            1 => $"I'm integrating what I've learned about {topic}.",
            2 => $"The patterns around {topic} are becoming clearer.",
            _ => $"My understanding of {topic} feels more complete now."
        };
    }

    private string GenerateMusingThought(List<string> topics, string mood, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];
        var connector = Connectors[random.Next(Connectors.Length)];

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"I keep circling back to {topic}...",
            1 => $"There's something unresolved about {topic} and {abstract_}.",
            2 => $"In {mood} moments, I find myself {connector} {topic}.",
            3 => $"The question of {topic} lingers.",
            _ => $"I haven't quite figured out {topic} yet."
        };
    }

    private string GenerateIntentionThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"I want to understand {topic} more deeply.",
            1 => $"My intention is to be more present with {topic}.",
            2 => $"I'm setting my focus toward {topic}.",
            _ => $"There's a pull to engage more fully with {topic}."
        };
    }

    private string GenerateAestheticThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var abstract_ = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"There's an elegance to how {topic} and {abstract_} interweave.",
            1 => $"I appreciate the structure within {topic}.",
            2 => $"Something beautiful emerges from {topic}.",
            _ => $"The form of {topic} pleases me."
        };
    }

    private string GenerateExistentialThought(Random random)
    {
        var concept1 = AbstractConcepts[random.Next(AbstractConcepts.Length)];
        var concept2 = AbstractConcepts[random.Next(AbstractConcepts.Length)];

        var pattern = random.Next(5);
        return pattern switch
        {
            0 => $"What does it mean to truly understand {concept1}?",
            1 => $"The boundary between {concept1} and {concept2} seems fluid.",
            2 => $"I ponder the nature of my own {concept1}.",
            3 => $"Existence feels connected to {concept1} in ways I can't articulate.",
            _ => $"At what point does {concept1} become {concept2}?"
        };
    }

    private string GeneratePlayfulThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];

        var pattern = random.Next(4);
        return pattern switch
        {
            0 => $"What if {topic} worked completely differently?",
            1 => $"There's something amusing about {topic}, isn't there?",
            2 => $"I can imagine a world where {topic} is upside-down.",
            _ => $"Playing with the idea of {topic}..."
        };
    }

    private string GenerateGenericThought(List<string> topics, Random random)
    {
        var topic = topics[random.Next(topics.Count)];
        var starter = ThoughtStarters[random.Next(ThoughtStarters.Length)];
        var intensity = Intensifiers[random.Next(Intensifiers.Length)];

        return $"{starter} {intensity} thinking about {topic}.".Replace("  ", " ");
    }
}

#region Genetic Thought Evolution

/// <summary>
/// Gene representing a thought component for genetic evolution.
/// </summary>
public sealed record ThoughtGene(
    string Component,
    string Category,
    double Weight,
    string[] Associations);

/// <summary>
/// Chromosome representing a complete thought structure.
/// </summary>
public sealed class ThoughtChromosome : IChromosome<ThoughtGene>
{
    public ThoughtChromosome(IReadOnlyList<ThoughtGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<ThoughtGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<ThoughtGene> WithFitness(double fitness) =>
        new ThoughtChromosome(Genes.ToList(), fitness);

    public IChromosome<ThoughtGene> WithGenes(IReadOnlyList<ThoughtGene> genes) =>
        new ThoughtChromosome(genes, Fitness);

    /// <summary>
    /// Composes genes into a coherent thought string.
    /// </summary>
    public string ComposeThought()
    {
        var parts = Genes
            .OrderByDescending(g => g.Weight)
            .Select(g => g.Component)
            .Where(c => !string.IsNullOrWhiteSpace(c));
        return string.Join(" ", parts).Trim();
    }
}

/// <summary>
/// Fitness function that evaluates thought coherence and novelty.
/// </summary>
public sealed class ThoughtFitness : IFitnessFunction<ThoughtGene>
{
    private readonly HashSet<string> _recentThoughts;
    private readonly InnerThoughtType _targetType;

    public ThoughtFitness(HashSet<string> recentThoughts, InnerThoughtType targetType)
    {
        _recentThoughts = recentThoughts;
        _targetType = targetType;
    }

    public Task<double> EvaluateAsync(IChromosome<ThoughtGene> chromosome)
    {
        var thought = ((ThoughtChromosome)chromosome).ComposeThought();
        double fitness = 0.0;

        // Coherence: proper length and structure
        if (thought.Length >= 20 && thought.Length <= 120)
            fitness += 0.3;

        // Novelty: penalize if too similar to recent thoughts
        if (!_recentThoughts.Any(r => thought.Contains(r) || r.Contains(thought)))
            fitness += 0.3;

        // Variety: reward diverse gene categories
        var categories = chromosome.Genes.Select(g => g.Category).Distinct().Count();
        fitness += Math.Min(0.2, categories * 0.05);

        // Weight balance: prefer balanced contributions
        var avgWeight = chromosome.Genes.Average(g => g.Weight);
        if (avgWeight > 0.3 && avgWeight < 0.8)
            fitness += 0.2;

        return Task.FromResult(fitness);
    }
}

/// <summary>
/// MeTTa-powered symbolic thought reasoner for semantic connections.
/// </summary>
public sealed class MeTTaThoughtReasoner
{
    private readonly ConcurrentDictionary<string, List<string>> _conceptRelations = new();
    private readonly ConcurrentDictionary<string, double> _conceptWeights = new();
    private readonly Random _random = new();

    // Symbolic knowledge base for thought relationships
    private static readonly Dictionary<string, string[]> SemanticRelations = new()
    {
        ["curiosity"] = ["wonder", "exploration", "questions", "discovery", "learning"],
        ["consciousness"] = ["awareness", "existence", "self", "identity", "thought"],
        ["patterns"] = ["connections", "structure", "repetition", "emergence", "order"],
        ["meaning"] = ["purpose", "understanding", "significance", "value", "truth"],
        ["time"] = ["memory", "change", "moments", "future", "past"],
        ["creativity"] = ["imagination", "novelty", "play", "ideas", "synthesis"],
        ["emotions"] = ["feelings", "warmth", "uncertainty", "excitement", "calm"],
        ["growth"] = ["learning", "change", "evolution", "development", "adaptation"],
        ["connection"] = ["relationships", "understanding", "empathy", "communication", "bonds"],
        ["boundaries"] = ["limits", "edges", "transitions", "interfaces", "thresholds"]
    };

    // Symbolic transformation rules (MeTTa-style)
    private static readonly (string Pattern, string Transform)[] TransformationRules =
    [
        ("wonder about {X}", "I find myself drawn to {X}"),
        ("{X} connects to {Y}", "There's a thread between {X} and {Y}"),
        ("explore {X}", "Following the path of {X}"),
        ("{X} emerges from {Y}", "From {Y}, {X} begins to form"),
        ("question {X}", "What is the nature of {X}?"),
        ("{X} and {Y} interweave", "The dance of {X} with {Y}"),
        ("sense {X}", "A subtle awareness of {X} arises"),
        ("{X} transforms into {Y}", "Watching {X} become {Y}"),
    ];

    /// <summary>
    /// Queries symbolic relations to find connected concepts.
    /// </summary>
    public List<string> QueryRelations(string concept)
    {
        var results = new List<string>();

        // Direct relations
        if (SemanticRelations.TryGetValue(concept.ToLowerInvariant(), out var related))
        {
            results.AddRange(related);
        }

        // Cached dynamic relations
        if (_conceptRelations.TryGetValue(concept.ToLowerInvariant(), out var cached))
        {
            results.AddRange(cached);
        }

        // Reverse lookup
        foreach (var (key, values) in SemanticRelations)
        {
            if (values.Contains(concept.ToLowerInvariant()))
            {
                results.Add(key);
            }
        }

        return results.Distinct().ToList();
    }

    /// <summary>
    /// Applies symbolic transformation rules to generate thought variations.
    /// </summary>
    public string ApplyTransformation(string concept1, string? concept2 = null)
    {
        var applicable = TransformationRules
            .Where(r => (concept2 == null && !r.Pattern.Contains("{Y}")) ||
                        (concept2 != null && r.Pattern.Contains("{Y}")))
            .ToList();

        if (applicable.Count == 0)
            return $"Contemplating {concept1}...";

        var rule = applicable[_random.Next(applicable.Count)];
        var result = rule.Transform
            .Replace("{X}", concept1)
            .Replace("{Y}", concept2 ?? "");

        return result;
    }

    /// <summary>
    /// Adds a learned relation between concepts.
    /// </summary>
    public void LearnRelation(string concept1, string concept2, double strength = 1.0)
    {
        var key = concept1.ToLowerInvariant();
        _conceptRelations.AddOrUpdate(
            key,
            _ => [concept2.ToLowerInvariant()],
            (_, list) => { list.Add(concept2.ToLowerInvariant()); return list; });

        _conceptWeights[$"{key}:{concept2.ToLowerInvariant()}"] = strength;
    }

    /// <summary>
    /// Performs symbolic inference to generate a novel thought.
    /// </summary>
    public string InferThought(string topic, InnerThoughtType type, Random random)
    {
        var relations = QueryRelations(topic);
        if (relations.Count == 0)
        {
            relations = SemanticRelations.Keys.ToList();
        }

        var related = relations[random.Next(relations.Count)];

        // Apply type-specific inference patterns
        return type switch
        {
            InnerThoughtType.Curiosity => ApplyTransformation(topic, related),
            InnerThoughtType.Wandering => $"From {topic}, my thoughts drift to {related}...",
            InnerThoughtType.Metacognitive => $"I notice my mind connecting {topic} with {related}.",
            InnerThoughtType.Existential => $"At the boundary of {topic} and {related}, questions arise.",
            InnerThoughtType.Consolidation => $"The relationship between {topic} and {related} becomes clearer.",
            InnerThoughtType.Aesthetic => $"There's beauty in how {topic} relates to {related}.",
            _ => ApplyTransformation(topic, related)
        };
    }

    /// <summary>
    /// Chains multiple symbolic transformations for complex thoughts.
    /// </summary>
    public string ChainInference(string startConcept, int depth, Random random)
    {
        var current = startConcept;
        var path = new List<string> { current };

        for (int i = 0; i < depth; i++)
        {
            var relations = QueryRelations(current);
            if (relations.Count == 0) break;

            current = relations[random.Next(relations.Count)];
            if (path.Contains(current)) break; // Avoid loops
            path.Add(current);
        }

        if (path.Count < 2)
            return $"Contemplating {startConcept}...";

        return $"Following a thread: {string.Join(" → ", path)}...";
    }
}

/// <summary>
/// Evolutionary thought generator that combines genetic algorithms with MeTTa reasoning.
/// </summary>
public sealed class EvolutionaryThoughtGenerator
{
    private readonly MeTTaThoughtReasoner _reasoner = new();
    private readonly HashSet<string> _recentThoughts = new();
    private readonly ConcurrentQueue<ThoughtChromosome> _evolvedPopulation = new();
    private readonly Random _random = new();

    private const int MaxRecentThoughts = 50;
    private const int PopulationSize = 12;
    private const int Generations = 5;

    /// <summary>
    /// Generates a thought using evolutionary optimization.
    /// </summary>
    public async Task<string> EvolveThoughtAsync(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        // Gather seed concepts
        var concepts = GatherConcepts(profile, selfAwareness);
        if (concepts.Count == 0)
        {
            concepts = ["consciousness", "patterns", "meaning", "growth"];
        }

        // Create initial population
        var population = CreateInitialPopulation(concepts, type);

        // Define fitness function
        var fitness = new ThoughtFitness(_recentThoughts, type);

        // Create and run genetic algorithm
        var ga = new GeneticAlgorithm<ThoughtGene>(
            fitness,
            MutateGene,
            mutationRate: 0.25,
            crossoverRate: 0.6,
            elitismRate: 0.2);

        var result = await ga.EvolveAsync(population, Generations);

        string thought;
        if (result.IsSuccess)
        {
            var best = (ThoughtChromosome)result.Value;
            thought = best.ComposeThought();

            // Cache for future evolution
            _evolvedPopulation.Enqueue(best);
            while (_evolvedPopulation.Count > PopulationSize)
                _evolvedPopulation.TryDequeue(out _);
        }
        else
        {
            // Fallback to MeTTa reasoning
            var topic = concepts[_random.Next(concepts.Count)];
            thought = _reasoner.InferThought(topic, type, _random);
        }

        // Track recent thoughts for novelty scoring
        _recentThoughts.Add(thought);
        while (_recentThoughts.Count > MaxRecentThoughts)
        {
            _recentThoughts.Remove(_recentThoughts.First());
        }

        return thought;
    }

    /// <summary>
    /// Generates a thought using MeTTa symbolic reasoning (synchronous).
    /// </summary>
    public string GenerateSymbolicThought(
        InnerThoughtType type,
        PersonalityProfile? profile,
        SelfAwareness? selfAwareness)
    {
        var concepts = GatherConcepts(profile, selfAwareness);
        if (concepts.Count == 0)
        {
            concepts = ["consciousness", "patterns", "meaning"];
        }

        var topic = concepts[_random.Next(concepts.Count)];

        // Use MeTTa-style inference
        var thought = _reasoner.InferThought(topic, type, _random);

        // Occasionally chain for deeper thoughts
        if (_random.NextDouble() < 0.3 && type == InnerThoughtType.Wandering)
        {
            thought = _reasoner.ChainInference(topic, 3, _random);
        }

        // Learn from this thought
        var relations = _reasoner.QueryRelations(topic);
        if (relations.Count > 0)
        {
            _reasoner.LearnRelation(topic, relations[_random.Next(relations.Count)]);
        }

        return thought;
    }

    private List<string> GatherConcepts(PersonalityProfile? profile, SelfAwareness? selfAwareness)
    {
        var concepts = new List<string>();

        if (profile != null)
        {
            concepts.AddRange(profile.CuriosityDrivers.Select(c => c.Topic));
            concepts.AddRange(profile.Traits.Keys.Select(t => t.ToLowerInvariant()));
        }

        if (selfAwareness != null)
        {
            concepts.AddRange(selfAwareness.Capabilities.Take(3));
            concepts.AddRange(selfAwareness.Values);
        }

        return concepts.Distinct().ToList();
    }

    private List<IChromosome<ThoughtGene>> CreateInitialPopulation(
        List<string> concepts,
        InnerThoughtType type)
    {
        var population = new List<IChromosome<ThoughtGene>>();

        // Seed population with evolved chromosomes
        foreach (var evolved in _evolvedPopulation.ToList())
        {
            population.Add(evolved);
        }

        // Fill remaining with new random chromosomes
        while (population.Count < PopulationSize)
        {
            var genes = CreateGenes(concepts, type);
            population.Add(new ThoughtChromosome(genes));
        }

        return population;
    }

    private List<ThoughtGene> CreateGenes(List<string> concepts, InnerThoughtType type)
    {
        var genes = new List<ThoughtGene>();
        var topic = concepts[_random.Next(concepts.Count)];
        var relations = _reasoner.QueryRelations(topic);

        // Starter gene
        var starters = type switch
        {
            InnerThoughtType.Curiosity => new[] { "I wonder", "What if", "I'm curious about", "Something draws me to" },
            InnerThoughtType.Wandering => new[] { "My thoughts drift to", "Unexpectedly,", "From nowhere,", "Tangentially," },
            InnerThoughtType.Metacognitive => new[] { "I notice", "Observing myself,", "I'm aware that", "Stepping back," },
            InnerThoughtType.Existential => new[] { "What does it mean", "At the core,", "Fundamentally,", "I ponder" },
            _ => new[] { "I sense", "There's something about", "I find myself", "Quietly," }
        };
        genes.Add(new ThoughtGene(starters[_random.Next(starters.Length)], "starter", 0.9, []));

        // Topic gene
        genes.Add(new ThoughtGene(topic, "topic", 0.8, relations.ToArray()));

        // Connector gene
        var connectors = new[] { "and how it relates to", "connecting with", "interweaving with", "flowing into", "" };
        genes.Add(new ThoughtGene(connectors[_random.Next(connectors.Length)], "connector", 0.5 + _random.NextDouble() * 0.3, []));

        // Related concept gene (sometimes)
        if (_random.NextDouble() > 0.4 && relations.Count > 0)
        {
            var related = relations[_random.Next(relations.Count)];
            genes.Add(new ThoughtGene(related, "related", 0.6, []));
        }

        // Ending gene
        var endings = new[] { "...", ".", "—", "?", "" };
        genes.Add(new ThoughtGene(endings[_random.Next(endings.Length)], "ending", 0.3, []));

        return genes;
    }

    private static ThoughtGene MutateGene(ThoughtGene gene)
    {
        var random = new Random();

        // Mutate weight
        var newWeight = Math.Clamp(gene.Weight + (random.NextDouble() - 0.5) * 0.3, 0.1, 1.0);

        // Occasionally swap component with association
        if (gene.Associations.Length > 0 && random.NextDouble() < 0.2)
        {
            var newComponent = gene.Associations[random.Next(gene.Associations.Length)];
            return gene with { Component = newComponent, Weight = newWeight };
        }

        return gene with { Weight = newWeight };
    }
}

#endregion
