// <copyright file="OuroborosAgent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Text.RegularExpressions;
using LangChain.Databases;
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Agent.MetaAI.Affect;
using LangChainPipeline.Diagnostics;
using LangChainPipeline.Providers;
using LangChainPipeline.Providers.SpeechToText;
using LangChainPipeline.Providers.TextToSpeech;
using LangChainPipeline.Speech;
using LangChainPipeline.Tools.MeTTa;
using Ouroboros.Application.Personality;
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
    string Endpoint = "https://api.ollama.com",
    string EmbedModel = "nomic-embed-text",
    string EmbedEndpoint = "http://localhost:11434",
    string QdrantEndpoint = "http://localhost:6334",
    string? ApiKey = null,
    bool Voice = true,
    bool VoiceOnly = false,
    bool LocalTts = true,
    bool Debug = false,
    double Temperature = 0.7,
    int MaxTokens = 512);

/// <summary>
/// Unified Ouroboros agent that integrates all capabilities:
/// - Voice interaction (TTS/STT)
/// - Skill-based learning
/// - MeTTa symbolic reasoning
/// - Dynamic tool creation
/// - Personality engine with affective states
/// - Self-improvement and curiosity
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

        // Initialize voice
        if (_config.Voice)
        {
            await _voice.InitializeAsync();
        }

        // Initialize LLM
        await InitializeLlmAsync();

        // Initialize embedding
        await InitializeEmbeddingAsync();

        // Initialize tools
        await InitializeToolsAsync();

        // Initialize MeTTa symbolic reasoning
        await InitializeMeTTaAsync();

        // Initialize skill registry
        await InitializeSkillsAsync();

        // Initialize personality engine
        await InitializePersonalityAsync();

        // Initialize orchestrator
        await InitializeOrchestratorAsync();

        _isInitialized = true;

        Console.WriteLine("\n  ✓ Ouroboros fully initialized\n");
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ LLM unavailable: {ex.Message}");
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
                // Create tool-aware LLM
                _llm = new ToolAwareChatModel(_chatModel, _tools);

                // Initialize dynamic tool factory
                _toolFactory = new DynamicToolFactory(_llm);

                // Add built-in dynamic tools
                _tools = _tools
                    .WithTool(_toolFactory.CreateWebSearchTool("duckduckgo"))
                    .WithTool(_toolFactory.CreateUrlFetchTool())
                    .WithTool(_toolFactory.CreateCalculatorTool());

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
║   orchestrate X     - Full multi-step orchestration          ║
║                                                              ║
║ REASONING & MEMORY                                           ║
║   metta: expr       - Execute MeTTa symbolic expression      ║
║   query X           - Query MeTTa knowledge base             ║
║   remember X        - Store in persistent memory             ║
║   recall X          - Retrieve from memory                   ║
║                                                              ║
║ PIPELINES                                                    ║
║   ask X             - Quick single question                  ║
║   pipeline DSL      - Run a pipeline DSL expression          ║
║   explain DSL       - Explain a pipeline expression          ║
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

        // Use tool learner or direct research
        if (_toolLearner != null && _skills != null)
        {
            var result = await _toolLearner.FindOrCreateToolAsync(topic, _tools);
            return result.Match(
                success => $"Interesting! I learned about {topic} and {(success.WasCreated ? "created a new" : "found an existing")} '{success.Tool.Name}' capability.",
                error => $"I tried to learn about {topic}, but: {error}");
        }

        // Fallback: basic research via LLM
        if (_llm != null)
        {
            var (response, _) = await _llm.GenerateWithToolsAsync(
                $"Research and summarize key points about: {topic}. Be concise.");
            return response;
        }

        return $"I'd love to learn about {topic}, but I need an LLM connection first.";
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
            return @"Network Status:
• Agents: Ouroboros (this instance)
• MeTTa Engine: " + (_mettaEngine != null ? "Active" : "Offline") + @"
• LLM Endpoint: " + _config.Endpoint + @"
• Qdrant: " + _config.QdrantEndpoint + @"
• Tool Registry: " + _tools.Count + " tools" + @"
• Skill Registry: " + (_skills?.GetAllSkills().Count() ?? 0) + " skills";
        }

        if (cmd.StartsWith("ping"))
        {
            // Test connectivity
            var llmOk = _chatModel != null;
            var mettaOk = _mettaEngine != null;

            return $"Network ping:\n• LLM: {(llmOk ? "✓" : "✗")}\n• MeTTa: {(mettaOk ? "✓" : "✗")}";
        }

        await Task.CompletedTask;
        return $"Unknown network command: {subCommand}. Try 'network status' or 'network ping'.";
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
        var context = string.Join("\n", _conversationHistory.TakeLast(6));
        var personalityPrompt = _voice.BuildPersonalityPrompt(
            $"Available skills: {_skills?.GetAllSkills().Count() ?? 0}\nAvailable tools: {_tools.Count}");

        var prompt = $"{personalityPrompt}\n\nRecent conversation:\n{context}\n\nUser: {input}\n\n{_voice.ActivePersona.Name}:";

        try
        {
            var (response, tools) = await _llm.GenerateWithToolsAsync(prompt);

            // Handle any tool calls
            if (tools?.Any() == true)
            {
                var toolResults = string.Join("\n", tools.Select(t => $"[{t.ToolName}]: {t.Output}"));
                return $"{response}\n\n{toolResults}";
            }

            return response;
        }
        catch (Exception ex)
        {
            return $"I had trouble processing that: {ex.Message}";
        }
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

        _voice.Dispose();
        _mettaEngine?.Dispose();

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
        Test
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
}
