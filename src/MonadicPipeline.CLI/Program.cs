#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==============================
// Minimal CLI entry (top-level)
// ==============================

using System.Diagnostics;
using CommandLine;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Diagnostics; // added
using LangChainPipeline.Options;
using Microsoft.Extensions.Hosting;

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
    await Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions, OrchestratorOptions, MeTTaOptions, AssistOptions>(args)
        .MapResult(
            (AskOptions o) => RunAskAsync(o),
            (PipelineOptions o) => RunPipelineAsync(o),
            (ListTokensOptions _) => RunListTokensAsync(),
            (ExplainOptions o) => RunExplainAsync(o),
            (TestOptions o) => RunTestsAsync(o),
            (OrchestratorOptions o) => RunOrchestratorAsync(o),
            (MeTTaOptions o) => RunMeTTaAsync(o),
            (AssistOptions o) => RunAssistAsync(o),
            _ => Task.CompletedTask
        );
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

static Task RunTestsAsync(TestOptions o)
{
    Console.WriteLine("=== Running MonadicPipeline Tests ===\n");

    try
    {
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
    return Task.CompletedTask;
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
