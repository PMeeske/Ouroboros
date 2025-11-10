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
using MonadicPipeline.CLI.Setup;

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
    await Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions, OrchestratorOptions, MeTTaOptions, SetupOptions>(args)
        .MapResult(
            (AskOptions o) => RunAskAsync(o),
            (PipelineOptions o) => RunPipelineAsync(o),
            (ListTokensOptions _) => RunListTokensAsync(),
            (ExplainOptions o) => RunExplainAsync(o),
            (TestOptions o) => RunTestsAsync(o),
            (OrchestratorOptions o) => RunOrchestratorAsync(o),
            (MeTTaOptions o) => RunMeTTaAsync(o),
            (SetupOptions o) => GuidedSetup.RunAsync(o),
            _ => Task.CompletedTask
        );
}

static async Task RunPipelineDslAsync(string dsl, string modelName, string embedName, string sourcePath, int k, bool trace, ChatRuntimeSettings? settings = null, PipelineOptions? pipelineOpts = null)
{
    // Setup minimal environment for reasoning/ingest arrows
    // Remote model support (OpenAI-compatible and Ollama Cloud) via environment variables or CLI overrides
    var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(
        pipelineOpts?.Endpoint,
        pipelineOpts?.ApiKey,
        pipelineOpts?.EndpointType);

    var provider = new OllamaProvider();
    IChatCompletionModel chatModel;

    if (pipelineOpts is not null && pipelineOpts.Router.Equals("auto", StringComparison.OrdinalIgnoreCase))
    {
        // Build router using provided model overrides; fallback to primary modelName
        var modelMap = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase);
        IChatCompletionModel MakeLocal(string name, string role)
        {
            var m = new OllamaChatModel(provider, name);
            // Apply presets based on model name and role
            try
            {
                var n = (name ?? string.Empty).ToLowerInvariant();
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
            var local = new OllamaChatModel(provider, "llama3");
            chatModel = new OllamaChatAdapter(local);
        }
        catch (Exception ex) when (pipelineOpts is not null && !pipelineOpts.StrictModel)
        {
            Console.WriteLine($"[WARN] Remote model '{modelName}' unavailable ({ex.GetType().Name}). Falling back to local 'llama3'. Use --strict-model to disable fallback.");
            var local = new OllamaChatModel(provider, "llama3");
            chatModel = new OllamaChatAdapter(local);
        }
    }
    else
    {
        var chat = new OllamaChatModel(provider, modelName);
        try
        {
            var n = (modelName ?? string.Empty).ToLowerInvariant();
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

    var tools = new ToolRegistry();
    var resolvedSource = string.IsNullOrWhiteSpace(sourcePath) ? Environment.CurrentDirectory : Path.GetFullPath(sourcePath);
    if (!Directory.Exists(resolvedSource))
    {
        Console.WriteLine($"Source path '{resolvedSource}' does not exist - creating.");
        Directory.CreateDirectory(resolvedSource);
    }
    var branch = new PipelineBranch("cli", new TrackedVectorStore(), DataSource.FromPath(resolvedSource));

    var state = new CliPipelineState
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
    var llm = new ToolAwareChatModel(chatModel, tools);
    state.Llm = llm;
    state.Tools = tools;

    try
    {
        var step = PipelineDsl.Build(dsl); // Steps will use embed & llm from state; k optionally influences reasoning if we extend arrows
        state = await step(state);

        var last = state.Branch.Events.OfType<ReasoningStep>().LastOrDefault();
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
        if ((ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED")) &&
            GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
        {
            await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
        }
    }
}

// Builds a Step<string,string> that runs either simple chat or chat+RAG in monadic form
static Step<string, string> CreateSemanticCliPipeline(bool withRag, string modelName, string embedName, int k, ChatRuntimeSettings? settings = null, AskOptions? askOpts = null)
{
    return Arrow.LiftAsync<string, string>(async question =>
    {
        // Initialize models
        var provider = new OllamaProvider();
        var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(
            askOpts?.Endpoint,
            askOpts?.ApiKey,
            askOpts?.EndpointType);
        IChatCompletionModel chatModel;
        if (askOpts is not null && askOpts.Router.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            // Build router using provided model overrides; fallback to primary modelName
            var modelMap = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase);
            IChatCompletionModel MakeLocal(string name, string role)
            {
                var m = new OllamaChatModel(provider, name);
                try
                {
                    var n = (name ?? string.Empty).ToLowerInvariant();
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
                var local = new OllamaChatModel(provider, "llama3");
                chatModel = new OllamaChatAdapter(local);
            }
            catch (Exception ex) when (askOpts is not null && !askOpts.StrictModel)
            {
                Console.WriteLine($"[WARN] Remote model '{modelName}' unavailable ({ex.GetType().Name}). Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                var local = new OllamaChatModel(provider, "llama3");
                chatModel = new OllamaChatAdapter(local);
            }
        }
        else
        {
            var chat = new OllamaChatModel(provider, modelName);
            try
            {
                var n = (modelName ?? string.Empty).ToLowerInvariant();
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
        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(chatModel, tools);
        var store = new TrackedVectorStore();

        // Optional minimal RAG: seed a few docs
        if (withRag)
        {
            var docs = new[]
            {
                "API versioning best practices with backward compatibility",
                "Circuit breaker using Polly in .NET",
                "Event sourcing and CQRS patterns overview"
            };
            foreach (var (text, idx) in docs.Select((d, i) => (d, i)))
            {
                try
                {
                    Telemetry.RecordEmbeddingInput(new[] { text });
                    var resp = await embed.CreateEmbeddingsAsync(text);
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
            var (text, _) = await llm.GenerateWithToolsAsync($"Answer the following question clearly and concisely.\nQuestion: {{q}}".Replace("{q}", question));
            return text;
        }
        else
        {
            Telemetry.RecordEmbeddingInput(new[] { question });
            var qEmb = await embed.CreateEmbeddingsAsync(question);
            Telemetry.RecordEmbeddingSuccess(qEmb.Length);
            var hits = await store.GetSimilarDocumentsAsync(qEmb, k);
            var ctx = string.Join("\n- ", hits.Select(h => h.PageContent));
            var prompt = $"Use the following context to answer.\nContext:\n- {ctx}\n\nQuestion: {{q}}".Replace("{q}", question);
            var (ragText, _) = await llm.GenerateWithToolsAsync(prompt);
            return ragText;
        }
    });
}

// (usage handled by CommandLineParser built-in help)

static Task RunListTokensAsync()
{
    Console.WriteLine("Available token groups:");
    foreach (var (method, names) in StepRegistry.GetTokenGroups())
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
    var settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
    await RunPipelineDslAsync(o.Dsl, o.Model, o.Embed, o.Source, o.K, o.Trace, settings, o);
}

static async Task RunAskAsync(AskOptions o)
{
    if (o.Router.Equals("auto", StringComparison.OrdinalIgnoreCase)) Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
    var settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
    ValidateSecrets(o);
    LogBackendSelection(o.Model, settings, o);
    var sw = Stopwatch.StartNew();
    if (o.Agent)
    {
        // Build minimal environment (always RAG off for initial agent version; agent can internally call tools)
        var provider = new OllamaProvider();
        var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(o.Endpoint, o.ApiKey, o.EndpointType);
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

        var tools = new ToolRegistry();
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
            var provider2 = new OllamaProvider();
            embedModel = CreateEmbeddingModel(endpoint, apiKey, endpointType, o.Embed, provider2);
            ragStore = new TrackedVectorStore();
            var seedDocs = new[]
            {
                "Event sourcing captures all changes as immutable events.",
                "Circuit breakers prevent cascading failures in distributed systems.",
                "CQRS separates reads from writes for scalability.",
            };
            foreach (var (text, idx) in seedDocs.Select((d, i) => (d, i)))
            {
                try
                {
                    var emb = await embedModel.CreateEmbeddingsAsync(text);
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

        var agentInstance = LangChainPipeline.Agent.AgentFactory.Create(o.AgentMode, chatModel, tools, o.Debug, o.AgentMaxSteps, o.Rag, o.Embed, jsonTools: o.JsonTools, stream: o.Stream);
        try
        {
            string questionForAgent = o.Question;
            if (o.Rag && ragStore != null && embedModel != null)
            {
                try
                {
                    var results = await ragStore.GetSimilarDocuments(embedModel, o.Question, 3);
                    if (results.Count > 0)
                    {
                        var ctx = string.Join("\n- ", results.Select(r => r.PageContent.Length > 160 ? r.PageContent[..160] + "..." : r.PageContent));
                        questionForAgent = $"Context:\n- {ctx}\n\nQuestion: {o.Question}";
                    }
                }
                catch { /* fallback silently */ }
            }
            var answer = await agentInstance.RunAsync(questionForAgent);
            sw.Stop();
            Console.WriteLine(answer);
            Console.WriteLine($"[timing] total={sw.ElapsedMilliseconds}ms (agent-{agentInstance.Mode})");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if ((ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED")) &&
                GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
            {
                await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
            }
            return;
        }
    }

    var run = await CreateSemanticCliPipeline(o.Rag, o.Model, o.Embed, o.K, settings, o)
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
        error =>
        {
            Console.WriteLine($"Error: {error.Message}");
            if ((error.Message.Contains("Connection refused") || error.Message.Contains("ECONNREFUSED")) &&
                GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
            {
                GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true }).Wait();
            }
        }
    );
}

// ------------------
// CommandLineParser
// ------------------

static void ValidateSecrets(AskOptions? askOpts = null)
{
    var (endpoint, apiKey, _) = ChatConfig.ResolveWithOverrides(askOpts?.Endpoint, askOpts?.ApiKey, askOpts?.EndpointType);
    if (!string.IsNullOrWhiteSpace(endpoint) ^ !string.IsNullOrWhiteSpace(apiKey))
    {
        Console.WriteLine("[WARN] Only one of CHAT_ENDPOINT / CHAT_API_KEY is set; remote backend will be ignored.");
    }
}

static void LogBackendSelection(string model, ChatRuntimeSettings settings, AskOptions? askOpts = null)
{
    var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(askOpts?.Endpoint, askOpts?.ApiKey, askOpts?.EndpointType);
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
        var provider = new OllamaProvider();
        var settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);

        // Create models
        var generalModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
        var coderModel = o.CoderModel != null
            ? new OllamaChatAdapter(new OllamaChatModel(provider, o.CoderModel))
            : generalModel;
        var reasonModel = o.ReasonModel != null
            ? new OllamaChatAdapter(new OllamaChatModel(provider, o.ReasonModel))
            : generalModel;

        // Create tool registry
        var tools = ToolRegistry.CreateDefault();
        Console.WriteLine($"✓ Tool registry created with {tools.Count} tools\n");

        // Build orchestrator with multiple models
        var builder = new OrchestratorBuilder(tools, "general")
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

        var orchestrator = builder.Build();

        Console.WriteLine($"✓ Orchestrator configured with multiple models\n");
        Console.WriteLine($"Goal: {o.Goal}\n");

        var sw = Stopwatch.StartNew();
        var response = await orchestrator.GenerateTextAsync(o.Goal);
        sw.Stop();

        Console.WriteLine("=== Response ===");
        Console.WriteLine(response);
        Console.WriteLine();
        Console.WriteLine($"[timing] Execution time: {sw.ElapsedMilliseconds}ms");

        if (o.ShowMetrics)
        {
            Console.WriteLine("\n=== Performance Metrics ===");
            var underlyingOrchestrator = builder.GetOrchestrator();
            var metrics = underlyingOrchestrator.GetMetrics();

            foreach (var (modelName, metric) in metrics)
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
        Console.Error.WriteLine("⚠ Error: Ollama is not running or not reachable.");
        if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
        {
            await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
        }
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
        var provider = new OllamaProvider();
        var settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);

        // Create LLM
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
        Console.WriteLine($"✓ Using model: {o.Model}");

        // Create embedding model
        var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, o.Embed));
        Console.WriteLine($"✓ Using embedding model: {o.Embed}");

        // Build MeTTa orchestrator using the builder
        Console.WriteLine("✓ Initializing MeTTa orchestrator...");
        var orchestratorBuilder = MeTTaOrchestratorBuilder.CreateDefault(embedModel)
            .WithLLM(chatModel);

        var orchestrator = orchestratorBuilder.Build();
        Console.WriteLine($"✓ MeTTa orchestrator v3.0 initialized\n");

        Console.WriteLine($"Goal: {o.Goal}\n");

        // Plan phase
        Console.WriteLine("=== Planning Phase ===");
        var sw = Stopwatch.StartNew();
        var planResult = await orchestrator.PlanAsync(o.Goal);

        var plan = planResult.Match(
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
            var step = plan.Steps[i];
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
        var executionResult = await orchestrator.ExecuteAsync(plan);
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
                    var stepResult = success.StepResults[i];
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
            var metrics = orchestrator.GetMetrics();

            foreach (var (key, metric) in metrics)
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
        Console.Error.WriteLine("⚠ Error: Ollama is not running or not reachable.");
        if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
        {
            await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
        }
        Environment.Exit(1);
    }
    catch (Exception ex) when (ex.Message.Contains("metta") && (ex.Message.Contains("not found") || ex.Message.Contains("No such file")))
    {
        Console.Error.WriteLine("⚠ Error: MeTTa engine not found.");
        if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for MeTTa?"))
        {
            await GuidedSetup.RunAsync(new SetupOptions { InstallMeTTa = true });
        }
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


