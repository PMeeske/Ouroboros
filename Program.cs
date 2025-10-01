// ==============================
// Minimal CLI entry (top-level)
// ==============================

using LangChain.Providers.Ollama;
using LangChain.DocumentLoaders;
using LangChain.Databases;
using LangChainPipeline.CLI;
using CommandLine;
using LangChainPipeline.Options;
using System.Diagnostics;
using LangChainPipeline.Diagnostics; // added
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
    await Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args)
        .MapResult(
            (AskOptions o) => RunAskAsync(o),
            (PipelineOptions o) => RunPipelineAsync(o),
            (ListTokensOptions _) => RunListTokensAsync(),
            (ExplainOptions o) => RunExplainAsync(o),
            (TestOptions o) => RunTestsAsync(o),
            _ => Task.CompletedTask
        );
}

static async Task RunPipelineDslAsync(string dsl, string modelName, string embedName, string sourcePath, int k, bool trace, ChatRuntimeSettings? settings = null)
{
    // Setup minimal environment for reasoning/ingest arrows
    // Remote model support (OpenAI-compatible and Ollama Cloud) via environment variables only inside this function
    var (endpoint, apiKey, endpointType) = ChatConfig.Resolve();

    var provider = new OllamaProvider();
    IChatCompletionModel chatModel;
    if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
    {
        chatModel = CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
    }
    else
    {
        var chat = new OllamaChatModel(provider, modelName);
        if (modelName == "deepseek-coder:33b")
            chat.Settings = OllamaPresets.DeepSeekCoder33B;
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
            IChatCompletionModel MakeLocal(string name)
            {
                var m = new OllamaChatModel(provider, name);
                if (name == "deepseek-coder:33b") m.Settings = OllamaPresets.DeepSeekCoder33B;
                return new OllamaChatAdapter(m);
            }
            string general = askOpts.GeneralModel ?? modelName;
            modelMap["general"] = MakeLocal(general);
            if (!string.IsNullOrWhiteSpace(askOpts.CoderModel)) modelMap["coder"] = MakeLocal(askOpts.CoderModel!);
            if (!string.IsNullOrWhiteSpace(askOpts.SummarizeModel)) modelMap["summarize"] = MakeLocal(askOpts.SummarizeModel!);
            if (!string.IsNullOrWhiteSpace(askOpts.ReasonModel)) modelMap["reason"] = MakeLocal(askOpts.ReasonModel!);
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
            if (modelName == "deepseek-coder:33b")
                chat.Settings = OllamaPresets.DeepSeekCoder33B;
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
                    Telemetry.RecordEmbeddingInput(new[]{text});
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
                        Console.WriteLine($"[embed] seed ok id={(idx+1)} dim={resp.Length}");
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
                        Console.WriteLine($"[embed] seed fail id={(idx+1)} fallback-dim=8");
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
            Telemetry.RecordEmbeddingInput(new[]{question});
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
    if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
    await RunPipelineDslAsync(o.Dsl, o.Model, o.Embed, o.Source, o.K, o.Trace, new ChatRuntimeSettings());
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
            foreach (var (text, idx) in seedDocs.Select((d,i)=>(d,i)))
            {
                try
                {
                    var emb = await embedModel.CreateEmbeddingsAsync(text);
                    await ragStore.AddAsync(new[]{ new Vector{ Id=(idx+1).ToString(), Text=text, Embedding=emb } });
                }
                catch
                {
                    await ragStore.AddAsync(new[]{ new Vector{ Id=(idx+1).ToString(), Text=text, Embedding=new float[8] } });
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
                        var ctx = string.Join("\n- ", results.Select(r=>r.PageContent.Length>160? r.PageContent[..160]+"...": r.PageContent));
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
            return;
        }
    }

    var run = await CreateSemanticCliPipeline(o.Rag, o.Model, o.Embed, o.K, settings, o)
        .Catch()
        .Invoke(o.Question);
    sw.Stop();
    run.Match(
        success => {
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

static async Task RunTestsAsync(TestOptions o)
{
    Console.WriteLine("=== Running MonadicPipeline Tests ===\n");
    
    try
    {
        if (o.All || o.IntegrationOnly)
        {
            await LangChainPipeline.Tests.OllamaCloudIntegrationTests.RunAllTests();
            Console.WriteLine();
        }
        
        if (o.All || o.CliOnly)
        {
            await LangChainPipeline.Tests.CliEndToEndTests.RunAllTests();
            Console.WriteLine();
        }
        
        if (o.All)
        {
            await LangChainPipeline.Tests.TrackedVectorStoreTests.RunAllTests();
            Console.WriteLine();
            
            LangChainPipeline.Tests.MemoryContextTests.RunAllTests();
            Console.WriteLine();
            
            await LangChainPipeline.Tests.LangChainConversationTests.RunAllTests();
            Console.WriteLine();
            
            // Run meta-AI tests
            await LangChainPipeline.Tests.MetaAiTests.RunAllTests();
            Console.WriteLine();
            
            // Run Meta-AI v2 tests
            await LangChainPipeline.Tests.MetaAIv2Tests.RunAllTests();
            Console.WriteLine();
            
            // Run orchestrator tests
            await LangChainPipeline.Tests.OrchestratorTests.RunAllTests();
            Console.WriteLine();
            
            // Run MeTTa integration tests
            await LangChainPipeline.Tests.MeTTaTests.RunAllTests();
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


