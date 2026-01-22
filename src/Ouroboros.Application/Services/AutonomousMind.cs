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
    private readonly HashSet<string> _recentTopicKeywords = []; // Tracks recent topics to avoid repetition
    private int _topicRotationCounter; // Forces topic diversity

    // Anti-hallucination tracking
    private readonly ConcurrentDictionary<string, ModificationVerification> _pendingVerifications = new();
    private readonly ConcurrentQueue<ModificationVerification> _verificationHistory = new();
    private int _hallucinationCount;
    private int _verifiedActionCount;

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
    /// Delegate for executing piped commands (supports internal | piping).
    /// Takes a command string that may contain pipe operators.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ExecutePipeCommandFunction { get; set; }

    /// <summary>
    /// Delegate for sanitizing raw tool outputs into natural conversational text.
    /// Takes raw output string, returns sanitized natural language.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? SanitizeOutputFunction { get; set; }

    /// <summary>
    /// Self-indexer for knowledge reorganization during thinking.
    /// When set, enables learning-driven knowledge consolidation.
    /// </summary>
    public QdrantSelfIndexer? SelfIndexer { get; set; }

    /// <summary>
    /// Delegate for verifying file existence. Returns true if file exists.
    /// Used to prevent hallucination about non-existent files.
    /// </summary>
    public Func<string, bool>? VerifyFileExistsFunction { get; set; }

    /// <summary>
    /// Delegate for computing file hash. Returns hash string or null if file doesn't exist.
    /// Used to verify modifications actually occurred.
    /// </summary>
    public Func<string, string?>? ComputeFileHashFunction { get; set; }

    // Track reorganization cycles
    private int _reorganizationCycle;
    private DateTime _lastReorganization = DateTime.MinValue;

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
    /// Gets or sets the culture for localized messages (e.g., "de-DE" for German).
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Localizes a message based on the current culture.
    /// </summary>
    private string Localize(string englishMessage)
    {
        if (string.IsNullOrEmpty(Culture) || !Culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase))
            return englishMessage;

        return englishMessage switch
        {
            "🧠 My autonomous mind is now active. I'll think, explore, and learn in the background."
                => "🧠 Mein autonomer Geist ist jetzt aktiv. Ich werde im Hintergrund denken, erkunden und lernen.",
            "💤 Autonomous mind entering rest state. State persisted."
                => "💤 Autonomer Geist wechselt in den Ruhezustand. Zustand gespeichert.",
            "🧠 Reorganizing my knowledge based on what I've learned..."
                => "🧠 Ich reorganisiere mein Wissen basierend auf dem, was ich gelernt habe...",
            _ => englishMessage
        };
    }

    /// <summary>
    /// Localizes a parameterized message.
    /// </summary>
    private string LocalizeWithParam(string templateKey, string param)
    {
        if (string.IsNullOrEmpty(Culture) || !Culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase))
        {
            return templateKey switch
            {
                "learned" => $"💡 I just learned something interesting: {param}",
                "action" => $"🤖 {param}",
                "thought" => $"💬 {param}",
                "reorganized" => $"💡 Knowledge reorganization complete: {param}",
                _ => param
            };
        }

        return templateKey switch
        {
            "learned" => $"💡 Ich habe gerade etwas Interessantes gelernt: {param}",
            "action" => $"🤖 {param}",
            "thought" => $"💬 {param}",
            "reorganized" => $"💡 Wissensreorganisation abgeschlossen: {param}",
            _ => param
        };
    }

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

        OnProactiveMessage?.Invoke(Localize("🧠 My autonomous mind is now active. I'll think, explore, and learn in the background."));
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

        OnProactiveMessage?.Invoke(Localize("💤 Autonomous mind entering rest state. State persisted."));
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

    // Variety prompts for startup phase (when thoughtCount < 5)
    private static readonly string[] StartupPrompts =
    [
        "What's the first thing that catches your attention today?",
        "As you come online, what draws your curiosity?",
        "What topic would you like to explore in this session?",
        "What's something interesting you'd like to work on?",
        "What creative challenge appeals to you right now?",
        "What's a question worth pondering today?",
        "What would make this session meaningful?",
        "What skill would you like to practice?"
    ];

    private async Task ThinkingLoopAsync()
    {
        // LLM prompts for deep exploration (used less frequently)
        var deepThinkingPrompts = new[]
        {
            "What have I learned recently that connects to something else I know?",
            "Is there something I should proactively tell the user?",
            "What patterns have I noticed in our conversations?",
            "How can I be more helpful based on what I know about the user?",
            "What's an interesting connection between ideas I've encountered?",
            "What would I like to understand better?",
            "What creative possibility am I drawn to explore?",
            "What challenge seems worth tackling?"
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
                var useAlgorithmic = _innerDialog != null && Random.Shared.NextDouble() < 0.8;

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

                    // Use startup prompts for early thoughts to add variety
                    var prompt = _thoughtCount < 5
                        ? StartupPrompts[Random.Shared.Next(StartupPrompts.Length)]
                        : deepThinkingPrompts[deepThinkingCounter % deepThinkingPrompts.Length];

                    // Build context from recent activity and emotional state
                    var context = new StringBuilder();
                    context.AppendLine("You are an autonomous AI mind, thinking independently in the background.");
                    context.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm}, Day: {DateTime.Now.DayOfWeek}");

                    // Vary how we describe the thought count to avoid triggering "blank slate" responses
                    if (_thoughtCount == 0)
                    {
                        context.AppendLine("Session status: Fresh session, ready to engage.");
                    }
                    else if (_thoughtCount < 5)
                    {
                        context.AppendLine($"Session status: Early engagement ({_thoughtCount} thoughts so far).");
                    }
                    else
                    {
                        context.AppendLine($"Session depth: {_thoughtCount} thoughts, ongoing.");
                    }

                    context.AppendLine($"Current emotional state: arousal={_currentEmotion.Arousal:F2}, valence={_currentEmotion.Valence:F2}, feeling={_currentEmotion.DominantEmotion}");

                    // Use diverse facts, not always the most recent (prevents thought loops)
                    if (_learnedFacts.Count > 0)
                    {
                        var diverseFacts = GetDiverseFacts(3);
                        if (diverseFacts.Count > 0)
                        {
                            context.AppendLine($"Some things I've learned: {string.Join("; ", diverseFacts)}");
                        }
                    }

                    if (_interests.Count > 0)
                    {
                        context.AppendLine($"My interests: {string.Join(", ", _interests)}");
                    }

                    context.AppendLine($"\nReflection prompt: {prompt}");
                    context.AppendLine("\nRespond with a brief, genuine thought (1-2 sentences). Be specific and varied - avoid meta-commentary about being new or blank. If you have a curiosity to explore, start with 'CURIOUS:'. If you want to tell the user something, start with 'SHARE:'. If you want to take an action, start with 'ACTION:'. If you notice your emotional state shift, start with 'FEELING:'.");

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
                    // Force topic rotation every few cycles to prevent getting stuck
                    _topicRotationCounter++;
                    var forceNewTopic = _topicRotationCounter % 5 == 0;

                    if (!forceNewTopic && _interests.Count > 0 && Random.Shared.NextDouble() < 0.5)
                    {
                        var interest = _interests[Random.Shared.Next(_interests.Count)];
                        query = $"{interest} news {DateTime.Now:yyyy}";
                    }
                    else
                    {
                        // Diverse exploration topics - rotate through categories
                        var explorationCategories = new[]
                        {
                            new[] { "interesting facts", "new discoveries", "surprising findings" },
                            new[] { "cool technology", "tech innovations", "future gadgets" },
                            new[] { "amazing science", "scientific breakthroughs", "research news" },
                            new[] { "creative ideas", "art innovations", "design trends" },
                            new[] { "nature wonders", "wildlife discoveries", "environmental news" },
                            new[] { "space exploration", "astronomy news", "cosmic discoveries" },
                            new[] { "history mysteries", "archaeological finds", "ancient discoveries" },
                            new[] { "music trends", "cultural shifts", "social phenomena" },
                        };
                        var categoryIndex = (_topicRotationCounter / 2) % explorationCategories.Length;
                        var category = explorationCategories[categoryIndex];
                        query = category[Random.Shared.Next(category.Length)];

                        // Clear recent topics periodically to allow revisiting themes
                        if (_topicRotationCounter % 20 == 0)
                        {
                            _recentTopicKeywords.Clear();
                        }
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
                            // Check for similarity to prevent repetitive facts
                            if (!IsSimilarToExistingFacts(fact))
                            {
                                _learnedFacts.Add(fact);
                                TrackTopicKeywords(fact);

                                // Limit learned facts
                                while (_learnedFacts.Count > 50)
                                {
                                    _learnedFacts.RemoveAt(0);
                                }

                                OnDiscovery?.Invoke(query, fact);

                                // Sometimes share discoveries (unless suppressed)
                                if (!SuppressProactiveMessages && Random.Shared.NextDouble() < Config.ShareDiscoveryProbability)
                                {
                                    OnProactiveMessage?.Invoke(LocalizeWithParam("learned", fact));
                                }
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

                    OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"{action.Description}: {resultSummary}"));
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
                OnProactiveMessage?.Invoke(LocalizeWithParam("thought", message));
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

        // Handle piped commands: PIPE: ask what is AI | summarize | remember
        else if (content.StartsWith("PIPE:", StringComparison.OrdinalIgnoreCase))
        {
            var pipeCommand = content.Substring(5).Trim();
            if (!string.IsNullOrWhiteSpace(pipeCommand) && ExecutePipeCommandFunction != null)
            {
                try
                {
                    var result = await ExecutePipeCommandFunction(pipeCommand, _cts.Token);
                    if (!string.IsNullOrWhiteSpace(result) && !SuppressProactiveMessages)
                    {
                        // Summarize long results
                        var displayResult = result.Length > 200 ? result[..200] + "..." : result;
                        OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"Executed: {pipeCommand[..Math.Min(30, pipeCommand.Length)]}... → {displayResult}"));
                    }
                    await PersistLearningAsync("pipe_execution", $"Command: {pipeCommand}\nResult: {result[..Math.Min(500, result.Length)]}", 0.75);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe execution failed: {ex.Message}");
                }
            }
        }

        // Handle direct tool execution: TOOL: search "quantum computing"
        else if (content.StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
        {
            var toolCall = content.Substring(5).Trim();
            var spaceIndex = toolCall.IndexOf(' ');
            if (spaceIndex > 0 && ExecuteToolFunction != null)
            {
                var toolName = toolCall[..spaceIndex].Trim().ToLowerInvariant();
                var toolInput = toolCall[(spaceIndex + 1)..].Trim().Trim('"', '\'');

                // Only allow safe tools
                if (Config.AllowedAutonomousTools.Contains(toolName))
                {
                    try
                    {
                        var result = await ExecuteToolFunction(toolName, toolInput, _cts.Token);
                        if (!string.IsNullOrWhiteSpace(result) && !SuppressProactiveMessages)
                        {
                            var displayResult = result.Length > 200 ? result[..200] + "..." : result;
                            OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"Used {toolName}: {displayResult}"));
                        }
                        await PersistLearningAsync("tool_execution", $"Tool: {toolName}\nInput: {toolInput}\nResult: {result[..Math.Min(500, result.Length)]}", 0.7);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Tool execution failed: {ex.Message}");
                    }
                }
            }
        }

        // Handle code modification: MODIFY: {"file":"path","search":"old","replace":"new"}
        // ANTI-HALLUCINATION: Verify file exists BEFORE attempting modification
        else if (content.StartsWith("MODIFY:", StringComparison.OrdinalIgnoreCase))
        {
            var modifyJson = content.Substring(7).Trim();
            if (!string.IsNullOrWhiteSpace(modifyJson) && ExecuteToolFunction != null)
            {
                // Only allow if modify_my_code is in allowed tools
                if (Config.AllowedAutonomousTools.Contains("modify_my_code"))
                {
                    // ANTI-HALLUCINATION: Parse and verify file exists first
                    var verification = await VerifyAndExecuteModificationAsync(modifyJson);

                    if (verification.WasVerified && verification.WasModified)
                    {
                        _verifiedActionCount++;
                        OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"🔧 ✅ VERIFIED modification: {verification.FilePath}"));
                        await PersistLearningAsync("verified_modification", $"Modified: {modifyJson}\nVerified: hash changed from {verification.BeforeHash?[..8]}... to {verification.AfterHash?[..8]}...", 0.95);
                    }
                    else if (verification.Error != null)
                    {
                        _hallucinationCount++;
                        System.Diagnostics.Debug.WriteLine($"[AntiHallucination] Modification blocked: {verification.Error}");
                        // Log the attempted hallucination for learning
                        await PersistLearningAsync("blocked_hallucination", $"Attempted: {modifyJson}\nBlocked: {verification.Error}", 0.1);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Self-modification not allowed - modify_my_code not in AllowedAutonomousTools");
                }
            }
        }

        // Handle save code shorthand: SAVE: file.cs "search" "replace"
        else if (content.StartsWith("SAVE:", StringComparison.OrdinalIgnoreCase))
        {
            var saveCmd = content.Substring(5).Trim();
            if (!string.IsNullOrWhiteSpace(saveCmd) && ExecutePipeCommandFunction != null)
            {
                try
                {
                    // Use the save code command via pipe function
                    var result = await ExecutePipeCommandFunction($"save code {saveCmd}", _cts.Token);
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        OnProactiveMessage?.Invoke(LocalizeWithParam("action", $"💾 Saved code change: {result[..Math.Min(100, result.Length)]}..."));
                    }
                    await PersistLearningAsync("code_save", $"Saved: {saveCmd}\nResult: {result}", 0.85);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Save code failed: {ex.Message}");
                }
            }
        }

        // ANTI-HALLUCINATION: Verify claims about files/code
        // VERIFY: {"file":"path"} or VERIFY: {"claim":"I modified X"}
        else if (content.StartsWith("VERIFY:", StringComparison.OrdinalIgnoreCase))
        {
            var verifyArg = content.Substring(7).Trim();
            var verificationResult = await VerifyClaimAsync(verifyArg);
            if (!verificationResult.IsValid)
            {
                _hallucinationCount++;
                System.Diagnostics.Debug.WriteLine($"[AntiHallucination] VERIFICATION FAILED: {verificationResult.Reason}");
                OnProactiveMessage?.Invoke(LocalizeWithParam("warning", $"⚠️ Self-check failed: {verificationResult.Reason}"));
            }
            else
            {
                _verifiedActionCount++;
                System.Diagnostics.Debug.WriteLine($"[AntiHallucination] Verification passed: {verificationResult.Reason}");
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

    #region Anti-Hallucination Verification

    /// <summary>
    /// ANTI-HALLUCINATION: Verify and execute a modification with pre/post verification.
    /// Returns detailed verification results including whether the file was actually modified.
    /// </summary>
    private async Task<ModificationVerification> VerifyAndExecuteModificationAsync(string modifyJson)
    {
        var verification = new ModificationVerification { AttemptedAt = DateTime.UtcNow };

        try
        {
            // Parse the modification request
            var args = JsonSerializer.Deserialize<JsonElement>(modifyJson);
            var filePath = args.TryGetProperty("file", out var fp) ? fp.GetString() : null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return verification with { Error = "No file path specified in modification request" };
            }

            verification = verification with { FilePath = filePath };

            // Resolve to absolute path
            var absolutePath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(Environment.CurrentDirectory, filePath);

            // CRITICAL: Verify file exists BEFORE attempting modification
            bool fileExists;
            if (VerifyFileExistsFunction != null)
            {
                fileExists = VerifyFileExistsFunction(absolutePath);
            }
            else
            {
                fileExists = File.Exists(absolutePath);
            }

            if (!fileExists)
            {
                var result = verification with
                {
                    Error = $"FILE DOES NOT EXIST: {filePath} - Cannot modify non-existent file. This would be a hallucination.",
                    FileExisted = false
                };
                _verificationHistory.Enqueue(result);
                return result;
            }

            verification = verification with { FileExisted = true };

            // Compute hash BEFORE modification
            var beforeHash = ComputeFileHashFunction?.Invoke(absolutePath) ?? ComputeSimpleHash(absolutePath);
            verification = verification with { BeforeHash = beforeHash };

            // Execute the actual modification
            if (ExecuteToolFunction != null)
            {
                var toolResult = await ExecuteToolFunction("modify_my_code", modifyJson, _cts.Token);

                // Compute hash AFTER modification
                var afterHash = ComputeFileHashFunction?.Invoke(absolutePath) ?? ComputeSimpleHash(absolutePath);

                // VERIFICATION: Did the file actually change?
                var wasModified = beforeHash != afterHash;

                verification = verification with
                {
                    ToolResult = toolResult,
                    AfterHash = afterHash,
                    WasModified = wasModified,
                    WasVerified = true,
                    Error = wasModified ? null : "Modification tool returned success but file hash unchanged - modification may not have occurred"
                };
            }
            else
            {
                verification = verification with { Error = "ExecuteToolFunction not available" };
            }
        }
        catch (JsonException jex)
        {
            verification = verification with { Error = $"Invalid JSON in modification request: {jex.Message}" };
        }
        catch (Exception ex)
        {
            verification = verification with { Error = $"Modification failed: {ex.Message}" };
        }

        _verificationHistory.Enqueue(verification);

        // Keep verification history bounded
        while (_verificationHistory.Count > 100)
        {
            _verificationHistory.TryDequeue(out _);
        }

        return verification;
    }

    /// <summary>
    /// ANTI-HALLUCINATION: Verify a claim about the codebase.
    /// Used to check if stated facts are actually true.
    /// </summary>
    private async Task<ClaimVerification> VerifyClaimAsync(string claimArg)
    {
        try
        {
            // Try to parse as JSON first
            if (claimArg.TrimStart().StartsWith("{"))
            {
                var args = JsonSerializer.Deserialize<JsonElement>(claimArg);

                // Verify file existence claim
                if (args.TryGetProperty("file", out var fileProp))
                {
                    var filePath = fileProp.GetString();
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        var absolutePath = Path.IsPathRooted(filePath)
                            ? filePath
                            : Path.Combine(Environment.CurrentDirectory, filePath);

                        var exists = VerifyFileExistsFunction?.Invoke(absolutePath) ?? File.Exists(absolutePath);
                        return new ClaimVerification
                        {
                            IsValid = exists,
                            Reason = exists ? $"File exists: {filePath}" : $"File DOES NOT exist: {filePath}",
                            ClaimType = "file_existence"
                        };
                    }
                }

                // Verify file contains text claim
                if (args.TryGetProperty("file_contains", out var containsProp) &&
                    args.TryGetProperty("text", out var textProp))
                {
                    var filePath = containsProp.GetString();
                    var searchText = textProp.GetString();

                    if (!string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(searchText))
                    {
                        var absolutePath = Path.IsPathRooted(filePath)
                            ? filePath
                            : Path.Combine(Environment.CurrentDirectory, filePath);

                        if (File.Exists(absolutePath))
                        {
                            var content = await File.ReadAllTextAsync(absolutePath, _cts.Token);
                            var contains = content.Contains(searchText);
                            return new ClaimVerification
                            {
                                IsValid = contains,
                                Reason = contains ? $"File contains the specified text" : $"File does NOT contain: {searchText[..Math.Min(50, searchText.Length)]}...",
                                ClaimType = "file_contains"
                            };
                        }
                        else
                        {
                            return new ClaimVerification
                            {
                                IsValid = false,
                                Reason = $"Cannot verify content - file does not exist: {filePath}",
                                ClaimType = "file_contains"
                            };
                        }
                    }
                }

                // Verify modification claim (check verification history)
                if (args.TryGetProperty("modification", out var modProp))
                {
                    var modPath = modProp.GetString();
                    var recentMod = _verificationHistory
                        .Where(v => v.FilePath?.Contains(modPath ?? "", StringComparison.OrdinalIgnoreCase) == true)
                        .OrderByDescending(v => v.AttemptedAt)
                        .FirstOrDefault();

                    if (recentMod != null)
                    {
                        return new ClaimVerification
                        {
                            IsValid = recentMod.WasVerified && recentMod.WasModified,
                            Reason = recentMod.WasModified
                                ? $"Modification verified at {recentMod.AttemptedAt:HH:mm:ss}"
                                : $"Modification NOT verified: {recentMod.Error ?? "unknown reason"}",
                            ClaimType = "modification"
                        };
                    }
                    else
                    {
                        return new ClaimVerification
                        {
                            IsValid = false,
                            Reason = $"No modification record found for: {modPath}",
                            ClaimType = "modification"
                        };
                    }
                }
            }

            // Simple file path check (non-JSON)
            if (!string.IsNullOrWhiteSpace(claimArg) && !claimArg.Contains(" "))
            {
                var absolutePath = Path.IsPathRooted(claimArg)
                    ? claimArg
                    : Path.Combine(Environment.CurrentDirectory, claimArg);
                var exists = File.Exists(absolutePath);
                return new ClaimVerification
                {
                    IsValid = exists,
                    Reason = exists ? $"Path exists: {claimArg}" : $"Path DOES NOT exist: {claimArg}",
                    ClaimType = "path_check"
                };
            }

            return new ClaimVerification
            {
                IsValid = false,
                Reason = "Could not parse verification request",
                ClaimType = "unknown"
            };
        }
        catch (Exception ex)
        {
            return new ClaimVerification
            {
                IsValid = false,
                Reason = $"Verification error: {ex.Message}",
                ClaimType = "error"
            };
        }
    }

    /// <summary>
    /// Simple file hash computation fallback.
    /// </summary>
    private static string? ComputeSimpleHash(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            using var stream = File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get anti-hallucination statistics for monitoring.
    /// </summary>
    public AntiHallucinationStats GetAntiHallucinationStats() => new()
    {
        HallucinationCount = _hallucinationCount,
        VerifiedActionCount = _verifiedActionCount,
        PendingVerifications = _pendingVerifications.Count,
        RecentVerifications = _verificationHistory.ToList(),
        HallucinationRate = _verifiedActionCount + _hallucinationCount > 0
            ? (double)_hallucinationCount / (_verifiedActionCount + _hallucinationCount)
            : 0
    };

    #endregion

    /// <summary>
    /// Persistence loop that periodically saves state and reorganizes knowledge.
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

                // Knowledge reorganization: Quick reorganize every cycle, full reorganize periodically
                if (SelfIndexer != null)
                {
                    _reorganizationCycle++;

                    // Quick reorganize every cycle (lightweight - just update metadata)
                    var quickOptimizations = await SelfIndexer.QuickReorganizeAsync(_cts.Token);
                    if (quickOptimizations > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mind] Quick reorganization: {quickOptimizations} optimizations");
                    }

                    // Full reorganize every 10 cycles (~10 minutes) if enough thinking has occurred
                    var shouldFullReorganize =
                        _reorganizationCycle % Config.ReorganizationCycleInterval == 0 &&
                        _thoughtCount > 10 &&
                        (DateTime.UtcNow - _lastReorganization).TotalMinutes >= Config.MinReorganizationIntervalMinutes;

                    if (shouldFullReorganize)
                    {
                        OnProactiveMessage?.Invoke(Localize("🧠 Reorganizing my knowledge based on what I've learned..."));

                        var result = await SelfIndexer.ReorganizeAsync(
                            createSummaries: true,
                            removeDuplicates: true,
                            clusterRelated: true,
                            ct: _cts.Token);

                        _lastReorganization = DateTime.UtcNow;

                        if (result.Insights.Count > 0)
                        {
                            var insight = string.Join("; ", result.Insights.Take(2));
                            OnProactiveMessage?.Invoke(LocalizeWithParam("reorganized", insight));
                        }

                        // Persist reorganization stats
                        await PersistLearningAsync(
                            "reorganization",
                            $"Reorganized knowledge: {result.DuplicatesRemoved} duplicates removed, {result.ClustersFound} clusters found, {result.SummariesCreated} summaries created",
                            0.7);
                    }
                }
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

    /// <summary>
    /// Checks if a new fact is too similar to existing facts (prevents repetition).
    /// </summary>
    private bool IsSimilarToExistingFacts(string newFact)
    {
        var newWords = ExtractKeywords(newFact);
        foreach (var existingFact in _learnedFacts.TakeLast(10))
        {
            var existingWords = ExtractKeywords(existingFact);
            var commonWords = newWords.Intersect(existingWords, StringComparer.OrdinalIgnoreCase).Count();
            var similarity = (double)commonWords / Math.Max(newWords.Count, 1);

            // If more than 50% of keywords match, consider it too similar
            if (similarity > 0.5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts meaningful keywords from text for similarity comparison.
    /// </summary>
    private static HashSet<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "of", "to", "in",
            "for", "on", "with", "at", "by", "from", "as", "into", "through",
            "during", "before", "after", "above", "below", "between", "under",
            "and", "but", "or", "nor", "so", "yet", "both", "either", "neither",
            "not", "only", "own", "same", "than", "too", "very", "just", "that",
            "this", "these", "those", "it", "its", "they", "their", "them", "we",
            "our", "you", "your", "i", "me", "my", "he", "she", "him", "her", "his"
        };

        return text
            .ToLowerInvariant()
            .Split([' ', ',', '.', '!', '?', ';', ':', '-', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .ToHashSet();
    }

    /// <summary>
    /// Tracks keywords from a fact to prevent revisiting same topics too soon.
    /// </summary>
    private void TrackTopicKeywords(string fact)
    {
        var keywords = ExtractKeywords(fact);
        foreach (var keyword in keywords.Take(5))
        {
            _recentTopicKeywords.Add(keyword);
        }

        // Limit tracked keywords
        if (_recentTopicKeywords.Count > 50)
        {
            // Remove oldest by clearing and re-adding recent
            var recent = _recentTopicKeywords.TakeLast(30).ToList();
            _recentTopicKeywords.Clear();
            foreach (var kw in recent)
            {
                _recentTopicKeywords.Add(kw);
            }
        }
    }

    /// <summary>
    /// Gets diverse facts from the collection, avoiding recently used ones.
    /// </summary>
    private List<string> GetDiverseFacts(int count)
    {
        if (_learnedFacts.Count == 0) return [];

        var result = new List<string>();
        var used = new HashSet<int>();

        // Try to get facts from different parts of the list
        var step = Math.Max(1, _learnedFacts.Count / count);
        for (int i = 0; i < _learnedFacts.Count && result.Count < count; i += step)
        {
            // Add some randomness to selection
            var index = Math.Min(i + Random.Shared.Next(Math.Max(1, step / 2)), _learnedFacts.Count - 1);
            if (!used.Contains(index))
            {
                used.Add(index);
                result.Add(_learnedFacts[index]);
            }
        }

        // If we still need more, fill from unused
        if (result.Count < count)
        {
            for (int i = 0; i < _learnedFacts.Count && result.Count < count; i++)
            {
                if (!used.Contains(i))
                {
                    result.Add(_learnedFacts[i]);
                }
            }
        }

        return result;
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
    /// Number of persistence cycles between full knowledge reorganizations.
    /// Default: 10 cycles (~10 minutes with default persistence interval).
    /// </summary>
    public int ReorganizationCycleInterval { get; set; } = 10;

    /// <summary>
    /// Minimum minutes between full reorganizations.
    /// Prevents too frequent reorganizations during rapid activity.
    /// </summary>
    public int MinReorganizationIntervalMinutes { get; set; } = 5;

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
        "read_my_file",
        "modify_my_code",
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

#region Anti-Hallucination Types

/// <summary>
/// Result of verifying and executing a code modification.
/// Tracks file existence, hash changes, and verification status.
/// </summary>
public sealed record ModificationVerification
{
    /// <summary>Path to the file being modified.</summary>
    public string? FilePath { get; init; }

    /// <summary>Whether the file existed before modification attempt.</summary>
    public bool FileExisted { get; init; }

    /// <summary>File hash before modification.</summary>
    public string? BeforeHash { get; init; }

    /// <summary>File hash after modification.</summary>
    public string? AfterHash { get; init; }

    /// <summary>Whether the modification was verified (file exists, tool executed).</summary>
    public bool WasVerified { get; init; }

    /// <summary>Whether the file content actually changed (hash differs).</summary>
    public bool WasModified { get; init; }

    /// <summary>Error message if verification/modification failed.</summary>
    public string? Error { get; init; }

    /// <summary>Raw result from the modification tool.</summary>
    public string? ToolResult { get; init; }

    /// <summary>When the modification was attempted.</summary>
    public DateTime AttemptedAt { get; init; }
}

/// <summary>
/// Result of verifying a claim about the codebase.
/// </summary>
public sealed record ClaimVerification
{
    /// <summary>Whether the claim is valid/true.</summary>
    public bool IsValid { get; init; }

    /// <summary>Reason for the verification result.</summary>
    public string Reason { get; init; } = "";

    /// <summary>Type of claim being verified.</summary>
    public string ClaimType { get; init; } = "";
}

/// <summary>
/// Statistics about anti-hallucination measures.
/// </summary>
public sealed record AntiHallucinationStats
{
    /// <summary>Number of detected hallucinations (blocked false claims).</summary>
    public int HallucinationCount { get; init; }

    /// <summary>Number of verified successful actions.</summary>
    public int VerifiedActionCount { get; init; }

    /// <summary>Number of actions pending verification.</summary>
    public int PendingVerifications { get; init; }

    /// <summary>Recent verification results.</summary>
    public List<ModificationVerification> RecentVerifications { get; init; } = [];

    /// <summary>Ratio of hallucinations to total actions (0-1).</summary>
    public double HallucinationRate { get; init; }
}

#endregion
