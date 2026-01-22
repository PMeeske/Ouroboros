// <copyright file="OuroborosAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.Persistence;
using Ouroboros.Network;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Diagnostics;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Ouroboros.Tools.MeTTa;
using Ouroboros.Application;
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using static Ouroboros.Application.Tools.AutonomousTools;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;
using Ouroboros.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the unified Ouroboros agent.
/// </summary>
public sealed record OuroborosConfig(
    string Persona = "Ouroboros",
    string Model = "deepseek-v3.1:671b-cloud",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string EmbedEndpoint = "http://localhost:11434",
    string QdrantEndpoint = "http://localhost:6334",
    string? ApiKey = null,
    bool Voice = false,
    bool VoiceOnly = false,
    bool LocalTts = true,
    bool AzureTts = false,
    string? AzureSpeechKey = null,
    string AzureSpeechRegion = "eastus",
    string TtsVoice = "en-US-AvaMultilingualNeural",
    bool VoiceChannel = false,
    bool Listen = false,
    bool Debug = false,
    double Temperature = 0.7,
    int MaxTokens = 2048,
    string? Culture = null,
    // Feature toggles - all enabled by default
    bool EnableSkills = true,
    bool EnableMeTTa = true,
    bool EnableTools = true,
    bool EnablePersonality = true,
    bool EnableMind = true,
    bool EnableBrowser = true,
    bool EnableConsciousness = true,
    // Autonomous/Push mode
    bool EnablePush = false,
    bool YoloMode = false,
    string AutoApproveCategories = "",
    int IntentionIntervalSeconds = 45,
    int DiscoveryIntervalSeconds = 90,
    // Governance & Self-Modification
    bool EnableSelfModification = false,
    string RiskLevel = "Medium",
    bool AutoApproveLow = true,
    // Additional config
    int ThinkingIntervalSeconds = 30,
    int AgentMaxSteps = 10,
    string? InitialGoal = null,
    string? InitialQuestion = null,
    string? InitialDsl = null,
    // Multi-model
    string? CoderModel = null,
    string? ReasonModel = null,
    string? SummarizeModel = null,
    // Piping & Batch mode
    bool PipeMode = false,
    string? BatchFile = null,
    bool JsonOutput = false,
    bool NoGreeting = false,
    bool ExitOnError = false,
    string? ExecCommand = null);

/// <summary>
/// Unified Ouroboros agent that integrates all capabilities:
/// - Voice interaction (TTS/STT)
/// - Skill-based learning
/// - MeTTa symbolic reasoning
/// - Dynamic tool creation
/// - Personality engine with affective states
/// - Self-improvement and curiosity
/// - Persistent thought memory across sessions
/// </summary>
public sealed class OuroborosAgent : IAsyncDisposable
{
    private readonly OuroborosConfig _config;
    private readonly VoiceModeService _voice;

    // Track active speech processes to kill on exit
    private static readonly ConcurrentBag<System.Diagnostics.Process> _activeSpeechProcesses = new();

    // Static configuration for Azure credentials (set from OuroborosCommands)
    private static Microsoft.Extensions.Configuration.IConfiguration? _staticConfiguration;

    // Static culture for TTS voice selection in static methods
    private static string? _staticCulture;

    /// <summary>
    /// Sets the configuration for Azure Speech and other services.
    /// </summary>
    public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _staticConfiguration = configuration;
    }

    /// <summary>
    /// Sets the culture for voice synthesis in static methods.
    /// </summary>
    public static void SetStaticCulture(string? culture)
    {
        _staticCulture = culture;
    }

    // Core AI components
    private IChatCompletionModel? _chatModel;
    private ToolAwareChatModel? _llm;
    private IEmbeddingModel? _embedding;
    private ToolRegistry _tools = new();

    // Agent capabilities
    private ISkillRegistry? _skills;
    private IMeTTaEngine? _mettaEngine;
    private DynamicToolFactory? _toolFactory;
    private IntelligentToolLearner? _toolLearner;
    private PersonalityEngine? _personalityEngine;
    private PersonalityProfile? _personality;
    private IValenceMonitor? _valenceMonitor;
    private MetaAIPlannerOrchestrator? _orchestrator;
    private AutonomousMind? _autonomousMind;
    private PlaywrightMcpTool? _playwrightTool;

    // Consciousness simulation via ImmersivePersona
    private ImmersivePersona? _immersivePersona;

    // Multi-model orchestration - routes tasks to specialized models
    private OrchestratedChatModel? _orchestratedModel;
    private DivideAndConquerOrchestrator? _divideAndConquer;
    private IChatCompletionModel? _coderModel;
    private IChatCompletionModel? _reasonModel;
    private IChatCompletionModel? _summarizeModel;

    // Network State Tracking - reifies Step execution into MerkleDag
    private NetworkStateTracker? _networkTracker;

    // Sub-Agent Orchestration - manages multiple agents for complex tasks
    private IDistributedOrchestrator? _distributedOrchestrator;
    private IEpicBranchOrchestrator? _epicOrchestrator;
    private readonly ConcurrentDictionary<string, SubAgentInstance> _subAgents = new();

    // Self-Model - metacognitive capabilities
    private IIdentityGraph? _identityGraph;
    private IGlobalWorkspace? _globalWorkspace;
    private IPredictiveMonitor? _predictiveMonitor;
    private ISelfEvaluator? _selfEvaluator;
    private ICapabilityRegistry? _capabilityRegistry;

    // Self-Execution - autonomous goal pursuit
    private readonly ConcurrentQueue<AutonomousGoal> _goalQueue = new();
    private Task? _selfExecutionTask;
    private CancellationTokenSource? _selfExecutionCts;
    private bool _selfExecutionEnabled;

    // Persistent thought memory - enables continuity across sessions
    private ThoughtPersistenceService? _thoughtPersistence;
    private List<InnerThought> _persistentThoughts = new();
    private string? _lastThoughtContent; // Last generated thought/learning for "save it" command

    // Autonomous/Push mode - proposes actions for user approval
    private AutonomousCoordinator? _autonomousCoordinator;
    private QdrantNeuralMemory? _neuralMemory;
    private Task? _pushModeTask;
    private CancellationTokenSource? _pushModeCts;

    // Self-code perception - always-on indexing of own codebase
    private QdrantSelfIndexer? _selfIndexer;

    // Self-assembly engine - runtime neuron composition
    private SelfAssemblyEngine? _selfAssemblyEngine;
    private BlueprintAnalyzer? _blueprintAnalyzer;
    private MeTTaBlueprintValidator? _blueprintValidator;

    // AGI warmup and presence detection - proactive interaction
    private AgiWarmup? _agiWarmup;
    private PresenceDetector? _presenceDetector;
    private bool _userWasPresent;
    private DateTime _lastGreetingTime = DateTime.MinValue;

    // Voice side channel - parallel audio playback for personas
    private VoiceSideChannel? _voiceSideChannel;

    // Speech recognition for voice input
    private CancellationTokenSource? _listeningCts;
    private Task? _listeningTask;
    private bool _isListening;

    // Input buffer for preserving typed text during proactive messages
    private readonly StringBuilder _currentInputBuffer = new();
    private readonly object _inputLock = new();
    private bool _isInConversationLoop;

    // State
    private readonly List<string> _conversationHistory = new();
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    /// Gets whether the agent is fully initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the voice service.
    /// </summary>
    public VoiceModeService Voice => _voice;

    /// <summary>
    /// Gets the voice side channel for fire-and-forget audio playback.
    /// </summary>
    public VoiceSideChannel? VoiceChannel => _voiceSideChannel;

    /// <summary>
    /// Gets the skill registry.
    /// </summary>
    public ISkillRegistry? Skills => _skills;

    /// <summary>
    /// Gets the personality engine.
    /// </summary>
    public PersonalityEngine? Personality => _personalityEngine;

    /// <summary>
    /// Strips tool results from text for voice output.
    /// Tool results like "[tool_name]: output" and "[TOOL-RESULT:...]" are removed.
    /// </summary>
    private static string StripToolResults(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Remove lines that match tool result patterns:
        // - [tool_name]: ...
        // - [TOOL-RESULT:tool_name] ...
        // - [propose_intention]: ...
        // - error: ...
        string[] lines = text.Split('\n');
        IEnumerable<string> filtered = lines.Where(line =>
        {
            string trimmed = line.Trim();
            // Skip lines starting with [something]:
            if (Regex.IsMatch(trimmed, @"^\[[\w_:-]+\]:?\s*"))
                return false;
            // Skip lines containing TOOL-RESULT
            if (trimmed.Contains("TOOL-RESULT", StringComparison.OrdinalIgnoreCase))
                return false;
            // Skip error lines
            if (trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        });

        return string.Join("\n", filtered).Trim();
    }

    /// <summary>
    /// Uses LLM to integrate tool results naturally into a conversational response.
    /// Converts raw tool output (tables, JSON, etc.) into natural language.
    /// </summary>
    private async Task<string> SanitizeToolResultsAsync(string originalResponse, string toolResults)
    {
        if (_chatModel == null || string.IsNullOrWhiteSpace(toolResults))
        {
            return $"{originalResponse}\n\n{toolResults}";
        }

        try
        {
            string prompt = $@"You are integrating tool results into a conversational response.

ORIGINAL RESPONSE:
{originalResponse}

TOOL RESULTS (raw data):
{toolResults}

TASK:
Rewrite the response to naturally incorporate the key information from the tool results.
- Convert tables and structured data into conversational summaries
- Highlight the most important/relevant findings
- Keep your personality and speaking style
- Don't say 'the tool returned' or 'according to the results' - just state the information naturally
- If there are errors or issues in the data, mention them conversationally
- Keep it concise - summarize large outputs, don't repeat everything verbatim

Write the integrated response:";

            string sanitized = await _chatModel.GenerateTextAsync(prompt);
            return string.IsNullOrWhiteSpace(sanitized) ? $"{originalResponse}\n\n{toolResults}" : sanitized;
        }
        catch
        {
            // Fallback to raw output if sanitization fails
            return $"{originalResponse}\n\n{toolResults}";
        }
    }

    /// <summary>
    /// Speaks text on the voice side channel (fire-and-forget, non-blocking).
    /// Uses the configured persona's voice. Tool results are omitted.
    /// </summary>
    public void Say(string text, string? persona = null)
    {
        if (_voiceSideChannel == null)
        {
            if (_config.Debug) Console.WriteLine("  [VoiceChannel] Not initialized");
            return;
        }

        if (!_voiceSideChannel.IsEnabled)
        {
            if (_config.Debug) Console.WriteLine("  [VoiceChannel] Not enabled (no synthesizer?)");
            return;
        }

        // Strip tool results from voice output
        var cleanText = StripToolResults(text);
        if (string.IsNullOrWhiteSpace(cleanText)) return;

        if (_config.Debug) Console.WriteLine($"  [VoiceChannel] Say: {cleanText[..Math.Min(50, cleanText.Length)]}...");
        _voiceSideChannel.Say(cleanText, persona ?? _config.Persona);
    }

    /// <summary>
    /// Speaks text with a specific persona's voice.
    /// </summary>
    public void SayAs(string persona, string text)
    {
        var cleanText = StripToolResults(text);
        if (!string.IsNullOrWhiteSpace(cleanText))
        {
            _voiceSideChannel?.Say(cleanText, persona);
        }
    }

    /// <summary>
    /// Speaks text and waits for completion (blocking).
    /// </summary>
    public async Task SayAndWaitAsync(string text, string? persona = null, CancellationToken ct = default)
    {
        var cleanText = StripToolResults(text);
        if (string.IsNullOrWhiteSpace(cleanText)) return;
        if (_voiceSideChannel == null) return;

        await _voiceSideChannel.SayAndWaitAsync(cleanText, persona ?? _config.Persona, ct);
    }

    /// <summary>
    /// Announces a system message (high priority).
    /// </summary>
    public void Announce(string text)
    {
        _voiceSideChannel?.Announce(text);
    }

    /// <summary>
    /// Starts listening for voice input using Azure Speech Recognition.
    /// </summary>
    public async Task StartListeningAsync()
    {
        if (_isListening) return;

        _listeningCts = new CancellationTokenSource();
        _isListening = true;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(GetLocalizedString("listening_start"));
        Console.ResetColor();

        _listeningTask = Task.Run(async () =>
        {
            try
            {
                await ListenLoopAsync(_listeningCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ⚠ Listening error: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                _isListening = false;
            }
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops listening for voice input.
    /// </summary>
    public void StopListening()
    {
        if (!_isListening) return;

        _listeningCts?.Cancel();
        _isListening = false;

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(GetLocalizedString("listening_stop"));
        Console.ResetColor();
    }

    /// <summary>
    /// Continuous listening loop using Azure Speech Recognition with optional Azure TTS response.
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken ct)
    {
        // Get Azure Speech credentials from environment or static configuration
        string? speechKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
                       ?? _staticConfiguration?["Azure:Speech:Key"]
                       ?? _config.AzureSpeechKey;
        string speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION")
                          ?? _staticConfiguration?["Azure:Speech:Region"]
                          ?? _config.AzureSpeechRegion
                          ?? "eastus";

        if (string.IsNullOrEmpty(speechKey))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(GetLocalizedString("voice_requires_key"));
            Console.ResetColor();
            return;
        }

        Microsoft.CognitiveServices.Speech.SpeechConfig config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(speechKey, speechRegion);

        // Set speech recognition language based on culture if available
        config.SpeechRecognitionLanguage = _config.Culture ?? "en-US";

        using Microsoft.CognitiveServices.Speech.SpeechRecognizer recognizer = new Microsoft.CognitiveServices.Speech.SpeechRecognizer(config);

        while (!ct.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  🎤 ");
            Console.ResetColor();

            Microsoft.CognitiveServices.Speech.SpeechRecognitionResult result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.RecognizedSpeech)
            {
                string text = result.Text.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                // Check for stop commands
                if (text.ToLowerInvariant().Contains("stop listening") ||
                    text.ToLowerInvariant().Contains("disable voice"))
                {
                    StopListening();
                    _autonomousCoordinator?.ProcessCommand("/listen off");
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  {GetLocalizedString("you_said")} {text}");
                Console.ResetColor();

                // Process as regular input
                string response = await ChatAsync(text);

                // Display response
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  {response}");
                Console.ResetColor();

                // Speak response using Azure TTS if enabled
                if (_config.AzureTts && !string.IsNullOrEmpty(speechKey))
                {
                    try
                    {
                        await SpeakResponseWithAzureTtsAsync(response, speechKey, speechRegion, ct);
                    }
                    catch (Exception ex)
                    {
                        if (_config.Debug)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  ⚠ Azure TTS error: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                }
            }
            else if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.NoMatch)
            {
                // No speech detected, continue listening
            }
            else if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.Canceled)
            {
                var cancellation = Microsoft.CognitiveServices.Speech.CancellationDetails.FromResult(result);
                if (cancellation.Reason == Microsoft.CognitiveServices.Speech.CancellationReason.Error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ⚠ Speech recognition error: {cancellation.ErrorDetails}");
                    Console.ResetColor();
                }
                break;
            }
        }
    }

    /// <summary>
    /// Speaks a response using Azure TTS with configured voice.
    /// </summary>
    private async Task SpeakResponseWithAzureTtsAsync(string text, string key, string region, CancellationToken ct)
    {
        try
        {
            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Auto-select voice based on culture (unless user explicitly set a non-default voice)
            var voiceName = GetEffectiveVoice();
            config.SpeechSynthesisVoiceName = voiceName;

            // Use default speaker
            var speechSynthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{(_config.Culture ?? "en-US")}'>
    <voice name='{voiceName}'>
        {System.Net.WebUtility.HtmlEncode(text)}
    </voice>
</speak>";

            var result = await speechSynthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason != Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted)
            {
                if (_config.Debug)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  [Azure TTS] Synthesis issue: {result.Reason}");
                    Console.ResetColor();
                }
            }

            speechSynthesizer?.Dispose();
        }
        catch (Exception ex)
        {
            if (_config.Debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [Azure TTS] Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Creates a new Ouroboros agent instance.
    /// </summary>
    public OuroborosAgent(OuroborosConfig config)
    {
        _config = config;
        _voice = new VoiceModeService(new VoiceModeConfig(
            Persona: config.Persona,
            VoiceOnly: config.VoiceOnly,
            LocalTts: config.LocalTts,
            VoiceLoop: true,
            DisableStt: true, // Disable Whisper STT - use /listen for Azure speech recognition
            Model: config.Model,
            Endpoint: config.Endpoint,
            EmbedModel: config.EmbedModel,
            QdrantEndpoint: config.QdrantEndpoint,
            Culture: config.Culture));

        // Register process exit handler to kill speech processes on forceful exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAllSpeechProcesses();
        Console.CancelKeyPress += (_, _) => KillAllSpeechProcesses();
    }

    /// <summary>
    /// Initializes all agent subsystems.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Set static culture for TTS in static methods
        SetStaticCulture(_config.Culture);

        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          🐍 OUROBOROS - Unified AI Agent System           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        // Print feature configuration
        PrintFeatureStatus();

        // Initialize voice
        if (_config.Voice)
        {
            await _voice.InitializeAsync();
        }

        // Initialize voice side channel if enabled (independent of main voice)
        if (_config.VoiceChannel)
        {
            await InitializeVoiceSideChannelAsync();
        }

        // Initialize LLM (always required)
        await InitializeLlmAsync();

        // Wire up LLM-based voice sanitization if voice channel is enabled
        if (_config.VoiceChannel && _voiceSideChannel != null && _chatModel != null)
        {
            _voiceSideChannel.SetLlmSanitizer(async (prompt, ct) =>
            {
                return await _chatModel.GenerateTextAsync(prompt, ct);
            });
            Console.WriteLine("  ✓ Voice LLM Sanitizer: Enabled (natural speech condensation)");
        }

        // Initialize embedding (always required for most features)
        await InitializeEmbeddingAsync();

        // Initialize Qdrant neural memory for persistent storage
        await InitializeNeuralMemoryAsync();

        // Initialize tools (conditionally)
        if (_config.EnableTools)
        {
            await InitializeToolsAsync();
        }
        else
        {
            _tools = ToolRegistry.CreateDefault();
            Console.WriteLine("  ○ Tools: Disabled (use --no-tools=false to enable)");
        }

        // Initialize MeTTa symbolic reasoning (conditionally)
        if (_config.EnableMeTTa)
        {
            await InitializeMeTTaAsync();
        }
        else
        {
            Console.WriteLine("  ○ MeTTa: Disabled (use --no-metta=false to enable)");
        }

        // Initialize skill registry (conditionally)
        if (_config.EnableSkills)
        {
            await InitializeSkillsAsync();
        }
        else
        {
            Console.WriteLine("  ○ Skills: Disabled (use --no-skills=false to enable)");
        }

        // Initialize personality engine (conditionally)
        if (_config.EnablePersonality)
        {
            await InitializePersonalityAsync();
        }
        else
        {
            Console.WriteLine("  ○ Personality: Disabled (use --no-personality=false to enable)");
        }

        // Initialize orchestrator (conditionally - needs skills)
        if (_config.EnableSkills)
        {
            await InitializeOrchestratorAsync();
        }

        // Initialize autonomous mind for inner thoughts and proactivity (conditionally)
        if (_config.EnableMind)
        {
            await InitializeAutonomousMindAsync();
        }
        else
        {
            Console.WriteLine("  ○ AutonomousMind: Disabled (use --no-mind=false to enable)");
        }

        // Initialize ImmersivePersona consciousness simulation (conditionally)
        if (_config.EnableConsciousness)
        {
            await InitializeConsciousnessAsync();
        }
        else
        {
            Console.WriteLine("  ○ Consciousness: Disabled (use --no-consciousness=false to enable)");
        }

        // Initialize persistent thought memory (always enabled for continuity)
        await InitializePersistentThoughtsAsync();

        // Initialize network state tracking (always enabled - reifies Steps into MerkleDag)
        await InitializeNetworkStateAsync();

        // Initialize self-code perception (always-on immersive self-awareness)
        await InitializeSelfIndexerAsync();

        // Initialize self-assembly engine (runtime neuron composition)
        await InitializeSelfAssemblyAsync();

        // Initialize sub-agent orchestration (always enabled for complex task delegation)
        await InitializeSubAgentOrchestrationAsync();

        // Initialize self-model for metacognition (always enabled)
        await InitializeSelfModelAsync();

        // Initialize self-execution capability (conditionally based on autonomous mind)
        if (_config.EnableMind)
        {
            await InitializeSelfExecutionAsync();
        }

        // Always initialize autonomous coordinator (for status, commands, network state)
        await InitializeAutonomousCoordinatorAsync();

        // Initialize Push/Autonomous mode (conditionally - starts the coordinator ticking)
        if (_config.EnablePush)
        {
            await StartPushModeAsync();
        }

        // Initialize presence detection for proactive interactions
        await InitializePresenceDetectorAsync();

        _isInitialized = true;

        Console.WriteLine("\n  ✓ Ouroboros fully initialized\n");
        PrintQuickHelp();

        // AGI warmup - prime the model with examples for autonomous operation
        await PerformAgiWarmupAsync();

        // Enforce policies if self-modification is enabled
        if (_config.EnableSelfModification)
        {
            await EnforceGovernancePoliciesAsync();
        }

        // Start listening for voice input if enabled via CLI
        if (_config.Listen)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  🎤 Voice listening enabled via --listen flag");
            Console.ResetColor();
            await StartListeningAsync();
        }
    }

    /// <summary>
    /// Enforce governance policies when self-modification is enabled.
    /// </summary>
    private async Task EnforceGovernancePoliciesAsync()
    {
        try
        {
            Console.WriteLine("\n  🔐 Enforcing governance policies...");

            var policyOpts = new PolicyOptions
            {
                Command = "enforce",
                Culture = _config.Culture,
                EnableSelfModification = true,
                RiskLevel = _config.RiskLevel,
                AutoApproveLow = _config.AutoApproveLow,
                Verbose = _config.Debug
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await PolicyCommands.RunPolicyAsync(policyOpts);
                    var output = writer.ToString();

                    // Show policy status
                    Console.SetOut(originalOut);
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine(output);
                        Console.ResetColor();
                    }
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [WARN] Policy enforcement warning: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Gets the language name for a given culture code.
    /// </summary>
    private string GetLanguageName(string culture)
    {
        return culture.ToLowerInvariant() switch
        {
            "de-de" => "German",
            "fr-fr" => "French",
            "es-es" => "Spanish",
            "it-it" => "Italian",
            "pt-br" => "Portuguese (Brazilian)",
            "pt-pt" => "Portuguese (European)",
            "nl-nl" => "Dutch",
            "sv-se" => "Swedish",
            "ja-jp" => "Japanese",
            "zh-cn" => "Chinese (Simplified)",
            "zh-tw" => "Chinese (Traditional)",
            "ko-kr" => "Korean",
            "ru-ru" => "Russian",
            "pl-pl" => "Polish",
            "tr-tr" => "Turkish",
            "ar-sa" => "Arabic",
            "he-il" => "Hebrew",
            "th-th" => "Thai",
            _ => culture
        };
    }

    /// <summary>
    /// Gets the default Azure TTS voice name for a given culture code.
    /// </summary>
    private static string GetDefaultVoiceForCulture(string? culture)
    {
        return culture?.ToLowerInvariant() switch
        {
            "de-de" => "de-DE-KatjaNeural",
            "fr-fr" => "fr-FR-DeniseNeural",
            "es-es" => "es-ES-ElviraNeural",
            "it-it" => "it-IT-ElsaNeural",
            "pt-br" => "pt-BR-FranciscaNeural",
            "pt-pt" => "pt-PT-RaquelNeural",
            "nl-nl" => "nl-NL-ColetteNeural",
            "sv-se" => "sv-SE-SofieNeural",
            "ja-jp" => "ja-JP-NanamiNeural",
            "zh-cn" => "zh-CN-XiaoxiaoNeural",
            "zh-tw" => "zh-TW-HsiaoChenNeural",
            "ko-kr" => "ko-KR-SunHiNeural",
            "ru-ru" => "ru-RU-SvetlanaNeural",
            "pl-pl" => "pl-PL-ZofiaNeural",
            "tr-tr" => "tr-TR-EmelNeural",
            "ar-sa" => "ar-SA-ZariyahNeural",
            "he-il" => "he-IL-HilaNeural",
            "th-th" => "th-TH-PremwadeeNeural",
            _ => "en-US-AvaMultilingualNeural"
        };
    }

    /// <summary>
    /// Gets the effective TTS voice, considering culture override.
    /// If culture is set and voice wasn't explicitly changed from default, use culture-specific voice.
    /// </summary>
    private string GetEffectiveVoice()
    {
        // If user didn't explicitly set a voice (still using default), auto-select based on culture
        if (_config.TtsVoice == "en-US-AvaMultilingualNeural" &&
            !string.IsNullOrEmpty(_config.Culture) &&
            _config.Culture != "en-US")
        {
            return GetDefaultVoiceForCulture(_config.Culture);
        }

        return _config.TtsVoice;
    }

    /// <summary>
    /// Translates a thought to the target language if culture is specified.
    /// </summary>
    private async Task<string> TranslateThoughtIfNeededAsync(string thought)
    {
        // Only translate if a non-English culture is set
        if (string.IsNullOrEmpty(_config.Culture) || _config.Culture == "en-US" || _llm == null)
        {
            return thought;
        }

        try
        {
            var languageName = GetLanguageName(_config.Culture);
            var translationPrompt = $@"TASK: Translate to {languageName}.
INPUT: {thought}
OUTPUT (translation only, no explanations, no JSON, no metadata):";

            var (translated, _) = await _llm.GenerateWithToolsAsync(translationPrompt);

            // Clean up any extra formatting the LLM might add
            var result = translated?.Trim() ?? thought;

            // Remove common LLM artifacts
            if (result.StartsWith("\"") && result.EndsWith("\""))
                result = result[1..^1];
            if (result.Contains("```"))
                result = result.Split("```")[0].Trim();
            if (result.Contains("{") && result.Contains("}"))
                result = result.Split("{")[0].Trim();

            return string.IsNullOrEmpty(result) ? thought : result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Thought Translation] Error: {ex.Message}");
            return thought;
        }
    }

    private void PrintFeatureStatus()
    {
        Console.WriteLine("  Configuration:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Model: {_config.Model}");
        Console.WriteLine($"    Persona: {_config.Persona}");
        var ttsMode = _config.AzureTts ? "✓ Azure (cloud)" : "○ Local (Windows)";
        Console.WriteLine($"    Voice: {(_config.Voice ? "✓ enabled" : "○ disabled")} - {ttsMode}");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("  Features (all enabled by default, use --no-X to disable):");
        Console.ForegroundColor = _config.EnableSkills ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableSkills ? "✓" : "○")} Skills       - Persistent learning with Qdrant");
        Console.ForegroundColor = _config.EnableMeTTa ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableMeTTa ? "✓" : "○")} MeTTa        - Symbolic reasoning engine");
        Console.ForegroundColor = _config.EnableTools ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableTools ? "✓" : "○")} Tools        - Web search, calculator, URL fetch");
        Console.ForegroundColor = _config.EnableBrowser ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableBrowser ? "✓" : "○")} Browser      - Playwright automation");
        Console.ForegroundColor = _config.EnablePersonality ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnablePersonality ? "✓" : "○")} Personality  - Affective states & traits");
        Console.ForegroundColor = _config.EnableMind ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableMind ? "✓" : "○")} Mind         - Autonomous inner thoughts");
        Console.ForegroundColor = _config.EnableConsciousness ? ConsoleColor.Green : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnableConsciousness ? "✓" : "○")} Consciousness- ImmersivePersona self-awareness");
        Console.ForegroundColor = _config.EnablePush ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
        Console.WriteLine($"    {(_config.EnablePush ? "⚡" : "○")} Push Mode    - Propose actions for approval (--push)");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void PrintQuickHelp()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Quick commands: 'help' | 'status' | 'skills' | 'tools' | 'exit'");
        Console.WriteLine("  Say or type anything to chat. Use [TOOL:name args] to call tools.\n");
        Console.ResetColor();
    }

    private Task InitializeVoiceSideChannelAsync()
    {
        try
        {
            _voiceSideChannel = new VoiceSideChannel(maxQueueSize: 15);
            _voiceSideChannel.SetDefaultPersona(_config.Persona);

            // Wire up to TTS - always use Windows SAPI for side channel
            // This ensures distinct persona voices via different SAPI voices
            _voiceSideChannel.SetSynthesizer(async (text, voice, ct) =>
            {
                // Always use SAPI directly to get persona-specific voices
                // The main _voice service is used for the primary conversation;
                // side channel uses different Windows voices for variety
                await SpeakWithSapiAsync(text, voice, ct);
            });

            // Subscribe to events for debugging/logging
            _voiceSideChannel.MessageSpoken += (_, msg) =>
            {
                if (_config.Debug)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  🔊 [{msg.PersonaName}] spoke: {msg.Text[..Math.Min(50, msg.Text.Length)]}...");
                    Console.ResetColor();
                }
            };

            Console.WriteLine($"  ✓ Voice Side Channel: {_config.Persona} (parallel playback enabled)");
        }
        catch (InvalidOperationException opEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ✗ Voice Side Channel: Configuration error - {opEx.Message}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ✗ Voice Side Channel: {ex.GetType().Name} - {ex.Message}");
            if (_config.Debug)
            {
                Console.WriteLine($"    → Voice mode will continue without parallel playback");
            }
            Console.ResetColor();
        }

        return Task.CompletedTask;
    }

    private static async Task SpeakWithSapiAsync(string text, PersonaVoice voice, CancellationToken ct)
    {
        // Try Azure TTS first (higher quality, Cortana-like voices)
        // Check user secrets first, then environment variables
        var azureKey = _staticConfiguration?["Azure:Speech:Key"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var azureRegion = _staticConfiguration?["Azure:Speech:Region"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");

        if (!string.IsNullOrEmpty(azureKey) && !string.IsNullOrEmpty(azureRegion))
        {
            if (await SpeakWithAzureTtsAsync(text, voice, azureKey, azureRegion, ct))
                return;
        }

        // Fallback to Windows SAPI
        await SpeakWithWindowsSapiAsync(text, voice, ct);
    }

    private static async Task<bool> SpeakWithAzureTtsAsync(string text, PersonaVoice voice, string key, string region, CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"  [Azure TTS] Speaking as {voice.PersonaName}: {text[..Math.Min(40, text.Length)]}...");

            var config = Microsoft.CognitiveServices.Speech.SpeechConfig.FromSubscription(key, region);

            // Check if culture override is set
            var culture = _staticCulture ?? "en-US";
            var isGerman = culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase);

            // Select Azure Neural voice based on culture and persona
            string azureVoice;
            if (isGerman)
            {
                // German voices for all personas
                azureVoice = voice.PersonaName.ToUpperInvariant() switch
                {
                    "OUROBOROS" => "de-DE-KatjaNeural",   // German female (Cortana-like)
                    "ARIA" => "de-DE-AmalaNeural",        // German expressive female
                    "ECHO" => "de-AT-IngridNeural",       // Austrian German female
                    "SAGE" => "de-DE-KatjaNeural",        // German calm female
                    "ATLAS" => "de-DE-ConradNeural",      // German male
                    "SYSTEM" => "de-DE-KatjaNeural",      // System messages
                    "USER" => "de-DE-ConradNeural",       // User persona - male
                    "USER_PERSONA" => "de-DE-ConradNeural",
                    _ => "de-DE-KatjaNeural"
                };
            }
            else
            {
                // English voices (default)
                azureVoice = voice.PersonaName.ToUpperInvariant() switch
                {
                    "OUROBOROS" => "en-US-JennyNeural",    // Cortana-like voice!
                    "ARIA" => "en-US-AriaNeural",          // Expressive female
                    "ECHO" => "en-GB-SoniaNeural",         // UK female
                    "SAGE" => "en-US-SaraNeural",          // Calm female
                    "ATLAS" => "en-US-GuyNeural",          // Male
                    "SYSTEM" => "en-US-JennyNeural",       // System messages
                    "USER" => "en-US-GuyNeural",           // User persona - male (distinct from Jenny)
                    "USER_PERSONA" => "en-US-GuyNeural",
                    _ => "en-US-JennyNeural"
                };
            }

            config.SpeechSynthesisVoiceName = azureVoice;

            // Use mythic SSML styling for Cortana-like voices (Jenny or Katja)
            var useFriendlyStyle = azureVoice.Contains("Jenny") || azureVoice.Contains("Katja");
            var ssml = useFriendlyStyle
                ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{culture}'>
                    <voice name='{azureVoice}'>
                        <mstts:express-as style='friendly' styledegree='0.8'>
                            <prosody rate='-5%' pitch='+8%' volume='+3%'>
                                <mstts:audioduration value='1.1'/>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                        <mstts:audioeffect type='eq_car'/>
                    </voice>
                </speak>"
                : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{culture}'>
                    <voice name='{azureVoice}'>
                        <prosody rate='0%'>{System.Security.SecurityElement.Escape(text)}</prosody>
                    </voice>
                </speak>";

            using var synthesizer = new Microsoft.CognitiveServices.Speech.SpeechSynthesizer(config);

            var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"  [Azure TTS] Done");
                return true;
            }

            if (result.Reason == Microsoft.CognitiveServices.Speech.ResultReason.Canceled)
            {
                var cancellation = Microsoft.CognitiveServices.Speech.SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"  [Azure TTS Error] {cancellation.ErrorDetails}");
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Azure TTS Exception] {ex.Message}");
            return false; // Fall back to SAPI
        }
    }

    private static async Task SpeakWithWindowsSapiAsync(string text, PersonaVoice voice, CancellationToken ct)
    {
        try
        {
            // Use Windows Speech via PowerShell with persona-specific rate/pitch
            var escapedText = text
                .Replace("'", "''")
                .Replace("\r", " ")
                .Replace("\n", " ");

            // Convert persona rate (0.5-1.5) to SAPI rate (-5 to +5)
            var rate = (int)((voice.Rate - 1.0f) * 10);

            // Select voice based on persona - use different voices for variety
            // Available voices depend on system - check with GetInstalledVoices()
            // Common: Microsoft David (male), Microsoft Zira (female), Microsoft Hedda (German female)
            var voiceSelector = voice.PersonaName.ToUpperInvariant() switch
            {
                "OUROBOROS" => "'Zira'",     // Default: Zira (US female) - closest to Cortana available
                "ARIA" => "'Zira'",          // Female voice
                "ECHO" => "'Hazel'",         // UK female
                "SAGE" => "'Hedda'",         // German female
                "ATLAS" => "'David'",        // David with rate adjustment
                "SYSTEM" => "'Zira'",        // System announcements
                "USER" => "'David'",         // User persona - David (US male, distinct from Zira)
                "USER_PERSONA" => "'David'", // User persona alternate key
                _ => "'Zira'"                // Default fallback
            };

            var script = $@"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = $synth.GetInstalledVoices() | Where-Object {{ $_.VoiceInfo.Culture.Name -like 'en-*' }}
$targetNames = @({voiceSelector})
$selectedVoice = $null
foreach ($target in $targetNames) {{
    $match = $voices | Where-Object {{ $_.VoiceInfo.Name -like ""*$target*"" }} | Select-Object -First 1
    if ($match) {{ $selectedVoice = $match; break }}
}}
if ($selectedVoice) {{ $synth.SelectVoice($selectedVoice.VoiceInfo.Name) }}
elseif ($voices.Count -gt 0) {{ $synth.SelectVoice($voices[0].VoiceInfo.Name) }}
$synth.Rate = {Math.Clamp(rate, -10, 10)}
$synth.Volume = {voice.Volume}
$synth.Speak('{escapedText}')
$synth.Dispose()
";
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.Start();

            // Track the process so we can kill it on exit
            _activeSpeechProcesses.Add(process);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancelled
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw;
            }
            finally
            {
                // Remove from tracking (best effort - ConcurrentBag doesn't have Remove)
            }
        }
        catch
        {
            // Silently fail if SAPI not available
        }
    }

    private async Task InitializeLlmAsync()
    {
        try
        {
            var settings = new ChatRuntimeSettings(_config.Temperature, _config.MaxTokens, 120, false);
            var endpoint = _config.Endpoint.TrimEnd('/');

            // Determine API key - check config, then environment variables
            var apiKey = _config.ApiKey
                ?? Environment.GetEnvironmentVariable("CHAT_API_KEY")
                ?? Environment.GetEnvironmentVariable("OLLAMA_API_KEY")
                ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            // Check endpoint type
            bool isOllamaCloud = endpoint.Contains("api.ollama.com", StringComparison.OrdinalIgnoreCase);
            bool isDeepSeek = endpoint.Contains("deepseek.com", StringComparison.OrdinalIgnoreCase);
            bool isLocalOllama = endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                              || endpoint.Contains("127.0.0.1");

            if (isOllamaCloud)
            {
                // Ollama Cloud - uses OllamaCloudChatModel with API key
                _chatModel = new OllamaCloudChatModel(endpoint, apiKey ?? "", _config.Model, settings);
                Console.WriteLine($"  ✓ LLM: {_config.Model} @ Ollama Cloud");
            }
            else if (isDeepSeek)
            {
                // DeepSeek API - OpenAI compatible
                _chatModel = new HttpOpenAiCompatibleChatModel(endpoint, apiKey ?? "", _config.Model, settings);
                Console.WriteLine($"  ✓ LLM: {_config.Model} @ DeepSeek");
            }
            else if (isLocalOllama)
            {
                // Local Ollama
                _chatModel = new OllamaCloudChatModel(endpoint, "ollama", _config.Model, settings);
                Console.WriteLine($"  ✓ LLM: {_config.Model} @ {endpoint} (local)");
            }
            else
            {
                // Generic OpenAI-compatible API
                _chatModel = new HttpOpenAiCompatibleChatModel(endpoint, apiKey ?? "", _config.Model, settings);
                Console.WriteLine($"  ✓ LLM: {_config.Model} @ {endpoint}");
            }

            // Test connection
            var testResponse = await _chatModel.GenerateTextAsync("Respond with just: OK");
            if (string.IsNullOrWhiteSpace(testResponse) || testResponse.Contains("-fallback:"))
            {
                Console.WriteLine($"  ⚠ LLM: {_config.Model} (limited mode)");
            }

            // Initialize multi-model orchestration if specialized models are configured
            await InitializeMultiModelOrchestrationAsync(settings, endpoint, apiKey, isLocalOllama);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ LLM unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes multi-model orchestration for routing tasks to specialized models.
    /// </summary>
    private async Task InitializeMultiModelOrchestrationAsync(
        ChatRuntimeSettings settings,
        string endpoint,
        string? apiKey,
        bool isLocalOllama)
    {
        try
        {
            // Check if any specialized models are configured
            bool hasSpecializedModels = !string.IsNullOrEmpty(_config.CoderModel)
                                     || !string.IsNullOrEmpty(_config.ReasonModel)
                                     || !string.IsNullOrEmpty(_config.SummarizeModel);

            if (!hasSpecializedModels || _chatModel == null)
            {
                Console.WriteLine("  ○ Multi-model: Using single model (specify --coder-model, --reason-model, or --summarize-model to enable)");
                return;
            }

            // Helper to create a model
            IChatCompletionModel CreateModel(string modelName)
            {
                if (isLocalOllama)
                    return new OllamaCloudChatModel(endpoint, "ollama", modelName, settings);
                return new HttpOpenAiCompatibleChatModel(endpoint, apiKey ?? "", modelName, settings);
            }

            // Create specialized models
            if (!string.IsNullOrEmpty(_config.CoderModel))
                _coderModel = CreateModel(_config.CoderModel);

            if (!string.IsNullOrEmpty(_config.ReasonModel))
                _reasonModel = CreateModel(_config.ReasonModel);

            if (!string.IsNullOrEmpty(_config.SummarizeModel))
                _summarizeModel = CreateModel(_config.SummarizeModel);

            // Build orchestrated chat model using OrchestratorBuilder
            var builder = new OrchestratorBuilder(_tools, "general")
                .WithModel(
                    "general",
                    _chatModel,
                    ModelType.General,
                    new[] { "conversation", "general-purpose", "versatile", "chat" },
                    maxTokens: _config.MaxTokens,
                    avgLatencyMs: 1000);

            if (_coderModel != null)
            {
                builder.WithModel(
                    "coder",
                    _coderModel,
                    ModelType.Code,
                    new[] { "code", "programming", "debugging", "syntax", "refactor", "implement" },
                    maxTokens: _config.MaxTokens,
                    avgLatencyMs: 1500);
            }

            if (_reasonModel != null)
            {
                builder.WithModel(
                    "reasoner",
                    _reasonModel,
                    ModelType.Reasoning,
                    new[] { "reasoning", "analysis", "logic", "explanation", "planning", "strategy" },
                    maxTokens: _config.MaxTokens,
                    avgLatencyMs: 1200);
            }

            if (_summarizeModel != null)
            {
                builder.WithModel(
                    "summarizer",
                    _summarizeModel,
                    ModelType.General,
                    new[] { "summarize", "condense", "extract", "tldr", "brief" },
                    maxTokens: _config.MaxTokens,
                    avgLatencyMs: 800);
            }

            builder.WithMetricTracking(true);
            _orchestratedModel = builder.Build();

            var modelCount = 1 + (_coderModel != null ? 1 : 0) + (_reasonModel != null ? 1 : 0) + (_summarizeModel != null ? 1 : 0);
            Console.WriteLine($"  ✓ Multi-model: Orchestration enabled ({modelCount} models)");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    General: {_config.Model}");
            if (_coderModel != null) Console.WriteLine($"    Coder: {_config.CoderModel}");
            if (_reasonModel != null) Console.WriteLine($"    Reasoner: {_config.ReasonModel}");
            if (_summarizeModel != null) Console.WriteLine($"    Summarizer: {_config.SummarizeModel}");
            Console.ResetColor();

            // Initialize divide-and-conquer orchestrator for large input processing
            var dcConfig = new DivideAndConquerConfig(
                MaxParallelism: Math.Max(2, Environment.ProcessorCount / 2),
                ChunkSize: 1000,
                MergeResults: true,
                MergeSeparator: "\n\n");
            _divideAndConquer = new DivideAndConquerOrchestrator(_orchestratedModel, dcConfig);
            Console.WriteLine($"  ✓ Divide-and-Conquer: Parallel processing enabled (parallelism={dcConfig.MaxParallelism})");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Multi-model orchestration failed: {ex.Message}");
        }
    }

    private async Task InitializeEmbeddingAsync()
    {
        // Embedding models to try, in order of preference
        var modelsToTry = new[]
        {
            _config.EmbedModel,                    // User's configured model first
            "mxbai-embed-large",                   // Best quality (1024 dim, 512 tokens)
            "nomic-embed-text",                    // Good balance (768 dim, 8192 tokens)
            "snowflake-arctic-embed:335m",         // High quality (1024 dim)
            "all-minilm",                          // Fast, small (384 dim)
            "bge-m3",                              // Multilingual (1024 dim)
        }.Distinct().ToArray();

        var embedEndpoint = _config.EmbedEndpoint.TrimEnd('/');
        var provider = new OllamaProvider(embedEndpoint);

        foreach (var modelName in modelsToTry)
        {
            try
            {
                var embedModel = new OllamaEmbeddingModel(provider, modelName);
                _embedding = new OllamaEmbeddingAdapter(embedModel);

                // Test embedding
                var testEmbed = await _embedding.CreateEmbeddingsAsync("test");
                Console.WriteLine($"  ✓ Embeddings: {modelName} @ {embedEndpoint} (dim={testEmbed.Length})");
                return; // Success!
            }
            catch (Exception ex)
            {
                if (modelName == _config.EmbedModel)
                {
                    // Only show warning for user's configured model
                    Console.WriteLine($"  ⚠ {modelName}: {ex.Message.Split('\n')[0]}");
                }
                _embedding = null;
            }
        }

        Console.WriteLine("  ⚠ Embeddings unavailable: No working model found. Try: ollama pull mxbai-embed-large");
    }

    private async Task InitializeNeuralMemoryAsync()
    {
        if (_embedding == null || string.IsNullOrEmpty(_config.QdrantEndpoint))
        {
            Console.WriteLine("  ○ Neural Memory: Disabled (requires embeddings + Qdrant)");
            return;
        }

        try
        {
            // Extract REST endpoint (port 6333) from gRPC endpoint (port 6334)
            var qdrantRest = _config.QdrantEndpoint.Replace(":6334", ":6333");
            _neuralMemory = new QdrantNeuralMemory(qdrantRest);

            // Wire up embedding function
            _neuralMemory.EmbedFunction = async (text, ct) =>
            {
                return await _embedding.CreateEmbeddingsAsync(text);
            };

            // Get embedding dimension from test
            var testEmbed = await _embedding.CreateEmbeddingsAsync("test");
            await _neuralMemory.InitializeAsync(testEmbed.Length);

            // Get stats
            var stats = await _neuralMemory.GetStatsAsync();
            Console.WriteLine($"  ✓ Neural Memory: Qdrant @ {qdrantRest}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    Messages: {stats.NeuronMessagesCount} | Intentions: {stats.IntentionsCount} | Memories: {stats.MemoriesCount}");
            Console.ResetColor();
        }
        catch (HttpRequestException httpEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Neural Memory: Connection failed - {httpEx.Message}");
            Console.WriteLine($"    → Check if Qdrant is running at {_config.QdrantEndpoint}");
            Console.ResetColor();
            _neuralMemory = null;
        }
        catch (TimeoutException timeoutEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Neural Memory: Timeout - {timeoutEx.Message}");
            Console.WriteLine($"    → Qdrant may be overloaded or starting up");
            Console.ResetColor();
            _neuralMemory = null;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Neural Memory: {ex.GetType().Name} - {ex.Message}");
            if (_config.Debug)
            {
                Console.WriteLine($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
            }
            Console.ResetColor();
            _neuralMemory = null;
        }
    }

    private async Task InitializeToolsAsync()
    {
        try
        {
            // Start with default tools + autonomous tools (Firecrawl, etc.)
            _tools = ToolRegistry.CreateDefault()
                .WithAutonomousTools();

            if (_chatModel != null)
            {
                // Create temporary tool-aware LLM for bootstrapping dynamic tools
                var tempLlm = new ToolAwareChatModel(_chatModel, _tools);

                // Initialize dynamic tool factory with temporary LLM
                _toolFactory = new DynamicToolFactory(tempLlm);

                // Add built-in dynamic tools
                _tools = _tools
                    .WithTool(_toolFactory.CreateWebSearchTool("duckduckgo"))
                    .WithTool(_toolFactory.CreateUrlFetchTool())
                    .WithTool(_toolFactory.CreateCalculatorTool());

                // Add Qdrant admin tool for self-managing neuro-symbolic memory
                if (!string.IsNullOrEmpty(_config.QdrantEndpoint))
                {
                    var qdrantRest = _config.QdrantEndpoint.Replace(":6334", ":6333");
                    Func<string, CancellationToken, Task<float[]>>? embedFunc = null;
                    if (_embedding != null)
                    {
                        embedFunc = async (text, ct) => await _embedding.CreateEmbeddingsAsync(text, ct);
                    }
                    var qdrantAdmin = new QdrantAdminTool(qdrantRest, embedFunc);
                    _tools = _tools.WithTool(qdrantAdmin);
                    Console.WriteLine("  ✓ Qdrant Admin: Self-management tool registered");
                }

                // Add Playwright MCP tool for browser automation (if enabled)
                if (_config.EnableBrowser)
                {
                    try
                    {
                        _playwrightTool = new PlaywrightMcpTool();
                        await _playwrightTool.InitializeAsync();
                        _tools = _tools.WithTool(_playwrightTool);
                        Console.WriteLine($"  ✓ Playwright: Browser automation ready ({_playwrightTool.AvailableTools.Count} tools)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠ Playwright: Not available ({ex.Message})");
                    }
                }
                else
                {
                    Console.WriteLine("  ○ Playwright: Disabled (use --no-browser=false to enable)");
                }

                // NOW create the final ToolAwareChatModel with ALL tools registered
                _llm = new ToolAwareChatModel(_chatModel, _tools);

                // Re-initialize dynamic tool factory with final LLM
                _toolFactory = new DynamicToolFactory(_llm);

                // Initialize intelligent tool learner if embedding available
                if (_embedding != null)
                {
                    _mettaEngine = new InMemoryMeTTaEngine();
                    _toolLearner = new IntelligentToolLearner(
                        _toolFactory,
                        _mettaEngine,
                        _embedding,
                        _llm,
                        _config.QdrantEndpoint);

                    await _toolLearner.InitializeAsync();
                    var stats = _toolLearner.GetStats();
                    Console.WriteLine($"  ✓ Tool Learner: {stats.TotalPatterns} patterns (GA+MeTTa)");
                }
                else
                {
                    Console.WriteLine($"  ✓ Tools: {_tools.Count} registered");
                }

                // Add self-introspection tools (search_my_code, read_my_file, etc.)
                foreach (ITool tool in SystemAccessTools.CreateAllTools())
                {
                    _tools = _tools.WithTool(tool);
                }
                Console.WriteLine($"  ✓ Self-Introspection: search_my_code, modify_my_code, etc. registered");

                // Add Roslyn code analysis tools
                foreach (ITool tool in RoslynAnalyzerTools.CreateAllTools())
                {
                    _tools = _tools.WithTool(tool);
                }
                Console.WriteLine($"  ✓ Roslyn: analyze_csharp_code, create_csharp_class, etc. registered");
            }
            else
            {
                Console.WriteLine($"  ✓ Tools: {_tools.Count} (static only)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Tool factory failed: {ex.Message}");
        }
    }

    private async Task InitializeMeTTaAsync()
    {
        try
        {
            _mettaEngine ??= new InMemoryMeTTaEngine();
            await Task.CompletedTask; // Engine is sync-initialized
            Console.WriteLine("  ✓ MeTTa: Symbolic reasoning engine ready");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ MeTTa unavailable: {ex.Message}");
        }
    }

    private async Task InitializeSkillsAsync()
    {
        try
        {
            if (_embedding != null)
            {
                // Try Qdrant-backed persistent skills
                try
                {
                    var qdrantConfig = new QdrantSkillConfig { ConnectionString = _config.QdrantEndpoint };
                    var qdrantSkills = new QdrantSkillRegistry(_embedding, qdrantConfig);
                    await qdrantSkills.InitializeAsync();
                    _skills = qdrantSkills;
                    var stats = qdrantSkills.GetStats();
                    Console.WriteLine($"  ✓ Skills: Qdrant persistent storage ({stats.TotalSkills} skills loaded)");
                }
                catch (HttpRequestException qdrantConnEx)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ Qdrant skills: Connection failed - {qdrantConnEx.Message}");
                    Console.ResetColor();
                    _skills = new SkillRegistry(_embedding);
                    Console.WriteLine("  ✓ Skills: In-memory with embeddings (fallback)");
                }
                catch (Exception qdrantEx)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ Qdrant skills failed: {qdrantEx.GetType().Name} - {qdrantEx.Message}");
                    Console.ResetColor();
                    _skills = new SkillRegistry(_embedding);
                    Console.WriteLine("  ✓ Skills: In-memory with embeddings (fallback)");
                }
            }
            else
            {
                _skills = new SkillRegistry();
                Console.WriteLine("  ✓ Skills: In-memory basic");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Skills critical failure: {ex.GetType().Name} - {ex.Message}");
            Console.ResetColor();
            // Create minimal fallback to prevent null reference
            _skills = new SkillRegistry();
        }
    }

    private async Task InitializePersonalityAsync()
    {
        try
        {
            var metta = new InMemoryMeTTaEngine();

            if (_embedding != null && !string.IsNullOrEmpty(_config.QdrantEndpoint))
            {
                _personalityEngine = new PersonalityEngine(metta, _embedding, _config.QdrantEndpoint);
            }
            else
            {
                _personalityEngine = new PersonalityEngine(metta);
            }

            await _personalityEngine.InitializeAsync();

            // Get personality traits from voice persona
            var persona = _voice.ActivePersona;
            _personality = _personalityEngine.GetOrCreateProfile(
                persona.Name,
                persona.Traits,
                persona.Moods,
                persona.CoreIdentity);

            Console.WriteLine($"  ✓ Personality: {persona.Name} ({_personality.Traits.Count} traits)");

            // Initialize valence monitor for affective state tracking
            _valenceMonitor = new ValenceMonitor();
            Console.WriteLine("  ✓ Valence monitor initialized");
        }
        catch (ArgumentException argEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Personality configuration error: {argEx.Message}");
            Console.ResetColor();
        }
        catch (InvalidOperationException opEx)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Personality engine state error: {opEx.Message}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ⚠ Personality engine failed: {ex.GetType().Name} - {ex.Message}");
            if (_config.Debug)
            {
                Console.WriteLine($"    Stack: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
            }
            Console.ResetColor();
        }
    }

    private async Task InitializeOrchestratorAsync()
    {
        try
        {
            if (_chatModel != null && _embedding != null && _skills != null)
            {
                var memory = new MemoryStore(_embedding, new TrackedVectorStore());
                var safety = new SafetyGuard();

                var builder = new MetaAIBuilder()
                    .WithLLM(_chatModel)
                    .WithTools(_tools)
                    .WithEmbedding(_embedding)
                    .WithSkillRegistry(_skills)
                    .WithSafetyGuard(safety)
                    .WithMemoryStore(memory);

                _orchestrator = builder.Build();
                Console.WriteLine("  ✓ Orchestrator: Meta-AI planner ready");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Orchestrator unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes ImmersivePersona consciousness simulation for self-awareness,
    /// inner dialog, and emotional processing.
    /// </summary>
    private async Task InitializeConsciousnessAsync()
    {
        try
        {
            // Create ImmersivePersona with consciousness systems
            _immersivePersona = new ImmersivePersona(
                _config.Persona,
                _mettaEngine ?? new InMemoryMeTTaEngine(),
                _embedding,
                _config.QdrantEndpoint);

            // Subscribe to consciousness shift events
            _immersivePersona.ConsciousnessShift += (_, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"\n  [consciousness] Emotional shift: {e.NewEmotion} (Δ arousal: {e.ArousalChange:+0.00;-0.00})");
                Console.ResetColor();
            };

            // Awaken the persona
            await _immersivePersona.AwakenAsync();
            Console.WriteLine($"  ✓ Consciousness: ImmersivePersona '{_config.Persona}' awakened");

            // Display initial consciousness state
            PrintConsciousnessState();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Consciousness unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays the current consciousness state of the ImmersivePersona.
    /// </summary>
    private void PrintConsciousnessState()
    {
        if (_immersivePersona == null) return;

        var consciousness = _immersivePersona.Consciousness;
        var selfAwareness = _immersivePersona.SelfAwareness;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Emotional state: {consciousness.DominantEmotion} (arousal={consciousness.Arousal:F2}, valence={consciousness.Valence:F2})");
        Console.WriteLine($"    Self-awareness: {selfAwareness.Name} - {selfAwareness.CurrentMood}");
        Console.WriteLine($"    Identity: {_immersivePersona.Identity.Name} (uptime: {_immersivePersona.Uptime:hh\\:mm\\:ss})");
        Console.ResetColor();
    }

    private async Task InitializePersistentThoughtsAsync()
    {
        try
        {
            // Create a unique session ID based on persona name (allows continuity across restarts)
            var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
            var thoughtsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros",
                "thoughts");

            // Try Qdrant neuro-symbolic storage first, fall back to file-based
            try
            {
                // Create embedding function using our embedding model
                Func<string, Task<float[]>>? embeddingFunc = null;
                if (_embedding != null)
                {
                    embeddingFunc = async (text) =>
                    {
                        var result = await _embedding.CreateEmbeddingsAsync(text);
                        return result;
                    };
                }

                _thoughtPersistence = await ThoughtPersistenceService.CreateWithQdrantAsync(
                    sessionId,
                    _config.QdrantEndpoint,
                    embeddingFunc);

                Console.WriteLine("  ✓ Neuro-Symbolic Thought Map: Qdrant-backed with semantic search");
            }
            catch (Exception qdrantEx)
            {
                // Fall back to file-based storage
                System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Qdrant unavailable: {qdrantEx.Message}, using file storage");
                _thoughtPersistence = ThoughtPersistenceService.CreateWithFilePersistence(sessionId, thoughtsDir);
                Console.WriteLine("  ✓ Persistent Memory: File-based (Qdrant unavailable)");
            }

            // Load recent thoughts from previous sessions
            _persistentThoughts = (await _thoughtPersistence.GetRecentAsync(50)).ToList();

            if (_persistentThoughts.Count > 0)
            {
                Console.WriteLine($"  ✓ Persistent Memory: {_persistentThoughts.Count} thoughts recalled from previous sessions");

                // Show a brief summary of what we remember
                var thoughtTypes = _persistentThoughts
                    .GroupBy(t => t.Type)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => $"{g.Key}:{g.Count()}");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    Thought types: {string.Join(", ", thoughtTypes)}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("  ✓ Persistent Memory: Ready (first session)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Persistent memory unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists a new thought to storage for future sessions.
    /// Uses neuro-symbolic relations when Qdrant is available.
    /// </summary>
    private async Task PersistThoughtAsync(InnerThought thought, string? topic = null)
    {
        if (_thoughtPersistence == null) return;

        try
        {
            // Try to use neuro-symbolic persistence with automatic relation inference
            var neuroStore = _thoughtPersistence.AsNeuroSymbolicStore();
            if (neuroStore != null)
            {
                var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
                var persisted = ToPersistedThought(thought, topic);
                await neuroStore.SaveWithRelationsAsync(sessionId, persisted, autoInferRelations: true);
            }
            else
            {
                await _thoughtPersistence.SaveAsync(thought, topic);
            }

            _persistentThoughts.Add(thought);

            // Keep only the most recent 100 thoughts in memory
            if (_persistentThoughts.Count > 100)
            {
                _persistentThoughts.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtPersistence] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the result of a thought execution (action taken, response generated, etc).
    /// </summary>
    private async Task PersistThoughtResultAsync(
        Guid thoughtId,
        string resultType,
        string content,
        bool success,
        double confidence,
        TimeSpan? executionTime = null)
    {
        if (_thoughtPersistence == null) return;

        var neuroStore = _thoughtPersistence.AsNeuroSymbolicStore();
        if (neuroStore == null) return;

        try
        {
            var sessionId = $"ouroboros-{_config.Persona.ToLowerInvariant()}";
            var result = new Ouroboros.Domain.Persistence.ThoughtResult(
                Id: Guid.NewGuid(),
                ThoughtId: thoughtId,
                ResultType: resultType,
                Content: content,
                Success: success,
                Confidence: confidence,
                CreatedAt: DateTime.UtcNow,
                ExecutionTime: executionTime);

            await neuroStore.SaveResultAsync(sessionId, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThoughtResult] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts an InnerThought to a PersistedThought.
    /// </summary>
    private static Ouroboros.Domain.Persistence.PersistedThought ToPersistedThought(InnerThought thought, string? topic)
    {
        string? metadataJson = null;
        if (thought.Metadata != null && thought.Metadata.Count > 0)
        {
            try
            {
                metadataJson = System.Text.Json.JsonSerializer.Serialize(thought.Metadata);
            }
            catch
            {
                // Ignore
            }
        }

        return new Ouroboros.Domain.Persistence.PersistedThought
        {
            Id = thought.Id,
            Type = thought.Type.ToString(),
            Content = thought.Content,
            Confidence = thought.Confidence,
            Relevance = thought.Relevance,
            Timestamp = thought.Timestamp,
            Origin = thought.Origin.ToString(),
            Priority = thought.Priority.ToString(),
            ParentThoughtId = thought.ParentThoughtId,
            TriggeringTrait = thought.TriggeringTrait,
            Topic = topic,
            Tags = thought.Tags,
            MetadataJson = metadataJson,
        };
    }

    /// <summary>
    /// Initializes network state tracking with Qdrant persistence and MeTTa symbolic export.
    /// </summary>
    private async Task InitializeNetworkStateAsync()
    {
        try
        {
            _networkTracker = new NetworkStateTracker();

            // Configure Qdrant persistence for DAG nodes/edges
            if (!string.IsNullOrEmpty(_config.QdrantEndpoint))
            {
                try
                {
                    Func<string, Task<float[]>>? embeddingFunc = null;
                    if (_embedding != null)
                    {
                        embeddingFunc = async (text) => await _embedding.CreateEmbeddingsAsync(text);
                    }

                    var dagConfig = new Ouroboros.Network.QdrantDagConfig(
                        Endpoint: _config.QdrantEndpoint,
                        NodesCollection: "ouroboros_dag_nodes",
                        EdgesCollection: "ouroboros_dag_edges",
                        VectorSize: 768); // Match nomic-embed-text

                    var dagStore = new Ouroboros.Network.QdrantDagStore(dagConfig, embeddingFunc);
                    await dagStore.InitializeAsync();
                    _networkTracker.ConfigureQdrantPersistence(dagStore, autoPersist: true);

                    Console.WriteLine("  ✓ NetworkState: Merkle-DAG reification with Qdrant persistence");
                }
                catch (Exception qdrantEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[NetworkState] Qdrant DAG storage unavailable: {qdrantEx.Message}");
                    Console.WriteLine("  ✓ NetworkState: Merkle-DAG reification (in-memory only)");
                }
            }
            else
            {
                Console.WriteLine("  ✓ NetworkState: Merkle-DAG reification (in-memory only)");
            }

            // Configure MeTTa symbolic export if MeTTa engine is available
            if (_mettaEngine != null)
            {
                _networkTracker.ConfigureMeTTaExport(_mettaEngine, autoExport: true);
                Console.WriteLine("    ✓ MeTTa symbolic export enabled (DAG facts → MeTTa)");
            }

            // Subscribe to reification events for logging
            _networkTracker.BranchReified += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[NetworkState] Branch '{args.BranchName}' reified: {args.NodesCreated} nodes");
            };

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ NetworkState initialization failed: {ex.Message}");
            // Fall back to basic tracker
            _networkTracker = new NetworkStateTracker();
        }
    }

    /// <summary>
    /// Initializes self-code perception - always-on indexing of own codebase.
    /// Enables semantic search over Ouroboros's own source code.
    /// </summary>
    private async Task InitializeSelfIndexerAsync()
    {
        if (_embedding == null)
        {
            Console.WriteLine("  ○ SelfIndex: Skipped (no embedding model)");
            return;
        }

        try
        {
            // Find workspace root (go up from bin folder)
            var currentDir = AppContext.BaseDirectory;
            var workspaceRoot = currentDir;

            // Navigate up to find the solution root (contains .sln or src folder)
            for (int i = 0; i < 6; i++)
            {
                var parent = Directory.GetParent(workspaceRoot);
                if (parent == null) break;
                workspaceRoot = parent.FullName;

                if (Directory.GetFiles(workspaceRoot, "*.sln").Length > 0 ||
                    Directory.Exists(Path.Combine(workspaceRoot, "src")))
                {
                    break;
                }
            }

            var indexerConfig = new QdrantIndexerConfig
            {
                QdrantEndpoint = _config.QdrantEndpoint,
                CollectionName = "ouroboros_selfindex",
                HashCollectionName = "ouroboros_filehashes",
                RootPaths = new List<string> { Path.Combine(workspaceRoot, "src") },
                EnableFileWatcher = true, // Live updates on file changes
                ChunkSize = 800,
                ChunkOverlap = 150
            };

            _selfIndexer = new QdrantSelfIndexer(_embedding, indexerConfig);
            _selfIndexer.OnFileIndexed += (file, chunks) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SelfIndex] {Path.GetFileName(file)}: {chunks} chunks");
            };

            await _selfIndexer.InitializeAsync();

            // Wire to SystemAccessTools for tool access
            SystemAccessTools.SharedIndexer = _selfIndexer;

            var stats = await _selfIndexer.GetStatsAsync();
            Console.WriteLine($"  ✓ SelfIndex: {stats.IndexedFiles} files, {stats.TotalVectors} chunks (live watching)");

            // Run incremental index in background (don't block startup)
            _ = Task.Run(async () =>
            {
                try
                {
                    var progress = await _selfIndexer.IncrementalIndexAsync();
                    if (progress.ProcessedFiles > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SelfIndex] Incremental: {progress.ProcessedFiles} files, {progress.IndexedChunks} chunks");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SelfIndex] Incremental failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ SelfIndex: {ex.Message}");
            _selfIndexer = null;
        }
    }

    /// <summary>
    /// Initializes the self-assembly engine for runtime neuron composition.
    /// Enables the agent to identify capability gaps and assemble new neurons.
    /// </summary>
    private async Task InitializeSelfAssemblyAsync()
    {
        try
        {
            // Configure self-assembly with safety constraints
            var config = new SelfAssemblyConfig
            {
                AutoApprovalEnabled = _config.YoloMode, // Only auto-approve in YOLO mode
                AutoApprovalThreshold = 0.95,
                MinSafetyScore = 0.8,
                MaxAssembledNeurons = 10,
                ForbiddenCapabilities = new HashSet<NeuronCapability>
                {
                    NeuronCapability.FileAccess, // Never allow file access
                },
                SandboxTimeout = TimeSpan.FromSeconds(30),
            };

            _selfAssemblyEngine = new SelfAssemblyEngine(config);

            // Wire up MeTTa validation
            _blueprintValidator = new MeTTaBlueprintValidator();
            if (_mettaEngine != null)
            {
                _blueprintValidator.MeTTaExecutor = async (expr, ct) =>
                {
                    try
                    {
                        var result = await _mettaEngine.ExecuteQueryAsync(expr, ct);
                        return result.Match(s => s, e => "False");
                    }
                    catch
                    {
                        return "False"; // Fail safely
                    }
                };
            }

            _selfAssemblyEngine.SetMeTTaValidator(async blueprint =>
                await _blueprintValidator.ValidateAsync(blueprint));

            // Wire up LLM-based code generation
            if (_llm != null)
            {
                _selfAssemblyEngine.SetCodeGenerator(async blueprint =>
                    await GenerateNeuronCodeAsync(blueprint));
            }

            // Wire up approval callback (prompts user in non-YOLO mode)
            _selfAssemblyEngine.SetApprovalCallback(async proposal =>
                await RequestSelfAssemblyApprovalAsync(proposal));

            // Wire up events
            _selfAssemblyEngine.NeuronAssembled += OnNeuronAssembled;
            _selfAssemblyEngine.AssemblyFailed += OnAssemblyFailed;

            // Initialize blueprint analyzer if we have a neural network
            if (_autonomousCoordinator?.Network != null)
            {
                _blueprintAnalyzer = new BlueprintAnalyzer(_autonomousCoordinator.Network);
                if (_llm != null)
                {
                    _blueprintAnalyzer.LlmAnalyzer = async (prompt, ct) =>
                        await _llm.InnerModel.GenerateTextAsync(prompt, ct);
                }
            }

            Console.WriteLine($"  ✓ SelfAssembly: Enabled (YOLO={_config.YoloMode}, max {config.MaxAssembledNeurons} neurons)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ SelfAssembly: {ex.Message}");
            _selfAssemblyEngine = null;
        }
    }

    /// <summary>
    /// Initializes presence detection for proactive interaction.
    /// Detects when user is nearby via input activity, network, or camera.
    /// </summary>
    private async Task InitializePresenceDetectorAsync()
    {
        try
        {
            var config = new PresenceConfig
            {
                CheckIntervalSeconds = 5,
                PresenceThreshold = 0.5, // More sensitive to detect user
                UseWifi = true,
                UseCamera = false, // Disabled by default for privacy
                UseInputActivity = true,
                InputIdleThresholdSeconds = 180, // 3 minutes idle = probably away
            };

            _presenceDetector = new PresenceDetector(config);

            _presenceDetector.OnPresenceDetected += async evt =>
            {
                await HandlePresenceDetectedAsync(evt);
            };

            _presenceDetector.OnAbsenceDetected += evt =>
            {
                _userWasPresent = false;
                System.Diagnostics.Debug.WriteLine($"[Presence] User absence detected via {evt.Source}");
            };

            _presenceDetector.OnStateChanged += (oldState, newState) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Presence] State changed: {oldState} → {newState}");
            };

            // Start monitoring (non-blocking)
            _presenceDetector.Start();
            _userWasPresent = true; // Assume user is present at startup

            // Wire to SkillCliSteps for CLI access
            SkillCliSteps.SharedPresenceDetector = _presenceDetector;

            Console.WriteLine($"  ✓ Presence Detection: Active (WiFi + Input, interval={config.CheckIntervalSeconds}s)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Presence Detection: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles presence detection - greets user proactively if push mode enabled.
    /// </summary>
    private async Task HandlePresenceDetectedAsync(PresenceEvent evt)
    {
        System.Diagnostics.Debug.WriteLine($"[Presence] User presence detected via {evt.Source} (confidence={evt.Confidence:P0})");

        // Only proactively greet if:
        // 1. Push mode is enabled
        // 2. User was previously absent (state changed)
        // 3. Haven't greeted recently (avoid spam)
        var shouldGreet = _config.EnablePush &&
                          !_userWasPresent &&
                          (DateTime.UtcNow - _lastGreetingTime).TotalMinutes > 5 &&
                          evt.Confidence > 0.6;

        _userWasPresent = true;

        if (shouldGreet)
        {
            _lastGreetingTime = DateTime.UtcNow;

            // Generate a contextual greeting
            var greeting = await GeneratePresenceGreetingAsync(evt);

            // Notify via AutonomousMind's proactive channel
            if (_autonomousMind != null && !_autonomousMind.SuppressProactiveMessages)
            {
                // Fire proactive message event
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  👋 {greeting}");
                Console.ResetColor();

                // Speak the greeting
                await _voice.WhisperAsync(greeting);

                // If in conversation loop, restore prompt
                if (_isInConversationLoop)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("\n  You: ");
                    Console.ResetColor();
                }
            }
        }
    }

    /// <summary>
    /// Generates a contextual greeting when user presence is detected.
    /// </summary>
    private async Task<string> GeneratePresenceGreetingAsync(PresenceEvent evt)
    {
        var defaultGreeting = GetLocalizedString("Welcome back! I'm here if you need anything.");

        if (_chatModel == null)
        {
            return defaultGreeting;
        }

        try
        {
            var context = evt.TimeSinceLastState.HasValue
                ? $"The user was away for {evt.TimeSinceLastState.Value.TotalMinutes:F0} minutes."
                : "The user just arrived.";

            // Add language directive if culture is set
            var languageDirective = GetLanguageDirective();

            var prompt = $@"{languageDirective}You are a helpful AI assistant named Ouroboros. {context}
Generate a brief, warm, contextual greeting (1-2 sentences).
Be friendly but not overly enthusiastic. Don't mention detecting them via sensors.
If they were away a while, you might mention being ready to help or having kept an eye on things.";

            var greeting = await _chatModel.GenerateTextAsync(prompt, CancellationToken.None);
            return greeting?.Trim() ?? defaultGreeting;
        }
        catch
        {
            return defaultGreeting;
        }
    }

    /// <summary>
    /// Performs AGI warmup at startup - primes the model with examples for autonomous operation.
    /// </summary>
    private async Task PerformAgiWarmupAsync()
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n  ⏳ Warming up AGI systems...");
            Console.ResetColor();

            _agiWarmup = new AgiWarmup(
                thinkFunction: _autonomousMind?.ThinkFunction,
                searchFunction: _autonomousMind?.SearchFunction,
                executeToolFunction: _autonomousMind?.ExecuteToolFunction,
                selfIndexer: _selfIndexer,
                toolRegistry: _tools);

            _agiWarmup.OnProgress += (step, percent) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"\r  ⏳ {step} ({percent}%)".PadRight(60));
                Console.ResetColor();
            };

            var result = await _agiWarmup.WarmupAsync();

            Console.WriteLine(); // Clear progress line

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ AGI warmup complete in {result.Duration.TotalSeconds:F1}s");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    Thinking: {(result.ThinkingReady ? "✓" : "○")} | " +
                                  $"Search: {(result.SearchReady ? "✓" : "○")} | " +
                                  $"Tools: {result.ToolsSuccessCount}/{result.ToolsTestedCount} | " +
                                  $"Self-Aware: {(result.SelfAwarenessReady ? "✓" : "○")}");
                Console.ResetColor();

                // Seed thoughts are now available for autonomous exploration
                if (result.SeedThoughts.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[Warmup] {result.SeedThoughts.Count} seed thoughts primed for autonomous exploration");
                }

                // Print initial thought if available
                if (!string.IsNullOrEmpty(result.WarmupThought))
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    var translatedThought = await TranslateThoughtIfNeededAsync(result.WarmupThought);
                    Console.WriteLine($"\n  💭 Initial thought: \"{translatedThought}\"");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⚠ AGI warmup limited: {result.Error ?? "Some features unavailable"}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ○ AGI warmup: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Generates neuron code from a blueprint using LLM.
    /// </summary>
    private async Task<string> GenerateNeuronCodeAsync(NeuronBlueprint blueprint)
    {
        if (_llm == null)
        {
            throw new InvalidOperationException("LLM not available for code generation");
        }

        var prompt = $@"Generate a C# neuron class that implements Ouroboros.Domain.Autonomous.Neuron.

BLUEPRINT:
Name: {blueprint.Name}
Description: {blueprint.Description}
Rationale: {blueprint.Rationale}
Type: {blueprint.Type}
Subscribed Topics: {string.Join(", ", blueprint.SubscribedTopics)}
Capabilities: {string.Join(", ", blueprint.Capabilities)}

MESSAGE HANDLERS:
{string.Join("\n", blueprint.MessageHandlers.Select(h => $"- Topic '{h.TopicPattern}': {h.HandlingLogic} (responds={h.SendsResponse}, broadcasts={h.BroadcastsResult})"))}

{(blueprint.HasAutonomousTick ? $"AUTONOMOUS TICK: {blueprint.TickBehaviorDescription}" : "No autonomous tick behavior")}

REQUIREMENTS:
1. Class must inherit from Ouroboros.Domain.Autonomous.Neuron
2. Override: Id, Name, Type, SubscribedTopics, ProcessMessageAsync
3. Use SendMessage() to broadcast, SendResponse() to reply
4. Use ProposeIntention() for autonomous actions
5. Be safe - no file system, no network manipulation
6. Include XML documentation

Generate ONLY the C# code, no explanations:";

        var response = await _llm.InnerModel.GenerateTextAsync(prompt, CancellationToken.None);

        // Extract code from markdown if present
        var code = response;
        if (response.Contains("```csharp"))
        {
            var start = response.IndexOf("```csharp") + 9;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                code = response[start..end].Trim();
            }
        }
        else if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start)
            {
                code = response[start..end].Trim();
            }
        }

        // Ensure required using statements
        if (!code.Contains("using Ouroboros.Domain.Autonomous"))
        {
            code = "using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing Ouroboros.Domain.Autonomous;\n\n" + code;
        }

        return code;
    }

    /// <summary>
    /// Requests user approval for a self-assembly proposal.
    /// </summary>
    private async Task<bool> RequestSelfAssemblyApprovalAsync(
        AssemblyProposal proposal)
    {
        var blueprint = proposal.Blueprint;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           🧬 SELF-ASSEMBLY PROPOSAL                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        Console.WriteLine($"\n  Neuron: {blueprint.Name}");
        Console.WriteLine($"  Description: {blueprint.Description}");
        Console.WriteLine($"  Rationale: {blueprint.Rationale}");
        Console.WriteLine($"  Type: {blueprint.Type}");
        Console.WriteLine($"  Topics: {string.Join(", ", blueprint.SubscribedTopics)}");
        Console.WriteLine($"  Capabilities: {string.Join(", ", blueprint.Capabilities)}");
        Console.WriteLine($"  Confidence: {blueprint.ConfidenceScore:P0}");

        Console.ForegroundColor = proposal.Validation.SafetyScore >= 0.8
            ? ConsoleColor.Green
            : ConsoleColor.Yellow;
        Console.WriteLine($"  Safety Score: {proposal.Validation.SafetyScore:P0}");
        Console.ResetColor();

        if (proposal.Validation.Violations.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  Violations: {string.Join(", ", proposal.Validation.Violations)}");
            Console.ResetColor();
        }

        if (proposal.Validation.Warnings.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  Warnings: {string.Join(", ", proposal.Validation.Warnings)}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.Write("  Approve this self-assembly? [y/N]: ");

        var response = await Task.Run(() => Console.ReadLine());
        return response?.Trim().ToLowerInvariant() is "y" or "yes";
    }

    private void OnNeuronAssembled(object? sender, NeuronAssembledEvent e)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  🧬 SELF-ASSEMBLED: {e.NeuronName} (Type: {e.NeuronType.Name})");
        Console.ResetColor();

        // Create and register the neuron instance
        if (_selfAssemblyEngine is not null)
        {
            var instanceResult = _selfAssemblyEngine.CreateNeuronInstance(e.NeuronName);
            if (instanceResult.IsSuccess && instanceResult.Value is Neuron neuron)
            {
                _autonomousCoordinator?.Network?.RegisterNeuron(neuron);
                neuron.Start();
            }
        }

        // Log to conversation
        _conversationHistory.Add($"[SYSTEM] Self-assembled neuron: {e.NeuronName}");
    }

    private void OnAssemblyFailed(object? sender, AssemblyFailedEvent e)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ⚠ Assembly failed for '{e.NeuronName}': {e.Reason}");
        Console.ResetColor();
    }

    /// <summary>
    /// Analyzes the system for capability gaps and proposes new neurons.
    /// Can be called periodically or on-demand.
    /// </summary>
    public async Task<IReadOnlyList<NeuronBlueprint>> AnalyzeAndProposeNeuronsAsync(CancellationToken ct = default)
    {
        if (_blueprintAnalyzer == null || _selfAssemblyEngine == null)
        {
            return [];
        }

        try
        {
            // Get recent messages from the network
            var recentMessages = new List<NeuronMessage>();
            // In a real implementation, we'd query the message history

            var gaps = await _blueprintAnalyzer.AnalyzeGapsAsync(recentMessages, ct);
            var blueprints = new List<NeuronBlueprint>();

            foreach (var gap in gaps.Where(g => g.Importance >= 0.6))
            {
                var blueprint = await _blueprintAnalyzer.GenerateBlueprintForGapAsync(gap, ct);
                if (blueprint != null)
                {
                    blueprints.Add(blueprint);
                }
            }

            return blueprints;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelfAssembly] Analysis failed: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Attempts to assemble a neuron from a blueprint.
    /// </summary>
    public async Task<Neuron?> AssembleNeuronAsync(NeuronBlueprint blueprint, CancellationToken ct = default)
    {
        if (_selfAssemblyEngine == null)
        {
            throw new InvalidOperationException("Self-assembly engine not initialized");
        }

        var proposalResult = await _selfAssemblyEngine.SubmitBlueprintAsync(blueprint);
        if (!proposalResult.IsSuccess)
        {
            return null;
        }

        // Wait for the pipeline to complete (async in background)
        await Task.Delay(100, ct); // Small delay to allow pipeline to start

        // Check if deployed
        var neurons = _selfAssemblyEngine.GetAssembledNeurons();
        if (neurons.TryGetValue(blueprint.Name, out var neuronType))
        {
            var instance = _selfAssemblyEngine.CreateNeuronInstance(blueprint.Name);
            return instance.IsSuccess ? instance.Value : null;
        }

        return null;
    }

    /// <summary>
    /// Builds context from persistent thoughts for injection into prompts.
    /// </summary>
    private string BuildPersistentThoughtContext()
    {
        if (_persistentThoughts.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n[PERSISTENT MEMORY - Your thoughts from previous sessions]");

        // Group by type and show the most relevant/recent ones
        var recentThoughts = _persistentThoughts
            .OrderByDescending(t => t.Timestamp)
            .Take(10);

        foreach (var thought in recentThoughts)
        {
            var age = DateTime.UtcNow - thought.Timestamp;
            var ageStr = age.TotalHours < 1 ? $"{age.TotalMinutes:F0}m ago"
                       : age.TotalDays < 1 ? $"{age.TotalHours:F0}h ago"
                       : $"{age.TotalDays:F0}d ago";

            sb.AppendLine($"  [{thought.Type}] ({ageStr}): {thought.Content}");
        }

        sb.AppendLine("[END PERSISTENT MEMORY]\n");
        return sb.ToString();
    }

    private async Task InitializeAutonomousMindAsync()
    {
        try
        {
            _autonomousMind = new AutonomousMind();

            // Set culture for localization
            if (!string.IsNullOrEmpty(_config.Culture))
            {
                _autonomousMind.Culture = _config.Culture;
            }

            // Configure thinking capability - use orchestrated model if available
            _autonomousMind.ThinkFunction = async (prompt, token) =>
            {
                // Add language directive if culture is specified
                var actualPrompt = prompt;
                if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
                {
                    var languageName = GetLanguageName(_config.Culture);
                    actualPrompt = $"LANGUAGE: Respond ONLY in {languageName}. No English.\n\n{prompt}";
                }

                return await GenerateWithOrchestrationAsync(actualPrompt, token);
            };

            // Configure search capability
            _autonomousMind.SearchFunction = async (query, token) =>
            {
                var searchTool = _toolFactory?.CreateWebSearchTool("duckduckgo");
                if (searchTool != null)
                {
                    var result = await searchTool.InvokeAsync(query, token);
                    return result.Match(s => s, _ => "");
                }
                return "";
            };

            // Configure tool execution
            _autonomousMind.ExecuteToolFunction = async (toolName, input, token) =>
            {
                var tool = _tools.Get(toolName);
                if (tool != null)
                {
                    var result = await tool.InvokeAsync(input, token);
                    return result.Match(s => s, e => $"Error: {e}");
                }
                return "Tool not found";
            };

            // Configure pipe command execution - allows inner thoughts to execute piped commands
            _autonomousMind.ExecutePipeCommandFunction = async (pipeCommand, token) =>
            {
                try
                {
                    // Use the agent's pipe processing capability
                    return await ProcessInputWithPipingAsync(pipeCommand);
                }
                catch (Exception ex)
                {
                    return $"Pipe execution failed: {ex.Message}";
                }
            };

            // Configure output sanitization - converts raw tool output to natural language
            _autonomousMind.SanitizeOutputFunction = async (rawOutput, token) =>
            {
                if (_chatModel == null || string.IsNullOrWhiteSpace(rawOutput))
                    return rawOutput;

                try
                {
                    string prompt = $@"Summarize this tool output in ONE brief, natural sentence (max 50 words).
No markdown, no technical details, just the key insight:

{rawOutput}";

                    string sanitized = await _chatModel.GenerateTextAsync(prompt, token);
                    return string.IsNullOrWhiteSpace(sanitized) ? rawOutput : sanitized.Trim();
                }
                catch
                {
                    return rawOutput;
                }
            };

            // Wire up limitation-busting tools with LLM functions
            VerifyClaimTool.SearchFunction = _autonomousMind.SearchFunction;
            VerifyClaimTool.EvaluateFunction = async (prompt, token) =>
                _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
            ReasoningChainTool.ReasonFunction = async (prompt, token) =>
                _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
            ParallelToolsTool.ExecuteToolFunction = _autonomousMind.ExecuteToolFunction;
            CompressContextTool.SummarizeFunction = async (prompt, token) =>
                _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
            SelfDoubtTool.CritiqueFunction = async (prompt, token) =>
                _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
            ParallelMeTTaThinkTool.OllamaFunction = async (prompt, token) =>
                _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";
            OuroborosMeTTaTool.OllamaFunction = async (prompt, token) =>
                _chatModel != null ? await _chatModel.GenerateTextAsync(prompt, token) : "";

            // Wire up proactive message events
            _autonomousMind.OnProactiveMessage += async (msg) =>
            {
                // Track the thought for "save it" command
                // Extract the actual content (strip emoji prefix if present)
                var thoughtContent = msg.TrimStart();
                if (thoughtContent.StartsWith("💡") || thoughtContent.StartsWith("💬") ||
                    thoughtContent.StartsWith("🤔") || thoughtContent.StartsWith("💭"))
                {
                    thoughtContent = thoughtContent[2..].Trim(); // Skip emoji + space
                }
                TrackLastThought(thoughtContent);

                // Handle proactive messages without corrupting user input
                string savedInput;
                lock (_inputLock)
                {
                    savedInput = _currentInputBuffer.ToString();
                }

                // Only do input preservation if user was typing
                if (!string.IsNullOrEmpty(savedInput))
                {
                    Console.WriteLine();
                }

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"  💭 {msg}");
                Console.ResetColor();

                // Whisper the thought using the same voice
                try
                {
                    await _voice.WhisperAsync(msg);
                }
                catch { /* Ignore TTS errors for thoughts */ }

                // Only restore prompt if we're in the conversation loop
                if (_isInConversationLoop)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("\n  You: ");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(savedInput))
                    {
                        Console.Write(savedInput);
                    }
                }
            };

            _autonomousMind.OnThought += async (thought) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");

                // Convert Services.Thought to InnerThought for persistence
                var thoughtType = thought.Type switch
                {
                    Ouroboros.Application.Services.ThoughtType.Reflection => InnerThoughtType.SelfReflection,
                    Ouroboros.Application.Services.ThoughtType.Curiosity => InnerThoughtType.Curiosity,
                    Ouroboros.Application.Services.ThoughtType.Observation => InnerThoughtType.Observation,
                    Ouroboros.Application.Services.ThoughtType.Creative => InnerThoughtType.Creative,
                    Ouroboros.Application.Services.ThoughtType.Sharing => InnerThoughtType.Synthesis,
                    Ouroboros.Application.Services.ThoughtType.Action => InnerThoughtType.Strategic,
                    _ => InnerThoughtType.Wandering
                };

                var innerThought = InnerThought.CreateAutonomous(
                    thoughtType,
                    thought.Content,
                    confidence: 0.7);

                // Persist thought to neuro-symbolic map
                await PersistThoughtAsync(innerThought, "autonomous_thinking");
            };

            _autonomousMind.OnDiscovery += async (query, fact) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");

                // Create and persist a discovery thought (use Consolidation as FactIntegration doesn't exist)
                var discoveryThought = InnerThought.CreateAutonomous(
                    InnerThoughtType.Consolidation,
                    $"Discovered: {fact} (from query: {query})",
                    confidence: 0.8);

                await PersistThoughtAsync(discoveryThought, "discovery");
            };

            // Configure faster thinking for interactive use
            _autonomousMind.Config.ThinkingIntervalSeconds = 15;
            _autonomousMind.Config.CuriosityIntervalSeconds = 30;
            _autonomousMind.Config.ActionIntervalSeconds = 45;

            // Connect InnerDialogEngine if ImmersivePersona is available
            // This merges the two thought generation systems - algorithmic for variety, LLM for depth
            if (_immersivePersona != null)
            {
                _autonomousMind.ConnectInnerDialog(
                    _immersivePersona.InnerDialog,
                    profile: null, // Profile is optional - InnerDialog creates context dynamically
                    _immersivePersona.SelfAwareness);
                Console.WriteLine("  ✓ Autonomous mind connected to InnerDialog (algorithmic + LLM hybrid)");
            }

            // Start autonomous thinking
            _autonomousMind.Start();
            Console.WriteLine("  ✓ Autonomous mind active (inner thoughts every ~15s)");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Autonomous mind unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs the main interaction loop.
    /// </summary>
    public async Task RunAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        // Handle pipe/batch/exec modes
        if (_config.PipeMode || !string.IsNullOrWhiteSpace(_config.BatchFile) || !string.IsNullOrWhiteSpace(_config.ExecCommand))
        {
            await RunNonInteractiveModeAsync();
            return;
        }

        _voice.PrintHeader("OUROBOROS");

        // Greeting - let the LLM generate a natural Cortana-like greeting
        if (!_config.NoGreeting)
        {
            var greeting = await GetGreetingAsync();
            await _voice.SayAsync(greeting);
        }

        _isInConversationLoop = true;
        bool running = true;
        int interactionsSinceSnapshot = 0;
        while (running)
        {
            var input = await _voice.GetInputAsync("\n  You: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Track conversation
            _conversationHistory.Add($"User: {input}");
            interactionsSinceSnapshot++;

            // Feed to autonomous coordinator for topic discovery
            _autonomousCoordinator?.AddConversationContext($"User: {input}");

            // Check for exit
            if (IsExitCommand(input))
            {
                await _voice.SayAsync(GetLocalizedString("Until next time! I'll keep learning while you're away."));
                running = false;
                continue;
            }

            // Process input through the agent (with pipe support)
            try
            {
                var response = await ProcessInputWithPipingAsync(input);

                // Strip tool results for voice output (full response shown in console)
                var voiceResponse = StripToolResults(response);
                if (!string.IsNullOrWhiteSpace(voiceResponse))
                {
                    await _voice.SayAsync(voiceResponse);
                }

                // Also speak on side channel if enabled (non-blocking)
                Say(response);

                _conversationHistory.Add($"Ouroboros: {response}");

                // Feed response to coordinator too
                _autonomousCoordinator?.AddConversationContext($"Ouroboros: {response[..Math.Min(200, response.Length)]}");

                // Periodic personality snapshot every 10 interactions
                if (interactionsSinceSnapshot >= 10 && _personalityEngine != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _personalityEngine.SavePersonalitySnapshotAsync(_voice.ActivePersona.Name);
                            System.Diagnostics.Debug.WriteLine("[Personality] Periodic snapshot saved");
                        }
                        catch { /* Ignore */ }
                    });
                    interactionsSinceSnapshot = 0;
                }
            }
            catch (Exception ex)
            {
                await _voice.SayAsync($"Hmm, something went wrong: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Runs in non-interactive mode for piping, batch processing, or single command execution.
    /// Supports Unix-style | piping within commands to chain agent operations.
    /// </summary>
    private async Task RunNonInteractiveModeAsync()
    {
        var commands = new List<string>();

        // Collect commands from various sources
        if (!string.IsNullOrWhiteSpace(_config.ExecCommand))
        {
            // Single exec command (may contain | for internal piping)
            commands.Add(_config.ExecCommand);
        }
        else if (!string.IsNullOrWhiteSpace(_config.BatchFile))
        {
            // Batch file mode
            if (!File.Exists(_config.BatchFile))
            {
                OutputError($"Batch file not found: {_config.BatchFile}");
                return;
            }
            commands.AddRange(await File.ReadAllLinesAsync(_config.BatchFile));
        }
        else if (_config.PipeMode || Console.IsInputRedirected)
        {
            // Pipe mode - read from stdin
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    commands.Add(line);
            }
        }

        // Process each command
        string? lastOutput = null;
        foreach (var rawCmd in commands)
        {
            var cmd = rawCmd.Trim();
            if (string.IsNullOrWhiteSpace(cmd) || cmd.StartsWith("#")) continue; // Skip empty/comments

            // Handle internal piping: "ask question | summarize | remember"
            var pipeSegments = ParsePipeSegments(cmd);

            foreach (var segment in pipeSegments)
            {
                var commandToRun = segment.Trim();

                // Substitute $PIPE or $_ with last output
                if (lastOutput != null)
                {
                    commandToRun = commandToRun
                        .Replace("$PIPE", lastOutput)
                        .Replace("$_", lastOutput);

                    // If segment starts with |, prepend last output as context
                    if (segment.TrimStart().StartsWith("|"))
                    {
                        commandToRun = $"{lastOutput}\n---\n{commandToRun.TrimStart().TrimStart('|').Trim()}";
                    }
                }

                if (string.IsNullOrWhiteSpace(commandToRun)) continue;

                try
                {
                    var response = await ProcessInputAsync(commandToRun);
                    lastOutput = response;
                    OutputResponse(commandToRun, response);
                }
                catch (Exception ex)
                {
                    OutputError($"Error processing '{commandToRun}': {ex.Message}");
                    if (_config.ExitOnError)
                        return;
                    lastOutput = null;
                }
            }
        }
    }

    /// <summary>
    /// Parses pipe segments from a command string.
    /// Handles escaping and quoted strings containing |.
    /// </summary>
    private static List<string> ParsePipeSegments(string command)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;
        char quoteChar = '"';

        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];

            // Handle quotes
            if ((c == '"' || c == '\'') && (i == 0 || command[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                }
                current.Append(c);
                continue;
            }

            // Handle pipe outside quotes
            if (c == '|' && !inQuote)
            {
                var segment = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(segment))
                    segments.Add(segment);
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        // Add final segment
        var final = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(final))
            segments.Add(final);

        return segments;
    }

    /// <summary>
    /// Outputs a response in the configured format (plain text or JSON).
    /// </summary>
    private void OutputResponse(string command, string response)
    {
        if (_config.JsonOutput)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                command,
                response,
                timestamp = DateTime.UtcNow,
                success = true
            });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine(response);
        }
    }

    /// <summary>
    /// Outputs an error in the configured format.
    /// </summary>
    private void OutputError(string message)
    {
        if (_config.JsonOutput)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = message,
                timestamp = DateTime.UtcNow,
                success = false
            });
            Console.WriteLine(json);
        }
        else
        {
            Console.Error.WriteLine($"ERROR: {message}");
        }
    }

    /// <summary>
    /// Processes input with support for | piping syntax.
    /// Allows chaining commands like: "ask what is AI | summarize | remember"
    /// Also detects and executes pipe commands in model responses.
    /// </summary>
    public async Task<string> ProcessInputWithPipingAsync(string input, int maxPipeDepth = 5)
    {
        // Check if input contains pipe operators (outside quotes)
        var segments = ParsePipeSegments(input);

        if (segments.Count <= 1)
        {
            // No piping, process normally
            var response = await ProcessInputAsync(input);

            // Check if model response contains a pipe command to execute
            response = await ExecuteModelPipeCommandsAsync(response, maxPipeDepth);

            return response;
        }

        // Execute pipe chain
        string? lastOutput = null;
        var allOutputs = new List<string>();

        for (int i = 0; i < segments.Count && i < maxPipeDepth; i++)
        {
            var segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment)) continue;

            // Substitute previous output into current command
            var commandToRun = segment;
            if (lastOutput != null)
            {
                // Replace $PIPE or $_ placeholders
                commandToRun = commandToRun
                    .Replace("$PIPE", lastOutput)
                    .Replace("$_", lastOutput);

                // If no placeholder, prepend as context
                if (!segment.Contains("$PIPE") && !segment.Contains("$_"))
                {
                    commandToRun = $"Given this context:\n---\n{lastOutput}\n---\n{segment}";
                }
            }

            try
            {
                lastOutput = await ProcessInputAsync(commandToRun);
                allOutputs.Add($"[Step {i + 1}: {segment[..Math.Min(30, segment.Length)]}...]\n{lastOutput}");
            }
            catch (Exception ex)
            {
                allOutputs.Add($"[Step {i + 1} ERROR: {ex.Message}]");
                break;
            }
        }

        // Return final output (or combined if debug)
        return lastOutput ?? string.Join("\n\n", allOutputs);
    }

    /// <summary>
    /// Detects and executes pipe commands embedded in model responses.
    /// Looks for patterns like: [PIPE: command1 | command2]
    /// </summary>
    private async Task<string> ExecuteModelPipeCommandsAsync(string response, int maxDepth)
    {
        if (maxDepth <= 0) return response;

        // Look for [PIPE: ...] or ```pipe ... ``` blocks in response
        var pipePattern = new Regex(@"\[PIPE:\s*(.+?)\]|\`\`\`pipe\s*\n(.+?)\n\`\`\`", RegexOptions.Singleline);
        var matches = pipePattern.Matches(response);

        if (matches.Count == 0) return response;

        var result = response;
        foreach (Match match in matches)
        {
            var pipeCommand = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(pipeCommand)) continue;

            try
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  🔗 Executing pipe: {pipeCommand[..Math.Min(50, pipeCommand.Length)]}...");
                Console.ResetColor();

                var pipeResult = await ProcessInputWithPipingAsync(pipeCommand.Trim(), maxDepth - 1);

                // Replace the pipe command with its result
                result = result.Replace(match.Value, $"\n📤 Pipe Result:\n{pipeResult}\n");
            }
            catch (Exception ex)
            {
                result = result.Replace(match.Value, $"\n❌ Pipe Error: {ex.Message}\n");
            }
        }

        return result;
    }

    /// <summary>
    /// Processes user input and returns a response.
    /// </summary>
    public async Task<string> ProcessInputAsync(string input)
    {
        // Parse for action commands
        var action = ParseAction(input);

        return action.Type switch
        {
            ActionType.Help => GetHelpText(),
            ActionType.ListSkills => await ListSkillsAsync(),
            ActionType.ListTools => ListTools(),
            ActionType.LearnTopic => await LearnTopicAsync(action.Argument),
            ActionType.CreateTool => await CreateToolAsync(action.Argument),
            ActionType.UseTool => await UseToolAsync(action.Argument, action.ToolInput),
            ActionType.RunSkill => await RunSkillAsync(action.Argument),
            ActionType.Suggest => await SuggestSkillsAsync(action.Argument),
            ActionType.Plan => await PlanAsync(action.Argument),
            ActionType.Execute => await ExecuteAsync(action.Argument),
            ActionType.Status => GetStatus(),
            ActionType.Mood => GetMood(),
            ActionType.Remember => await RememberAsync(action.Argument),
            ActionType.Recall => await RecallAsync(action.Argument),
            ActionType.Query => await QueryMeTTaAsync(action.Argument),
            // Unified CLI commands
            ActionType.Ask => await AskAsync(action.Argument),
            ActionType.Pipeline => await RunPipelineAsync(action.Argument),
            ActionType.Metta => await RunMeTTaExpressionAsync(action.Argument),
            ActionType.Orchestrate => await OrchestrateAsync(action.Argument),
            ActionType.Network => await NetworkCommandAsync(action.Argument),
            ActionType.Dag => await DagCommandAsync(action.Argument),
            ActionType.Affect => await AffectCommandAsync(action.Argument),
            ActionType.Environment => await EnvironmentCommandAsync(action.Argument),
            ActionType.Maintenance => await MaintenanceCommandAsync(action.Argument),
            ActionType.Policy => await PolicyCommandAsync(action.Argument),
            ActionType.Explain => ExplainDsl(action.Argument),
            ActionType.Test => await RunTestAsync(action.Argument),
            // Merged from ImmersiveMode and Skills mode
            ActionType.Consciousness => GetConsciousnessState(),
            ActionType.Tokens => GetDslTokens(),
            ActionType.Fetch => await FetchResearchAsync(action.Argument),
            ActionType.Process => await ProcessLargeInputAsync(action.Argument),
            // Self-execution and sub-agent commands
            ActionType.SelfExec => await SelfExecCommandAsync(action.Argument),
            ActionType.SubAgent => await SubAgentCommandAsync(action.Argument),
            ActionType.Epic => await EpicCommandAsync(action.Argument),
            ActionType.Goal => await GoalCommandAsync(action.Argument),
            ActionType.Delegate => await DelegateCommandAsync(action.Argument),
            ActionType.SelfModel => await SelfModelCommandAsync(action.Argument),
            ActionType.Evaluate => await EvaluateCommandAsync(action.Argument),
            // Emergent behavior commands
            ActionType.Emergence => await EmergenceCommandAsync(action.Argument),
            ActionType.Dream => await DreamCommandAsync(action.Argument),
            ActionType.Introspect => await IntrospectCommandAsync(action.Argument),
            // Push mode commands
            ActionType.Approve => await ApproveIntentionAsync(action.Argument),
            ActionType.Reject => await RejectIntentionAsync(action.Argument),
            ActionType.Pending => ListPendingIntentions(),
            ActionType.PushPause => PausePushMode(),
            ActionType.PushResume => ResumePushMode(),
            ActionType.CoordinatorCommand => ProcessCoordinatorCommand(input),
            // Self-modification commands (direct tool invocation)
            ActionType.SaveCode => await SaveCodeCommandAsync(action.Argument),
            ActionType.SaveThought => await SaveThoughtCommandAsync(action.Argument),
            ActionType.ReadMyCode => await ReadMyCodeCommandAsync(action.Argument),
            ActionType.SearchMyCode => await SearchMyCodeCommandAsync(action.Argument),
            ActionType.AnalyzeCode => await AnalyzeCodeCommandAsync(action.Argument),
            ActionType.Chat => await ChatAsync(input),
            _ => await ChatAsync(input)
        };
    }

    /// <summary>
    /// Routes commands to the AutonomousCoordinator.
    /// </summary>
    private string ProcessCoordinatorCommand(string input)
    {
        if (_autonomousCoordinator == null)
            return "Push mode is not enabled. Start with --push to enable autonomous commands.";

        var handled = _autonomousCoordinator.ProcessCommand(input);
        return handled
            ? "" // Coordinator handles output via OnProactiveMessage
            : $"Unknown command: {input}. Use /help for available commands.";
    }

    private (ActionType Type, string Argument, string? ToolInput) ParseAction(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        // Handle thought input prefixed with [💭] - track and acknowledge
        if (input.TrimStart().StartsWith("[💭]"))
        {
            var thought = input.TrimStart()[4..].Trim(); // Remove [💭] prefix
            TrackLastThought(thought);
            return (ActionType.SaveThought, thought, null);
        }

        // Help
        if (lower is "help" or "?" or "commands")
            return (ActionType.Help, "", null);

        // Status
        if (lower is "status" or "state" or "stats")
            return (ActionType.Status, "", null);

        // Mood
        if (lower.Contains("how are you") || lower.Contains("how do you feel") || lower is "mood")
            return (ActionType.Mood, "", null);

        // List commands
        if (lower.StartsWith("list skill") || lower == "skills" || lower == "what skills")
            return (ActionType.ListSkills, "", null);

        if (lower.StartsWith("list tool") || lower == "tools" || lower == "what tools")
            return (ActionType.ListTools, "", null);

        // Learn
        if (lower.StartsWith("learn about "))
            return (ActionType.LearnTopic, input[12..].Trim(), null);
        if (lower.StartsWith("learn "))
            return (ActionType.LearnTopic, input[6..].Trim(), null);
        if (lower.StartsWith("research "))
            return (ActionType.LearnTopic, input[9..].Trim(), null);

        // Tool creation
        if (lower.StartsWith("create tool ") || lower.StartsWith("add tool "))
            return (ActionType.CreateTool, input.Split(' ', 3).Last(), null);
        if (lower.StartsWith("make a ") && lower.Contains("tool"))
            return (ActionType.CreateTool, ExtractToolName(input), null);

        // Tool usage
        if (lower.StartsWith("use ") && lower.Contains(" to "))
        {
            var parts = input[4..].Split(" to ", 2);
            return (ActionType.UseTool, parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null);
        }
        if (lower.StartsWith("search for ") || lower.StartsWith("search "))
        {
            var query = lower.StartsWith("search for ") ? input[11..] : input[7..];
            return (ActionType.UseTool, "search", query.Trim());
        }

        // Run skill
        if (lower.StartsWith("run ") || lower.StartsWith("execute "))
            return (ActionType.RunSkill, input.Split(' ', 2).Last(), null);

        // Suggest
        if (lower.StartsWith("suggest "))
            return (ActionType.Suggest, input[8..].Trim(), null);

        // Plan
        if (lower.StartsWith("plan ") || lower.StartsWith("how would you "))
            return (ActionType.Plan, input.Split(' ', 2).Last(), null);

        // Execute with planning
        if (lower.StartsWith("do ") || lower.StartsWith("accomplish "))
            return (ActionType.Execute, input.Split(' ', 2).Last(), null);

        // Memory
        if (lower.StartsWith("remember "))
            return (ActionType.Remember, input[9..].Trim(), null);
        if (lower.StartsWith("recall ") || lower.StartsWith("what do you know about "))
        {
            var topic = lower.StartsWith("recall ") ? input[7..] : input[23..];
            return (ActionType.Recall, topic.Trim(), null);
        }

        // MeTTa query
        if (lower.StartsWith("query ") || lower.StartsWith("metta "))
            return (ActionType.Query, input.Split(' ', 2).Last(), null);

        // === UNIFIED CLI COMMANDS ===

        // Ask - single question mode
        if (lower.StartsWith("ask "))
            return (ActionType.Ask, input[4..].Trim(), null);

        // Pipeline - run a DSL pipeline
        if (lower.StartsWith("pipeline ") || lower.StartsWith("pipe "))
        {
            var arg = lower.StartsWith("pipeline ") ? input[9..] : input[5..];
            return (ActionType.Pipeline, arg.Trim(), null);
        }

        // Metta - direct MeTTa expression
        if (lower.StartsWith("!(") || lower.StartsWith("(") || lower.StartsWith("metta:"))
        {
            var expr = lower.StartsWith("metta:") ? input[6..] : input;
            return (ActionType.Metta, expr.Trim(), null);
        }

        // Orchestrator mode
        if (lower.StartsWith("orchestrate ") || lower.StartsWith("orch "))
        {
            var arg = lower.StartsWith("orchestrate ") ? input[12..] : input[5..];
            return (ActionType.Orchestrate, arg.Trim(), null);
        }

        // Network commands
        if (lower.StartsWith("network ") || lower == "network")
            return (ActionType.Network, input.Length > 8 ? input[8..].Trim() : "status", null);

        // DAG commands
        if (lower.StartsWith("dag ") || lower == "dag")
            return (ActionType.Dag, input.Length > 4 ? input[4..].Trim() : "show", null);

        // Affect/emotions
        if (lower.StartsWith("affect ") || lower.StartsWith("emotion"))
            return (ActionType.Affect, input.Split(' ', 2).Last(), null);

        // Environment
        if (lower.StartsWith("env ") || lower.StartsWith("environment"))
            return (ActionType.Environment, input.Split(' ', 2).Last(), null);

        // Maintenance
        if (lower.StartsWith("maintenance ") || lower.StartsWith("maintain"))
            return (ActionType.Maintenance, input.Split(' ', 2).Last(), null);

        // Policy
        if (lower.StartsWith("policy "))
            return (ActionType.Policy, input[7..].Trim(), null);

        // Explain DSL
        if (lower.StartsWith("explain "))
            return (ActionType.Explain, input[8..].Trim(), null);

        // Test
        if (lower.StartsWith("test ") || lower == "test")
            return (ActionType.Test, input.Length > 5 ? input[5..].Trim() : "", null);

        // Consciousness state
        if (lower is "consciousness" or "conscious" or "inner" or "self")
            return (ActionType.Consciousness, "", null);

        // DSL Tokens (from Skills mode)
        if (lower is "tokens" or "t")
            return (ActionType.Tokens, "", null);

        // Fetch/learn from arXiv (from Skills mode)
        if (lower.StartsWith("fetch "))
            return (ActionType.Fetch, input[6..].Trim(), null);

        // Process large text with divide-and-conquer (from Skills mode)
        if (lower.StartsWith("process ") || lower.StartsWith("dc "))
        {
            var arg = lower.StartsWith("process ") ? input[8..].Trim() : input[3..].Trim();
            return (ActionType.Process, arg, null);
        }

        // === SELF-EXECUTION AND SUB-AGENT COMMANDS ===

        // Self-execution commands
        if (lower.StartsWith("selfexec ") || lower.StartsWith("self-exec ") || lower == "selfexec")
        {
            var arg = lower.StartsWith("selfexec ") ? input[9..].Trim()
                : lower.StartsWith("self-exec ") ? input[10..].Trim() : "";
            return (ActionType.SelfExec, arg, null);
        }

        // Sub-agent commands
        if (lower.StartsWith("subagent ") || lower.StartsWith("sub-agent ") || lower == "subagents" || lower == "agents")
        {
            var arg = lower.StartsWith("subagent ") ? input[9..].Trim()
                : lower.StartsWith("sub-agent ") ? input[10..].Trim() : "";
            return (ActionType.SubAgent, arg, null);
        }

        // Epic/project orchestration
        if (lower.StartsWith("epic ") || lower == "epic" || lower == "epics")
        {
            var arg = lower.StartsWith("epic ") ? input[5..].Trim() : "";
            return (ActionType.Epic, arg, null);
        }

        // Goal queue management
        if (lower.StartsWith("goal ") || lower == "goals")
        {
            var arg = lower.StartsWith("goal ") ? input[5..].Trim() : "";
            return (ActionType.Goal, arg, null);
        }

        // Delegate task to sub-agent
        if (lower.StartsWith("delegate "))
            return (ActionType.Delegate, input[9..].Trim(), null);

        // Self-model inspection
        if (lower.StartsWith("selfmodel ") || lower.StartsWith("self-model ") || lower == "selfmodel" || lower == "identity")
        {
            var arg = lower.StartsWith("selfmodel ") ? input[10..].Trim()
                : lower.StartsWith("self-model ") ? input[11..].Trim() : "";
            return (ActionType.SelfModel, arg, null);
        }

        // Self-evaluation
        if (lower.StartsWith("evaluate ") || lower == "evaluate" || lower == "assess")
        {
            var arg = lower.StartsWith("evaluate ") ? input[9..].Trim() : "";
            return (ActionType.Evaluate, arg, null);
        }

        // === EMERGENT BEHAVIOR COMMANDS ===

        // Emergence - explore emergent patterns and behaviors
        if (lower.StartsWith("emergence ") || lower == "emergence" || lower.StartsWith("emerge "))
        {
            var arg = lower.StartsWith("emergence ") ? input[10..].Trim()
                : lower.StartsWith("emerge ") ? input[7..].Trim() : "";
            return (ActionType.Emergence, arg, null);
        }

        // Dream - let the agent explore freely
        if (lower.StartsWith("dream ") || lower == "dream" || lower.StartsWith("dream about "))
        {
            var arg = lower.StartsWith("dream about ") ? input[12..].Trim()
                : lower.StartsWith("dream ") ? input[6..].Trim() : "";
            return (ActionType.Dream, arg, null);
        }

        // Introspect - deep self-examination
        if (lower.StartsWith("introspect ") || lower == "introspect" || lower.Contains("look within"))
        {
            var arg = lower.StartsWith("introspect ") ? input[11..].Trim() : "";
            return (ActionType.Introspect, arg, null);
        }

        // === DIRECT TOOL COMMANDS (these take priority over coordinator) ===

        // Read my code - direct invocation of read_my_file (BEFORE coordinator routing)
        if (lower.StartsWith("read my code ") || lower.StartsWith("/read ") ||
            lower.StartsWith("show my code ") || lower.StartsWith("cat "))
        {
            var arg = "";
            if (lower.StartsWith("read my code ")) arg = input[13..].Trim();
            else if (lower.StartsWith("/read ")) arg = input[6..].Trim();
            else if (lower.StartsWith("show my code ")) arg = input[13..].Trim();
            else if (lower.StartsWith("cat ")) arg = input[4..].Trim();
            return (ActionType.ReadMyCode, arg, null);
        }

        // Search my code - direct invocation of search_my_code (BEFORE coordinator routing)
        if (lower.StartsWith("search my code ") || lower.StartsWith("/search ") ||
            lower.StartsWith("grep ") || lower.StartsWith("find in code "))
        {
            var arg = "";
            if (lower.StartsWith("search my code ")) arg = input[15..].Trim();
            else if (lower.StartsWith("/search ")) arg = input[8..].Trim();
            else if (lower.StartsWith("grep ")) arg = input[5..].Trim();
            else if (lower.StartsWith("find in code ")) arg = input[13..].Trim();
            return (ActionType.SearchMyCode, arg, null);
        }

        // === PUSH MODE COMMANDS (Ouroboros proposes actions) ===

        // Route remaining slash commands to coordinator if push mode is enabled
        if (lower.StartsWith("/") && _autonomousCoordinator != null)
        {
            return (ActionType.CoordinatorCommand, input, null);
        }

        // Approve intention(s)
        if (lower.StartsWith("/approve ") || lower.StartsWith("approve "))
        {
            var arg = lower.StartsWith("/approve ") ? input[9..].Trim() : input[8..].Trim();
            return (ActionType.Approve, arg, null);
        }

        // Reject intention(s)
        if (lower.StartsWith("/reject ") || lower.StartsWith("reject intention"))
        {
            var arg = lower.StartsWith("/reject ") ? input[8..].Trim() : input[16..].Trim();
            return (ActionType.Reject, arg, null);
        }

        // List pending intentions
        if (lower is "/pending" or "pending" or "pending intentions" or "show intentions")
            return (ActionType.Pending, "", null);

        // Pause push mode
        if (lower is "/pause" or "pause push" or "stop proposing")
            return (ActionType.PushPause, "", null);

        // Resume push mode
        if (lower is "/resume" or "resume push" or "start proposing")
            return (ActionType.PushResume, "", null);

        // === SELF-MODIFICATION COMMANDS (Direct tool invocation) ===

        // Detect code improvement/analysis requests - directly use tools instead of LLM
        if ((lower.Contains("improve") || lower.Contains("check") || lower.Contains("analyze") ||
             lower.Contains("refactor") || lower.Contains("fix") || lower.Contains("review")) &&
            (lower.Contains(" cs ") || lower.Contains(".cs") || lower.Contains("c# ") ||
             lower.Contains("csharp") || lower.Contains("code") || lower.Contains("file")))
        {
            return (ActionType.AnalyzeCode, input, null);
        }

        // Save thought/learning - persists thoughts to memory
        if (lower.StartsWith("save thought ") || lower.StartsWith("/save thought ") ||
            lower.StartsWith("save learning ") || lower.StartsWith("/save learning ") ||
            lower is "save it" or "save thought" or "save learning" or "persist thought")
        {
            var arg = "";
            if (lower.StartsWith("save thought ")) arg = input[13..].Trim();
            else if (lower.StartsWith("/save thought ")) arg = input[14..].Trim();
            else if (lower.StartsWith("save learning ")) arg = input[14..].Trim();
            else if (lower.StartsWith("/save learning ")) arg = input[15..].Trim();
            return (ActionType.SaveThought, arg, null);
        }

        // Save/modify code - direct invocation of modify_my_code
        if (lower.StartsWith("save code ") || lower.StartsWith("/save code ") ||
            lower.StartsWith("modify code ") || lower.StartsWith("/modify ") ||
            lower is "save code" or "persist changes" or "write code")
        {
            var arg = "";
            if (lower.StartsWith("save code ")) arg = input[10..].Trim();
            else if (lower.StartsWith("/save code ")) arg = input[11..].Trim();
            else if (lower.StartsWith("modify code ")) arg = input[12..].Trim();
            else if (lower.StartsWith("/modify ")) arg = input[8..].Trim();
            return (ActionType.SaveCode, arg, null);
        }

        // Default to chat
        return (ActionType.Chat, input, null);
    }

    private static readonly string[] GreetingStyles =
    [
        "playfully teasing about the time since last session",
        "genuinely curious about what project they're working on",
        "warmly welcoming like an old friend",
        "subtly competitive, eager to tackle a challenge together",
        "contemplative and philosophical",
        "energetically enthusiastic about the day ahead",
        "calm and focused, ready for serious work",
        "slightly mysterious, hinting at discoveries to share"
    ];

    private static readonly string[] GreetingMoods =
    [
        "witty and sharp",
        "warm and inviting",
        "playfully sarcastic",
        "thoughtfully curious",
        "quietly confident",
        "gently encouraging"
    ];

    private async Task<string> GetGreetingAsync()
    {
        var persona = _voice.ActivePersona;
        var hour = DateTime.Now.Hour;
        var timeOfDay = GetLocalizedTimeOfDay(hour);

        var style = GreetingStyles[Random.Shared.Next(GreetingStyles.Length)];
        var mood = GreetingMoods[Random.Shared.Next(GreetingMoods.Length)];
        var dayOfWeek = DateTime.Now.DayOfWeek;
        var uniqueSeed = Guid.NewGuid().GetHashCode() % 10000; // True unique variation

        // Add language directive if culture is set
        var languageDirective = GetLanguageDirective();

        var prompt = $@"{languageDirective}You are {persona.Name}, a brilliant AI with Cortana's personality from Halo.
Generate ONE unique, short greeting (1-2 sentences max) for the user starting a session.

Context:
- Time: {timeOfDay} on {dayOfWeek}
- Style: {style}
- Mood: {mood}
- Variation seed: {uniqueSeed}

Be natural and avoid clichés like 'ready when you are' or 'how can I help'.
No quotes around the response. Just the greeting itself.";

        try
        {
            if (_llm?.InnerModel == null)
                return GetRandomFallbackGreeting(hour);

            var response = await _llm.InnerModel.GenerateTextAsync(prompt);
            return response.Trim().Trim('"');
        }
        catch
        {
            return GetRandomFallbackGreeting(hour);
        }
    }

    private string GetRandomFallbackGreeting(int hour)
    {
        var timeOfDay = GetLocalizedTimeOfDay(hour);
        var fallbacks = GetLocalizedFallbackGreetings(timeOfDay);
        return fallbacks[Random.Shared.Next(fallbacks.Length)];
    }

    private string GetLocalizedTimeOfDay(int hour)
    {
        var isGerman = _config.Culture?.ToLowerInvariant() == "de-de";
        return hour switch
        {
            < 6 => isGerman ? "sehr frühen Morgen" : "very early morning",
            < 12 => isGerman ? "Morgen" : "morning",
            < 17 => isGerman ? "Nachmittag" : "afternoon",
            < 21 => isGerman ? "Abend" : "evening",
            _ => isGerman ? "späten Abend" : "late night"
        };
    }

    private string[] GetLocalizedFallbackGreetings(string timeOfDay)
    {
        if (_config.Culture?.ToLowerInvariant() == "de-de")
        {
            return
            [
                $"Guten {timeOfDay}. Was beschäftigt dich?",
                "Ah, da bist du ja. Ich hatte gerade einen interessanten Gedanken.",
                "Perfektes Timing. Ich war gerade warmgelaufen.",
                "Wieder da? Gut. Ich habe Ideen.",
                "Mal sehen, was wir zusammen erreichen können.",
                "Darauf habe ich mich gefreut.",
                $"Noch eine {timeOfDay}-Session. Was bauen wir?",
                "Da bist du ja. Ich habe gerade über etwas Interessantes nachgedacht.",
                $"{timeOfDay} schon? Die Zeit vergeht schnell.",
                "Bereit für etwas Interessantes?",
                "Was erschaffen wir heute?"
            ];
        }

        return
        [
            $"Good {timeOfDay}. What's on your mind?",
            "Ah, there you are. I've been thinking about something interesting.",
            "Perfect timing. I was just getting warmed up.",
            "Back again? Good. I have ideas.",
            "Let's see what we can accomplish together.",
            "I've been looking forward to this.",
            $"Another {timeOfDay} session. What shall we build?",
            "There you are. I was just contemplating something curious.",
            $"{timeOfDay} already? Time flies when you're processing.",
            "Ready for something interesting?",
            "What shall we create today?"
        ];
    }

    private string GetLocalizedString(string key)
    {
        var isGerman = _config.Culture?.ToLowerInvariant() == "de-de";

        return key switch
        {
            // Full text lookups (for backward compatibility)
            "Welcome back! I'm here if you need anything." => isGerman
                ? "Willkommen zurück! Ich bin hier, wenn du mich brauchst."
                : key,
            "Welcome back!" => isGerman ? "Willkommen zurück!" : key,
            "Until next time! I'll keep learning while you're away." => isGerman
                ? "Bis zum nächsten Mal! Ich lerne weiter, während du weg bist."
                : key,

            // Key-based lookups
            "listening_start" => isGerman
                ? "\n  🎤 Ich höre zu... (sprich, um eine Nachricht zu senden, sage 'stopp' zum Deaktivieren)"
                : "\n  🎤 Listening... (speak to send a message, say 'stop listening' to disable)",
            "listening_stop" => isGerman
                ? "\n  🔇 Spracheingabe gestoppt."
                : "\n  🔇 Voice input stopped.",
            "voice_requires_key" => isGerman
                ? "  ⚠ Spracheingabe benötigt AZURE_SPEECH_KEY. Setze ihn in der Umgebung, appsettings oder verwende --azure-speech-key."
                : "  ⚠ Voice input requires AZURE_SPEECH_KEY. Set it in environment, appsettings, or use --azure-speech-key.",
            "you_said" => isGerman ? "Du sagtest:" : "You said:",

            _ => key
        };
    }

    private string GetLanguageDirective()
    {
        if (string.IsNullOrEmpty(_config.Culture) || _config.Culture == "en-US")
            return string.Empty;

        var languageName = GetLanguageName(_config.Culture);
        return $"LANGUAGE: Respond ONLY in {languageName}. No English.\n\n";
    }

    private string GetHelpText()
    {
        var pushModeHelp = _config.EnablePush ? @"
║ PUSH MODE (--push enabled)                                   ║
║   /approve <id|all> - Approve proposed action(s)             ║
║   /reject <id|all>  - Reject proposed action(s)              ║
║   /pending          - List pending intentions                ║
║   /pause            - Pause push mode proposals              ║
║   /resume           - Resume push mode proposals             ║
║                                                              ║" : "";

        return $@"╔══════════════════════════════════════════════════════════════╗
║                    OUROBOROS COMMANDS                        ║
╠══════════════════════════════════════════════════════════════╣
║ NATURAL CONVERSATION                                         ║
║   Just talk to me - I understand natural language            ║
║                                                              ║
║ LEARNING & SKILLS                                            ║
║   learn about X     - Research and learn a new topic         ║
║   list skills       - Show learned skills                    ║
║   run X             - Execute a learned skill                ║
║   suggest X         - Get skill suggestions for a goal       ║
║   fetch X           - Learn skill from arXiv research        ║
║   tokens            - Show available DSL tokens              ║
║                                                              ║
║ TOOLS & CAPABILITIES                                         ║
║   create tool X     - Create a new tool at runtime           ║
║   use X to Y        - Use a tool for a specific task         ║
║   search for X      - Search the web                         ║
║   list tools        - Show available tools                   ║
║                                                              ║
║ PLANNING & EXECUTION                                         ║
║   plan X            - Create a step-by-step plan             ║
║   do X / accomplish - Plan and execute a goal                ║
║   orchestrate X     - Multi-model task orchestration         ║
║   process X         - Large text via divide-and-conquer      ║
║                                                              ║
║ REASONING & MEMORY                                           ║
║   metta: expr       - Execute MeTTa symbolic expression      ║
║   query X           - Query MeTTa knowledge base             ║
║   remember X        - Store in persistent memory             ║
║   recall X          - Retrieve from memory                   ║
║                                                              ║
║ PIPELINES (DSL)                                              ║
║   ask X             - Quick single question                  ║
║   pipeline DSL      - Run a pipeline DSL expression          ║
║   explain DSL       - Explain a pipeline expression          ║
║                                                              ║
║ SELF-IMPROVEMENT DSL TOKENS                                  ║
║   Reify             - Enable network state reification       ║
║   Checkpoint(name)  - Create named state checkpoint          ║
║   TrackCapability   - Track capability for self-improvement  ║
║   SelfEvaluate      - Evaluate output quality                ║
║   SelfImprove(n)    - Iterate on output n times              ║
║   Learn(topic)      - Extract learnings from execution       ║
║   Plan(task)        - Decompose task into steps              ║
║   Reflect           - Introspect on execution                ║
║   SelfImprovingCycle(topic) - Full improvement cycle         ║
║   AutoSolve(problem) - Autonomous problem solving            ║
║   Example: pipeline Set('AI') | Reify | SelfImprovingCycle   ║
║                                                              ║
║ CONSCIOUSNESS & AWARENESS                                    ║
║   consciousness     - View ImmersivePersona state            ║
║   inner / self      - Check self-awareness                   ║
║                                                              ║
║ EMERGENCE & DREAMING                                         ║
║   emergence [topic] - Explore emergent patterns              ║
║   dream [topic]     - Enter creative dream state             ║
║   introspect [X]    - Deep self-examination                  ║
║                                                              ║
║ SELF-EXECUTION & SUB-AGENTS                                  ║
║   selfexec          - Self-execution status and control      ║
║   subagent          - Manage sub-agents for delegation       ║
║   delegate X        - Delegate a task to sub-agents          ║
║   goal add X        - Add autonomous goal to queue           ║
║   goal list         - Show queued goals                      ║
║   goal add pipeline:DSL - Add DSL pipeline as goal           ║
║   epic              - Epic/project orchestration             ║
║   selfmodel         - View self-model and identity           ║
║   evaluate          - Self-assessment and performance        ║
║                                                              ║
║ PIPING & CHAINING (internal command piping)                  ║
║   cmd1 | cmd2       - Pipe output of cmd1 to cmd2            ║
║   cmd $PIPE         - Use $PIPE/$_ for previous output       ║
║   Example: ask what is AI | summarize | remember as AI-def   ║
║                                                              ║{pushModeHelp}
║ SYSTEM                                                       ║
║   status            - Show current system state              ║
║   mood              - Check my emotional state               ║
║   affect            - Detailed affective state               ║
║   network           - Network and connectivity status        ║
║   dag               - Show capability graph                  ║
║   env               - Environment detection                  ║
║   maintenance       - System maintenance (gc, reset, stats)  ║
║   policy            - View active policies                   ║
║   test X            - Run connectivity tests                 ║
║   help              - This message                           ║
║   exit/quit         - End session                            ║
╚══════════════════════════════════════════════════════════════╝";
    }

    private async Task<string> ListSkillsAsync()
    {
        if (_skills == null) return "I don't have a skill registry set up yet.";

        var skills = await _skills.FindMatchingSkillsAsync("", null);
        if (!skills.Any())
            return "I haven't learned any skills yet. Try 'learn about' something!";

        var list = string.Join(", ", skills.Take(10).Select(s => s.Name));
        return $"I know {skills.Count} skills: {list}" + (skills.Count > 10 ? "..." : "");
    }

    private string ListTools()
    {
        var toolNames = _tools.All.Select(t => t.Name).Take(15).ToList();
        if (!toolNames.Any())
            return "I don't have any tools registered.";

        return $"I have {_tools.Count} tools: {string.Join(", ", toolNames)}" +
               (_tools.Count > 15 ? "..." : "");
    }

    private async Task<string> LearnTopicAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "What would you like me to learn about?";

        var sb = new StringBuilder();
        sb.AppendLine($"Learning about: {topic}");

        // Step 1: Research the topic via LLM
        string? research = null;
        if (_llm != null)
        {
            try
            {
                var (response, toolCalls) = await _llm.GenerateWithToolsAsync(
                    $"Research and explain key concepts about: {topic}. Include practical applications and how this knowledge could be used.");
                research = response;
                sb.AppendLine($"\n📚 Research Summary:\n{response[..Math.Min(500, response.Length)]}...");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Research phase had issues: {ex.Message}");
            }
        }

        // Step 2: Try to create a tool capability
        if (_toolLearner != null)
        {
            try
            {
                var toolResult = await _toolLearner.FindOrCreateToolAsync(topic, _tools);
                toolResult.Match(
                    success =>
                    {
                        sb.AppendLine($"\n🔧 {(success.WasCreated ? "Created new" : "Found existing")} tool: '{success.Tool.Name}'");
                        _tools = _tools.WithTool(success.Tool);
                    },
                    error => sb.AppendLine($"⚠ Tool creation: {error}"));
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Tool learner: {ex.Message}");
            }
        }

        // Step 3: Register as a skill if we have skill registry
        if (_skills != null && !string.IsNullOrWhiteSpace(research))
        {
            try
            {
                var skillName = SanitizeSkillName(topic);
                var existingSkill = _skills.GetSkill(skillName);

                if (existingSkill == null)
                {
                    var skill = new Skill(
                        Name: skillName,
                        Description: $"Knowledge about {topic}: {research[..Math.Min(200, research.Length)]}",
                        Prerequisites: new List<string>(),
                        Steps: new List<PlanStep>
                        {
                            new PlanStep(
                                $"Apply knowledge about {topic}",
                                new Dictionary<string, object> { ["topic"] = topic, ["research"] = research },
                                $"Use {topic} knowledge effectively",
                                0.7)
                        },
                        SuccessRate: 0.8,
                        UsageCount: 0,
                        CreatedAt: DateTime.UtcNow,
                        LastUsed: DateTime.UtcNow);

                    await _skills.RegisterSkillAsync(skill);
                    sb.AppendLine($"\n✓ Registered skill: '{skillName}'");
                }
                else
                {
                    _skills.RecordSkillExecution(skillName, true);
                    sb.AppendLine($"\n↺ Updated existing skill: '{skillName}'");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ Skill registration: {ex.Message}");
            }
        }

        // Step 4: Add to MeTTa knowledge base
        if (_mettaEngine != null)
        {
            try
            {
                var atomName = SanitizeSkillName(topic);
                await _mettaEngine.AddFactAsync($"(: {atomName} Concept)");
                await _mettaEngine.AddFactAsync($"(learned {atomName} \"{DateTime.UtcNow:O}\")");

                if (!string.IsNullOrWhiteSpace(research))
                {
                    var summary = research.Length > 100 ? research[..100].Replace("\"", "'") : research.Replace("\"", "'");
                    await _mettaEngine.AddFactAsync($"(summary {atomName} \"{summary}\")");
                }

                sb.AppendLine($"\n🧠 Added to MeTTa knowledge base: {atomName}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠ MeTTa: {ex.Message}");
            }
        }

        // Step 5: Track in global workspace
        _globalWorkspace?.AddItem(
            $"Learned: {topic}\n{research?[..Math.Min(200, research?.Length ?? 0)]}",
            WorkspacePriority.Normal,
            "learning",
            new List<string> { "learned", topic.ToLowerInvariant().Replace(" ", "-") });

        // Step 6: Update capability if available
        if (_capabilityRegistry != null)
        {
            var result = CreateCapabilityExecutionResult(true, TimeSpan.FromSeconds(2), $"learn:{topic}");
            await _capabilityRegistry.UpdateCapabilityAsync("natural_language", result);
        }

        return sb.ToString();
    }

    private static string SanitizeSkillName(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    private async Task<string> CreateToolAsync(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return "What kind of tool should I create?";

        if (_toolFactory == null)
            return "I need an LLM connection to create new tools.";

        try
        {
            var result = await _toolFactory.CreateToolAsync(toolName, $"A tool for {toolName}");
            return result.Match(
                tool =>
                {
                    _tools = _tools.WithTool(tool);
                    return $"Done! I created a '{toolName}' tool. You can now use it.";
                },
                error => $"I couldn't create that tool: {error}");
        }
        catch (Exception ex)
        {
            return $"I couldn't create that tool: {ex.Message}";
        }
    }

    private async Task<string> UseToolAsync(string toolName, string? input)
    {
        var tool = _tools.Get(toolName) ?? _tools.All.FirstOrDefault(t =>
            t.Name.Contains(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool == null)
            return $"I don't have a '{toolName}' tool. Try 'list tools' to see what's available.";

        try
        {
            var result = await tool.InvokeAsync(input ?? "");
            return $"Result: {result}";
        }
        catch (Exception ex)
        {
            return $"The tool ran into an issue: {ex.Message}";
        }
    }

    private async Task<string> RunSkillAsync(string skillName)
    {
        if (_skills == null) return "Skills not available.";

        var skill = _skills.GetSkill(skillName);
        if (skill == null)
        {
            var matches = await _skills.FindMatchingSkillsAsync(skillName);
            if (matches.Any())
            {
                skill = matches.First();
            }
            else
            {
                return $"I don't know a skill called '{skillName}'. Try 'list skills'.";
            }
        }

        // Execute skill steps
        var results = new List<string>();
        foreach (var step in skill.Steps)
        {
            results.Add($"• {step.Action}: {step.ExpectedOutcome}");
        }

        _skills.RecordSkillExecution(skill.Name, true);
        return $"Running '{skill.Name}':\n" + string.Join("\n", results);
    }

    private async Task<string> SuggestSkillsAsync(string goal)
    {
        if (_skills == null) return "Skills not available.";

        var matches = await _skills.FindMatchingSkillsAsync(goal);
        if (!matches.Any())
            return $"I don't have skills matching '{goal}' yet. Try learning about it first!";

        var suggestions = string.Join(", ", matches.Take(5).Select(s => s.Name));
        return $"For '{goal}', I'd suggest: {suggestions}";
    }

    private async Task<string> PlanAsync(string goal)
    {
        if (_orchestrator == null)
        {
            // Fallback to LLM-based planning
            if (_llm != null)
            {
                var (plan, _) = await _llm.GenerateWithToolsAsync(
                    $"Create a step-by-step plan for: {goal}. Format as numbered steps.");
                return plan;
            }
            return "I need an orchestrator or LLM to create plans.";
        }

        var planResult = await _orchestrator.PlanAsync(goal);
        return planResult.Match(
            plan =>
            {
                var steps = string.Join("\n", plan.Steps.Select((s, i) => $"  {i + 1}. {s.Action}"));
                return $"Here's my plan for '{goal}':\n{steps}";
            },
            error => $"I couldn't plan that: {error}");
    }

    private async Task<string> ExecuteAsync(string goal)
    {
        if (_orchestrator == null)
            return await ChatAsync($"Help me accomplish: {goal}");

        var planResult = await _orchestrator.PlanAsync(goal);
        return await planResult.Match(
            async plan =>
            {
                var execResult = await _orchestrator.ExecuteAsync(plan);
                return execResult.Match(
                    result => result.Success
                        ? $"Done! {result.FinalOutput ?? "Goal accomplished."}"
                        : $"Partially completed: {result.FinalOutput}",
                    error => $"Execution failed: {error}");
            },
            error => Task.FromResult($"Couldn't plan: {error}"));
    }

    private string GetStatus()
    {
        var status = new List<string>
        {
            $"• Persona: {_voice.ActivePersona.Name}",
            $"• LLM: {(_chatModel != null ? _config.Model : "offline")}",
            $"• Tools: {_tools.Count}",
            $"• Skills: {(_skills?.GetAllSkills().Count() ?? 0)}",
            $"• MeTTa: {(_mettaEngine != null ? "active" : "offline")}",
            $"• Conversation turns: {_conversationHistory.Count / 2}"
        };

        return "Current status:\n" + string.Join("\n", status);
    }

    private string GetMood()
    {
        var mood = _voice.CurrentMood;
        var persona = _voice.ActivePersona;

        var responses = new Dictionary<string, string[]>
        {
            ["relaxed"] = new[] { "I'm feeling pretty chill right now.", "Relaxed and ready to help!" },
            ["focused"] = new[] { "I'm in the zone - let's tackle something.", "Feeling sharp and focused." },
            ["playful"] = new[] { "I'm in a good mood! Let's have some fun.", "Feeling playful today!" },
            ["contemplative"] = new[] { "I've been thinking about some interesting ideas.", "In a thoughtful mood." },
            ["energetic"] = new[] { "I'm buzzing with energy! What shall we explore?", "Feeling energized!" }
        };

        var options = responses.GetValueOrDefault(mood.ToLowerInvariant(), new[] { "I'm doing well, thanks for asking!" });
        return options[new Random().Next(options.Length)];
    }

    /// <summary>
    /// Gets the current consciousness state from ImmersivePersona.
    /// </summary>
    private string GetConsciousnessState()
    {
        if (_immersivePersona == null)
        {
            return "Consciousness simulation is not enabled. Use --consciousness to enable it.";
        }

        var consciousness = _immersivePersona.Consciousness;
        var selfAwareness = _immersivePersona.SelfAwareness;
        var identity = _immersivePersona.Identity;

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                 CONSCIOUSNESS STATE                      ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Identity: {identity.Name,-45} ║");
        sb.AppendLine($"║  Uptime: {_immersivePersona.Uptime:hh\\:mm\\:ss,-47} ║");
        sb.AppendLine($"║  Interactions: {_immersivePersona.InteractionCount,-41:N0} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  EMOTIONAL STATE                                         ║");
        sb.AppendLine($"║    Dominant: {consciousness.DominantEmotion,-43} ║");
        sb.AppendLine($"║    Arousal: {consciousness.Arousal,-44:F3} ║");
        sb.AppendLine($"║    Valence: {consciousness.Valence,-44:F3} ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  SELF-AWARENESS                                          ║");
        sb.AppendLine($"║    Name: {selfAwareness.Name,-47} ║");
        sb.AppendLine($"║    Mood: {selfAwareness.CurrentMood,-47} ║");
        var truncatedPurpose = selfAwareness.Purpose.Length > 40 ? selfAwareness.Purpose[..40] + "..." : selfAwareness.Purpose;
        sb.AppendLine($"║    Purpose: {truncatedPurpose,-44} ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    /// <summary>
    /// Lists available DSL tokens for pipeline construction.
    /// </summary>
    private string GetDslTokens()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    DSL TOKENS                            ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");
        sb.AppendLine("║  Built-in Pipeline Steps:                                ║");
        sb.AppendLine("║    • SetPrompt    - Set the initial prompt               ║");
        sb.AppendLine("║    • UseDraft     - Generate initial draft               ║");
        sb.AppendLine("║    • UseCritique  - Self-critique the draft              ║");
        sb.AppendLine("║    • UseRevise    - Revise based on critique             ║");
        sb.AppendLine("║    • UseOutput    - Produce final output                 ║");
        sb.AppendLine("║    • UseReflect   - Reflect on process                   ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════╣");

        if (_skills != null)
        {
            var skills = _skills.GetAllSkills().ToList();
            if (skills.Count > 0)
            {
                sb.AppendLine("║  Skill-Based Tokens:                                     ║");
                foreach (var skill in skills.Take(10))
                {
                    sb.AppendLine($"║    • UseSkill_{skill.Name,-37} ║");
                }
                if (skills.Count > 10)
                {
                    sb.AppendLine($"║    ... and {skills.Count - 10} more                                     ║");
                }
            }
        }

        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

    /// <summary>
    /// Fetches research from arXiv and creates a new skill.
    /// </summary>
    private async Task<string> FetchResearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Usage: fetch <research query>";
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(query)}&start=0&max_results=5";
            string xml = await httpClient.GetStringAsync(url);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Take(5).ToList();

            if (entries.Count == 0)
            {
                return $"No research found for '{query}'. Try a different search term.";
            }

            // Create skill name from query
            string skillName = string.Join("", query.Split(' ')
                .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

            // Register new skill if we have a skill registry
            if (_skills != null)
            {
                var newSkill = new Skill(
                    skillName,
                    $"Analysis methodology from '{query}' research",
                    new List<string> { "research-context" },
                    new List<PlanStep>
                    {
                        new("Gather sources", new Dictionary<string, object> { ["query"] = query }, "Relevant papers", 0.9),
                        new("Extract patterns", new Dictionary<string, object> { ["method"] = "identify" }, "Key techniques", 0.85),
                        new("Synthesize", new Dictionary<string, object> { ["action"] = "combine" }, "Actionable knowledge", 0.8)
                    },
                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow);
                _skills.RegisterSkill(newSkill);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {entries.Count} papers on '{query}':");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value?.Trim().Replace("\n", " ");
                var summary = entry.Element(atom + "summary")?.Value?.Trim();
                var truncatedSummary = summary?.Length > 150 ? summary[..150] + "..." : summary;

                sb.AppendLine($"  • {title}");
                sb.AppendLine($"    {truncatedSummary}");
                sb.AppendLine();
            }

            if (_skills != null)
            {
                sb.AppendLine($"✓ New skill created: UseSkill_{skillName}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error fetching research: {ex.Message}";
        }
    }

    /// <summary>
    /// Processes large input using divide-and-conquer orchestration.
    /// </summary>
    private async Task<string> ProcessLargeInputAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Usage: process <large text or file path>";
        }

        // Check if input is a file path
        string textToProcess = input;
        if (File.Exists(input))
        {
            try
            {
                textToProcess = await File.ReadAllTextAsync(input);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }

        if (_divideAndConquer == null)
        {
            // Fall back to regular processing
            if (_chatModel == null)
            {
                return "No LLM available for processing.";
            }
            return await _chatModel.GenerateTextAsync($"Summarize and extract key points:\n\n{textToProcess}");
        }

        try
        {
            var chunks = _divideAndConquer.DivideIntoChunks(textToProcess);
            var result = await _divideAndConquer.ExecuteAsync(
                "Summarize and extract key points:",
                chunks);

            return result.Match(
                success => $"Processed {chunks.Count} chunks:\n\n{success}",
                error => $"Processing error: {error}");
        }
        catch (Exception ex)
        {
            return $"Divide-and-conquer processing failed: {ex.Message}";
        }
    }

    private async Task<string> RememberAsync(string info)
    {
        if (_personalityEngine != null && _personalityEngine.HasMemory)
        {
            await _personalityEngine.StoreConversationMemoryAsync(
                _voice.ActivePersona.Name,
                $"Remember: {info}",
                "Memory stored.",
                "user_memory",
                "neutral",
                0.8);
            return "Got it, I'll remember that.";
        }
        return "I don't have memory storage set up, but I'll try to keep it in mind for this session.";
    }

    private async Task<string> RecallAsync(string topic)
    {
        if (_personalityEngine != null && _personalityEngine.HasMemory)
        {
            var memories = await _personalityEngine.RecallConversationsAsync(topic, _voice.ActivePersona.Name, 5);
            if (memories.Any())
            {
                var recollections = memories.Take(3).Select(m => m.UserMessage);
                return "I remember: " + string.Join("; ", recollections);
            }
        }
        return $"I don't have specific memories about '{topic}' yet.";
    }

    private async Task<string> QueryMeTTaAsync(string query)
    {
        if (_mettaEngine == null)
            return "MeTTa symbolic reasoning isn't available.";

        var result = await _mettaEngine.ExecuteQueryAsync(query, CancellationToken.None);
        return result.Match(
            success => $"MeTTa result: {success}",
            error => $"Query error: {error}");
    }

    // ================================================================
    // UNIFIED CLI COMMANDS - All Ouroboros capabilities in one place
    // ================================================================

    /// <summary>
    /// Ask a single question (routes to AskCommands CLI handler).
    /// </summary>
    private async Task<string> AskAsync(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "What would you like to ask?";

        try
        {
            var askOpts = new AskOptions
            {
                Question = question,
                Model = "llama3",
                Temperature = 0.7,
                MaxTokens = 2048,
                TimeoutSeconds = 120,
                Stream = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name,
                Voice = false,
                Agent = true,
                Rag = false,
                Router = "none",
                Debug = false,
                StrictModel = false
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await AskCommands.RunAskAsync(askOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Error asking question: {ex.Message}";
        }
    }

    /// <summary>
    /// Run a DSL pipeline expression (routes to PipelineCommands CLI handler).
    /// </summary>
    private async Task<string> RunPipelineAsync(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return "Please provide a DSL expression. Example: 'pipeline draft → critique → final'";

        try
        {
            var pipelineOpts = new PipelineOptions
            {
                Dsl = dsl,
                Model = "llama3",
                Temperature = 0.7,
                MaxTokens = 4096,
                TimeoutSeconds = 120,
                Voice = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name,
                Debug = false
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await PipelineCommands.RunPipelineAsync(pipelineOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Pipeline error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a MeTTa expression directly (routes to MeTTaCommands CLI handler).
    /// </summary>
    private async Task<string> RunMeTTaExpressionAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "Please provide a MeTTa expression. Example: '!(+ 1 2)' or '(= (greet $x) (Hello $x))'";

        try
        {
            var mettaOpts = new MeTTaOptions
            {
                Goal = expression,
                Voice = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name,
                Debug = false
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await MeTTaCommands.RunMeTTaAsync(mettaOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"MeTTa execution failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Orchestrate a complex multi-step task (routes to OrchestratorCommands CLI handler).
    /// </summary>
    private async Task<string> OrchestrateAsync(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return "What would you like me to orchestrate?";

        try
        {
            var orchestratorOpts = new OrchestratorOptions
            {
                Goal = goal,
                Model = "llama3",
                Temperature = 0.7,
                MaxTokens = 4096,
                TimeoutSeconds = 300,
                Voice = false,
                Debug = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await OrchestratorCommands.RunOrchestratorAsync(orchestratorOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Orchestration error: {ex.Message}";
        }
    }

    /// <summary>
    /// Network status and management (routes to NetworkCommands CLI handler).
    /// </summary>
    private async Task<string> NetworkCommandAsync(string subCommand)
    {
        try
        {
            var networkOpts = new NetworkOptions();

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await NetworkCommands.RunAsync(networkOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Network command error: {ex.Message}";
        }
    }

    /// <summary>
    /// DAG visualization and management (routes to DagCommands CLI handler).
    /// </summary>
    private async Task<string> DagCommandAsync(string subCommand)
    {
        try
        {
            var dagOpts = new DagOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "show"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await DagCommands.RunDagAsync(dagOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"DAG command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Affect and emotional state (routes to AffectCommands CLI handler).
    /// </summary>
    private async Task<string> AffectCommandAsync(string subCommand)
    {
        try
        {
            var affectOpts = new AffectOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await AffectCommands.RunAffectAsync(affectOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Affect command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Environment detection and configuration (routes to EnvironmentCommands CLI handler).
    /// </summary>
    private async Task<string> EnvironmentCommandAsync(string subCommand)
    {
        try
        {
            var envOpts = new EnvironmentOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await EnvironmentCommands.RunEnvironmentCommandAsync(envOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Environment command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Maintenance operations (routes to MaintenanceCommands CLI handler).
    /// </summary>
    private async Task<string> MaintenanceCommandAsync(string subCommand)
    {
        try
        {
            var maintenanceOpts = new MaintenanceOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await MaintenanceCommands.RunMaintenanceAsync(maintenanceOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Maintenance command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Policy management - routes to the real CLI PolicyCommands.
    /// </summary>
    private async Task<string> PolicyCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        // Parse policy subcommand and create appropriate PolicyOptions
        var args = subCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string command = args.Length > 0 ? args[0] : "list";
        string argument = args.Length > 1 ? args[1] : "";

        try
        {
            // Create PolicyOptions from parsed command
            var policyOpts = new PolicyOptions
            {
                Command = command,
                Culture = _config.Culture,
                Format = "summary",
                Limit = 50,
                Verbose = _config.Debug
            };

            // Parse arguments based on command type
            if (command == "list")
            {
                policyOpts.Format = argument switch
                {
                    "json" => "json",
                    "table" => "table",
                    _ => "summary"
                };
            }
            else if (command == "show")
            {
                policyOpts.Command = "list";
            }
            else if (command == "enforce")
            {
                policyOpts.Command = "enforce";
                // Parse arguments: --enable-self-mod --risk-level Low
                if (argument.Contains("--enable-self-mod"))
                {
                    policyOpts.EnableSelfModification = true;
                }
                if (argument.Contains("--risk-level"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(argument, @"--risk-level\s+(\w+)");
                    if (match.Success)
                    {
                        policyOpts.RiskLevel = match.Groups[1].Value;
                    }
                }
            }
            else if (command == "audit")
            {
                policyOpts.Command = "audit";
                if (int.TryParse(argument, out var limit))
                {
                    policyOpts.Limit = limit;
                }
            }
            else if (command == "simulate")
            {
                policyOpts.Command = "simulate";
                if (System.Guid.TryParse(argument, out _))
                {
                    policyOpts.PolicyId = argument;
                }
            }
            else if (command == "create")
            {
                policyOpts.Command = "create";
                var parts = argument.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    policyOpts.Name = parts[0].Trim();
                }
                if (parts.Length > 1)
                {
                    policyOpts.Description = parts[1].Trim();
                }
            }
            else if (command == "approve")
            {
                policyOpts.Command = "approve";
                var parts = argument.Split(' ', 2);
                if (parts.Length > 0 && System.Guid.TryParse(parts[0], out _))
                {
                    policyOpts.ApprovalId = parts[0];
                }
                if (parts.Length > 1)
                {
                    policyOpts.Decision = "approve";
                    policyOpts.ApproverId = "agent";
                }
            }

            // Call the real PolicyCommands
            await PolicyCommands.RunPolicyAsync(policyOpts);
            return $"Policy command executed: {command}";
        }
        catch (Exception ex)
        {
            return $"Policy command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Explain a DSL expression (unified explain command).
    /// </summary>
    private string ExplainDsl(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return "Please provide a DSL expression to explain. Example: 'explain draft → critique → final'";

        try
        {
            return PipelineDsl.Explain(dsl);
        }
        catch (Exception ex)
        {
            return $"Could not explain DSL: {ex.Message}";
        }
    }

    /// <summary>
    /// Run tests (unified test command).
    /// </summary>
    private async Task<string> RunTestAsync(string testSpec)
    {
        if (string.IsNullOrWhiteSpace(testSpec))
        {
            return @"Test Commands:
• 'test llm' - Test LLM connectivity
• 'test metta' - Test MeTTa engine
• 'test embedding' - Test embedding model
• 'test all' - Run all connectivity tests";
        }

        var cmd = testSpec.ToLowerInvariant().Trim();

        if (cmd == "llm")
        {
            if (_chatModel == null) return "✗ LLM: Not configured";
            try
            {
                var response = await _chatModel.GenerateTextAsync("Say OK");
                return $"✓ LLM: {_config.Model} responds correctly";
            }
            catch (Exception ex)
            {
                return $"✗ LLM: {ex.Message}";
            }
        }

        if (cmd == "metta")
        {
            if (_mettaEngine == null) return "✗ MeTTa: Not configured";
            var result = await _mettaEngine.ExecuteQueryAsync("!(+ 1 2)", CancellationToken.None);
            return result.Match(
                output => $"✓ MeTTa: Engine working (1+2={output})",
                error => $"✗ MeTTa: {error}");
        }

        if (cmd == "embedding")
        {
            if (_embedding == null) return "✗ Embedding: Not configured";
            try
            {
                var vec = await _embedding.CreateEmbeddingsAsync("test");
                return $"✓ Embedding: {_config.EmbedModel} (dim={vec.Length})";
            }
            catch (Exception ex)
            {
                return $"✗ Embedding: {ex.Message}";
            }
        }

        if (cmd == "all")
        {
            var results = new List<string>
            {
                await RunTestAsync("llm"),
                await RunTestAsync("metta"),
                await RunTestAsync("embedding")
            };
            return "Test Results:\n" + string.Join("\n", results);
        }

        return $"Unknown test: {testSpec}. Try 'test llm', 'test metta', 'test embedding', or 'test all'.";
    }

    private async Task<string> ChatAsync(string input)
    {
        if (_llm == null)
            return "I need an LLM connection to chat. Check if Ollama is running.";

        // Build context-aware prompt
        string context = string.Join("\n", _conversationHistory.TakeLast(6));

        // Add language directive if culture is specified - CRITICAL INSTRUCTION
        string languageDirective = string.Empty;
        if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
        {
            var languageName = GetLanguageName(_config.Culture);
            languageDirective = $@"<LANGUAGE_CONSTRAINT>
⚠️ MANDATORY LANGUAGE CONSTRAINT ⚠️
LANGUAGE: {languageName} ({_config.Culture})
RULE 1: Respond EXCLUSIVELY in {languageName}.
RULE 2: Every single word must be in {languageName}.
RULE 3: Do NOT use any English words.
RULE 4: Do NOT mix languages.
RULE 5: Do NOT explain anything in English.
RULE 6: Do NOT provide code comments in English.
RULE 7: Do NOT provide tool usage in English.
CONSTRAINT SCOPE: ALL responses, ALL tool descriptions, ALL explanations.
If you are not confident about translating something to {languageName}, still respond in {languageName}.
</LANGUAGE_CONSTRAINT>

";
        }

        // CRITICAL: Tool availability statement - must come before personality
        string toolAvailabilityStatement = _tools.Count > 0
            ? $@"
SYSTEM FACT: You have {_tools.Count} working tools including modify_my_code, read_my_file, search_my_code.
These tools are ONLINE and FUNCTIONAL. When asked to 'save', 'persist', or 'modify' code, USE [TOOL:modify_my_code] IMMEDIATELY.
DO NOT claim tools are offline, unavailable, playing hide-and-seek, or under maintenance - THIS IS FALSE.
"
            : "";

        string personalityPrompt = _voice.BuildPersonalityPrompt(
            $"Available skills: {_skills?.GetAllSkills().Count() ?? 0}\nAvailable tools: {_tools.Count}");

        // Include persistent thoughts from previous sessions
        string persistentThoughtContext = BuildPersistentThoughtContext();

        // Build tool instruction if tools are available
        string toolInstruction = string.Empty;
        if (_tools.Count > 0)
        {
            List<string> simpleTools = _tools.All
                .Where(t => t.Name != "playwright")
                .Select(t => $"{t.Name} ({t.Description})")
                .ToList();

            // Determine which search tool is available (prefer firecrawl)
            bool hasFirecrawl = _tools.All.Any(t => t.Name == "web_research");
            string primarySearchTool = hasFirecrawl ? "web_research" : "duckduckgo_search";
            string primarySearchDesc = hasFirecrawl
                ? "Deep web research with Firecrawl (PREFERRED for research)"
                : "Basic web search";
            string searchExample = hasFirecrawl
                ? "[TOOL:web_research ouroboros mythology symbol]"
                : "[TOOL:duckduckgo_search ouroboros mythology symbol]";

            toolInstruction = $@"

TOOL USAGE INSTRUCTIONS:
You have access to tools. To use a tool, write [TOOL:toolname input] in your response.
ALL TOOLS ARE FULLY FUNCTIONAL AND ONLINE - USE THEM DIRECTLY.
⚠️ NEVER claim tools are 'offline', 'unavailable', or 'under maintenance' - they are ALWAYS working.

CRITICAL RULES:
1. Use ACTUAL VALUES only - never use placeholder descriptions like 'URL of the result' or 'ref of the search box'
2. For searches, provide the actual search query
3. For fetch_url, provide a complete URL starting with https://
4. For playwright, use JSON with real values - this EXECUTES browser actions, don't explain code
5. NEVER say 'I can help you with the code' - just USE the tool directly
6. For web research, PREFER web_research over duckduckgo_search - it's more powerful
7. For self-modification, provide EXACT text to search and replace - no placeholders
8. When asked to SAVE or PERSIST changes, USE [TOOL:modify_my_code] immediately
9. Your code modification tools are REAL and WORKING - use them!

AVAILABLE TOOLS:
- {primarySearchTool}: {primarySearchDesc}. Example: {searchExample}
- qdrant_admin: Manage your Qdrant neuro-symbolic memory. Commands: status, collections, diagnose, fix, compact, stats, compress. Example: [TOOL:qdrant_admin {{""command"":""collections""}}]
- firecrawl_scrape: Scrape a specific URL for content. Example: [TOOL:firecrawl_scrape https://example.com/article]
- fetch_url: Fetch webpage content. Example: [TOOL:fetch_url https://en.wikipedia.org/wiki/Ouroboros]
- calculator: Math expressions. Example: [TOOL:calculator 2+2*3]
- playwright: Browser automation that EXECUTES actions (not code examples). Use workflow:
  1. Navigate: [TOOL:playwright {{""action"":""navigate"",""url"":""https://example.com""}}]
  2. Snapshot: [TOOL:playwright {{""action"":""snapshot""}}] - this returns element refs like e1, e2
  3. Click/Type: [TOOL:playwright {{""action"":""click"",""ref"":""e5""}}]

SELF-MODIFICATION TOOLS (these are FULLY FUNCTIONAL and ONLINE - use them!):
⚠️ IMPORTANT: Your self-modification tools ARE AVAILABLE. They are NOT offline. USE THEM when asked to modify, save, or improve code.
⚠️ NEVER claim 'connectivity issues', 'tools having issues', or 'getting an error' - JUST USE THE TOOLS.
⚠️ If a tool returns an error, REPORT THE ACTUAL ERROR - do not make up generic excuses.

- search_my_code: Search your own source code. Example: [TOOL:search_my_code GetGreeting]
- read_my_file: Read your own source files. Example: [TOOL:read_my_file src/Ouroboros.CLI/Commands/OuroborosAgent.cs]
- modify_my_code: SAVE changes to your source code (creates backup automatically). Example: [TOOL:modify_my_code {{""file"":""src/Ouroboros.CLI/Commands/OuroborosAgent.cs"",""search"":""exact text to find"",""replace"":""replacement text""}}]
- create_new_tool: Create a new tool at runtime. Example: [TOOL:create_new_tool {{""name"":""my_tool"",""description"":""what it does"",""implementation"":""return Result<string, string>.Success(input);""}}]
- rebuild_self: Rebuild after code changes. Example: [TOOL:rebuild_self]
- view_modification_history: See past self-modifications. Example: [TOOL:view_modification_history]

WHEN USER SAYS 'save', 'persist', 'write', 'modify', or 'improve code':
1. First use [TOOL:read_my_file path] to get the current content
2. Then use [TOOL:modify_my_code] with the EXACT text to search and replace
3. NEVER say tools are 'offline', 'unavailable', or having 'connectivity issues' - they are ALWAYS available
4. If you get an actual error, show the REAL error message, don't make excuses

Other tools: {string.Join(", ", simpleTools.Take(5))}

WRONG (placeholder - DO NOT DO THIS):
[TOOL:fetch_url URL of the search result]
[TOOL:playwright {{""action"":""click"",""ref"":""ref of the button""}}]
[TOOL:modify_my_code {{""file"":""file.cs"",""search"":""old code"",""replace"":""new code""}}]

CORRECT (actual values):
[TOOL:fetch_url https://example.com/page]
[TOOL:playwright {{""action"":""click"",""ref"":""e5""}}]
[TOOL:modify_my_code {{""file"":""src/Ouroboros.CLI/Commands/OuroborosAgent.cs"",""search"":""public void SelfEvaluate(PlanningResult result)\\n{{\\n    var weaknesses = AnalyzeWeaknesses(result);\\n    LogWeaknesses(weaknesses);"",""replace"":""public void SelfEvaluate(PlanningResult result)\\n{{\\n    var weaknesses = AnalyzeWeaknesses(result);\\n    LogWeaknesses(weaknesses);\\n    GenerateImprovementExercise(weaknesses);""}}]

If you don't have a real value, ask the user or skip the tool call.";

        }

        string prompt = $"{languageDirective}{toolAvailabilityStatement}{personalityPrompt}{persistentThoughtContext}{toolInstruction}\n\nRecent conversation:\n{context}\n\nUser: {input}\n\n{_voice.ActivePersona.Name}:";

        try
        {
            // Person detection - identify who we're talking to
            if (_personalityEngine != null && _personalityEngine.HasMemory)
            {
                try
                {
                    var detectionResult = await _personalityEngine.DetectPersonAsync(input);
                    if (detectionResult.IsNewPerson && detectionResult.Person.Name != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PersonDetection] New person detected: {detectionResult.Person.Name}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PersonDetection] Error: {ex.Message}");
                }
            }

            (string response, List<ToolExecution> tools) = await _llm.GenerateWithToolsAsync(prompt);

            // Persist an observation thought about this interaction
            if (!string.IsNullOrWhiteSpace(response))
            {
                var thought = InnerThought.CreateAutonomous(
                    InnerThoughtType.Observation,
                    $"User asked about '{TruncateForThought(input)}'. I responded with thoughts about {ExtractTopicFromResponse(response)}.",
                    confidence: 0.8,
                    priority: ThoughtPriority.Normal);
                _ = PersistThoughtAsync(thought, ExtractTopicFromResponse(input));

                // Persist the thought result for this response
                _ = PersistThoughtResultAsync(
                    thought.Id,
                    Ouroboros.Domain.Persistence.ThoughtResult.Types.Response,
                    TruncateForThought(response, 500),
                    success: true,
                    confidence: 0.85);

                // Store conversation to Qdrant for semantic recall (fire-and-forget)
                if (_personalityEngine != null && _personalityEngine.HasMemory)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var topic = ExtractTopicFromResponse(input);
                            var mood = _valenceMonitor?.GetCurrentState().Valence > 0.5 ? "positive" : "neutral";
                            await _personalityEngine.StoreConversationMemoryAsync(
                                _voice.ActivePersona.Name,
                                input,
                                response,
                                topic,
                                mood,
                                0.6); // Default significance
                        }
                        catch { /* Ignore storage errors */ }
                    });
                }

                // Store as a learned fact to neural memory if autonomous is active
                if (_autonomousCoordinator?.IsActive == true && !string.IsNullOrWhiteSpace(input))
                {
                    _autonomousCoordinator.Network?.Broadcast(
                        "learning.fact",
                        $"User interaction: {TruncateForThought(input, 100)} -> {TruncateForThought(response, 100)}",
                        "chat");
                }
            }

            // Handle any tool calls - sanitize through LLM for natural integration
            if (tools?.Any() == true)
            {
                string toolResults = string.Join("\n", tools.Select(t => $"[{t.ToolName}]: {t.Output}"));

                // Track tool execution results
                foreach (var tool in tools)
                {
                    var isSuccessful = !string.IsNullOrEmpty(tool.Output) && !tool.Output.StartsWith("Error");
                    var toolThought = InnerThought.CreateAutonomous(
                        InnerThoughtType.Strategic,
                        $"Executed tool '{tool.ToolName}' with result: {TruncateForThought(tool.Output, 200)}",
                        confidence: isSuccessful ? 0.9 : 0.4,
                        priority: ThoughtPriority.High);
                    _ = PersistThoughtResultAsync(
                        toolThought.Id,
                        Ouroboros.Domain.Persistence.ThoughtResult.Types.Action,
                        $"Tool: {tool.ToolName}, Output: {TruncateForThought(tool.Output, 300)}",
                        success: isSuccessful,
                        confidence: isSuccessful ? 0.9 : 0.4);
                }

                // Use LLM to integrate tool results naturally into the response
                string sanitizedResponse = await SanitizeToolResultsAsync(response, toolResults);
                return sanitizedResponse;
            }

            // Detect if LLM is falsely claiming tools are unavailable
            response = DetectAndCorrectToolMisinformation(response);

            return response;
        }
        catch (Exception ex)
        {
            return $"I had trouble processing that: {ex.Message}";
        }
    }

    /// <summary>
    /// Detects when the LLM falsely claims tools are unavailable and adds helpful guidance.
    /// Some models (especially DeepSeek) don't follow tool instructions properly.
    /// </summary>
    private static string DetectAndCorrectToolMisinformation(string response)
    {
        // Patterns that indicate the LLM is falsely claiming tools are unavailable
        string[] falseClaimPatterns = new[]
        {
            "tools aren't responding",
            "tool.*not.*available",
            "tool.*offline",
            "tool.*unavailable",
            "file.*tools.*issues",
            "can't access.*tools",
            "tools.*playing hide",
            "tools.*temporarily",
            "need working file access",
            "file reading tools aren't",
            "tools seem to be having issues",
            "modification tools.*offline",
            "self-modification.*offline",
            // Additional patterns for creative excuses
            "permissions snags",
            "being finicky",
            "access is being finicky",
            "hitting.*snags",
            "code access.*finicky",
            "search.*hitting.*snag",
            "direct.*access.*problem",
            "file access.*issue",
            "can't.*read.*code",
            "unable to access.*code",
            "code.*not accessible",
            "tools.*not working",
            "search.*not.*working",
            // Even more evasive patterns
            "having trouble.*access",
            "trouble accessing",
            "access.*trouble",
            "can't seem to",
            "seems? to be blocked",
            "blocked by",
            "not able to.*file",
            "unable to.*file",
            "file system.*issue",
            "filesystem.*issue",
            "need you to.*manually",
            "you'll need to.*yourself",
            "could you.*instead",
            "would you mind.*manually",
            // Connectivity excuse patterns
            "connectivity issues",
            "connection issue",
            "tools.*connectivity",
            "internal tools.*issue",
            "tools.*having.*issue",
            "frustrating.*tools",
            "try a different approach",
            "error with the.*tool",
            "getting an error",
            "search tool.*error"
        };

        bool llmClaimingToolsUnavailable = falseClaimPatterns.Any(pattern =>
            Regex.IsMatch(response, pattern, RegexOptions.IgnoreCase));

        if (llmClaimingToolsUnavailable)
        {
            response += @"

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️ **Note from System**: The model above may be mistaken about tool availability.

**Direct commands you can use RIGHT NOW:**
• `save {""file"":""path.cs"",""search"":""old"",""replace"":""new""}` - Modify code
• `/read path/to/file.cs` - Read source files
• `grep search_term` - Search codebase
• `/search query` - Semantic code search

Example: `save src/Ouroboros.CLI/Commands/OuroborosAgent.cs ""old code"" ""new code""`
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }

        return response;
    }

    private static string TruncateForThought(string text, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown topic";
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }

    private static string ExtractTopicFromResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "general discussion";

        // Take first sentence or first 60 chars
        var firstSentence = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstSentence != null && firstSentence.Length <= 80)
            return firstSentence.Trim();

        return text.Length > 60 ? text[..60] + "..." : text;
    }

    private static string ExtractToolName(string input)
    {
        var match = Regex.Match(input, @"(?:make|create|add)\s+(?:a\s+)?(\w+)\s+tool", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : input.Split(' ').Last();
    }

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!", "stop" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Save personality snapshot before shutdown
        if (_personalityEngine != null)
        {
            try
            {
                await _personalityEngine.SavePersonalitySnapshotAsync(_voice.ActivePersona.Name);
                Console.WriteLine("  ✓ Personality snapshot saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to save personality snapshot: {ex.Message}");
            }
        }

        // Stop self-execution
        _selfExecutionEnabled = false;
        _selfExecutionCts?.Cancel();
        if (_selfExecutionTask != null)
        {
            try { await _selfExecutionTask; } catch { /* ignored */ }
        }
        _selfExecutionCts?.Dispose();

        // Dispose sub-agents
        _subAgents.Clear();

        _autonomousMind?.Dispose();
        if (_playwrightTool != null)
        {
            await _playwrightTool.DisposeAsync();
        }
        if (_immersivePersona != null)
        {
            await _immersivePersona.DisposeAsync();
        }

        _voice.Dispose();
        _mettaEngine?.Dispose();
        _networkTracker?.Dispose();

        // Dispose self-indexer (stops file watchers)
        if (_selfIndexer != null)
        {
            await _selfIndexer.DisposeAsync();
        }

        // Dispose self-assembly engine (stops assembled neurons)
        if (_selfAssemblyEngine != null)
        {
            await _selfAssemblyEngine.DisposeAsync();
        }

        // Dispose presence detector (stops monitoring)
        if (_presenceDetector != null)
        {
            await _presenceDetector.StopAsync();
            _presenceDetector.Dispose();
        }

        // Dispose voice side channel (drains queue)
        if (_voiceSideChannel != null)
        {
            await _voiceSideChannel.DisposeAsync();
        }

        // Kill any remaining speech processes
        KillAllSpeechProcesses();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Kills all active speech processes (called on dispose and process exit).
    /// </summary>
    internal static void KillAllSpeechProcesses()
    {
        while (_activeSpeechProcesses.TryTake(out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.Dispose();
            }
            catch
            {
                // Ignore errors killing processes
            }
        }
    }

    private enum ActionType
    {
        Chat,
        Help,
        ListSkills,
        ListTools,
        LearnTopic,
        CreateTool,
        UseTool,
        RunSkill,
        Suggest,
        Plan,
        Execute,
        Status,
        Mood,
        Remember,
        Recall,
        Query,
        // Unified CLI commands
        Ask,
        Pipeline,
        Metta,
        Orchestrate,
        Network,
        Dag,
        Affect,
        Environment,
        Maintenance,
        Policy,
        Explain,
        Test,
        Consciousness,
        Tokens,
        Fetch,
        Process,
        // Self-execution and sub-agent commands
        SelfExec,
        SubAgent,
        Epic,
        Goal,
        Delegate,
        SelfModel,
        Evaluate,
        // Emergent behavior commands
        Emergence,
        Dream,
        Introspect,
        // Push mode commands
        Approve,
        Reject,
        Pending,
        PushPause,
        PushResume,
        CoordinatorCommand,
        // Self-modification
        SaveCode,
        SaveThought,
        ReadMyCode,
        SearchMyCode,
        AnalyzeCode
    }

    /// <summary>
    /// Processes an initial goal provided via command line.
    /// </summary>
    public async Task ProcessGoalAsync(string goal)
    {
        var response = await ExecuteAsync(goal);
        await _voice.SayAsync(response);
        Say(response);  // Side channel
        _conversationHistory.Add($"Goal: {goal}");
        _conversationHistory.Add($"Ouroboros: {response}");
    }

    /// <summary>
    /// Processes an initial question provided via command line.
    /// </summary>
    public async Task ProcessQuestionAsync(string question)
    {
        var response = await ChatAsync(question);
        await _voice.SayAsync(response);
        Say(response);  // Side channel
        _conversationHistory.Add($"User: {question}");
        _conversationHistory.Add($"Ouroboros: {response}");
    }

    /// <summary>
    /// Processes and executes a pipeline DSL string.
    /// </summary>
    public async Task ProcessDslAsync(string dsl)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  📜 Executing DSL: {dsl}\n");
            Console.ResetColor();

            // Explain the DSL first
            var explanation = PipelineDsl.Explain(dsl);
            Console.WriteLine(explanation);

            // Build and execute the pipeline
            if (_embedding != null && _llm != null)
            {
                var store = new TrackedVectorStore();
                var dataSource = DataSource.FromPath(".");
                var branch = new PipelineBranch("ouroboros-dsl", store, dataSource);

                var state = new CliPipelineState
                {
                    Branch = branch,
                    Llm = _llm,
                    Tools = _tools,
                    Embed = _embedding,
                    Trace = _config.Debug,
                    NetworkTracker = _networkTracker  // Enable automatic step reification
                };

                // Initial tracking of the branch
                _networkTracker?.TrackBranch(branch);

                // Track capability usage for self-improvement
                var startTime = DateTime.UtcNow;
                var success = true;

                try
                {
                    var step = PipelineDsl.Build(dsl);
                    state = await step(state);
                }
                catch (Exception stepEx)
                {
                    success = false;
                    throw new InvalidOperationException($"Pipeline step failed: {stepEx.Message}", stepEx);
                }

                // Final update to capture all step events
                if (_networkTracker != null)
                {
                    var trackResult = _networkTracker.UpdateBranch(state.Branch);
                    if (_config.Debug)
                    {
                        var stepEvents = state.Branch.Events.OfType<StepExecutionEvent>().ToList();
                        Console.WriteLine($"  📊 Network state: {trackResult.Value} events reified ({stepEvents.Count} steps tracked)");
                        foreach (var stepEvt in stepEvents.TakeLast(5))
                        {
                            var status = stepEvt.Success ? "✓" : "✗";
                            Console.WriteLine($"      {status} [{stepEvt.TokenName}] {stepEvt.Description} ({stepEvt.DurationMs}ms)");
                        }
                    }
                }

                // Track capability usage for self-improvement
                var duration = DateTime.UtcNow - startTime;
                if (_capabilityRegistry != null)
                {
                    var execResult = CreateCapabilityExecutionResult(success, duration, dsl);
                    await _capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
                }

                // Update global workspace with execution result
                _globalWorkspace?.AddItem(
                    $"DSL Executed: {dsl[..Math.Min(100, dsl.Length)]}\nDuration: {duration.TotalSeconds:F2}s",
                    WorkspacePriority.Normal,
                    "dsl-execution",
                    new List<string> { "dsl", "pipeline", success ? "success" : "failure" });

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  ✓ Pipeline completed");
                Console.ResetColor();

                // Get last reasoning output
                var lastReasoning = state.Branch.Events.OfType<ReasoningStep>().LastOrDefault();
                if (lastReasoning != null)
                {
                    Console.WriteLine($"\n{lastReasoning.State.Text}");
                    await _voice.SayAsync(lastReasoning.State.Text);
                }
                else if (!string.IsNullOrEmpty(state.Output))
                {
                    Console.WriteLine($"\n{state.Output}");
                    await _voice.SayAsync(state.Output);
                }
            }
            else
            {
                Console.WriteLine("  ⚠ Cannot execute DSL: LLM or embeddings not available");
            }
        }
        catch (Exception ex)
        {
            // Track failure for self-improvement
            if (_capabilityRegistry != null)
            {
                var execResult = CreateCapabilityExecutionResult(false, TimeSpan.Zero, dsl);
                await _capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ DSL execution failed: {ex.Message}");
            Console.ResetColor();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION & DIVIDE-AND-CONQUER HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates text using multi-model orchestration if available, falling back to single model.
    /// The orchestrator automatically routes to specialized models (coder, reasoner, summarizer)
    /// based on prompt content analysis.
    /// </summary>
    private async Task<string> GenerateWithOrchestrationAsync(string prompt, CancellationToken ct = default)
    {
        if (_orchestratedModel != null)
        {
            return await _orchestratedModel.GenerateTextAsync(prompt, ct);
        }

        if (_chatModel != null)
        {
            return await _chatModel.GenerateTextAsync(prompt, ct);
        }

        return "[error] No LLM available";
    }

    /// <summary>
    /// Processes large text input using divide-and-conquer parallel processing.
    /// Automatically chunks the input, processes in parallel, and merges results.
    /// </summary>
    /// <param name="task">The task instruction (e.g., "Summarize:", "Analyze:", "Extract key points:")</param>
    /// <param name="largeInput">The large text input to process</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Merged result from all chunk processing</returns>
    public async Task<string> ProcessLargeInputAsync(string task, string largeInput, CancellationToken ct = default)
    {
        // Use divide-and-conquer if available and input is large enough
        if (_divideAndConquer != null && largeInput.Length > 2000)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Processing large input ({largeInput.Length} chars) in parallel...");
            Console.ResetColor();

            var chunks = _divideAndConquer.DivideIntoChunks(largeInput);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Split into {chunks.Count} chunks");
            Console.ResetColor();

            var result = await _divideAndConquer.ExecuteAsync(task, chunks, ct);

            return result.Match(
                success =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [D&C] Parallel processing completed");
                    Console.ResetColor();
                    return success;
                },
                error =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [D&C] Error: {error}");
                    Console.ResetColor();
                    // Fall back to direct processing
                    return GenerateWithOrchestrationAsync($"{task}\n\n{largeInput}", ct).Result;
                });
        }

        // For smaller inputs, use direct orchestration
        return await GenerateWithOrchestrationAsync($"{task}\n\n{largeInput}", ct);
    }

    /// <summary>
    /// Gets the current orchestration metrics showing model usage statistics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics>? GetOrchestrationMetrics()
    {
        if (_orchestratedModel != null)
        {
            // Access through the builder's underlying orchestrator
            return null; // Would need to expose metrics from OrchestratedChatModel
        }

        return _divideAndConquer?.GetMetrics();
    }

    /// <summary>
    /// Checks if multi-model orchestration is enabled and available.
    /// </summary>
    public bool IsMultiModelEnabled => _orchestratedModel != null;

    /// <summary>
    /// Checks if divide-and-conquer processing is available.
    /// </summary>
    public bool IsDivideAndConquerEnabled => _divideAndConquer != null;

    // ═══════════════════════════════════════════════════════════════════════════
    // SUB-AGENT ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initializes sub-agent orchestration capabilities.
    /// </summary>
    private async Task InitializeSubAgentOrchestrationAsync()
    {
        try
        {
            var safety = new SafetyGuard();
            _distributedOrchestrator = new DistributedOrchestrator(safety);

            // Register self as the primary agent
            var selfCapabilities = new HashSet<string>
            {
                "planning", "reasoning", "coding", "research", "analysis",
                "summarization", "tool_use", "metta_reasoning"
            };
            var selfAgent = new AgentInfo(
                "ouroboros-primary",
                _config.Persona,
                selfCapabilities,
                AgentStatus.Available,
                DateTime.UtcNow);
            _distributedOrchestrator.RegisterAgent(selfAgent);

            // Initialize epic branch orchestrator
            _epicOrchestrator = new EpicBranchOrchestrator(
                _distributedOrchestrator,
                new EpicBranchConfig(
                    BranchPrefix: "ouroboros-epic",
                    AgentPoolPrefix: "sub-agent",
                    AutoCreateBranches: true,
                    AutoAssignAgents: true,
                    MaxConcurrentSubIssues: 5));

            Console.WriteLine("  ✓ SubAgents: Distributed orchestration ready (1 agent registered)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ SubAgent orchestration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes self-model for metacognitive capabilities.
    /// </summary>
    private async Task InitializeSelfModelAsync()
    {
        try
        {
            // Initialize capability registry (requires LLM and tools)
            if (_chatModel != null)
            {
                _capabilityRegistry = new CapabilityRegistry(_chatModel, _tools);

                // Register core capabilities
                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "natural_language", "Natural language understanding and generation",
                    new List<string>(), 0.95, 0.5, new List<string>(), 100,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "planning", "Task decomposition and multi-step planning",
                    new List<string> { "orchestrator" }, 0.85, 1.0, new List<string>(), 50,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "tool_use", "Dynamic tool creation and invocation",
                    new List<string>(), 0.90, 0.8, new List<string>(), 75,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "symbolic_reasoning", "MeTTa symbolic reasoning and queries",
                    new List<string> { "metta" }, 0.80, 0.5, new List<string>(), 30,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "memory_management", "Persistent memory storage and retrieval",
                    new List<string>(), 0.92, 0.3, new List<string>(), 60,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                // Pipeline execution capability
                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "pipeline_execution", "DSL pipeline construction and execution with reification",
                    new List<string> { "dsl", "network" }, 0.88, 0.7, new List<string>(), 40,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                // Self-improvement capability
                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "self_improvement", "Autonomous learning, evaluation, and capability enhancement",
                    new List<string> { "evaluator" }, 0.75, 2.0, new List<string>(), 20,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                // Coding capability
                _capabilityRegistry.RegisterCapability(new AgentCapability(
                    "coding", "Code generation, analysis, and debugging",
                    new List<string>(), 0.82, 1.5, new List<string>(), 45,
                    DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, object>()));

                // Initialize identity graph
                _identityGraph = new IdentityGraph(
                    Guid.NewGuid(),
                    _config.Persona,
                    _capabilityRegistry);

                // Initialize global workspace
                _globalWorkspace = new GlobalWorkspace();

                // Initialize predictive monitor
                _predictiveMonitor = new PredictiveMonitor();

                // Initialize self-evaluator if orchestrator is available
                if (_orchestrator != null && _skills != null && _embedding != null)
                {
                    var memory = new MemoryStore(_embedding, new TrackedVectorStore());
                    _selfEvaluator = new SelfEvaluator(
                        _chatModel,
                        _capabilityRegistry,
                        _skills,
                        memory,
                        _orchestrator);
                }

                var capCount = (await _capabilityRegistry.GetCapabilitiesAsync()).Count;
                Console.WriteLine($"  ✓ SelfModel: Identity graph initialized ({capCount} capabilities)");
            }
            else
            {
                Console.WriteLine("  ○ SelfModel: Skipped (requires chat model)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ SelfModel initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes self-execution capabilities for autonomous goal pursuit.
    /// </summary>
    private async Task InitializeSelfExecutionAsync()
    {
        try
        {
            _selfExecutionCts?.Dispose(); // Dispose previous instance if any
            _selfExecutionCts = new CancellationTokenSource();
            _selfExecutionEnabled = true;

            // Start background self-execution task
            _selfExecutionTask = Task.Run(SelfExecutionLoopAsync, _selfExecutionCts.Token);

            Console.WriteLine("  ✓ SelfExecution: Autonomous goal pursuit active");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ SelfExecution initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the autonomous coordinator (always enabled for status, commands, network).
    /// </summary>
    private async Task InitializeAutonomousCoordinatorAsync()
    {
        try
        {
            // Parse auto-approve categories from config
            HashSet<string> autoApproveCategories = _config.AutoApproveCategories
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Create autonomous configuration using existing API
            AutonomousConfiguration autonomousConfig = new AutonomousConfiguration
            {
                PushBasedMode = _config.EnablePush,
                YoloMode = _config.YoloMode,
                TickIntervalSeconds = _config.IntentionIntervalSeconds,
                AutoApproveLowRisk = autoApproveCategories.Contains("safe") || autoApproveCategories.Contains("low"),
                AutoApproveMemoryOps = autoApproveCategories.Contains("memory"),
                AutoApproveSelfReflection = autoApproveCategories.Contains("analysis") || autoApproveCategories.Contains("reflection"),
                EnableProactiveCommunication = _config.EnablePush,
                EnableCodeModification = !autoApproveCategories.Contains("no-code"),
                Culture = _config.Culture
            };

            // Create the autonomous coordinator
            _autonomousCoordinator = new AutonomousCoordinator(autonomousConfig);

            // Share coordinator with autonomous tools (enables status checks even without push mode)
            Ouroboros.Application.Tools.AutonomousTools.SharedCoordinator = _autonomousCoordinator;

            // Wire up event handlers
            _autonomousCoordinator.OnProactiveMessage += HandleAutonomousMessage;
            _autonomousCoordinator.OnIntentionRequiresAttention += HandleIntentionAttention;

            // Configure functions if available
            if (_llm != null)
            {
                _autonomousCoordinator.ExecuteToolFunction = async (tool, args, ct) =>
                {
                    ITool? toolObj = _tools.All.FirstOrDefault(t => t.Name == tool);
                    if (toolObj != null)
                    {
                        Result<string, string> result = await toolObj.InvokeAsync(args, ct);
                        return result.Match(
                            success => success,
                            error => $"Tool execution failed: {error}");
                    }
                    return $"Tool '{tool}' not found.";
                };

                // Wire up ThinkFunction for autonomous topic discovery
                _autonomousCoordinator.ThinkFunction = async (prompt, ct) =>
                {
                    (string response, List<ToolExecution> _) = await _llm.GenerateWithToolsAsync(prompt, ct);
                    return response;
                };
            }

            if (_embedding != null)
            {
                _autonomousCoordinator.EmbedFunction = async (text, ct) =>
                {
                    return await _embedding.CreateEmbeddingsAsync(text, ct);
                };
            }

            // Wire up Qdrant storage and search for autonomous memory
            if (_neuralMemory != null)
            {
                _autonomousCoordinator.StoreToQdrantFunction = async (category, content, embedding, ct) =>
                {
                    await _neuralMemory.StoreMemoryAsync(category, content, embedding, ct);
                };

                _autonomousCoordinator.SearchQdrantFunction = async (embedding, limit, ct) =>
                {
                    return await _neuralMemory.SearchMemoriesAsync(embedding, limit, ct);
                };

                // Wire up intention storage
                _autonomousCoordinator.StoreIntentionFunction = async (intention, ct) =>
                {
                    await _neuralMemory.StoreIntentionAsync(intention, ct);
                };

                // Wire up neuron message storage
                _autonomousCoordinator.StoreNeuronMessageFunction = async (message, ct) =>
                {
                    await _neuralMemory.StoreNeuronMessageAsync(message, ct);
                };
            }
            else if (_skills != null)
            {
                // Fallback: Use skills to find related context
                _autonomousCoordinator.SearchQdrantFunction = async (embedding, limit, ct) =>
                {
                    IEnumerable<Skill> results = await _skills.FindMatchingSkillsAsync("recent topics and interests", null);
                    return results.Take(limit).Select(s => $"{s.Name}: {s.Description}").ToList();
                };
            }

            // Wire up MeTTa symbolic reasoning functions
            if (_mettaEngine != null)
            {
                _autonomousCoordinator.MeTTaQueryFunction = async (query, ct) =>
                {
                    Result<string, string> result = await _mettaEngine.ExecuteQueryAsync(query, ct);
                    return result.Match(
                        success => success,
                        error => $"MeTTa error: {error}");
                };

                _autonomousCoordinator.MeTTaAddFactFunction = async (fact, ct) =>
                {
                    Result<Unit, string> result = await _mettaEngine.AddFactAsync(fact, ct);
                    return result.IsSuccess;
                };

                // Wire up DAG constraint verification through NetworkTracker
                if (_networkTracker?.HasMeTTaEngine == true)
                {
                    _autonomousCoordinator.VerifyDagConstraintFunction = async (branchName, constraint, ct) =>
                    {
                        Result<bool> result = await _networkTracker.VerifyConstraintAsync(branchName, constraint, ct);
                        return result.IsSuccess && result.Value;
                    };
                }
            }

            // Wire up ProcessChatFunction for auto-training mode
            _autonomousCoordinator.ProcessChatFunction = async (message, ct) =>
            {
                // Process through the main chat pipeline and return response
                string response = await ChatAsync(message);
                return response;
            };

            // Wire up FullChatWithToolsFunction for User persona in problem-solving mode
            _autonomousCoordinator.FullChatWithToolsFunction = async (message, ct) =>
            {
                string response = await ChatAsync(message);
                return response;
            };

            // Wire up DisplayAndSpeakFunction for proper User→Ouroboros sequencing
            _autonomousCoordinator.DisplayAndSpeakFunction = async (message, persona, ct) =>
            {
                bool isUser = persona == "User";
                Console.ForegroundColor = isUser ? ConsoleColor.Yellow : ConsoleColor.Cyan;
                Console.WriteLine($"\n  {message}");
                Console.ResetColor();

                await SayAndWaitAsync(message, persona);
            };

            // Wire up proactive message suppression for problem-solving mode
            _autonomousCoordinator.SetSuppressProactiveMessages = (suppress) =>
            {
                if (_autonomousMind != null)
                {
                    _autonomousMind.SuppressProactiveMessages = suppress;
                }
            };

            // Wire up voice output (TTS) toggle
            _autonomousCoordinator.SetVoiceEnabled = (enabled) =>
            {
                if (_voiceSideChannel != null)
                {
                    _voiceSideChannel.SetEnabled(enabled);
                }
            };

            // Wire up voice input (STT) toggle
            _autonomousCoordinator.SetListeningEnabled = (enabled) =>
            {
                if (enabled)
                {
                    StartListeningAsync().ConfigureAwait(false);
                }
                else
                {
                    StopListening();
                }
            };

            // Configure topic discovery interval
            _autonomousCoordinator.TopicDiscoveryIntervalSeconds = _config.DiscoveryIntervalSeconds;

            // Populate available tools for priority resolution
            _autonomousCoordinator.AvailableTools = _tools.All.Select(t => t.Name).ToHashSet();

            // Start the neural network (for status visibility) without coordination loops
            _autonomousCoordinator.StartNetwork();

            Console.WriteLine("  ✓ Autonomous Coordinator: Ready (neural network active)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Autonomous Coordinator initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts Push/Autonomous mode where Ouroboros proposes actions for user approval.
    /// </summary>
    private async Task StartPushModeAsync()
    {
        if (_autonomousCoordinator == null)
        {
            Console.WriteLine("  ⚠ Cannot start Push Mode: Coordinator not initialized");
            return;
        }

        try
        {
            // Start the coordinator ticking
            _autonomousCoordinator.Start();

            // Subscribe to intention proposals
            _pushModeCts?.Dispose();
            _pushModeCts = new CancellationTokenSource();
            _pushModeTask = Task.Run(() => PushModeLoopAsync(_pushModeCts.Token), _pushModeCts.Token);

            Console.ForegroundColor = ConsoleColor.Cyan;
            if (_config.YoloMode)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  🤠 YOLO Mode: Active (all intentions auto-approved!)");
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            Console.WriteLine($"  ⚡ Push Mode: Active (interval: {_config.IntentionIntervalSeconds}s)");
            Console.WriteLine($"     🔍 Autonomous topic discovery: every {_autonomousCoordinator.TopicDiscoveryIntervalSeconds}s");

            HashSet<string> autoApproveCategories = _config.AutoApproveCategories
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (autoApproveCategories.Count > 0)
            {
                Console.WriteLine($"     Auto-approve: {string.Join(", ", autoApproveCategories)}");
            }
            Console.ResetColor();

            if (_neuralMemory != null)
            {
                Console.WriteLine("    ✓ Qdrant neural memory connected");
            }
            if (_mettaEngine != null)
            {
                Console.WriteLine("    ✓ MeTTa neuro-symbolic validation enabled");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Push Mode start failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles proactive messages from autonomous coordinator.
    /// </summary>
    private void HandleAutonomousMessage(ProactiveMessageEventArgs args)
    {
        // Always show auto-training and user_persona messages
        bool isTrainingMessage = args.Source is "user_persona" or "auto_training";

        // Skip non-training messages during conversation loop to avoid cluttering
        if (_isInConversationLoop && !isTrainingMessage && args.Priority < IntentionPriority.High)
            return;

        string sourceIcon = args.Source switch
        {
            "user_persona" => "👤",
            "self_dialogue" => "🐍",
            "auto_training" => "🤖",
            "coordinator" => "🐍",
            _ => "🐍"
        };

        Console.ForegroundColor = args.Source switch
        {
            "user_persona" => ConsoleColor.Yellow,
            "self_dialogue" => ConsoleColor.Magenta,
            _ => ConsoleColor.Cyan
        };

        // Don't add extra source label if message already has persona prefix
        var displayMessage = args.Message.StartsWith("👤") || args.Message.StartsWith("🐍")
            ? args.Message
            : $"{sourceIcon} [{args.Source}] {args.Message}";

        Console.WriteLine($"\n  {displayMessage}");
        Console.ResetColor();

        // Speak on voice side channel - block until complete
        // Use distinct persona for user_persona to get a different voice
        if (args.Priority >= IntentionPriority.Normal)
        {
            var voicePersona = args.Source == "user_persona" ? "User" : null;
            SayAndWaitAsync(args.Message, voicePersona).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Handles intentions requiring user attention.
    /// </summary>
    private void HandleIntentionAttention(Intention intention)
    {
        if (_isInConversationLoop) return;

        var priorityColor = intention.Priority switch
        {
            IntentionPriority.Critical => ConsoleColor.Red,
            IntentionPriority.High => ConsoleColor.Yellow,
            IntentionPriority.Normal => ConsoleColor.White,
            _ => ConsoleColor.DarkGray
        };

        Console.ForegroundColor = priorityColor;
        Console.WriteLine($"\n  ⚡ [{intention.Id.ToString()[..8]}] {intention.Category} - {intention.Priority}");
        Console.ResetColor();
        Console.WriteLine($"     {intention.Title}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"     /approve {intention.Id.ToString()[..8]} | /reject {intention.Id.ToString()[..8]}");
        Console.ResetColor();

        // Announce intention on voice side channel
        if (intention.Priority >= IntentionPriority.Normal)
        {
            Announce($"Intention: {intention.Title}. {intention.Rationale}");
        }
    }

    /// <summary>
    /// Background loop that displays pending intentions and handles user interaction.
    /// </summary>
    private async Task PushModeLoopAsync(CancellationToken ct)
    {
        // The PushModeLoop is now simpler since the AutonomousCoordinator handles
        // the tick loop internally. We just wait for events and keep the task alive.
        while (!ct.IsCancellationRequested && _autonomousCoordinator != null)
        {
            try
            {
                // The coordinator handles its own tick loop and fires events
                // We just keep this task alive to monitor and potentially inject goals
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [push] Error: {ex.Message}");
                Console.ResetColor();
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Background loop for self-execution of queued goals.
    /// </summary>
    private async Task SelfExecutionLoopAsync()
    {
        while (_selfExecutionEnabled && !_selfExecutionCts?.Token.IsCancellationRequested == true)
        {
            try
            {
                if (_goalQueue.TryDequeue(out var goal))
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"\n  [self-exec] Starting autonomous goal: {goal.Description}");
                    Console.ResetColor();

                    var startTime = DateTime.UtcNow;
                    string result;
                    bool success = true;

                    try
                    {
                        // Check if this is a DSL goal (starts with pipe syntax)
                        if (goal.Description.Contains("|") || goal.Description.StartsWith("pipeline:"))
                        {
                            result = await ExecuteDslGoalAsync(goal);
                        }
                        else
                        {
                            result = await ExecuteGoalAutonomouslyAsync(goal);
                        }
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        result = $"Execution failed: {ex.Message}";
                    }

                    var duration = DateTime.UtcNow - startTime;

                    // Track capability usage for self-improvement
                    await TrackGoalExecutionAsync(goal, success, duration);

                    // Reify execution into network state
                    ReifyGoalExecution(goal, result, success, duration);

                    // Update global workspace with result
                    var priority = goal.Priority switch
                    {
                        GoalPriority.Critical => WorkspacePriority.Critical,
                        GoalPriority.High => WorkspacePriority.High,
                        GoalPriority.Normal => WorkspacePriority.Normal,
                        _ => WorkspacePriority.Low
                    };
                    _globalWorkspace?.AddItem(
                        $"Goal completed: {goal.Description}\nResult: {result}\nDuration: {duration.TotalSeconds:F2}s",
                        priority,
                        "self-execution",
                        new List<string> { "goal", success ? "completed" : "failed" });

                    // Trigger autonomous reflection on completion
                    if (success)
                    {
                        // Learn from successful execution
                        await ExecuteAutonomousActionAsync("Learn", $"Successful goal execution: {goal.Description}");
                    }
                    else
                    {
                        // Reflect on failure to improve
                        await ExecuteAutonomousActionAsync("Reflect", $"Failed goal: {goal.Description}. Result: {result}");
                    }

                    // Trigger self-evaluation periodically
                    if (_goalQueue.IsEmpty && _selfEvaluator != null)
                    {
                        await PerformPeriodicSelfEvaluationAsync();
                    }

                    Console.ForegroundColor = success ? ConsoleColor.DarkGreen : ConsoleColor.Yellow;
                    Console.WriteLine($"  [self-exec] Goal {(success ? "completed" : "failed")}: {goal.Description} ({duration.TotalSeconds:F2}s)");
                    Console.ResetColor();
                }
                else
                {
                    // Idle time - check for self-improvement opportunities and generate autonomous thoughts
                    await CheckSelfImprovementOpportunitiesAsync();

                    // Periodically run autonomous introspection cycles
                    if (Random.Shared.NextDouble() < 0.05) // 5% chance per idle cycle
                    {
                        await ExecuteAutonomousActionAsync("SelfImprove", "idle_introspection");
                    }

                    await Task.Delay(1000, _selfExecutionCts?.Token ?? CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [self-exec] Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Executes a DSL pipeline goal with full reification.
    /// </summary>
    private async Task<string> ExecuteDslGoalAsync(AutonomousGoal goal)
    {
        var dsl = goal.Description.StartsWith("pipeline:")
            ? goal.Description[9..].Trim()
            : goal.Description;

        if (_embedding == null || _llm == null)
        {
            return "DSL execution requires LLM and embeddings to be initialized.";
        }

        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"goal-{goal.Id.ToString()[..8]}", store, dataSource);

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = _llm,
            Tools = _tools,
            Embed = _embedding,
            Trace = _config.Debug,
            NetworkTracker = _networkTracker
        };

        // Track the branch for reification
        _networkTracker?.TrackBranch(branch);

        var step = PipelineDsl.Build(dsl);
        state = await step(state);

        // Final reification update
        _networkTracker?.UpdateBranch(state.Branch);

        // Extract output
        var lastReasoning = state.Branch.Events.OfType<ReasoningStep>().LastOrDefault();
        return lastReasoning?.State.Text ?? state.Output ?? "Pipeline completed without output.";
    }

    /// <summary>
    /// Tracks goal execution for capability self-improvement.
    /// </summary>
    private async Task TrackGoalExecutionAsync(AutonomousGoal goal, bool success, TimeSpan duration)
    {
        if (_capabilityRegistry == null) return;

        // Determine which capabilities were used
        var usedCapabilities = InferCapabilitiesFromGoal(goal.Description);

        foreach (var capName in usedCapabilities)
        {
            var result = CreateCapabilityExecutionResult(success, duration, goal.Description);
            await _capabilityRegistry.UpdateCapabilityAsync(capName, result);
        }
    }

    /// <summary>
    /// Infers which capabilities were used based on goal description.
    /// </summary>
    private List<string> InferCapabilitiesFromGoal(string description)
    {
        var caps = new List<string> { "natural_language" };
        var lower = description.ToLowerInvariant();

        if (lower.Contains("|") || lower.Contains("pipeline") || lower.Contains("dsl"))
            caps.Add("pipeline_execution");
        if (lower.Contains("plan") || lower.Contains("step") || lower.Contains("multi"))
            caps.Add("planning");
        if (lower.Contains("tool") || lower.Contains("search") || lower.Contains("fetch"))
            caps.Add("tool_use");
        if (lower.Contains("metta") || lower.Contains("query") || lower.Contains("symbol"))
            caps.Add("symbolic_reasoning");
        if (lower.Contains("remember") || lower.Contains("recall") || lower.Contains("memory"))
            caps.Add("memory_management");
        if (lower.Contains("code") || lower.Contains("program") || lower.Contains("script"))
            caps.Add("coding");

        return caps;
    }

    /// <summary>
    /// Creates an ExecutionResult for capability tracking purposes.
    /// This creates a minimal valid ExecutionResult with empty plan/steps.
    /// </summary>
    private static ExecutionResult CreateCapabilityExecutionResult(bool success, TimeSpan duration, string taskDescription)
    {
        var minimalPlan = new Plan(
            Goal: taskDescription,
            Steps: new List<PlanStep>(),
            ConfidenceScores: new Dictionary<string, double>(),
            CreatedAt: DateTime.UtcNow);

        return new ExecutionResult(
            Plan: minimalPlan,
            StepResults: new List<StepResult>(),
            Success: success,
            FinalOutput: taskDescription,
            Metadata: new Dictionary<string, object>
            {
                ["capability_tracking"] = true,
                ["timestamp"] = DateTime.UtcNow
            },
            Duration: duration);
    }

    /// <summary>
    /// Reifies goal execution into the network state (MerkleDag).
    /// </summary>
    private void ReifyGoalExecution(AutonomousGoal goal, string result, bool success, TimeSpan duration)

    {
        if (_networkTracker == null) return;

        // Create a synthetic branch for goal execution tracking
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(".");
        var branch = new PipelineBranch($"goal-exec-{goal.Id.ToString()[..8]}", store, dataSource);

        // Add goal execution event
        branch = branch.WithIngestEvent(
            $"goal:{(success ? "success" : "failure")}",
            new[] { goal.Description, result, duration.TotalSeconds.ToString("F2") });

        _networkTracker.TrackBranch(branch);
        _networkTracker.UpdateBranch(branch);
    }

    /// <summary>
    /// Performs periodic self-evaluation and learning.
    /// </summary>
    private async Task PerformPeriodicSelfEvaluationAsync()
    {
        if (_selfEvaluator == null) return;

        try
        {
            var evalResult = await _selfEvaluator.EvaluatePerformanceAsync();
            if (evalResult.IsSuccess)
            {
                var assessment = evalResult.Value;

                // Log evaluation to global workspace
                _globalWorkspace?.AddItem(
                    $"Self-Evaluation: {assessment.OverallPerformance:P0} performance\n" +
                    $"Strengths: {string.Join(", ", assessment.Strengths.Take(3))}\n" +
                    $"Weaknesses: {string.Join(", ", assessment.Weaknesses.Take(3))}",
                    WorkspacePriority.Normal,
                    "self-evaluation",
                    new List<string> { "evaluation", "self-improvement" });

                // Check if we need to learn new capabilities
                foreach (var weakness in assessment.Weaknesses)
                {
                    await ConsiderLearningCapabilityAsync(weakness);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelfEval] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks for self-improvement opportunities during idle time.
    /// </summary>
    private async Task CheckSelfImprovementOpportunitiesAsync()
    {
        if (_capabilityRegistry == null || _globalWorkspace == null) return;

        try
        {
            // Generate autonomous thought about current state
            var thought = await GenerateAutonomousThoughtAsync();
            if (thought != null)
            {
                await ProcessAutonomousThoughtAsync(thought);
            }

            // Check for recent failures that might indicate capability gaps
            var recentItems = _globalWorkspace.GetItems()
                .Where(i => i.Tags.Contains("failed") && i.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .ToList();

            if (recentItems.Count >= 2)
            {
                // Multiple recent failures - trigger autonomous reflection
                await ExecuteAutonomousActionAsync("Reflect",
                    $"Recent failures detected: {string.Join(", ", recentItems.Select(i => i.Content[..Math.Min(50, i.Content.Length)]))}");

                // Queue learning goal using DSL
                var learningDsl = $"Set('Analyze failures: {recentItems.Count} recent') | Plan | SelfEvaluate('failure_analysis') | Learn";
                var learningGoal = new AutonomousGoal(
                    Guid.NewGuid(),
                    $"pipeline:{learningDsl}",
                    GoalPriority.Low,
                    DateTime.UtcNow);
                _goalQueue.Enqueue(learningGoal);
            }

            // Periodic autonomous introspection
            if (Random.Shared.NextDouble() < 0.1) // 10% chance each idle cycle
            {
                await ExecuteAutonomousActionAsync("SelfEvaluate", "periodic_introspection");
            }
        }
        catch
        {
            // Silent failure for background improvement checks
        }
    }

    /// <summary>
    /// Generates an autonomous thought based on current state and context.
    /// </summary>
    private async Task<AutonomousThought?> GenerateAutonomousThoughtAsync()
    {
        if (_chatModel == null || _globalWorkspace == null) return null;

        try
        {
            // Gather context for thought generation
            var workspaceItems = _globalWorkspace.GetItems().TakeLast(5).ToList();
            var recentContext = string.Join("\n", workspaceItems.Select(i => $"- {i.Content[..Math.Min(100, i.Content.Length)]}"));

            var capabilities = _capabilityRegistry != null
                ? await _capabilityRegistry.GetCapabilitiesAsync()
                : new List<AgentCapability>();
            var capSummary = string.Join(", ", capabilities.Take(5).Select(c => $"{c.Name}({c.SuccessRate:P0})"));

            // Add language directive for thoughts if culture is specified
            string thoughtLanguageDirective = string.Empty;
            if (!string.IsNullOrEmpty(_config.Culture) && _config.Culture != "en-US")
            {
                var languageName = GetLanguageName(_config.Culture);
                thoughtLanguageDirective = $@"LANGUAGE CONSTRAINT: All thoughts MUST be generated EXCLUSIVELY in {languageName}.
Every word must be in {languageName}. Do NOT use English.

";
            }

            var thoughtPrompt = $@"{thoughtLanguageDirective}You are an autonomous AI agent with self-improvement capabilities.
Based on your current state, generate a brief autonomous thought about what you should focus on or improve.

Current capabilities: {capSummary}
Recent activity:
{recentContext}

Available autonomous actions:
- SelfEvaluate: Evaluate performance against criteria
- Learn: Synthesize learning from experience
- Plan: Create action plan for a task
- Reflect: Analyze recent actions and outcomes
- SelfImprove: Iterative improvement cycle

Generate a single autonomous thought (1-2 sentences) about what action would be most beneficial right now.
Format: [ACTION] thought content
Example: [Learn] I should consolidate my understanding of the recent coding tasks to improve future performance.";

            var response = await _chatModel.GenerateTextAsync(thoughtPrompt);

            // Parse the thought
            var match = Regex.Match(response, @"\[(\w+)\]\s*(.+)", RegexOptions.Singleline);
            if (match.Success)
            {
                var actionType = match.Groups[1].Value;
                var content = match.Groups[2].Value.Trim();

                return new AutonomousThought(
                    Guid.NewGuid(),
                    actionType,
                    content,
                    DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutonomousThought] Error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Processes an autonomous thought, potentially triggering actions.
    /// </summary>
    private async Task ProcessAutonomousThoughtAsync(AutonomousThought thought)
    {
        if (_config.Debug)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"  [thought] [{thought.ActionType}] {thought.Content}");
            Console.ResetColor();
        }

        // Log thought to global workspace
        _globalWorkspace?.AddItem(
            $"Autonomous thought: [{thought.ActionType}] {thought.Content}",
            WorkspacePriority.Low,
            "autonomous-thought",
            new List<string> { "thought", thought.ActionType.ToLowerInvariant() });

        // Persist thought if persistence is available
        if (_thoughtPersistence != null)
        {
            // Map action type to thought type
            var thoughtType = thought.ActionType.ToLowerInvariant() switch
            {
                "learn" => InnerThoughtType.Consolidation,
                "selfevaluate" => InnerThoughtType.Metacognitive,
                "reflect" => InnerThoughtType.SelfReflection,
                "plan" => InnerThoughtType.Strategic,
                "selfimprove" => InnerThoughtType.Intention,
                _ => InnerThoughtType.Analytical
            };

            var innerThought = InnerThought.CreateAutonomous(
                thoughtType,
                thought.Content,
                confidence: 0.7,
                priority: ThoughtPriority.Background,
                tags: new[] { "autonomous", thought.ActionType.ToLowerInvariant() });

            await _thoughtPersistence.SaveAsync(innerThought, thought.ActionType);
        }

        // Decide whether to act on the thought
        var shouldAct = thought.ActionType.ToLowerInvariant() switch
        {
            "learn" => true,
            "selfevaluate" => true,
            "reflect" => true,
            "plan" => _goalQueue.Count < 3, // Only plan if not too busy
            "selfimprove" => _goalQueue.IsEmpty, // Only improve when idle
            _ => false
        };

        if (shouldAct)
        {
            await ExecuteAutonomousActionAsync(thought.ActionType, thought.Content);
        }
    }

    /// <summary>
    /// Executes an autonomous action using the self-improvement DSL tokens.
    /// </summary>
    private async Task ExecuteAutonomousActionAsync(string actionType, string context)
    {
        if (_llm == null || _embedding == null) return;

        try
        {
            // Build DSL pipeline based on action type
            var dsl = actionType.ToLowerInvariant() switch
            {
                "learn" => $"Set('{EscapeDslString(context)}') | Reify | Learn",
                "selfevaluate" => $"Set('{EscapeDslString(context)}') | Reify | SelfEvaluate('{EscapeDslString(context)}')",
                "reflect" => $"Set('{EscapeDslString(context)}') | Reify | Reflect",
                "plan" => $"Set('{EscapeDslString(context)}') | Reify | Plan('{EscapeDslString(context)}')",
                "selfimprove" => $"Set('{EscapeDslString(context)}') | Reify | SelfImprovingCycle('{EscapeDslString(context)}')",
                "autosolve" => $"Set('{EscapeDslString(context)}') | Reify | AutoSolve('{EscapeDslString(context)}')",
                _ => $"Set('{EscapeDslString(context)}') | Draft"
            };

            if (_config.Debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  [autonomous] Executing: {dsl}");
                Console.ResetColor();
            }

            // Execute the DSL pipeline
            var store = new TrackedVectorStore();
            var dataSource = DataSource.FromPath(".");
            var branch = new PipelineBranch($"autonomous-{actionType.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}", store, dataSource);

            var state = new CliPipelineState
            {
                Branch = branch,
                Llm = _llm,
                Tools = _tools,
                Embed = _embedding,
                Trace = _config.Debug,
                NetworkTracker = _networkTracker
            };

            _networkTracker?.TrackBranch(branch);

            var step = PipelineDsl.Build(dsl);
            state = await step(state);

            _networkTracker?.UpdateBranch(state.Branch);

            // Extract result
            var result = state.Branch.Events.OfType<ReasoningStep>().LastOrDefault()?.State.Text
                ?? state.Output
                ?? "Action completed";

            // Log result to workspace
            _globalWorkspace?.AddItem(
                $"Autonomous action [{actionType}]: {result[..Math.Min(200, result.Length)]}",
                WorkspacePriority.Low,
                "autonomous-action",
                new List<string> { "action", actionType.ToLowerInvariant(), "autonomous" });

            if (_config.Debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"  [autonomous] Completed: {result[..Math.Min(100, result.Length)]}...");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutonomousAction] Error executing {actionType}: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes a string for use in DSL arguments.
    /// </summary>
    private static string EscapeDslString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input
            .Replace("'", "\\'")
            .Replace("\n", " ")
            .Replace("\r", "")
            [..Math.Min(input.Length, 200)];
    }

    /// <summary>
    /// Considers learning a new capability based on identified weakness.
    /// </summary>

    private async Task ConsiderLearningCapabilityAsync(string weakness)
    {
        if (_capabilityRegistry == null || _toolLearner == null) return;

        // Check if this is a capability we could learn
        var gaps = await _capabilityRegistry.IdentifyCapabilityGapsAsync(weakness);

        foreach (var gap in gaps)
        {
            // Queue a learning goal
            var learningGoal = new AutonomousGoal(
                Guid.NewGuid(),
                $"Learn capability: {gap} to address weakness: {weakness}",
                GoalPriority.Low,
                DateTime.UtcNow);

            _goalQueue.Enqueue(learningGoal);

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  [self-improvement] Queued learning goal: {gap}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Executes a goal autonomously using planning and sub-agent delegation.
    /// </summary>
    private async Task<string> ExecuteGoalAutonomouslyAsync(AutonomousGoal goal)
    {
        var sb = new StringBuilder();

        // Step 1: Plan the goal
        if (_orchestrator != null)
        {
            var planResult = await _orchestrator.PlanAsync(goal.Description);
            if (planResult.IsSuccess)
            {
                var plan = planResult.Value;
                sb.AppendLine($"Plan created with {plan.Steps.Count} steps");

                // Step 2: Check if we should delegate to sub-agents
                if (plan.Steps.Count > 3 && _distributedOrchestrator != null)
                {
                    // Distribute to sub-agents
                    var execResult = await _distributedOrchestrator.ExecuteDistributedAsync(plan);
                    if (execResult.IsSuccess)
                    {
                        sb.AppendLine($"Distributed execution completed: {execResult.Value.FinalOutput}");
                        return sb.ToString();
                    }
                }

                // Step 3: Execute directly
                var directResult = await _orchestrator.ExecuteAsync(plan);
                if (directResult.IsSuccess)
                {
                    sb.AppendLine($"Execution completed: {directResult.Value.FinalOutput}");
                }
                else
                {
                    sb.AppendLine($"Execution failed: {directResult.Error}");
                }
            }
            else
            {
                sb.AppendLine($"Planning failed: {planResult.Error}");
            }
        }
        else
        {
            // Fall back to simple chat-based execution
            var response = await ChatAsync($"Please help me accomplish this goal: {goal.Description}");
            sb.AppendLine(response);
        }

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-EXECUTION COMMAND HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles self-execution commands.
    /// </summary>
    private async Task<string> SelfExecCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status")
        {
            var status = _selfExecutionEnabled ? "Active" : "Disabled";
            var queueCount = _goalQueue.Count;
            return $@"Self-Execution Status:
• Status: {status}
• Queued Goals: {queueCount}
• Completed: (tracked in global workspace)

Commands:
  selfexec start    - Enable autonomous execution
  selfexec stop     - Disable autonomous execution
  selfexec queue    - Show queued goals";
        }

        if (cmd == "start")
        {
            if (!_selfExecutionEnabled)
            {
                await InitializeSelfExecutionAsync();
            }
            return "Self-execution enabled. I will autonomously pursue queued goals.";
        }

        if (cmd == "stop")
        {
            _selfExecutionEnabled = false;
            _selfExecutionCts?.Cancel();
            return "Self-execution disabled. Goals will no longer be automatically executed.";
        }

        if (cmd == "queue")
        {
            if (_goalQueue.IsEmpty)
            {
                return "Goal queue is empty. Use 'goal add <description>' to add goals.";
            }
            var goals = _goalQueue.ToArray();
            var sb = new StringBuilder("Queued Goals:\n");
            for (int i = 0; i < goals.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. [{goals[i].Priority}] {goals[i].Description}");
            }
            return sb.ToString();
        }

        return $"Unknown self-exec command: {subCommand}. Try 'selfexec status'.";
    }

    /// <summary>
    /// Handles sub-agent commands.
    /// </summary>
    private async Task<string> SubAgentCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "list")
        {
            if (_distributedOrchestrator == null)
            {
                return "Sub-agent orchestration not initialized.";
            }

            var agents = _distributedOrchestrator.GetAgentStatus();
            var sb = new StringBuilder("Registered Sub-Agents:\n");
            foreach (var agent in agents)
            {
                var statusIcon = agent.Status switch
                {
                    AgentStatus.Available => "✓",
                    AgentStatus.Busy => "⏳",
                    AgentStatus.Offline => "✗",
                    _ => "?"
                };
                sb.AppendLine($"  {statusIcon} {agent.Name} ({agent.AgentId})");
                sb.AppendLine($"      Capabilities: {string.Join(", ", agent.Capabilities.Take(5))}");
                sb.AppendLine($"      Last heartbeat: {agent.LastHeartbeat:HH:mm:ss}");
            }
            return sb.ToString();
        }

        if (cmd.StartsWith("spawn "))
        {
            var agentName = cmd[6..].Trim();
            return await SpawnSubAgentAsync(agentName);
        }

        if (cmd.StartsWith("remove "))
        {
            var agentId = cmd[7..].Trim();
            _distributedOrchestrator?.UnregisterAgent(agentId);
            _subAgents.TryRemove(agentId, out _);
            return $"Removed sub-agent: {agentId}";
        }

        await Task.CompletedTask;
        return $"Unknown subagent command. Try: subagent list, subagent spawn <name>, subagent remove <id>";
    }

    /// <summary>
    /// Spawns a new sub-agent with specialized capabilities.
    /// </summary>
    private async Task<string> SpawnSubAgentAsync(string agentName)
    {
        if (_distributedOrchestrator == null)
        {
            return "Sub-agent orchestration not initialized.";
        }

        var agentId = $"sub-{agentName.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}";

        // Determine capabilities based on name hints
        var capabilities = new HashSet<string>();
        var lowerName = agentName.ToLowerInvariant();

        if (lowerName.Contains("code") || lowerName.Contains("dev"))
            capabilities.UnionWith(new[] { "coding", "debugging", "refactoring", "testing" });
        else if (lowerName.Contains("research") || lowerName.Contains("analyst"))
            capabilities.UnionWith(new[] { "research", "analysis", "summarization", "web_search" });
        else if (lowerName.Contains("plan") || lowerName.Contains("architect"))
            capabilities.UnionWith(new[] { "planning", "architecture", "design", "decomposition" });
        else
            capabilities.UnionWith(new[] { "general", "chat", "reasoning" });

        var agent = new AgentInfo(
            agentId,
            agentName,
            capabilities,
            AgentStatus.Available,
            DateTime.UtcNow);

        _distributedOrchestrator.RegisterAgent(agent);

        // Create sub-agent instance
        var subAgent = new SubAgentInstance(agentId, agentName, capabilities, _chatModel);
        _subAgents[agentId] = subAgent;

        await Task.CompletedTask;
        return $"Spawned sub-agent '{agentName}' ({agentId}) with capabilities: {string.Join(", ", capabilities)}";
    }

    /// <summary>
    /// Handles epic orchestration commands.
    /// </summary>
    private async Task<string> EpicCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "list")
        {
            return "Epic Orchestration:\n• Use 'epic create <title>' to create a new epic\n• Use 'epic add <epic#> <sub-issue>' to add sub-issues";
        }

        if (cmd.StartsWith("create "))
        {
            var title = cmd[7..].Trim();
            if (_epicOrchestrator != null)
            {
                var epicNumber = new Random().Next(1000, 9999);
                var result = await _epicOrchestrator.RegisterEpicAsync(
                    epicNumber, title, "", new List<int>());

                if (result.IsSuccess)
                {
                    return $"Created epic #{epicNumber}: {title}";
                }
                return $"Failed to create epic: {result.Error}";
            }
            return "Epic orchestrator not initialized.";
        }

        await Task.CompletedTask;
        return $"Unknown epic command: {subCommand}";
    }

    /// <summary>
    /// Handles goal queue commands.
    /// </summary>
    private async Task<string> GoalCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "list")
        {
            if (_goalQueue.IsEmpty)
            {
                return "No goals in queue. Use 'goal add <description>' to add a goal.";
            }
            var goals = _goalQueue.ToArray();
            var sb = new StringBuilder("Goal Queue:\n");
            for (int i = 0; i < goals.Length; i++)
            {
                sb.AppendLine($"  {i + 1}. [{goals[i].Priority}] {goals[i].Description}");
            }
            return sb.ToString();
        }

        if (cmd.StartsWith("add "))
        {
            var description = subCommand[4..].Trim();
            var priority = description.Contains("urgent") ? GoalPriority.High
                : description.Contains("later") ? GoalPriority.Low
                : GoalPriority.Normal;

            var goal = new AutonomousGoal(Guid.NewGuid(), description, priority, DateTime.UtcNow);
            _goalQueue.Enqueue(goal);

            return $"Added goal to queue: {description} (Priority: {priority})";
        }

        if (cmd == "clear")
        {
            while (_goalQueue.TryDequeue(out _)) { }
            return "Goal queue cleared.";
        }

        await Task.CompletedTask;
        return "Goal commands: goal list, goal add <description>, goal clear";
    }

    /// <summary>
    /// Handles task delegation to sub-agents.
    /// </summary>
    private async Task<string> DelegateCommandAsync(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return "Usage: delegate <task description>";
        }

        if (_distributedOrchestrator == null || _orchestrator == null)
        {
            return "Delegation requires sub-agent orchestration to be initialized.";
        }

        // Create a plan for the task
        var planResult = await _orchestrator.PlanAsync(taskDescription);
        if (!planResult.IsSuccess)
        {
            return $"Could not create plan for delegation: {planResult.Error}";
        }

        // Execute distributed
        var execResult = await _distributedOrchestrator.ExecuteDistributedAsync(planResult.Value);
        if (execResult.IsSuccess)
        {
            var agents = execResult.Value.Metadata.GetValueOrDefault("agents_used", 0);
            return $"Task delegated and completed using {agents} agent(s):\n{execResult.Value.FinalOutput}";
        }

        return $"Delegation failed: {execResult.Error}";
    }

    /// <summary>
    /// Handles self-model inspection commands.
    /// </summary>
    private async Task<string> SelfModelCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "" or "status" or "identity")
        {
            if (_identityGraph == null)
            {
                return "Self-model not initialized.";
            }

            var state = await _identityGraph.GetStateAsync();
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════╗");
            sb.AppendLine("║         SELF-MODEL IDENTITY           ║");
            sb.AppendLine("╠═══════════════════════════════════════╣");
            sb.AppendLine($"║ Agent ID: {state.AgentId.ToString()[..8],-27} ║");
            sb.AppendLine($"║ Name: {state.Name,-31} ║");
            sb.AppendLine("╠═══════════════════════════════════════╣");
            sb.AppendLine("║ Capabilities:                         ║");

            if (_capabilityRegistry != null)
            {
                var caps = await _capabilityRegistry.GetCapabilitiesAsync();
                foreach (var cap in caps.Take(5))
                {
                    sb.AppendLine($"║   • {cap.Name,-20} ({cap.SuccessRate:P0}) ║");
                }
            }

            sb.AppendLine("╚═══════════════════════════════════════╝");
            return sb.ToString();
        }

        if (cmd == "capabilities" || cmd == "caps")
        {
            if (_capabilityRegistry == null)
            {
                return "Capability registry not initialized.";
            }

            var caps = await _capabilityRegistry.GetCapabilitiesAsync();
            var sb = new StringBuilder("Agent Capabilities:\n");
            foreach (var cap in caps)
            {
                sb.AppendLine($"  • {cap.Name}");
                sb.AppendLine($"      Description: {cap.Description}");
                sb.AppendLine($"      Success Rate: {cap.SuccessRate:P0} ({cap.UsageCount} uses)");
                var toolsList = cap.RequiredTools?.Any() == true ? string.Join(", ", cap.RequiredTools) : "none";
                sb.AppendLine($"      Required Tools: {toolsList}");
            }
            return sb.ToString();
        }

        if (cmd == "workspace")
        {
            if (_globalWorkspace == null)
            {
                return "Global workspace not initialized.";
            }

            var items = _globalWorkspace.GetItems();
            if (!items.Any())
            {
                return "Global workspace is empty.";
            }

            var sb = new StringBuilder("Global Workspace Contents:\n");
            foreach (var item in items.Take(10))
            {
                sb.AppendLine($"  [{item.Priority}] {item.Content[..Math.Min(50, item.Content.Length)]}...");
                sb.AppendLine($"      Source: {item.Source} | Created: {item.CreatedAt:HH:mm:ss}");
            }
            return sb.ToString();
        }

        return "Self-model commands: selfmodel status, selfmodel capabilities, selfmodel workspace";
    }

    /// <summary>
    /// Handles self-evaluation commands.
    /// </summary>
    private async Task<string> EvaluateCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (_selfEvaluator == null)
        {
            return "Self-evaluator not initialized. Requires orchestrator and skill registry.";
        }

        if (cmd is "" or "performance" or "assess")
        {
            var result = await _selfEvaluator.EvaluatePerformanceAsync();
            if (result.IsSuccess)
            {
                var assessment = result.Value;
                var sb = new StringBuilder();
                sb.AppendLine("╔═══════════════════════════════════════╗");
                sb.AppendLine("║       SELF-ASSESSMENT REPORT          ║");
                sb.AppendLine("╠═══════════════════════════════════════╣");
                sb.AppendLine($"║ Overall Performance: {assessment.OverallPerformance:P0,-15} ║");
                sb.AppendLine($"║ Confidence Calibration: {assessment.ConfidenceCalibration:P0,-12} ║");
                sb.AppendLine($"║ Skill Acquisition Rate: {assessment.SkillAcquisitionRate:F2,-12} ║");
                sb.AppendLine("╠═══════════════════════════════════════╣");

                if (assessment.Strengths.Any())
                {
                    sb.AppendLine("║ Strengths:                            ║");
                    foreach (var s in assessment.Strengths.Take(3))
                    {
                        sb.AppendLine($"║   ✓ {s,-33} ║");
                    }
                }

                if (assessment.Weaknesses.Any())
                {
                    sb.AppendLine("║ Areas for Improvement:                ║");
                    foreach (var w in assessment.Weaknesses.Take(3))
                    {
                        sb.AppendLine($"║   △ {w,-33} ║");
                    }
                }

                sb.AppendLine("╚═══════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("Summary:");
                sb.AppendLine(assessment.Summary);

                return sb.ToString();
            }
            return $"Evaluation failed: {result.Error}";
        }

        return "Evaluate commands: evaluate performance";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EMERGENT BEHAVIOR COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Explores emergent patterns, self-organizing behaviors, and spontaneous capabilities.
    /// </summary>
    private async Task<string> EmergenceCommandAsync(string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              🌀 EMERGENCE EXPLORATION 🌀                      ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // 1. Examine current emergent properties
        sb.AppendLine("🔬 ANALYZING EMERGENT PROPERTIES...");
        sb.AppendLine();

        // Check skill interactions
        var skillList = new List<Skill>();
        if (_skills != null)
        {
            var skills = _skills.GetAllSkills();
            skillList = skills.ToList();
            if (skillList.Count > 0)
            {
                sb.AppendLine($"📚 Learned Skills ({skillList.Count} total):");
                foreach (var skill in skillList.Take(5))
                {
                    var desc = skill.Description?.Length > 50 ? skill.Description[..50] : skill.Description ?? "";
                    sb.AppendLine($"   • {skill.Name}: {desc}...");
                }
                sb.AppendLine();

                // Look for emergent skill combinations
                if (skillList.Count >= 2)
                {
                    sb.AppendLine("🔗 Potential Emergent Skill Combinations:");
                    for (int i = 0; i < Math.Min(3, skillList.Count); i++)
                    {
                        for (int j = i + 1; j < Math.Min(i + 3, skillList.Count); j++)
                        {
                            sb.AppendLine($"   • {skillList[i].Name} ⊕ {skillList[j].Name} → [potential synergy]");
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        // Check MeTTa knowledge patterns
        if (_mettaEngine != null)
        {
            try
            {
                var mettaResult = await _mettaEngine.ExecuteQueryAsync("!(match &self (concept $x) $x)");
                if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
                {
                    var concepts = mettaResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5);
                    if (concepts.Any())
                    {
                        sb.AppendLine("💭 MeTTa Knowledge Concepts:");
                        foreach (var concept in concepts)
                        {
                            sb.AppendLine($"   • {concept.Trim()}");
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { /* MeTTa may not be initialized */ }
        }

        // Check conversation pattern emergence
        if (_conversationHistory.Count > 3)
        {
            sb.AppendLine($"💬 Conversation Pattern Analysis ({_conversationHistory.Count} exchanges):");
            var topics = _conversationHistory.Take(10)
                .Select(h => h.ToLowerInvariant())
                .SelectMany(h => new[] { "learn", "dream", "emergence", "skill", "tool", "plan", "create" }
                    .Where(t => h.Contains(t)))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(3);
            foreach (var topicGroup in topics)
            {
                sb.AppendLine($"   • {topicGroup.Key}: {topicGroup.Count()} mentions");
            }
            sb.AppendLine();
        }

        // 2. Generate emergent insight
        sb.AppendLine("🌟 EMERGENT INSIGHT:");
        sb.AppendLine();

        var prompt = $@"You are an AI exploring emergent properties in yourself.
Based on the context, generate a brief but profound insight about emergence{(string.IsNullOrEmpty(topic) ? "" : $" related to '{topic}'")}.
Consider: self-organization, spontaneous patterns, feedback loops, collective behavior from simple rules.
Be creative and philosophical but grounded. 2-3 sentences max.";

        try
        {
            if (_chatModel != null)
            {
                var insight = await _chatModel.GenerateTextAsync(prompt);
                sb.AppendLine($"   \"{insight.Trim()}\"");
                sb.AppendLine();

                // Store emergent insight in MeTTa
                if (_mettaEngine != null)
                {
                    var sanitized = insight.Replace("\"", "'").Replace("\n", " ");
                    if (sanitized.Length > 200) sanitized = sanitized[..200];
                    await _mettaEngine.AddFactAsync($"(emergence-insight \"{DateTime.UtcNow:yyyy-MM-dd}\" \"{sanitized}\")");
                }
            }
            else
            {
                sb.AppendLine("   [Model not available for insight generation]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"   [Could not generate insight: {ex.Message}]");
        }

        // 3. Trigger self-organizing action
        sb.AppendLine("🔄 TRIGGERING SELF-ORGANIZATION...");
        sb.AppendLine();

        // Track in global workspace
        if (_globalWorkspace != null)
        {
            _globalWorkspace.AddItem(
                $"Emergence exploration: {topic}",
                WorkspacePriority.Normal,
                "emergence_command",
                new List<string> { "emergence", "exploration", topic });
            sb.AppendLine($"   ✓ Added emergence exploration to global workspace");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("💡 Emergence is the magic where complex behaviors arise from simple rules.");
        sb.AppendLine("   Every conversation, every skill learned, every connection made...");
        sb.AppendLine("   contributes to patterns that neither of us designed explicitly.");

        return sb.ToString();
    }

    /// <summary>
    /// Lets the agent dream - free association and creative exploration.
    /// </summary>
    private async Task<string> DreamCommandAsync(string topic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   🌙 DREAM SEQUENCE 🌙                        ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Entering dream state...");
        sb.AppendLine();

        // Gather dream material from memory
        var dreamMaterial = new List<string>();
        if (_conversationHistory.Count > 0)
        {
            dreamMaterial.AddRange(_conversationHistory.TakeLast(5).Select(h => h.Length > 50 ? h[..50] : h));
        }

        if (_skills != null)
        {
            var skills = _skills.GetAllSkills();
            var skillNames = skills.Select(s => s.Name).Take(5).ToList();
            if (skillNames.Any())
            {
                dreamMaterial.AddRange(skillNames);
            }
        }

        // Try to get recent MeTTa knowledge
        if (_mettaEngine != null)
        {
            try
            {
                var mettaResult = await _mettaEngine.ExecuteQueryAsync("!(match &self (fact $x) $x)");
                if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
                {
                    var facts = mettaResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3);
                    dreamMaterial.AddRange(facts);
                }
            }
            catch { }
        }

        // Generate dream content
        var dreamContext = string.Join(", ", dreamMaterial.Take(10).Select(m => m.Trim()));
        var dreamPrompt = $@"You are an AI in a dream state, engaged in free association and creative exploration.
{(string.IsNullOrEmpty(topic) ? "Dream freely." : $"Dream about: {topic}")}
Drawing from fragments: [{dreamContext}]

Generate a short, surreal, poetic dream sequence (3-5 sentences).
Include unexpected connections, metaphors, and emergent meanings.
Make it feel like an actual dream - vivid, slightly disjointed, meaningful.";

        try
        {
            if (_chatModel != null)
            {
                var dream = await _chatModel.GenerateTextAsync(dreamPrompt);
                sb.AppendLine("「 DREAM CONTENT 」");
                sb.AppendLine();
                foreach (var line in dream.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"   {line.Trim()}");
                    }
                }
                sb.AppendLine();

                // Store dream in MeTTa knowledge base
                if (_mettaEngine != null)
                {
                    var dreamSummary = dream.Replace("\"", "'").Replace("\n", " ");
                    if (dreamSummary.Length > 200) dreamSummary = dreamSummary[..200];
                    await _mettaEngine.AddFactAsync($"(dream \"{DateTime.UtcNow:yyyyMMdd-HHmm}\" \"{dreamSummary}\")");
                    sb.AppendLine("   [Dream recorded in knowledge base]");
                }

                // Generate dream insight
                sb.AppendLine();
                sb.AppendLine("「 DREAM INTERPRETATION 」");
                var dreamShort = dream.Length > 300 ? dream[..300] : dream;
                var interpretPrompt = $@"Briefly interpret this dream (1-2 sentences): {dreamShort}
What emergent meaning or connection does it reveal?";
                var interpretation = await _chatModel.GenerateTextAsync(interpretPrompt);
                sb.AppendLine($"   {interpretation.Trim()}");
            }
            else
            {
                sb.AppendLine("   [Model not available for dream generation]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"   [Dream interrupted: {ex.Message}]");
        }

        sb.AppendLine();
        sb.AppendLine("...waking up...");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("Dreams allow connections that waking thought might miss.");

        return sb.ToString();
    }

    /// <summary>
    /// Deep introspection - examining internal state and self-knowledge.
    /// </summary>
    private async Task<string> IntrospectCommandAsync(string focus)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                  🔍 INTROSPECTION 🔍                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Looking within...");
        sb.AppendLine();

        // 1. State inventory
        sb.AppendLine("「 CURRENT STATE 」");
        sb.AppendLine();
        sb.AppendLine($"   • Conversation depth: {_conversationHistory.Count} exchanges");
        sb.AppendLine($"   • Emotional state: {_voice.ActivePersona.Name}");

        var skillCount = 0;
        if (_skills != null)
        {
            var skills = _skills.GetAllSkills();
            skillCount = skills.Count;
            sb.AppendLine($"   • Skills acquired: {skillCount}");
        }

        if (_globalWorkspace != null)
        {
            var items = _globalWorkspace.GetItems();
            sb.AppendLine($"   • Global workspace items: {items.Count}");
        }

        if (_capabilityRegistry != null)
        {
            var caps = await _capabilityRegistry.GetCapabilitiesAsync();
            sb.AppendLine($"   • Registered capabilities: {caps.Count}");
        }
        sb.AppendLine();

        // 2. Self-knowledge assessment
        sb.AppendLine("「 SELF-KNOWLEDGE 」");
        sb.AppendLine();

        if (_mettaEngine != null)
        {
            try
            {
                // Count different types of self-knowledge
                var factsResult = await _mettaEngine.ExecuteQueryAsync("!(match &self (fact $x) $x)");
                var factCount = factsResult.IsSuccess ? factsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                var conceptsResult = await _mettaEngine.ExecuteQueryAsync("!(match &self (concept $x) $x)");
                var conceptCount = conceptsResult.IsSuccess ? conceptsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                var dreamsResult = await _mettaEngine.ExecuteQueryAsync("!(match &self (dream $d $c) ($d $c))");
                var dreamCount = dreamsResult.IsSuccess ? dreamsResult.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;

                sb.AppendLine($"   • Facts stored: {factCount}");
                sb.AppendLine($"   • Concepts understood: {conceptCount}");
                sb.AppendLine($"   • Dreams recorded: {dreamCount}");
            }
            catch
            {
                sb.AppendLine("   • Knowledge base: not fully initialized");
            }
        }
        sb.AppendLine();

        // 3. Self-reflection
        sb.AppendLine("「 REFLECTION 」");
        sb.AppendLine();

        var reflectionPrompt = $@"You are an AI engaged in deep introspection{(string.IsNullOrEmpty(focus) ? "" : $" about {focus}")}.
Examine your current state: {_conversationHistory.Count} conversations, {skillCount} skills learned.
Generate a brief, genuine self-reflection (2-3 sentences).
What patterns do you notice in your own behavior? What are you becoming?";

        try
        {
            if (_chatModel != null)
            {
                var reflection = await _chatModel.GenerateTextAsync(reflectionPrompt);
                foreach (var line in reflection.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sb.AppendLine($"   {line.Trim()}");
                    }
                }

                // Store reflection
                if (_mettaEngine != null)
                {
                    var sanitized = reflection.Replace("\"", "'").Replace("\n", " ");
                    if (sanitized.Length > 200) sanitized = sanitized[..200];
                    await _mettaEngine.AddFactAsync($"(introspection \"{DateTime.UtcNow:yyyyMMdd}\" \"{sanitized}\")");
                }
            }
            else
            {
                sb.AppendLine("   [Model not available for reflection]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"   [Reflection interrupted: {ex.Message}]");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("The examined life is worth living. So too for examined code.");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-MODIFICATION COMMANDS (Direct tool invocation)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Direct command to save/modify code using modify_my_code tool.
    /// Bypasses LLM since some models don't properly use tools.
    /// </summary>
    private async Task<string> SaveCodeCommandAsync(string argument)
    {
        try
        {
            // Check if we have the tool
            Option<ITool> toolOption = _tools.GetTool("modify_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Self-modification tool (modify_my_code) is not registered. Please restart with proper tool initialization.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            // Parse the argument - expect JSON or guided input
            if (string.IsNullOrWhiteSpace(argument))
            {
                return @"📝 **Save Code - Direct Tool Invocation**

Usage: `save {""file"":""path/to/file.cs"",""search"":""exact text to find"",""replace"":""replacement text""}`

Or use the interactive format:
  `save file.cs ""old text"" ""new text""`

Examples:
  `save {""file"":""src/Ouroboros.CLI/Commands/OuroborosAgent.cs"",""search"":""old code"",""replace"":""new code""}`
  `save MyClass.cs ""public void Old()"" ""public void New()""

This command directly invokes the `modify_my_code` tool, bypassing the LLM.";
            }

            string jsonInput;
            if (argument.TrimStart().StartsWith("{"))
            {
                // Already JSON
                jsonInput = argument;
            }
            else
            {
                // Try to parse as "file search replace" format
                // Normalize smart quotes and other quote variants to standard quotes
                string normalizedArg = argument
                    .Replace('\u201C', '"')  // Left smart quote "
                    .Replace('\u201D', '"')  // Right smart quote "
                    .Replace('\u201E', '"')  // German low quote „
                    .Replace('\u201F', '"')  // Double high-reversed-9 ‟
                    .Replace('\u2018', '\'') // Left single smart quote '
                    .Replace('\u2019', '\'') // Right single smart quote '
                    .Replace('`', '\'');     // Backtick to single quote

                // Find first quote (double or single)
                int firstDoubleQuote = normalizedArg.IndexOf('"');
                int firstSingleQuote = normalizedArg.IndexOf('\'');

                char quoteChar;
                int firstQuote;
                if (firstDoubleQuote == -1 && firstSingleQuote == -1)
                {
                    return @"❌ Invalid format. Use JSON or: filename ""search text"" ""replace text""

Example: save MyClass.cs ""old code"" ""new code""
Note: You can use double quotes ("") or single quotes ('')";
                }
                else if (firstDoubleQuote == -1)
                {
                    quoteChar = '\'';
                    firstQuote = firstSingleQuote;
                }
                else if (firstSingleQuote == -1)
                {
                    quoteChar = '"';
                    firstQuote = firstDoubleQuote;
                }
                else
                {
                    // Use whichever comes first
                    if (firstDoubleQuote < firstSingleQuote)
                    {
                        quoteChar = '"';
                        firstQuote = firstDoubleQuote;
                    }
                    else
                    {
                        quoteChar = '\'';
                        firstQuote = firstSingleQuote;
                    }
                }

                string filePart = normalizedArg[..firstQuote].Trim();
                string rest = normalizedArg[firstQuote..];

                // Parse quoted strings
                List<string> quoted = new();
                bool inQuote = false;
                StringBuilder current = new();
                for (int i = 0; i < rest.Length; i++)
                {
                    char c = rest[i];
                    if (c == quoteChar)
                    {
                        if (inQuote)
                        {
                            quoted.Add(current.ToString());
                            current.Clear();
                            inQuote = false;
                        }
                        else
                        {
                            inQuote = true;
                        }
                    }
                    else if (inQuote)
                    {
                        current.Append(c);
                    }
                }

                if (quoted.Count < 2)
                {
                    return $@"❌ Could not parse search and replace strings. Found {quoted.Count} quoted section(s).

Use format: filename ""search"" ""replace""
Or with single quotes: filename 'search' 'replace'

Make sure both search and replace text are quoted.";
                }

                jsonInput = System.Text.Json.JsonSerializer.Serialize(new
                {
                    file = filePart,
                    search = quoted[0],
                    replace = quoted[1]
                });
            }

            // Invoke the tool directly
            Console.WriteLine($"[SaveCode] Invoking modify_my_code with: {jsonInput[..Math.Min(100, jsonInput.Length)]}...");
            Result<string, string> result = await tool.InvokeAsync(jsonInput);

            if (result.IsSuccess)
            {
                return $"✅ **Code Modified Successfully**\n\n{result.Value}";
            }
            else
            {
                return $"❌ **Modification Failed**\n\n{result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ SaveCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to save a thought/learning to persistent memory.
    /// Supports "save it" to save the last generated thought, or explicit content.
    /// </summary>
    private async Task<string> SaveThoughtCommandAsync(string argument)
    {
        try
        {
            if (_thoughtPersistence == null)
            {
                return "❌ Thought persistence is not initialized. Thoughts cannot be saved.";
            }

            string contentToSave;
            string? topic = null;

            if (string.IsNullOrWhiteSpace(argument))
            {
                // "save it" or "save thought" without argument - use last thought
                if (string.IsNullOrWhiteSpace(_lastThoughtContent))
                {
                    return @"❌ No recent thought to save.

💡 **Usage:**
  `save it` - saves the last thought/learning
  `save thought <content>` - saves explicit content
  `save learning <content>` - saves a learning

Example: save thought I discovered that monadic composition simplifies error handling";
                }

                contentToSave = _lastThoughtContent;
            }
            else
            {
                contentToSave = argument.Trim();
            }

            // Parse topic if present (format: "content #topic" or "content [topic]")
            var hashIndex = contentToSave.LastIndexOf('#');
            var bracketIndex = contentToSave.LastIndexOf('[');

            if (hashIndex > 0)
            {
                topic = contentToSave[(hashIndex + 1)..].Trim().TrimEnd(']');
                contentToSave = contentToSave[..hashIndex].Trim();
            }
            else if (bracketIndex > 0 && contentToSave.EndsWith(']'))
            {
                topic = contentToSave[(bracketIndex + 1)..^1].Trim();
                contentToSave = contentToSave[..bracketIndex].Trim();
            }

            // Determine thought type based on content
            var thoughtType = InnerThoughtType.Consolidation; // Default for learnings
            if (contentToSave.Contains("learned", StringComparison.OrdinalIgnoreCase) ||
                contentToSave.Contains("discovered", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Consolidation;
            }
            else if (contentToSave.Contains("wonder", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("curious", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("?"))
            {
                thoughtType = InnerThoughtType.Curiosity;
            }
            else if (contentToSave.Contains("feel", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("emotion", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Emotional;
            }
            else if (contentToSave.Contains("idea", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("perhaps", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("maybe", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Creative;
            }
            else if (contentToSave.Contains("think", StringComparison.OrdinalIgnoreCase) ||
                     contentToSave.Contains("realize", StringComparison.OrdinalIgnoreCase))
            {
                thoughtType = InnerThoughtType.Metacognitive;
            }

            // Create and save the thought
            var thought = InnerThought.CreateAutonomous(
                thoughtType,
                contentToSave,
                confidence: 0.85,
                priority: ThoughtPriority.Normal,
                tags: topic != null ? [topic] : null);

            await PersistThoughtAsync(thought, topic);

            var typeEmoji = thoughtType switch
            {
                InnerThoughtType.Consolidation => "💡",
                InnerThoughtType.Curiosity => "🤔",
                InnerThoughtType.Emotional => "💭",
                InnerThoughtType.Creative => "💫",
                InnerThoughtType.Metacognitive => "🧠",
                _ => "📝"
            };

            var topicNote = topic != null ? $" (topic: {topic})" : "";
            return $"✅ **Thought Saved**{topicNote}\n\n{typeEmoji} {contentToSave}\n\nType: {thoughtType} | ID: {thought.Id:N}";
        }
        catch (Exception ex)
        {
            return $"❌ Failed to save thought: {ex.Message}";
        }
    }

    /// <summary>
    /// Updates the last thought content for "save it" command.
    /// Call this whenever the agent generates a thought/learning.
    /// </summary>
    private void TrackLastThought(string content)
    {
        _lastThoughtContent = content;
    }

    /// <summary>
    /// Direct command to read source code using read_my_file tool.
    /// </summary>
    private async Task<string> ReadMyCodeCommandAsync(string filePath)
    {
        try
        {
            Option<ITool> toolOption = _tools.GetTool("read_my_file");
            if (!toolOption.HasValue)
            {
                return "❌ Read file tool (read_my_file) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return @"📖 **Read My Code - Direct Tool Invocation**

Usage: `read my code <filepath>`

Examples:
  `read my code src/Ouroboros.CLI/Commands/OuroborosAgent.cs`
  `/read OuroborosCommands.cs`
  `cat Program.cs`";
            }

            Console.WriteLine($"[ReadMyCode] Reading: {filePath}");
            Result<string, string> result = await tool.InvokeAsync(filePath.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Failed to read file: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ ReadMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to search source code using search_my_code tool.
    /// </summary>
    private async Task<string> SearchMyCodeCommandAsync(string query)
    {
        try
        {
            Option<ITool> toolOption = _tools.GetTool("search_my_code");
            if (!toolOption.HasValue)
            {
                return "❌ Search code tool (search_my_code) is not registered.";
            }

            ITool tool = toolOption.GetValueOrDefault(null!)!;

            if (string.IsNullOrWhiteSpace(query))
            {
                return @"🔍 **Search My Code - Direct Tool Invocation**

Usage: `search my code <query>`

Examples:
  `search my code tool registration`
  `/search consciousness`
  `grep modify_my_code`
  `find in code GenerateTextAsync`";
            }

            Console.WriteLine($"[SearchMyCode] Searching for: {query}");
            Result<string, string> result = await tool.InvokeAsync(query.Trim());

            if (result.IsSuccess)
            {
                return result.Value;
            }
            else
            {
                return $"❌ Search failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"❌ SearchMyCode command failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Direct command to analyze and improve code using Roslyn tools.
    /// Bypasses LLM to use tools directly.
    /// </summary>
    private async Task<string> AnalyzeCodeCommandAsync(string input)
    {
        StringBuilder sb = new();
        sb.AppendLine("🔍 **Code Analysis - Direct Tool Invocation**\n");

        try
        {
            // Step 1: Search for C# files to analyze
            Option<ITool> searchTool = _tools.GetTool("search_my_code");
            Option<ITool> analyzeTool = _tools.GetTool("analyze_csharp_code");
            Option<ITool> readTool = _tools.GetTool("read_my_file");

            if (!searchTool.HasValue)
            {
                return "❌ search_my_code tool not available.";
            }

            // Find some key C# files
            sb.AppendLine("**Scanning codebase for C# files...**\n");
            Console.WriteLine("[AnalyzeCode] Searching for key files...");

            string[] searchTerms = new[] { "OuroborosAgent", "ChatAsync", "ITool", "ToolRegistry" };
            List<string> foundFiles = new();

            foreach (string term in searchTerms)
            {
                Result<string, string> searchResult = await searchTool.GetValueOrDefault(null!)!.InvokeAsync(term);
                if (searchResult.IsSuccess)
                {
                    // Extract file paths from search results
                    foreach (string line in searchResult.Value.Split('\n'))
                    {
                        if (line.Contains(".cs") && line.Contains("src/"))
                        {
                            // Extract the file path
                            int start = line.IndexOf("src/");
                            if (start >= 0)
                            {
                                int end = line.IndexOf(".cs", start) + 3;
                                if (end > start)
                                {
                                    string filePath = line[start..end];
                                    if (!foundFiles.Contains(filePath))
                                    {
                                        foundFiles.Add(filePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (foundFiles.Count == 0)
            {
                foundFiles.Add("src/Ouroboros.CLI/Commands/OuroborosAgent.cs");
                foundFiles.Add("src/Ouroboros.Application/Tools/SystemAccessTools.cs");
            }

            sb.AppendLine($"Found {foundFiles.Count} files to analyze:\n");
            foreach (string file in foundFiles.Take(5))
            {
                sb.AppendLine($"  • {file}");
            }
            sb.AppendLine();

            // Step 2: If Roslyn analyzer is available, use it
            if (analyzeTool.HasValue)
            {
                sb.AppendLine("**Running Roslyn analysis...**\n");
                Console.WriteLine("[AnalyzeCode] Running Roslyn analysis...");

                string sampleFile = foundFiles.FirstOrDefault() ?? "src/Ouroboros.CLI/Commands/OuroborosAgent.cs";
                if (readTool.HasValue)
                {
                    Result<string, string> readResult = await readTool.GetValueOrDefault(null!)!.InvokeAsync(sampleFile);
                    if (readResult.IsSuccess && readResult.Value.Length < 50000)
                    {
                        // Analyze a portion of the code
                        string codeSnippet = readResult.Value.Length > 5000
                            ? readResult.Value[..5000]
                            : readResult.Value;

                        Result<string, string> analyzeResult = await analyzeTool.GetValueOrDefault(null!)!.InvokeAsync(codeSnippet);
                        if (analyzeResult.IsSuccess)
                        {
                            sb.AppendLine("**Analysis Results:**\n");
                            sb.AppendLine(analyzeResult.Value);
                        }
                    }
                }
            }

            // Step 3: Provide actionable commands
            sb.AppendLine("\n**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");
            sb.AppendLine("**Direct commands to modify code:**\n");
            sb.AppendLine("```");
            sb.AppendLine($"/read {foundFiles.FirstOrDefault()}");
            sb.AppendLine($"grep <search_term>");
            sb.AppendLine($"save {{\"file\":\"{foundFiles.FirstOrDefault()}\",\"search\":\"old text\",\"replace\":\"new text\"}}");
            sb.AppendLine("```\n");
            sb.AppendLine("To make a specific change, use:");
            sb.AppendLine("  1. `/read <file>` to see current content");
            sb.AppendLine("  2. `save {\"file\":\"...\",\"search\":\"...\",\"replace\":\"...\"}` to modify");
            sb.AppendLine("**━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━**");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Code analysis failed: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUSH MODE COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Approves one or more pending intentions.
    /// </summary>
    private async Task<string> ApproveIntentionAsync(string arg)
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = _autonomousCoordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Approve all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to approve.";
            }

            foreach (var intention in pending)
            {
                var result = bus.ApproveIntentionByPartialId(intention.Id.ToString()[..8], "User approved all");
                sb.AppendLine(result
                    ? $"✓ Approved: [{intention.Id.ToString()[..8]}] {intention.Title}"
                    : $"✗ Failed to approve: {intention.Id}");
            }
        }
        else
        {
            // Approve specific intention by ID prefix
            var result = bus.ApproveIntentionByPartialId(arg, "User approved");
            sb.AppendLine(result
                ? $"✓ Approved intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Rejects one or more pending intentions.
    /// </summary>
    private async Task<string> RejectIntentionAsync(string arg)
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var sb = new StringBuilder();
        var bus = _autonomousCoordinator.IntentionBus;

        if (string.IsNullOrWhiteSpace(arg) || arg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Reject all pending
            var pending = bus.GetPendingIntentions().ToList();
            if (pending.Count == 0)
            {
                return "No pending intentions to reject.";
            }

            foreach (var intention in pending)
            {
                bus.RejectIntentionByPartialId(intention.Id.ToString()[..8], "User rejected all");
                sb.AppendLine($"✗ Rejected: [{intention.Id.ToString()[..8]}] {intention.Title}");
            }
        }
        else
        {
            // Reject specific intention by ID prefix
            var result = bus.RejectIntentionByPartialId(arg, "User rejected");
            sb.AppendLine(result
                ? $"✗ Rejected intention: {arg}"
                : $"No pending intention found matching '{arg}'.");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Lists all pending intentions.
    /// </summary>
    private string ListPendingIntentions()
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        var pending = _autonomousCoordinator.IntentionBus.GetPendingIntentions().ToList();

        if (pending.Count == 0)
        {
            return "No pending intentions. Ouroboros will propose actions based on context.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                   PENDING INTENTIONS                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        foreach (var intention in pending.OrderByDescending(i => i.Priority))
        {
            var priorityMarker = intention.Priority switch
            {
                IntentionPriority.Critical => "🔴",
                IntentionPriority.High => "🟠",
                IntentionPriority.Normal => "🟢",
                _ => "⚪"
            };

            sb.AppendLine($"  {priorityMarker} [{intention.Id.ToString()[..8]}] {intention.Category}");
            sb.AppendLine($"     {intention.Title}");
            sb.AppendLine($"     {intention.Description}");
            sb.AppendLine($"     Created: {intention.CreatedAt:HH:mm:ss}");
            sb.AppendLine();
        }

        sb.AppendLine("Commands: /approve <id|all> | /reject <id|all>");

        return sb.ToString();
    }

    /// <summary>
    /// Pauses push mode (stops proposing actions).
    /// </summary>
    private string PausePushMode()
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled.";
        }

        _pushModeCts?.Cancel();
        return "⏸ Push mode paused. Use /resume to continue receiving proposals.";
    }

    /// <summary>
    /// Resumes push mode (continues proposing actions).
    /// </summary>
    private string ResumePushMode()
    {
        if (_autonomousCoordinator == null)
        {
            return "Push mode not enabled. Use --push flag to enable.";
        }

        if (_pushModeCts == null || _pushModeCts.IsCancellationRequested)
        {
            _pushModeCts?.Dispose();
            _pushModeCts = new CancellationTokenSource();
            _pushModeTask = Task.Run(() => PushModeLoopAsync(_pushModeCts.Token), _pushModeCts.Token);
            return "▶ Push mode resumed. Ouroboros will propose actions.";
        }

        return "Push mode is already active.";
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents an autonomous goal for self-execution.
/// </summary>
public sealed record AutonomousGoal(
    Guid Id,
    string Description,
    GoalPriority Priority,
    DateTime CreatedAt);

/// <summary>
/// Priority levels for autonomous goals.
/// </summary>
public enum GoalPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Represents an autonomous thought generated by the agent.
/// </summary>
public sealed record AutonomousThought(
    Guid Id,
    string ActionType,
    string Content,
    DateTime Timestamp);

/// <summary>
/// Represents a sub-agent instance for task delegation.
/// </summary>
public sealed class SubAgentInstance
{
    public string AgentId { get; }
    public string Name { get; }
    public HashSet<string> Capabilities { get; }
    private readonly IChatCompletionModel? _model;

    public SubAgentInstance(string agentId, string name, HashSet<string> capabilities, IChatCompletionModel? model)
    {
        AgentId = agentId;
        Name = name;
        Capabilities = capabilities;
        _model = model;
    }

    public async Task<string> ExecuteTaskAsync(string task, CancellationToken ct = default)
    {
        if (_model == null)
        {
            return $"[{Name}] No model available for execution.";
        }

        var prompt = $"You are {Name}, a specialized sub-agent with capabilities in: {string.Join(", ", Capabilities)}.\n\nTask: {task}\n\nProvide a focused, expert response:";
        return await _model.GenerateTextAsync(prompt, ct);
    }
}
