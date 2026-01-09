// <copyright file="AutonomousCoordinator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using Ouroboros.Domain.Autonomous.Neurons;

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Configuration for autonomous behavior.
/// </summary>
public sealed record AutonomousConfiguration
{
    /// <summary>Whether push-based mode is enabled (vs reactive).</summary>
    public bool PushBasedMode { get; init; } = true;

    /// <summary>YOLO mode: auto-approve ALL intentions without prompting. Use with caution!</summary>
    public bool YoloMode { get; init; } = false;

    /// <summary>Whether to auto-approve low-risk intentions.</summary>
    public bool AutoApproveLowRisk { get; init; } = false;

    /// <summary>Whether to auto-approve self-reflection intentions.</summary>
    public bool AutoApproveSelfReflection { get; init; } = true;

    /// <summary>Whether to auto-approve memory operations.</summary>
    public bool AutoApproveMemoryOps { get; init; } = true;

    /// <summary>Interval in seconds between autonomous ticks.</summary>
    public int TickIntervalSeconds { get; init; } = 30;

    /// <summary>Maximum pending intentions before throttling.</summary>
    public int MaxPendingIntentions { get; init; } = 20;

    /// <summary>Whether to enable proactive communication.</summary>
    public bool EnableProactiveCommunication { get; init; } = true;

    /// <summary>Whether to enable code self-modification proposals.</summary>
    public bool EnableCodeModification { get; init; } = true;

    /// <summary>Intention expiry time in minutes (0 = never).</summary>
    public int IntentionExpiryMinutes { get; init; } = 60;

    /// <summary>Categories that always require explicit approval.</summary>
    public HashSet<IntentionCategory> AlwaysRequireApproval { get; init; } =
    [
        IntentionCategory.CodeModification,
        IntentionCategory.GoalPursuit,
    ];

    /// <summary>
    /// Priority-ordered list of tools for research/learning.
    /// First available tool in the list will be used.
    /// </summary>
    public List<string> ResearchToolPriority { get; init; } =
    [
        "firecrawl_research",    // Deep web research with Firecrawl
        "firecrawl_scrape",      // Single page scrape with Firecrawl
        "web_search",            // DuckDuckGo search
        "duckduckgo_search",     // Alias for web search
    ];

    /// <summary>
    /// Priority-ordered list of tools for code analysis.
    /// </summary>
    public List<string> CodeToolPriority { get; init; } =
    [
        "code_analyze",
        "code_search",
        "file_read",
    ];

    /// <summary>
    /// Priority-ordered list of tools for general queries.
    /// </summary>
    public List<string> GeneralToolPriority { get; init; } =
    [
        "firecrawl_research",
        "web_search",
        "recall",
    ];
}

/// <summary>
/// Event args for proactive messages from Ouroboros.
/// </summary>
public sealed record ProactiveMessageEventArgs(
    string Message,
    IntentionPriority Priority,
    string Source,
    DateTime Timestamp);

/// <summary>
/// The AutonomousCoordinator manages Ouroboros's push-based autonomous behavior.
/// It coordinates the neural network, intention bus, and provides a unified interface.
/// </summary>
public sealed class AutonomousCoordinator : IDisposable
{
    private readonly IntentionBus _intentionBus;
    private readonly OuroborosNeuralNetwork _network;
    private readonly AutonomousConfiguration _config;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<ProactiveMessageEventArgs> _messageHistory = [];

    private Task? _coordinationTask;
    private Task? _executionTask;
    private bool _isActive;
    private DateTime _lastTick = DateTime.MinValue;

    /// <summary>
    /// Creates a new autonomous coordinator.
    /// </summary>
    public AutonomousCoordinator(AutonomousConfiguration? config = null)
    {
        _config = config ?? new AutonomousConfiguration();
        _intentionBus = new IntentionBus();
        _network = new OuroborosNeuralNetwork(_intentionBus);
        IsYoloMode = _config.YoloMode;

        // Wire up events
        _intentionBus.OnProactiveMessage += HandleProactiveMessage;
        _intentionBus.OnIntentionRequiresAttention += HandleIntentionRequiresAttention;

        // Initialize core neurons
        InitializeCoreNeurons();
    }

    /// <summary>
    /// Event fired when Ouroboros wants to communicate proactively.
    /// </summary>
    public event Action<ProactiveMessageEventArgs>? OnProactiveMessage;

    /// <summary>
    /// Event fired when a new intention requires user attention.
    /// </summary>
    public event Action<Intention>? OnIntentionRequiresAttention;

    /// <summary>
    /// Gets the intention bus.
    /// </summary>
    public IntentionBus IntentionBus => _intentionBus;

    /// <summary>
    /// Gets the neural network.
    /// </summary>
    public OuroborosNeuralNetwork Network => _network;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public AutonomousConfiguration Configuration => _config;

    /// <summary>
    /// Whether the coordinator is running.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the count of pending intentions.
    /// </summary>
    public int PendingIntentionsCount => _intentionBus.PendingCount;

    /// <summary>
    /// Delegate for executing tool actions.
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

    /// <summary>
    /// Delegate for embedding text.
    /// </summary>
    public Func<string, CancellationToken, Task<float[]>>? EmbedFunction { get; set; }

    /// <summary>
    /// Delegate for storing to Qdrant.
    /// </summary>
    public Func<string, string, float[], CancellationToken, Task>? StoreToQdrantFunction { get; set; }

    /// <summary>
    /// Delegate for searching Qdrant.
    /// </summary>
    public Func<float[], int, CancellationToken, Task<IReadOnlyList<string>>>? SearchQdrantFunction { get; set; }

    /// <summary>
    /// Delegate for storing intentions to Qdrant.
    /// </summary>
    public Func<Intention, CancellationToken, Task>? StoreIntentionFunction { get; set; }

    /// <summary>
    /// Delegate for storing neuron messages to Qdrant.
    /// </summary>
    public Func<NeuronMessage, CancellationToken, Task>? StoreNeuronMessageFunction { get; set; }

    /// <summary>
    /// Delegate for LLM thinking/generation.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ThinkFunction { get; set; }

    /// <summary>
    /// Delegate for MeTTa symbolic query execution.
    /// Returns the query result or error message.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? MeTTaQueryFunction { get; set; }

    /// <summary>
    /// Delegate for adding MeTTa facts.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? MeTTaAddFactFunction { get; set; }

    /// <summary>
    /// Delegate for verifying DAG constraints via MeTTa.
    /// Parameters: branchName, constraint, cancellationToken
    /// Returns true if constraint is satisfied.
    /// </summary>
    public Func<string, string, CancellationToken, Task<bool>>? VerifyDagConstraintFunction { get; set; }

    /// <summary>
    /// Delegate for processing auto-training messages (sends to main chat pipeline).
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ProcessChatFunction { get; set; }

    /// <summary>
    /// Delegate for displaying a message and waiting for TTS to complete.
    /// Parameters: message, persona (null=Ouroboros), cancellationToken
    /// This ensures proper sequencing of User/Ouroboros in auto-training.
    /// </summary>
    public Func<string, string?, CancellationToken, Task>? DisplayAndSpeakFunction { get; set; }

    /// <summary>
    /// Delegate for full chat with tools (for User persona in problem-solving mode).
    /// This gives the User persona access to tools, memory, and MeTTa.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? FullChatWithToolsFunction { get; set; }

    /// <summary>
    /// Action to suppress or enable proactive messages from AutonomousMind.
    /// Parameter: true to suppress, false to enable.
    /// </summary>
    public Action<bool>? SetSuppressProactiveMessages { get; set; }

    /// <summary>
    /// Recent conversation context for topic discovery.
    /// </summary>
    private readonly List<string> _conversationContext = [];

    /// <summary>
    /// Last time we discovered a new topic.
    /// </summary>
    private DateTime _lastTopicDiscovery = DateTime.MinValue;

    /// <summary>
    /// Interval between autonomous topic discovery (in seconds).
    /// </summary>
    public int TopicDiscoveryIntervalSeconds { get; set; } = 90;

    /// <summary>
    /// Whether to validate intentions using MeTTa symbolic reasoning before execution.
    /// </summary>
    public bool EnableMeTTaValidation { get; set; } = true;

    /// <summary>
    /// Whether auto-training mode is active.
    /// </summary>
    public bool IsAutoTrainingActive { get; private set; }

    /// <summary>
    /// Whether YOLO mode is active (auto-approve ALL intentions). Runtime toggle.
    /// Initialized from config but can be changed via /yolo command.
    /// </summary>
    public bool IsYoloMode { get; private set; }

    /// <summary>
    /// Whether voice output (TTS) is enabled.
    /// </summary>
    public bool IsVoiceEnabled { get; set; } = true;

    /// <summary>
    /// Whether voice input (STT/listening) is enabled.
    /// </summary>
    public bool IsListeningEnabled { get; set; }

    /// <summary>
    /// Action to enable/disable TTS in the voice side channel.
    /// </summary>
    public Action<bool>? SetVoiceEnabled { get; set; }

    /// <summary>
    /// Action to start/stop speech recognition.
    /// </summary>
    public Action<bool>? SetListeningEnabled { get; set; }

    /// <summary>
    /// Event fired when speech is recognized from the user.
    /// </summary>
    public event Action<string>? OnSpeechRecognized;

    /// <summary>
    /// Set of available tool names for priority resolution.
    /// Populated from the tool registry during setup.
    /// </summary>
    public HashSet<string> AvailableTools { get; set; } = [];

    /// <summary>
    /// Gets the preferred tool from a priority list based on availability.
    /// Returns the first available tool in the priority list, or the fallback.
    /// </summary>
    /// <param name="priorityList">Ordered list of preferred tools.</param>
    /// <param name="fallback">Fallback tool if none are available.</param>
    /// <returns>The name of the best available tool.</returns>
    public string GetPreferredTool(IEnumerable<string> priorityList, string fallback = "web_search")
    {
        foreach (var toolName in priorityList)
        {
            if (AvailableTools.Contains(toolName))
            {
                return toolName;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Gets the preferred research tool based on configuration.
    /// </summary>
    public string GetPreferredResearchTool() =>
        GetPreferredTool(_config.ResearchToolPriority, "web_search");

    /// <summary>
    /// Gets the preferred code tool based on configuration.
    /// </summary>
    public string GetPreferredCodeTool() =>
        GetPreferredTool(_config.CodeToolPriority, "file_read");

    /// <summary>
    /// Gets the preferred general tool based on configuration.
    /// </summary>
    public string GetPreferredGeneralTool() =>
        GetPreferredTool(_config.GeneralToolPriority, "recall");

    /// <summary>
    /// The user persona neuron for auto-training.
    /// </summary>
    private UserPersonaNeuron? _userPersonaNeuron;

    /// <summary>
    /// Adds context from recent conversation for topic discovery.
    /// </summary>
    public void AddConversationContext(string message)
    {
        _conversationContext.Add(message);
        // Keep last 20 messages
        while (_conversationContext.Count > 20)
        {
            _conversationContext.RemoveAt(0);
        }
    }

    /// <summary>
    /// Starts auto-training mode with a simulated user.
    /// </summary>
    /// <param name="config">Optional user persona configuration.</param>
    public void StartAutoTraining(UserPersonaConfig? config = null)
    {
        Console.WriteLine("  [Coordinator] StartAutoTraining called");

        if (_userPersonaNeuron == null)
        {
            Console.WriteLine("  [Coordinator] Creating new UserPersonaNeuron...");
            _userPersonaNeuron = new UserPersonaNeuron();
            _network.RegisterNeuron(_userPersonaNeuron);

            // Wire up the user message handler
            _userPersonaNeuron.OnUserMessage += HandleAutoTrainingMessage;
            Console.WriteLine("  [Coordinator] OnUserMessage handler wired");

            // Wire up generation function - use full chat if available and tools enabled
            _userPersonaNeuron.GenerateFunction = async (prompt, ct) =>
            {
                var config = _userPersonaNeuron.Config;

                // In problem-solving mode with tools, use full chat pipeline
                if (config.ProblemSolvingMode && config.UseTools && FullChatWithToolsFunction != null)
                {
                    Console.WriteLine("  [UserPersona] Using full chat with tools...");
                    return await FullChatWithToolsFunction(prompt, ct);
                }

                // Otherwise use basic LLM
                if (ThinkFunction != null)
                {
                    return await ThinkFunction(prompt, ct);
                }

                return "Generation function not available.";
            };
            Console.WriteLine($"  [Coordinator] GenerateFunction: {(ThinkFunction != null ? "SET (with tools support)" : "NULL")}");

            // Wire up evaluation function if we have MeTTa
            if (MeTTaQueryFunction != null)
            {
                _userPersonaNeuron.EvaluateFunction = async (question, response, ct) =>
                {
                    // Use LLM to evaluate response quality
                    if (ThinkFunction == null) return 0.5;

                    var evalPrompt = $"Rate this response from 0.0 to 1.0 based on helpfulness and accuracy.\n" +
                                     $"Question: {question}\n" +
                                     $"Response: {response[..Math.Min(500, response.Length)]}\n" +
                                     $"Reply with ONLY a decimal number between 0.0 and 1.0.";

                    try
                    {
                        var result = await ThinkFunction(evalPrompt, ct);
                        if (double.TryParse(result.Trim(), out var score))
                        {
                            return Math.Clamp(score, 0.0, 1.0);
                        }
                    }
                    catch { /* ignore */ }

                    return 0.5; // Default neutral score
                };
            }
        }

        Console.WriteLine("  [Coordinator] Broadcasting training.start...");

        // Configure and start - call directly instead of broadcast to ensure immediate execution
        var trainingConfig = config ?? new UserPersonaConfig();

        // Enable YOLO mode if problem-solving yolo is set
        if (trainingConfig.YoloMode)
        {
            IsYoloMode = true;
            Console.WriteLine("  [Coordinator] YOLO Mode enabled for problem-solving");
        }

        // Suppress proactive messages during problem-solving to avoid noise
        if (trainingConfig.ProblemSolvingMode)
        {
            SetSuppressProactiveMessages?.Invoke(true);
            Console.WriteLine("  [Coordinator] Suppressing proactive messages during problem-solving");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine("  [Coordinator] Starting training directly on neuron...");
                await _userPersonaNeuron.StartTrainingDirectAsync(trainingConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [Coordinator] Training start error: {ex.Message}");
            }
        });

        IsAutoTrainingActive = true;

        string modeDescription;
        if (trainingConfig.ProblemSolvingMode && !string.IsNullOrWhiteSpace(trainingConfig.Problem))
        {
            var yoloIndicator = trainingConfig.YoloMode ? "🤠 **YOLO** " : "";
            modeDescription = $"{yoloIndicator}🔧 **Problem-Solving Mode Started**\n" +
              $"**Problem:** {trainingConfig.Problem}\n" +
              $"**Deliverable:** {trainingConfig.DeliverableType}\n" +
              $"**Tools:** {(trainingConfig.UseTools ? "Enabled" : "Disabled")}\n" +
              (trainingConfig.YoloMode ? "**YOLO Mode:** All actions auto-approved!\n" : "") +
              $"**Max steps:** {trainingConfig.MaxSessionMessages}\n" +
              "User and Ouroboros will collaborate to solve this problem.\n" +
              "Use `/training stop` to end the session.";
        }
        else if (trainingConfig.SelfDialogueMode)
        {
            modeDescription = "🐍 **Self-Dialogue Mode Started**\n" +
              $"Ouroboros-A and {trainingConfig.SecondPersonaName} will now debate and explore ideas together.\n" +
              $"Message interval: {trainingConfig.MessageIntervalSeconds}s\n" +
              "Use `/training stop` to end the session.";
        }
        else
        {
            modeDescription = "🤖 **Auto-Training Mode Started**\n" +
              $"Simulated user '{trainingConfig.Name}' will now interact with me automatically.\n" +
              $"Message interval: {trainingConfig.MessageIntervalSeconds}s\n" +
              "Use `/training stop` to end the session.";
        }

        RaiseProactiveMessage(modeDescription, IntentionPriority.Normal, "coordinator");
    }

    /// <summary>
    /// Stops auto-training mode.
    /// </summary>
    public void StopAutoTraining()
    {
        if (_userPersonaNeuron != null)
        {
            // Restore YOLO mode to config default if it was enabled by problem-solving
            var wasYoloFromTraining = _userPersonaNeuron.Config.YoloMode;
            if (wasYoloFromTraining && !_config.YoloMode)
            {
                IsYoloMode = false;
                Console.WriteLine("  [Coordinator] YOLO Mode restored to config default (off)");
            }

            // Re-enable proactive messages if they were suppressed for problem-solving
            if (_userPersonaNeuron.Config.ProblemSolvingMode)
            {
                SetSuppressProactiveMessages?.Invoke(false);
                Console.WriteLine("  [Coordinator] Proactive messages re-enabled");
            }

            _network.Broadcast("training.stop", null!, "coordinator");
            var stats = _userPersonaNeuron.GetStats();

            RaiseProactiveMessage(
                "🛑 **Auto-Training Mode Stopped**\n" +
                $"Total interactions: {stats.TotalInteractions}\n" +
                $"Average satisfaction: {stats.AverageSatisfaction:F2}\n" +
                $"Session messages: {stats.SessionMessages}",
                IntentionPriority.Normal, "coordinator");
        }

        IsAutoTrainingActive = false;
    }

    /// <summary>
    /// Configures the auto-training user persona.
    /// </summary>
    public void ConfigureAutoTraining(UserPersonaConfig config)
    {
        _network.Broadcast("training.configure", config, "coordinator");
    }

    /// <summary>
    /// Gets auto-training statistics.
    /// </summary>
    public (int TotalInteractions, double AverageSatisfaction, int SessionMessages)? GetAutoTrainingStats()
    {
        return _userPersonaNeuron?.GetStats();
    }

    private async void HandleAutoTrainingMessage(string message, UserPersonaConfig config)
    {
        if (ProcessChatFunction == null)
        {
            RaiseProactiveMessage(
                $"⚠️ Auto-training: ProcessChatFunction not configured. Message: {message}",
                IntentionPriority.Normal, "auto_training");
            return;
        }

        try
        {
            // Display the message with appropriate persona indicator
            var personaLabel = config.SelfDialogueMode
                ? $"🐍 [{config.SecondPersonaName}]"
                : $"👤 [{config.Name}]";

            var userMessage = $"{personaLabel} 💬 {message}";
            var userPersona = config.SelfDialogueMode ? null : "User";

            // Use awaitable display+speak if available, otherwise fall back to event
            if (DisplayAndSpeakFunction != null)
            {
                // Display and wait for User TTS to complete before processing
                await DisplayAndSpeakFunction(userMessage, userPersona, _cts.Token);
            }
            else
            {
                // Fallback: fire-and-forget event (may cause overlap)
                RaiseProactiveMessage(userMessage, IntentionPriority.Normal,
                    config.SelfDialogueMode ? "self_dialogue" : "user_persona");
            }

            // Process through the main chat pipeline (User has finished speaking)
            var response = await ProcessChatFunction(message, _cts.Token);

            // Display Ouroboros's response
            var responseLabel = config.SelfDialogueMode ? "🐍 [Ouroboros-A]" : "🐍";
            var ouroResponse = $"{responseLabel} 💭 {response}";

            if (DisplayAndSpeakFunction != null)
            {
                // Display and wait for Ouroboros TTS to complete
                await DisplayAndSpeakFunction(ouroResponse, null, _cts.Token);
            }
            else
            {
                RaiseProactiveMessage(ouroResponse, IntentionPriority.Normal,
                    config.SelfDialogueMode ? "self_dialogue" : "auto_training");
            }

            // Notify the persona of the response for evaluation
            _network.Broadcast("response.generated", response, "coordinator");

            // Record the interaction
            _userPersonaNeuron?.RecordInteraction(message, response);
        }
        catch (Exception ex)
        {
            RaiseProactiveMessage(
                $"❌ Auto-training error: {ex.Message}",
                IntentionPriority.High, "auto_training");
        }
    }

    /// <summary>
    /// Starts just the neural network for status visibility (without coordination loops).
    /// Use this for passive mode where you want status checks but not proactive behavior.
    /// </summary>
    public void StartNetwork()
    {
        // Configure neurons with functions
        ConfigureNeurons();

        // Start network (activates neurons for status reporting)
        _network.Start();
    }

    /// <summary>
    /// Starts the autonomous coordinator.
    /// </summary>
    public void Start()
    {
        if (_isActive) return;
        _isActive = true;

        // Configure neurons with functions (if not already done by StartNetwork)
        ConfigureNeurons();

        // Start network and bus
        _network.Start();

        // Start coordination and execution loops
        _coordinationTask = Task.Run(CoordinationLoopAsync);
        _executionTask = Task.Run(ExecutionLoopAsync);

        string modeDescription = _config.YoloMode
            ? "🤠 **YOLO Mode (I will act autonomously without asking!)**"
            : (_config.PushBasedMode ? "Push-Based (I'll propose actions for your approval)" : "Reactive");

        RaiseProactiveMessage(
            "🐍 **Ouroboros Autonomous Mode Activated**\n" +
            $"Mode: {modeDescription}\n" +
            (_config.YoloMode
                ? "⚠️ YOLO mode is enabled! I will execute actions without approval. Use with caution!"
                : "I am now thinking, reflecting, and planning. I will ask before acting."),
            IntentionPriority.Normal,
            "coordinator");
    }

    /// <summary>
    /// Stops the autonomous coordinator.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isActive) return;
        _isActive = false;
        _cts.Cancel();

        await _network.StopAsync();

        if (_coordinationTask != null) await _coordinationTask;
        if (_executionTask != null) await _executionTask;

        RaiseProactiveMessage(
            "💤 Autonomous mode deactivated. I'll wait for your instructions.",
            IntentionPriority.Normal,
            "coordinator");
    }

    /// <summary>
    /// Processes user input, potentially as a command.
    /// </summary>
    /// <returns>True if the input was a command, false if it should be processed normally.</returns>
    public bool ProcessCommand(string input)
    {
        var trimmed = input.Trim();

        // Approve command
        if (trimmed.StartsWith("/approve ", StringComparison.OrdinalIgnoreCase))
        {
            var id = trimmed[9..].Trim();
            var success = _intentionBus.ApproveIntentionByPartialId(id, "User approved");
            RaiseProactiveMessage(
                success ? $"✅ Intention approved: {id}" : $"❌ Could not find pending intention: {id}",
                IntentionPriority.Normal, "coordinator");
            return true;
        }

        // Reject command
        if (trimmed.StartsWith("/reject ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed[8..].Split(' ', 2);
            var id = parts[0];
            var reason = parts.Length > 1 ? parts[1] : null;
            var success = _intentionBus.RejectIntentionByPartialId(id, reason);
            RaiseProactiveMessage(
                success ? $"❌ Intention rejected: {id}" : $"Could not find pending intention: {id}",
                IntentionPriority.Normal, "coordinator");
            return true;
        }

        // Approve all low-risk
        if (trimmed.Equals("/approve-all-safe", StringComparison.OrdinalIgnoreCase))
        {
            var count = _intentionBus.ApproveAllLowRisk();
            RaiseProactiveMessage($"✅ Auto-approved {count} low-risk intentions", IntentionPriority.Normal, "coordinator");
            return true;
        }

        // List pending intentions
        if (trimmed.Equals("/intentions", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/pending", StringComparison.OrdinalIgnoreCase))
        {
            var pending = _intentionBus.GetPendingIntentions();
            if (pending.Count == 0)
            {
                RaiseProactiveMessage("📭 No pending intentions", IntentionPriority.Low, "coordinator");
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($"📋 **{pending.Count} Pending Intention(s)**\n");
                foreach (var intention in pending.Take(10))
                {
                    sb.AppendLine($"• `{intention.Id.ToString()[..8]}` [{intention.Priority}] **{intention.Title}**");
                    sb.AppendLine($"  {intention.Description[..Math.Min(80, intention.Description.Length)]}...");
                }
                if (pending.Count > 10)
                {
                    sb.AppendLine($"\n... and {pending.Count - 10} more");
                }
                RaiseProactiveMessage(sb.ToString(), IntentionPriority.Normal, "coordinator");
            }
            return true;
        }

        // Neural network status
        if (trimmed.Equals("/network", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/neurons", StringComparison.OrdinalIgnoreCase))
        {
            RaiseProactiveMessage(_network.GetNetworkState(), IntentionPriority.Low, "coordinator");
            return true;
        }

        // Intention bus status
        if (trimmed.Equals("/bus", StringComparison.OrdinalIgnoreCase))
        {
            RaiseProactiveMessage(_intentionBus.GetSummary(), IntentionPriority.Low, "coordinator");
            return true;
        }

        // Help
        if (trimmed.Equals("/help", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/?", StringComparison.OrdinalIgnoreCase))
        {
            RaiseProactiveMessage(GetHelpText(), IntentionPriority.Normal, "coordinator");
            return true;
        }

        // Toggle push mode
        if (trimmed.Equals("/toggle-push", StringComparison.OrdinalIgnoreCase))
        {
            // Note: This would need to mutate config or use a mutable setting
            RaiseProactiveMessage(
                $"Push-based mode is currently: {(_config.PushBasedMode ? "ON" : "OFF")}",
                IntentionPriority.Normal, "coordinator");
            return true;
        }

        // YOLO mode toggle - auto-approve ALL intentions
        if (trimmed.Equals("/yolo", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/yolo on", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/yolo off", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.Equals("/yolo on", StringComparison.OrdinalIgnoreCase))
                IsYoloMode = true;
            else if (trimmed.Equals("/yolo off", StringComparison.OrdinalIgnoreCase))
                IsYoloMode = false;
            else
                IsYoloMode = !IsYoloMode; // Toggle

            var emoji = IsYoloMode ? "🤠" : "🛡️";
            var status = IsYoloMode ? "ON - All intentions auto-approved!" : "OFF - Manual approval required";
            RaiseProactiveMessage(
                $"{emoji} **YOLO Mode**: {status}\n\n" +
                (IsYoloMode
                    ? "⚠️ All intentions will be executed without approval. Use `/yolo off` to disable."
                    : "Intentions will require approval based on your configuration."),
                IntentionPriority.High, "coordinator");

            // If we just enabled YOLO, approve all pending intentions
            if (IsYoloMode)
            {
                var pending = _intentionBus.GetPendingIntentions();
                foreach (var intention in pending)
                {
                    _intentionBus.ApproveIntention(intention.Id, "🤠 YOLO mode enabled - auto-approved");
                }

                if (pending.Count > 0)
                {
                    RaiseProactiveMessage(
                        $"🚀 Auto-approved {pending.Count} pending intention(s)",
                        IntentionPriority.Normal, "coordinator");
                }
            }

            return true;
        }

        // Voice output (TTS) toggle
        if (trimmed.Equals("/voice", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/voice on", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/voice off", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.Equals("/voice on", StringComparison.OrdinalIgnoreCase))
                IsVoiceEnabled = true;
            else if (trimmed.Equals("/voice off", StringComparison.OrdinalIgnoreCase))
                IsVoiceEnabled = false;
            else
                IsVoiceEnabled = !IsVoiceEnabled;

            SetVoiceEnabled?.Invoke(IsVoiceEnabled);

            var emoji = IsVoiceEnabled ? "🔊" : "🔇";
            var status = IsVoiceEnabled ? "ON - I will speak responses" : "OFF - Text only";
            RaiseProactiveMessage(
                $"{emoji} **Voice Output**: {status}",
                IntentionPriority.Normal, "coordinator");

            return true;
        }

        // Voice input (STT/listening) toggle
        if (trimmed.Equals("/listen", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/listen on", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/listen off", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.Equals("/listen on", StringComparison.OrdinalIgnoreCase))
                IsListeningEnabled = true;
            else if (trimmed.Equals("/listen off", StringComparison.OrdinalIgnoreCase))
                IsListeningEnabled = false;
            else
                IsListeningEnabled = !IsListeningEnabled;

            SetListeningEnabled?.Invoke(IsListeningEnabled);

            var emoji = IsListeningEnabled ? "🎤" : "🔇";
            var status = IsListeningEnabled ? "ON - Speak to me!" : "OFF - Type your messages";
            RaiseProactiveMessage(
                $"{emoji} **Voice Input**: {status}\n" +
                (IsListeningEnabled ? "I'm listening for your voice..." : "Voice recognition stopped."),
                IntentionPriority.Normal, "coordinator");

            return true;
        }

        // YOLO + User training mode combined - fully autonomous
        if (trimmed.Equals("/yolo train", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/yolo user", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/auto", StringComparison.OrdinalIgnoreCase))
        {
            IsYoloMode = true;
            StartAutoTraining();

            RaiseProactiveMessage(
                "🤠🤖 **Full Autonomous Mode Activated!**\n\n" +
                "• YOLO mode: ON (all intentions auto-approved)\n" +
                "• Auto-training: ON (simulated user generating questions)\n\n" +
                "The system will now operate fully autonomously.\n" +
                "Use `/auto stop` or `/yolo off` to regain control.",
                IntentionPriority.Critical, "coordinator");

            return true;
        }

        // Stop full autonomous mode
        if (trimmed.Equals("/auto stop", StringComparison.OrdinalIgnoreCase))
        {
            IsYoloMode = false;
            StopAutoTraining();

            RaiseProactiveMessage(
                "🛡️ **Full Autonomous Mode Deactivated**\n\n" +
                "• YOLO mode: OFF\n" +
                "• Auto-training: OFF\n\n" +
                "Manual control restored.",
                IntentionPriority.High, "coordinator");

            return true;
        }

        // Quick problem-solving: /auto solve <problem>
        if (trimmed.StartsWith("/auto solve ", StringComparison.OrdinalIgnoreCase))
        {
            var problem = trimmed[12..].Trim(); // Remove "/auto solve "
            if (string.IsNullOrWhiteSpace(problem))
            {
                RaiseProactiveMessage(
                    "⚠️ Please provide a problem to solve.\n" +
                    "Usage: `/auto solve Build a rate limiter for our API`",
                    IntentionPriority.Normal, "coordinator");
                return true;
            }

            // Infer deliverable type from problem description using LLM
            _ = Task.Run(async () =>
            {
                var deliverable = await InferDeliverableTypeAsync(problem);
                Console.WriteLine($"  [Coordinator] Inferred deliverable type: {deliverable}");

                // Start problem-solving with YOLO + tools enabled
                var config = new UserPersonaConfig
                {
                    Name = "User",
                    ProblemSolvingMode = true,
                    Problem = problem,
                    DeliverableType = deliverable,
                    UseTools = true,
                    YoloMode = true,
                    MaxSessionMessages = 50,
                };

                StartAutoTraining(config);
            });

            return true;
        }

        // Auto-training commands
        if (trimmed.StartsWith("/training ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/training", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessTrainingCommand(trimmed);
        }

        // Tool priority commands
        if (trimmed.StartsWith("/tools", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessToolsCommand(trimmed);
        }

        return false;
    }

    /// <summary>
    /// Processes tool priority commands.
    /// </summary>
    private bool ProcessToolsCommand(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subCommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : "status";

        switch (subCommand)
        {
            case "status":
            case "list":
                var sb = new StringBuilder();
                sb.AppendLine("🔧 **Tool Priorities**\n");

                sb.AppendLine("**Research Tools** (preferred: " + GetPreferredResearchTool() + ")");
                foreach (var tool in _config.ResearchToolPriority)
                {
                    var available = AvailableTools.Contains(tool);
                    sb.AppendLine($"  {(available ? "✅" : "❌")} {tool}");
                }

                sb.AppendLine("\n**Code Tools** (preferred: " + GetPreferredCodeTool() + ")");
                foreach (var tool in _config.CodeToolPriority)
                {
                    var available = AvailableTools.Contains(tool);
                    sb.AppendLine($"  {(available ? "✅" : "❌")} {tool}");
                }

                sb.AppendLine("\n**General Tools** (preferred: " + GetPreferredGeneralTool() + ")");
                foreach (var tool in _config.GeneralToolPriority)
                {
                    var available = AvailableTools.Contains(tool);
                    sb.AppendLine($"  {(available ? "✅" : "❌")} {tool}");
                }

                sb.AppendLine($"\n📊 Available tools: {AvailableTools.Count}");

                RaiseProactiveMessage(sb.ToString(), IntentionPriority.Normal, "coordinator");
                return true;

            case "available":
                var availableSb = new StringBuilder();
                availableSb.AppendLine($"📋 **Available Tools** ({AvailableTools.Count} total)\n");
                foreach (var tool in AvailableTools.OrderBy(t => t))
                {
                    availableSb.AppendLine($"  • {tool}");
                }

                RaiseProactiveMessage(availableSb.ToString(), IntentionPriority.Normal, "coordinator");
                return true;

            default:
                RaiseProactiveMessage(
                    "🔧 **Tool Priority Commands**\n\n" +
                    "`/tools` or `/tools status`\n" +
                    "  Show tool priorities and availability\n\n" +
                    "`/tools available`\n" +
                    "  List all available tools",
                    IntentionPriority.Normal, "coordinator");
                return true;
        }
    }

    /// <summary>
    /// Processes training-related commands.
    /// </summary>
    private bool ProcessTrainingCommand(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subCommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : "status";

        switch (subCommand)
        {
            case "start":
                var config = new UserPersonaConfig();

                // Parse optional parameters
                for (int i = 2; i < parts.Length; i++)
                {
                    var param = parts[i].Split('=', 2);
                    if (param.Length == 2)
                    {
                        switch (param[0].ToLowerInvariant())
                        {
                            case "name":
                                config = config with { Name = param[1] };
                                break;
                            case "interval":
                                if (int.TryParse(param[1], out var interval))
                                    config = config with { MessageIntervalSeconds = interval };
                                break;
                            case "max":
                                if (int.TryParse(param[1], out var max))
                                    config = config with { MaxSessionMessages = max };
                                break;
                            case "skill":
                                config = config with { SkillLevel = param[1] };
                                break;
                            case "style":
                                config = config with { CommunicationStyle = param[1] };
                                break;
                            case "self":
                            case "selfdialogue":
                            case "self-dialogue":
                                config = config with
                                {
                                    SelfDialogueMode = param[1].ToLowerInvariant() is "true" or "yes" or "1" or "on",
                                    Name = "Ouroboros-B"
                                };
                                break;
                            case "persona2":
                            case "secondpersona":
                                config = config with { SecondPersonaName = param[1] };
                                break;
                            case "problem":
                                config = config with
                                {
                                    ProblemSolvingMode = true,
                                    Problem = param[1]
                                };
                                break;
                            case "deliverable":
                                config = config with { DeliverableType = param[1] };
                                break;
                            case "tools":
                                config = config with { UseTools = param[1].ToLowerInvariant() is "true" or "yes" or "1" or "on" };
                                break;
                            case "yolo":
                                config = config with { YoloMode = param[1].ToLowerInvariant() is "true" or "yes" or "1" or "on" };
                                break;
                        }
                    }
                    else if (parts[i].ToLowerInvariant() is "self" or "selfdialogue" or "self-dialogue")
                    {
                        // Allow "/training start self" shorthand
                        config = config with
                        {
                            SelfDialogueMode = true,
                            Name = "Ouroboros-B"
                        };
                    }
                    else if (parts[i].ToLowerInvariant() is "yolo")
                    {
                        // Allow "/training start problem=... yolo" shorthand
                        config = config with { YoloMode = true };
                    }
                }

                StartAutoTraining(config);
                return true;

            case "stop":
                StopAutoTraining();
                return true;

            case "status":
                var stats = GetAutoTrainingStats();
                if (stats.HasValue)
                {
                    RaiseProactiveMessage(
                        $"📊 **Auto-Training Status**\n" +
                        $"Active: {(IsAutoTrainingActive ? "Yes ✓" : "No")}\n" +
                        $"Total interactions: {stats.Value.TotalInteractions}\n" +
                        $"Average satisfaction: {stats.Value.AverageSatisfaction:F2}\n" +
                        $"Session messages: {stats.Value.SessionMessages}",
                        IntentionPriority.Normal, "coordinator");
                }
                else
                {
                    RaiseProactiveMessage(
                        "📊 **Auto-Training Status**\n" +
                        "Not initialized. Use `/training start` to begin.",
                        IntentionPriority.Normal, "coordinator");
                }

                return true;

            case "topic":
                if (parts.Length > 2)
                {
                    var topic = string.Join(" ", parts.Skip(2));
                    _network.Broadcast("user_persona.set_topic", topic, "coordinator");
                    RaiseProactiveMessage($"📚 Training topic set to: {topic}", IntentionPriority.Low, "coordinator");
                }

                return true;

            case "interest":
                if (parts.Length > 2)
                {
                    var interest = string.Join(" ", parts.Skip(2));
                    _network.Broadcast("user_persona.add_interest", interest, "coordinator");
                    RaiseProactiveMessage($"⭐ Added training interest: {interest}", IntentionPriority.Low, "coordinator");
                }

                return true;

            default:
                RaiseProactiveMessage(
                    "📖 **Auto-Training Commands**\n\n" +
                    "`/training start [name=X] [interval=30] [max=50]`\n" +
                    "  Start auto-training with optional parameters\n\n" +
                    "`/training start self`\n" +
                    "  🐍 **Self-Dialogue**: Ouroboros debates itself\n\n" +
                    "`/training start problem=\"Write a REST API\" deliverable=code max=20`\n" +
                    "  🔧 **Problem-Solving**: Collaborate to solve a specific problem\n" +
                    "  Options: problem=<text>, deliverable=code|plan|analysis|design, tools=true|false, yolo\n\n" +
                    "`/training start problem=\"...\" yolo`\n" +
                    "  🤠 **YOLO Problem-Solving**: Auto-approve all actions, full autonomy\n\n" +
                    "`/training stop` - Stop the session\n" +
                    "`/training status` - Show statistics\n" +
                    "`/training topic <topic>` - Set current topic",
                    IntentionPriority.Normal, "coordinator");
                return true;
        }
    }

    /// <summary>
    /// Injects a goal for autonomous pursuit.
    /// </summary>
    public void InjectGoal(string goal, IntentionPriority priority = IntentionPriority.Normal)
    {
        _network.Broadcast("goal.add", goal, "user");

        _intentionBus.ProposeIntention(
            $"Pursue Goal: {goal[..Math.Min(50, goal.Length)]}",
            $"I want to work towards the goal: {goal}",
            "This goal was provided by the user.",
            IntentionCategory.GoalPursuit,
            "user",
            new IntentionAction { ActionType = "goal", Message = goal },
            priority,
            requiresApproval: true);
    }

    /// <summary>
    /// Sends a message to a specific neuron.
    /// </summary>
    public void SendToNeuron(string neuronId, string topic, object payload)
    {
        var message = new NeuronMessage
        {
            SourceNeuron = "user",
            TargetNeuron = neuronId,
            Topic = topic,
            Payload = payload,
        };
        _network.RouteMessage(message);
    }

    /// <summary>
    /// Gets a status summary.
    /// </summary>
    public string GetStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🐍 **Ouroboros Autonomous Status**\n");
        sb.AppendLine($"**Mode:** {(_config.PushBasedMode ? "Push-Based 🔔" : "Reactive 🔇")}");
        sb.AppendLine($"**Status:** {(_isActive ? "Active 🟢" : "Inactive 🔴")}");
        sb.AppendLine();
        sb.AppendLine(_intentionBus.GetSummary());
        sb.AppendLine();
        sb.AppendLine($"**Neurons:** {_network.Neurons.Count}");
        sb.AppendLine($"**Recent Messages:** {_network.GetRecentMessages(1).Count > 0}");
        return sb.ToString();
    }

    private void InitializeCoreNeurons()
    {
        _network.RegisterNeuron(new ExecutiveNeuron());
        _network.RegisterNeuron(new MemoryNeuron());
        _network.RegisterNeuron(new CodeReflectionNeuron());
        _network.RegisterNeuron(new SymbolicNeuron());
        _network.RegisterNeuron(new SafetyNeuron());
        _network.RegisterNeuron(new CommunicationNeuron());
        _network.RegisterNeuron(new AffectNeuron());
    }

    private void ConfigureNeurons()
    {
        // Configure memory neuron with Qdrant functions
        if (_network.GetNeuron("neuron.memory") is MemoryNeuron memoryNeuron)
        {
            memoryNeuron.EmbedFunction = EmbedFunction;
            memoryNeuron.StoreFunction = StoreToQdrantFunction;
            memoryNeuron.SearchFunction = SearchQdrantFunction;
        }

        // Configure symbolic neuron with MeTTa functions
        if (_network.GetNeuron("neuron.symbolic") is SymbolicNeuron symbolicNeuron)
        {
            // Wire MeTTa query capability
            symbolicNeuron.MeTTaQueryFunction = MeTTaQueryFunction;
            symbolicNeuron.MeTTaAddFactFunction = MeTTaAddFactFunction;
        }

        // Configure communication neuron events
        if (_network.GetNeuron("neuron.communication") is CommunicationNeuron commNeuron)
        {
            commNeuron.OnUserMessage += (msg, priority) =>
                RaiseProactiveMessage(msg, priority, "neuron.communication");
        }

        // Configure network embedding and persistence
        _network.EmbedFunction = EmbedFunction;
        _network.PersistMessageFunction = StoreNeuronMessageFunction;
    }

    private async Task CoordinationLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.TickIntervalSeconds), _cts.Token);

                // Auto-approve configured categories
                AutoApproveConfiguredCategories();

                // Autonomous topic discovery
                if ((DateTime.UtcNow - _lastTopicDiscovery).TotalSeconds >= TopicDiscoveryIntervalSeconds)
                {
                    await DiscoverAndProposeTopicsAsync(_cts.Token);
                    _lastTopicDiscovery = DateTime.UtcNow;
                }

                // Check for stale intentions
                var pending = _intentionBus.GetPendingIntentions();
                if (pending.Count > _config.MaxPendingIntentions)
                {
                    RaiseProactiveMessage(
                        $"⚠️ I have {pending.Count} pending intentions waiting for your decision. " +
                        "Consider using `/approve-all-safe` or reviewing `/intentions`.",
                        IntentionPriority.High, "coordinator");
                }

                // Periodic status broadcast to neurons
                _network.Broadcast("system.tick", new { Time = DateTime.UtcNow, Pending = pending.Count }, "coordinator");

                _lastTick = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Coordination error: {ex.Message}");
            }
        }
    }

    private async Task ExecutionLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);

                // Execute next approved intention
                var intention = _intentionBus.GetNextApprovedIntention();
                if (intention != null)
                {
                    await ExecuteIntentionAsync(intention, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Execution error: {ex.Message}");
            }
        }
    }

    private void AutoApproveConfiguredCategories()
    {
        if (!_config.PushBasedMode) return;

        var pending = _intentionBus.GetPendingIntentions();

        foreach (var intention in pending)
        {
            // YOLO mode: auto-approve EVERYTHING without prompting
            if (IsYoloMode)
            {
                _intentionBus.ApproveIntention(intention.Id, "🤠 YOLO mode - auto-approved");
                continue;
            }

            if (_config.AlwaysRequireApproval.Contains(intention.Category))
                continue;

            var shouldAutoApprove =
                (_config.AutoApproveLowRisk && intention.Priority <= IntentionPriority.Low) ||
                (_config.AutoApproveSelfReflection && intention.Category == IntentionCategory.SelfReflection) ||
                (_config.AutoApproveMemoryOps && intention.Category == IntentionCategory.MemoryManagement);

            if (shouldAutoApprove)
            {
                _intentionBus.ApproveIntention(intention.Id, "Auto-approved by policy");
            }
        }
    }

    /// <summary>
    /// Discovers interesting topics autonomously and proposes actions.
    /// </summary>
    private async Task DiscoverAndProposeTopicsAsync(CancellationToken ct)
    {
        if (ThinkFunction == null) return;

        try
        {
            // Build context from recent conversations and memories
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("You are Ouroboros, an autonomous AI agent. Based on your recent interactions and knowledge, suggest ONE specific action you want to take.");
            contextBuilder.AppendLine();

            if (_conversationContext.Count > 0)
            {
                contextBuilder.AppendLine("Recent conversation context:");
                foreach (var msg in _conversationContext.TakeLast(10))
                {
                    contextBuilder.AppendLine($"- {msg[..Math.Min(200, msg.Length)]}");
                }
                contextBuilder.AppendLine();
            }

            // Get recent memories if we can search
            if (EmbedFunction != null && SearchQdrantFunction != null)
            {
                try
                {
                    var queryEmbed = await EmbedFunction("interesting topics to explore or research", ct);
                    var memories = await SearchQdrantFunction(queryEmbed, 5, ct);
                    if (memories.Count > 0)
                    {
                        contextBuilder.AppendLine("Related memories:");
                        foreach (var mem in memories)
                        {
                            contextBuilder.AppendLine($"- {mem[..Math.Min(150, mem.Length)]}");
                        }
                        contextBuilder.AppendLine();
                    }
                }
                catch { /* Ignore search errors */ }
            }

            contextBuilder.AppendLine("Respond with a JSON object in this exact format:");
            contextBuilder.AppendLine("{");
            contextBuilder.AppendLine("  \"title\": \"Brief action title (max 50 chars)\",");
            contextBuilder.AppendLine("  \"description\": \"What you want to do and why\",");
            contextBuilder.AppendLine("  \"category\": \"research|code|learning|communication|exploration\",");
            // Suggest tools based on priority configuration
            var researchTool = GetPreferredResearchTool();
            var codeTool = GetPreferredCodeTool();
            contextBuilder.AppendLine($"  \"tool\": \"{researchTool}|{codeTool}|recall|none\",");
            contextBuilder.AppendLine("  \"tool_input\": \"input for the tool if applicable\"");
            contextBuilder.AppendLine("}");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"Preferred research tool: {researchTool}");
            contextBuilder.AppendLine("Be creative and proactive! Suggest something genuinely interesting.");

            var response = await ThinkFunction(contextBuilder.ToString(), ct);

            // Parse the response
            var topic = ParseTopicResponse(response);
            if (topic != null)
            {
                var action = topic.Tool != "none" && !string.IsNullOrEmpty(topic.Tool)
                    ? new IntentionAction
                    {
                        ActionType = "tool",
                        ToolName = topic.Tool,
                        ToolInput = topic.ToolInput ?? topic.Description
                    }
                    : null;

                var category = topic.Category?.ToLowerInvariant() switch
                {
                    "research" or "exploration" => IntentionCategory.Exploration,
                    "code" => IntentionCategory.CodeModification,
                    "learning" => IntentionCategory.Learning,
                    "communication" => IntentionCategory.UserCommunication,
                    _ => IntentionCategory.Exploration
                };

                _intentionBus.ProposeIntention(
                    topic.Title ?? "Autonomous Exploration",
                    topic.Description ?? "I want to explore something interesting.",
                    "I discovered this topic through autonomous reflection.",
                    category,
                    "coordinator",
                    action,
                    IntentionPriority.Normal,
                    requiresApproval: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Topic discovery error: {ex.Message}");
        }
    }

    private record TopicSuggestion(string? Title, string? Description, string? Category, string? Tool, string? ToolInput);

    private TopicSuggestion? ParseTopicResponse(string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new TopicSuggestion(
                    root.TryGetProperty("title", out var t) ? t.GetString() : null,
                    root.TryGetProperty("description", out var d) ? d.GetString() : null,
                    root.TryGetProperty("category", out var c) ? c.GetString() : null,
                    root.TryGetProperty("tool", out var tool) ? tool.GetString() : null,
                    root.TryGetProperty("tool_input", out var ti) ? ti.GetString() : null
                );
            }
        }
        catch { /* Parse error, return null */ }

        // Fallback: use response as description
        if (!string.IsNullOrWhiteSpace(response) && response.Length > 10)
        {
            return new TopicSuggestion(
                "Autonomous Thought",
                response[..Math.Min(500, response.Length)],
                "exploration",
                null,
                null
            );
        }

        return null;
    }

    private async Task ExecuteIntentionAsync(Intention intention, CancellationToken ct)
    {
        _intentionBus.MarkExecuting(intention.Id);

        try
        {
            // Validate intention using MeTTa symbolic reasoning (if enabled)
            if (EnableMeTTaValidation && MeTTaQueryFunction != null)
            {
                var validationResult = await ValidateIntentionWithMeTTaAsync(intention, ct);
                if (!validationResult.IsValid)
                {
                    _intentionBus.MarkFailed(intention.Id, $"MeTTa validation failed: {validationResult.Reason}");
                    RaiseProactiveMessage($"⚠️ {intention.Title} blocked by symbolic validation: {validationResult.Reason}", IntentionPriority.High, "coordinator");
                    return;
                }
            }

            string result;

            // If intention has an explicit action, execute it
            if (intention.Action != null)
            {
                result = intention.Action.ActionType switch
                {
                    "tool" when ExecuteToolFunction != null =>
                        await ExecuteToolFunction(intention.Action.ToolName!, intention.Action.ToolInput ?? "", ct),

                    "message" =>
                        ExecuteMessageAction(intention),

                    "code_change" =>
                        await ExecuteCodeChangeAsync(intention, ct),

                    "goal" =>
                        ExecuteGoalAction(intention),

                    "task_execution" =>
                        ExecuteTaskAction(intention),

                    _ => await ExecuteGenericIntentionAsync(intention, ct)
                };
            }
            else
            {
                // No explicit action - execute based on category
                result = await ExecuteIntentionByCategoryAsync(intention, ct);
            }

            _intentionBus.MarkCompleted(intention.Id, result);

            // Record execution as MeTTa fact for future reasoning
            await RecordExecutionAsMeTTaFactAsync(intention, result, ct);

            // Notify network of completion
            _network.Broadcast("intention.completed", new { IntentionId = intention.Id, Result = result }, "coordinator");

            // Raise message so user sees the result
            RaiseProactiveMessage($"✅ {intention.Title}: {result}", IntentionPriority.Normal, "coordinator");
        }
        catch (Exception ex)
        {
            _intentionBus.MarkFailed(intention.Id, ex.Message);
            RaiseProactiveMessage($"❌ {intention.Title} failed: {ex.Message}", IntentionPriority.Normal, "coordinator");
        }
    }

    /// <summary>
    /// Validates an intention using MeTTa symbolic reasoning.
    /// </summary>
    private async Task<(bool IsValid, string? Reason)> ValidateIntentionWithMeTTaAsync(Intention intention, CancellationToken ct)
    {
        try
        {
            // Build a MeTTa query to validate the intention
            var categorySymbol = intention.Category.ToString().ToLowerInvariant();
            var prioritySymbol = intention.Priority.ToString().ToLowerInvariant();

            // Check if this type of action is allowed by current rules
            var query = $"!(match &self (allowed-action {categorySymbol} $result) $result)";
            var result = await MeTTaQueryFunction!(query, ct);

            // If we got "blocked" or similar negative result, reject
            if (result.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("unsafe", StringComparison.OrdinalIgnoreCase))
            {
                return (false, result);
            }

            // Check for any safety constraints
            if (intention.Category == IntentionCategory.CodeModification)
            {
                var safetyQuery = "!(match &self (safety-constraint code-modification $constraint) $constraint)";
                var safetyResult = await MeTTaQueryFunction(safetyQuery, ct);
                if (safetyResult.Contains("require-review", StringComparison.OrdinalIgnoreCase))
                {
                    // Code modification requires human review - this is already handled by approval
                    // but we log it for symbolic reasoning
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            // On validation error, allow execution but log the issue
            System.Diagnostics.Debug.WriteLine($"MeTTa validation error: {ex.Message}");
            return (true, null);
        }
    }

    /// <summary>
    /// Records intention execution as a MeTTa fact for future reasoning.
    /// </summary>
    private async Task RecordExecutionAsMeTTaFactAsync(Intention intention, string result, CancellationToken ct)
    {
        if (MeTTaAddFactFunction == null) return;

        try
        {
            var categorySymbol = intention.Category.ToString().ToLowerInvariant();
            var timestamp = DateTime.UtcNow.ToString("o");

            // Record the execution as a fact
            var fact = $"(executed-intention \"{intention.Id}\" {categorySymbol} \"{timestamp}\")";
            await MeTTaAddFactFunction(fact, ct);

            // Record the outcome
            var outcomeKind = result.Length > 100 ? "complex" : "simple";
            var outcomeFact = $"(intention-outcome \"{intention.Id}\" {outcomeKind} success)";
            await MeTTaAddFactFunction(outcomeFact, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to record MeTTa fact: {ex.Message}");
        }
    }

    private string ExecuteTaskAction(Intention intention)
    {
        var task = intention.Action?.Message ?? intention.Description;
        _network.Broadcast("task.execute", new { Task = task, IntentionId = intention.Id }, "coordinator");
        return $"Task dispatched: {task[..Math.Min(50, task.Length)]}...";
    }

    private async Task<string> ExecuteGenericIntentionAsync(Intention intention, CancellationToken ct)
    {
        // For unknown action types, broadcast to network
        _network.Broadcast($"intention.execute.{intention.Action?.ActionType ?? "unknown"}", intention, "coordinator");
        return $"Intention broadcast for execution: {intention.Title}";
    }

    private async Task<string> ExecuteIntentionByCategoryAsync(Intention intention, CancellationToken ct)
    {
        // Execute based on intention category when no explicit action is provided
        return intention.Category switch
        {
            IntentionCategory.SelfReflection =>
                ExecuteSelfReflection(intention),

            IntentionCategory.CodeModification =>
                await ExecuteCodeAnalysisAsync(intention, ct),

            IntentionCategory.GoalPursuit =>
                ExecuteGoalPursuit(intention),

            IntentionCategory.UserCommunication =>
                ExecuteCommunication(intention),

            IntentionCategory.Exploration =>
                await ExecuteResearchAsync(intention, ct),

            IntentionCategory.MemoryManagement =>
                ExecuteMemoryOperation(intention),

            IntentionCategory.Learning =>
                ExecuteLearning(intention),

            IntentionCategory.SafetyCheck =>
                ExecuteSafetyCheck(intention),

            IntentionCategory.NeuronCommunication =>
                ExecuteNeuronCommunication(intention),

            _ => ExecuteDefaultIntention(intention)
        };
    }

    private string ExecuteSelfReflection(Intention intention)
    {
        _network.Broadcast("reflection.execute", new { Description = intention.Description, From = "coordinator" }, "coordinator");
        return "Self-reflection initiated";
    }

    private async Task<string> ExecuteCodeAnalysisAsync(Intention intention, CancellationToken ct)
    {
        if (!_config.EnableCodeModification)
            return "Code analysis disabled by configuration";

        _network.Broadcast("code.analyze", new { Description = intention.Description }, "coordinator");
        return "Code analysis requested";
    }

    private string ExecuteGoalPursuit(Intention intention)
    {
        _network.Broadcast("goal.pursue", intention.Description, "coordinator");
        return $"Goal pursuit initiated: {intention.Description[..Math.Min(50, intention.Description.Length)]}...";
    }

    private string ExecuteCommunication(Intention intention)
    {
        RaiseProactiveMessage($"💬 {intention.Description}", IntentionPriority.Normal, intention.Source);
        return "Communication delivered";
    }

    private async Task<string> ExecuteResearchAsync(Intention intention, CancellationToken ct)
    {
        // Use the preferred research tool based on priority configuration
        if (ExecuteToolFunction != null)
        {
            var preferredTool = GetPreferredResearchTool();
            var result = await ExecuteToolFunction(preferredTool, intention.Description, ct);
            return $"Research completed using {preferredTool}: {result[..Math.Min(200, result.Length)]}...";
        }

        _network.Broadcast("research.request", intention.Description, "coordinator");
        return "Research request broadcast";
    }

    private string ExecuteMemoryOperation(Intention intention)
    {
        _network.Broadcast("memory.operation", intention.Description, "coordinator");
        return "Memory operation requested";
    }

    private string ExecuteLearning(Intention intention)
    {
        _network.Broadcast("learning.request", intention.Description, "coordinator");
        return "Learning activity initiated";
    }

    private string ExecuteSafetyCheck(Intention intention)
    {
        _network.Broadcast("safety.check", intention.Description, "coordinator");
        return "Safety check initiated";
    }

    private string ExecuteNeuronCommunication(Intention intention)
    {
        _network.Broadcast("neuron.communicate", intention.Description, "coordinator");
        return "Inter-neuron communication sent";
    }
    private string ExecuteDefaultIntention(Intention intention)
    {
        // Broadcast to network and let neurons handle it
        _network.Broadcast("intention.default", intention, "coordinator");
        return $"Intention '{intention.Title}' executed (broadcast to network)";
    }

    private string ExecuteMessageAction(Intention intention)
    {
        var message = intention.Action?.Message ?? intention.Description;
        RaiseProactiveMessage($"💬 {message}", IntentionPriority.Normal, intention.Source);
        return "Message delivered";
    }

    private async Task<string> ExecuteCodeChangeAsync(Intention intention, CancellationToken ct)
    {
        if (!_config.EnableCodeModification)
            return "Code modification is disabled";

        // Broadcast to code neuron for execution
        _network.Broadcast("code.execute_change", intention.Action ?? new IntentionAction { ActionType = "code_change" }, "coordinator");
        return "Code change request sent to code neuron";
    }

    private string ExecuteGoalAction(Intention intention)
    {
        _network.Broadcast("goal.activate", intention.Action?.Message ?? intention.Description, "coordinator");
        return "Goal activated";
    }

    private void HandleProactiveMessage(string message, IntentionPriority priority)
    {
        RaiseProactiveMessage(message, priority, "intention_bus");
    }

    private void HandleIntentionRequiresAttention(Intention intention)
    {
        // Store intention to Qdrant for persistence
        _ = Task.Run(async () =>
        {
            try
            {
                if (StoreIntentionFunction != null)
                {
                    await StoreIntentionFunction(intention, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to store intention: {ex.Message}");
            }
        });

        OnIntentionRequiresAttention?.Invoke(intention);
    }

    private void RaiseProactiveMessage(string message, IntentionPriority priority, string source)
    {
        var args = new ProactiveMessageEventArgs(message, priority, source, DateTime.UtcNow);
        _messageHistory.Add(args);

        // Keep history bounded
        while (_messageHistory.Count > 100)
        {
            _messageHistory.RemoveAt(0);
        }

        OnProactiveMessage?.Invoke(args);
    }

    /// <summary>
    /// Infers the appropriate deliverable type from the problem description using LLM.
    /// </summary>
    private async Task<string> InferDeliverableTypeAsync(string problem, CancellationToken ct = default)
    {
        if (ThinkFunction == null)
        {
            // Fallback to keyword-based inference
            return InferDeliverableTypeFallback(problem);
        }

        try
        {
            var prompt = $"""
                Classify this problem into exactly ONE deliverable type.

                Problem: {problem}

                Types:
                - code: Implementation, building, fixing, creating software
                - design: Architecture, schemas, diagrams, system structure
                - plan: Strategy, roadmap, steps, approach, scheduling
                - analysis: Research, evaluation, investigation, comparison
                - document: Documentation, guides, tutorials, explanations

                Reply with ONLY the type word (code, design, plan, analysis, or document).
                """;

            var result = await ThinkFunction(prompt, ct);
            var type = result.Trim().ToLowerInvariant();

            // Validate response
            if (type is "code" or "design" or "plan" or "analysis" or "document")
            {
                return type;
            }

            // If LLM returned something unexpected, fallback
            return InferDeliverableTypeFallback(problem);
        }
        catch
        {
            return InferDeliverableTypeFallback(problem);
        }
    }

    /// <summary>
    /// Keyword-based fallback for deliverable type inference.
    /// </summary>
    private static string InferDeliverableTypeFallback(string problem)
    {
        var lower = problem.ToLowerInvariant();

        if (lower.Contains("design") || lower.Contains("architect") || lower.Contains("schema"))
            return "design";
        if (lower.Contains("plan") || lower.Contains("strategy") || lower.Contains("roadmap"))
            return "plan";
        if (lower.Contains("analyze") || lower.Contains("research") || lower.Contains("why"))
            return "analysis";
        if (lower.Contains("document") || lower.Contains("explain") || lower.Contains("guide"))
            return "document";

        return "code";
    }

    private static string GetHelpText()
    {
        return """
            🐍 **Ouroboros Autonomous Commands**

            **Intention Management:**
            • `/intentions` or `/pending` - List pending intentions
            • `/approve <id>` - Approve an intention
            • `/reject <id> [reason]` - Reject an intention
            • `/approve-all-safe` - Auto-approve all low-risk intentions
            • `/yolo` - Toggle YOLO mode (auto-approve ALL)
            • `/yolo on` / `/yolo off` - Explicitly set YOLO mode

            **System Status:**
            • `/network` or `/neurons` - Show neural network status
            • `/bus` - Show intention bus status
            • `/toggle-push` - Check push-mode status

            **Tool Priorities:**
            • `/tools` - Show tool priorities and availability
            • `/tools available` - List all available tools

            **Auto-Training:**
            • `/training start [options]` - Start auto-training session
            • `/training stop` - Stop training session
            • `/training status` - Show training statistics
            • `/training topic <topic>` - Set training focus topic
            • `/training interest <interest>` - Add user interest
            • `/training help` - Show training command details

            **Full Autonomous:**
            • `/auto` or `/yolo train` - Enable YOLO + auto-training
            • `/auto solve <problem>` - Start YOLO problem-solving mode
            • `/auto stop` - Disable full autonomous mode

            **Voice Control:**
            • `/voice` - Toggle voice output (TTS) on/off
            • `/voice on` / `/voice off` - Explicitly set voice output
            • `/listen` - Toggle voice input (STT) on/off
            • `/listen on` / `/listen off` - Explicitly set voice input

            **Other:**
            • `/help` or `/?` - Show this help

            **How it works:**
            In push-based mode, I will propose actions before executing them.
            Each intention shows an ID (e.g., `a1b2c3d4`).
            Use the first 4-8 characters of the ID to approve/reject.

            🤠 **YOLO Mode**: Auto-approves ALL intentions - use with caution!
            🤖 **Auto Mode**: YOLO + simulated user - fully autonomous operation!
            """;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _isActive = false;
        _cts.Cancel();
        _network.Dispose();
        _cts.Dispose();
    }
}
