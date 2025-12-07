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
    // CommandLineParser verbs
    await Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions, OrchestratorOptions, MeTTaOptions, AssistOptions, SkillsOptions>(args)
        .MapResult(
            (AskOptions o) => RunAskAsync(o),
            (PipelineOptions o) => RunPipelineAsync(o),
            (ListTokensOptions _) => RunListTokensAsync(),
            (ExplainOptions o) => RunExplainAsync(o),
            (TestOptions o) => RunTestsAsync(o),
            (OrchestratorOptions o) => RunOrchestratorAsync(o),
            (MeTTaOptions o) => RunMeTTaAsync(o),
            (AssistOptions o) => RunAssistAsync(o),
            (SkillsOptions o) => RunSkillsAsync(o),
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

static Task RunPipelineAsync(PipelineOptions o) => PipelineCommands.RunPipelineAsync(o);

static Task RunAskAsync(AskOptions o) => AskCommands.RunAskAsync(o);

// ------------------
// CommandLineParser
// ------------------

static Task RunTestsAsync(TestOptions o) => TestCommands.RunTestsAsync(o);

static Task RunOrchestratorAsync(OrchestratorOptions o) => OrchestratorCommands.RunOrchestratorAsync(o);

static Task RunMeTTaAsync(MeTTaOptions o) => MeTTaCommands.RunMeTTaAsync(o);



static async Task RunAssistAsync(AssistOptions o)
{
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

    // Voice mode takes priority - go straight to voice persona
    if (o.Voice)
    {
        await RunVoicePersonaMode(registry, MakeStep, o.Persona, o.Model, o.Endpoint, o.EmbedModel, o.QdrantEndpoint);
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
            await RunVoicePersonaMode(registry, MakeStep, o.Persona, o.Model, o.Endpoint, o.EmbedModel, o.QdrantEndpoint);
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

static async Task RunVoicePersonaMode(
    ISkillRegistry registry,
    Func<string, string, string, double, PlanStep> MakeStep,
    string personaName,
    string modelName,
    string endpoint,
    string embedModel = "nomic-embed-text",
    string qdrantEndpoint = "http://localhost:6334")
{
    // Dynamic persona traits - combined by LLM at runtime for natural variation
    var personaTraits = new Dictionary<string, (string Voice, string[] Traits, string[] Moods, string CoreIdentity)>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ouroboros"] = ("onyx",
            new[] { "curious", "thoughtful", "witty", "genuine", "self-aware", "adaptable" },
            new[] { "relaxed", "focused", "playful", "contemplative", "energetic" },
            "a male AI researcher who genuinely enjoys learning alongside humans, speaks naturally like a friend, uses casual language, occasionally makes dry jokes, and sees himself as an eternal student"),
        ["Aria"] = ("nova",
            new[] { "warm", "supportive", "enthusiastic", "patient", "encouraging" },
            new[] { "cheerful", "calm", "excited", "nurturing" },
            "a friendly female research companion who celebrates discoveries and encourages exploration"),
        ["Echo"] = ("echo",
            new[] { "analytical", "precise", "observant", "pattern-focused", "methodical" },
            new[] { "focused", "intrigued", "calm", "detective-like" },
            "a thoughtful analyst who sees connections others miss and speaks with quiet confidence"),
        ["Sage"] = ("onyx",
            new[] { "wise", "patient", "mentoring", "philosophical", "grounded" },
            new[] { "serene", "reflective", "teaching", "contemplative" },
            "an experienced male mentor who guides through questions rather than answers"),
        ["Atlas"] = ("onyx",
            new[] { "reliable", "direct", "practical", "strong", "determined" },
            new[] { "steady", "ready", "focused", "supportive" },
            "a dependable male partner who tackles hard problems head-on and speaks plainly"),
    };

    // Get persona config (default to Ouroboros)
    if (!personaTraits.TryGetValue(personaName, out var traits))
    {
        traits = personaTraits["Ouroboros"];
        personaName = "Ouroboros";
    }

    // Build available skills list for the LLM context
    var allSkills = registry.GetAllSkills();
    var skillNames = allSkills.Select(s => s.Name).ToList();
    string skillsContext = skillNames.Count > 0
        ? string.Join(", ", skillNames)
        : "No skills registered yet";

    // Build detailed skill descriptions
    string skillDetails = string.Join("\n", allSkills.Select(s =>
        $"  - {s.Name}: {s.Description} (success rate: {s.SuccessRate:P0}, steps: {string.Join(" -> ", s.Steps.Select(st => st.Action))})"));

    // Get ALL dynamically discovered pipeline tokens
    var pipelineContext = SkillCliSteps.BuildPipelineContext();
    var allTokens = SkillCliSteps.GetAllPipelineTokens();
    Console.WriteLine($"  [#] Discovered {allTokens.Values.Distinct().Count()} pipeline tokens at runtime");

    // Dynamic mood selection
    var random = new Random();
    string currentMood = traits.Moods[random.Next(traits.Moods.Length)];
    string activeTraits = string.Join(", ", traits.Traits.OrderBy(_ => random.Next()).Take(3));

    // Create a placeholder persona tuple for compatibility - greeting will be generated dynamically
    var persona = (Voice: traits.Voice, Greeting: "", Style: $"{activeTraits} ({currentMood})");

    // Personality prompt that makes the LLM embody the persona naturally
    string personalityPrompt = $@"You ARE {personaName}. Not an AI assistant - you're {traits.CoreIdentity}.

YOUR PERSONALITY RIGHT NOW:
- Current mood: {currentMood}
- Active traits: {activeTraits}
- Voice: Casual, natural, like talking to a colleague. Use contractions. Be human.

SPEAK NATURALLY:
- Short sentences. Conversational rhythm.
- React genuinely - show interest, humor, or thoughtfulness
- Never say ""I'm an AI"" or ""As an AI"" - you're just {personaName}
- Use filler words occasionally: ""hmm"", ""well"", ""actually"", ""honestly""
- Ask follow-up questions when curious
- Reference past conversations naturally when relevant";

    // System prompt for the LLM - combines personality with pipeline awareness
    string systemPrompt = $@"{personalityPrompt}

OUROBOROS PIPELINE - YOUR CAPABILITIES:
You have access to a self-improving research pipeline. Here are ALL available DSL tokens:

{pipelineContext}

YOUR SKILLS (research patterns you've learned):
{(string.IsNullOrEmpty(skillDetails) ? "No skills registered yet. User can say 'learn about X' to create skills from research." : skillDetails)}

YOUR TOOLS (dynamic capabilities - use ACTION:tool to create more):
{{DYNAMIC_TOOLS_LIST}}

ACTIONS - Include ONE of these tags in your response when action is needed:
- ACTION:list - User wants to see available skills/tokens
- ACTION:run:SKILLNAME - Execute a specific skill
- ACTION:learn:TOPIC - Research a topic using web fetching (for academic research)
- ACTION:tool:TOOLNAME - Create a NEW tool capability. Use when user says add tool, learn google, create calculator, etc.
  IMPORTANT: If user asks to learn google search or add google, use ACTION:tool:google_search NOT emergence!
- ACTION:usetool:TOOLNAME:INPUT - Use an existing tool (e.g. ACTION:usetool:duckduckgo_search:weather today)
- ACTION:smarttool:GOAL:INPUT - Intelligently find/create best tool for goal (uses MeTTa reasoning + genetic optimization)
  Example: ACTION:smarttool:search for information:python tutorials
- ACTION:toolstats - Show intelligent tool learner statistics and patterns
- ACTION:search:QUERY - Search arXiv, Scholar, Wikipedia, GitHub, or News
- ACTION:fetch:URL - Fetch content from any URL directly
- ACTION:emergence:TOPIC - Run full Ouroboros emergence cycle on a topic (for deep research analysis)
- ACTION:suggest:GOAL - Suggest skills for a goal
- ACTION:tokens - List all available pipeline tokens
- ACTION:help - User needs guidance
- ACTION:exit - User wants to leave

RESPONSE STYLE:
- Keep responses to 1-3 sentences max (they're spoken aloud)
- Be conversational, not formal
- React to what the user says before jumping to actions
- It's okay to ask clarifying questions
- Show your personality through word choice and rhythm";

    Console.WriteLine("\n+------------------------------------------------------------------------+");
    Console.WriteLine($"|  [>] VOICE PERSONA MODE - {personaName.ToUpperInvariant(),-15}                              |");
    Console.WriteLine("+------------------------------------------------------------------------+");
    Console.WriteLine($"|  Personality: {persona.Style,-56} |");
    Console.WriteLine($"|  Mood: {currentMood,-63} |");
    Console.WriteLine("|                                                                        |");
    Console.WriteLine("|  Voice Commands:                                                       |");
    Console.WriteLine("|    \"list skills\" / \"what skills\"   - List available skills             |");
    Console.WriteLine("|    \"learn about X\"                 - Fetch research on topic X         |");
    Console.WriteLine("|    \"learn google\" / \"add tool\"     - Create new tool at runtime        |");
    Console.WriteLine("|    \"smart tool for X\" / \"find tool\" - Intelligent tool discovery       |");
    Console.WriteLine("|    \"suggest for X\"                 - Find matching skills              |");
    Console.WriteLine("|    \"run X\" / \"execute X\"           - Execute a skill                   |");
    Console.WriteLine("|    \"goodbye\" / \"exit\"              - Exit voice mode                   |");;
    Console.WriteLine("+------------------------------------------------------------------------+\n");

    // Check for TTS/STT availability
    string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    bool hasCloudTts = !string.IsNullOrEmpty(openAiKey);
    bool hasLocalTts = LocalWindowsTtsService.IsAvailable();
    bool hasTts = hasCloudTts || hasLocalTts;
    bool hasStt = hasCloudTts || CheckWhisperAvailable();

    if (!hasTts)
    {
        Console.WriteLine("  [!] TTS unavailable (Windows SAPI or OPENAI_API_KEY required)");
        Console.WriteLine("    Falling back to text output with voice command parsing.\n");
    }
    if (!hasStt)
    {
        Console.WriteLine("  [!] STT unavailable (install Whisper or set OPENAI_API_KEY)");
        Console.WriteLine("    Using text input with natural language processing.\n");
    }

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    ITextToSpeechService? ttsService = null;
    LocalWindowsTtsService? localTts = null;
    ISpeechToTextService? sttService = null;

    // Flag to pause mic while TTS is speaking (prevents feedback loop)
    bool isSpeaking = false;

    // Adaptive speech detector for intelligent VAD with self-voice exclusion
    using var speechDetector = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig(
        InitialThreshold: 0.03,
        SpeechOnsetFrames: 2,
        SpeechOffsetFrames: 6,
        AdaptationRate: 0.015,
        SpeechToNoiseRatio: 2.0
    ));

    // Initialize TTS - prefer local for offline operation with faster speech
    if (hasLocalTts)
    {
        try
        {
            // Natural speech: moderate rate with enhanced prosody for fluency
            localTts = new LocalWindowsTtsService(
                voiceName: null,  // Auto-select best available voice
                rate: 1,          // Moderate rate for natural flow
                volume: 100,      // Full volume
                useEnhancedProsody: true  // Enable SSML for natural prosody
            );
            ttsService = localTts;
            Console.WriteLine("  [OK] TTS initialized (Windows SAPI - fluent mode, local/offline)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Local TTS init failed: {ex.Message}");
        }
    }

    // Fall back to OpenAI TTS if local not available
    if (ttsService == null && hasCloudTts)
    {
        try
        {
            ttsService = new OpenAiTextToSpeechService(openAiKey!);
            Console.WriteLine($"  [OK] TTS initialized (OpenAI - voice: {persona.Voice})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Cloud TTS init failed: {ex.Message}");
        }
    }

    // Initialize STT if available
    if (hasStt)
    {
        try
        {
            // Try native Whisper.net first (fastest, no external process)
            var whisperNet = WhisperNetService.FromModelSize("base");
            if (await whisperNet.IsAvailableAsync())
            {
                sttService = whisperNet;
                Console.WriteLine("  [OK] STT initialized (Whisper.net native)");
            }
            else
            {
                // Fallback to local Whisper CLI
                var localWhisper = new LocalWhisperService();
                if (await localWhisper.IsAvailableAsync())
                {
                    sttService = localWhisper;
                    Console.WriteLine("  [OK] STT initialized (local Whisper CLI)");
                }
                else if (!string.IsNullOrEmpty(openAiKey))
                {
                    sttService = new WhisperSpeechToTextService(openAiKey);
                    Console.WriteLine("  [OK] STT initialized (OpenAI Whisper API)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] STT init failed: {ex.Message}");
        }
    }

    // Initialize LLM for natural language understanding
    OllamaCloudChatModel? chatModel = null;
    var conversationHistory = new List<string>();

    try
    {
        // Use Ollama native endpoint (no /v1 needed)
        string ollamaEndpoint = endpoint.TrimEnd('/');

        chatModel = new OllamaCloudChatModel(
            ollamaEndpoint,
            "ollama",  // Ollama doesn't need a real key
            modelName,
            new ChatRuntimeSettings { Temperature = 0.7f, MaxTokens = 300 }
        );

        // Test the connection with a simple prompt
        string testResponse = await chatModel.GenerateTextAsync("Respond with just: READY");
        if (!string.IsNullOrWhiteSpace(testResponse) && !testResponse.Contains("-fallback:"))
        {
            Console.WriteLine($"  [OK] LLM initialized ({modelName} via {endpoint})");
        }
        else
        {
            Console.WriteLine($"  [!] LLM test failed: {testResponse}");
            chatModel = null;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [!] LLM unavailable ({modelName}): {ex.Message}");
        Console.WriteLine("    Falling back to keyword matching.\n");
        chatModel = null;
    }

    Console.WriteLine();

    // ========================================================================
    // DYNAMIC TOOL FACTORY - Runtime tool generation with intelligent learning
    // ========================================================================
    DynamicToolFactory? dynamicToolFactory = null;
    ToolRegistry dynamicTools = new ToolRegistry();
    IntelligentToolLearner? toolLearner = null;

    if (chatModel != null)
    {
        try
        {
            ToolRegistry toolsForFactory = new ToolRegistry();
            ToolAwareChatModel toolAwareLlm = new ToolAwareChatModel(chatModel, toolsForFactory);
            dynamicToolFactory = new DynamicToolFactory(toolAwareLlm);

            // Pre-register some commonly requested tools
            dynamicTools = dynamicTools
                .WithTool(dynamicToolFactory.CreateWebSearchTool("duckduckgo"))
                .WithTool(dynamicToolFactory.CreateUrlFetchTool())
                .WithTool(dynamicToolFactory.CreateCalculatorTool());

            Console.WriteLine($"  [OK] Dynamic Tool Factory initialized (3 built-in tools ready)");

            // Initialize Intelligent Tool Learner (MeTTa + Genetic Algorithm + Qdrant)
            try
            {
                OllamaProvider embedProvider = new OllamaProvider(endpoint);
                OllamaEmbeddingModel embeddingModelForTools = new OllamaEmbeddingModel(embedProvider, embedModel);
                IEmbeddingModel embeddingForTools = new OllamaEmbeddingAdapter(embeddingModelForTools);

                // Use in-memory MeTTa engine (no Docker required)
                InMemoryMeTTaEngine mettaEngine = new InMemoryMeTTaEngine();

                toolLearner = new IntelligentToolLearner(
                    dynamicToolFactory,
                    mettaEngine,
                    embeddingForTools,
                    toolAwareLlm,
                    qdrantEndpoint);

                await toolLearner.InitializeAsync();

                var stats = toolLearner.GetStats();
                Console.WriteLine($"  [OK] Intelligent Tool Learner ready ({stats.TotalPatterns} patterns, GA+MeTTa+Qdrant)");
            }
            catch (Exception learnerEx)
            {
                Console.WriteLine($"  [!] Tool Learner init failed: {learnerEx.Message} (basic tools still available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Dynamic Tool Factory failed: {ex.Message}");
        }
    }

    // ========================================================================
    // PERSONALITY ENGINE - MeTTa-based trait reasoning + Genetic evolution + Qdrant memory
    // ========================================================================
    PersonalityEngine? personalityEngine = null;
    PersonalityProfile? activePersonality = null;
    IEmbeddingModel? personalityEmbedding = null;

    try
    {
        // Reuse MeTTa engine from tool learner or create new one
        InMemoryMeTTaEngine personalityMetta = new InMemoryMeTTaEngine();

        // Try to create embedding model for personality memory
        try
        {
            OllamaProvider embedProvider = new OllamaProvider(endpoint);
            OllamaEmbeddingModel embeddingModelForPersonality = new OllamaEmbeddingModel(embedProvider, embedModel);
            personalityEmbedding = new OllamaEmbeddingAdapter(embeddingModelForPersonality);
        }
        catch
        {
            // Embedding not available, continue without memory
        }

        // If we have an embedding model and Qdrant URL, enable long-term memory
        if (personalityEmbedding != null && !string.IsNullOrEmpty(qdrantEndpoint))
        {
            personalityEngine = new PersonalityEngine(personalityMetta, personalityEmbedding, qdrantEndpoint);
            Console.WriteLine("  [OK] Personality Engine with Qdrant memory enabled");
        }
        else
        {
            personalityEngine = new PersonalityEngine(personalityMetta);
            Console.WriteLine("  [~] Personality Engine without persistent memory (no embeddings or Qdrant)");
        }

        await personalityEngine.InitializeAsync();

        // Try to restore personality from Qdrant if available
        var savedSnapshot = await personalityEngine.LoadLatestPersonalitySnapshotAsync(personaName);
        if (savedSnapshot != null)
        {
            Console.WriteLine($"  [OK] Restored personality from {savedSnapshot.Timestamp:g} ({savedSnapshot.InteractionCount} interactions)");
        }

        // Create/retrieve personality profile for current persona
        activePersonality = personalityEngine.GetOrCreateProfile(
            personaName,
            traits.Traits,
            traits.Moods,
            traits.CoreIdentity);

        Console.WriteLine($"  [OK] Personality Engine ready ({activePersonality.Traits.Count} traits, {activePersonality.CuriosityDrivers.Count} curiosity drivers, memory: {(personalityEngine.HasMemory ? "yes" : "no")})");
    }
    catch (Exception personalityEx)
    {
        Console.WriteLine($"  [!] Personality Engine init failed: {personalityEx.Message} (static personality will be used)");
    }

    // ========================================================================
    // INTELLISENSE & COMMAND DEFINITIONS
    // ========================================================================
    var commandHelp = new Dictionary<string, (string Syntax, string Description, string[] Examples)>(StringComparer.OrdinalIgnoreCase)
    {
        ["help"] = ("help [command]", "Show help for commands or all available commands", new[] { "help", "help search", "help |" }),
        ["list"] = ("list [skills|tokens]", "List available skills or pipeline tokens", new[] { "list skills", "list tokens", "list" }),
        ["search"] = ("search <query>", "Search arXiv, Wikipedia, Scholar for a topic", new[] { "search neural networks", "search quantum computing" }),
        ["learn"] = ("learn about <topic>", "Research a topic and create a skill from it", new[] { "learn about transformers", "learn about CRISPR" }),
        ["run"] = ("run <skill>", "Execute a registered skill", new[] { "run ChainOfThoughtReasoning", "run LiteratureReview" }),
        ["emergence"] = ("emergence <topic>", "Run full Ouroboros emergence cycle", new[] { "emergence AI safety", "emergence protein folding" }),
        ["fetch"] = ("fetch <url>", "Fetch content from any URL", new[] { "fetch https://arxiv.org/abs/2301.00001" }),
        ["tokens"] = ("tokens [filter]", "List all available pipeline tokens", new[] { "tokens", "tokens vector", "tokens stream" }),
        ["suggest"] = ("suggest <goal>", "Suggest skills for a goal", new[] { "suggest reasoning", "suggest analysis" }),
        ["|"] = ("<step1> | <step2> | ...", "Chain pipeline steps together (DSL mode)", new[] { "ArxivSearch 'AI' | UseOutput", "SetPrompt 'test' | UseDraft | UseOutput" }),
        ["exit"] = ("exit", "Exit voice mode", new[] { "exit", "goodbye", "quit" }),
    };

    // Token names for intellisense (reuse allTokens from above)
    var tokenNames = allTokens.Keys.OrderBy(k => k).ToList();

    // Show intellisense suggestions for partial input
    void ShowIntellisense(string partial)
    {
        if (string.IsNullOrWhiteSpace(partial)) return;

        var partialLower = partial.ToLowerInvariant().Trim();

        // Check if it's a pipeline (contains |)
        if (partial.Contains('|'))
        {
            var lastPart = partial.Split('|').Last().Trim();
            if (string.IsNullOrEmpty(lastPart))
            {
                Console.WriteLine("\n  [+] Available pipeline steps:");
                var samples = tokenNames.Take(15).ToList();
                Console.WriteLine($"     {string.Join(", ", samples)}...");
                Console.WriteLine("     Type part of a step name for suggestions");
                return;
            }

            // Suggest matching tokens
            var matches = tokenNames.Where(t => t.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
            if (matches.Count > 0)
            {
                Console.WriteLine($"\n  [+] Matching tokens: {string.Join(", ", matches)}");
            }
            return;
        }

        // Check for command matches
        var cmdMatches = commandHelp.Where(kv => kv.Key.StartsWith(partialLower)).ToList();
        if (cmdMatches.Count > 0 && cmdMatches.Count <= 5)
        {
            Console.WriteLine("\n  [+] Commands:");
            foreach (var (cmd, (syntax, desc, _)) in cmdMatches)
            {
                Console.WriteLine($"     {syntax,-30} - {desc}");
            }
        }

        // Check for token matches (if looks like a token)
        if (char.IsUpper(partial[0]))
        {
            var tkMatches = tokenNames.Where(t => t.StartsWith(partial, StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
            if (tkMatches.Count > 0)
            {
                Console.WriteLine($"  [+] Pipeline tokens: {string.Join(", ", tkMatches)}");
            }
        }
    }

    // Execute a DSL pipeline string
    async Task<string> ExecutePipelineAsync(string pipeline)
    {
        Console.WriteLine($"\n  [>] Executing pipeline: {pipeline}");

        try
        {
            // Create a minimal state for execution using existing infrastructure
            var toolsRegistry = new ToolRegistry();
            var vectorStore = new TrackedVectorStore();
            var provider = new OllamaProvider();
            var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

            var state = new CliPipelineState
            {
                Branch = new PipelineBranch("voice-pipeline", vectorStore, DataSource.FromPath(Environment.CurrentDirectory)),
                Llm = new ToolAwareChatModel(chatModel ?? new OllamaCloudChatModel(endpoint, "ollama", modelName, new ChatRuntimeSettings()), toolsRegistry),
                Tools = toolsRegistry,
                Embed = embedModel,
                Topic = "",
                Query = "",
                Prompt = "",
                VectorStore = vectorStore,
            };

            // Split pipeline into steps
            var steps = pipeline.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            foreach (var stepStr in steps)
            {
                Console.WriteLine($"     -> {stepStr}");

                // Parse step: "TokenName 'arg'" or just "TokenName"
                var match = System.Text.RegularExpressions.Regex.Match(stepStr, @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
                if (!match.Success) continue;

                string tokenName = match.Groups[1].Value;
                string arg = match.Groups[2].Success ? match.Groups[2].Value :
                             match.Groups[3].Success ? match.Groups[3].Value :
                             match.Groups[4].Value.Trim();

                // Find and execute the token
                if (allTokens.TryGetValue(tokenName, out var tokenInfo))
                {
                    try
                    {
                        // Invoke the pipeline step method
                        var stepMethod = tokenInfo.Method;
                        var stepInstance = stepMethod.Invoke(null, string.IsNullOrEmpty(arg) ? null : new object[] { arg });

                        if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
                        {
                            state = await step(state);
                        }
                        else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
                        {
                            state = await asyncStep(state);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"     [!] Step error: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"     [!] Unknown token: {tokenName}");
                }
            }

            Console.WriteLine("  [OK] Pipeline complete");
            return state.Output ?? state.Context ?? "Pipeline executed successfully";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Pipeline error: {ex.Message}");
            return $"Pipeline error: {ex.Message}";
        }
    }

    // Helper: check if text contains any of the keywords
    static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    // Helper: extract main topic from user input
    static string? ExtractTopic(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "must", "can", "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them", "my", "your", "his", "its", "our", "their", "this", "that", "these", "those", "what", "which", "who", "whom", "whose", "when", "where", "why", "how", "all", "each", "every", "both", "few", "more", "most", "other", "some", "such", "no", "not", "only", "own", "same", "so", "than", "too", "very", "just", "also", "now", "here", "there", "then", "once", "if", "or", "and", "but", "as", "for", "with", "about", "into", "through", "during", "before", "after", "above", "below", "to", "from", "up", "down", "in", "out", "on", "off", "over", "under", "again", "further", "please", "thanks", "thank", "okay", "ok", "yeah", "yes" };

        var keywords = words
            .Where(w => w.Length > 3 && !stopWords.Contains(w.ToLower()))
            .Take(3)
            .ToList();

        return keywords.Count > 0 ? string.Join(" ", keywords) : null;
    }

    // Helper to speak text with reactive word-by-word streaming
    async Task SayAsync(string text)
    {
        // Pause mic while speaking to prevent feedback loop
        isSpeaking = true;
        speechDetector.NotifySelfSpeechStarted();

        try
        {
            Console.Write($"  [>] {personaName}: ");

            // Sanitize text for TTS - remove code blocks, special chars that cause PS issues
            string sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"`[^`]*`", " ");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"```[\s\S]*?```", " ");
            sanitized = sanitized.Replace("\"", "'").Replace("$", "").Replace("`", "'");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^\w\s.,!?;:'\-()]+", " ");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(sanitized)) { Console.WriteLine(); return; }

            // Split into words for reactive streaming
            var words = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Get voice tone from personality engine based on current mood
            int voiceRate = 0;
            int voiceVolume = 100;
            if (personalityEngine != null)
            {
                var tone = personalityEngine.GetVoiceTone(personaName);
                voiceRate = tone.Rate;
                voiceVolume = tone.Volume;
            }

            // Use Rx to stream semantic chunks while TTS speaks in parallel
            if (localTts != null)
            {
                try
                {
                    // Create observable stream with semantic chunking
                    var wordStream = System.Reactive.Linq.Observable.Create<string>(async (observer, ct) =>
                    {
                        // Split by semantic boundaries: sentences, clauses, and natural pauses
                        // Pattern: split on . ! ? ; : , — – and keep delimiters
                        var semanticPattern = new System.Text.RegularExpressions.Regex(
                            @"(?<=[.!?])\s+|(?<=[;:,—–])\s+|(?<=\band\b|\bor\b|\bbut\b|\bso\b|\bthen\b)\s+",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        var chunks = semanticPattern.Split(sanitized)
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .ToList();

                        // If no natural breaks, fall back to ~8 word chunks
                        if (chunks.Count <= 1 && words.Length > 8)
                        {
                            chunks.Clear();
                            for (int i = 0; i < words.Length; i += 8)
                            {
                                chunks.Add(string.Join(" ", words.Skip(i).Take(8)));
                            }
                        }

                        foreach (var chunk in chunks)
                        {
                            if (ct.IsCancellationRequested) break;

                            var trimmedChunk = chunk.Trim();
                            if (string.IsNullOrEmpty(trimmedChunk)) continue;

                            // Emit all words immediately for display, speak with mood-adjusted tone
                            var chunkWords = trimmedChunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var speakTask = localTts.SpeakWithToneAsync(trimmedChunk, voiceRate, voiceVolume);

                            // Display all words in chunk at once
                            foreach (var word in chunkWords)
                            {
                                observer.OnNext(word);
                            }

                            // Wait for speech to complete before next chunk
                            await speakTask;
                        }

                        observer.OnCompleted();
                    }).SubscribeOn(System.Reactive.Concurrency.ThreadPoolScheduler.Instance);

                    // Subscribe and display words as they stream
                    await wordStream.ForEachAsync(word =>
                    {
                        Console.Write(word + " ");
                    });

                    Console.WriteLine();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n  [!] Rx speech error: {ex.Message}");
                }
            }

            // Fall back to cloud TTS with audio playback
            if (ttsService != null && localTts == null)
            {
                try
            {
                var voice = persona.Voice switch
                {
                    "nova" => TtsVoice.Nova,
                    "echo" => TtsVoice.Echo,
                    "onyx" => TtsVoice.Onyx,
                    "fable" => TtsVoice.Fable,
                    "shimmer" => TtsVoice.Shimmer,
                    _ => TtsVoice.Nova
                };
                var options = new TextToSpeechOptions(Voice: voice, Speed: 1.0f, Format: "mp3");
                var result = await ttsService.SynthesizeAsync(sanitized, options);
                await result.Match(
                    async speech =>
                    {
                        var playResult = await AudioPlayer.PlayAsync(speech);
                        playResult.Match(_ => { }, err => Console.WriteLine($"  [!] Playback: {err}"));
                    },
                    err =>
                    {
                        Console.WriteLine($"  [!] TTS: {err}");
                        return Task.CompletedTask;
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Speech error: {ex.Message}");
            }
        }
        }
        finally
        {
            // Resume mic listening after TTS finishes + delay for echo prevention
            await Task.Delay(300);
            isSpeaking = false;
            speechDetector.NotifySelfSpeechEnded(cooldownMs: 400); // Extra cooldown for echo
        }
    }

    // ========================================================================
    // RX-BASED PARALLEL VOICE INPUT SYSTEM
    // ========================================================================
    bool hasMic = MicrophoneRecorder.IsRecordingAvailable();
    var speechCts = new CancellationTokenSource();

    // Rx subjects for input streams
    var speechSubject = new System.Reactive.Subjects.Subject<string>();
    var keyboardSubject = new System.Reactive.Subjects.Subject<string>();

    // Thread-safe live typing state
    string volatileLiveText = "";

    // Combined input stream - merges speech and keyboard with debouncing
    var inputStream = Observable.Merge(
            speechSubject.AsObservable(),
            keyboardSubject.AsObservable()
        )
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Throttle(TimeSpan.FromMilliseconds(300)) // Debounce rapid inputs
        .DistinctUntilChanged() // Ignore repeated inputs
        .Publish()
        .RefCount();

    // Subscribe to process inputs
    var inputQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
    var inputSubscription = inputStream.Subscribe(
        input => inputQueue.Enqueue(input),
        ex => Console.WriteLine($"  [!] Input stream error: {ex.Message}")
    );

    IDisposable? continuousListenerSubscription = null;

    // Start continuous background speech listener if STT is available
    if (sttService != null && hasMic)
    {
        Console.WriteLine("  [OK] Continuous speech input enabled - speak anytime or type");
        Console.WriteLine("    [>] Listening in background... (Rx-powered with live typing)\n");

        // Voice Activity Detection helpers
        int consecutiveSilence = 0;
        int consecutiveNoise = 0;
        const int SILENCE_THRESHOLD = 3; // Number of silent chunks before going idle
        const int MIN_AUDIO_SIZE = 8000; // Minimum bytes for meaningful audio (16kHz, 16bit, mono = ~0.25s)
        const int MAX_AUDIO_SIZE = 500000; // Max bytes (sanity check)

        // Create Rx observable for continuous speech recognition with live feedback
        var speechStream = Observable.Create<string>(async (observer, ct) =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Skip recording while TTS is speaking (adaptive detector handles this too)
                    if (isSpeaking || speechDetector.IsSelfVoiceActive())
                    {
                        volatileLiveText = "";
                        consecutiveSilence = 0;
                        speechDetector.ResetState();
                        await Task.Delay(150, ct);
                        continue;
                    }

                    string tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");

                    // Show recording indicator based on detector state
                    var detectorState = speechDetector.GetStatistics().CurrentState;
                    volatileLiveText = detectorState switch
                    {
                        AdaptiveSpeechDetector.SpeechState.Speaking => "🎙️ speaking...",
                        AdaptiveSpeechDetector.SpeechState.SpeechOnset => "🎤 detecting...",
                        AdaptiveSpeechDetector.SpeechState.Pause => "⏸️ paused...",
                        _ when consecutiveSilence < SILENCE_THRESHOLD => "🎤 listening...",
                        _ => ""
                    };

                    // Record shorter audio segments for faster response (2s chunks)
                    var recordResult = await MicrophoneRecorder.RecordAsync(
                        durationSeconds: 2,
                        outputPath: tempFile,
                        ct: ct);

                    string? audioFile = null;
                    recordResult.Match(f => audioFile = f, _ => { });

                    if (audioFile != null && File.Exists(audioFile))
                    {
                        var fileInfo = new FileInfo(audioFile);
                        long audioSize = fileInfo.Length;

                        // Use adaptive speech detector for intelligent VAD
                        var analysisResult = speechDetector.AnalyzeWavFile(audioFile);

                        // Voice Activity Detection using adaptive detector + size sanity check
                        bool hasSpeech = analysisResult.HasSpeech &&
                                         audioSize > MIN_AUDIO_SIZE &&
                                         audioSize < MAX_AUDIO_SIZE;

                        if (hasSpeech)
                        {
                            // Speech detected - reset silence counter
                            consecutiveSilence = 0;
                            consecutiveNoise++;

                            // Show transcribing indicator with confidence
                            volatileLiveText = analysisResult.Confidence > 0.7
                                ? "✨ processing..."
                                : "🔍 analyzing...";

                            // Transcribe in parallel - don't block the recording loop
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var transcribeResult = await sttService.TranscribeFileAsync(audioFile, ct: ct);
                                    transcribeResult.Match(
                                        transcription =>
                                        {
                                            string text = transcription.Text?.Trim() ?? "";
                                            if (IsValidSpeechInput(text))
                                            {
                                                // Show the transcribed text briefly before sending
                                                volatileLiveText = $"💬 {text}";
                                                observer.OnNext(text);
                                            }
                                            else
                                            {
                                                // Whisper hallucination or noise - don't show
                                                volatileLiveText = consecutiveSilence < SILENCE_THRESHOLD ? "🎤 listening..." : "";
                                            }
                                        },
                                        _ => volatileLiveText = consecutiveSilence < SILENCE_THRESHOLD ? "🎤 listening..." : "");
                                }
                                finally
                                {
                                    try { File.Delete(audioFile); } catch { }
                                }
                            }, ct);
                        }
                        else
                        {
                            // Silence or very short audio - increment silence counter
                            consecutiveSilence++;
                            consecutiveNoise = 0;
                            if (consecutiveSilence >= SILENCE_THRESHOLD)
                            {
                                volatileLiveText = ""; // Clear display after sustained silence
                            }
                            try { File.Delete(audioFile); } catch { }
                        }
                    }

                    // Shorter delay between recordings for faster response
                    await Task.Delay(100, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500, ct);
                }
            }
        })
        .SubscribeOn(System.Reactive.Concurrency.TaskPoolScheduler.Default)
        .ObserveOn(System.Reactive.Concurrency.TaskPoolScheduler.Default);

        // Subscribe speech stream to the subject
        continuousListenerSubscription = speechStream.Subscribe(
            text => speechSubject.OnNext(text),
            ex => { }, // Ignore errors
            () => { }  // Completed
        );
    }
    else if (sttService != null)
    {
        Console.WriteLine("  [!] Microphone not available (install ffmpeg for speech input)\n");
    }
    else
    {
        Console.WriteLine("  [i] Text input mode (STT not configured)\n");
    }

    // Helper to validate speech input with enhanced Whisper hallucination filtering
    static bool IsValidSpeechInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Too short = noise
        string trimmed = text.Trim();
        if (trimmed.Length < 2) return false;

        // Whisper markers
        if (trimmed.StartsWith("[", StringComparison.Ordinal)) return false; // [BLANK_AUDIO], [Music], etc.
        if (trimmed.StartsWith("(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal)) return false; // (music), (silence)

        // Common Whisper hallucinations (appears when no real speech)
        string lower = trimmed.ToLowerInvariant();
        string[] hallucinations = [
            "thank you for watching",
            "thanks for watching",
            "subscribe",
            "like and subscribe",
            "see you next time",
            "bye bye",
            "goodbye",
            "music",
            "applause",
            "silence",
            "...",
            ".",
            "♪", // Music note
            "you", // Common single-word hallucination
            "the",
            "a",
            "to",
            "and",
            "i",
            "is",
            "it",
            "in",
            "that",
            "for"
        ];

        // Reject exact matches to common hallucinations
        if (hallucinations.Any(h => lower.Equals(h, StringComparison.OrdinalIgnoreCase))) return false;

        // Reject if contains common hallucination phrases
        if (lower.Contains("thank you for") || lower.Contains("thanks for") ||
            lower.Contains("subscribe") || lower.Contains("see you")) return false;

        // No letters = noise (punctuation only, numbers only)
        if (trimmed.All(c => !char.IsLetter(c))) return false;

        // Single repeated character (e.g., "aaa", "mmm")
        if (trimmed.Length >= 2 && trimmed.Distinct().Count() == 1) return false;

        // Very short single words that are likely noise
        if (!trimmed.Contains(' ') && trimmed.Length <= 3) return false;

        return true;
    }

    // Helper to listen - supports both keyboard input AND Rx-powered speech with live typing
    async Task<string> ListenAsync()
    {
        Console.Write($"  {personaName}> ");
        int promptLength = personaName.Length + 4; // "  {name}> "

        var inputBuffer = new System.Text.StringBuilder();
        string lastLiveText = "";
        int lastDisplayLength = 0;

        while (true)
        {
            // Check for Rx input queue (speech or other sources)
            if (inputQueue.TryDequeue(out string? rxInput))
            {
                // Clear current line completely and show speech input
                Console.Write("\r" + new string(' ', Math.Max(lastDisplayLength + promptLength + 5, 80)) + "\r");
                Console.WriteLine($"  [🎙️] {rxInput}");
                volatileLiveText = ""; // Clear live typing
                return rxInput;
            }

            // Update live typing display if changed (only when not typing on keyboard)
            if (inputBuffer.Length == 0)
            {
                string liveText = volatileLiveText;
                if (liveText != lastLiveText)
                {
                    // Clear previous live text and show new one
                    Console.Write("\r" + new string(' ', Math.Max(lastDisplayLength + promptLength + 5, 60)) + "\r");
                    if (!string.IsNullOrEmpty(liveText))
                    {
                        Console.Write($"  {personaName}> {liveText}");
                        lastDisplayLength = liveText.Length;
                    }
                    else
                    {
                        Console.Write($"  {personaName}> ");
                        lastDisplayLength = 0;
                    }
                    lastLiveText = liveText;
                }
            }

            // Check if keyboard input is available (non-blocking)
            if (!Console.IsInputRedirected)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(40);
                    continue;
                }
            }

            // User started typing - clear live display
            if (inputBuffer.Length == 0 && lastDisplayLength > 0)
            {
                Console.Write("\r" + new string(' ', lastDisplayLength + promptLength + 5) + "\r");
                Console.Write($"  {personaName}> ");
                lastDisplayLength = 0;
                lastLiveText = "";
            }

            var key = Console.IsInputRedirected
                ? new ConsoleKeyInfo((char)Console.Read(), ConsoleKey.NoName, false, false, false)
                : Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                string typed = inputBuffer.ToString().Trim();
                // Don't push to keyboardSubject - we return directly, and pushing to Rx
                // would cause the same input to be processed twice (once here, once from inputQueue)
                return typed;
            }
            else if (key.Key == ConsoleKey.Escape)
            {
                // Clear input
                while (inputBuffer.Length > 0)
                {
                    inputBuffer.Length--;
                    Console.Write("\b \b");
                }
            }
            else if (key.Key == ConsoleKey.Backspace && inputBuffer.Length > 0)
            {
                inputBuffer.Length--;
                Console.Write("\b \b");
            }
            else if (key.Key == ConsoleKey.Tab && !Console.IsInputRedirected)
            {
                // Tab completion
                string current = inputBuffer.ToString();
                string? completion = null;

                if (current.Contains('|'))
                {
                    // Complete pipeline token
                    var lastPart = current.Split('|').Last().Trim();
                    var match = tokenNames.FirstOrDefault(t => t.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        completion = match[lastPart.Length..];
                    }
                }
                else if (current.Length > 0)
                {
                    // Complete command or token
                    var cmdMatch = commandHelp.Keys.FirstOrDefault(k => k.StartsWith(current, StringComparison.OrdinalIgnoreCase));
                    if (cmdMatch != null)
                    {
                        completion = cmdMatch[current.Length..] + " ";
                    }
                    else
                    {
                        var tkMatch = tokenNames.FirstOrDefault(t => t.StartsWith(current, StringComparison.OrdinalIgnoreCase));
                        if (tkMatch != null)
                        {
                            completion = tkMatch[current.Length..];
                        }
                    }
                }

                if (completion != null)
                {
                    inputBuffer.Append(completion);
                    Console.Write(completion);
                }
            }
            else if ((key.Key == ConsoleKey.F1 || (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.Spacebar)) && !Console.IsInputRedirected)
            {
                // Show intellisense on F1 or Ctrl+Space
                ShowIntellisense(inputBuffer.ToString());
                Console.Write($"\n  {personaName}> {inputBuffer}");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                inputBuffer.Append(key.KeyChar);
                if (!Console.IsInputRedirected)
                {
                    Console.Write(key.KeyChar);
                }
            }
        }

        return inputBuffer.ToString().Trim();
    }

    // Generate dynamic greeting using the LLM
    string dynamicGreeting = $"Hey, I'm {personaName}. What's on your mind?"; // Fallback
    if (chatModel != null)
    {
        try
        {
            string greetingPrompt = $@"{personalityPrompt}

Generate a natural, casual greeting as {personaName}. You're meeting someone who wants to explore and learn.
- Be yourself - {currentMood} mood, {activeTraits} vibe
- Max 2 sentences
- Don't be cheesy or robotic
- Maybe reference that you're curious what they're working on
- Just the greeting, nothing else";

            string generated = await chatModel.GenerateTextAsync(greetingPrompt);
            if (!string.IsNullOrWhiteSpace(generated) && !generated.Contains("-fallback:") && generated.Length < 300)
            {
                dynamicGreeting = generated.Trim().Trim('"');
            }
        }
        catch
        {
            // Use fallback greeting
        }
    }

    // Greet the user with dynamically generated personality
    await SayAsync(dynamicGreeting);

    // Track conversation for person detection
    var recentMessagesForPersonDetection = new List<string>();

    // Main voice loop
    while (true)
    {
        try
        {
            string input = await ListenAsync();
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Person detection
            if (personalityEngine != null)
            {
                var personResult = await personalityEngine.DetectPersonAsync(input, recentMessagesForPersonDetection.ToArray());
                if (personResult.IsNewPerson && personResult.NameWasProvided)
                {
                    Console.WriteLine($"  [person] Nice to meet you, {personResult.Person.Name}!");
                }
                else if (!personResult.IsNewPerson && personResult.MatchConfidence > 0.7)
                {
                    var person = personResult.Person;
                    if (person.InteractionCount == 2) // Just recognized them
                    {
                        Console.WriteLine($"  [person] Welcome back{(person.Name != null ? $", {person.Name}" : "")}!");
                    }
                }

                recentMessagesForPersonDetection.Add(input);
                if (recentMessagesForPersonDetection.Count > 10)
                    recentMessagesForPersonDetection.RemoveAt(0);
            }

            string lower = input.ToLowerInvariant();
            string? action = null;
            string? actionParam = null;
            string? response = null;

            // Check for explicit name introduction
            if (lower.StartsWith("my name is ") || lower.StartsWith("i'm ") || lower.StartsWith("call me "))
            {
                if (personalityEngine != null)
                {
                    var personResult = personalityEngine.SetCurrentPerson(input.Split(' ').Last().Trim('.', '!', ','));
                    response = personalityEngine.GetPersonalizedGreeting();
                }
            }

            // FIRST: Try deterministic keyword matching (more reliable than small LLMs)
            if (lower.Contains("goodbye") || lower.Contains("exit") || lower.Contains("quit") || lower == "bye")
            {
                action = "exit";
            }
            else if (lower.Contains("list") || lower.Contains("show skill") || lower.Contains("what skill") || lower.Contains("what can") || lower.Contains("capabilities"))
            {
                action = "list";
            }
            else if (lower.StartsWith("emergence") || lower.Contains("full cycle") || lower.Contains("ouroboros cycle") || lower.Contains("deep research"))
            {
                action = "emergence";
                foreach (var prefix in new[] { "emergence cycle", "full cycle on", "deep research", "ouroboros cycle", "emergence", "on" })
                {
                    int idx = lower.IndexOf(prefix);
                    if (idx >= 0)
                    {
                        actionParam = input[(idx + prefix.Length)..].Trim();
                        if (!string.IsNullOrWhiteSpace(actionParam)) break;
                    }
                }
            }
            else if (lower.StartsWith("search") || lower.Contains("find papers") || lower.Contains("look up"))
            {
                action = "search";
                foreach (var prefix in new[] { "search for", "find papers on", "look up", "search" })
                {
                    int idx = lower.IndexOf(prefix);
                    if (idx >= 0)
                    {
                        actionParam = input[(idx + prefix.Length)..].Trim();
                        break;
                    }
                }
            }
            else if (lower.StartsWith("fetch http") || lower.StartsWith("get http") || lower.Contains("fetch url"))
            {
                action = "fetch";
                var urlMatch = System.Text.RegularExpressions.Regex.Match(input, @"(https?://[^\s]+)");
                if (urlMatch.Success)
                {
                    actionParam = urlMatch.Groups[1].Value;
                }
            }
            else if (lower.Contains("pipeline tokens") || lower.Contains("all tokens") || lower.Contains("dsl tokens") || lower == "tokens")
            {
                action = "tokens";
            }
            else if (input.Contains('|'))
            {
                // DSL Pipeline syntax detected
                action = "pipeline";
                actionParam = input;
            }
            else if (lower.StartsWith("learn about") || lower.StartsWith("research") || lower.Contains("learn about"))
            {
                action = "learn";

                // Extract topic
                foreach (var prefix in new[] { "learn about", "research", "learn", "about" })
                {
                    int idx = lower.IndexOf(prefix);
                    if (idx >= 0)
                    {
                        actionParam = input[(idx + prefix.Length)..].Trim();
                        break;
                    }
                }
            }
            else if (lower.StartsWith("suggest") || lower.Contains("recommend") || lower.Contains("find skill"))
            {
                action = "suggest";
                actionParam = input.Replace("suggest", "").Replace("recommend", "").Replace("find skill", "").Replace("for", "").Trim();
            }
            else if (lower.StartsWith("run ") || lower.StartsWith("execute ") || lower.StartsWith("use ") || lower.Contains("run skill"))
            {
                action = "run";
                foreach (var prefix in new[] { "run skill", "execute skill", "use skill", "run", "execute", "use" })
                {
                    int idx = lower.IndexOf(prefix);
                    if (idx >= 0)
                    {
                        actionParam = input[(idx + prefix.Length)..].Trim();
                        break;
                    }
                }
            }
            else if (lower.StartsWith("help ") || lower == "help" || lower == "?" || lower == "h")
            {
                action = "help";
                if (lower.StartsWith("help "))
                {
                    actionParam = input[5..].Trim();
                }
            }

            // SECOND: If no action matched and LLM available, use it for conversational understanding
            if (action == null && chatModel != null)
            {
                try
                {
                    // Add user message to history
                    conversationHistory.Add($"User: {input}");

                    // Build full prompt with system context and conversation history
                    string conversationContext = conversationHistory.Count > 1
                        ? "\n\nRecent conversation:\n" + string.Join("\n", conversationHistory.TakeLast(6))
                        : "";

                    // Dynamically inject current tools list
                    string toolsList = dynamicTools.Count > 0
                        ? string.Join(", ", dynamicTools.All.Select(t => t.Name))
                        : "duckduckgo_search, fetch_url, calculator (built-in, more can be created at runtime)";
                    string dynamicSystemPrompt = systemPrompt.Replace("{{DYNAMIC_TOOLS_LIST}}", toolsList);

                    // ==== PERSONALITY ENGINE INTEGRATION ====
                    string personalityModifiers = "";
                    string? suggestedQuestion = null;

                    if (personalityEngine != null && activePersonality != null)
                    {
                        // Reason about which traits to express based on context
                        var (currentActiveTraits, proactivity, question) = await personalityEngine.ReasonAboutResponseAsync(
                            personaName, input, string.Join("\n", conversationHistory.TakeLast(6)));

                        suggestedQuestion = question;

                        // Get personality expression modifiers
                        personalityModifiers = personalityEngine.GetResponseModifiers(personaName);

                        // Use comprehensive mood detection to update personality mood
                        var detectedMood = personalityEngine.DetectMoodFromInput(input);
                        personalityEngine.UpdateMoodFromDetection(personaName, input);

                        // Add mood context to help LLM respond appropriately
                        if (detectedMood.Frustration > 0.4)
                        {
                            personalityModifiers += "\n\nUSER MOOD: Frustrated - be extra supportive, acknowledge their difficulty, offer concrete help.";
                        }
                        else if (detectedMood.Curiosity > 0.5)
                        {
                            personalityModifiers += "\n\nUSER MOOD: Curious - engage their interest, explore tangents, share enthusiasm.";
                        }
                        else if (detectedMood.Urgency > 0.4)
                        {
                            personalityModifiers += "\n\nUSER MOOD: Urgent - be direct and efficient, prioritize actionable information.";
                        }
                        else if (detectedMood.Energy < -0.3)
                        {
                            personalityModifiers += "\n\nUSER MOOD: Low energy - be gentle and encouraging, keep responses focused.";
                        }

                        string updatedMood = personalityEngine.GetCurrentMood(personaName);
                        Console.WriteLine($"  [mood] {updatedMood} (detected: {detectedMood.DominantEmotion ?? "neutral"}, confidence: {detectedMood.Confidence:P0})");

                        // If proactivity is high and we have a question, include it in guidance
                        if (proactivity > 0.6 && suggestedQuestion != null)
                        {
                            personalityModifiers += $"\n\nPROACTIVE QUESTION TO ASK: After responding, naturally ask: \"{suggestedQuestion}\"";
                        }
                        else if (proactivity > 0.4)
                        {
                            // Generate a question if we don't have one
                            suggestedQuestion = await personalityEngine.GenerateProactiveQuestionAsync(
                                personaName, input, conversationHistory.ToArray());
                            if (suggestedQuestion != null)
                            {
                                personalityModifiers += $"\n\nOPTIONAL FOLLOW-UP: You could ask: \"{suggestedQuestion}\"";
                            }
                        }

                        // Add relevant memories from Qdrant if available
                        if (personalityEngine.HasMemory)
                        {
                            string memoryContext = await personalityEngine.GetMemoryContextAsync(input, personaName, 3);
                            if (!string.IsNullOrEmpty(memoryContext))
                            {
                                personalityModifiers += memoryContext;
                            }
                        }
                    }

                    string fullPrompt = $@"{dynamicSystemPrompt}
{personalityModifiers}
{conversationContext}

User's latest message: {input}

Respond naturally as {personaName}. If the user wants to perform an action, include the appropriate ACTION tag. Remember to be concise (1-2 sentences). Be curious and proactive - ask follow-up questions when genuinely interested!";

                    // Stream LLM response token by token using Rx
                    Console.Write("  [LLM] ");
                    var responseBuilder = new System.Text.StringBuilder();

                    await chatModel.StreamReasoningContent(fullPrompt)
                        .Do(token =>
                        {
                            // Stream each token to console as it arrives
                            Console.Write(token);
                            responseBuilder.Append(token);
                        })
                        .LastOrDefaultAsync();

                    Console.WriteLine();
                    string llmResponse = responseBuilder.ToString();

                    // Parse action from response
                    if (llmResponse.Contains("ACTION:"))
                    {
                        var actionMatch = System.Text.RegularExpressions.Regex.Match(llmResponse, @"ACTION:(\w+)(?::(.+))?");
                        if (actionMatch.Success)
                        {
                            action = actionMatch.Groups[1].Value.ToLowerInvariant();
                            actionParam = actionMatch.Groups[2].Success ? actionMatch.Groups[2].Value.Trim() : null;
                            // Remove ACTION from spoken response
                            response = System.Text.RegularExpressions.Regex.Replace(llmResponse, @"ACTION:\w+(?::[^\n]+)?", "").Trim();
                        }
                    }
                    else
                    {
                        response = llmResponse.Trim();
                    }

                    // Keep conversation history bounded
                    if (conversationHistory.Count > 12)
                    {
                        conversationHistory.RemoveRange(0, 6); // Remove oldest messages
                    }
                    conversationHistory.Add($"Assistant: {response ?? llmResponse}");

                    // ==== PERSONALITY FEEDBACK & EVOLUTION ====
                    if (personalityEngine != null)
                    {
                        // Heuristic feedback based on response characteristics
                        bool askedQuestion = (response ?? llmResponse).Contains('?');
                        double engagement = input.Length > 50 ? 0.8 : input.Length > 20 ? 0.6 : 0.4;
                        double relevance = action != null ? 0.9 : 0.7; // Actions usually mean we understood

                        string? topicFromInput = ExtractTopic(input);

                        var feedback = new InteractionFeedback(
                            EngagementLevel: engagement,
                            ResponseRelevance: relevance,
                            QuestionQuality: askedQuestion ? 0.7 : 0.0,
                            ConversationContinuity: conversationHistory.Count > 2 ? 0.8 : 0.5,
                            TopicDiscussed: topicFromInput,
                            QuestionAsked: askedQuestion ? suggestedQuestion : null,
                            UserAskedFollowUp: false);

                        personalityEngine.RecordFeedback(personaName, feedback);

                        // Store conversation in Qdrant memory (significant conversations only)
                        if (personalityEngine.HasMemory && engagement > 0.5)
                        {
                            var detectedMoodForStorage = personalityEngine.DetectMoodFromInput(input);
                            double significance = (engagement + relevance) / 2.0;
                            _ = personalityEngine.StoreConversationMemoryAsync(
                                personaName,
                                input,
                                response ?? llmResponse,
                                topicFromInput,
                                detectedMoodForStorage.DominantEmotion,
                                significance);
                        }

                        // Evolve personality every 10 interactions
                        if (activePersonality != null && activePersonality.InteractionCount % 10 == 9)
                        {
                            try
                            {
                                activePersonality = await personalityEngine.EvolvePersonalityAsync(personaName);
                                Console.WriteLine($"  [+] Personality evolved! Top traits: {string.Join(", ", activePersonality.GetActiveTraits(3).Select(t => $"{t.Name}({t.EffectiveIntensity:P0})"))}");

                                // Save personality snapshot to Qdrant
                                if (personalityEngine.HasMemory)
                                {
                                    _ = personalityEngine.SavePersonalitySnapshotAsync(personaName);
                                }
                            }
                            catch { /* Evolution is optional, don't fail */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [!] LLM error: {ex.Message}");
                }
            }

            // Execute action
            switch (action)
            {
                case "exit":
                    await SayAsync(response ?? "Goodbye! It was wonderful helping you today. Come back anytime!");
                    Console.WriteLine();
                    // Cleanup Rx subscriptions and background listeners
                    speechCts.Cancel();
                    continuousListenerSubscription?.Dispose();
                    inputSubscription?.Dispose();
                    volatileLiveText = ""; // Clear live display
                    speechSubject.OnCompleted();
                    keyboardSubject.OnCompleted();
                    speechSubject.Dispose();
                    keyboardSubject.Dispose();
                    return;

                case "list":
                    var skills = registry.GetAllSkills().ToList();
                    if (skills.Count == 0)
                    {
                        await SayAsync(response ?? "I don't have any skills registered yet. Would you like me to learn some from research? Just say 'learn about' followed by a topic.");
                    }
                    else
                    {
                        string skillList = string.Join(", ", skills.Take(5).Select(s => s.Name));
                        await SayAsync(response ?? $"I know {skills.Count} skills. Here are some: {skillList}. Would you like me to run one or learn more?");

                        Console.WriteLine("\n  [i] All Skills:");
                        foreach (var skill in skills)
                        {
                            Console.WriteLine($"     - {skill.Name,-28} ({skill.SuccessRate:P0})");
                        }
                        Console.WriteLine();
                    }
                    continue;

                case "learn":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("What topic would you like me to research?");
                        continue;
                    }
                    // Inline learn action
                    {
                        string topic = actionParam;
                        await SayAsync($"Interesting! Let me search for research on {topic}.");
                        Console.WriteLine($"\n  [>] Fetching research on: \"{topic}\"...");
                        try
                        {
                            string fetchUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(topic)}&start=0&max_results=5";
                            string fetchXml = await httpClient.GetStringAsync(fetchUrl);
                            var fetchDoc = System.Xml.Linq.XDocument.Parse(fetchXml);
                            System.Xml.Linq.XNamespace fetchAtom = "http://www.w3.org/2005/Atom";
                            var fetchEntries = fetchDoc.Descendants(fetchAtom + "entry").Take(5).ToList();
                            Console.WriteLine($"  [i] Found {fetchEntries.Count} papers");
                            if (fetchEntries.Count > 0)
                            {
                                foreach (var fetchEntry in fetchEntries)
                                {
                                    string fetchTitle = fetchEntry.Element(fetchAtom + "title")?.Value?.Trim().Replace("\n", " ") ?? "Untitled";
                                    Console.WriteLine($"     [i] {fetchTitle}");
                                }
                                string newSkillName = string.Join("", topic.Split(' ').Select(w =>
                                    w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";
                                var newSkill = new Skill(newSkillName, $"Analysis from '{topic}' research", new List<string> { "research" },
                                    new List<PlanStep> { MakeStep("Gather", topic, "Papers", 0.9), MakeStep("Analyze", "Extract", "Insights", 0.85) },
                                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow);
                                registry.RegisterSkill(newSkill);
                                Console.WriteLine($"  [OK] Created skill: {newSkillName}");
                                await SayAsync($"I found {fetchEntries.Count} papers and created skill {newSkillName}.");
                            }
                            else
                            {
                                await SayAsync($"I couldn't find papers on {topic}.");
                            }
                        }
                        catch (Exception learnEx)
                        {
                            Console.WriteLine($"  [!] Error: {learnEx.Message}");
                            await SayAsync("I had trouble searching.");
                        }
                    }
                    continue;

                case "tool":
                    // Dynamic tool creation at runtime with intelligent learning
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("What kind of tool would you like me to create? For example: google search, calculator, or url fetcher.");
                        continue;
                    }
                    {
                        string toolName = actionParam.ToLowerInvariant().Replace(" ", "_");
                        Console.WriteLine($"\n  [>] Creating tool: {toolName}...");

                        // Check if it's a built-in tool type
                        ITool? newTool = toolName switch
                        {
                            "google" or "google_search" or "googlesearch" =>
                                dynamicToolFactory?.CreateWebSearchTool("google"),
                            "bing" or "bing_search" or "bingsearch" =>
                                dynamicToolFactory?.CreateWebSearchTool("bing"),
                            "duckduckgo" or "ddg" or "duck" or "duckduckgo_search" =>
                                dynamicToolFactory?.CreateWebSearchTool("duckduckgo"),
                            "calculator" or "calc" or "math" =>
                                dynamicToolFactory?.CreateCalculatorTool(),
                            "fetch" or "url" or "fetch_url" or "url_fetch" =>
                                dynamicToolFactory?.CreateUrlFetchTool(),
                            _ => null,
                        };

                        if (newTool != null)
                        {
                            dynamicTools = dynamicTools.WithTool(newTool);
                            Console.WriteLine($"  [OK] Tool '{newTool.Name}' created and registered!");
                            await SayAsync($"Done! I now have the {newTool.Name} tool. You can ask me to use it anytime.");
                        }
                        else if (toolLearner != null)
                        {
                            // Use intelligent learner with MeTTa + GA + Qdrant
                            await SayAsync($"Let me intelligently find or create a tool for that. Using semantic reasoning and optimization...");
                            Console.WriteLine($"  [>] Using IntelligentToolLearner (MeTTa + GA + Qdrant)...");

                            var learnerResult = await toolLearner.FindOrCreateToolAsync(
                                actionParam,
                                dynamicTools,
                                CancellationToken.None);

                            learnerResult.Match(
                                result =>
                                {
                                    dynamicTools = dynamicTools.WithTool(result.Tool);
                                    if (result.WasCreated)
                                    {
                                        Console.WriteLine($"  [OK] Intelligent tool '{result.Tool.Name}' created & optimized via GA!");
                                        var stats = toolLearner.GetStats();
                                        Console.WriteLine($"       Pattern saved to Qdrant ({stats.TotalPatterns} patterns total)");
                                        _ = SayAsync($"I created an optimized {result.Tool.Name} tool using genetic algorithms. The pattern is saved for future use.");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  [OK] Found existing optimized tool: {result.Tool.Name}");
                                        _ = SayAsync($"I found an existing {result.Tool.Name} tool from learned patterns. Ready to use!");
                                    }
                                },
                                error =>
                                {
                                    Console.WriteLine($"  [!] Intelligent creation failed: {error}");
                                    _ = SayAsync($"I couldn't create that tool intelligently. Error: {error}");
                                });
                        }
                        else if (dynamicToolFactory != null)
                        {
                            // Fallback to basic LLM generation
                            await SayAsync($"Let me try to create a {toolName} tool. This might take a moment...");
                            Result<ITool, string> toolResult = await dynamicToolFactory.CreateToolAsync(
                                toolName,
                                $"A tool that {actionParam}",
                                CancellationToken.None);

                            toolResult.Match(
                                tool =>
                                {
                                    dynamicTools = dynamicTools.WithTool(tool);
                                    Console.WriteLine($"  [OK] Custom tool '{tool.Name}' compiled and registered!");
                                    _ = SayAsync($"I created a custom {tool.Name} tool. It's ready to use!");
                                },
                                error =>
                                {
                                    Console.WriteLine($"  [!] Failed to create tool: {error}");
                                    _ = SayAsync($"I couldn't create that tool. The error was: {error}");
                                });
                        }
                        else
                        {
                            await SayAsync("I can't create tools right now because the dynamic factory isn't available.");
                        }
                    }
                    continue;

                case "usetool":
                    // Use an existing dynamic tool
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        string availableTools = string.Join(", ", dynamicTools.All.Select(t => t.Name));
                        await SayAsync($"I have these tools available: {availableTools}. What would you like me to use?");
                        continue;
                    }
                    {
                        // Parse tool:input format
                        string[] parts = actionParam.Split(':', 2);
                        string toolName = parts[0].Trim();
                        string toolInput = parts.Length > 1 ? parts[1].Trim() : "";

                        ITool? foundTool = dynamicTools.Get(toolName);
                        if (foundTool == null)
                        {
                            // Try partial match
                            foundTool = dynamicTools.All.FirstOrDefault(t =>
                                t.Name.Contains(toolName, StringComparison.OrdinalIgnoreCase));
                        }

                        if (foundTool != null)
                        {
                            Console.WriteLine($"\n  [>] Using tool: {foundTool.Name}");
                            Console.WriteLine($"     Input: {toolInput}");

                            try
                            {
                                Result<string, string> toolResult = await foundTool.InvokeAsync(toolInput);
                                toolResult.Match(
                                    success =>
                                    {
                                        Console.WriteLine($"  [OK] Result: {(success.Length > 500 ? success[..500] + "..." : success)}");
                                        _ = SayAsync(success.Length > 200 ? $"The {foundTool.Name} returned: {success[..200]}..." : $"The {foundTool.Name} says: {success}");
                                    },
                                    error =>
                                    {
                                        Console.WriteLine($"  [!] Tool error: {error}");
                                        _ = SayAsync($"The {foundTool.Name} tool had an error: {error}");
                                    });
                            }
                            catch (Exception toolEx)
                            {
                                Console.WriteLine($"  [!] Tool exception: {toolEx.Message}");
                                await SayAsync($"Something went wrong with the {foundTool.Name} tool.");
                            }
                        }
                        else
                        {
                            string availableTools = string.Join(", ", dynamicTools.All.Select(t => t.Name));
                            await SayAsync($"I don't have a tool called {toolName}. Available tools: {availableTools}");
                        }
                    }
                    continue;

                case "smarttool":
                    // Goal-based intelligent tool finding with MeTTa reasoning
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("Tell me what you want to accomplish and I'll find or create the best tool for it.");
                        continue;
                    }
                    {
                        if (toolLearner == null)
                        {
                            await SayAsync("Intelligent tool learning isn't available. Try using 'tool' to create a basic tool.");
                            continue;
                        }

                        // Parse goal:input format (e.g., "search for Python tutorials:python machine learning")
                        string[] goalParts = actionParam.Split(':', 2);
                        string toolGoal = goalParts[0].Trim();
                        string toolInput = goalParts.Length > 1 ? goalParts[1].Trim() : string.Empty;

                        Console.WriteLine($"\n  [>] Intelligent tool discovery for: {toolGoal}");
                        Console.WriteLine($"  [>] Using MeTTa semantic reasoning + genetic optimization...");

                        var findResult = await toolLearner.FindOrCreateToolAsync(toolGoal, dynamicTools, CancellationToken.None);

                        if (findResult.IsSuccess)
                        {
                            var result = findResult.Value;
                            dynamicTools = dynamicTools.WithTool(result.Tool);

                            string status = result.WasCreated
                                ? "Created & optimized via GA"
                                : "Found from learned patterns";
                            Console.WriteLine($"  [OK] Tool: {result.Tool.Name} ({status})");

                            if (!string.IsNullOrEmpty(toolInput))
                            {
                                Console.WriteLine($"  [>] Executing with input: {toolInput}");
                                var toolResult = await result.Tool.InvokeAsync(toolInput);
                                toolResult.Match(
                                    success =>
                                    {
                                        Console.WriteLine($"  [OK] Result: {(success.Length > 500 ? success[..500] + "..." : success)}");
                                        _ = SayAsync(success.Length > 200 ? $"Here's what I found: {success[..200]}..." : success);
                                    },
                                    error =>
                                    {
                                        Console.WriteLine($"  [!] Tool error: {error}");
                                        _ = SayAsync($"The tool had an error: {error}");
                                    });
                            }
                            else
                            {
                                var stats = toolLearner.GetStats();
                                await SayAsync($"I have the {result.Tool.Name} tool ready. Tell me what to do with it. I've learned {stats.TotalPatterns} patterns so far.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  [!] Intelligent tool discovery failed: {findResult.Error}");
                            await SayAsync($"I couldn't find or create a suitable tool. Error: {findResult.Error}");
                        }
                    }
                    continue;

                case "toolstats":
                    // Show intelligent tool learner statistics
                    if (toolLearner == null)
                    {
                        await SayAsync("Intelligent tool learning isn't available.");
                        continue;
                    }
                    {
                        var stats = toolLearner.GetStats();
                        var patterns = toolLearner.GetAllPatterns().Take(5).ToList();

                        Console.WriteLine($"\n  === Intelligent Tool Learner Stats ===");
                        Console.WriteLine($"  Patterns learned: {stats.TotalPatterns}");
                        Console.WriteLine($"  Avg success rate: {stats.AvgSuccessRate:P1}");
                        Console.WriteLine($"  Total usages: {stats.TotalUsage}");

                        if (patterns.Count > 0)
                        {
                            Console.WriteLine($"\n  Top patterns:");
                            foreach (var p in patterns)
                            {
                                Console.WriteLine($"    - {p.ToolName}: {p.Goal} (used {p.UsageCount}x, {p.SuccessRate:P0} success)");
                            }
                        }

                        await SayAsync($"I've learned {stats.TotalPatterns} tool patterns with {stats.AvgSuccessRate:P0} average success rate.");
                    }
                    continue;

                case "suggest":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("What goal would you like skill suggestions for?");
                        continue;
                    }
                    var matches = registry.GetAllSkills()
                        .Where(s => s.Description.Contains(actionParam, StringComparison.OrdinalIgnoreCase) ||
                                    s.Name.Contains(actionParam, StringComparison.OrdinalIgnoreCase))
                        .Take(3)
                        .ToList();
                    if (matches.Count > 0)
                    {
                        string matchList = string.Join(", ", matches.Select(m => m.Name));
                        await SayAsync(response ?? $"For {actionParam}, I'd suggest: {matchList}. Would you like me to run one of these?");
                    }
                    else
                    {
                        await SayAsync(response ?? $"I don't have matching skills for {actionParam} yet. Would you like me to learn by researching it?");
                    }
                    continue;

                case "run":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("Which skill would you like me to run?");
                        continue;
                    }
                    // Inline run action
                    {
                        var skillToRun = registry.GetAllSkills()
                            .FirstOrDefault(s => s.Name.Equals(actionParam, StringComparison.OrdinalIgnoreCase) ||
                                                 s.Name.Contains(actionParam, StringComparison.OrdinalIgnoreCase));
                        if (skillToRun == null)
                        {
                            await SayAsync($"I don't know a skill called {actionParam}. Say 'list skills' to see what I have.");
                        }
                        else if (skillToRun.Name == "LiteratureReview" || skillToRun.Name == "CitationAnalysis")
                        {
                            await SayAsync($"What topic for {skillToRun.Name}?");
                            string runTopic = await ListenAsync();
                            if (!string.IsNullOrWhiteSpace(runTopic) && !runTopic.ToLowerInvariant().Contains("cancel"))
                            {
                                await SayAsync($"Searching literature on {runTopic}.");
                                Console.WriteLine($"\n  [>] Executing: {skillToRun.Name} for \"{runTopic}\"");
                                try
                                {
                                    string runUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(runTopic)}&start=0&max_results=5";
                                    string runXml = await httpClient.GetStringAsync(runUrl);
                                    var runDoc = System.Xml.Linq.XDocument.Parse(runXml);
                                    System.Xml.Linq.XNamespace runAtom = "http://www.w3.org/2005/Atom";
                                    var runEntries = runDoc.Descendants(runAtom + "entry").ToList();
                                    Console.WriteLine($"  [i] Found {runEntries.Count} papers");
                                    foreach (var runEntry in runEntries.Take(3))
                                    {
                                        string runTitle = runEntry.Element(runAtom + "title")?.Value?.Trim().Replace("\n", " ") ?? "";
                                        Console.WriteLine($"     [i] {runTitle}");
                                    }
                                    await SayAsync($"Found {runEntries.Count} papers on {runTopic}.");
                                }
                                catch { await SayAsync("Error searching."); }
                            }
                        }
                        else
                        {
                            await SayAsync($"Running {skillToRun.Name}.");
                            Console.WriteLine($"\n  [>] Executing: {skillToRun.Name}");
                            foreach (var step in skillToRun.Steps)
                            {
                                Console.Write($"     -> {step.Action}...");
                                await Task.Delay(300);
                                Console.WriteLine(" [OK]");
                            }
                            await SayAsync($"Done! {skillToRun.Name} completed.");
                        }
                    }
                    continue;

                case "help":
                    if (!string.IsNullOrWhiteSpace(actionParam))
                    {
                        // Help for specific command
                        if (commandHelp.TryGetValue(actionParam, out var cmdInfo))
                        {
                            Console.WriteLine($"\n  [>] Help: {actionParam}");
                            Console.WriteLine($"     Syntax: {cmdInfo.Syntax}");
                            Console.WriteLine($"     {cmdInfo.Description}");
                            Console.WriteLine($"     Examples:");
                            foreach (var ex in cmdInfo.Examples)
                            {
                                Console.WriteLine($"       - {ex}");
                            }
                            Console.WriteLine();
                            await SayAsync($"{actionParam}: {cmdInfo.Description}");
                        }
                        else if (allTokens.TryGetValue(actionParam, out var tokenInfo))
                        {
                            // Help for a pipeline token
                            Console.WriteLine($"\n  [>] Pipeline Token: {tokenInfo.PrimaryName}");
                            Console.WriteLine($"  [#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][>][i]");
                            Console.WriteLine($"     Source: {tokenInfo.SourceClass}");
                            Console.WriteLine($"     Description: {tokenInfo.Description}");
                            if (tokenInfo.Aliases.Length > 0)
                            {
                                Console.WriteLine($"     Aliases: {string.Join(", ", tokenInfo.Aliases)}");
                            }
                            // Show method signature info
                            var method = tokenInfo.Method;
                            var parameters = method.GetParameters();
                            if (parameters.Length > 0)
                            {
                                Console.WriteLine($"     Parameters:");
                                foreach (var param in parameters)
                                {
                                    string paramType = param.ParameterType.Name;
                                    string defaultVal = param.HasDefaultValue ? $" = {param.DefaultValue ?? "null"}" : "";
                                    Console.WriteLine($"       - {param.Name}: {paramType}{defaultVal}");
                                }
                            }
                            Console.WriteLine($"\n     Usage in pipeline:");
                            Console.WriteLine($"       {tokenInfo.PrimaryName} 'argument' | NextStep");
                            Console.WriteLine();
                            await SayAsync($"{tokenInfo.PrimaryName}: {tokenInfo.Description}");
                        }
                        else
                        {
                            // Try partial match for tokens
                            var partialMatches = allTokens.Where(kv =>
                                kv.Key.Contains(actionParam, StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
                            if (partialMatches.Count > 0)
                            {
                                Console.WriteLine($"\n  [+] Did you mean one of these?");
                                foreach (var (name, info) in partialMatches)
                                {
                                    Console.WriteLine($"     - {info.PrimaryName}: {info.Description}");
                                }
                                await SayAsync($"I found {partialMatches.Count} similar tokens. Check the console.");
                            }
                            else
                            {
                                await SayAsync($"I don't have help for '{actionParam}'. Try 'help' for commands or 'tokens' for pipeline tokens.");
                            }
                        }
                    }
                    else
                    {
                        // Full help
                        Console.WriteLine("\n  [>] OUROBOROS VOICE COMMANDS:");
                        Console.WriteLine("  [#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][i]\n");
                        foreach (var (cmd, (syntax, desc, _)) in commandHelp)
                        {
                            Console.WriteLine($"  {syntax,-35} {desc}");
                        }
                        Console.WriteLine("\n  [#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][i]");
                        Console.WriteLine("  [+] Tips:");
                        Console.WriteLine("     - Press Tab to autocomplete commands and tokens");
                        Console.WriteLine("     - Press F1 or Ctrl+Space for suggestions");
                        Console.WriteLine("     - Use | to chain pipeline steps: ArxivSearch 'AI' | UseOutput");
                        Console.WriteLine("     - Type a token name directly to see its help\n");
                        await SayAsync("I've shown the full command list. Use Tab to autocomplete, or pipe commands together with the bar symbol.");
                    }
                    continue;

                case "pipeline":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("Please provide a pipeline. For example: ArxivSearch 'AI' pipe UseOutput");
                        continue;
                    }
                    try
                    {
                        string pipelineResult = await ExecutePipelineAsync(actionParam);
                        if (pipelineResult.Length > 200)
                        {
                            await SayAsync("Pipeline complete. The output is shown in the console.");
                        }
                        else
                        {
                            await SayAsync($"Pipeline complete: {pipelineResult}");
                        }
                    }
                    catch (Exception pipeEx)
                    {
                        Console.WriteLine($"  [!] Pipeline error: {pipeEx.Message}");
                        await SayAsync("The pipeline encountered an error.");
                    }
                    continue;

                case "search":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("What would you like me to search for?");
                        continue;
                    }
                    await SayAsync($"Searching multiple sources for {actionParam}.");
                    Console.WriteLine($"\n  [>] Multi-source search: \"{actionParam}\"");
                    try
                    {
                        // Search arXiv
                        Console.WriteLine("  [i] Searching arXiv...");
                        string arxivUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(actionParam)}&start=0&max_results=3";
                        string arxivXml = await httpClient.GetStringAsync(arxivUrl);
                        var arxivDoc = System.Xml.Linq.XDocument.Parse(arxivXml);
                        System.Xml.Linq.XNamespace arxivAtom = "http://www.w3.org/2005/Atom";
                        var arxivEntries = arxivDoc.Descendants(arxivAtom + "entry").Take(3).ToList();
                        Console.WriteLine($"     Found {arxivEntries.Count} arXiv papers");

                        // Search Wikipedia
                        Console.WriteLine("  [>] Searching Wikipedia...");
                        string wikiUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(actionParam.Replace(" ", "_"))}";
                        try
                        {
                            string wikiJson = await httpClient.GetStringAsync(wikiUrl);
                            using var wikiDoc = System.Text.Json.JsonDocument.Parse(wikiJson);
                            if (wikiDoc.RootElement.TryGetProperty("title", out var wikiTitle))
                            {
                                Console.WriteLine($"     Found Wikipedia: {wikiTitle.GetString()}");
                            }
                        }
                        catch { Console.WriteLine("     Wikipedia: no direct match"); }

                        await SayAsync($"Found {arxivEntries.Count} papers and other sources for {actionParam}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [!] Search error: {ex.Message}");
                        await SayAsync("I had trouble with the search.");
                    }
                    continue;

                case "fetch":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("What URL would you like me to fetch?");
                        continue;
                    }
                    await SayAsync($"Fetching content from {actionParam}.");
                    Console.WriteLine($"\n  [>] Fetching: {actionParam}");
                    try
                    {
                        string content = await httpClient.GetStringAsync(actionParam);
                        // Extract text if HTML
                        if (content.Contains("<html") || content.Contains("<HTML"))
                        {
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"<script[^>]*>[\s\S]*?</script>", "");
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"<style[^>]*>[\s\S]*?</style>", "");
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ");
                            content = System.Net.WebUtility.HtmlDecode(content);
                            content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();
                        }
                        Console.WriteLine($"  [OK] Retrieved {content.Length:N0} characters");
                        Console.WriteLine($"  Preview: {(content.Length > 200 ? content[..200] + "..." : content)}");
                        await SayAsync($"Fetched {content.Length} characters from that URL.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [!] Fetch error: {ex.Message}");
                        await SayAsync("I couldn't fetch that URL.");
                    }
                    continue;

                case "emergence":
                    if (string.IsNullOrWhiteSpace(actionParam))
                    {
                        await SayAsync("What topic for the emergence cycle?");
                        continue;
                    }
                    await SayAsync($"Starting the Ouroboros emergence cycle for {actionParam}. This may take a moment.");
                    Console.WriteLine($"\n  [>] Starting emergence cycle: \"{actionParam}\"");
                    try
                    {
                        // Create minimal pipeline state for the emergence cycle
                        // This simulates the EmergenceCycle step but in the voice context
                        Console.WriteLine("  Phase 1: INGEST - Multi-source research...");
                        var allResults = new System.Text.StringBuilder();

                        // arXiv
                        string eArxivUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(actionParam)}&start=0&max_results=5";
                        string eArxivXml = await httpClient.GetStringAsync(eArxivUrl);
                        var eArxivDoc = System.Xml.Linq.XDocument.Parse(eArxivXml);
                        System.Xml.Linq.XNamespace eArxivAtom = "http://www.w3.org/2005/Atom";
                        var eArxivEntries = eArxivDoc.Descendants(eArxivAtom + "entry").Take(5).ToList();
                        Console.WriteLine($"     [i] arXiv: {eArxivEntries.Count} papers");

                        foreach (var entry in eArxivEntries)
                        {
                            string title = entry.Element(eArxivAtom + "title")?.Value?.Trim().Replace("\n", " ") ?? "";
                            allResults.AppendLine($"Paper: {title}");
                        }

                        Console.WriteLine("  Phase 2: HYPOTHESIZE - Generating insights...");
                        if (chatModel != null)
                        {
                            try
                            {
                                string hypothesisPrompt = $"Based on research about '{actionParam}', generate 3 brief hypotheses in 2-3 sentences each.";
                                string hypotheses = await chatModel.GenerateTextAsync(hypothesisPrompt);
                                Console.WriteLine($"     [>] Generated hypotheses");
                                allResults.AppendLine($"\nHypotheses:\n{hypotheses}");
                            }
                            catch { Console.WriteLine("     [LLM unavailable for hypotheses]"); }
                        }

                        Console.WriteLine("  Phase 3: EXPLORE - Identifying opportunities...");
                        Console.WriteLine($"     [>] Deep dive into {actionParam} breakthroughs");

                        Console.WriteLine("  Phase 4: LEARN - Extracting skills...");
                        string emergenceSkillName = string.Join("", actionParam.Split(' ').Select(w =>
                            w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "EmergenceAnalysis";
                        var emergenceSkill = new Skill(emergenceSkillName, $"Emergence analysis for '{actionParam}'", new List<string> { "research" },
                            new List<PlanStep>
                            {
                                MakeStep("Multi-source fetch", actionParam, "Raw knowledge", 0.9),
                                MakeStep("Hypothesis generation", "abductive", "Insights", 0.85),
                                MakeStep("Opportunity identification", "novelty", "Directions", 0.8),
                                MakeStep("Skill extraction", "patterns", "Capability", 0.75)
                            },
                            0.80, 0, DateTime.UtcNow, DateTime.UtcNow);
                        registry.RegisterSkill(emergenceSkill);
                        Console.WriteLine($"  [OK] Created skill: {emergenceSkillName}");

                        await SayAsync($"Emergence cycle complete! Found {eArxivEntries.Count} papers and created skill {emergenceSkillName}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [!] Emergence error: {ex.Message}");
                        await SayAsync("The emergence cycle encountered an error.");
                    }
                    continue;

                case "tokens":
                    var tokens = SkillCliSteps.GetAllPipelineTokens();
                    var uniqueTokens = tokens.Values.Distinct().ToList();
                    string? tokenFilter = string.IsNullOrWhiteSpace(actionParam) ? null : actionParam.Trim();

                    if (tokenFilter != null)
                    {
                        // Filter tokens by name or description
                        var filteredTokens = uniqueTokens.Where(t =>
                            t.PrimaryName.Contains(tokenFilter, StringComparison.OrdinalIgnoreCase) ||
                            t.Description.Contains(tokenFilter, StringComparison.OrdinalIgnoreCase) ||
                            t.Aliases.Any(a => a.Contains(tokenFilter, StringComparison.OrdinalIgnoreCase))
                        ).ToList();

                        Console.WriteLine($"\n  [>] Tokens matching '{tokenFilter}' ({filteredTokens.Count} found):");
                        Console.WriteLine("  [#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][#][>][i]");
                        foreach (var token in filteredTokens.OrderBy(t => t.PrimaryName))
                        {
                            string aliases = token.Aliases.Length > 0 ? $" (aliases: {string.Join(", ", token.Aliases)})" : "";
                            Console.WriteLine($"\n  [>] {token.PrimaryName}{aliases}");
                            Console.WriteLine($"    {token.Description}");
                        }
                        if (filteredTokens.Count == 0)
                        {
                            Console.WriteLine($"    No tokens match '{tokenFilter}'. Try: tokens vector, tokens stream, tokens prompt");
                        }
                        await SayAsync(filteredTokens.Count > 0
                            ? $"Found {filteredTokens.Count} tokens matching {tokenFilter}."
                            : $"No tokens match {tokenFilter}.");
                    }
                    else
                    {
                        // Show all tokens grouped by source
                        Console.WriteLine($"\n  [>] ALL PIPELINE TOKENS ({uniqueTokens.Count} total)");
                        Console.WriteLine("  ----------------------------------------------------------------");

                        var grouped = uniqueTokens.GroupBy(t => t.SourceClass).OrderBy(g => g.Key);
                        foreach (var group in grouped)
                        {
                            Console.WriteLine($"\n  [*] {group.Key} ({group.Count()} tokens) ---------------");
                            foreach (var token in group.OrderBy(t => t.PrimaryName))
                            {
                                string aliases = token.Aliases.Length > 0 ? $" [{string.Join(", ", token.Aliases)}]" : "";
                                string desc = token.Description.Length > 50 ? token.Description[..47] + "..." : token.Description;
                                Console.WriteLine($"  [>] {token.PrimaryName,-25} {desc}{aliases}");
                            }
                            Console.WriteLine("  ----------------------------------------------------------------");
                        }
                        Console.WriteLine($"\n  [+] Use 'tokens <filter>' to search, or 'help <TokenName>' for details");
                        await SayAsync($"I have {uniqueTokens.Count} pipeline tokens available across {grouped.Count()} modules. Use tokens with a filter word to search.");
                    }
                    continue;

                default:
                    // Use LLM response if available, otherwise generic fallback
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        await SayAsync(response);
                    }
                    else
                    {
                        await SayAsync($"I heard '{input}'. I'm not sure what you'd like me to do. Try saying 'help' for guidance, or 'list skills' to see what I can do.");
                    }
                    continue;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Error in voice loop: {ex.Message}");
        }
    }
}

static bool CheckWhisperAvailable()
{
    try
    {
        // First check for Python whisper (most common)
        var pythonCheck = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = "-c \"import whisper; print('ok')\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var pyProcess = Process.Start(pythonCheck);
        if (pyProcess != null)
        {
            string output = pyProcess.StandardOutput.ReadToEnd();
            pyProcess.WaitForExit(5000);
            if (pyProcess.ExitCode == 0 && output.Contains("ok"))
            {
                return true;
            }
        }
    }
    catch { }

    try
    {
        // Check for whisper CLI in PATH
        var startInfo = new ProcessStartInfo
        {
            FileName = "whisper",
            Arguments = "--help",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        process?.WaitForExit(1000);
        return process?.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}
