// <copyright file="AutonomousMind.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Ouroboros.Tools;

/// <summary>
/// Autonomous mind that thinks, explores, and acts independently in the background.
/// Enables curiosity-driven learning, proactive actions, and self-directed exploration.
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
    private bool _isActive;
    private DateTime _lastThought = DateTime.MinValue;
    private int _thoughtCount;

    /// <summary>
    /// Delegate for generating AI responses.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ThinkFunction { get; set; }

    /// <summary>
    /// Delegate for web search.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? SearchFunction { get; set; }

    /// <summary>
    /// Delegate for executing tools.
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

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
    /// Configuration for autonomous behavior.
    /// </summary>
    public AutonomousConfig Config { get; set; } = new();

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

        OnProactiveMessage?.Invoke("💤 Autonomous mind entering rest state.");
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
        var thinkingPrompts = new[]
        {
            "What have I learned recently that connects to something else I know?",
            "What am I curious about right now?",
            "Is there something I should proactively tell the user?",
            "What patterns have I noticed in our conversations?",
            "What's something interesting I could explore or research?",
            "How can I be more helpful based on what I know about the user?",
            "What questions would I like to answer for myself?",
            "What creative ideas come to mind?",
            "Is there something happening in the world I should know about?",
            "What tools do I have that I haven't used creatively yet?",
        };

        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.ThinkingIntervalSeconds), _cts.Token);

                if (ThinkFunction == null) continue;

                // Generate a thought
                var prompt = thinkingPrompts[_random.Next(thinkingPrompts.Length)];

                // Add context from recent activity
                var context = new StringBuilder();
                context.AppendLine("You are an autonomous AI mind, thinking independently in the background.");
                context.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm}");
                context.AppendLine($"Thoughts so far: {_thoughtCount}");

                if (_learnedFacts.Count > 0)
                {
                    context.AppendLine($"Recent discoveries: {string.Join("; ", _learnedFacts.TakeLast(3))}");
                }

                if (_interests.Count > 0)
                {
                    context.AppendLine($"My interests: {string.Join(", ", _interests)}");
                }

                context.AppendLine($"\nReflection prompt: {prompt}");
                context.AppendLine("\nRespond with a brief, genuine thought (1-2 sentences). If you have a curiosity to explore, start with 'CURIOUS:'. If you want to tell the user something, start with 'SHARE:'. If you want to take an action, start with 'ACTION:'.");

                var response = await ThinkFunction(context.ToString(), _cts.Token);

                var thought = new Thought
                {
                    Timestamp = DateTime.Now,
                    Prompt = prompt,
                    Content = response,
                    Type = DetermineThoughtType(response),
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

                            // Sometimes share discoveries
                            if (_random.NextDouble() < Config.ShareDiscoveryProbability)
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

                if (action.Success && Config.ReportActions)
                {
                    OnProactiveMessage?.Invoke($"🤖 I autonomously executed: {action.Description}\nResult: {action.Result?.Substring(0, Math.Min(200, action.Result?.Length ?? 0))}");
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
        }

        // Handle proactive sharing
        else if (content.StartsWith("SHARE:", StringComparison.OrdinalIgnoreCase))
        {
            var message = content.Substring(6).Trim();
            OnProactiveMessage?.Invoke($"💬 {message}");
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
