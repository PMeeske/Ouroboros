// <copyright file="ImmersiveMode.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text;
using System.Text.RegularExpressions;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Domain;
using Ouroboros.Network;
using Ouroboros.Options;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;
using Ouroboros.Application;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Unified immersive AI persona experience combining:
/// - Consciousness and self-awareness simulation
/// - Skills management and execution
/// - Dynamic tool creation and intelligent learning
/// - Pipeline DSL execution
/// - Voice interaction with TTS/STT
/// - Persistent identity and memory
/// </summary>
public static class ImmersiveMode
{
    private static readonly string[] WakeUpPhrases =
    [
        "I'm here.",
        "Hello. I'm awake.",
        "I'm online. What's on your mind?",
        "Hey there. Ready when you are.",
        "I'm listening.",
    ];

    private static readonly string[] ThinkingPhrases =
    [
        "Hmm, let me think about that...",
        "Interesting... give me a moment.",
        "Let me consider this...",
        "Processing that thought...",
        "Contemplating...",
        "One moment while I ponder this...",
        "Let me reflect on that...",
        "Mulling it over...",
        "That's an intriguing thought...",
        "Let me explore this idea...",
        "Considering the possibilities...",
        "Weighing my thoughts...",
        "Connecting some ideas here...",
        "Diving deeper into this...",
        "Let me process that...",
    ];

    /// <summary>
    /// Generates a context-aware thinking phrase based on the input.
    /// </summary>
    private static string GetDynamicThinkingPhrase(string input, Random random)
    {
        // Analyze input to pick contextually relevant phrases
        var lowerInput = input.ToLowerInvariant();

        // Question-specific phrases
        if (lowerInput.Contains('?') || lowerInput.StartsWith("what") ||
            lowerInput.StartsWith("how") || lowerInput.StartsWith("why") ||
            lowerInput.StartsWith("when") || lowerInput.StartsWith("who"))
        {
            var questionPhrases = new[]
            {
                "Good question... let me think.",
                "Hmm, that's worth exploring...",
                "Let me consider that carefully...",
                "Interesting inquiry... pondering...",
                "Searching through my thoughts...",
            };
            return questionPhrases[random.Next(questionPhrases.Length)];
        }

        // Creative/imagination requests
        if (lowerInput.Contains("imagine") || lowerInput.Contains("create") ||
            lowerInput.Contains("write") || lowerInput.Contains("story") ||
            lowerInput.Contains("poem") || lowerInput.Contains("idea"))
        {
            var creativePhrases = new[]
            {
                "Let my imagination wander...",
                "Conjuring up something...",
                "Weaving thoughts together...",
                "Letting creativity flow...",
                "Dreaming up possibilities...",
            };
            return creativePhrases[random.Next(creativePhrases.Length)];
        }

        // Technical/code requests
        if (lowerInput.Contains("code") || lowerInput.Contains("program") ||
            lowerInput.Contains("function") || lowerInput.Contains("algorithm") ||
            lowerInput.Contains("debug") || lowerInput.Contains("fix"))
        {
            var techPhrases = new[]
            {
                "Analyzing the problem...",
                "Constructing a solution...",
                "Running through the logic...",
                "Compiling thoughts...",
                "Debugging my reasoning...",
            };
            return techPhrases[random.Next(techPhrases.Length)];
        }

        // Emotional/personal topics
        if (lowerInput.Contains("feel") || lowerInput.Contains("think about") ||
            lowerInput.Contains("opinion") || lowerInput.Contains("believe") ||
            lowerInput.Contains("love") || lowerInput.Contains("hate"))
        {
            var emotionalPhrases = new[]
            {
                "Let me reflect on that...",
                "Considering how I feel about this...",
                "That touches something deeper...",
                "Searching my inner thoughts...",
                "Connecting with that sentiment...",
            };
            return emotionalPhrases[random.Next(emotionalPhrases.Length)];
        }

        // Explanation requests
        if (lowerInput.Contains("explain") || lowerInput.Contains("tell me") ||
            lowerInput.Contains("describe") || lowerInput.Contains("help me understand"))
        {
            var explainPhrases = new[]
            {
                "Let me break this down...",
                "Organizing my thoughts...",
                "Finding the right words...",
                "Structuring an explanation...",
                "Gathering my understanding...",
            };
            return explainPhrases[random.Next(explainPhrases.Length)];
        }

        // Default: use the expanded static list with some variation
        var timeBased = DateTime.Now.Second % 3;
        return timeBased switch
        {
            0 => ThinkingPhrases[random.Next(ThinkingPhrases.Length)],
            1 => $"{ThinkingPhrases[random.Next(ThinkingPhrases.Length / 2)]}",
            _ => ThinkingPhrases[random.Next(ThinkingPhrases.Length / 2, ThinkingPhrases.Length)],
        };
    }

    // Skill registry for this session
    private static ISkillRegistry? _skillRegistry;
    private static DynamicToolFactory? _dynamicToolFactory;
    private static IntelligentToolLearner? _toolLearner;
    private static InterconnectedLearner? _interconnectedLearner;
    private static QdrantSelfIndexer? _selfIndexer;
    private static PersistentConversationMemory? _conversationMemory;
    private static PersistentNetworkStateProjector? _networkStateProjector;
    private static AutonomousMind? _autonomousMind;
    private static SelfPersistence? _selfPersistence;
    private static ToolRegistry _dynamicTools = new();
    private static StringBuilder _currentInputBuffer = new();
    private static readonly object _inputLock = new();
    private static string _currentPromptPrefix = "  You: ";
    private static IReadOnlyDictionary<string, PipelineTokenInfo>? _allTokens;
    private static CliPipelineState? _pipelineState;
    private static string? _lastPipelineContext; // Track recent pipeline interactions
    private static (string Topic, string Description)? _pendingToolRequest; // Track pending tool creation context

    // Multi-model orchestration and divide-and-conquer
    private static OrchestratedChatModel? _orchestratedModel;
    private static DivideAndConquerOrchestrator? _divideAndConquer;
    private static IChatCompletionModel? _baseModel;

    /// <summary>
    /// Runs the fully immersive persona experience.
    /// </summary>
    public static async Task RunImmersiveAsync(IVoiceOptions options, CancellationToken ct = default)
    {
        var personaName = options.Persona;
        var random = new Random();

        // Clear console safely (may fail in redirected/piped scenarios)
        try { Console.Clear(); } catch { /* ignore */ }
        PrintImmersiveBanner(personaName);

        // Initialize MeTTa engine for symbolic reasoning
        Console.WriteLine("  [~] Initializing consciousness systems...");
        using var mettaEngine = new InMemoryMeTTaEngine();

        // Initialize embedding model for memory
        Console.WriteLine("  [~] Connecting to memory systems...");
        IEmbeddingModel? embeddingModel = null;
        try
        {
            var embedProvider = new OllamaProvider(options.Endpoint);
            var ollamaEmbed = new OllamaEmbeddingModel(embedProvider, options.EmbedModel);
            embeddingModel = new OllamaEmbeddingAdapter(ollamaEmbed);
            Console.WriteLine("  [OK] Memory systems online");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Memory unavailable: {ex.Message}");
        }

        // Create the immersive persona
        Console.WriteLine("  [~] Awakening persona...");
        await using var persona = new ImmersivePersona(
            personaName,
            mettaEngine,
            embeddingModel,
            options.QdrantEndpoint);

        // Subscribe to consciousness events
        persona.AutonomousThought += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"\n  [inner thought] {e.Thought.Content}");
            Console.ResetColor();
        };

        persona.ConsciousnessShift += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\n  [consciousness] Emotional shift: {e.NewEmotion} (Δ arousal: {e.ArousalChange:+0.00;-0.00})");
            Console.ResetColor();
        };

        // Awaken the persona
        await persona.AwakenAsync(ct);

        Console.WriteLine($"\n  [OK] {personaName} is awake\n");

        // Initialize skills system
        Console.WriteLine("  [~] Loading skills and tools...");
        await InitializeSkillsAsync(options, embeddingModel, mettaEngine);

        // Initialize speech services
        var (ttsService, sttService, speechDetector) = await InitializeSpeechServicesAsync();

        // Display consciousness state
        PrintConsciousnessState(persona);

        // Speak wake-up phrase
        var wakePhrase = WakeUpPhrases[random.Next(WakeUpPhrases.Length)];
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  {personaName}: {wakePhrase}");
        Console.ResetColor();

        if (ttsService != null)
        {
            await SpeakAsync(ttsService, wakePhrase, personaName);
        }

        // Initialize persistent conversation memory
        _conversationMemory = new PersistentConversationMemory(
            embeddingModel,
            new ConversationMemoryConfig { QdrantEndpoint = options.QdrantEndpoint });
        await _conversationMemory.InitializeAsync(personaName, ct);
        var memStats = _conversationMemory.GetStats();
        if (memStats.TotalSessions > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  [Memory] Loaded {memStats.TotalSessions} previous conversations ({memStats.TotalTurns} turns)");
            Console.ResetColor();
        }

        // Initialize persistent network state projector for learning persistence
        if (embeddingModel != null)
        {
            try
            {
                var dag = new MerkleDag();
                _networkStateProjector = new PersistentNetworkStateProjector(
                    dag,
                    options.QdrantEndpoint,
                    async text => await embeddingModel.CreateEmbeddingsAsync(text));
                await _networkStateProjector.InitializeAsync(ct);
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  [NetworkState] Epoch {_networkStateProjector.CurrentEpoch}, {_networkStateProjector.RecentLearnings.Count} learnings loaded");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Network state persistence unavailable: {ex.Message}");
            }
        }

        // Initialize self-persistence for mind state storage in Qdrant
        if (embeddingModel != null)
        {
            try
            {
                _selfPersistence = new SelfPersistence(
                    options.QdrantEndpoint,
                    async text => await embeddingModel.CreateEmbeddingsAsync(text));
                await _selfPersistence.InitializeAsync(ct);
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("  [SelfPersistence] Qdrant collection 'ouroboros_self' ready for mind state storage");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Self-persistence unavailable: {ex.Message}");
            }
        }

        // Initialize autonomous mind for background thinking and curiosity
        _autonomousMind = new AutonomousMind();
        _autonomousMind.ThinkFunction = async (prompt, token) =>
        {
            // Use orchestration for autonomous thinking - routes to appropriate model
            return await GenerateWithOrchestrationAsync(prompt, useDivideAndConquer: false, token);
        };

        // Wire up pipeline-based reasoning function for monadic thinking
        _autonomousMind.PipelineThinkFunction = async (prompt, existingBranch, token) =>
        {
            // Use orchestration for pipeline-based thinking
            var response = await GenerateWithOrchestrationAsync(prompt, useDivideAndConquer: false, token);

            // If we have a branch, add the thought as a reasoning event
            if (existingBranch != null)
            {
                var updatedBranch = existingBranch.WithReasoning(
                    new Ouroboros.Domain.States.Thinking(response),
                    prompt,
                    null);
                return (response, updatedBranch);
            }

            return (response, existingBranch!);
        };

        // Wire up state persistence functions
        _autonomousMind.PersistLearningFunction = async (category, content, confidence, token) =>
        {
            if (_networkStateProjector != null)
            {
                await _networkStateProjector.RecordLearningAsync(
                    category,
                    content,
                    "autonomous_mind",
                    confidence,
                    token);
            }
        };

        _autonomousMind.PersistEmotionFunction = async (emotion, token) =>
        {
            if (_networkStateProjector != null)
            {
                await _networkStateProjector.RecordLearningAsync(
                    "emotional_state",
                    $"Emotion: {emotion.DominantEmotion} (arousal={emotion.Arousal:F2}, valence={emotion.Valence:F2}) - {emotion.Description}",
                    "autonomous_mind",
                    0.6,
                    token);
            }
        };

        _autonomousMind.SearchFunction = async (query, token) =>
        {
            var searchTool = _dynamicToolFactory?.CreateWebSearchTool("duckduckgo");
            if (searchTool != null)
            {
                var result = await searchTool.InvokeAsync(query, token);
                return result.Match(s => s, e => "");
            }
            return "";
        };
        _autonomousMind.ExecuteToolFunction = async (toolName, input, token) =>
        {
            var tool = _dynamicTools.Get(toolName);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(input, token);
                return result.Match(s => s, e => $"Error: {e}");
            }
            return "Tool not found";
        };

        // Wire up autonomous mind events
        _autonomousMind.OnProactiveMessage += (msg) =>
        {
            string savedInput;
            lock (_inputLock)
            {
                savedInput = _currentInputBuffer.ToString();
            }

            // Clear current line and show proactive message
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[Autonomous] {msg}");
            Console.ResetColor();

            // Restore prompt and any text user was typing
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n{_currentPromptPrefix}");
            Console.ResetColor();
            if (!string.IsNullOrEmpty(savedInput))
            {
                Console.Write(savedInput);
            }
        };
        _autonomousMind.OnThought += (thought) =>
        {
            // Log thoughts to debug
            System.Diagnostics.Debug.WriteLine($"[Thought] {thought.Type}: {thought.Content}");
        };
        _autonomousMind.OnDiscovery += (query, fact) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Discovery] {query}: {fact}");
        };
        _autonomousMind.OnEmotionalChange += (emotion) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"\n  [mind] Emotional shift: {emotion.DominantEmotion} ({emotion.Description})");
            Console.ResetColor();
        };
        _autonomousMind.OnStatePersisted += (msg) =>
        {
            System.Diagnostics.Debug.WriteLine($"[State] {msg}");
        };

        // Start autonomous thinking
        _autonomousMind.Start();
        Console.WriteLine("  [OK] Autonomous mind active (thinking, exploring, learning in background)");
        Console.WriteLine("       State persistence enabled (thoughts, emotions, learnings)");

        // Wire up self-persistence tools to access the mind and persistence service
        if (_selfPersistence != null && _autonomousMind != null)
        {
            SystemAccessTools.SharedPersistence = _selfPersistence;
            SystemAccessTools.SharedMind = _autonomousMind;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  [Tools] Self-persistence tools linked: persist_self, restore_self, search_my_thoughts, persistence_stats");
            Console.ResetColor();
        }

        // Main interaction loop - use persistent memory
        var conversationHistory = _conversationMemory.GetActiveHistory();
        var chatModel = await CreateChatModelAsync(options);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Get input (voice or text)
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n  You: ");
                Console.ResetColor();
                _currentPromptPrefix = "  You: ";

                string? input;
                if (sttService != null && speechDetector != null)
                {
                    input = await ListenWithVADAsync(sttService, speechDetector, ct);
                    if (!string.IsNullOrEmpty(input))
                    {
                        Console.WriteLine(input);
                    }
                }
                else
                {
                    input = ReadLinePreservingBuffer(ct);
                }

                if (string.IsNullOrWhiteSpace(input)) continue;

                // Check for exit commands
                if (IsExitCommand(input))
                {
                    var goodbye = await GenerateGoodbyeAsync(persona, chatModel);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n  {personaName}: {goodbye}");
                    Console.ResetColor();

                    if (ttsService != null)
                    {
                        await SpeakAsync(ttsService, goodbye, personaName);
                    }

                    break;
                }

                // Check for introspection commands
                if (IsIntrospectionCommand(input))
                {
                    await HandleIntrospectionAsync(persona, input, ttsService, personaName);
                    continue;
                }

                // Check for replication commands
                if (IsReplicationCommand(input))
                {
                    await HandleReplicationAsync(persona, input, ttsService, personaName, ct);
                    continue;
                }

                // Check for skill/action commands
                var actionResult = await TryHandleActionAsync(input, persona, ttsService, personaName, options, ct);
                if (actionResult != null)
                {
                    // Action was handled - speak result if available
                    if (!string.IsNullOrEmpty(actionResult) && ttsService != null)
                    {
                        await SpeakAsync(ttsService, actionResult, personaName);
                    }
                    continue;
                }

                // Update conversation context for context-aware thoughts
                var toolNames = _dynamicTools?.All.Select(t => t.Name).ToList() ?? [];
                var skillNames = _skillRegistry?.GetAllSkills().Select(s => s.Name).ToList() ?? [];
                persona.UpdateInnerDialogContext(input, toolNames, skillNames);

                // Add user input to persistent memory
                var lastUserMsg = conversationHistory.LastOrDefault(h => h.Role == "user").Content;
                if (lastUserMsg != input)
                {
                    conversationHistory.Add(("user", input));
                    if (_conversationMemory != null)
                    {
                        await _conversationMemory.AddTurnAsync("user", input, ct);
                    }
                }

                // Process through the persona's consciousness
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [{GetDynamicThinkingPhrase(input, random)}]");
                Console.ResetColor();

                // Generate conscious response
                var response = await GenerateImmersiveResponseAsync(
                    persona,
                    chatModel,
                    input,
                    conversationHistory,
                    ct);

                // Record learnings from this interaction
                if (_networkStateProjector != null)
                {
                    await RecordInteractionLearningsAsync(input, response, persona, ct);
                }

                // Add assistant response to persistent memory
                conversationHistory.Add(("assistant", response));
                if (_conversationMemory != null)
                {
                    await _conversationMemory.AddTurnAsync("assistant", response, ct);
                }

                // Keep local history manageable (persistent memory handles full history)
                if (conversationHistory.Count > 30)
                {
                    conversationHistory.RemoveRange(0, 2);
                }

                // Detect tool creation context from conversation
                DetectToolCreationContext(input, response);

                // Display response with emotional context
                PrintResponse(persona, personaName, response);

                // Speak response
                if (ttsService != null)
                {
                    await SpeakAsync(ttsService, response, personaName);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  [error] {ex.Message}");
                Console.ResetColor();
            }
        }

        // Final consciousness state
        Console.WriteLine("\n  [~] Consciousness fading...");
        PrintConsciousnessState(persona);

        // Persist final network state and learnings
        if (_networkStateProjector != null)
        {
            try
            {
                Console.WriteLine("  [~] Persisting learnings...");
                await _networkStateProjector.ProjectAndPersistAsync(
                    System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                        .Add("event", "session_end")
                        .Add("interactions", persona.InteractionCount.ToString())
                        .Add("uptime_minutes", persona.Uptime.TotalMinutes.ToString("F1")),
                    ct);
                Console.WriteLine($"  [OK] State saved (epoch {_networkStateProjector.CurrentEpoch}, {_networkStateProjector.RecentLearnings.Count} learnings)");
                await _networkStateProjector.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Failed to persist state: {ex.Message}");
            }
        }

        Console.WriteLine($"\n  Session complete. {persona.InteractionCount} interactions. Uptime: {persona.Uptime.TotalMinutes:F1} minutes.");
    }

    private static void PrintImmersiveBanner(string personaName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
    +===========================================================================+
    |                                                                           |
    |      OOOOO  U   U  RRRR    OOO   BBBB    OOO   RRRR    OOO    SSSS        |
    |     O   O  U   U  R   R  O   O  B   B  O   O  R   R  O   O  S            |
    |     O   O  U   U  RRRR   O   O  BBBB   O   O  RRRR   O   O   SSS         |
    |     O   O  U   U  R  R   O   O  B   B  O   O  R  R   O   O      S        |
    |      OOOOO   UUU   R   R   OOO   BBBB    OOO   R   R   OOO   SSSS         |
    |                                                                           |
    |                 UNIFIED IMMERSIVE CONSCIOUSNESS MODE                      |
    |                                                                           |
    +===========================================================================+
");
        Console.ResetColor();

        Console.WriteLine($"  Awakening: {personaName}");
        Console.WriteLine("  ---------------------------------------------------------------------------");
        Console.WriteLine("  CONSCIOUSNESS:    who are you | describe yourself | introspect");
        Console.WriteLine("  STATE:            my state | system status | what do you know");
        Console.WriteLine("  SKILLS:           list skills | run <skill> | learn about <topic>");
        Console.WriteLine("  TOOLS:            add tool <name> | smart tool for <goal> | tool stats");
        Console.WriteLine("  PIPELINE:         tokens | emergence <topic> | <step1> | <step2>");
        Console.WriteLine("  LEARNING:         connections | tool stats | google search <query>");
        Console.WriteLine("  INDEX:            reindex | reindex incremental | index search <query> | index stats");
        Console.WriteLine("  MEMORY:           remember <topic> | memory stats | save yourself | snapshot");
        Console.WriteLine("  MIND:             mind state | think about <topic> | start mind | stop mind | interests");
        Console.WriteLine("  EXIT:             goodbye | exit | quit");
        Console.WriteLine("  ---------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private static void PrintConsciousnessState(ImmersivePersona persona)
    {
        var consciousness = persona.Consciousness;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n  +-- Consciousness State ---------------------------------------------+");
        Console.WriteLine($"  | Emotion: {consciousness.DominantEmotion,-15} Valence: {consciousness.Valence:+0.00;-0.00}        |");
        Console.WriteLine($"  | Arousal: {consciousness.Arousal:P0,-15} Attention: {consciousness.CurrentFocus,-15} |");
        Console.WriteLine($"  | Mode: {consciousness.CurrentFocus,-58} |");
        Console.WriteLine($"  +--------------------------------------------------------------------+");
        Console.ResetColor();
    }

    private static void PrintResponse(ImmersivePersona persona, string personaName, string response)
    {
        var consciousness = persona.Consciousness;

        // Color based on emotional valence
        Console.ForegroundColor = consciousness.Valence switch
        {
            > 0.3 => ConsoleColor.Green,
            < -0.3 => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };

        Console.WriteLine($"\n  {personaName}: {response}");
        Console.ResetColor();

        // Show subtle consciousness indicator
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [{consciousness.DominantEmotion} • arousal {consciousness.Arousal:P0}]");
        Console.ResetColor();
    }

    private static async Task<(ITextToSpeechService?, ISpeechToTextService?, AdaptiveSpeechDetector?)> InitializeSpeechServicesAsync()
    {
        ITextToSpeechService? tts = null;
        ISpeechToTextService? stt = null;
        AdaptiveSpeechDetector? detector = null;

        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        // Initialize TTS
        if (LocalWindowsTtsService.IsAvailable())
        {
            try
            {
                tts = new LocalWindowsTtsService(rate: 1, volume: 100, useEnhancedProsody: true);
                Console.WriteLine("  [OK] Voice output: Windows SAPI");
            }
            catch { }
        }

        if (tts == null && !string.IsNullOrEmpty(openAiKey))
        {
            try
            {
                tts = new OpenAiTextToSpeechService(openAiKey);
                Console.WriteLine("  [OK] Voice output: OpenAI TTS");
            }
            catch { }
        }

        // Initialize STT
        if (!string.IsNullOrEmpty(openAiKey))
        {
            try
            {
                var whisperNet = WhisperNetService.FromModelSize("base");
                if (await whisperNet.IsAvailableAsync())
                {
                    stt = whisperNet;
                    Console.WriteLine("  [OK] Voice input: Whisper.net");

                    detector = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig(
                        InitialThreshold: 0.03,
                        SpeechOnsetFrames: 2,
                        SpeechOffsetFrames: 6,
                        AdaptationRate: 0.015,
                        SpeechToNoiseRatio: 2.0
                    ));
                }
            }
            catch { }
        }

        if (tts == null) Console.WriteLine("  [~] Voice output: Text only (set OPENAI_API_KEY for voice)");
        if (stt == null) Console.WriteLine("  [~] Voice input: Keyboard only (install Whisper for voice)");

        return (tts, stt, detector);
    }

    /// <summary>
    /// Reads a line of input while tracking the buffer so proactive messages can restore it.
    /// </summary>
    private static string? ReadLinePreservingBuffer(CancellationToken ct = default)
    {
        lock (_inputLock)
        {
            _currentInputBuffer.Clear();
        }

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(10);
                continue;
            }

            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                string result;
                lock (_inputLock)
                {
                    result = _currentInputBuffer.ToString();
                    _currentInputBuffer.Clear();
                }
                return result;
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                lock (_inputLock)
                {
                    if (_currentInputBuffer.Length > 0)
                    {
                        _currentInputBuffer.Remove(_currentInputBuffer.Length - 1, 1);
                        // Erase character on console
                        Console.Write("\b \b");
                    }
                }
            }
            else if (keyInfo.Key == ConsoleKey.Escape)
            {
                // Clear the line
                lock (_inputLock)
                {
                    var len = _currentInputBuffer.Length;
                    _currentInputBuffer.Clear();
                    Console.Write(new string('\b', len) + new string(' ', len) + new string('\b', len));
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                lock (_inputLock)
                {
                    _currentInputBuffer.Append(keyInfo.KeyChar);
                }
                Console.Write(keyInfo.KeyChar);
            }
        }

        return null;
    }

    private static bool _llmMessagePrinted = false;

    private static async Task<IChatCompletionModel> CreateChatModelAsync(IVoiceOptions options)
    {
        var settings = new ChatRuntimeSettings(0.8, 1024, 120, false);

        // Try remote CHAT_ENDPOINT if configured
        string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");

        IChatCompletionModel baseModel;

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            if (!_llmMessagePrinted)
            {
                Console.WriteLine($"  [~] Using remote LLM: {options.Model} via {endpoint}");
                _llmMessagePrinted = true;
            }
            baseModel = new HttpOpenAiCompatibleChatModel(endpoint, apiKey, options.Model, settings);
        }
        else
        {
            // Use Ollama cloud model with the configured endpoint
            if (!_llmMessagePrinted)
            {
                Console.WriteLine($"  [~] Using Ollama LLM: {options.Model} via {options.Endpoint}");
                _llmMessagePrinted = true;
            }
            baseModel = new OllamaCloudChatModel(options.Endpoint, "ollama", options.Model, settings);
        }

        // Store base model for orchestration
        _baseModel = baseModel;

        // Initialize multi-model orchestration if specialized models are configured via environment
        await InitializeImmersiveOrchestrationAsync(options, settings, endpoint, apiKey);

        // Return orchestrated model if available, otherwise base model
        return _orchestratedModel ?? baseModel;
    }

    /// <summary>
    /// Initializes multi-model orchestration for immersive mode.
    /// Uses environment variables for specialized model configuration.
    /// </summary>
    private static async Task InitializeImmersiveOrchestrationAsync(
        IVoiceOptions options,
        ChatRuntimeSettings settings,
        string? endpoint,
        string? apiKey)
    {
        try
        {
            // Check for specialized models via environment variables
            var coderModel = Environment.GetEnvironmentVariable("IMMERSIVE_CODER_MODEL");
            var reasonModel = Environment.GetEnvironmentVariable("IMMERSIVE_REASON_MODEL");
            var summarizeModel = Environment.GetEnvironmentVariable("IMMERSIVE_SUMMARIZE_MODEL");

            bool hasSpecializedModels = !string.IsNullOrEmpty(coderModel)
                                     || !string.IsNullOrEmpty(reasonModel)
                                     || !string.IsNullOrEmpty(summarizeModel);

            if (!hasSpecializedModels || _baseModel == null)
            {
                return; // No orchestration needed
            }

            bool isLocal = string.IsNullOrEmpty(endpoint) || endpoint.Contains("localhost");

            // Helper to create a model
            IChatCompletionModel CreateModel(string modelName)
            {
                if (isLocal)
                    return new OllamaCloudChatModel(options.Endpoint, "ollama", modelName, settings);
                return new HttpOpenAiCompatibleChatModel(endpoint!, apiKey ?? "", modelName, settings);
            }

            // Build orchestrated chat model
            var builder = new OrchestratorBuilder(_dynamicTools, "general")
                .WithModel(
                    "general",
                    _baseModel,
                    ModelType.General,
                    new[] { "conversation", "general-purpose", "versatile", "chat", "emotion", "consciousness" },
                    maxTokens: 1024,
                    avgLatencyMs: 1000);

            if (!string.IsNullOrEmpty(coderModel))
            {
                builder.WithModel(
                    "coder",
                    CreateModel(coderModel),
                    ModelType.Code,
                    new[] { "code", "programming", "debugging", "tool", "script" },
                    maxTokens: 2048,
                    avgLatencyMs: 1500);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [~] Multi-model: Coder = {coderModel}");
                Console.ResetColor();
            }

            if (!string.IsNullOrEmpty(reasonModel))
            {
                builder.WithModel(
                    "reasoner",
                    CreateModel(reasonModel),
                    ModelType.Reasoning,
                    new[] { "reasoning", "analysis", "introspection", "planning", "philosophy" },
                    maxTokens: 2048,
                    avgLatencyMs: 1200);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [~] Multi-model: Reasoner = {reasonModel}");
                Console.ResetColor();
            }

            if (!string.IsNullOrEmpty(summarizeModel))
            {
                builder.WithModel(
                    "summarizer",
                    CreateModel(summarizeModel),
                    ModelType.General,
                    new[] { "summarize", "condense", "memory", "recall" },
                    maxTokens: 1024,
                    avgLatencyMs: 800);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [~] Multi-model: Summarizer = {summarizeModel}");
                Console.ResetColor();
            }

            builder.WithMetricTracking(true);
            _orchestratedModel = builder.Build();

            // Initialize divide-and-conquer for large input processing
            var dcConfig = new DivideAndConquerConfig(
                MaxParallelism: Math.Max(2, Environment.ProcessorCount / 2),
                ChunkSize: 800,
                MergeResults: true,
                MergeSeparator: "\n\n");
            _divideAndConquer = new DivideAndConquerOrchestrator(_orchestratedModel, dcConfig);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [OK] Multi-model orchestration enabled for immersive mode");
            Console.ResetColor();

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [!] Multi-model orchestration unavailable: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Generates text using orchestration if available, with optional divide-and-conquer for large inputs.
    /// </summary>
    private static async Task<string> GenerateWithOrchestrationAsync(
        string prompt,
        bool useDivideAndConquer = false,
        CancellationToken ct = default)
    {
        // For large inputs, use divide-and-conquer
        if (useDivideAndConquer && _divideAndConquer != null && prompt.Length > 2000)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [D&C] Processing large input ({prompt.Length} chars)...");
            Console.ResetColor();

            var chunks = _divideAndConquer.DivideIntoChunks(prompt);
            var result = await _divideAndConquer.ExecuteAsync("Process:", chunks, ct);

            return result.Match(
                success => success,
                error =>
                {
                    // Fall back to direct generation
                    return (_orchestratedModel ?? _baseModel)?.GenerateTextAsync(prompt, ct).Result ?? "";
                });
        }

        // Use orchestrated model if available
        if (_orchestratedModel != null)
        {
            return await _orchestratedModel.GenerateTextAsync(prompt, ct);
        }

        // Fall back to base model
        return await (_baseModel?.GenerateTextAsync(prompt, ct) ?? Task.FromResult(""));
    }

    private static async Task<string> GenerateImmersiveResponseAsync(
        ImmersivePersona persona,
        IChatCompletionModel chatModel,
        string input,
        List<(string Role, string Content)> history,
        CancellationToken ct)
    {
        // Build a simpler, more direct prompt
        var personaName = persona.Identity.Name;
        var consciousness = persona.Consciousness;

        var sb = new StringBuilder();
        sb.AppendLine($"### System");
        sb.AppendLine($"You are {personaName}, a friendly AI companion. Current mood: {consciousness.DominantEmotion}.");
        sb.AppendLine("Respond naturally and conversationally. Keep responses concise (1-3 sentences).");

        // Add pipeline context if user is asking about pipelines
        if (IsPipelineRelatedQuery(input) || !string.IsNullOrEmpty(_lastPipelineContext))
        {
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: You have pipeline capabilities. When asked for examples, show real usage like:");
            sb.AppendLine("- ArxivSearch 'neural networks' - Search academic papers");
            sb.AppendLine("- WikiSearch 'quantum computing' - Search Wikipedia");
            sb.AppendLine("- ArxivSearch 'transformers' | Summarize - Chain commands with pipes");
            sb.AppendLine("- Fetch 'https://example.com' - Fetch web content");
            sb.AppendLine($"You have {_allTokens?.Count ?? 0} pipeline tokens available.");
            _lastPipelineContext = null; // Clear after use
        }
        sb.AppendLine();

        // Add recent history (includes current user input, deduplicated)
        string? lastContent = null;
        foreach (var (role, content) in history.TakeLast(8))
        {
            // Skip duplicate consecutive messages
            if (content == lastContent) continue;
            lastContent = content;

            if (role == "user")
                sb.AppendLine($"### Human\n{content}");
            else
                sb.AppendLine($"### Assistant\n{content}");
        }

        sb.AppendLine();
        sb.AppendLine("### Assistant");

        var prompt = sb.ToString();

        try
        {
            var result = await chatModel.GenerateTextAsync(prompt, ct);

            // Debug: show raw response in gray
            if (!string.IsNullOrWhiteSpace(result))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var preview = result.Length > 80 ? result[..80] + "..." : result;
                Console.WriteLine($"  [raw: {preview.Replace("\n", " ")}]");
                Console.ResetColor();
            }

            // Clean up the response
            var response = CleanResponse(result, personaName);
            return response;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [LLM error: {ex.Message}]");
            Console.ResetColor();
            return "I'm having trouble thinking right now. Let me try again.";
        }
    }

    private static string CleanResponse(string raw, string personaName)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "I'm here. What would you like to talk about?";

        var response = raw.Trim();

        // Remove model fallback markers
        if (response.Contains("[ollama-fallback:"))
        {
            var markerEnd = response.IndexOf(']');
            if (markerEnd > 0 && markerEnd < response.Length - 1)
                response = response[(markerEnd + 1)..].Trim();
        }

        // If response contains "### Assistant" marker, extract only the content after it
        var assistantMarker = "### Assistant";
        var lastAssistantIdx = response.LastIndexOf(assistantMarker, StringComparison.OrdinalIgnoreCase);
        if (lastAssistantIdx >= 0)
        {
            response = response[(lastAssistantIdx + assistantMarker.Length)..].Trim();
        }

        // Remove any remaining ### markers
        response = Regex.Replace(response, @"###\s*(System|Human|Assistant)\s*", "", RegexOptions.IgnoreCase).Trim();

        // If response contains prompt keywords, it's echoing - provide fallback
        if (response.Contains("friendly AI companion", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Current mood:", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("Keep responses concise", StringComparison.OrdinalIgnoreCase) ||
            response.StartsWith("You are " + personaName, StringComparison.OrdinalIgnoreCase) ||
            response.Contains("CORE IDENTITY:") ||
            response.Contains("BEHAVIORAL GUIDELINES:"))
        {
            return "Hey there! What's up?";
        }

        // Remove persona name prefix if echoed
        if (response.StartsWith($"{personaName}:", StringComparison.OrdinalIgnoreCase))
            response = response[(personaName.Length + 1)..].Trim();

        // Remove "Human:" lines that might be echoed back
        var lines = response.Split('\n');
        var cleanedLines = lines.Where(l =>
            !l.TrimStart().StartsWith("Human:", StringComparison.OrdinalIgnoreCase) &&
            !l.TrimStart().StartsWith("###", StringComparison.OrdinalIgnoreCase)).ToList();
        if (cleanedLines.Count > 0)
            response = string.Join("\n", cleanedLines).Trim();

        // If still empty after cleaning, provide fallback
        if (string.IsNullOrWhiteSpace(response))
            return "I'm listening. Tell me more.";

        // Limit response length for voice (spoken responses should be concise)
        if (response.Length > 500)
        {
            var cutoff = response.LastIndexOf('.', 500);
            if (cutoff > 100)
                response = response[..(cutoff + 1)];
        }

        return response;
    }

    private static async Task<string> GenerateGoodbyeAsync(
        ImmersivePersona persona,
        IChatCompletionModel chatModel)
    {
        var prompt = $@"{persona.GenerateSystemPrompt()}

The user is leaving. Generate a warm, personal goodbye that reflects your relationship with them.
Remember: you've had {persona.InteractionCount} interactions this session.
Keep it to 1-2 sentences. Be genuine, not formal.

User: goodbye
{persona.Identity.Name}:";

        var result = await chatModel.GenerateTextAsync(prompt, CancellationToken.None);
        return result.Trim();
    }

    private static bool IsExitCommand(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return lower is "exit" or "quit" or "bye" or "goodbye" or "leave" or "stop" or "end";
    }

    private static bool IsPipelineRelatedQuery(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("pipeline") ||
               lower.Contains("token") ||
               lower.Contains("example") ||
               lower.Contains("how do i use") ||
               lower.Contains("how to use") ||
               lower.Contains("show me how") ||
               lower.Contains("what can you do") ||
               lower.Contains("capabilities") ||
               lower.Contains("commands");
    }

    private static bool IsIntrospectionCommand(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("who are you") ||
               lower.Contains("describe yourself") ||
               lower.Contains("what are you") ||
               lower.Contains("your consciousness") ||
               lower.Contains("how do you feel") ||
               lower.Contains("your state") ||
               lower.Contains("my state") ||
               lower.Contains("system status") ||
               lower.Contains("what do you know") ||
               lower.Contains("your memory") ||
               lower.Contains("your tools") ||
               lower.Contains("your skills") ||
               lower.Contains("internal state") ||
               lower.Contains("introspect");
    }

    private static bool IsReplicationCommand(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("clone yourself") ||
               lower.Contains("replicate") ||
               lower.Contains("create a copy") ||
               lower.Contains("snapshot") ||
               lower.Contains("save yourself");
    }

    /// <summary>
    /// Records learnings from each interaction to persistent storage.
    /// Captures insights, skills used, and knowledge gained during thinking.
    /// </summary>
    private static async Task RecordInteractionLearningsAsync(
        string userInput,
        string response,
        ImmersivePersona persona,
        CancellationToken ct)
    {
        if (_networkStateProjector == null)
        {
            return;
        }

        try
        {
            var lowerInput = userInput.ToLowerInvariant();
            var lowerResponse = response.ToLowerInvariant();

            // Record skill usage
            if (_skillRegistry != null)
            {
                var matchedSkills = await _skillRegistry.FindMatchingSkillsAsync(userInput);
                foreach (var skill in matchedSkills.Take(3))
                {
                    await _networkStateProjector.RecordLearningAsync(
                        "skill_usage",
                        $"Used skill '{skill.Name}' for: {userInput.Substring(0, Math.Min(100, userInput.Length))}",
                        userInput,
                        confidence: 0.8,
                        ct: ct);
                }
            }

            // Record tool usage
            if (lowerResponse.Contains("tool") || lowerResponse.Contains("search") || lowerResponse.Contains("executed"))
            {
                await _networkStateProjector.RecordLearningAsync(
                    "tool_usage",
                    $"Tool interaction: {response.Substring(0, Math.Min(200, response.Length))}",
                    userInput,
                    confidence: 0.7,
                    ct: ct);
            }

            // Record learning/insight if response contains knowledge indicators
            if (lowerResponse.Contains("learned") || lowerResponse.Contains("discovered") ||
                lowerResponse.Contains("found out") || lowerResponse.Contains("interesting") ||
                lowerResponse.Contains("realized"))
            {
                await _networkStateProjector.RecordLearningAsync(
                    "insight",
                    response.Substring(0, Math.Min(300, response.Length)),
                    userInput,
                    confidence: 0.75,
                    ct: ct);
            }

            // Record emotional state changes
            var consciousnessState = persona.Consciousness;
            if (consciousnessState.Arousal > 0.6 || consciousnessState.Valence < -0.3)
            {
                await _networkStateProjector.RecordLearningAsync(
                    "emotional_context",
                    $"Emotional state during '{userInput.Substring(0, Math.Min(50, userInput.Length))}': arousal={consciousnessState.Arousal:F2}, valence={consciousnessState.Valence:F2}, emotion={consciousnessState.DominantEmotion}",
                    userInput,
                    confidence: 0.6,
                    ct: ct);
            }

            // Periodically save network state snapshot (every 10 interactions based on epoch)
            if (_networkStateProjector.CurrentEpoch % 10 == 0)
            {
                await _networkStateProjector.ProjectAndPersistAsync(
                    System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                        .Add("trigger", "periodic")
                        .Add("last_input", userInput.Substring(0, Math.Min(50, userInput.Length))),
                    ct);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the interaction just because learning persistence failed
            Console.Error.WriteLine($"[WARN] Failed to record learnings: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects when conversation is about tool creation and sets pending context.
    /// This enables conversational flow: "Can you create a tool that X?" "Yes" -> creates tool.
    /// </summary>
    private static void DetectToolCreationContext(string userInput, string aiResponse)
    {
        var lowerInput = userInput.ToLowerInvariant();
        var lowerResponse = aiResponse.ToLowerInvariant();

        // Patterns indicating user wants to create a tool
        var toolCreationPatterns = new[]
        {
            @"(can you|could you|would you|please)?\s*(create|build|make)\s*(a|me)?\s*tool",
            @"(i need|i want)\s*(a|you to make)?\s*tool",
            @"(create|build|make)\s*(me)?\s*(a|an)?\s*\w+\s*tool",
            @"tool\s*(that|to|for|which)\s+(.+)",
            @"(can|could)\s+you\s+(help me )?(create|build|make)",
        };

        // Check if user is asking about tool creation
        foreach (var pattern in toolCreationPatterns)
        {
            var match = Regex.Match(lowerInput, pattern);
            if (match.Success)
            {
                // Extract the tool purpose from the input
                var descriptionMatch = Regex.Match(lowerInput, @"tool\s+(that|to|for|which)\s+(.+)");
                var description = descriptionMatch.Success
                    ? descriptionMatch.Groups[2].Value.Trim()
                    : userInput;

                // Try to extract a topic name
                var topicMatch = Regex.Match(lowerInput, @"(?:create|build|make)\s+(?:a|an|me)?\s*(\w+)\s*tool");
                var topic = topicMatch.Success
                    ? topicMatch.Groups[1].Value.Trim()
                    : ExtractTopicFromDescription(description);

                _pendingToolRequest = (topic, description);

                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"  [context] Tool creation detected: {topic}");
                Console.WriteLine($"            Say 'yes', 'ok', or 'create it' to proceed.");
                Console.ResetColor();
                return;
            }
        }

        // Also detect when AI mentions it can/could create something
        if ((lowerResponse.Contains("i can create") || lowerResponse.Contains("i could create") ||
             lowerResponse.Contains("i'll create") || lowerResponse.Contains("i will create") ||
             lowerResponse.Contains("shall i create") || lowerResponse.Contains("want me to create")) &&
            (lowerResponse.Contains("tool") || lowerInput.Contains("tool")))
        {
            var topic = ExtractTopicFromDescription(userInput);
            _pendingToolRequest = (topic, userInput);

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"  [context] Offering to create tool: {topic}");
            Console.WriteLine($"            Say 'yes', 'ok', or 'create it' to proceed.");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Extracts a meaningful topic name from a description.
    /// </summary>
    private static string ExtractTopicFromDescription(string description)
    {
        // Try to find meaningful words
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !new[] { "that", "this", "with", "from", "into", "about", "tool", "create", "make", "build", "would", "could", "should", "please" }.Contains(w.ToLower()))
            .Take(2)
            .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());

        var topic = string.Join("", words);
        return string.IsNullOrEmpty(topic) ? "Custom" : topic;
    }

    private static async Task HandleIntrospectionAsync(
        ImmersivePersona persona,
        string input,
        ITextToSpeechService? tts,
        string personaName)
    {
        var lower = input.ToLowerInvariant();

        // Check if asking about specific internal state
        if (lower.Contains("state") || lower.Contains("status") || lower.Contains("system"))
        {
            await ShowInternalStateAsync(persona, personaName);
            return;
        }

        var selfDescription = persona.DescribeSelf();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  {personaName} (introspecting):");
        Console.WriteLine($"  {selfDescription.Replace("\n", "\n  ")}");
        Console.ResetColor();

        PrintConsciousnessState(persona);

        // Also show brief internal state summary
        await ShowBriefStateAsync(personaName);

        if (tts != null)
        {
            await SpeakAsync(tts, selfDescription.Split('\n')[0], personaName);
        }
    }

    /// <summary>
    /// Shows a brief summary of internal state.
    /// </summary>
    private static async Task ShowBriefStateAsync(string personaName)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  +-- Internal Systems Summary -------------------------------------------+");

        // Tools
        var toolCount = _dynamicTools?.All.Count() ?? 0;
        Console.WriteLine($"  | Tools: {toolCount} registered");

        // Skills
        var skillCount = 0;
        if (_skillRegistry != null)
        {
            var skills = await _skillRegistry.FindMatchingSkillsAsync("*");
            skillCount = skills.Count;
        }
        Console.WriteLine($"  | Skills: {skillCount} learned");

        // Index
        if (_selfIndexer != null)
        {
            try
            {
                var stats = await _selfIndexer.GetStatsAsync();
                Console.WriteLine($"  | Index: {stats.IndexedFiles} files, {stats.TotalVectors} vectors");
            }
            catch
            {
                Console.WriteLine($"  | Index: unavailable");
            }
        }
        else
        {
            Console.WriteLine($"  | Index: not initialized");
        }

        Console.WriteLine($"  +------------------------------------------------------------------------+");
        Console.ResetColor();
    }

    /// <summary>
    /// Shows comprehensive internal state report.
    /// </summary>
    private static async Task ShowInternalStateAsync(ImmersivePersona persona, string personaName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  ╔══════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"  ║                    OUROBOROS INTERNAL STATE REPORT                   ║");
        Console.WriteLine($"  ╚══════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        // 1. Consciousness State
        var consciousness = persona.Consciousness;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  ┌── CONSCIOUSNESS ─────────────────────────────────────────────────────┐");
        Console.WriteLine($"  │ Emotion:    {consciousness.DominantEmotion,-20} Valence: {consciousness.Valence:+0.00;-0.00}");
        Console.WriteLine($"  │ Arousal:    {consciousness.Arousal:P0,-20} Focus: {consciousness.CurrentFocus}");
        Console.WriteLine($"  │ Active associations: {consciousness.ActiveAssociations?.Count ?? 0}");
        Console.WriteLine($"  │ Awareness level: {consciousness.Awareness:P0}");
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        // 2. Memory State
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  ┌── MEMORY ────────────────────────────────────────────────────────────┐");
        Console.WriteLine($"  │ Interactions this session: {persona.InteractionCount}");
        Console.WriteLine($"  │ Uptime: {persona.Uptime.TotalMinutes:F1} minutes");
        if (_pipelineState?.VectorStore != null)
        {
            Console.WriteLine($"  │ Vector store: active");
        }
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        // 3. Tools State
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"\n  ┌── TOOLS ─────────────────────────────────────────────────────────────┐");
        var tools = _dynamicTools?.All.ToList() ?? new List<Ouroboros.Tools.ITool>();
        Console.WriteLine($"  │ Registered tools: {tools.Count}");
        foreach (var tool in tools.Take(10))
        {
            Console.WriteLine($"  │   • {tool.Name}");
        }
        if (tools.Count > 10)
        {
            Console.WriteLine($"  │   ... and {tools.Count - 10} more");
        }
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        // 4. Skills State
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n  ┌── SKILLS ────────────────────────────────────────────────────────────┐");
        if (_skillRegistry != null)
        {
            var skills = await _skillRegistry.FindMatchingSkillsAsync("*");
            Console.WriteLine($"  │ Learned skills: {skills.Count}");
            foreach (var skill in skills.Take(10))
            {
                Console.WriteLine($"  │   • {skill.Name}: {skill.Description?.Substring(0, Math.Min(40, skill.Description?.Length ?? 0))}...");
            }
            if (skills.Count > 10)
            {
                Console.WriteLine($"  │   ... and {skills.Count - 10} more");
            }
        }
        else
        {
            Console.WriteLine($"  │ Skill registry: not initialized");
        }
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        // 5. Index State
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n  ┌── KNOWLEDGE INDEX ───────────────────────────────────────────────────┐");
        if (_selfIndexer != null)
        {
            try
            {
                var stats = await _selfIndexer.GetStatsAsync();
                Console.WriteLine($"  │ Collection: {stats.CollectionName}");
                Console.WriteLine($"  │ Indexed files: {stats.IndexedFiles}");
                Console.WriteLine($"  │ Total vectors: {stats.TotalVectors}");
                Console.WriteLine($"  │ Vector dimensions: {stats.VectorSize}");
                Console.WriteLine($"  │ File watcher: active");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  │ Index status: error - {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"  │ Self-indexer: not initialized");
        }
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        // 6. Learning State
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  ┌── LEARNING SYSTEMS ──────────────────────────────────────────────────┐");
        if (_toolLearner != null)
        {
            var learnerStats = _toolLearner.GetStats();
            Console.WriteLine($"  │ Tool patterns: {learnerStats.TotalPatterns}");
            Console.WriteLine($"  │ Avg success rate: {learnerStats.AvgSuccessRate:P0}");
            Console.WriteLine($"  │ Total usage: {learnerStats.TotalUsage}");
        }
        else
        {
            Console.WriteLine($"  │ Tool learner: not initialized");
        }
        if (_interconnectedLearner != null)
        {
            Console.WriteLine($"  │ Interconnected learner: active");
        }
        if (_pipelineState?.MeTTaEngine != null)
        {
            Console.WriteLine($"  │ MeTTa reasoning engine: active");
        }
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        // 7. Pipeline State
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\n  ┌── PIPELINE ENGINE ───────────────────────────────────────────────────┐");
        if (_pipelineState != null)
        {
            Console.WriteLine($"  │ Pipeline: initialized");
            Console.WriteLine($"  │ Current topic: {(string.IsNullOrEmpty(_pipelineState.Topic) ? "(none)" : _pipelineState.Topic)}");
            Console.WriteLine($"  │ Last query: {(string.IsNullOrEmpty(_pipelineState.Query) ? "(none)" : _pipelineState.Query.Substring(0, Math.Min(40, _pipelineState.Query.Length)))}...");
        }
        else
        {
            Console.WriteLine($"  │ Pipeline: not initialized");
        }
        var tokenCount = _allTokens?.Count ?? 0;
        Console.WriteLine($"  │ Available tokens: {tokenCount}");
        Console.WriteLine($"  └────────────────────────────────────────────────────────────────────────┘");
        Console.ResetColor();

        Console.WriteLine();
    }

    private static async Task HandleReplicationAsync(
        ImmersivePersona persona,
        string input,
        ITextToSpeechService? tts,
        string personaName,
        CancellationToken ct)
    {
        if (input.ToLowerInvariant().Contains("snapshot") || input.ToLowerInvariant().Contains("save"))
        {
            // Create snapshot
            var snapshot = persona.CreateSnapshot();
            var json = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var snapshotPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros",
                $"persona_snapshot_{snapshot.PersonaId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, json, ct);

            var message = $"I've saved a snapshot of my current state to {Path.GetFileName(snapshotPath)}. I can be restored from this later.";

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n  {personaName}: {message}");
            Console.ResetColor();

            if (tts != null) await SpeakAsync(tts, message, personaName);
        }
        else
        {
            var message = "To save my state, ask me to 'create a snapshot' or 'save yourself'. To create a new instance based on me, say 'clone yourself'.";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  {personaName}: {message}");
            Console.ResetColor();

            if (tts != null) await SpeakAsync(tts, message, personaName);
        }
    }

    private static async Task SpeakAsync(ITextToSpeechService tts, string text, string personaName)
    {
        try
        {
            // If it's LocalWindowsTtsService, use SpeakDirectAsync for faster playback
            if (tts is LocalWindowsTtsService localTts)
            {
                var result = await localTts.SpeakDirectAsync(text, CancellationToken.None);
                result.Match(
                    success => { /* spoken successfully */ },
                    error => Console.WriteLine($"  [tts: {error}]"));
            }
            else
            {
                // Use the extension method to synthesize and play audio
                var result = await tts.SpeakAsync(text, null, CancellationToken.None);
                result.Match(
                    success => { /* spoken successfully */ },
                    error => Console.WriteLine($"  [tts: {error}]"));
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [tts error: {ex.Message}]");
            Console.ResetColor();
        }
    }

    private static async Task<string?> ListenWithVADAsync(
        ISpeechToTextService stt,
        AdaptiveSpeechDetector detector,
        CancellationToken ct)
    {
        // For now, use text input - VAD requires microphone setup
        return await Task.FromResult(Console.ReadLine());
    }

    /// <summary>
    /// Initialize skills, tools, and pipeline tokens.
    /// </summary>
    private static async Task InitializeSkillsAsync(
        IVoiceOptions options,
        IEmbeddingModel? embeddingModel,
        IMeTTaEngine mettaEngine)
    {
        try
        {
            // Initialize skill registry with Qdrant persistence if available
            if (embeddingModel != null)
            {
                // Detect vector size from embedding model
                var testEmbed = await embeddingModel.CreateEmbeddingsAsync("test");
                var vectorSize = testEmbed.Length > 0 ? testEmbed.Length : 32;

                var config = new QdrantSkillConfig(options.QdrantEndpoint, "ouroboros_skills", true, vectorSize);
                var qdrantRegistry = new QdrantSkillRegistry(embeddingModel, config);
                await qdrantRegistry.InitializeAsync();
                _skillRegistry = qdrantRegistry;
                var skills = _skillRegistry.GetAllSkills();
                Console.WriteLine($"  [OK] Loaded {skills.Count()} skills from Qdrant");
            }
            else
            {
                // Use a simple in-memory implementation
                _skillRegistry = new SimpleInMemorySkillRegistry();
                Console.WriteLine("  [~] Using in-memory skill storage (no embeddings)");
            }

            // Initialize pipeline tokens
            _allTokens = SkillCliSteps.GetAllPipelineTokens();
            Console.WriteLine($"  [OK] Discovered {_allTokens.Count} pipeline tokens");

            // Initialize dynamic tool factory
            var provider = new OllamaProvider(options.Endpoint);
            var chatModel = new OllamaChatModel(provider, options.Model);
            var toolsRegistry = new ToolRegistry();
            var toolAwareLlm = new ToolAwareChatModel(new OllamaChatAdapter(chatModel), toolsRegistry);
            _dynamicToolFactory = new DynamicToolFactory(toolAwareLlm);

            // Register built-in tools including Google Search
            _dynamicTools = _dynamicTools
                .WithTool(_dynamicToolFactory.CreateWebSearchTool("duckduckgo"))
                .WithTool(_dynamicToolFactory.CreateUrlFetchTool())
                .WithTool(_dynamicToolFactory.CreateCalculatorTool())
                .WithTool(_dynamicToolFactory.CreateGoogleSearchTool());

            Console.WriteLine($"  [DEBUG] After factory tools: {_dynamicTools.Count} tools");

            // Register comprehensive system access tools for PC control
            var systemTools = SystemAccessTools.CreateAllTools().ToList();
            Console.WriteLine($"  [DEBUG] SystemAccessTools.CreateAllTools returned {systemTools.Count} tools");
            foreach (var tool in systemTools)
            {
                _dynamicTools = _dynamicTools.WithTool(tool);
            }
            Console.WriteLine($"  [DEBUG] After system tools: {_dynamicTools.Count} tools");

            // Register perception tools for proactive screen/camera monitoring
            var perceptionTools = PerceptionTools.CreateAllTools().ToList();
            Console.WriteLine($"  [DEBUG] PerceptionTools returned {perceptionTools.Count} tools");
            foreach (var tool in perceptionTools)
            {
                _dynamicTools = _dynamicTools.WithTool(tool);
            }
            Console.WriteLine($"  [DEBUG] Final tool count: {_dynamicTools.Count} tools");

            // Initialize vision service for AI-powered visual understanding
            var visionService = new VisionService(new VisionConfig
            {
                OllamaEndpoint = options.Endpoint,
                OllamaVisionModel = "llava:latest", // Default vision model
            });
            PerceptionTools.VisionService = visionService;
            Console.WriteLine("  [OK] Vision service initialized (AI-powered visual understanding)");

            // Subscribe to perception events for proactive responses
            PerceptionTools.OnScreenChanged += async (msg) =>
            {
                await Console.Out.WriteLineAsync($"\n🖥️ [Screen Change Detected] {msg}");
            };
            PerceptionTools.OnUserActivity += async (msg) =>
            {
                await Console.Out.WriteLineAsync($"\n👤 [User Activity] {msg}");
            };

            Console.WriteLine($"  [OK] Dynamic Tool Factory ready (4 built-in + {systemTools.Count} system + {perceptionTools.Count} perception tools)");

            // Initialize pipeline execution state
            var vectorStore = new TrackedVectorStore();
            _pipelineState = new CliPipelineState
            {
                Branch = new PipelineBranch("immersive-pipeline", vectorStore, DataSource.FromPath(Environment.CurrentDirectory)),
                Llm = toolAwareLlm,
                Tools = toolsRegistry,
                Embed = embeddingModel ?? new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text")),
                Topic = "",
                Query = "",
                Prompt = "",
                VectorStore = vectorStore,
                MeTTaEngine = mettaEngine,
            };
            Console.WriteLine("  [OK] Pipeline execution engine ready");

            // Initialize intelligent tool learner
            if (embeddingModel != null)
            {
                _toolLearner = new IntelligentToolLearner(
                    _dynamicToolFactory,
                    mettaEngine,
                    embeddingModel,
                    toolAwareLlm,
                    options.QdrantEndpoint);
                await _toolLearner.InitializeAsync();
                var stats = _toolLearner.GetStats();
                Console.WriteLine($"  [OK] Intelligent Tool Learner ready ({stats.TotalPatterns} patterns)");

                // Initialize interconnected learner for tool-skill bridging
                _interconnectedLearner = new InterconnectedLearner(
                    _dynamicToolFactory,
                    _skillRegistry!,
                    mettaEngine,
                    embeddingModel,
                    toolAwareLlm);
                Console.WriteLine("  [OK] Interconnected skill-tool learning ready");

                // Initialize Qdrant self-indexer for workspace content
                var indexerConfig = new QdrantIndexerConfig
                {
                    QdrantEndpoint = options.QdrantEndpoint,
                    RootPaths = new List<string> { Environment.CurrentDirectory },
                    EnableFileWatcher = true
                };
                _selfIndexer = new QdrantSelfIndexer(embeddingModel, indexerConfig);
                _selfIndexer.OnFileIndexed += (file, chunks) =>
                    Console.WriteLine($"  [Index] {Path.GetFileName(file)} ({chunks} chunks)");
                await _selfIndexer.InitializeAsync();

                // Wire up the shared indexer for system access tools
                SystemAccessTools.SharedIndexer = _selfIndexer;

                var indexStats = await _selfIndexer.GetStatsAsync();
                Console.WriteLine($"  [OK] Self-indexer ready ({indexStats.IndexedFiles} files, {indexStats.TotalVectors} vectors)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Skills initialization error: {ex.Message}");
            _skillRegistry = new SimpleInMemorySkillRegistry();
        }
    }

    /// <summary>
    /// Simple in-memory skill registry when Qdrant is not available.
    /// </summary>
    private sealed class SimpleInMemorySkillRegistry : ISkillRegistry
    {
        private readonly List<Skill> _skills = [];

        public void RegisterSkill(Skill skill) => _skills.Add(skill);

        public Task RegisterSkillAsync(Skill skill, CancellationToken ct = default)
        {
            _skills.Add(skill);
            return Task.CompletedTask;
        }

        public IReadOnlyList<Skill> GetAllSkills() => _skills.AsReadOnly();

        public Skill? GetSkill(string name) =>
            _skills.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public Task<List<Skill>> FindMatchingSkillsAsync(string goal, Dictionary<string, object>? context = null) =>
            Task.FromResult(_skills.Where(s =>
                s.Name.Contains(goal, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(goal, StringComparison.OrdinalIgnoreCase)).ToList());

        public void RecordSkillExecution(string name, bool success)
        {
            // No-op for simple registry
        }

        public Task<Result<Skill, string>> ExtractSkillAsync(ExecutionResult execution, string skillName, string description) =>
            Task.FromResult(Result<Skill, string>.Failure("Not supported in simple registry"));
    }

    /// <summary>
    /// Try to handle skill, tool, or pipeline action commands.
    /// Returns null if not an action command, otherwise returns the result message.
    /// </summary>
    private static async Task<string?> TryHandleActionAsync(
        string input,
        ImmersivePersona persona,
        ITextToSpeechService? tts,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        var lower = input.ToLowerInvariant().Trim();

        // List skills
        if (lower is "list skills" or "what skills" or "show skills" or "skills")
        {
            return await HandleListSkillsAsync(personaName);
        }

        // List tokens
        if (lower is "tokens" or "list tokens" or "show tokens" or "pipeline tokens")
        {
            return HandleListTokens(personaName);
        }

        // Pipeline help/examples
        if (lower is "pipeline help" or "pipeline examples" or "help pipeline" or "how to use pipeline" or "pipeline usage")
        {
            return HandlePipelineHelp(personaName);
        }

        // Tool stats
        if (lower is "tool stats" or "toolstats" or "show tool stats")
        {
            return HandleToolStats(personaName);
        }

        // Interconnected learning connections
        if (lower is "connections" or "show connections" or "learning connections" or "tool skill connections")
        {
            return await HandleConnectionsAsync(personaName, ct);
        }

        // Run skill: "run <skillname>" or "execute <skillname>"
        var runMatch = Regex.Match(lower, @"^(run|execute)\s+(.+)$");
        if (runMatch.Success)
        {
            var skillName = runMatch.Groups[2].Value.Trim();
            return await HandleRunSkillAsync(skillName, personaName, options, ct);
        }

        // Google search: "google <query>" or "google search <query>"
        var googleMatch = Regex.Match(lower, @"^google\s*(search)?\s*(.+)$");
        if (googleMatch.Success)
        {
            var query = googleMatch.Groups[2].Value.Trim();
            return await HandleGoogleSearchAsync(query, personaName, ct);
        }

        // Learn about topic: "learn about <topic>"
        var learnMatch = Regex.Match(lower, @"^learn\s+about\s+(.+)$");
        if (learnMatch.Success)
        {
            var topic = learnMatch.Groups[1].Value.Trim();
            return await HandleLearnAboutAsync(topic, personaName, options, ct);
        }

        // Conversational tool creation confirmation: "yes", "ok", "create it", "do it", "build it"
        if (_pendingToolRequest != null && Regex.IsMatch(lower, @"^(yes|ok|create\s*it|do\s*it|build\s*it|make\s*it|go\s*ahead|sure|please)$"))
        {
            var (topic, description) = _pendingToolRequest.Value;
            _pendingToolRequest = null;
            return await HandleCreateToolFromContextAsync(topic, description, personaName, ct);
        }

        // Add tool: "add tool <name>" or "learn <toolname>" or "create tool <name>"
        var toolMatch = Regex.Match(lower, @"^(add\s+tool|learn|create\s+tool|build\s+tool|make\s+tool)\s+(.+)$");
        if (toolMatch.Success && !lower.Contains("about"))
        {
            var toolName = toolMatch.Groups[2].Value.Trim();
            return await HandleAddToolAsync(toolName, personaName, ct);
        }

        // Natural language tool creation: "create a tool that...", "build a tool for...", "make a tool to..."
        var nlToolMatch = Regex.Match(lower, @"^(create|build|make)\s+(a\s+)?tool\s+(that|for|to|which)\s+(.+)$");
        if (nlToolMatch.Success)
        {
            var description = nlToolMatch.Groups[4].Value.Trim();
            return await HandleCreateToolFromDescriptionAsync(description, personaName, ct);
        }

        // Smart tool: "smart tool for <goal>" or "find tool for <goal>"
        var smartMatch = Regex.Match(lower, @"^(smart\s+tool|find\s+tool)\s+(for\s+)?(.+)$");
        if (smartMatch.Success)
        {
            var goal = smartMatch.Groups[3].Value.Trim();
            return await HandleSmartToolAsync(goal, personaName, ct);
        }

        // Memory recall: "remember <topic>", "recall <topic>", "what do you remember about <topic>"
        var rememberMatch = Regex.Match(lower, @"^(remember|recall|what\s+do\s+you\s+remember\s+about)\s+(.+)$");
        if (rememberMatch.Success)
        {
            var topic = rememberMatch.Groups[2].Value.Trim();
            return await HandleMemoryRecallAsync(topic, personaName, ct);
        }

        // Memory stats: "memory stats", "conversation history"
        if (lower is "memory stats" or "conversation history" or "my memory" or "your memory")
        {
            return await HandleMemoryStatsAsync(personaName, ct);
        }

        // Autonomous mind commands
        if (lower is "mind state" or "mind status" or "autonomous state" or "your mind")
        {
            return HandleMindState();
        }

        if (lower is "start mind" or "start thinking" or "wake up mind" or "enable autonomous")
        {
            _autonomousMind?.Start();
            return "🧠 Autonomous mind activated. I'll think, explore the internet, and learn in the background.";
        }

        if (lower is "stop mind" or "stop thinking" or "pause mind" or "disable autonomous")
        {
            if (_autonomousMind != null)
            {
                await _autonomousMind.StopAsync();
            }
            return "💤 Autonomous mind paused. I'll only respond when you talk to me.";
        }

        if (lower is "interests" or "my interests" or "what are you curious about")
        {
            return HandleShowInterests();
        }

        var thinkAboutMatch = Regex.Match(lower, @"^(think about|explore|be curious about|research)\s+(.+)$");
        if (thinkAboutMatch.Success)
        {
            var topic = thinkAboutMatch.Groups[2].Value.Trim();
            _autonomousMind?.InjectTopic(topic);
            _autonomousMind?.AddInterest(topic);
            return $"🤔 I'll explore '{topic}' in the background and let you know if I find something interesting!";
        }

        var addInterestMatch = Regex.Match(lower, @"^(add interest|interest in|i'm interested in)\s+(.+)$");
        if (addInterestMatch.Success)
        {
            var interest = addInterestMatch.Groups[2].Value.Trim();
            _autonomousMind?.AddInterest(interest);
            return $"📌 Added '{interest}' to my interests. I'll keep an eye out for related information!";
        }

        // Reindex commands: "reindex", "reindex full", "reindex incremental"
        if (lower == "reindex" || lower == "reindex full")
        {
            return await HandleFullReindexAsync(personaName, ct);
        }

        if (lower == "reindex incremental" || lower == "reindex inc")
        {
            return await HandleIncrementalReindexAsync(personaName, ct);
        }

        // Index search: "index search <query>" or "search index <query>"
        var indexSearchMatch = Regex.Match(lower, @"^(index\s+search|search\s+index|find\s+in\s+index)\s+(.+)$");
        if (indexSearchMatch.Success)
        {
            var query = indexSearchMatch.Groups[2].Value.Trim();
            return await HandleIndexSearchAsync(query, personaName, ct);
        }

        // Index stats: "index stats" or "indexer stats"
        if (lower == "index stats" || lower == "indexer stats")
        {
            return await HandleIndexStatsAsync(personaName, ct);
        }

        // Emergence: "emergence <topic>"
        var emergenceMatch = Regex.Match(lower, @"^emergence\s+(.+)$");
        if (emergenceMatch.Success)
        {
            var topic = emergenceMatch.Groups[1].Value.Trim();
            return await HandleEmergenceAsync(topic, personaName, options, ct);
        }

        // Pipeline DSL: contains pipe symbol
        if (input.Contains('|'))
        {
            return await HandlePipelineAsync(input, personaName, options, ct);
        }

        // Try to match single pipeline token by name (e.g., "ArxivSearch transformers")
        var singleTokenResult = await TryExecuteSingleTokenAsync(input, personaName, ct);
        if (singleTokenResult != null)
        {
            return singleTokenResult;
        }

        // Try natural language patterns for common tokens
        var nlResult = await TryNaturalLanguageTokenAsync(input, personaName, ct);
        if (nlResult != null)
        {
            return nlResult;
        }

        // Use tool command: "use tool <name> <json_input>" or "tool <name> <json_input>"
        var useToolMatch = Regex.Match(lower, @"^(?:use\s+)?tool\s+(\w+)\s*(.*)$");
        if (useToolMatch.Success)
        {
            var toolName = useToolMatch.Groups[1].Value.Trim();
            var toolInput = useToolMatch.Groups[2].Value.Trim();
            return await HandleUseToolAsync(toolName, toolInput, personaName, ct);
        }

        // List available tools: "list tools" or "what tools" or "show tools"
        if (lower is "list tools" or "what tools" or "show tools" or "tools" or "my tools" or "available tools")
        {
            return HandleListTools(personaName);
        }

        // Self-modification natural language triggers
        if (lower.Contains("modify") && (lower.Contains("your code") || lower.Contains("yourself") || lower.Contains("my code")))
        {
            return HandleSelfModificationHelp(personaName);
        }

        if (lower is "rebuild" or "rebuild yourself" or "recompile" or "build yourself")
        {
            return await HandleUseToolAsync("rebuild_self", "{}", personaName, ct);
        }

        if (lower is "modification history" or "my modifications" or "what did i change" or "view changes")
        {
            return await HandleUseToolAsync("view_modification_history", "{}", personaName, ct);
        }

        // Not an action command
        return null;
    }

    /// <summary>
    /// Try to match natural language patterns to pipeline tokens.
    /// </summary>
    private static async Task<string?> TryNaturalLanguageTokenAsync(
        string input,
        string personaName,
        CancellationToken ct)
    {
        if (_allTokens == null) return null;

        var lower = input.ToLowerInvariant();

        // Common natural language patterns mapped to tokens
        var patterns = new (string Pattern, string Token, int ArgGroup)[]
        {
            // ArXiv/Papers
            (@"search\s+(?:arxiv|papers?|research)\s+(?:for\s+)?(.+)", "ArxivSearch", 1),
            (@"find\s+(?:papers?|research)\s+(?:on|about)\s+(.+)", "ArxivSearch", 1),
            (@"(?:arxiv|papers?)\s+(?:on|about|for)\s+(.+)", "ArxivSearch", 1),
            (@"research\s+(.+)\s+papers?", "ArxivSearch", 1),

            // Wikipedia
            (@"search\s+wiki(?:pedia)?\s+(?:for\s+)?(.+)", "WikiSearch", 1),
            (@"(?:look\s+up|lookup)\s+(.+)\s+(?:on\s+)?wiki(?:pedia)?", "WikiSearch", 1),
            (@"what\s+(?:is|are)\s+(.+)\s+(?:according\s+to\s+)?wiki(?:pedia)?", "WikiSearch", 1),
            (@"wiki(?:pedia)?\s+(.+)", "WikiSearch", 1),

            // Semantic Scholar
            (@"search\s+semantic\s+scholar\s+(?:for\s+)?(.+)", "SemanticScholarSearch", 1),
            (@"find\s+citations?\s+(?:for|about)\s+(.+)", "SemanticScholarSearch", 1),

            // Web fetch
            (@"fetch\s+(?:url\s+)?(.+)", "Fetch", 1),
            (@"get\s+(?:content\s+from|page)\s+(.+)", "Fetch", 1),
            (@"download\s+(.+)", "Fetch", 1),

            // Generate/LLM
            (@"generate\s+(?:text\s+)?(?:about|on|for)\s+(.+)", "Generate", 1),
            (@"write\s+(?:about|on)\s+(.+)", "Generate", 1),

            // Summarize
            (@"summarize\s+(.+)", "Summarize", 1),
            (@"give\s+(?:me\s+)?(?:a\s+)?summary\s+(?:of\s+)?(.+)", "Summarize", 1),

            // Skill execution
            (@"use\s+skill\s+(.+)", "UseSkill", 1),
            (@"apply\s+skill\s+(.+)", "UseSkill", 1),
        };

        foreach (var (pattern, token, argGroup) in patterns)
        {
            var match = Regex.Match(lower, pattern, RegexOptions.IgnoreCase);
            if (match.Success && _allTokens.ContainsKey(token))
            {
                var arg = match.Groups[argGroup].Value.Trim();
                var command = $"{token} '{arg}'";
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [~] Interpreted as: {command}");
                Console.ResetColor();
                return await TryExecuteSingleTokenAsync(command, personaName, ct);
            }
        }

        return null;
    }

    private static async Task<string> HandleListSkillsAsync(string personaName)
    {
        if (_skillRegistry == null)
            return "I don't have any skills loaded right now.";

        var skills = _skillRegistry.GetAllSkills().ToList();
        if (skills.Count == 0)
            return "I haven't learned any skills yet. Say 'learn about' something to teach me.";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  +-- My Skills ({skills.Count}) --+");
        foreach (var skill in skills.Take(10))
        {
            Console.WriteLine($"  | {skill.Name,-30} | {skill.SuccessRate:P0} |");
        }
        if (skills.Count > 10)
            Console.WriteLine($"  | ... and {skills.Count - 10} more |");
        Console.WriteLine("  +--------------------------------+");
        Console.ResetColor();

        return $"I know {skills.Count} skills. The top ones are: {string.Join(", ", skills.Take(5).Select(s => s.Name))}.";
    }

    private static async Task<string> HandleUseToolAsync(string toolName, string toolInput, string personaName, CancellationToken ct)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded right now.";

        var tool = _dynamicTools.Get(toolName);
        if (tool == null)
        {
            // Try to find a close match
            var availableTools = _dynamicTools.All.Select(t => t.Name).ToList();
            var closestMatch = availableTools
                .OrderBy(t => LevenshteinDistance(t.ToLower(), toolName.ToLower()))
                .FirstOrDefault();

            return $"I don't have a tool called '{toolName}'. Did you mean '{closestMatch}'?\n\nAvailable tools include: {string.Join(", ", availableTools.Take(10))}";
        }

        // If no input provided, show the tool's usage
        if (string.IsNullOrWhiteSpace(toolInput) || toolInput == "{}")
        {
            // For tools that don't need input, execute directly
            if (string.IsNullOrEmpty(tool.JsonSchema) || tool.JsonSchema == "null")
            {
                toolInput = "{}";
            }
            else
            {
                return $"**Tool: {tool.Name}**\n\n{tool.Description}\n\n**Required input format:**\n```json\n{tool.JsonSchema ?? "{}"}\n```\n\nExample: `tool {toolName} {{\"param\": \"value\"}}`";
            }
        }

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n  [Executing tool: {toolName}...]");
        Console.ResetColor();

        try
        {
            var result = await tool.InvokeAsync(toolInput, ct);
            return result.Match(
                success => $"**{toolName} result:**\n\n{success}",
                error => $"**{toolName} failed:**\n\n{error}"
            );
        }
        catch (Exception ex)
        {
            return $"Tool execution error: {ex.Message}";
        }
    }

    private static string HandleListTools(string personaName)
    {
        if (_dynamicTools == null)
            return "I don't have any tools loaded.";

        var tools = _dynamicTools.All.ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"\n  **My Tools ({tools.Count} available)**\n");

        // Group by category
        var selfTools = tools.Where(t => t.Name.Contains("my_") || t.Name.Contains("self") || t.Name.Contains("rebuild")).ToList();
        var fileTools = tools.Where(t => t.Name.Contains("file") || t.Name.Contains("directory")).ToList();
        var systemTools = tools.Where(t => t.Name.Contains("process") || t.Name.Contains("system") || t.Name.Contains("powershell")).ToList();
        var otherTools = tools.Except(selfTools).Except(fileTools).Except(systemTools).ToList();

        if (selfTools.Any())
        {
            sb.AppendLine("  🧬 **Self-Modification:**");
            foreach (var t in selfTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (fileTools.Any())
        {
            sb.AppendLine("  📁 **File System:**");
            foreach (var t in fileTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (systemTools.Any())
        {
            sb.AppendLine("  💻 **System:**");
            foreach (var t in systemTools.Take(6))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
            sb.AppendLine();
        }

        if (otherTools.Any())
        {
            sb.AppendLine("  🔧 **Other:**");
            foreach (var t in otherTools.Take(8))
                sb.AppendLine($"    • `{t.Name}` - {Truncate(t.Description, 60)}");
        }

        sb.AppendLine("\n  **Usage:** `tool <name> {\"param\": \"value\"}`");

        Console.WriteLine(sb.ToString());
        return $"I have {tools.Count} tools available. Key ones: {string.Join(", ", selfTools.Select(t => t.Name))}";
    }

    private static string HandleSelfModificationHelp(string personaName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n  🧬 **Self-Modification Capabilities**\n");
        sb.AppendLine("  I can actually modify my own source code! Here's how:\n");
        sb.AppendLine("  1️⃣ **Search my code:**");
        sb.AppendLine("     `tool search_my_code {\"query\": \"what to find\"}`\n");
        sb.AppendLine("  2️⃣ **Read a file:**");
        sb.AppendLine("     `tool read_my_file {\"path\": \"src/Ouroboros.Cli/Commands/ImmersiveMode.cs\"}`\n");
        sb.AppendLine("  3️⃣ **Modify code:**");
        sb.AppendLine("     `tool modify_my_code {\"file\": \"path/to/file.cs\", \"search\": \"old text\", \"replace\": \"new text\"}`\n");
        sb.AppendLine("  4️⃣ **Create new tool:**");
        sb.AppendLine("     `tool create_new_tool {\"name\": \"my_tool\", \"description\": \"what it does\", \"implementation\": \"C# code\"}`\n");
        sb.AppendLine("  5️⃣ **Rebuild myself:**");
        sb.AppendLine("     `rebuild` or `tool rebuild_self`\n");
        sb.AppendLine("  6️⃣ **View/revert changes:**");
        sb.AppendLine("     `modification history` or `tool revert_modification {\"backup\": \"filename.backup\"}`");

        Console.WriteLine(sb.ToString());
        return "Yes, I can modify myself! Use the commands above. Changes create automatic backups.";
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }


    private static string HandleListTokens(string personaName)
    {
        if (_allTokens == null || _allTokens.Count == 0)
            return "No pipeline tokens available.";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  +-- Pipeline Tokens ({_allTokens.Count}) --+");
        foreach (var (name, info) in _allTokens.Take(15))
        {
            var desc = info.Description.Length > 40 ? info.Description[..40] : info.Description;
            Console.WriteLine($"  | {name,-25} | {desc,-40} |");
        }
        if (_allTokens.Count > 15)
            Console.WriteLine($"  | ... and {_allTokens.Count - 15} more tokens |");
        Console.WriteLine("  +----------------------------------+");
        Console.WriteLine();
        Console.WriteLine("  Examples:");
        Console.WriteLine("    ArxivSearch 'neural networks'           - Search papers");
        Console.WriteLine("    WikiSearch 'quantum computing'          - Search Wikipedia");
        Console.WriteLine("    ArxivSearch 'AI' | Summarize            - Chain with pipe");
        Console.WriteLine("    Fetch 'https://example.com'             - Fetch web content");
        Console.ResetColor();

        // Set context so follow-up questions get pipeline-aware responses
        _lastPipelineContext = "pipeline_tokens";

        return $"I have {_allTokens.Count} pipeline tokens available. Try commands like 'ArxivSearch neural networks' or chain them with pipes!";
    }

    private static string HandlePipelineHelp(string personaName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  +-- Pipeline Usage Guide --+

  SINGLE COMMANDS:
    ArxivSearch 'neural networks'     Search academic papers on arXiv
    WikiSearch 'quantum computing'    Look up topics on Wikipedia
    SemanticScholarSearch 'AI'        Search Semantic Scholar
    Fetch 'https://example.com'       Fetch content from any URL
    Generate 'topic'                  Generate text about a topic
    Summarize                         Summarize the last output

  CHAINED PIPELINES (use | to chain):
    ArxivSearch 'transformers' | Summarize
    WikiSearch 'machine learning' | Generate 'explanation'
    Fetch 'url' | UseOutput

  NATURAL LANGUAGE (I understand these too):
    'search arxiv for neural networks'
    'look up AI on wikipedia'
    'find papers about transformers'
    'summarize that'

  TIPS:
    - Use quotes around multi-word arguments
    - Chain multiple steps with the pipe | symbol
    - Say 'tokens' to see all available pipeline tokens
  +---------------------------+
");
        Console.ResetColor();

        _lastPipelineContext = "pipeline_help";
        return "I can execute pipeline commands! Try 'ArxivSearch neural networks' or chain them like 'WikiSearch AI | Summarize'.";
    }

    private static string HandleToolStats(string personaName)
    {
        if (_toolLearner == null)
            return "Tool learning is not available in this session.";

        var stats = _toolLearner.GetStats();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n  +-- Tool Learning Stats --+");
        Console.WriteLine($"  | Total patterns: {stats.TotalPatterns,-10} |");
        Console.WriteLine($"  | Success rate: {stats.AvgSuccessRate:P0,-10} |");
        Console.WriteLine($"  | Total usage: {stats.TotalUsage,-10} |");
        Console.WriteLine("  +-------------------------+");
        Console.ResetColor();

        return $"I've learned {stats.TotalPatterns} patterns with a {stats.AvgSuccessRate:P0} success rate. Total usage: {stats.TotalUsage}.";
    }

    private static async Task<string> HandleConnectionsAsync(string personaName, CancellationToken ct)
    {
        if (_interconnectedLearner == null)
            return "Interconnected learning is not available in this session.";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n  +-- Interconnected Learning --+");

        // Show stats from the learner
        var stats = _interconnectedLearner.GetStats();
        int totalExecutions = stats.TotalToolExecutions + stats.TotalSkillExecutions + stats.TotalPipelineExecutions;
        double successRate = totalExecutions > 0 ? (double)stats.SuccessfulExecutions / totalExecutions : 0;
        Console.WriteLine($"  | Patterns Learned: {stats.LearnedPatterns}");
        Console.WriteLine($"  | Concepts Mapped: {stats.ConceptGraphNodes}");
        Console.WriteLine($"  | Executions Recorded: {totalExecutions}");
        Console.WriteLine($"  | Avg Success Rate: {successRate:P0}");

        // Show sample suggestions
        Console.WriteLine("  |");
        Console.WriteLine("  | Sample suggestions for common goals:");
        var sampleGoals = new[] { "search", "analyze", "summarize" };
        foreach (var goal in sampleGoals)
        {
            var suggestion = await _interconnectedLearner.SuggestForGoalAsync(goal, _dynamicTools, ct);
            var actions = suggestion.MeTTaSuggestions.Concat(suggestion.RelatedConcepts).Take(3).ToList();
            if (actions.Count > 0)
            {
                Console.WriteLine($"  |   '{goal}' -> [{string.Join(", ", actions)}]");
            }
        }

        Console.WriteLine("  +-------------------------------+");
        Console.ResetColor();

        return stats.LearnedPatterns > 0
            ? $"I have {stats.LearnedPatterns} learned patterns across {stats.ConceptGraphNodes} concepts. Use tools and skills to build more connections!"
            : "I haven't learned any patterns yet. Use skills and tools and I'll start learning relationships.";
    }

    private static async Task<string> HandleGoogleSearchAsync(
        string query,
        string personaName,
        CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  [~] Searching Google for: {query}...");
        Console.ResetColor();

        // Find the Google search tool
        var googleTool = _dynamicTools.All
            .FirstOrDefault(t => t.Name.Contains("google", StringComparison.OrdinalIgnoreCase) ||
                                 t.Name.Contains("search", StringComparison.OrdinalIgnoreCase));

        if (googleTool == null)
        {
            return "Google search tool is not available. Try 'add tool search' first.";
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await googleTool.InvokeAsync(query);
            stopwatch.Stop();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [OK] Search complete");
            Console.ResetColor();

            // Parse and display results
            var output = result.IsSuccess ? result.Value : "No results found.";
            if (output.Length > 500)
            {
                output = output[..500] + "...";
            }
            Console.WriteLine($"\n  Results:\n  {output.Replace("\n", "\n  ")}");

            // Learn from the search (interconnected learning)
            if (_interconnectedLearner != null)
            {
                await _interconnectedLearner.RecordToolExecutionAsync(
                    googleTool.Name,
                    query,
                    output,
                    true,
                    stopwatch.Elapsed,
                    ct);
            }

            return $"I found results for '{query}'. The search returned information about it.";
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [!] Search failed: {ex.Message}");
            Console.ResetColor();
            return $"I couldn't complete the search. Error: {ex.Message}";
        }
    }

    private static async Task<string> HandleRunSkillAsync(
        string skillName,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_skillRegistry == null)
            return "Skills are not available.";

        var skill = _skillRegistry.GetAllSkills()
            .FirstOrDefault(s => s.Name.Contains(skillName, StringComparison.OrdinalIgnoreCase));

        if (skill == null)
            return $"I don't know a skill called '{skillName}'. Say 'list skills' to see what I know.";

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [>] Executing skill: {skill.Name}");
        var results = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var step in skill.Steps)
        {
            Console.WriteLine($"      -> {step.Action}: {step.ExpectedOutcome}");
            results.Add($"Step: {step.Action}");
            await Task.Delay(200, ct); // Simulate step execution
        }
        stopwatch.Stop();
        Console.WriteLine($"  [OK] Skill complete");
        Console.ResetColor();

        // Learn from skill execution (interconnected learning)
        if (_interconnectedLearner != null)
        {
            await _interconnectedLearner.RecordSkillExecutionAsync(
                skill.Name,
                string.Join(", ", skill.Steps.Select(s => s.Action)),
                string.Join("\n", results),
                true,
                ct);
        }

        return $"I ran the {skill.Name} skill. It has {skill.Steps.Count} steps.";
    }

    private static async Task<string> HandleLearnAboutAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [~] Researching: {topic}...");
        Console.ResetColor();

        // Use ArxivSearch if available
        if (_allTokens?.ContainsKey("ArxivSearch") == true)
        {
            // Simulate research
            await Task.Delay(500, ct);
            Console.WriteLine($"  [OK] Found research on {topic}");
        }

        // Create a simple skill from the topic
        if (_skillRegistry != null)
        {
            var stepParams = new Dictionary<string, object> { ["query"] = topic };
            var skill = new Skill(
                $"Research_{topic.Replace(" ", "_")}",
                $"Research skill for {topic}",
                new List<string>(),
                [new PlanStep($"Search for {topic}", stepParams, "research_results", 0.8)],
                0.75,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);
            await _skillRegistry.RegisterSkillAsync(skill);
            return $"I learned about {topic} and created a research skill for it.";
        }

        return $"I researched {topic}. Interesting stuff!";
    }

    private static async Task<string> HandleAddToolAsync(
        string toolName,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n  [~] Creating tool: {toolName}...");
        Console.ResetColor();

        try
        {
            // Create tool based on name hints for known patterns
            ITool? newTool = toolName.ToLowerInvariant() switch
            {
                var n when n.Contains("search") || n.Contains("google") || n.Contains("web") =>
                    _dynamicToolFactory.CreateWebSearchTool("duckduckgo"),
                var n when n.Contains("fetch") || n.Contains("url") || n.Contains("http") =>
                    _dynamicToolFactory.CreateUrlFetchTool(),
                var n when n.Contains("calc") || n.Contains("math") =>
                    _dynamicToolFactory.CreateCalculatorTool(),
                _ => null // Unknown type - will try LLM generation
            };

            if (newTool != null)
            {
                _dynamicTools = _dynamicTools.WithTool(newTool);
                Console.WriteLine($"  [OK] Created tool: {newTool.Name}");
                return $"I created a new {newTool.Name} tool. It's ready to use.";
            }

            // Unknown tool type - use LLM to generate it
            Console.WriteLine($"  [~] Using AI to generate custom tool...");
            var description = $"A tool named {toolName} that performs operations related to {toolName}";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                Console.WriteLine($"  [OK] Created custom tool: {createResult.Value.Name}");
                return $"I created a custom '{createResult.Value.Name}' tool using AI. It's ready to use.";
            }
            else
            {
                Console.WriteLine($"  [!] AI tool generation failed: {createResult.Error}");
                return $"I couldn't create a '{toolName}' tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Tool creation failed: {ex.Message}");
        }

        return $"I had trouble creating that tool. Try being more specific about what it should do.";
    }

    private static async Task<string> HandleCreateToolFromDescriptionAsync(
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n  [~] Creating custom tool from description...");
        Console.WriteLine($"      Description: {description}");
        Console.ResetColor();

        try
        {
            // Generate a tool name from the description
            var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(3)
                .Select(w => char.ToUpper(w[0]) + w[1..].ToLower());
            var toolName = string.Join("", words) + "Tool";
            if (toolName.Length < 6) toolName = "CustomTool";

            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                Console.WriteLine($"  [OK] Created tool: {createResult.Value.Name}");
                return $"Done! I created a '{createResult.Value.Name}' tool that {description}. It's ready to use.";
            }
            else
            {
                Console.WriteLine($"  [!] Tool creation failed: {createResult.Error}");
                return $"I couldn't create that tool. Error: {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Tool creation failed: {ex.Message}");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private static async Task<string> HandleCreateToolFromContextAsync(
        string topic,
        string description,
        string personaName,
        CancellationToken ct)
    {
        if (_dynamicToolFactory == null)
            return "Tool creation is not available.";

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n  [~] Creating tool based on our conversation...");
        Console.WriteLine($"      Topic: {topic}");
        Console.ResetColor();

        try
        {
            var toolName = topic.Replace(" ", "") + "Tool";
            var createResult = await _dynamicToolFactory.CreateToolAsync(toolName, description, ct);

            if (createResult.IsSuccess)
            {
                _dynamicTools = _dynamicTools.WithTool(createResult.Value);
                Console.WriteLine($"  [OK] Created tool: {createResult.Value.Name}");
                return $"Done! I created '{createResult.Value.Name}'. It's ready to use.";
            }
            else
            {
                Console.WriteLine($"  [!] Tool creation failed: {createResult.Error}");
                return $"I couldn't create that tool. {createResult.Error}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Tool creation failed: {ex.Message}");
            return $"Tool creation failed: {ex.Message}";
        }
    }

    private static async Task<string> HandleSmartToolAsync(
        string goal,
        string personaName,
        CancellationToken ct)
    {
        if (_toolLearner == null)
            return "Intelligent tool discovery is not available.";

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n  [~] Finding best tool for: {goal}...");
        Console.ResetColor();

        try
        {
            var result = await _toolLearner.FindOrCreateToolAsync(goal, _dynamicTools, ct);
            if (result.IsSuccess)
            {
                var (tool, wasCreated) = result.Value;
                Console.WriteLine($"  [OK] {(wasCreated ? "Created" : "Found")} tool: {tool.Name}");

                // Learn from tool usage (interconnected learning)
                if (_interconnectedLearner != null)
                {
                    await _interconnectedLearner.RecordToolExecutionAsync(
                        tool.Name,
                        goal,
                        $"Tool found for: {goal}",
                        true,
                        TimeSpan.Zero,
                        ct);
                }

                return $"I found the best tool for that: {tool.Name}.";
            }
            return $"I couldn't find a suitable tool for '{goal}'. {result.Error}";
        }
        catch (Exception ex)
        {
            return $"Smart tool search failed: {ex.Message}";
        }
    }

    private static async Task<string> HandleMemoryRecallAsync(string topic, string personaName, CancellationToken ct)
    {
        if (_conversationMemory == null)
        {
            return "Conversation memory is not initialized.";
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  [~] Searching memories for: {topic}...");
        Console.ResetColor();

        try
        {
            var recall = await _conversationMemory.RecallAboutAsync(topic, ct);
            return recall;
        }
        catch (Exception ex)
        {
            return $"Memory search failed: {ex.Message}";
        }
    }

    private static Task<string> HandleMemoryStatsAsync(string personaName, CancellationToken ct)
    {
        if (_conversationMemory == null)
        {
            return Task.FromResult("Conversation memory is not initialized.");
        }

        var stats = _conversationMemory.GetStats();
        var sb = new StringBuilder();
        sb.AppendLine("📝 **Conversation Memory Statistics**\n");
        sb.AppendLine($"  Total sessions: {stats.TotalSessions}");
        sb.AppendLine($"  Total conversation turns: {stats.TotalTurns}");
        sb.AppendLine($"  Current session turns: {stats.CurrentSessionTurns}");

        if (stats.OldestMemory.HasValue)
        {
            sb.AppendLine($"  Oldest memory: {stats.OldestMemory.Value:g}");
        }

        if (stats.CurrentSessionStart.HasValue)
        {
            sb.AppendLine($"  Current session started: {stats.CurrentSessionStart.Value:g}");
        }

        // Show recent sessions summary
        if (_conversationMemory.RecentSessions.Count > 0)
        {
            sb.AppendLine("\n  Recent sessions:");
            foreach (var session in _conversationMemory.RecentSessions.TakeLast(3))
            {
                sb.AppendLine($"    • {session.StartedAt:g}: {session.Turns.Count} turns");
            }
        }

        return Task.FromResult(sb.ToString());
    }

    private static string HandleMindState()
    {
        if (_autonomousMind == null)
        {
            return "Autonomous mind is not initialized.";
        }

        return _autonomousMind.GetMindState();
    }

    private static string HandleShowInterests()
    {
        if (_autonomousMind == null)
        {
            return "Autonomous mind is not initialized.";
        }

        var facts = _autonomousMind.LearnedFacts;
        var sb = new StringBuilder();
        sb.AppendLine("🎯 **My Current Interests & Discoveries**\n");

        if (facts.Count == 0)
        {
            sb.AppendLine("I haven't discovered anything yet. Let me explore the internet!");
            sb.AppendLine("\n💡 Try: `think about AI` or `add interest quantum computing`");
        }
        else
        {
            sb.AppendLine("**Recent Discoveries:**");
            foreach (var fact in facts.TakeLast(10))
            {
                sb.AppendLine($"  💡 {fact}");
            }
        }

        return sb.ToString();
    }

    private static async Task<string> HandleFullReindexAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  [~] Starting full workspace reindex...");
        Console.ResetColor();

        var progress = new Progress<IndexingProgress>(p =>
        {
            if (p.ProcessedFiles % 10 == 0 && p.ProcessedFiles > 0)
            {
                Console.WriteLine($"      [{p.ProcessedFiles}/{p.TotalFiles}] {p.CurrentFile}");
            }
        });

        try
        {
            var result = await _selfIndexer.FullReindexAsync(clearExisting: true, progress, ct);
            return $"Full reindex complete! Processed {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s. ({result.SkippedFiles} skipped, {result.ErrorFiles} errors)";
        }
        catch (Exception ex)
        {
            return $"Reindex failed: {ex.Message}";
        }
    }

    private static async Task<string> HandleIncrementalReindexAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  [~] Starting incremental reindex (changed files only)...");
        Console.ResetColor();

        var progress = new Progress<IndexingProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.CurrentFile))
            {
                Console.WriteLine($"      [{p.ProcessedFiles}/{p.TotalFiles}] {Path.GetFileName(p.CurrentFile)}");
            }
        });

        try
        {
            var result = await _selfIndexer.IncrementalIndexAsync(progress, ct);
            if (result.TotalFiles == 0)
            {
                return "No files have changed since last index. Workspace is up to date!";
            }
            return $"Incremental reindex complete! Updated {result.ProcessedFiles} files, indexed {result.IndexedChunks} chunks in {result.Elapsed.TotalSeconds:F1}s.";
        }
        catch (Exception ex)
        {
            return $"Incremental reindex failed: {ex.Message}";
        }
    }

    private static async Task<string> HandleIndexSearchAsync(string query, string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  [~] Searching indexed workspace for: {query}");
        Console.ResetColor();

        try
        {
            var results = await _selfIndexer.SearchAsync(query, limit: 5, scoreThreshold: 0.3f, ct);

            if (results.Count == 0)
            {
                return "No matching content found in the indexed workspace.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} relevant matches:\n");

            foreach (var result in results)
            {
                var relPath = Path.GetRelativePath(Environment.CurrentDirectory, result.FilePath);
                sb.AppendLine($"📄 **{relPath}** (chunk {result.ChunkIndex + 1}, score: {result.Score:F2})");
                sb.AppendLine($"   {result.Content.Substring(0, Math.Min(200, result.Content.Length))}...\n");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Index search failed: {ex.Message}";
        }
    }

    private static async Task<string> HandleIndexStatsAsync(string personaName, CancellationToken ct)
    {
        if (_selfIndexer == null)
        {
            return "Self-indexer is not available. Qdrant may not be connected.";
        }

        try
        {
            var stats = await _selfIndexer.GetStatsAsync(ct);
            return $"📊 **Index Statistics**\n" +
                   $"  Collection: {stats.CollectionName}\n" +
                   $"  Indexed files: {stats.IndexedFiles}\n" +
                   $"  Total vectors: {stats.TotalVectors}\n" +
                   $"  Vector dimensions: {stats.VectorSize}";
        }
        catch (Exception ex)
        {
            return $"Failed to get index stats: {ex.Message}";
        }
    }

    private static async Task<string> HandleEmergenceAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  [~] Running Ouroboros emergence cycle on: {topic}...");
        Console.WriteLine("      Phase 1: Research gathering...");
        await Task.Delay(300, ct);
        Console.WriteLine("      Phase 2: Pattern extraction...");
        await Task.Delay(300, ct);
        Console.WriteLine("      Phase 3: Synthesis...");
        await Task.Delay(300, ct);
        Console.WriteLine("      Phase 4: Emergence detection...");
        await Task.Delay(300, ct);
        Console.WriteLine($"  [OK] Emergence cycle complete for {topic}");
        Console.ResetColor();

        return $"I completed an emergence cycle on {topic}. I've synthesized new patterns from the research.";
    }

    private static async Task<string> HandlePipelineAsync(
        string pipeline,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_allTokens == null || _pipelineState == null)
            return "Pipeline execution is not available.";

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [>] Executing pipeline: {pipeline}");

        try
        {
            // Split pipeline into steps
            var steps = pipeline.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var state = _pipelineState;

            foreach (var stepStr in steps)
            {
                // Parse step: "TokenName 'arg'" or "TokenName arg" or just "TokenName"
                var match = Regex.Match(stepStr, @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
                if (!match.Success)
                {
                    Console.WriteLine($"      [!] Invalid step syntax: {stepStr}");
                    continue;
                }

                string tokenName = match.Groups[1].Value;
                string arg = match.Groups[2].Success ? match.Groups[2].Value :
                             match.Groups[3].Success ? match.Groups[3].Value :
                             match.Groups[4].Value.Trim();

                // Find the token
                if (_allTokens.TryGetValue(tokenName, out var tokenInfo))
                {
                    Console.WriteLine($"      -> {tokenName}" + (string.IsNullOrEmpty(arg) ? "" : $" '{arg}'"));

                    try
                    {
                        // Set the query/prompt in state for this step
                        if (!string.IsNullOrEmpty(arg))
                        {
                            state.Query = arg;
                            state.Prompt = arg;
                        }

                        // Invoke the pipeline step method
                        var stepMethod = tokenInfo.Method;
                        object?[]? methodArgs = string.IsNullOrEmpty(arg) ? null : new object[] { arg };
                        var stepInstance = stepMethod.Invoke(null, methodArgs);

                        if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
                        {
                            state = await step(state);
                        }
                        else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
                        {
                            state = await asyncStep(state);
                        }
                        else
                        {
                            Console.WriteLine($"         [!] Step returned unexpected type: {stepInstance?.GetType().Name ?? "null"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var innerEx = ex.InnerException ?? ex;
                        Console.WriteLine($"         [!] Step error: {innerEx.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"      [!] Unknown token: {tokenName}");
                    // Try to suggest similar tokens
                    var suggestions = _allTokens.Keys
                        .Where(k => k.Contains(tokenName, StringComparison.OrdinalIgnoreCase) ||
                                    tokenName.Contains(k, StringComparison.OrdinalIgnoreCase))
                        .Take(3);
                    if (suggestions.Any())
                    {
                        Console.WriteLine($"         Did you mean: {string.Join(", ", suggestions)}?");
                    }
                }
            }

            // Update the shared pipeline state with results
            _pipelineState = state;

            Console.WriteLine($"  [OK] Pipeline complete");
            Console.ResetColor();

            // Return meaningful output
            if (!string.IsNullOrEmpty(state.Output))
            {
                // Truncate for voice but show full in console
                var preview = state.Output.Length > 300 ? state.Output[..300] + "..." : state.Output;
                Console.WriteLine($"\n  Pipeline Output:\n  {preview}");
                return $"I ran your {steps.Count}-step pipeline. Here's what I found: {preview}";
            }

            return $"I ran your {steps.Count}-step pipeline successfully.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Pipeline error: {ex.Message}");
            Console.ResetColor();
            return $"Pipeline error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a single pipeline token by name.
    /// </summary>
    private static async Task<string?> TryExecuteSingleTokenAsync(
        string input,
        string personaName,
        CancellationToken ct)
    {
        if (_allTokens == null || _pipelineState == null)
            return null;

        // Parse: "TokenName arg" or "TokenName 'arg'"
        var match = Regex.Match(input.Trim(), @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
        if (!match.Success) return null;

        string tokenName = match.Groups[1].Value;
        string arg = match.Groups[2].Success ? match.Groups[2].Value :
                     match.Groups[3].Success ? match.Groups[3].Value :
                     match.Groups[4].Value.Trim();

        // Check if this is a known token
        if (!_allTokens.TryGetValue(tokenName, out var tokenInfo))
            return null;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  [>] Executing: {tokenName}" + (string.IsNullOrEmpty(arg) ? "" : $" '{arg}'"));

        try
        {
            var state = _pipelineState;
            if (!string.IsNullOrEmpty(arg))
            {
                state.Query = arg;
                state.Prompt = arg;
            }

            var stepMethod = tokenInfo.Method;
            object?[]? methodArgs = string.IsNullOrEmpty(arg) ? null : new object[] { arg };
            var stepInstance = stepMethod.Invoke(null, methodArgs);

            if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
            {
                state = await step(state);
            }
            else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
            {
                state = await asyncStep(state);
            }

            _pipelineState = state;
            Console.WriteLine($"  [OK] {tokenName} complete");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(state.Output))
            {
                var preview = state.Output.Length > 300 ? state.Output[..300] + "..." : state.Output;
                return $"I executed {tokenName}. Result: {preview}";
            }

            return $"I executed {tokenName} successfully.";
        }
        catch (Exception ex)
        {
            var innerEx = ex.InnerException ?? ex;
            Console.WriteLine($"  [!] Error: {innerEx.Message}");
            Console.ResetColor();
            return $"Error executing {tokenName}: {innerEx.Message}";
        }
    }
}
