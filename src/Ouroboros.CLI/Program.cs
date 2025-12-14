#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==============================
// Minimal CLI entry (top-level)
// ==============================

using System.Diagnostics;
using System.Reactive.Linq;
using CommandLine;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Diagnostics; // added
using LangChainPipeline.Options;
using LangChainPipeline.Providers; // for OllamaEmbeddingAdapter
using LangChainPipeline.Providers.SpeechToText; // for STT services
using LangChainPipeline.Providers.TextToSpeech; // for TTS services
using Microsoft.Extensions.Hosting;

using LangChainPipeline.Tools.MeTTa; // added
using LangChainPipeline.Speech; // Adaptive speech detection
using Ouroboros.Application.Tools; // Dynamic tool factory
using Ouroboros.Application.Personality; // Personality engine with MeTTa + GA
using Ouroboros.CLI; // added
using Ouroboros.CLI.Commands;

try
{
    // Optional minimal host
    if (args.Contains("--host-only"))
    {
        using IHost onlyHost = await LangChainPipeline.Interop.Hosting.MinimalHost.BuildAsync(args);
        await onlyHost.RunAsync();
        return;
    }

    await ParseAndRunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

return;

// ---------------
// Local functions
// ---------------

static async Task ParseAndRunAsync(string[] args)
{
    // CommandLineParser verbs - OuroborosOptions is the default (isDefault: true)
    await Parser.Default.ParseArguments<OuroborosOptions, AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions, OrchestratorOptions, MeTTaOptions, AssistOptions, SkillsOptions, NetworkOptions, DagOptions, EnvironmentOptions, AffectOptions, PolicyOptions, MaintenanceOptions>(args)
        .MapResult(
            (OuroborosOptions o) => RunOuroborosAsync(o),
            (AskOptions o) => RunAskAsync(o),
            (PipelineOptions o) => RunPipelineAsync(o),
            (ListTokensOptions _) => RunListTokensAsync(),
            (ExplainOptions o) => RunExplainAsync(o),
            (TestOptions o) => RunTestsAsync(o),
            (OrchestratorOptions o) => RunOrchestratorAsync(o),
            (MeTTaOptions o) => RunMeTTaAsync(o),
            (AssistOptions o) => RunAssistAsync(o),
            (SkillsOptions o) => RunSkillsAsync(o),
            (NetworkOptions o) => RunNetworkAsync(o),
            (DagOptions o) => RunDagAsync(o),
            (EnvironmentOptions o) => RunEnvironmentAsync(o),
            (AffectOptions o) => RunAffectAsync(o),
            (PolicyOptions o) => RunPolicyAsync(o),
            (MaintenanceOptions o) => RunMaintenanceAsync(o),
            _ => Task.CompletedTask
        );
}

// (usage handled by CommandLineParser built-in help)
static Task RunListTokensAsync()
{
    Console.WriteLine("Available token groups:");
    foreach ((System.Reflection.MethodInfo method, IReadOnlyList<string> names) in StepRegistry.GetTokenGroups())
    {
        Console.WriteLine($"- {method.DeclaringType?.Name}.{method.Name}(): {string.Join(", ", names)}");
    }
    return Task.CompletedTask;
}

// Helper method to create the appropriate remote chat model based on endpoint type
static IChatCompletionModel CreateRemoteChatModel(string endpoint, string apiKey, string modelName, ChatRuntimeSettings? settings, ChatEndpointType endpointType)
{
    return endpointType switch
    {
        ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey, modelName, settings),
        ChatEndpointType.LiteLLM => new LiteLLMChatModel(endpoint, apiKey, modelName, settings),
        ChatEndpointType.GitHubModels => new GitHubModelsChatModel(apiKey, modelName, endpoint, settings),
        ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings),
        ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings), // Default to OpenAI-compatible for auto
        _ => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings)
    };
}

static Task RunExplainAsync(ExplainOptions o)
{
    Console.WriteLine(PipelineDsl.Explain(o.Dsl));
    return Task.CompletedTask;
}

static Task RunOuroborosAsync(OuroborosOptions o) => OuroborosCommands.RunOuroborosAsync(o);

static Task RunPipelineAsync(PipelineOptions o) => PipelineCommands.RunPipelineAsync(o);

static async Task RunAskAsync(AskOptions o)
{
    if (await TryRunVoiceModeAsync(o)) return;
    await AskCommands.RunAskAsync(o);
}

// ------------------
// CommandLineParser
// ------------------

static Task RunTestsAsync(TestOptions o) => TestCommands.RunTestsAsync(o);

static async Task RunOrchestratorAsync(OrchestratorOptions o)
{
    if (await TryRunVoiceModeAsync(o)) return;
    await OrchestratorCommands.RunOrchestratorAsync(o);
}

static Task RunMeTTaAsync(MeTTaOptions o) => MeTTaCommands.RunMeTTaAsync(o);

static Task RunNetworkAsync(NetworkOptions o) => NetworkCommands.RunAsync(o);

static Task RunDagAsync(DagOptions o) => DagCommands.RunDagAsync(o);

static Task RunEnvironmentAsync(EnvironmentOptions o) => EnvironmentCommands.RunEnvironmentCommandAsync(o);

static Task RunAffectAsync(AffectOptions o) => AffectCommands.RunAffectAsync(o);

static Task RunPolicyAsync(PolicyOptions o) => PolicyCommands.RunPolicyAsync(o);

static Task RunMaintenanceAsync(MaintenanceOptions o) => MaintenanceCommands.RunMaintenanceAsync(o);



static async Task RunAssistAsync(AssistOptions o)
{
    // Run Ouroboros agent mode by default (unless --dsl-mode is specified)
    if (!o.DslMode)
    {
        // Convert AssistOptions to OuroborosConfig for the unified agent
        var config = new OuroborosConfig(
            Persona: o.Persona,
            Model: o.Model,
            Endpoint: o.Endpoint ?? "http://localhost:11434",
            EmbedModel: o.EmbedModel,
            EmbedEndpoint: "http://localhost:11434",
            QdrantEndpoint: o.QdrantEndpoint,
            ApiKey: Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            Voice: o.Voice,
            VoiceOnly: o.VoiceOnly,
            LocalTts: o.LocalTts,
            Debug: o.Debug,
            Temperature: o.Temperature,
            MaxTokens: o.MaxTokens
        );

        await using var agent = new OuroborosAgent(config);
        await agent.InitializeAsync();
        await agent.RunAsync();
        return;
    }

    // DSL Assistant mode (legacy)
    Console.WriteLine("+--------------------------------------------------------------+");
    Console.WriteLine("|   DSL Assistant - GitHub Copilot-like Code Intelligence     |");
    Console.WriteLine("+--------------------------------------------------------------+\n");

    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

    try
    {
        // Setup LLM
        OllamaProvider provider = new OllamaProvider();
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);

        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
            o.Endpoint,
            o.ApiKey,
            o.EndpointType);

        IChatCompletionModel chatModel;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            chatModel = CreateRemoteChatModel(endpoint, apiKey, o.Model, settings, endpointType);
        }
        else
        {
            chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
        }

        ToolRegistry tools = ToolRegistry.CreateDefault();
        ToolAwareChatModel llm = new ToolAwareChatModel(chatModel, tools);
        DslAssistant assistant = new DslAssistant(llm, tools);

        Console.WriteLine($"[OK] Assistant initialized\n");

        // Execute based on mode
        switch (o.Mode.ToLowerInvariant())
        {
            case "suggest":
                if (string.IsNullOrEmpty(o.Dsl))
                {
                    Console.WriteLine("Error: --dsl required for suggest mode");
                    return;
                }
                var suggestions = await assistant.SuggestNextStepAsync(o.Dsl, maxSuggestions: o.MaxSuggestions);
                suggestions.Match(
                    list =>
                    {
                        Console.WriteLine("=== Suggested Next Steps ===");
                        foreach (var s in list)
                            Console.WriteLine($"  - {s.Token}: {s.Explanation}");
                    },
                    error => Console.WriteLine($"Error: {error}"));
                break;

            case "complete":
                if (string.IsNullOrEmpty(o.PartialToken))
                {
                    Console.WriteLine("Error: --partial required for complete mode");
                    return;
                }
                var completions = assistant.CompleteToken(o.PartialToken, o.MaxSuggestions);
                completions.Match(
                    list => Console.WriteLine($"Completions: {string.Join(", ", list)}"),
                    error => Console.WriteLine($"Error: {error}"));
                break;

            case "validate":
                if (string.IsNullOrEmpty(o.Dsl))
                {
                    Console.WriteLine("Error: --dsl required for validate mode");
                    return;
                }
                var validation = await assistant.ValidateAndFixAsync(o.Dsl);
                validation.Match(
                    result =>
                    {
                        Console.WriteLine($"Valid: {result.IsValid}");
                        if (result.Errors.Count > 0)
                            Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
                        if (result.FixedDsl != null)
                            Console.WriteLine($"Fix: {result.FixedDsl}");
                    },
                    error => Console.WriteLine($"Error: {error}"));
                break;

            case "explain":
                if (string.IsNullOrEmpty(o.Dsl))
                {
                    Console.WriteLine("Error: --dsl required for explain mode");
                    return;
                }
                var explanation = await assistant.ExplainDslAsync(o.Dsl);
                explanation.Match(
                    text => Console.WriteLine($"=== Explanation ===\n{text}"),
                    error => Console.WriteLine($"Error: {error}"));
                break;

            case "build":
                if (string.IsNullOrEmpty(o.Goal))
                {
                    Console.WriteLine("Error: --goal required for build mode");
                    return;
                }
                var dsl = await assistant.BuildDslInteractivelyAsync(o.Goal);
                dsl.Match(
                    text => Console.WriteLine($"=== Generated DSL ===\n{text}"),
                    error => Console.WriteLine($"Error: {error}"));
                break;

            default:
                Console.WriteLine($"Unknown mode: {o.Mode}");
                Console.WriteLine("Available modes: suggest, complete, validate, explain, build");
                break;
        }

        Console.WriteLine("\n[OK] Assistant execution completed");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (o.Debug)
            Console.Error.WriteLine(ex.StackTrace);
        Environment.Exit(1);
    }
}

static async Task RunSkillsAsync(SkillsOptions o)
{
    Console.WriteLine("+--------------------------------------------------------------+");
    Console.WriteLine("|   [*] Research Skills - Auto-Generated DSL Tokens           |");
    Console.WriteLine("+--------------------------------------------------------------+\n");

    // Helper to create PlanStep with proper Dictionary
    static PlanStep MakeStep(string action, string param, string outcome, double confidence) =>
        new PlanStep(action, new Dictionary<string, object> { ["hint"] = param }, outcome, confidence);

    // Initialize skill registry - Qdrant by default, JSON as fallback
    ISkillRegistry registry;
    IAsyncDisposable? disposableRegistry = null;

    if (o.UseJsonStorage)
    {
        // Fallback: Use JSON file storage
        string skillsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ouroboros", "skills.json");
        var persistentConfig = new PersistentSkillConfig(StoragePath: skillsPath, AutoSave: true);
        var jsonRegistry = new PersistentSkillRegistry(config: persistentConfig);
        await jsonRegistry.InitializeAsync();
        registry = jsonRegistry;
        disposableRegistry = jsonRegistry;

        var stats = jsonRegistry.GetStats();
        if (stats.IsPersisted)
        {
            Console.WriteLine($"  [+] Loaded {stats.TotalSkills} skills from {stats.StoragePath} (JSON)");
        }
        else
        {
            Console.WriteLine($"  [i] Skills will be saved to {stats.StoragePath}");
        }
    }
    else
    {
        // Default: Use Qdrant vector storage
        try
        {
            // Initialize embedding model for semantic search
            OllamaProvider embedProvider = new OllamaProvider(o.Endpoint);
            OllamaEmbeddingModel embeddingModel = new OllamaEmbeddingModel(embedProvider, o.EmbedModel);
            IEmbeddingModel embedding = new OllamaEmbeddingAdapter(embeddingModel);

            var qdrantConfig = new QdrantSkillConfig(
                ConnectionString: o.QdrantEndpoint,
                CollectionName: o.QdrantCollection,
                AutoSave: true);
            var qdrantRegistry = new QdrantSkillRegistry(embedding, qdrantConfig);
            await qdrantRegistry.InitializeAsync();
            registry = qdrantRegistry;
            disposableRegistry = qdrantRegistry;

            var stats = qdrantRegistry.GetStats();
            Console.WriteLine($"  [+] Loaded {stats.TotalSkills} skills from Qdrant ({o.QdrantEndpoint}/{o.QdrantCollection})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Qdrant connection failed: {ex.Message}");
            Console.WriteLine($"  [i] Falling back to JSON storage...");

            // Fallback to JSON
            string skillsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ouroboros", "skills.json");
            var persistentConfig = new PersistentSkillConfig(StoragePath: skillsPath, AutoSave: true);
            var jsonRegistry = new PersistentSkillRegistry(config: persistentConfig);
            await jsonRegistry.InitializeAsync();
            registry = jsonRegistry;
            disposableRegistry = jsonRegistry;

            var stats = jsonRegistry.GetStats();
            Console.WriteLine($"  [+] Loaded {stats.TotalSkills} skills from {stats.StoragePath} (JSON fallback)");
        }
    }

    try
    {

    // Register predefined research skills (only if not already loaded)
    if (registry.GetAllSkills().Count == 0)
    {
        var predefinedSkills = new[]
        {
            new Skill("LiteratureReview", "Synthesize research papers into coherent review",
                new List<string> { "research-context" },
                new List<PlanStep> { MakeStep("Identify themes", "Scan papers", "Key themes", 0.9),
                        MakeStep("Compare findings", "Cross-reference", "Patterns", 0.85),
                        MakeStep("Synthesize", "Combine insights", "Review", 0.8) },
                0.85, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("HypothesisGeneration", "Generate testable hypotheses from observations",
                new List<string> { "observations" },
                new List<PlanStep> { MakeStep("Find gaps", "Identify unknowns", "Questions", 0.8),
                        MakeStep("Generate hypotheses", "Form predictions", "Hypotheses", 0.75),
                        MakeStep("Rank by testability", "Evaluate", "Ranked list", 0.7) },
                0.78, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("ChainOfThoughtReasoning", "Apply step-by-step reasoning to problems",
                new List<string> { "problem-statement" },
                new List<PlanStep> { MakeStep("Decompose problem", "Break down", "Sub-problems", 0.9),
                        MakeStep("Reason through steps", "Apply logic", "Intermediate results", 0.85),
                        MakeStep("Synthesize answer", "Combine", "Final answer", 0.88) },
                0.88, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("CrossDomainTransfer", "Transfer insights across domains",
                new List<string> { "source-domain", "target-domain" },
                new List<PlanStep> { MakeStep("Abstract patterns", "Generalize", "Abstract concepts", 0.7),
                        MakeStep("Map to target", "Apply", "Mapped insights", 0.65),
                        MakeStep("Validate transfer", "Test", "Validated insights", 0.6) },
                0.65, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("CitationAnalysis", "Analyze citation networks for influence",
                new List<string> { "paper-set" },
                new List<PlanStep> { MakeStep("Build citation graph", "Extract refs", "Graph", 0.85),
                        MakeStep("Rank by influence", "PageRank-style", "Rankings", 0.8),
                        MakeStep("Find key papers", "Identify", "Key papers", 0.82) },
                0.82, 0, DateTime.UtcNow, DateTime.UtcNow),
        };

        // Use async registration to ensure skills are persisted to Qdrant
        foreach (var skill in predefinedSkills)
            await registry.RegisterSkillAsync(skill);

        Console.WriteLine($"  [OK] Registered {predefinedSkills.Length} predefined skills");
    }
    Console.WriteLine();

    // Voice mode takes priority - use unified immersive mode
    if (o.Voice)
    {
        await ImmersiveMode.RunImmersiveAsync(o);
        return;
    }

    // Default to --list if no option specified (unless interactive mode)
    if (!o.List && !o.Tokens && !o.Interactive && string.IsNullOrEmpty(o.Fetch) && string.IsNullOrEmpty(o.Suggest) && string.IsNullOrEmpty(o.Run))
    {
        o.List = true;
    }

    if (o.List)
    {
        Console.WriteLine("[i] REGISTERED SKILLS:\n");
        foreach (var skill in registry.GetAllSkills())
        {
            Console.WriteLine($"  [*] {skill.Name,-30} ({skill.SuccessRate:P0})");
            Console.WriteLine($"     {skill.Description}");
            Console.WriteLine($"     Steps: {string.Join(" -> ", skill.Steps.Select(s => s.Action.Split(' ')[0]))}");
            Console.WriteLine();
        }
    }

    if (o.Tokens)
    {
        Console.WriteLine("[#] AUTO-GENERATED DSL TOKENS:\n");
        Console.WriteLine("  Built-in: SetPrompt, UseDraft, UseCritique, UseRevise, UseOutput\n");
        Console.WriteLine("  Skill-based (from research patterns):");
        foreach (var skill in registry.GetAllSkills())
        {
            Console.WriteLine($"    - UseSkill_{skill.Name}");
        }
        Console.WriteLine();
    }

    if (!string.IsNullOrEmpty(o.Fetch))
    {
        Console.WriteLine($"[>] Fetching research on: \"{o.Fetch}\"...\n");
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(o.Fetch)}&start=0&max_results=5";
        try
        {
            string xml = await httpClient.GetStringAsync(url);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Take(5).ToList();

            Console.WriteLine($"[i] Found {entries.Count} papers:");
            foreach (var entry in entries)
            {
                string title = entry.Element(atom + "title")?.Value?.Trim() ?? "Untitled";
                if (title.Length > 60) title = title[..57] + "...";
                Console.WriteLine($"   - {title}");
            }

            // Extract skill from query pattern
            string skillName = string.Join("", o.Fetch.Split(' ').Select(w =>
                w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";
            var newSkill = new Skill(
                skillName,
                $"Analysis methodology derived from '{o.Fetch}' research",
                new List<string> { "research-context" },
                new List<PlanStep>
                {
                    MakeStep("Gather sources", $"Query: {o.Fetch}", "Relevant papers", 0.9),
                    MakeStep("Extract patterns", "Identify methodology", "Key techniques", 0.85),
                    MakeStep("Synthesize", "Combine insights", "Actionable knowledge", 0.8)
                },
                0.75, 0, DateTime.UtcNow, DateTime.UtcNow
            );
            registry.RegisterSkill(newSkill);
            Console.WriteLine($"\n[OK] New skill extracted: UseSkill_{skillName}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Failed to fetch: {ex.Message}\n");
        }
    }

    if (!string.IsNullOrEmpty(o.Suggest))
    {
        Console.WriteLine($"[?] Skill suggestions for: \"{o.Suggest}\"\n");
        var matches = registry.GetAllSkills()
            .Where(s => s.Name.Contains(o.Suggest, StringComparison.OrdinalIgnoreCase) ||
                        s.Description.Contains(o.Suggest, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.SuccessRate)
            .Take(3)
            .ToList();

        if (matches.Count > 0)
        {
            foreach (var skill in matches)
            {
                Console.WriteLine($"  [>] UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
                Console.WriteLine($"     {skill.Description}\n");
            }
            Console.WriteLine($"  Example: dotnet run -- skills --run \"SetPrompt \\\"{o.Suggest}\\\" | UseSkill_{matches[0].Name} | UseOutput\"\n");
        }
        else
        {
            Console.WriteLine("  No matching skills found. Try --fetch to learn from research.\n");
        }
    }

    if (!string.IsNullOrEmpty(o.Run))
    {
        Console.WriteLine($"[>] Executing: {o.Run}\n");
        var tokens = o.Run.Split('|').Select(t => t.Trim()).ToList();
        foreach (var token in tokens)
        {
            Console.WriteLine($"  [*] {token}");
            if (token.StartsWith("UseSkill_", StringComparison.OrdinalIgnoreCase))
            {
                string skillName = token[9..];
                var skill = registry.GetAllSkills().FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
                if (skill != null)
                {
                    Console.WriteLine($"     [>] Executing: {skill.Name}");
                    foreach (var step in skill.Steps)
                    {
                        Console.WriteLine($"        -> {step.Action}");
                        await Task.Delay(100); // Simulate execution
                    }
                    Console.WriteLine($"     [OK] Complete");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("[OK] Pipeline complete\n");
    }

    // Interactive REPL mode
    if (o.Interactive)
    {
        if (o.Voice)
        {
            await ImmersiveMode.RunImmersiveAsync(o);
        }
        else
        {
            await RunInteractiveSkillsMode(registry, MakeStep);
        }
    }
    }
    finally
    {
        // Clean up registry resources
        if (disposableRegistry != null)
        {
            await disposableRegistry.DisposeAsync();
        }
    }
}

static async Task RunInteractiveSkillsMode(ISkillRegistry registry, Func<string, string, string, double, PlanStep> MakeStep)
{
    Console.WriteLine("\n+------------------------------------------------------------------------+");
    Console.WriteLine("|  [*] INTERACTIVE SKILLS MODE                                           |");
    Console.WriteLine("+------------------------------------------------------------------------+");
    Console.WriteLine("|  Commands:                                                             |");
    Console.WriteLine("|    list / ls          - List all skills                                |");
    Console.WriteLine("|    tokens / t         - Show DSL tokens                                |");
    Console.WriteLine("|    fetch <query>      - Learn skill from arXiv research                |");
    Console.WriteLine("|    suggest <goal>     - Find matching skills                           |");
    Console.WriteLine("|    run <skill>        - Execute a skill (simulated)                    |");
    Console.WriteLine("|    help / ?           - Show this help                                 |");
    Console.WriteLine("|    exit / quit        - Exit interactive mode                          |");
    Console.WriteLine("+------------------------------------------------------------------------+\n");

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    while (true)
    {
        Console.Write("  skills> ");
        string? input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input)) continue;

        string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1] : string.Empty;

        switch (cmd)
        {
            case "exit":
            case "quit":
            case "q":
                Console.WriteLine("\n  [+] Goodbye!\n");
                return;

            case "help":
            case "?":
                Console.WriteLine("\n  Commands: list, tokens, fetch <query>, suggest <goal>, run <skill>, exit\n");
                break;

            case "list":
            case "ls":
                Console.WriteLine("\n  [i] Skills:");
                foreach (var skill in registry.GetAllSkills())
                {
                    Console.WriteLine($"     - {skill.Name,-28} ({skill.SuccessRate:P0}) - {skill.Description}");
                }
                Console.WriteLine();
                break;

            case "tokens":
            case "t":
                Console.WriteLine("\n  [#] DSL Tokens:");
                Console.WriteLine("     Built-in: SetPrompt, UseDraft, UseCritique, UseRevise, UseOutput");
                Console.WriteLine("     Skills:");
                foreach (var skill in registry.GetAllSkills())
                {
                    Console.WriteLine($"       - UseSkill_{skill.Name}");
                }
                Console.WriteLine();
                break;

            case "fetch":
            case "learn":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    Console.WriteLine("  [!] Usage: fetch <query>\n");
                    break;
                }
                Console.WriteLine($"\n  [>] Fetching research on: \"{arg}\"...");
                try
                {
                    string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(arg)}&start=0&max_results=5";
                    string xml = await httpClient.GetStringAsync(url);
                    var doc = System.Xml.Linq.XDocument.Parse(xml);
                    System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
                    var entries = doc.Descendants(atom + "entry").Take(5).ToList();

                    Console.WriteLine($"  [i] Found {entries.Count} papers");

                    string skillName = string.Join("", arg.Split(' ').Select(w =>
                        w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

                    var newSkill = new Skill(
                        skillName,
                        $"Analysis methodology from '{arg}' research",
                        new List<string> { "research-context" },
                        new List<PlanStep>
                        {
                            MakeStep("Gather sources", $"Query: {arg}", "Relevant papers", 0.9),
                            MakeStep("Extract patterns", "Identify methodology", "Key techniques", 0.85),
                            MakeStep("Synthesize", "Combine insights", "Actionable knowledge", 0.8)
                        },
                        0.75, 0, DateTime.UtcNow, DateTime.UtcNow
                    );
                    registry.RegisterSkill(newSkill);
                    Console.WriteLine($"  [OK] New skill: UseSkill_{skillName}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [!] Error: {ex.Message}\n");
                }
                break;

            case "suggest":
            case "find":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    Console.WriteLine("  [!] Usage: suggest <goal>\n");
                    break;
                }
                var matches = registry.GetAllSkills()
                    .Where(s => s.Name.Contains(arg, StringComparison.OrdinalIgnoreCase) ||
                                s.Description.Contains(arg, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.SuccessRate)
                    .Take(3)
                    .ToList();

                if (matches.Count > 0)
                {
                    Console.WriteLine($"\n  [+] Matching skills for \"{arg}\":");
                    foreach (var skill in matches)
                    {
                        Console.WriteLine($"     [>] UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("  No matching skills. Try: fetch <query>\n");
                }
                break;

            case "run":
            case "exec":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    Console.WriteLine("  [!] Usage: run <skill_name>\n");
                    break;
                }
                var skillToRun = registry.GetAllSkills()
                    .FirstOrDefault(s => s.Name.Equals(arg, StringComparison.OrdinalIgnoreCase) ||
                                         s.Name.Equals(arg.Replace("UseSkill_", ""), StringComparison.OrdinalIgnoreCase));
                if (skillToRun != null)
                {
                    Console.WriteLine($"\n  [>] Executing: {skillToRun.Name}");
                    foreach (var step in skillToRun.Steps)
                    {
                        Console.Write($"     -> {step.Action}...");
                        await Task.Delay(200);
                        Console.WriteLine(" [OK]");
                    }
                    Console.WriteLine($"  [OK] {skillToRun.Name} complete\n");
                }
                else
                {
                    Console.WriteLine($"  [!] Skill '{arg}' not found. Use 'list' to see available skills.\n");
                }
                break;

            default:
                Console.WriteLine($"  [!] Unknown command: {cmd}. Type 'help' for commands.\n");
                break;
        }
    }
}

// Global helper to check if voice mode is requested and run it for any command.
// Returns true if voice mode was activated (caller should return), false otherwise.
static async Task<bool> TryRunVoiceModeAsync(IVoiceOptions voiceOptions)
{
    if (!voiceOptions.Voice)
        return false;

    // Use the new fully immersive persona mode
    await ImmersiveMode.RunImmersiveAsync(voiceOptions);
    return true;
}
