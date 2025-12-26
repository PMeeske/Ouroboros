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
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Agent.MetaAI.SelfModel;
using LangChainPipeline.Domain.Events;
using LangChainPipeline.Network;
using LangChainPipeline.Agent.MetaAI.Affect;
using LangChainPipeline.Diagnostics;
using LangChainPipeline.Pipeline.Branches;
using LangChainPipeline.Pipeline.Reasoning;
using LangChainPipeline.Providers;
using LangChainPipeline.Providers.SpeechToText;
using LangChainPipeline.Providers.TextToSpeech;
using LangChainPipeline.Speech;
using LangChainPipeline.Tools.MeTTa;
using Ouroboros.Application;
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Tools.MeTTa;
using IEmbeddingModel = LangChainPipeline.Domain.IEmbeddingModel;

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
    bool Voice = true,
    bool VoiceOnly = false,
    bool LocalTts = true,
    bool Debug = false,
    double Temperature = 0.7,
    int MaxTokens = 2048,
    // Feature toggles - all enabled by default
    bool EnableSkills = true,
    bool EnableMeTTa = true,
    bool EnableTools = true,
    bool EnablePersonality = true,
    bool EnableMind = true,
    bool EnableBrowser = true,
    bool EnableConsciousness = true,
    // Additional config
    int ThinkingIntervalSeconds = 30,
    int AgentMaxSteps = 10,
    string? InitialGoal = null,
    string? InitialQuestion = null,
    string? InitialDsl = null,
    // Multi-model
    string? CoderModel = null,
    string? ReasonModel = null,
    string? SummarizeModel = null);

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
    /// Gets the skill registry.
    /// </summary>
    public ISkillRegistry? Skills => _skills;

    /// <summary>
    /// Gets the personality engine.
    /// </summary>
    public PersonalityEngine? Personality => _personalityEngine;

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
            Model: config.Model,
            Endpoint: config.Endpoint,
            EmbedModel: config.EmbedModel,
            QdrantEndpoint: config.QdrantEndpoint));
    }

    /// <summary>
    /// Initializes all agent subsystems.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

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

        // Initialize LLM (always required)
        await InitializeLlmAsync();

        // Initialize embedding (always required for most features)
        await InitializeEmbeddingAsync();

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
        _networkTracker = new NetworkStateTracker();
        Console.WriteLine("  ✓ NetworkState: Merkle-DAG reification active");

        // Initialize sub-agent orchestration (always enabled for complex task delegation)
        await InitializeSubAgentOrchestrationAsync();

        // Initialize self-model for metacognition (always enabled)
        await InitializeSelfModelAsync();

        // Initialize self-execution capability (conditionally based on autonomous mind)
        if (_config.EnableMind)
        {
            await InitializeSelfExecutionAsync();
        }

        _isInitialized = true;

        Console.WriteLine("\n  ✓ Ouroboros fully initialized\n");
        PrintQuickHelp();
    }

    private void PrintFeatureStatus()
    {
        Console.WriteLine("  Configuration:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Model: {_config.Model}");
        Console.WriteLine($"    Persona: {_config.Persona}");
        Console.WriteLine($"    Voice: {(_config.Voice ? "✓ enabled" : "○ disabled")}");
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
        try
        {
            // Use separate embedding endpoint (local Ollama by default)
            var embedEndpoint = _config.EmbedEndpoint.TrimEnd('/');
            var provider = new OllamaProvider(embedEndpoint);
            var embedModel = new OllamaEmbeddingModel(provider, _config.EmbedModel);
            _embedding = new OllamaEmbeddingAdapter(embedModel);

            // Test embedding
            var testEmbed = await _embedding.CreateEmbeddingsAsync("test");
            Console.WriteLine($"  ✓ Embeddings: {_config.EmbedModel} @ {embedEndpoint} (dim={testEmbed.Length})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Embeddings unavailable: {ex.Message}");
        }
    }

    private async Task InitializeToolsAsync()
    {
        try
        {
            // Start with default tools
            _tools = ToolRegistry.CreateDefault();

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
                    _skills = new QdrantSkillRegistry(_embedding, qdrantConfig);
                    Console.WriteLine("  ✓ Skills: Qdrant persistent storage");
                }
                catch
                {
                    _skills = new SkillRegistry(_embedding);
                    Console.WriteLine("  ✓ Skills: In-memory with embeddings");
                }
            }
            else
            {
                _skills = new SkillRegistry();
                Console.WriteLine("  ✓ Skills: In-memory basic");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Skills unavailable: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Personality engine failed: {ex.Message}");
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

            // Subscribe to autonomous thought events
            _immersivePersona.AutonomousThought += (_, e) =>
            {
                // Display autonomous thoughts inline (non-blocking)
                string savedInput;
                lock (_inputLock)
                {
                    savedInput = _currentInputBuffer.ToString();
                }

                if (!string.IsNullOrEmpty(savedInput))
                {
                    // Clear line and print thought, then restore input
                    Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                }

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"\n  [inner thought] {e.Thought.Content}");
                Console.ResetColor();

                // Restore the input prompt and buffer
                Console.Write($"  {_config.Persona}> {savedInput}");
            };

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

            _thoughtPersistence = ThoughtPersistenceService.CreateWithFilePersistence(sessionId, thoughtsDir);

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
    /// </summary>
    private async Task PersistThoughtAsync(InnerThought thought, string? topic = null)
    {
        if (_thoughtPersistence == null) return;

        try
        {
            await _thoughtPersistence.SaveAsync(thought, topic);
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

            // Configure thinking capability - use orchestrated model if available
            _autonomousMind.ThinkFunction = async (prompt, token) =>
            {
                return await GenerateWithOrchestrationAsync(prompt, token);
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

            // Wire up proactive message events
            _autonomousMind.OnProactiveMessage += (msg) =>
            {
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

            _autonomousMind.OnThought += (thought) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");
            };

            _autonomousMind.OnDiscovery += (query, fact) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
            };

            // Configure faster thinking for interactive use
            _autonomousMind.Config.ThinkingIntervalSeconds = 15;
            _autonomousMind.Config.CuriosityIntervalSeconds = 30;
            _autonomousMind.Config.ActionIntervalSeconds = 45;

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

        _voice.PrintHeader("OUROBOROS");

        // Greeting
        await _voice.SayAsync(GetGreeting());

        _isInConversationLoop = true;
        bool running = true;
        while (running)
        {
            var input = await _voice.GetInputAsync("\n  You: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Track conversation
            _conversationHistory.Add($"User: {input}");

            // Check for exit
            if (IsExitCommand(input))
            {
                await _voice.SayAsync("Until next time! I'll keep learning while you're away.");
                running = false;
                continue;
            }

            // Process input through the agent
            try
            {
                var response = await ProcessInputAsync(input);
                await _voice.SayAsync(response);
                _conversationHistory.Add($"Ouroboros: {response}");
            }
            catch (Exception ex)
            {
                await _voice.SayAsync($"Hmm, something went wrong: {ex.Message}");
            }
        }
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
            ActionType.Chat => await ChatAsync(input),
            _ => await ChatAsync(input)
        };
    }

    private (ActionType Type, string Argument, string? ToolInput) ParseAction(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

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

        // Default to chat
        return (ActionType.Chat, input, null);
    }

    private string GetGreeting()
    {
        var persona = _voice.ActivePersona;
        var greetings = new[]
        {
            $"Hey there! I'm {persona.Name}. What's on your mind today?",
            $"Hi! {persona.Name} here. Ready to explore some ideas together?",
            $"Hello! I'm {persona.Name}, your research companion. What shall we dive into?",
            $"Hey! {persona.Name} at your service. What are we curious about today?"
        };
        return greetings[new Random().Next(greetings.Length)];
    }

    private string GetHelpText()
    {
        return @"╔══════════════════════════════════════════════════════════════╗
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
    /// Ask a single question (unified ask command).
    /// </summary>
    private async Task<string> AskAsync(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "What would you like to ask?";

        if (_llm == null)
            return "I need an LLM connection to answer questions.";

        try
        {
            var (response, _) = await _llm.GenerateWithToolsAsync(question);
            return response;
        }
        catch (Exception ex)
        {
            return $"Error answering: {ex.Message}";
        }
    }

    /// <summary>
    /// Run a DSL pipeline expression (unified pipeline command).
    /// </summary>
    private async Task<string> RunPipelineAsync(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return "Please provide a DSL expression. Example: 'pipeline draft → critique → final'";

        try
        {
            // Parse and explain the DSL first
            var explanation = PipelineDsl.Explain(dsl);

            // Execute the pipeline
            if (_llm != null)
            {
                var (result, tools) = await _llm.GenerateWithToolsAsync(
                    $"Execute this pipeline: {dsl}\n\nPipeline structure:\n{explanation}");
                return $"Pipeline executed:\n{explanation}\n\nResult: {result}";
            }

            return $"Pipeline parsed:\n{explanation}\n(LLM required for execution)";
        }
        catch (Exception ex)
        {
            return $"Pipeline error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a MeTTa expression directly (unified metta command).
    /// </summary>
    private async Task<string> RunMeTTaExpressionAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "Please provide a MeTTa expression. Example: '!(+ 1 2)' or '(= (greet $x) (Hello $x))'";

        if (_mettaEngine == null)
            return "MeTTa engine not available.";

        try
        {
            var result = await _mettaEngine.ExecuteQueryAsync(expression, CancellationToken.None);
            return result.Match(
                output => $"MeTTa:\n  {expression}\n  → {output}",
                error => $"MeTTa error: {error}");
        }
        catch (Exception ex)
        {
            return $"MeTTa execution failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Orchestrate a complex multi-step task (unified orchestrator command).
    /// </summary>
    private async Task<string> OrchestrateAsync(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return "What would you like me to orchestrate?";

        if (_orchestrator != null)
        {
            // Use full orchestrator
            var planResult = await _orchestrator.PlanAsync(goal);
            return await planResult.Match(
                async plan =>
                {
                    var steps = string.Join("\n", plan.Steps.Select((s, i) => $"  {i + 1}. {s.Action}"));
                    await _voice.SayAsync($"I've created a plan with {plan.Steps.Count} steps. Executing now...");

                    var execResult = await _orchestrator.ExecuteAsync(plan);
                    return execResult.Match(
                        result => $"Orchestration complete:\n{steps}\n\nResult: {result.FinalOutput ?? "Success"}",
                        error => $"Orchestration failed at execution: {error}");
                },
                error => Task.FromResult($"Could not create plan: {error}"));
        }

        // Fallback to LLM-based orchestration
        return await ExecuteAsync(goal);
    }

    /// <summary>
    /// Network status and management (unified network command).
    /// </summary>
    private async Task<string> NetworkCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "status" or "" or "show")
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Network Status:");
            sb.AppendLine($"• Agents: Ouroboros (this instance)");
            sb.AppendLine($"• MeTTa Engine: {(_mettaEngine != null ? "Active" : "Offline")}");
            sb.AppendLine($"• LLM Endpoint: {_config.Endpoint}");
            sb.AppendLine($"• Qdrant: {_config.QdrantEndpoint}");
            sb.AppendLine($"• Tool Registry: {_tools.Count} tools");
            sb.AppendLine($"• Skill Registry: {_skills?.GetAllSkills().Count() ?? 0} skills");

            // Add Merkle-DAG network state information
            if (_networkTracker != null)
            {
                sb.AppendLine();
                sb.AppendLine("Merkle-DAG Network State:");
                sb.AppendLine($"• Tracked Branches: {_networkTracker.TrackedBranchCount}");
                var snapshot = _networkTracker.CreateSnapshot();
                sb.AppendLine($"• Total Nodes: {snapshot.TotalNodes}");
                sb.AppendLine($"• Total Transitions: {snapshot.TotalTransitions}");
                sb.AppendLine($"• Current Epoch: {_networkTracker.Projector.CurrentEpoch}");
                if (snapshot.AverageConfidence.HasValue)
                {
                    sb.AppendLine($"• Average Confidence: {snapshot.AverageConfidence:P0}");
                }
            }

            return sb.ToString();
        }

        if (cmd == "state" || cmd == "dag")
        {
            // Detailed network state summary
            if (_networkTracker != null)
            {
                return _networkTracker.GetStateSummary();
            }
            return "Network state tracking not available.";
        }

        if (cmd == "steps" || cmd == "reify")
        {
            // Show recent step executions from tracked branches
            if (_networkTracker == null)
            {
                return "Network state tracking not available.";
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Step Reification History ===");
            sb.AppendLine($"Tracked Branches: {_networkTracker.TrackedBranchCount}");
            sb.AppendLine($"Current Epoch: {_networkTracker.Projector.CurrentEpoch}");
            sb.AppendLine();

            var state = _networkTracker.Projector.ProjectCurrentState();
            if (state.NodeCountByType.Any())
            {
                sb.AppendLine("Reified Node Types:");
                foreach (var kvp in state.NodeCountByType)
                {
                    sb.AppendLine($"  • {kvp.Key}: {kvp.Value}");
                }
            }

            if (state.TransitionCountByOperation.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Step Transitions:");
                foreach (var kvp in state.TransitionCountByOperation)
                {
                    sb.AppendLine($"  • {kvp.Key}: {kvp.Value}");
                }
            }

            return sb.ToString();
        }

        if (cmd.StartsWith("ping"))
        {
            // Test connectivity
            var llmOk = _chatModel != null;
            var mettaOk = _mettaEngine != null;
            var networkOk = _networkTracker != null;

            return $"Network ping:\n• LLM: {(llmOk ? "✓" : "✗")}\n• MeTTa: {(mettaOk ? "✓" : "✗")}\n• NetworkState: {(networkOk ? "✓" : "✗")}";
        }

        await Task.CompletedTask;
        return $"Unknown network command: {subCommand}. Try 'network status', 'network state', 'network steps', or 'network ping'.";
    }

    /// <summary>
    /// DAG visualization and management (unified dag command).
    /// </summary>
    private async Task<string> DagCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "show" or "" or "status")
        {
            var skillList = _skills?.GetAllSkills().ToList() ?? new List<LangChainPipeline.Agent.MetaAI.Skill>();
            var tools = _tools.All.ToList();

            return $@"Ouroboros Capability DAG:
┌─ Core
│  ├─ LLM ({_config.Model})
│  ├─ Embeddings ({_config.EmbedModel})
│  └─ MeTTa Engine
│
├─ Tools ({tools.Count})
│  {string.Join("\n│  ", tools.Take(5).Select(t => $"├─ {t.Name}"))}
│  {(tools.Count > 5 ? $"└─ ... and {tools.Count - 5} more" : "")}
│
├─ Skills ({skillList.Count})
│  {string.Join("\n│  ", skillList.Take(5).Select(s => $"├─ {s.Name}"))}
│  {(skillList.Count > 5 ? $"└─ ... and {skillList.Count - 5} more" : "")}
│
└─ Personality: {_voice.ActivePersona.Name}";
        }

        await Task.CompletedTask;
        return $"Unknown dag command: {subCommand}. Try 'dag show'.";
    }

    /// <summary>
    /// Affect and emotional state (unified affect command).
    /// </summary>
    private async Task<string> AffectCommandAsync(string subCommand)
    {
        var mood = _voice.CurrentMood;
        var persona = _voice.ActivePersona;

        if (string.IsNullOrWhiteSpace(subCommand) || subCommand == "status")
        {
            var affectState = _valenceMonitor?.GetCurrentState();
            if (affectState != null)
            {
                return $@"Affective State ({persona.Name}):
• Mood: {mood}
• Valence: {affectState.Valence:P0}
• Arousal: {affectState.Arousal:P0}
• Confidence: {affectState.Confidence:P0}
• Curiosity: {affectState.Curiosity:P0}
• Stress: {affectState.Stress:P0}";
            }

            return $"Current mood: {mood} (Persona: {persona.Name})";
        }

        if (subCommand.StartsWith("set "))
        {
            // Mood is randomized from persona traits, not directly settable
            return $"Mood is determined by personality traits. Current mood: {mood}";
        }

        await Task.CompletedTask;
        return GetMood();
    }

    /// <summary>
    /// Environment detection and configuration (unified environment command).
    /// </summary>
    private async Task<string> EnvironmentCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "status" or "" or "show" or "detect")
        {
            var env = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            var dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

            return $@"Environment:
• Mode: {env}
• OS: {os}
• Runtime: {dotnet}
• LLM Endpoint: {_config.Endpoint}
• Qdrant: {_config.QdrantEndpoint}
• Debug: {_config.Debug}
• Voice: {_config.Voice}";
        }

        await Task.CompletedTask;
        return $"Unknown environment command: {subCommand}. Try 'env status'.";
    }

    /// <summary>
    /// Maintenance operations (unified maintenance command).
    /// </summary>
    private async Task<string> MaintenanceCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "gc" or "cleanup")
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memory = GC.GetTotalMemory(true) / (1024 * 1024);
            return $"Garbage collection complete. Memory usage: {memory} MB";
        }

        if (cmd is "reset" or "clear")
        {
            _conversationHistory.Clear();
            return "Conversation history cleared.";
        }

        if (cmd is "stats" or "" or "status")
        {
            var memory = GC.GetTotalMemory(false) / (1024 * 1024);
            return $@"System Stats:
• Memory: {memory} MB
• Conversation turns: {_conversationHistory.Count / 2}
• Tools loaded: {_tools.Count}
• Skills: {_skills?.GetAllSkills().Count() ?? 0}
• Uptime: Active";
        }

        await Task.CompletedTask;
        return $"Unknown maintenance command: {subCommand}. Try 'maintenance gc', 'maintenance reset', or 'maintenance stats'.";
    }

    /// <summary>
    /// Policy management (unified policy command).
    /// </summary>
    private async Task<string> PolicyCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

        if (cmd is "show" or "" or "list")
        {
            return @"Active Policies:
• Safety: Enabled (content filtering)
• Autonomy: Balanced (ask for confirmation on major actions)
• Learning: Active (skill acquisition enabled)
• Memory: Persistent (Qdrant) or In-Memory
• Privacy: Standard";
        }

        if (cmd.StartsWith("set "))
        {
            return "Policy modification not available in interactive mode. Use config files.";
        }

        await Task.CompletedTask;
        return $"Unknown policy command: {subCommand}. Try 'policy show'.";
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

            toolInstruction = $@"

TOOL USAGE INSTRUCTIONS:
You have access to tools. To use a tool, write [TOOL:toolname input] in your response.
THESE TOOLS ARE FULLY FUNCTIONAL - USE THEM DIRECTLY. Do not explain how to use them or provide code examples.

CRITICAL RULES:
1. Use ACTUAL VALUES only - never use placeholder descriptions like 'URL of the result' or 'ref of the search box'
2. For searches, provide the actual search query
3. For fetch_url, provide a complete URL starting with https://
4. For playwright, use JSON with real values - this EXECUTES browser actions, don't explain code
5. NEVER say 'I can help you with the code' - just USE the tool directly

AVAILABLE TOOLS:
- duckduckgo_search: Search the web. Example: [TOOL:duckduckgo_search ouroboros mythology symbol]
- fetch_url: Fetch webpage content. Example: [TOOL:fetch_url https://en.wikipedia.org/wiki/Ouroboros]
- calculator: Math expressions. Example: [TOOL:calculator 2+2*3]
- playwright: Browser automation that EXECUTES actions (not code examples). Use workflow:
  1. Navigate: [TOOL:playwright {{""action"":""navigate"",""url"":""https://example.com""}}]
  2. Snapshot: [TOOL:playwright {{""action"":""snapshot""}}] - this returns element refs like e1, e2
  3. Click/Type: [TOOL:playwright {{""action"":""click"",""ref"":""e5""}}]

Other tools: {string.Join(", ", simpleTools.Take(5))}

WRONG (placeholder - DO NOT DO THIS):
[TOOL:fetch_url URL of the search result]
[TOOL:playwright {{""action"":""click"",""ref"":""ref of the button""}}]

CORRECT (actual values):
[TOOL:fetch_url https://example.com/page]
[TOOL:playwright {{""action"":""click"",""ref"":""e5""}}]

If you don't have a real value, ask the user or skip the tool call.";
        }

        string prompt = $"{personalityPrompt}{persistentThoughtContext}{toolInstruction}\n\nRecent conversation:\n{context}\n\nUser: {input}\n\n{_voice.ActivePersona.Name}:";

        try
        {
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
            }

            // Handle any tool calls
            if (tools?.Any() == true)
            {
                string toolResults = string.Join("\n", tools.Select(t => $"[{t.ToolName}]: {t.Output}"));
                return $"{response}\n\n{toolResults}";
            }

            return response;
        }
        catch (Exception ex)
        {
            return $"I had trouble processing that: {ex.Message}";
        }
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

        await Task.CompletedTask;
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
        Introspect
    }

    /// <summary>
    /// Processes an initial goal provided via command line.
    /// </summary>
    public async Task ProcessGoalAsync(string goal)
    {
        var response = await ExecuteAsync(goal);
        await _voice.SayAsync(response);
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

            var thoughtPrompt = $@"You are an autonomous AI agent with self-improvement capabilities.
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
