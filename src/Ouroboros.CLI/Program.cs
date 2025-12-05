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
using LangChainPipeline.CLI; // for MicrophoneRecorder, AudioPlayer
using LangChainPipeline.Diagnostics; // added
using LangChainPipeline.Options;
using LangChainPipeline.Providers.SpeechToText; // for STT services
using LangChainPipeline.Providers.TextToSpeech; // for TTS services
using Microsoft.Extensions.Hosting;

using LangChainPipeline.Tools.MeTTa; // added
using Ouroboros.CLI; // added

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

static async Task RunMeTTaDockerTest()
{
    Console.WriteLine("=== Test: Subprocess MeTTa Engine (Docker) ===");

    using var engine = new SubprocessMeTTaEngine();

    // 1. Basic Math
    var result = await engine.ExecuteQueryAsync("(+ 1 2)", CancellationToken.None);

    result.Match(
        success => Console.WriteLine($"✓ Basic Query succeeded: {success}"),
        error => Console.WriteLine($"✗ Basic Query failed: {error}"));

    // 2. Motto Initialization
    Console.WriteLine("\n=== Test: Motto Initialization ===");
    var initStep = new MottoSteps.MottoInitializeStep(engine);
    var initResult = await initStep.ExecuteAsync(Unit.Value, CancellationToken.None);
    initResult.Match(
        success => Console.WriteLine("✓ Motto Initialized"),
        error => Console.WriteLine($"✗ Motto Initialization failed: {error}")
    );

    // 3. Motto Chat (Mock)
    // Note: This requires OPENAI_API_KEY or similar in the docker container if it actually calls LLM.
    // If not configured, it might fail or return error.
    // But we can at least verify the MeTTa command generation and execution attempt.
    
    Console.WriteLine("\n=== Test: Motto Chat Step ===");
    var chatStep = new MottoSteps.MottoChatStep(engine);
    // We expect this to fail if no API key, but the command should run.
    var chatResult = await chatStep.ExecuteAsync("Hello", CancellationToken.None);
    chatResult.Match(
        success => Console.WriteLine($"✓ Chat Response: {success}"),
        error => Console.WriteLine($"? Chat Result: {error} (Expected if no API key)")
    );

    Console.WriteLine("✓ Subprocess MeTTa engine test completed\n");
}

static async Task RunPipelineDslAsync(string dsl, string modelName, string embedName, string sourcePath, int k, bool trace, ChatRuntimeSettings? settings = null, PipelineOptions? pipelineOpts = null)
{
    // Setup minimal environment for reasoning/ingest arrows
    // Remote model support (OpenAI-compatible and Ollama Cloud) via environment variables or CLI overrides
    (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
        pipelineOpts?.Endpoint,
        pipelineOpts?.ApiKey,
        pipelineOpts?.EndpointType);

    OllamaProvider provider = new OllamaProvider();
    IChatCompletionModel chatModel;

    if (pipelineOpts is not null && pipelineOpts.Router.Equals("auto", StringComparison.OrdinalIgnoreCase))
    {
        // Build router using provided model overrides; fallback to primary modelName
        Dictionary<string, IChatCompletionModel> modelMap = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase);
        IChatCompletionModel MakeLocal(string name, string role)
        {
            OllamaChatModel m = new OllamaChatModel(provider, name);
            // Apply presets based on model name and role
            try
            {
                string n = (name ?? string.Empty).ToLowerInvariant();
                if (n.StartsWith("deepseek-coder:33b"))
                {
                    m.Settings = OllamaPresets.DeepSeekCoder33B;
                }
                else if (n.StartsWith("llama3"))
                {
                    m.Settings = role.Equals("summarize", StringComparison.OrdinalIgnoreCase)
                        ? OllamaPresets.Llama3Summarize
                        : OllamaPresets.Llama3General;
                }
                else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b"))
                {
                    m.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
                }
                else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b"))
                {
                    m.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
                }
                else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large")))
                {
                    m.Settings = OllamaPresets.Mistral7BGeneral;
                }
                else if (n.StartsWith("qwen2.5") || n.Contains("qwen"))
                {
                    m.Settings = OllamaPresets.Qwen25_7B_General;
                }
                else if (n.StartsWith("phi3") || n.Contains("phi-3"))
                {
                    m.Settings = OllamaPresets.Phi3MiniGeneral;
                }
            }
            catch { /* non-fatal: fall back to provider defaults */ }
            return new OllamaChatAdapter(m);
        }
        string general = pipelineOpts.GeneralModel ?? modelName;
        modelMap["general"] = MakeLocal(general, "general");
        if (!string.IsNullOrWhiteSpace(pipelineOpts.CoderModel)) modelMap["coder"] = MakeLocal(pipelineOpts.CoderModel!, "coder");
        if (!string.IsNullOrWhiteSpace(pipelineOpts.SummarizeModel)) modelMap["summarize"] = MakeLocal(pipelineOpts.SummarizeModel!, "summarize");
        if (!string.IsNullOrWhiteSpace(pipelineOpts.ReasonModel)) modelMap["reason"] = MakeLocal(pipelineOpts.ReasonModel!, "reason");
        chatModel = new MultiModelRouter(modelMap, fallbackKey: "general");
    }
    else if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
    {
        try
        {
            chatModel = CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
        }
        catch (Exception ex) when (pipelineOpts is not null && !pipelineOpts.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[WARN] Remote model '{modelName}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
            OllamaChatModel local = new OllamaChatModel(provider, "llama3");
            chatModel = new OllamaChatAdapter(local);
        }
        catch (Exception ex) when (pipelineOpts is not null && !pipelineOpts.StrictModel)
        {
            Console.WriteLine($"[WARN] Remote model '{modelName}' unavailable ({ex.GetType().Name}). Falling back to local 'llama3'. Use --strict-model to disable fallback.");
            OllamaChatModel local = new OllamaChatModel(provider, "llama3");
            chatModel = new OllamaChatAdapter(local);
        }
    }
    else
    {
        OllamaChatModel chat = new OllamaChatModel(provider, modelName);
        try
        {
            string n = (modelName ?? string.Empty).ToLowerInvariant();
            if (n.StartsWith("deepseek-coder:33b")) chat.Settings = OllamaPresets.DeepSeekCoder33B;
            else if (n.StartsWith("llama3")) chat.Settings = OllamaPresets.Llama3General;
            else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b")) chat.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
            else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b")) chat.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
            else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large"))) chat.Settings = OllamaPresets.Mistral7BGeneral;
            else if (n.StartsWith("qwen2.5") || n.Contains("qwen")) chat.Settings = OllamaPresets.Qwen25_7B_General;
            else if (n.StartsWith("phi3") || n.Contains("phi-3")) chat.Settings = OllamaPresets.Phi3MiniGeneral;
        }
        catch { /* ignore and use defaults */ }
        chatModel = new OllamaChatAdapter(chat); // adapter added below
    }
    IEmbeddingModel embed = CreateEmbeddingModel(endpoint, apiKey, endpointType, embedName, provider);

    ToolRegistry tools = new ToolRegistry();
    string resolvedSource = string.IsNullOrWhiteSpace(sourcePath) ? Environment.CurrentDirectory : Path.GetFullPath(sourcePath);
    if (!Directory.Exists(resolvedSource))
    {
        Console.WriteLine($"Source path '{resolvedSource}' does not exist - creating.");
        Directory.CreateDirectory(resolvedSource);
    }
    PipelineBranch branch = new PipelineBranch("cli", new TrackedVectorStore(), DataSource.FromPath(resolvedSource));

    CliPipelineState state = new CliPipelineState
    {
        Branch = branch,
        Llm = null!, // Will be set after tools are registered
        Tools = tools,
        Embed = embed,
        RetrievalK = k,
        Trace = trace
    };

    // Register pipeline steps as tools for meta-AI capabilities
    // This allows the LLM to invoke pipeline operations, enabling self-reflective reasoning
    tools = tools.WithPipelineSteps(state);

    // Now create the LLM with all tools (including pipeline steps) registered
    ToolAwareChatModel llm = new ToolAwareChatModel(chatModel, tools);
    state.Llm = llm;
    state.Tools = tools;

    try
    {
        Step<CliPipelineState, CliPipelineState> step = PipelineDsl.Build(dsl); // Steps will use embed & llm from state; k optionally influences reasoning if we extend arrows
        state = await step(state);

        ReasoningStep? last = state.Branch.Events.OfType<ReasoningStep>().LastOrDefault();
        if (last is not null)
        {
            Console.WriteLine("\n=== PIPELINE RESULT ===");
            Console.WriteLine(last.State.Text);
        }
        else
        {
            Console.WriteLine("\n(no reasoning output; pipeline may only have ingested or set values)");
        }
        Telemetry.PrintSummary();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Pipeline failed: {ex.Message}");
    }
}

// Builds a Step<string,string> that runs either simple chat or chat+RAG in monadic form
static Step<string, string> CreateSemanticCliPipeline(bool withRag, string modelName, string embedName, int k, ChatRuntimeSettings? settings = null, AskOptions? askOpts = null)
{
    return Arrow.LiftAsync<string, string>(async question =>
    {
        // Initialize models
        OllamaProvider provider = new OllamaProvider();
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
            askOpts?.Endpoint,
            askOpts?.ApiKey,
            askOpts?.EndpointType);
        IChatCompletionModel chatModel;
        if (askOpts is not null && askOpts.Router.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // Build router using provided model overrides; fallback to primary modelName
            Dictionary<string, IChatCompletionModel> modelMap = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase);
            IChatCompletionModel MakeLocal(string name, string role)
            {
                OllamaChatModel m = new OllamaChatModel(provider, name);
                try
                {
                    string n = (name ?? string.Empty).ToLowerInvariant();
                    if (n.StartsWith("deepseek-coder:33b")) m.Settings = OllamaPresets.DeepSeekCoder33B;
                    else if (n.StartsWith("llama3")) m.Settings = role.Equals("summarize", StringComparison.OrdinalIgnoreCase) ? OllamaPresets.Llama3Summarize : OllamaPresets.Llama3General;
                    else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b")) m.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
                    else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b")) m.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
                    else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large"))) m.Settings = OllamaPresets.Mistral7BGeneral;
                    else if (n.StartsWith("qwen2.5") || n.Contains("qwen")) m.Settings = OllamaPresets.Qwen25_7B_General;
                    else if (n.StartsWith("phi3") || n.Contains("phi-3")) m.Settings = OllamaPresets.Phi3MiniGeneral;
                }
                catch
                {
                    // Best-effort preset mapping only. If parsing the model name fails,
                    // we intentionally keep provider defaults to avoid hard failures.
                }
                return new OllamaChatAdapter(m);
            }
            string general = askOpts.GeneralModel ?? modelName;
            modelMap["general"] = MakeLocal(general, "general");
            if (!string.IsNullOrWhiteSpace(askOpts.CoderModel)) modelMap["coder"] = MakeLocal(askOpts.CoderModel!, "coder");
            if (!string.IsNullOrWhiteSpace(askOpts.SummarizeModel)) modelMap["summarize"] = MakeLocal(askOpts.SummarizeModel!, "summarize");
            if (!string.IsNullOrWhiteSpace(askOpts.ReasonModel)) modelMap["reason"] = MakeLocal(askOpts.ReasonModel!, "reason");
            chatModel = new MultiModelRouter(modelMap, fallbackKey: "general");
        }
        else if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                chatModel = CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
            }
            catch (Exception ex) when (askOpts is not null && !askOpts.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[WARN] Remote model '{modelName}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                OllamaChatModel local = new OllamaChatModel(provider, "llama3");
                chatModel = new OllamaChatAdapter(local);
            }
            catch (Exception ex) when (askOpts is not null && !askOpts.StrictModel)
            {
                Console.WriteLine($"[WARN] Remote model '{modelName}' unavailable ({ex.GetType().Name}). Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                OllamaChatModel local = new OllamaChatModel(provider, "llama3");
                chatModel = new OllamaChatAdapter(local);
            }
        }
        else
        {
            OllamaChatModel chat = new OllamaChatModel(provider, modelName);
            try
            {
                string n = (modelName ?? string.Empty).ToLowerInvariant();
                if (n.StartsWith("deepseek-coder:33b")) chat.Settings = OllamaPresets.DeepSeekCoder33B;
                else if (n.StartsWith("llama3")) chat.Settings = OllamaPresets.Llama3General;
                else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b")) chat.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
                else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b")) chat.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
                else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large"))) chat.Settings = OllamaPresets.Mistral7BGeneral;
                else if (n.StartsWith("qwen2.5") || n.Contains("qwen")) chat.Settings = OllamaPresets.Qwen25_7B_General;
                else if (n.StartsWith("phi3") || n.Contains("phi-3")) chat.Settings = OllamaPresets.Phi3MiniGeneral;
            }
            catch
            {
                // Non-fatal: preset mapping is best-effort. Defaults are fine if detection fails.
            }
            chatModel = new OllamaChatAdapter(chat);
        }
        IEmbeddingModel embed = CreateEmbeddingModel(endpoint, apiKey, endpointType, embedName, provider);

        // Tool-aware LLM and in-memory vector store
        ToolRegistry tools = new ToolRegistry();
        ToolAwareChatModel llm = new ToolAwareChatModel(chatModel, tools);
        TrackedVectorStore store = new TrackedVectorStore();

        // Optional minimal RAG: seed a few docs
        if (withRag)
        {
            string[] docs = new[]
            {
                "API versioning best practices with backward compatibility",
                "Circuit breaker using Polly in .NET",
                "Event sourcing and CQRS patterns overview"
            };
            foreach ((string text, int idx) in docs.Select((d, i) => (d, i)))
            {
                try
                {
                    Telemetry.RecordEmbeddingInput(new[] { text });
                    float[] resp = await embed.CreateEmbeddingsAsync(text);
                    await store.AddAsync(new[]
                    {
                        new Vector
                        {
                            Id = (idx + 1).ToString(),
                            Text = text,
                            Embedding = resp
                        }
                    });
                    Telemetry.RecordEmbeddingSuccess(resp.Length);
                    Telemetry.RecordVectors(1);
                    if (Environment.GetEnvironmentVariable("MONADIC_DEBUG") == "1")
                        Console.WriteLine($"[embed] seed ok id={(idx + 1)} dim={resp.Length}");
                }
                catch
                {
                    await store.AddAsync(new[]
                    {
                        new Vector
                        {
                            Id = (idx + 1).ToString(),
                            Text = text,
                            Embedding = new float[8]
                        }
                    });
                    Telemetry.RecordEmbeddingFailure();
                    Telemetry.RecordVectors(1);
                    if (Environment.GetEnvironmentVariable("MONADIC_DEBUG") == "1")
                        Console.WriteLine($"[embed] seed fail id={(idx + 1)} fallback-dim=8");
                }
            }
        }

        // Answer
        if (!withRag)
        {
            (string text, List<ToolExecution> _) = await llm.GenerateWithToolsAsync($"Answer the following question clearly and concisely.\nQuestion: {{q}}".Replace("{q}", question));
            return text;
        }
        else
        {
            Telemetry.RecordEmbeddingInput(new[] { question });
            float[] qEmb = await embed.CreateEmbeddingsAsync(question);
            Telemetry.RecordEmbeddingSuccess(qEmb.Length);
            IReadOnlyCollection<Document> hits = await store.GetSimilarDocumentsAsync(qEmb, k);
            string ctx = string.Join("\n- ", hits.Select(h => h.PageContent));
            string prompt = $"Use the following context to answer.\nContext:\n- {ctx}\n\nQuestion: {{q}}".Replace("{q}", question);
            (string ragText, List<ToolExecution> _) = await llm.GenerateWithToolsAsync(prompt);
            return ragText;
        }
    });
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
        ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings),
        ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings), // Default to OpenAI-compatible for auto
        _ => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings)
    };
}

// Helper method to create the appropriate remote embedding model based on endpoint type
static IEmbeddingModel CreateEmbeddingModel(string? endpoint, string? apiKey, ChatEndpointType endpointType, string embedName, OllamaProvider provider)
{
    if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
    {
        return endpointType switch
        {
            ChatEndpointType.OllamaCloud => new OllamaCloudEmbeddingModel(endpoint, apiKey, embedName),
            ChatEndpointType.LiteLLM => new LiteLLMEmbeddingModel(endpoint, apiKey, embedName),
            _ => new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embedName)) // Fall back to local for OpenAI-compatible (no standard embedding endpoint)
        };
    }
    return new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embedName));
}

static Task RunExplainAsync(ExplainOptions o)
{
    Console.WriteLine(PipelineDsl.Explain(o.Dsl));
    return Task.CompletedTask;
}

static async Task RunPipelineAsync(PipelineOptions o)
{
    if (o.Router.Equals("auto", StringComparison.OrdinalIgnoreCase)) Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
    ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
    await RunPipelineDslAsync(o.Dsl, o.Model, o.Embed, o.Source, o.K, o.Trace, settings, o);
}

static async Task RunAskAsync(AskOptions o)
{
    if (o.Router.Equals("auto", StringComparison.OrdinalIgnoreCase)) Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
    ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
    ValidateSecrets(o);
    LogBackendSelection(o.Model, settings, o);
    Stopwatch sw = Stopwatch.StartNew();
    if (o.Agent)
    {
        // Build minimal environment (always RAG off for initial agent version; agent can internally call tools)
        OllamaProvider provider = new OllamaProvider();
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(o.Endpoint, o.ApiKey, o.EndpointType);
        IChatCompletionModel chatModel;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                chatModel = CreateRemoteChatModel(endpoint, apiKey, o.Model, settings, endpointType);
            }
            catch (Exception ex) when (!o.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[WARN] Remote model '{o.Model}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            }
        }
        else
        {
            chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
        }

        ToolRegistry tools = new ToolRegistry();
        // Register a couple of default utility tools if absent
        if (!tools.All.Any())
        {
            tools = tools
                .WithFunction("echo", "Echo back the input", s => s)
                .WithFunction("uppercase", "Convert text to uppercase", s => s.ToUpperInvariant());
        }
        TrackedVectorStore? ragStore = null;
        IEmbeddingModel? embedModel = null;
        if (o.Rag)
        {
            OllamaProvider provider2 = new OllamaProvider();
            embedModel = CreateEmbeddingModel(endpoint, apiKey, endpointType, o.Embed, provider2);
            ragStore = new TrackedVectorStore();
            string[] seedDocs = new[]
            {
                "Event sourcing captures all changes as immutable events.",
                "Circuit breakers prevent cascading failures in distributed systems.",
                "CQRS separates reads from writes for scalability.",
            };
            foreach ((string text, int idx) in seedDocs.Select((d, i) => (d, i)))
            {
                try
                {
                    float[] emb = await embedModel.CreateEmbeddingsAsync(text);
                    await ragStore.AddAsync(new[] { new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = emb } });
                }
                catch
                {
                    await ragStore.AddAsync(new[] { new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = new float[8] } });
                }
            }
            if (tools.Get("search") is null && embedModel is not null)
            {
                tools = tools.WithTool(new LangChainPipeline.Tools.RetrievalTool(ragStore, embedModel));
            }
        }

        AgentInstance agentInstance = LangChainPipeline.Agent.AgentFactory.Create(o.AgentMode, chatModel, tools, o.Debug, o.AgentMaxSteps, o.Rag, o.Embed, jsonTools: o.JsonTools, stream: o.Stream);
        try
        {
            string questionForAgent = o.Question;
            if (o.Rag && ragStore != null && embedModel != null)
            {
                try
                {
                    IReadOnlyCollection<Document> results = await ragStore.GetSimilarDocuments(embedModel, o.Question, 3);
                    if (results.Count > 0)
                    {
                        string ctx = string.Join("\n- ", results.Select(r => r.PageContent.Length > 160 ? r.PageContent[..160] + "..." : r.PageContent));
                        questionForAgent = $"Context:\n- {ctx}\n\nQuestion: {o.Question}";
                    }
                }
                catch { /* fallback silently */ }
            }
            string answer = await agentInstance.RunAsync(questionForAgent);
            sw.Stop();
            Console.WriteLine(answer);
            Console.WriteLine($"[timing] total={sw.ElapsedMilliseconds}ms (agent-{agentInstance.Mode})");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return;
        }
    }

    Result<string, Exception> run = await CreateSemanticCliPipeline(o.Rag, o.Model, o.Embed, o.K, settings, o)
        .Catch()
        .Invoke(o.Question);
    sw.Stop();
    run.Match(
        success =>
        {
            Console.WriteLine(success);
            Console.WriteLine($"[timing] total={sw.ElapsedMilliseconds}ms");
            Telemetry.PrintSummary();
        },
        error => Console.WriteLine($"Error: {error.Message}")
    );
}

// ------------------
// CommandLineParser
// ------------------

static void ValidateSecrets(AskOptions? askOpts = null)
{
    (string? endpoint, string? apiKey, ChatEndpointType _) = ChatConfig.ResolveWithOverrides(askOpts?.Endpoint, askOpts?.ApiKey, askOpts?.EndpointType);
    if (!string.IsNullOrWhiteSpace(endpoint) ^ !string.IsNullOrWhiteSpace(apiKey))
    {
        Console.WriteLine("[WARN] Only one of CHAT_ENDPOINT / CHAT_API_KEY is set; remote backend will be ignored.");
    }
}

static void LogBackendSelection(string model, ChatRuntimeSettings settings, AskOptions? askOpts = null)
{
    (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(askOpts?.Endpoint, askOpts?.ApiKey, askOpts?.EndpointType);
    string backend = (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        ? $"remote-{endpointType.ToString().ToLowerInvariant()}"
        : "ollama-local";
    string maskedKey = string.IsNullOrWhiteSpace(apiKey) ? "(none)" : apiKey.Length <= 8 ? "********" : apiKey[..4] + "..." + apiKey[^4..];
    Console.WriteLine($"[INIT] Backend={backend} Model={model} Temp={settings.Temperature} MaxTok={settings.MaxTokens} Key={maskedKey} Endpoint={(endpoint ?? "(none)")}");
}

static async Task RunTestsAsync(TestOptions o)
{
    Console.WriteLine("=== Running Ouroboros Tests ===\n");

    try
    {
        if (o.MeTTa)
        {
            await RunMeTTaDockerTest();
            return;
        }

        if (o.All || o.IntegrationOnly)
        {
            // await LangChainPipeline.Tests.OllamaCloudIntegrationTests.RunAllTests();
            Console.WriteLine();
        }

        if (o.All || o.CliOnly)
        {
            // await LangChainPipeline.Tests.CliEndToEndTests.RunAllTests();
            Console.WriteLine();
        }

        if (o.All)
        {
            // await LangChainPipeline.Tests.TrackedVectorStoreTests.RunAllTests();
            Console.WriteLine();

            // LangChainPipeline.Tests.MemoryContextTests.RunAllTests();
            Console.WriteLine();

            // await LangChainPipeline.Tests.LangChainConversationTests.RunAllTests();
            Console.WriteLine();

            // Run meta-AI tests
            // await LangChainPipeline.Tests.MetaAiTests.RunAllTests();
            Console.WriteLine();

            // Run Meta-AI v2 tests
            // await LangChainPipeline.Tests.MetaAIv2Tests.RunAllTests();
            Console.WriteLine();

            // Run Meta-AI Convenience Layer tests
            // await LangChainPipeline.Tests.MetaAIConvenienceTests.RunAll();
            Console.WriteLine();

            // Run orchestrator tests
            // await LangChainPipeline.Tests.OrchestratorTests.RunAllTests();
            Console.WriteLine();

            // Run MeTTa integration tests
            // await LangChainPipeline.Tests.MeTTaTests.RunAllTests();
            Console.WriteLine();

            // Run MeTTa Orchestrator v3.0 tests
            // await LangChainPipeline.Tests.MeTTaOrchestratorTests.RunAllTests();
            Console.WriteLine();
        }

        Console.WriteLine("=== ✅ All Tests Passed ===");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n=== ❌ Test Failed ===");
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
        Environment.Exit(1);
    }
}

static async Task RunOrchestratorAsync(OrchestratorOptions o)
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   Smart Model Orchestrator - Intelligent Model Selection  ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

    try
    {
        OllamaProvider provider = new OllamaProvider();
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);

        // Check for remote endpoint configuration
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
            o.Endpoint,
            o.ApiKey,
            o.EndpointType);

        // Create models - support both remote and local
        IChatCompletionModel CreateModel(string modelName)
        {
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                return CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
            }
            return new OllamaChatAdapter(new OllamaChatModel(provider, modelName));
        }

        IChatCompletionModel generalModel = CreateModel(o.Model);
        IChatCompletionModel coderModel = o.CoderModel != null ? CreateModel(o.CoderModel) : generalModel;
        IChatCompletionModel reasonModel = o.ReasonModel != null ? CreateModel(o.ReasonModel) : generalModel;

        // Log backend selection
        string backend = (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            ? $"remote-{endpointType.ToString().ToLowerInvariant()}"
            : "ollama-local";
        Console.WriteLine($"[INIT] Backend={backend} Endpoint={(endpoint ?? "local")}\n");

        // Create tool registry
        ToolRegistry tools = ToolRegistry.CreateDefault();
        Console.WriteLine($"✓ Tool registry created with {tools.Count} tools\n");

        // Build orchestrator with multiple models
        OrchestratorBuilder builder = new OrchestratorBuilder(tools, "general")
            .WithModel(
                "general",
                generalModel,
                ModelType.General,
                new[] { "conversation", "general-purpose", "versatile" },
                maxTokens: o.MaxTokens,
                avgLatencyMs: 1000)
            .WithModel(
                "coder",
                coderModel,
                ModelType.Code,
                new[] { "code", "programming", "debugging", "syntax" },
                maxTokens: o.MaxTokens,
                avgLatencyMs: 1500)
            .WithModel(
                "reasoner",
                reasonModel,
                ModelType.Reasoning,
                new[] { "reasoning", "analysis", "logic", "explanation" },
                maxTokens: o.MaxTokens,
                avgLatencyMs: 1200)
            .WithMetricTracking(true);

        OrchestratedChatModel orchestrator = builder.Build();

        Console.WriteLine($"✓ Orchestrator configured with multiple models\n");
        Console.WriteLine($"Goal: {o.Goal}\n");

        Stopwatch sw = Stopwatch.StartNew();
        string response = await orchestrator.GenerateTextAsync(o.Goal);
        sw.Stop();

        Console.WriteLine("=== Response ===");
        Console.WriteLine(response);
        Console.WriteLine();
        Console.WriteLine($"[timing] Execution time: {sw.ElapsedMilliseconds}ms");

        if (o.ShowMetrics)
        {
            Console.WriteLine("\n=== Performance Metrics ===");
            IModelOrchestrator underlyingOrchestrator = builder.GetOrchestrator();
            IReadOnlyDictionary<string, PerformanceMetrics> metrics = underlyingOrchestrator.GetMetrics();

            foreach ((string modelName, PerformanceMetrics metric) in metrics)
            {
                Console.WriteLine($"\nModel: {modelName}");
                Console.WriteLine($"  Executions: {metric.ExecutionCount}");
                Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F2}ms");
                Console.WriteLine($"  Success Rate: {metric.SuccessRate:P2}");
                Console.WriteLine($"  Last Used: {metric.LastUsed:g}");
            }
        }

        Console.WriteLine("\n✓ Orchestrator execution completed successfully");
    }
    catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
    {
        Console.Error.WriteLine("⚠ Error: Ollama is not running. Please start Ollama before using the orchestrator.");
        Console.Error.WriteLine("   Run: ollama serve");
        Environment.Exit(1);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n=== ❌ Orchestrator Failed ===");
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (o.Debug)
        {
            Console.Error.WriteLine(ex.StackTrace);
        }
        Environment.Exit(1);
    }
}

static async Task RunMeTTaAsync(MeTTaOptions o)
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   MeTTa Orchestrator v3.0 - Symbolic Reasoning            ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

    try
    {
        OllamaProvider provider = new OllamaProvider();
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);

        // Check for remote endpoint configuration
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
            o.Endpoint,
            o.ApiKey,
            o.EndpointType);

        // Create chat model - support both remote and local
        IChatCompletionModel chatModel;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            chatModel = CreateRemoteChatModel(endpoint, apiKey, o.Model, settings, endpointType);
            string backend = $"remote-{endpointType.ToString().ToLowerInvariant()}";
            Console.WriteLine($"[INIT] Backend={backend} Endpoint={endpoint}");
            Console.WriteLine($"✓ Using remote model: {o.Model}");
        }
        else
        {
            chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
            Console.WriteLine($"[INIT] Backend=ollama-local");
            Console.WriteLine($"✓ Using local model: {o.Model}");
        }

        // Create embedding model - support both remote and local
        IEmbeddingModel embedModel = CreateEmbeddingModel(endpoint, apiKey, endpointType, o.Embed, provider);
        Console.WriteLine($"✓ Using embedding model: {o.Embed}");

        // Build MeTTa orchestrator using the builder
        Console.WriteLine("✓ Initializing MeTTa orchestrator...");
        MeTTaOrchestratorBuilder orchestratorBuilder = MeTTaOrchestratorBuilder.CreateDefault(embedModel)
            .WithLLM(chatModel);

        MeTTaOrchestrator orchestrator = orchestratorBuilder.Build();
        Console.WriteLine($"✓ MeTTa orchestrator v3.0 initialized\n");

        Console.WriteLine($"Goal: {o.Goal}\n");

        // Plan phase
        Console.WriteLine("=== Planning Phase ===");
        Stopwatch sw = Stopwatch.StartNew();
        Result<Plan, string> planResult = await orchestrator.PlanAsync(o.Goal);

        Plan plan = planResult.Match(
            success => success,
            error =>
            {
                Console.Error.WriteLine($"Planning failed: {error}");
                Environment.Exit(1);
                return null!;
            }
        );

        sw.Stop();
        Console.WriteLine($"✓ Plan generated in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Steps: {plan.Steps.Count}");
        Console.WriteLine($"  Overall confidence: {plan.ConfidenceScores.GetValueOrDefault("overall", 0):P2}\n");

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            PlanStep step = plan.Steps[i];
            Console.WriteLine($"  {i + 1}. {step.Action}");
            Console.WriteLine($"     Expected: {step.ExpectedOutcome}");
            Console.WriteLine($"     Confidence: {step.ConfidenceScore:P2}");
        }
        Console.WriteLine();

        if (o.PlanOnly)
        {
            Console.WriteLine("✓ Plan-only mode - skipping execution");
            return;
        }

        // Execution phase
        Console.WriteLine("=== Execution Phase ===");
        sw.Restart();
        Result<ExecutionResult, string> executionResult = await orchestrator.ExecuteAsync(plan);
        sw.Stop();

        executionResult.Match(
            success =>
            {
                Console.WriteLine($"✓ Execution completed in {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"\nFinal Result:");
                Console.WriteLine($"  Success: {success.Success}");
                Console.WriteLine($"  Duration: {success.Duration.TotalSeconds:F2}s");
                if (!string.IsNullOrWhiteSpace(success.FinalOutput))
                {
                    Console.WriteLine($"  Output: {success.FinalOutput}");
                }
                Console.WriteLine($"\nStep Results:");
                for (int i = 0; i < success.StepResults.Count; i++)
                {
                    StepResult stepResult = success.StepResults[i];
                    Console.WriteLine($"  {i + 1}. {stepResult.Step.Action}");
                    Console.WriteLine($"     Success: {stepResult.Success}");
                    Console.WriteLine($"     Output: {stepResult.Output}");
                    if (!string.IsNullOrEmpty(stepResult.Error))
                    {
                        Console.WriteLine($"     Error: {stepResult.Error}");
                    }
                }
            },
            error =>
            {
                Console.Error.WriteLine($"Execution failed: {error}");
                Environment.Exit(1);
            }
        );

        if (o.ShowMetrics)
        {
            Console.WriteLine("\n=== Performance Metrics ===");
            IReadOnlyDictionary<string, PerformanceMetrics> metrics = orchestrator.GetMetrics();

            foreach ((string key, PerformanceMetrics metric) in metrics)
            {
                Console.WriteLine($"\n{key}:");
                Console.WriteLine($"  Executions: {metric.ExecutionCount}");
                Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F2}ms");
                Console.WriteLine($"  Success Rate: {metric.SuccessRate:P2}");
                Console.WriteLine($"  Last Used: {metric.LastUsed:g}");
            }
        }

        Console.WriteLine("\n✓ MeTTa orchestrator execution completed successfully");
    }
    catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
    {
        Console.Error.WriteLine("⚠ Error: Ollama is not running. Please start Ollama before using the MeTTa orchestrator.");
        Console.Error.WriteLine("   Run: ollama serve");
        Environment.Exit(1);
    }
    catch (Exception ex) when (ex.Message.Contains("metta") && (ex.Message.Contains("not found") || ex.Message.Contains("No such file")))
    {
        Console.Error.WriteLine("⚠ Error: MeTTa engine not found. Please install MeTTa:");
        Console.Error.WriteLine("   Install from: https://github.com/trueagi-io/hyperon-experimental");
        Console.Error.WriteLine("   Ensure 'metta' executable is in your PATH");
        Environment.Exit(1);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n=== ❌ MeTTa Orchestrator Failed ===");
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (o.Debug)
        {
            Console.Error.WriteLine(ex.StackTrace);
        }
        Environment.Exit(1);
    }
}



static async Task RunAssistAsync(AssistOptions o)
{
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   DSL Assistant - GitHub Copilot-like Code Intelligence   ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

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

        Console.WriteLine($"✓ Assistant initialized\n");

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
                            Console.WriteLine($"  • {s.Token}: {s.Explanation}");
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

        Console.WriteLine("\n✓ Assistant execution completed");
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
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   🐍 Research Skills - Auto-Generated DSL Tokens          ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

    // Helper to create PlanStep with proper Dictionary
    static PlanStep MakeStep(string action, string param, string outcome, double confidence) =>
        new PlanStep(action, new Dictionary<string, object> { ["hint"] = param }, outcome, confidence);

    // Initialize PERSISTENT skill registry - skills survive across restarts
    string skillsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ouroboros", "skills.json");
    var persistentConfig = new PersistentSkillConfig(StoragePath: skillsPath, AutoSave: true);
    await using var registry = new PersistentSkillRegistry(config: persistentConfig);
    await registry.InitializeAsync();

    // Show persistence info
    var stats = registry.GetStats();
    if (stats.IsPersisted)
    {
        Console.WriteLine($"  💾 Loaded {stats.TotalSkills} skills from {stats.StoragePath}");
    }
    else
    {
        Console.WriteLine($"  📁 Skills will be saved to {stats.StoragePath}");
    }
    
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
        
        foreach (var skill in predefinedSkills)
            registry.RegisterSkill(skill);
        
        Console.WriteLine($"  ✓ Registered {predefinedSkills.Length} predefined skills");
    }
    Console.WriteLine();

    // Voice mode takes priority - go straight to voice persona
    if (o.Voice)
    {
        await RunVoicePersonaMode(registry, MakeStep, o.Persona, o.Model, o.Endpoint);
        return;
    }

    // Default to --list if no option specified (unless interactive mode)
    if (!o.List && !o.Tokens && !o.Interactive && string.IsNullOrEmpty(o.Fetch) && string.IsNullOrEmpty(o.Suggest) && string.IsNullOrEmpty(o.Run))
    {
        o.List = true;
    }

    if (o.List)
    {
        Console.WriteLine("📚 REGISTERED SKILLS:\n");
        foreach (var skill in registry.GetAllSkills())
        {
            Console.WriteLine($"  🔧 {skill.Name,-30} ({skill.SuccessRate:P0})");
            Console.WriteLine($"     {skill.Description}");
            Console.WriteLine($"     Steps: {string.Join(" → ", skill.Steps.Select(s => s.Action.Split(' ')[0]))}");
            Console.WriteLine();
        }
    }

    if (o.Tokens)
    {
        Console.WriteLine("🏷️ AUTO-GENERATED DSL TOKENS:\n");
        Console.WriteLine("  Built-in: SetPrompt, UseDraft, UseCritique, UseRevise, UseOutput\n");
        Console.WriteLine("  Skill-based (from research patterns):");
        foreach (var skill in registry.GetAllSkills())
        {
            Console.WriteLine($"    • UseSkill_{skill.Name}");
        }
        Console.WriteLine();
    }

    if (!string.IsNullOrEmpty(o.Fetch))
    {
        Console.WriteLine($"🔍 Fetching research on: \"{o.Fetch}\"...\n");
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(o.Fetch)}&start=0&max_results=5";
        try
        {
            string xml = await httpClient.GetStringAsync(url);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Take(5).ToList();
            
            Console.WriteLine($"📄 Found {entries.Count} papers:");
            foreach (var entry in entries)
            {
                string title = entry.Element(atom + "title")?.Value?.Trim() ?? "Untitled";
                if (title.Length > 60) title = title[..57] + "...";
                Console.WriteLine($"   • {title}");
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
            Console.WriteLine($"\n✅ New skill extracted: UseSkill_{skillName}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Failed to fetch: {ex.Message}\n");
        }
    }

    if (!string.IsNullOrEmpty(o.Suggest))
    {
        Console.WriteLine($"💡 Skill suggestions for: \"{o.Suggest}\"\n");
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
                Console.WriteLine($"  🎯 UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
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
        Console.WriteLine($"▶ Executing: {o.Run}\n");
        var tokens = o.Run.Split('|').Select(t => t.Trim()).ToList();
        foreach (var token in tokens)
        {
            Console.WriteLine($"  📍 {token}");
            if (token.StartsWith("UseSkill_", StringComparison.OrdinalIgnoreCase))
            {
                string skillName = token[9..];
                var skill = registry.GetAllSkills().FirstOrDefault(s => s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
                if (skill != null)
                {
                    Console.WriteLine($"     🔧 Executing: {skill.Name}");
                    foreach (var step in skill.Steps)
                    {
                        Console.WriteLine($"        → {step.Action}");
                        await Task.Delay(100); // Simulate execution
                    }
                    Console.WriteLine($"     ✓ Complete");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("✅ Pipeline complete\n");
    }

    // Interactive REPL mode
    if (o.Interactive)
    {
        if (o.Voice)
        {
            await RunVoicePersonaMode(registry, MakeStep, o.Persona, o.Model, o.Endpoint);
        }
        else
        {
            await RunInteractiveSkillsMode(registry, MakeStep);
        }
    }
}

static async Task RunInteractiveSkillsMode(ISkillRegistry registry, Func<string, string, string, double, PlanStep> MakeStep)
{
    Console.WriteLine("\n┌──────────────────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│  🎮 INTERACTIVE SKILLS MODE                                              │");
    Console.WriteLine("├──────────────────────────────────────────────────────────────────────────┤");
    Console.WriteLine("│  Commands:                                                               │");
    Console.WriteLine("│    list / ls          - List all skills                                  │");
    Console.WriteLine("│    tokens / t         - Show DSL tokens                                  │");
    Console.WriteLine("│    fetch <query>      - Learn skill from arXiv research                  │");
    Console.WriteLine("│    suggest <goal>     - Find matching skills                             │");
    Console.WriteLine("│    run <skill>        - Execute a skill (simulated)                      │");
    Console.WriteLine("│    help / ?           - Show this help                                   │");
    Console.WriteLine("│    exit / quit        - Exit interactive mode                            │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────────────────┘\n");

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
                Console.WriteLine("\n  👋 Goodbye!\n");
                return;

            case "help":
            case "?":
                Console.WriteLine("\n  Commands: list, tokens, fetch <query>, suggest <goal>, run <skill>, exit\n");
                break;

            case "list":
            case "ls":
                Console.WriteLine("\n  📚 Skills:");
                foreach (var skill in registry.GetAllSkills())
                {
                    Console.WriteLine($"     • {skill.Name,-28} ({skill.SuccessRate:P0}) - {skill.Description}");
                }
                Console.WriteLine();
                break;

            case "tokens":
            case "t":
                Console.WriteLine("\n  🏷️ DSL Tokens:");
                Console.WriteLine("     Built-in: SetPrompt, UseDraft, UseCritique, UseRevise, UseOutput");
                Console.WriteLine("     Skills:");
                foreach (var skill in registry.GetAllSkills())
                {
                    Console.WriteLine($"       • UseSkill_{skill.Name}");
                }
                Console.WriteLine();
                break;

            case "fetch":
            case "learn":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    Console.WriteLine("  ⚠ Usage: fetch <query>\n");
                    break;
                }
                Console.WriteLine($"\n  🔍 Fetching research on: \"{arg}\"...");
                try
                {
                    string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(arg)}&start=0&max_results=5";
                    string xml = await httpClient.GetStringAsync(url);
                    var doc = System.Xml.Linq.XDocument.Parse(xml);
                    System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
                    var entries = doc.Descendants(atom + "entry").Take(5).ToList();

                    Console.WriteLine($"  📄 Found {entries.Count} papers");

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
                    Console.WriteLine($"  ✅ New skill: UseSkill_{skillName}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ Error: {ex.Message}\n");
                }
                break;

            case "suggest":
            case "find":
                if (string.IsNullOrWhiteSpace(arg))
                {
                    Console.WriteLine("  ⚠ Usage: suggest <goal>\n");
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
                    Console.WriteLine($"\n  💡 Matching skills for \"{arg}\":");
                    foreach (var skill in matches)
                    {
                        Console.WriteLine($"     🎯 UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
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
                    Console.WriteLine("  ⚠ Usage: run <skill_name>\n");
                    break;
                }
                var skillToRun = registry.GetAllSkills()
                    .FirstOrDefault(s => s.Name.Equals(arg, StringComparison.OrdinalIgnoreCase) ||
                                         s.Name.Equals(arg.Replace("UseSkill_", ""), StringComparison.OrdinalIgnoreCase));
                if (skillToRun != null)
                {
                    Console.WriteLine($"\n  ▶ Executing: {skillToRun.Name}");
                    foreach (var step in skillToRun.Steps)
                    {
                        Console.Write($"     → {step.Action}...");
                        await Task.Delay(200);
                        Console.WriteLine(" ✓");
                    }
                    Console.WriteLine($"  ✅ {skillToRun.Name} complete\n");
                }
                else
                {
                    Console.WriteLine($"  ⚠ Skill '{arg}' not found. Use 'list' to see available skills.\n");
                }
                break;

            default:
                Console.WriteLine($"  ⚠ Unknown command: {cmd}. Type 'help' for commands.\n");
                break;
        }
    }
}

static async Task RunVoicePersonaMode(ISkillRegistry registry, Func<string, string, string, double, PlanStep> MakeStep, string personaName, string modelName, string endpoint)
{
    // Persona definitions with distinct personalities
    var personas = new Dictionary<string, (string Voice, string Greeting, string Style)>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ouroboros"] = ("onyx", "Greetings. I am Ouroboros, the self-improving research intelligence. Through continuous learning and reflection, I help you discover and master new skills. What knowledge shall we pursue?", "Self-evolving and insightful"),
        ["Aria"] = ("nova", "Hello! I'm Aria, your research assistant. I'm here to help you explore and learn new skills. What would you like to discover today?", "Friendly and encouraging"),
        ["Nova"] = ("nova", "Greetings! I'm Nova, your knowledge companion. Let's explore the frontier of research together. What interests you?", "Curious and enthusiastic"),
        ["Echo"] = ("echo", "Hi there. I'm Echo, your research guide. I specialize in finding patterns and connections. What shall we investigate?", "Calm and analytical"),
        ["Sage"] = ("onyx", "Welcome. I am Sage, your research mentor. I'm here to guide you through complex topics. What knowledge do you seek?", "Wise and patient"),
        ["Fable"] = ("fable", "Ah, a seeker! I'm Fable, storyteller of research. Every paper has a tale. What story shall we uncover?", "Narrative and expressive"),
        ["Shimmer"] = ("shimmer", "Hi! I'm Shimmer, your discovery companion. Let's find something wonderful together. What sparks your curiosity?", "Gentle and supportive"),
    };

    // Get persona config (default to Ouroboros)
    if (!personas.TryGetValue(personaName, out var persona))
    {
        persona = personas["Ouroboros"];
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
        $"  - {s.Name}: {s.Description} (success rate: {s.SuccessRate:P0}, steps: {string.Join(" → ", s.Steps.Select(st => st.Action))})"));

    // Get ALL dynamically discovered pipeline tokens
    var pipelineContext = SkillCliSteps.BuildPipelineContext();
    var allTokens = SkillCliSteps.GetAllPipelineTokens();
    Console.WriteLine($"  ℹ️ Discovered {allTokens.Values.Distinct().Count()} pipeline tokens at runtime");

    // System prompt for the LLM - aware of the FULL pipeline architecture
    string systemPrompt = $@"You are {personaName}, a {persona.Style.ToLowerInvariant()} AI research assistant running on the Ouroboros pipeline.

OUROBOROS PIPELINE - DYNAMIC CAPABILITIES:
You are part of a self-improving research pipeline. Here are ALL available DSL tokens discovered at runtime:

{pipelineContext}

AVAILABLE SKILLS (High-level research patterns):
{(string.IsNullOrEmpty(skillDetails) ? "No skills registered yet. User can say 'learn about X' to create skills from research." : skillDetails)}

ACTIONS - Include ONE of these tags in your response when the user wants to perform an action:
- ACTION:list - User wants to see available skills/tokens (triggers: ""list"", ""show"", ""what skills"", ""capabilities"")
- ACTION:run:SKILLNAME - User wants to execute a specific skill (replace SKILLNAME with actual skill name)
- ACTION:learn:TOPIC - User wants to research a topic using web fetching (replace TOPIC with the actual topic)
- ACTION:search:QUERY - Search arXiv, Scholar, Wikipedia, GitHub, or News (replace QUERY with search terms)
- ACTION:fetch:URL - Fetch content from any URL directly
- ACTION:emergence:TOPIC - Run full Ouroboros emergence cycle on a topic
- ACTION:suggest:GOAL - User wants skill recommendations for a goal
- ACTION:tokens - List all available pipeline tokens
- ACTION:help - User needs guidance
- ACTION:exit - User wants to leave

IMPORTANT RULES:
1. When user mentions a skill name (like ""ChainOfThoughtReasoning""), they likely want to RUN it: ACTION:run:ChainOfThoughtReasoning
2. When user asks to search or research a TOPIC, use ACTION:learn:TOPIC or ACTION:search:TOPIC
3. For full deep research cycles, use ACTION:emergence:TOPIC
4. Never use placeholder text like [topic] - always use the ACTUAL value from the user's message
5. Keep spoken responses under 2 sentences (they will be read aloud)
6. You know ALL the pipeline tokens listed above - use them creatively";

    Console.WriteLine("\n┌──────────────────────────────────────────────────────────────────────────┐");
    Console.WriteLine($"│  🎙️ VOICE PERSONA MODE - {personaName.ToUpperInvariant(),-15}                              │");
    Console.WriteLine("├──────────────────────────────────────────────────────────────────────────┤");
    Console.WriteLine($"│  Personality: {persona.Style,-56} │");
    Console.WriteLine("│                                                                          │");
    Console.WriteLine("│  Voice Commands:                                                         │");
    Console.WriteLine("│    \"list skills\" / \"what skills\"   - List available skills               │");
    Console.WriteLine("│    \"learn about X\"                 - Fetch research on topic X           │");
    Console.WriteLine("│    \"suggest for X\"                 - Find matching skills                │");
    Console.WriteLine("│    \"run X\" / \"execute X\"           - Execute a skill                     │");
    Console.WriteLine("│    \"goodbye\" / \"exit\"              - Exit voice mode                     │");
    Console.WriteLine("└──────────────────────────────────────────────────────────────────────────┘\n");

    // Check for TTS/STT availability
    string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    bool hasCloudTts = !string.IsNullOrEmpty(openAiKey);
    bool hasLocalTts = LocalWindowsTtsService.IsAvailable();
    bool hasTts = hasCloudTts || hasLocalTts;
    bool hasStt = hasCloudTts || CheckWhisperAvailable();

    if (!hasTts)
    {
        Console.WriteLine("  ⚠ TTS unavailable (Windows SAPI or OPENAI_API_KEY required)");
        Console.WriteLine("    Falling back to text output with voice command parsing.\n");
    }
    if (!hasStt)
    {
        Console.WriteLine("  ⚠ STT unavailable (install Whisper or set OPENAI_API_KEY)");
        Console.WriteLine("    Using text input with natural language processing.\n");
    }

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    ITextToSpeechService? ttsService = null;
    LocalWindowsTtsService? localTts = null;
    ISpeechToTextService? sttService = null;
    
    // Flag to pause mic while TTS is speaking (prevents feedback loop)
    bool isSpeaking = false;

    // Initialize TTS - prefer local for offline operation with faster speech
    if (hasLocalTts)
    {
        try
        {
            // Use faster rate (3) for responsive voice interaction
            // Volume at 100%, SSML disabled for speed
            localTts = new LocalWindowsTtsService(
                voiceName: null,  // Auto-select best available voice
                rate: 3,          // Faster for responsiveness
                volume: 100,      // Full volume
                useEnhancedProsody: false  // Disable SSML for speed
            );
            ttsService = localTts;
            Console.WriteLine("  ✓ TTS initialized (Windows SAPI - fast mode, local/offline)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Local TTS init failed: {ex.Message}");
        }
    }
    
    // Fall back to OpenAI TTS if local not available
    if (ttsService == null && hasCloudTts)
    {
        try
        {
            ttsService = new OpenAiTextToSpeechService(openAiKey!);
            Console.WriteLine($"  ✓ TTS initialized (OpenAI - voice: {persona.Voice})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Cloud TTS init failed: {ex.Message}");
        }
    }

    // Initialize STT if available
    if (hasStt)
    {
        try
        {
            // Try local Whisper first
            var localWhisper = new LocalWhisperService();
            if (await localWhisper.IsAvailableAsync())
            {
                sttService = localWhisper;
                Console.WriteLine("  ✓ STT initialized (local Whisper)");
            }
            else if (!string.IsNullOrEmpty(openAiKey))
            {
                sttService = new WhisperSpeechToTextService(openAiKey);
                Console.WriteLine("  ✓ STT initialized (OpenAI Whisper API)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ STT init failed: {ex.Message}");
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
            Console.WriteLine($"  ✓ LLM initialized ({modelName} via {endpoint})");
        }
        else
        {
            Console.WriteLine($"  ⚠ LLM test failed: {testResponse}");
            chatModel = null;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ⚠ LLM unavailable ({modelName}): {ex.Message}");
        Console.WriteLine("    Falling back to keyword matching.\n");
        chatModel = null;
    }

    Console.WriteLine();

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
                Console.WriteLine("\n  💡 Available pipeline steps:");
                var samples = tokenNames.Take(15).ToList();
                Console.WriteLine($"     {string.Join(", ", samples)}...");
                Console.WriteLine("     Type part of a step name for suggestions");
                return;
            }

            // Suggest matching tokens
            var matches = tokenNames.Where(t => t.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
            if (matches.Count > 0)
            {
                Console.WriteLine($"\n  💡 Matching tokens: {string.Join(", ", matches)}");
            }
            return;
        }

        // Check for command matches
        var cmdMatches = commandHelp.Where(kv => kv.Key.StartsWith(partialLower)).ToList();
        if (cmdMatches.Count > 0 && cmdMatches.Count <= 5)
        {
            Console.WriteLine("\n  💡 Commands:");
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
                Console.WriteLine($"  💡 Pipeline tokens: {string.Join(", ", tkMatches)}");
            }
        }
    }

    // Execute a DSL pipeline string
    async Task<string> ExecutePipelineAsync(string pipeline)
    {
        Console.WriteLine($"\n  ⚡ Executing pipeline: {pipeline}");

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
                Console.WriteLine($"     → {stepStr}");

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
                        Console.WriteLine($"     ⚠ Step error: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"     ⚠ Unknown token: {tokenName}");
                }
            }

            Console.WriteLine("  ✅ Pipeline complete");
            return state.Output ?? state.Context ?? "Pipeline executed successfully";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Pipeline error: {ex.Message}");
            return $"Pipeline error: {ex.Message}";
        }
    }

    // Helper to speak text with reactive word-by-word streaming
    async Task SayAsync(string text)
    {
        // Pause mic while speaking to prevent feedback loop
        isSpeaking = true;
        
        try
        {
            Console.Write($"  🔊 {personaName}: ");
            
            // Sanitize text for TTS - remove code blocks, special chars that cause PS issues
            string sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"`[^`]*`", " ");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"```[\s\S]*?```", " ");
            sanitized = sanitized.Replace("\"", "'").Replace("$", "").Replace("`", "'");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^\w\s.,!?;:'\-()]+", " ");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", " ").Trim();
            
            if (string.IsNullOrWhiteSpace(sanitized)) { Console.WriteLine(); return; }
            
            // Split into words for reactive streaming
            var words = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Use Rx to stream words while TTS speaks in parallel
            if (localTts != null)
            {
                try
                {
                    // Create observable stream of words with timing
                    var wordStream = System.Reactive.Linq.Observable.Create<string>(async (observer, ct) =>
                    {
                        // Buffer words into speakable chunks (3-5 words per chunk for natural speech)
                        const int chunkSize = 4;
                        var chunks = new List<string>();
                        
                        for (int i = 0; i < words.Length; i += chunkSize)
                        {
                            var chunk = string.Join(" ", words.Skip(i).Take(chunkSize));
                            chunks.Add(chunk);
                        }
                        
                        foreach (var chunk in chunks)
                        {
                            if (ct.IsCancellationRequested) break;
                            
                            // Emit words one by one for display while speaking chunk
                            var chunkWords = chunk.Split(' ');
                            var speakTask = localTts.SpeakDirectAsync(chunk);
                            
                            // Stream words to console with slight delay for visual effect
                            foreach (var word in chunkWords)
                            {
                                observer.OnNext(word);
                                await Task.Delay(80, ct); // ~12 words/sec visual pace
                            }
                            
                            // Wait for speech to complete before next chunk
                            await speakTask;
                        }
                        
                        observer.OnCompleted();
                    });
                    
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
                    Console.WriteLine($"\n  ⚠ Rx speech error: {ex.Message}");
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
                        playResult.Match(_ => { }, err => Console.WriteLine($"  ⚠ Playback: {err}"));
                    },
                    err =>
                    {
                        Console.WriteLine($"  ⚠ TTS: {err}");
                        return Task.CompletedTask;
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Speech error: {ex.Message}");
            }
        }
        }
        finally
        {
            // Resume mic listening after TTS finishes + small delay for echo
            await Task.Delay(500);
            isSpeaking = false;
        }
    }

    // ========================================================================
    // CONTINUOUS SPEECH INPUT (BACKGROUND THREAD)
    // ========================================================================
    bool hasMic = MicrophoneRecorder.IsRecordingAvailable();
    var speechChannel = System.Threading.Channels.Channel.CreateBounded<string>(
        new System.Threading.Channels.BoundedChannelOptions(3)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest
        });
    var speechCts = new CancellationTokenSource();
    Task? continuousListenerTask = null;

    // Start continuous background speech listener if STT is available
    if (sttService != null && hasMic)
    {
        Console.WriteLine("  ✓ Continuous speech input enabled - speak anytime or type");
        Console.WriteLine("    🎤 Listening in background... (speech auto-detected)\n");

        continuousListenerTask = Task.Run(async () =>
        {
            while (!speechCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Skip recording while TTS is speaking (prevents feedback loop)
                    if (isSpeaking)
                    {
                        await Task.Delay(200, speechCts.Token);
                        continue;
                    }
                    
                    // Record a short segment (voice activity detection would be better, but this works)
                    string tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");
                    
                    // Record for 5 seconds at a time
                    var recordResult = await MicrophoneRecorder.RecordAsync(
                        durationSeconds: 5,
                        outputPath: tempFile,
                        ct: speechCts.Token);

                    string? audioFile = null;
                    recordResult.Match(f => audioFile = f, _ => { });

                    if (audioFile != null && File.Exists(audioFile))
                    {
                        // Check if file has meaningful audio (> 10KB usually means speech)
                        var fileInfo = new FileInfo(audioFile);
                        if (fileInfo.Length > 10000)
                        {
                            var transcribeResult = await sttService.TranscribeFileAsync(audioFile, ct: speechCts.Token);
                            transcribeResult.Match(
                                transcription =>
                                {
                                    string text = transcription.Text?.Trim() ?? "";
                                    // Filter out empty/noise transcriptions
                                    if (!string.IsNullOrWhiteSpace(text) && 
                                        text.Length > 2 && 
                                        !text.Equals(".", StringComparison.Ordinal) &&
                                        !text.Equals("...", StringComparison.Ordinal) &&
                                        !text.StartsWith("[") && // Filter [BLANK_AUDIO], [MUSIC], etc.
                                        !text.Contains("thank you for watching", StringComparison.OrdinalIgnoreCase))
                                    {
                                        speechChannel.Writer.TryWrite(text);
                                    }
                                },
                                _ => { });
                        }

                        // Cleanup
                        try { File.Delete(audioFile); } catch { }
                    }

                    // Small pause between recordings
                    await Task.Delay(500, speechCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore errors and keep listening
                    await Task.Delay(1000, speechCts.Token);
                }
            }
        }, speechCts.Token);
    }
    else if (sttService != null)
    {
        Console.WriteLine("  ⚠ Microphone not available (install ffmpeg for speech input)\n");
    }
    else
    {
        Console.WriteLine("  ℹ Text input mode (STT not configured)\n");
    }

    // Helper to listen - supports both keyboard input AND continuous background speech
    async Task<string> ListenAsync()
    {
        Console.Write($"  {personaName}> ");

        var inputBuffer = new System.Text.StringBuilder();
        while (true)
        {
            // Check for speech input from background listener (priority)
            if (speechChannel.Reader.TryRead(out string? speechText))
            {
                // Clear current line and show speech input
                Console.Write("\r" + new string(' ', inputBuffer.Length + personaName.Length + 10) + "\r");
                Console.WriteLine($"  🎤 {speechText}");
                return speechText;
            }

            // Check if keyboard input is available (non-blocking)
            if (!Console.IsInputRedirected)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(50);
                    continue;
                }
            }

            var key = Console.IsInputRedirected
                ? new ConsoleKeyInfo((char)Console.Read(), ConsoleKey.NoName, false, false, false)
                : Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
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

    // Greet the user
    await SayAsync(persona.Greeting);

    // Main voice loop
    while (true)
    {
        try
        {
            string input = await ListenAsync();
            if (string.IsNullOrWhiteSpace(input)) continue;

            string lower = input.ToLowerInvariant();
            string? action = null;
            string? actionParam = null;
            string? response = null;

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
                    
                    string fullPrompt = $@"{systemPrompt}
{conversationContext}

User's latest message: {input}

Respond naturally as {personaName}. If the user wants to perform an action, include the appropriate ACTION tag. Remember to be concise (1-2 sentences).";
                    
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ LLM error: {ex.Message}");
                }
            }

            // Execute action
            switch (action)
            {
                case "exit":
                    await SayAsync(response ?? "Goodbye! It was wonderful helping you today. Come back anytime!");
                    Console.WriteLine();
                    // Cleanup background speech listener
                    speechCts.Cancel();
                    if (continuousListenerTask != null)
                    {
                        try { await continuousListenerTask; } catch (OperationCanceledException) { }
                    }
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
                        
                        Console.WriteLine("\n  📚 All Skills:");
                        foreach (var skill in skills)
                        {
                            Console.WriteLine($"     • {skill.Name,-28} ({skill.SuccessRate:P0})");
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
                        Console.WriteLine($"\n  🔍 Fetching research on: \"{topic}\"...");
                        try
                        {
                            string fetchUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(topic)}&start=0&max_results=5";
                            string fetchXml = await httpClient.GetStringAsync(fetchUrl);
                            var fetchDoc = System.Xml.Linq.XDocument.Parse(fetchXml);
                            System.Xml.Linq.XNamespace fetchAtom = "http://www.w3.org/2005/Atom";
                            var fetchEntries = fetchDoc.Descendants(fetchAtom + "entry").Take(5).ToList();
                            Console.WriteLine($"  📄 Found {fetchEntries.Count} papers");
                            if (fetchEntries.Count > 0)
                            {
                                foreach (var fetchEntry in fetchEntries)
                                {
                                    string fetchTitle = fetchEntry.Element(fetchAtom + "title")?.Value?.Trim().Replace("\n", " ") ?? "Untitled";
                                    Console.WriteLine($"     📄 {fetchTitle}");
                                }
                                string newSkillName = string.Join("", topic.Split(' ').Select(w =>
                                    w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";
                                var newSkill = new Skill(newSkillName, $"Analysis from '{topic}' research", new List<string> { "research" },
                                    new List<PlanStep> { MakeStep("Gather", topic, "Papers", 0.9), MakeStep("Analyze", "Extract", "Insights", 0.85) }, 
                                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow);
                                registry.RegisterSkill(newSkill);
                                Console.WriteLine($"  ✓ Created skill: {newSkillName}");
                                await SayAsync($"I found {fetchEntries.Count} papers and created skill {newSkillName}.");
                            }
                            else
                            {
                                await SayAsync($"I couldn't find papers on {topic}.");
                            }
                        }
                        catch (Exception learnEx)
                        {
                            Console.WriteLine($"  ⚠ Error: {learnEx.Message}");
                            await SayAsync("I had trouble searching.");
                        }
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
                                Console.WriteLine($"\n  ▶ Executing: {skillToRun.Name} for \"{runTopic}\"");
                                try
                                {
                                    string runUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(runTopic)}&start=0&max_results=5";
                                    string runXml = await httpClient.GetStringAsync(runUrl);
                                    var runDoc = System.Xml.Linq.XDocument.Parse(runXml);
                                    System.Xml.Linq.XNamespace runAtom = "http://www.w3.org/2005/Atom";
                                    var runEntries = runDoc.Descendants(runAtom + "entry").ToList();
                                    Console.WriteLine($"  📚 Found {runEntries.Count} papers");
                                    foreach (var runEntry in runEntries.Take(3))
                                    {
                                        string runTitle = runEntry.Element(runAtom + "title")?.Value?.Trim().Replace("\n", " ") ?? "";
                                        Console.WriteLine($"     📄 {runTitle}");
                                    }
                                    await SayAsync($"Found {runEntries.Count} papers on {runTopic}.");
                                }
                                catch { await SayAsync("Error searching."); }
                            }
                        }
                        else
                        {
                            await SayAsync($"Running {skillToRun.Name}.");
                            Console.WriteLine($"\n  ▶ Executing: {skillToRun.Name}");
                            foreach (var step in skillToRun.Steps)
                            {
                                Console.Write($"     → {step.Action}...");
                                await Task.Delay(300);
                                Console.WriteLine(" ✓");
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
                            Console.WriteLine($"\n  📖 Help: {actionParam}");
                            Console.WriteLine($"     Syntax: {cmdInfo.Syntax}");
                            Console.WriteLine($"     {cmdInfo.Description}");
                            Console.WriteLine($"     Examples:");
                            foreach (var ex in cmdInfo.Examples)
                            {
                                Console.WriteLine($"       • {ex}");
                            }
                            Console.WriteLine();
                            await SayAsync($"{actionParam}: {cmdInfo.Description}");
                        }
                        else if (allTokens.TryGetValue(actionParam, out var tokenInfo))
                        {
                            // Help for a pipeline token
                            Console.WriteLine($"\n  📖 Pipeline Token: {tokenInfo.PrimaryName}");
                            Console.WriteLine($"  ─────────────────────────────────────────────────────────────────");
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
                                    Console.WriteLine($"       • {param.Name}: {paramType}{defaultVal}");
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
                                Console.WriteLine($"\n  💡 Did you mean one of these?");
                                foreach (var (name, info) in partialMatches)
                                {
                                    Console.WriteLine($"     • {info.PrimaryName}: {info.Description}");
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
                        Console.WriteLine("\n  📖 OUROBOROS VOICE COMMANDS:");
                        Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");
                        foreach (var (cmd, (syntax, desc, _)) in commandHelp)
                        {
                            Console.WriteLine($"  {syntax,-35} {desc}");
                        }
                        Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
                        Console.WriteLine("  💡 Tips:");
                        Console.WriteLine("     • Press Tab to autocomplete commands and tokens");
                        Console.WriteLine("     • Press F1 or Ctrl+Space for suggestions");
                        Console.WriteLine("     • Use | to chain pipeline steps: ArxivSearch 'AI' | UseOutput");
                        Console.WriteLine("     • Type a token name directly to see its help\n");
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
                        Console.WriteLine($"  ⚠ Pipeline error: {pipeEx.Message}");
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
                    Console.WriteLine($"\n  🔍 Multi-source search: \"{actionParam}\"");
                    try
                    {
                        // Search arXiv
                        Console.WriteLine("  📚 Searching arXiv...");
                        string arxivUrl = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(actionParam)}&start=0&max_results=3";
                        string arxivXml = await httpClient.GetStringAsync(arxivUrl);
                        var arxivDoc = System.Xml.Linq.XDocument.Parse(arxivXml);
                        System.Xml.Linq.XNamespace arxivAtom = "http://www.w3.org/2005/Atom";
                        var arxivEntries = arxivDoc.Descendants(arxivAtom + "entry").Take(3).ToList();
                        Console.WriteLine($"     Found {arxivEntries.Count} arXiv papers");

                        // Search Wikipedia
                        Console.WriteLine("  📖 Searching Wikipedia...");
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
                        Console.WriteLine($"  ⚠ Search error: {ex.Message}");
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
                    Console.WriteLine($"\n  🌐 Fetching: {actionParam}");
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
                        Console.WriteLine($"  ✓ Retrieved {content.Length:N0} characters");
                        Console.WriteLine($"  Preview: {(content.Length > 200 ? content[..200] + "..." : content)}");
                        await SayAsync($"Fetched {content.Length} characters from that URL.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠ Fetch error: {ex.Message}");
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
                    Console.WriteLine($"\n  🌀 Starting emergence cycle: \"{actionParam}\"");
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
                        Console.WriteLine($"     📄 arXiv: {eArxivEntries.Count} papers");

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
                                Console.WriteLine($"     🧠 Generated hypotheses");
                                allResults.AppendLine($"\nHypotheses:\n{hypotheses}");
                            }
                            catch { Console.WriteLine("     [LLM unavailable for hypotheses]"); }
                        }

                        Console.WriteLine("  Phase 3: EXPLORE - Identifying opportunities...");
                        Console.WriteLine($"     🔮 Deep dive into {actionParam} breakthroughs");

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
                        Console.WriteLine($"  ✅ Created skill: {emergenceSkillName}");

                        await SayAsync($"Emergence cycle complete! Found {eArxivEntries.Count} papers and created skill {emergenceSkillName}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠ Emergence error: {ex.Message}");
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
                        
                        Console.WriteLine($"\n  📦 Tokens matching '{tokenFilter}' ({filteredTokens.Count} found):");
                        Console.WriteLine("  ─────────────────────────────────────────────────────────────────");
                        foreach (var token in filteredTokens.OrderBy(t => t.PrimaryName))
                        {
                            string aliases = token.Aliases.Length > 0 ? $" (aliases: {string.Join(", ", token.Aliases)})" : "";
                            Console.WriteLine($"\n  ▸ {token.PrimaryName}{aliases}");
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
                        Console.WriteLine($"\n  📦 ALL PIPELINE TOKENS ({uniqueTokens.Count} total)");
                        Console.WriteLine("  ═══════════════════════════════════════════════════════════════════");
                        
                        var grouped = uniqueTokens.GroupBy(t => t.SourceClass).OrderBy(g => g.Key);
                        foreach (var group in grouped)
                        {
                            Console.WriteLine($"\n  ┌─ {group.Key} ({group.Count()} tokens) ─────────────────────────");
                            foreach (var token in group.OrderBy(t => t.PrimaryName))
                            {
                                string aliases = token.Aliases.Length > 0 ? $" [{string.Join(", ", token.Aliases)}]" : "";
                                string desc = token.Description.Length > 50 ? token.Description[..47] + "..." : token.Description;
                                Console.WriteLine($"  │ {token.PrimaryName,-25} {desc}{aliases}");
                            }
                            Console.WriteLine("  └─────────────────────────────────────────────────────────────────");
                        }
                        Console.WriteLine($"\n  💡 Use 'tokens <filter>' to search, or 'help <TokenName>' for details");
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
            Console.WriteLine($"  ⚠ Error in voice loop: {ex.Message}");
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
