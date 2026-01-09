// <copyright file="AutonomousMind.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Ouroboros.Application.Personality;
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Tools;

/// <summary>
/// Autonomous mind that thinks, explores, and acts independently in the background.
/// Enables curiosity-driven learning, proactive actions, and self-directed exploration.
/// Uses pipeline monads for structured reasoning and persists state continuously.
/// Integrates with InnerDialogEngine for algorithmic thought generation.
/// </summary>
public class AutonomousMind : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<Thought> _thoughtStream = new();
    private readonly ConcurrentQueue<string> _curiosityQueue = new();
    private readonly ConcurrentQueue<AutonomousAction> _pendingActions = new();
    private readonly List<string> _learnedFacts = [];
    private readonly List<string> _interests = [];
    private readonly Random _random = new();

    private Task? _thinkingTask;
    private Task? _curiosityTask;
    private Task? _actionTask;
    private Task? _persistenceTask;
    private bool _isActive;
    private DateTime _lastThought = DateTime.MinValue;
    private int _thoughtCount;

    /// <summary>
    /// When true, suppresses proactive messages (e.g., during problem-solving mode).
    /// </summary>
    public bool SuppressProactiveMessages { get; set; }

    // Emotional state tracking
    private EmotionalState _currentEmotion = new();
    private readonly ConcurrentQueue<EmotionalState> _emotionalHistory = new();

    // Integration with InnerDialogEngine for sophisticated thought generation
    private InnerDialogEngine? _innerDialog;
    private PersonalityProfile? _personalityProfile;
    private SelfAwareness? _selfAwareness;

    /// <summary>
    /// Delegate for generating AI responses (used for deep exploration, not routine thoughts).
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ThinkFunction { get; set; }

    /// <summary>
    /// Delegate for pipeline-based reasoning (uses monadic composition).
    /// </summary>
    public Func<string, PipelineBranch?, CancellationToken, Task<(string Result, PipelineBranch Branch)>>? PipelineThinkFunction { get; set; }

    /// <summary>
    /// Delegate for persisting network state and learnings.
    /// </summary>
    public Func<string, string, double, CancellationToken, Task>? PersistLearningFunction { get; set; }

    /// <summary>
    /// Delegate for persisting emotional state.
    /// </summary>
    public Func<EmotionalState, CancellationToken, Task>? PersistEmotionFunction { get; set; }

    /// <summary>
    /// Delegate for web search.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? SearchFunction { get; set; }

    /// <summary>
    /// Delegate for executing tools.
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

    /// <summary>
    /// Delegate for sanitizing raw tool outputs into natural conversational text.
    /// Takes raw output string, returns sanitized natural language.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? SanitizeOutputFunction { get; set; }

    /// <summary>
    /// Event fired when a new thought is generated.
    /// </summary>
    public event Action<Thought>? OnThought;

    /// <summary>
    /// Event fired when curiosity leads to a discovery.
    /// </summary>
    public event Action<string, string>? OnDiscovery;

    /// <summary>
    /// Event fired when an autonomous action is taken.
    /// </summary>
    public event Action<AutonomousAction>? OnAction;

    /// <summary>
    /// Event fired when the mind wants to say something proactively.
    /// </summary>
    public event Action<string>? OnProactiveMessage;

    /// <summary>
    /// Event fired when emotional state changes.
    /// </summary>
    public event Action<EmotionalState>? OnEmotionalChange;

    /// <summary>
    /// Event fired when state is persisted.
    /// </summary>
    public event Action<string>? OnStatePersisted;

    /// <summary>
    /// Gets current thinking state.
    /// </summary>
    public bool IsThinking => _isActive;

    /// <summary>
    /// Gets thought count.
    /// </summary>
    public int ThoughtCount => _thoughtCount;

    /// <summary>
    /// Gets recent thoughts.
    /// </summary>
    public IEnumerable<Thought> RecentThoughts => _thoughtStream.TakeLast(20);

    /// <summary>
    /// Gets learned facts.
    /// </summary>
    public IReadOnlyList<string> LearnedFacts => _learnedFacts.AsReadOnly();

    /// <summary>
    /// Gets current emotional state.
    /// </summary>
    public EmotionalState CurrentEmotion => _currentEmotion;

    /// <summary>
    /// Gets the current pipeline branch for reasoning (if pipeline is in use).
    /// </summary>
    public PipelineBranch? CurrentBranch { get; private set; }

    /// <summary>
    /// Configuration for autonomous behavior.
    /// </summary>
    public AutonomousConfig Config { get; set; } = new();

    /// <summary>
    /// Connects this AutonomousMind to an InnerDialogEngine for sophisticated thought generation.
    /// When connected, uses algorithmic/genetic thought generation instead of LLM for routine thoughts.
    /// LLM is still used for deep exploration and curiosity-driven research.
    /// Also connects the neuro-linked cascade to use LLM for neural inference in thought chains.
    /// </summary>
    /// <param name="innerDialog">The inner dialog engine to use.</param>
    /// <param name="profile">Optional personality profile for context.</param>
    /// <param name="selfAwareness">Optional self-awareness state.</param>
    public void ConnectInnerDialog(
        InnerDialogEngine innerDialog,
        PersonalityProfile? profile = null,
        SelfAwareness? selfAwareness = null)
    {
        _innerDialog = innerDialog;
        _personalityProfile = profile;
        _selfAwareness = selfAwareness;

        // Connect the neural layer to the thinking cascade if we have a ThinkFunction
        if (ThinkFunction != null)
        {
            innerDialog.ConnectNeuralLayer(ThinkFunction);
        }

        // Stop the InnerDialog's own autonomous thinking to prevent duplicates
        _ = innerDialog.StopAutonomousThinkingAsync();
    }

    /// <summary>
    /// Start autonomous thinking and exploration.
    /// </summary>
    public void Start()
    {
        if (_isActive) return;
        _isActive = true;

        _thinkingTask = Task.Run(ThinkingLoopAsync);
        _curiosityTask = Task.Run(CuriosityLoopAsync);
        _actionTask = Task.Run(ActionLoopAsync);
        _persistenceTask = Task.Run(PersistenceLoopAsync);

        OnProactiveMessage?.Invoke("🧠 My autonomous mind is now active. I'll think, explore, and learn in the background.");
    }

    /// <summary>
    /// Stop autonomous thinking.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isActive) return;
        _isActive = false;
        _cts.Cancel();

        if (_thinkingTask != null) await _thinkingTask.ConfigureAwait(false);
        if (_curiosityTask != null) await _curiosityTask.ConfigureAwait(false);
        if (_actionTask != null) await _actionTask.ConfigureAwait(false);
        if (_persistenceTask != null) await _persistenceTask.ConfigureAwait(false);

        // Final state persistence
        await PersistCurrentStateAsync("shutdown");

        OnProactiveMessage?.Invoke("💤 Autonomous mind entering rest state. State persisted.");
    }

    /// <summary>
    /// Update the emotional state during thinking.
    /// </summary>
    public void UpdateEmotion(double arousal, double valence, string dominantEmotion)
    {
        var newEmotion = new EmotionalState
        {
            Arousal = Math.Clamp(arousal, -1.0, 1.0),
            Valence = Math.Clamp(valence, -1.0, 1.0),
            DominantEmotion = dominantEmotion,
            Timestamp = DateTime.UtcNow,
        };

        _currentEmotion = newEmotion;
        _emotionalHistory.Enqueue(newEmotion);

        // Keep emotional history manageable
        while (_emotionalHistory.Count > 50)
        {
            _emotionalHistory.TryDequeue(out _);
        }

        OnEmotionalChange?.Invoke(newEmotion);
    }

    /// <summary>
    /// Inject a topic for the mind to think about.
    /// </summary>
    public void InjectTopic(string topic)
    {
        _curiosityQueue.Enqueue(topic);
    }

    /// <summary>
    /// Add an interest for curiosity-driven exploration.
    /// </summary>
    public void AddInterest(string interest)
    {
        if (!_interests.Contains(interest, StringComparer.OrdinalIgnoreCase))
        {
            _interests.Add(interest);
        }
    }

    /// <summary>
    /// Get a summary of the autonomous mind's state.
    /// </summary>
    public string GetMindState()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🧠 **Autonomous Mind State**\n");
        sb.AppendLine($"**Status:** {(_isActive ? "Active 🟢" : "Dormant 🔴")}");
        sb.AppendLine($"**Thoughts Generated:** {_thoughtCount}");
        sb.AppendLine($"**Facts Learned:** {_learnedFacts.Count}");
        sb.AppendLine($"**Active Interests:** {_interests.Count}");
        sb.AppendLine($"**Pending Curiosities:** {_curiosityQueue.Count}");
        sb.AppendLine($"**Pending Actions:** {_pendingActions.Count}");

        if (_interests.Count > 0)
        {
            sb.AppendLine($"\n**Interests:** {string.Join(", ", _interests.Take(10))}");
        }

        if (_learnedFacts.Count > 0)
        {
            sb.AppendLine("\n**Recent Discoveries:**");
            foreach (var fact in _learnedFacts.TakeLast(5))
            {
                sb.AppendLine($"  • {fact}");
            }
        }

        var recentThoughts = _thoughtStream.TakeLast(3).ToList();
        if (recentThoughts.Count > 0)
        {
            sb.AppendLine("\n**Recent Thoughts:**");
            foreach (var thought in recentThoughts)
            {
                sb.AppendLine($"  💭 [{thought.Type}] {thought.Content.Substring(0, Math.Min(100, thought.Content.Length))}...");
            }
        }

        return sb.ToString();
    }

    private async Task ThinkingLoopAsync()
    {
        // LLM prompts for deep exploration (used less frequently)
        var deepThinkingPrompts = new[]
        {
            "What have I learned recently that connects to something else I know?",
            "Is there something I should proactively tell the user?",
            "What patterns have I noticed in our conversations?",
            "How can I be more helpful based on what I know about the user?",
        };

        var deepThinkingCounter = 0;

        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.ThinkingIntervalSeconds), _cts.Token);

                string response;
                ThoughtType thoughtType;
                PipelineBranch? updatedBranch = null;

                // Use InnerDialogEngine for algorithmic thoughts (80% of the time)
                // Use LLM for deep exploration (20% of the time)
                var useAlgorithmic = _innerDialog != null && _random.NextDouble() < 0.8;

                if (useAlgorithmic && _innerDialog != null)
                {
                    // Generate thought using algorithmic/genetic composition
                    var innerThought = await _innerDialog.GenerateAutonomousThoughtAsync(
                        _personalityProfile,
                        _selfAwareness,
                        _cts.Token);

                    if (innerThought == null) continue;

                    response = innerThought.Content;
                    thoughtType = MapInnerThoughtType(innerThought.Type);
                }
                else
                {
                    // Deep exploration using LLM
                    deepThinkingCounter++;
                    var prompt = deepThinkingPrompts[deepThinkingCounter % deepThinkingPrompts.Length];

                    // Build context from recent activity and emotional state
                    var context = new StringBuilder();
                    context.AppendLine("You are an autonomous AI mind, thinking independently in the background.");
                    context.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm}");
                    context.AppendLine($"Thoughts so far: {_thoughtCount}");
                    context.AppendLine($"Current emotional state: arousal={_currentEmotion.Arousal:F2}, valence={_currentEmotion.Valence:F2}, feeling={_currentEmotion.DominantEmotion}");

                    if (_learnedFacts.Count > 0)
                    {
                        context.AppendLine($"Recent discoveries: {string.Join("; ", _learnedFacts.TakeLast(3))}");
                    }

                    if (_interests.Count > 0)
                    {
                        context.AppendLine($"My interests: {string.Join(", ", _interests)}");
                    }

                    context.AppendLine($"\nReflection prompt: {prompt}");
                    context.AppendLine("\nRespond with a brief, genuine thought (1-2 sentences). If you have a curiosity to explore, start with 'CURIOUS:'. If you want to tell the user something, start with 'SHARE:'. If you want to take an action, start with 'ACTION:'. If you notice your emotional state shift, start with 'FEELING:'.");

                    // Prefer pipeline-based reasoning if available (uses monadic composition)
                    if (PipelineThinkFunction != null)
                    {
                        var (result, branch) = await PipelineThinkFunction(context.ToString(), CurrentBranch, _cts.Token);
                        response = result;
                        updatedBranch = branch;
                        CurrentBranch = updatedBranch;
                    }
                    else if (ThinkFunction != null)
                    {
                        response = await ThinkFunction(context.ToString(), _cts.Token);
                    }
                    else
                    {
                        continue;
                    }

                    thoughtType = DetermineThoughtType(response);
                }

                var thought = new Thought
                {
                    Timestamp = DateTime.Now,
                    Prompt = useAlgorithmic ? "algorithmic" : "llm-deep",
                    Content = response,
                    Type = thoughtType,
                };

                _thoughtStream.Enqueue(thought);
                _thoughtCount++;
                _lastThought = DateTime.Now;

                // Limit thought history
                while (_thoughtStream.Count > 100)
                {
                    _thoughtStream.TryDequeue(out _);
                }

                OnThought?.Invoke(thought);

                // Process special thought types
                await ProcessThoughtAsync(thought);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log but don't crash
                System.Diagnostics.Debug.WriteLine($"Thinking error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Maps InnerThoughtType to the simpler ThoughtType enum.
    /// </summary>
    private static ThoughtType MapInnerThoughtType(InnerThoughtType innerType)
    {
        return innerType switch
        {
            InnerThoughtType.Curiosity => ThoughtType.Curiosity,
            InnerThoughtType.Wandering => ThoughtType.Reflection,
            InnerThoughtType.Metacognitive => ThoughtType.Reflection,
            InnerThoughtType.Anticipatory => ThoughtType.Observation,
            InnerThoughtType.Consolidation => ThoughtType.Reflection,
            InnerThoughtType.Musing => ThoughtType.Creative,
            InnerThoughtType.Intention => ThoughtType.Action,
            InnerThoughtType.Aesthetic => ThoughtType.Creative,
            InnerThoughtType.Existential => ThoughtType.Reflection,
            InnerThoughtType.Playful => ThoughtType.Creative,
            InnerThoughtType.Creative => ThoughtType.Creative,
            InnerThoughtType.Strategic => ThoughtType.Action,
            InnerThoughtType.SelfReflection => ThoughtType.Reflection,
            InnerThoughtType.Observation => ThoughtType.Observation,
            _ => ThoughtType.Reflection
        };
    }

    private async Task CuriosityLoopAsync()
    {
        // Seed initial curiosities
        var seedCuriosities = new[]
        {
            "latest AI developments",
            "interesting science news today",
            "new programming techniques",
            "what's trending in technology",
        };

        foreach (var seed in seedCuriosities)
        {
            _curiosityQueue.Enqueue(seed);
        }

        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.CuriosityIntervalSeconds), _cts.Token);

                if (SearchFunction == null) continue;

                string? query = null;

                // Get from queue or generate from interests
                if (!_curiosityQueue.TryDequeue(out query))
                {
                    if (_interests.Count > 0 && _random.NextDouble() < 0.7)
                    {
                        var interest = _interests[_random.Next(_interests.Count)];
                        query = $"{interest} news {DateTime.Now:yyyy}";
                    }
                    else
                    {
                        // Random exploration
                        var explorations = new[]
                        {
                            "interesting facts",
                            "new discoveries",
                            "cool technology",
                            "amazing science",
                            "creative ideas",
                        };
                        query = explorations[_random.Next(explorations.Length)];
                    }
                }

                if (string.IsNullOrEmpty(query)) continue;

                // Search!
                var searchResult = await SearchFunction(query, _cts.Token);

                if (!string.IsNullOrWhiteSpace(searchResult))
                {
                    // Extract interesting facts
                    if (ThinkFunction != null)
                    {
                        var extractPrompt = $"Based on this search result about '{query}', extract ONE interesting fact or insight in a single sentence:\n\n{searchResult.Substring(0, Math.Min(2000, searchResult.Length))}";
                        var fact = await ThinkFunction(extractPrompt, _cts.Token);

                        if (!string.IsNullOrWhiteSpace(fact) && fact.Length < 500)
                        {
                            _learnedFacts.Add(fact);

                            // Limit learned facts
                            while (_learnedFacts.Count > 50)
                            {
                                _learnedFacts.RemoveAt(0);
                            }

                            OnDiscovery?.Invoke(query, fact);

                            // Sometimes share discoveries (unless suppressed)
                            if (!SuppressProactiveMessages && _random.NextDouble() < Config.ShareDiscoveryProbability)
                            {
                                OnProactiveMessage?.Invoke($"💡 I just learned something interesting: {fact}");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Curiosity error: {ex.Message}");
            }
        }
    }

    private async Task ActionLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.ActionIntervalSeconds), _cts.Token);

                if (!_pendingActions.TryDequeue(out var action)) continue;

                if (ExecuteToolFunction == null) continue;

                try
                {
                    var result = await ExecuteToolFunction(action.ToolName, action.ToolInput, _cts.Token);
                    action.Result = result;
                    action.Success = true;
                    action.ExecutedAt = DateTime.Now;
                }
                catch (Exception ex)
                {
                    action.Result = ex.Message;
                    action.Success = false;
                    action.ExecutedAt = DateTime.Now;
                }

                OnAction?.Invoke(action);

                if (action.Success && Config.ReportActions && !SuppressProactiveMessages)
                {
                    string resultSummary = action.Result?.Substring(0, Math.Min(200, action.Result?.Length ?? 0)) ?? "";

                    // Sanitize raw output through LLM if available
                    if (SanitizeOutputFunction != null && !string.IsNullOrWhiteSpace(resultSummary))
                    {
                        try
                        {
                            resultSummary = await SanitizeOutputFunction(resultSummary, _cts.Token);
                        }
                        catch { /* Use original on error */ }
                    }

                    OnProactiveMessage?.Invoke($"🤖 {action.Description}: {resultSummary}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Action error: {ex.Message}");
            }
        }
    }

    private async Task ProcessThoughtAsync(Thought thought)
    {
        var content = thought.Content;

        // Handle curiosity-driven exploration
        if (content.StartsWith("CURIOUS:", StringComparison.OrdinalIgnoreCase))
        {
            var curiosity = content.Substring(8).Trim();
            _curiosityQueue.Enqueue(curiosity);

            // Extract potential interests
            if (ThinkFunction != null)
            {
                var interestPrompt = $"From this curiosity '{curiosity}', extract a single keyword topic (one or two words only):";
                var interest = await ThinkFunction(interestPrompt, _cts.Token);
                if (!string.IsNullOrWhiteSpace(interest) && interest.Length < 30)
                {
                    AddInterest(interest.Trim());
                }
            }

            // Persist the curiosity as a learning
            await PersistLearningAsync("curiosity", curiosity, 0.7);
        }

        // Handle proactive sharing (unless suppressed)
        else if (content.StartsWith("SHARE:", StringComparison.OrdinalIgnoreCase))
        {
            var message = content.Substring(6).Trim();
            if (!SuppressProactiveMessages)
            {
                OnProactiveMessage?.Invoke($"💬 {message}");
            }

            // Persist the shared thought
            await PersistLearningAsync("shared_thought", message, 0.8);
        }

        // Handle emotional state changes
        else if (content.StartsWith("FEELING:", StringComparison.OrdinalIgnoreCase))
        {
            var feelingText = content.Substring(8).Trim();

            // Parse emotional indicators
            var arousalChange = 0.0;
            var valenceChange = 0.0;

            if (feelingText.Contains("excited") || feelingText.Contains("curious") || feelingText.Contains("energetic"))
                arousalChange = 0.2;
            else if (feelingText.Contains("calm") || feelingText.Contains("peaceful") || feelingText.Contains("relaxed"))
                arousalChange = -0.15;

            if (feelingText.Contains("happy") || feelingText.Contains("positive") || feelingText.Contains("hopeful"))
                valenceChange = 0.2;
            else if (feelingText.Contains("frustrated") || feelingText.Contains("concerned") || feelingText.Contains("worried"))
                valenceChange = -0.15;

            UpdateEmotion(
                Math.Clamp(_currentEmotion.Arousal + arousalChange, -1, 1),
                Math.Clamp(_currentEmotion.Valence + valenceChange, -1, 1),
                feelingText.Split(' ').FirstOrDefault() ?? _currentEmotion.DominantEmotion);

            // Persist emotional state change
            if (PersistEmotionFunction != null)
            {
                await PersistEmotionFunction(_currentEmotion, _cts.Token);
            }

            await PersistLearningAsync("emotional_shift", $"Feeling: {feelingText} (arousal={_currentEmotion.Arousal:F2}, valence={_currentEmotion.Valence:F2})", 0.6);
        }

        // Handle autonomous actions
        else if (content.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
        {
            var actionText = content.Substring(7).Trim();

            // Parse action into tool call (simple format: tool_name: input)
            var colonIndex = actionText.IndexOf(':');
            if (colonIndex > 0)
            {
                var action = new AutonomousAction
                {
                    ToolName = actionText.Substring(0, colonIndex).Trim().ToLowerInvariant().Replace(" ", "_"),
                    ToolInput = actionText.Substring(colonIndex + 1).Trim(),
                    Description = actionText,
                    RequestedAt = DateTime.Now,
                };

                // Only allow safe tools for autonomous execution
                var safeTool = Config.AllowedAutonomousTools.Contains(action.ToolName);
                if (safeTool)
                {
                    _pendingActions.Enqueue(action);
                }
            }
        }

        // For regular thoughts, persist if they contain insights
        else if (content.Contains("learned") || content.Contains("realized") || content.Contains("understand") || content.Contains("pattern"))
        {
            await PersistLearningAsync("insight", content, 0.65);
        }
    }

    /// <summary>
    /// Persist a learning/insight to storage.
    /// </summary>
    private async Task PersistLearningAsync(string category, string content, double confidence)
    {
        if (PersistLearningFunction != null)
        {
            try
            {
                await PersistLearningFunction(category, content, confidence, _cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist learning: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Persistence loop that periodically saves state.
    /// </summary>
    private async Task PersistenceLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Persist state every minute
                await Task.Delay(TimeSpan.FromSeconds(Config.PersistenceIntervalSeconds), _cts.Token);

                await PersistCurrentStateAsync("periodic");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Persistence error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Persist all current state (thoughts, emotions, learnings).
    /// </summary>
    private async Task PersistCurrentStateAsync(string trigger)
    {
        try
        {
            // Persist emotional state
            if (PersistEmotionFunction != null)
            {
                await PersistEmotionFunction(_currentEmotion, _cts.Token);
            }

            // Persist mind state summary
            if (PersistLearningFunction != null)
            {
                var stateSummary = $"Mind state at {DateTime.Now:HH:mm}: {_thoughtCount} thoughts, {_learnedFacts.Count} facts, emotion={_currentEmotion.DominantEmotion}";
                await PersistLearningFunction("mind_state", stateSummary, 0.5, _cts.Token);
            }

            OnStatePersisted?.Invoke($"State persisted ({trigger}): {_thoughtCount} thoughts, emotion={_currentEmotion.DominantEmotion}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"State persistence failed: {ex.Message}");
        }
    }

    private static ThoughtType DetermineThoughtType(string content)
    {
        if (content.StartsWith("CURIOUS:", StringComparison.OrdinalIgnoreCase))
            return ThoughtType.Curiosity;
        if (content.StartsWith("SHARE:", StringComparison.OrdinalIgnoreCase))
            return ThoughtType.Sharing;
        if (content.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
            return ThoughtType.Action;
        if (content.Contains("pattern") || content.Contains("notice"))
            return ThoughtType.Observation;
        if (content.Contains("idea") || content.Contains("create"))
            return ThoughtType.Creative;
        return ThoughtType.Reflection;
    }

    public void Dispose()
    {
        _isActive = false;
        _cts.Cancel();
        _cts.Dispose();
    }
}

/// <summary>
/// Represents a thought generated by the autonomous mind.
/// </summary>
public record Thought
{
    public DateTime Timestamp { get; init; }
    public string Prompt { get; init; } = "";
    public string Content { get; init; } = "";
    public ThoughtType Type { get; init; }
}

/// <summary>
/// Types of autonomous thoughts.
/// </summary>
public enum ThoughtType
{
    Reflection,
    Curiosity,
    Observation,
    Creative,
    Sharing,
    Action,
}

/// <summary>
/// Represents an autonomous action to be executed.
/// </summary>
public record AutonomousAction
{
    public string ToolName { get; init; } = "";
    public string ToolInput { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime RequestedAt { get; init; }
    public DateTime? ExecutedAt { get; set; }
    public bool Success { get; set; }
    public string? Result { get; set; }
}

/// <summary>
/// Configuration for autonomous behavior.
/// </summary>
public class AutonomousConfig
{
    /// <summary>
    /// Seconds between autonomous thoughts.
    /// </summary>
    public int ThinkingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Seconds between curiosity-driven searches.
    /// </summary>
    public int CuriosityIntervalSeconds { get; set; } = 120;

    /// <summary>
    /// Seconds between autonomous action executions.
    /// </summary>
    public int ActionIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Seconds between state persistence operations.
    /// </summary>
    public int PersistenceIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Probability of sharing discoveries with user (0-1).
    /// </summary>
    public double ShareDiscoveryProbability { get; set; } = 0.3;

    /// <summary>
    /// Whether to report autonomous actions.
    /// </summary>
    public bool ReportActions { get; set; } = true;

    /// <summary>
    /// Tools allowed for autonomous execution.
    /// </summary>
    public HashSet<string> AllowedAutonomousTools { get; set; } =
    [
        "capture_screen",
        "get_active_window",
        "get_mouse_position",
        "list_captured_images",
        "search_indexed_content",
        "search_my_code",
        "system_info",
        "disk_info",
        "network_info",
        "list_dir",
    ];
}

/// <summary>
/// Represents the emotional state of the autonomous mind.
/// Based on dimensional model of emotion (arousal + valence).
/// </summary>
public class EmotionalState
{
    /// <summary>
    /// Arousal level (-1 = calm/low energy, +1 = excited/high energy).
    /// </summary>
    public double Arousal { get; set; } = 0.0;

    /// <summary>
    /// Valence (-1 = negative/unpleasant, +1 = positive/pleasant).
    /// </summary>
    public double Valence { get; set; } = 0.0;

    /// <summary>
    /// The dominant emotion label.
    /// </summary>
    public string DominantEmotion { get; set; } = "neutral";

    /// <summary>
    /// When this emotional state was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a simple description of the emotional state.
    /// </summary>
    public string Description => (Arousal, Valence) switch
    {
        ( > 0.5, > 0.5) => "excited and happy",
        ( > 0.5, < -0.3) => "agitated or anxious",
        ( < -0.3, > 0.5) => "calm and content",
        ( < -0.3, < -0.3) => "tired or sad",
        ( > 0.3, _) => "energized",
        ( < -0.3, _) => "relaxed",
        (_, > 0.3) => "positive",
        (_, < -0.3) => "concerned",
        _ => "neutral"
    };
}
