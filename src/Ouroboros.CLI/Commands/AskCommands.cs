using System.Diagnostics;
using CommandLine;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Diagnostics;
using Ouroboros.Options;
using Ouroboros.CLI;
using Ouroboros.Application.Services;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.CLI.Commands;

public static class AskCommands
{
    public static async Task RunAskAsync(AskOptions o)
    {
        // Voice mode integration
        if (o.Voice)
        {
            await RunAskVoiceModeAsync(o);
            return;
        }
        if (o.Router.Equals("auto", StringComparison.OrdinalIgnoreCase)) Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
        if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream, o.Culture);
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
                    chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, o.Model, settings, endpointType);
                }
                catch (Exception ex) when (!o.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[WARN] Remote model '{o.Model}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                    chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"), o.Culture);
                }
            }
            else
            {
                chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model), o.Culture);
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
                embedModel = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, o.Embed, provider2);
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
                    tools = tools.WithTool(new Ouroboros.Tools.RetrievalTool(ragStore, embedModel));
                }
            }

            AgentInstance agentInstance = Ouroboros.Agent.AgentFactory.Create(o.AgentMode, chatModel, tools, o.Debug, o.AgentMaxSteps, o.Rag, o.Embed, jsonTools: o.JsonTools, stream: o.Stream);
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

    // Builds a Step<string,string> that runs either simple chat or chat+RAG in monadic form
    public static Step<string, string> CreateSemanticCliPipeline(bool withRag, string modelName, string embedName, int k, ChatRuntimeSettings? settings = null, AskOptions? askOpts = null)
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
                    return new OllamaChatAdapter(m, settings?.Culture);
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
                    chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
                }
                catch (Exception ex) when (askOpts is not null && !askOpts.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[WARN] Remote model '{modelName}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                    OllamaChatModel local = new OllamaChatModel(provider, "llama3");
                    chatModel = new OllamaChatAdapter(local, settings?.Culture);
                }
                catch (Exception ex) when (askOpts is not null && !askOpts.StrictModel)
                {
                    Console.WriteLine($"[WARN] Remote model '{modelName}' unavailable ({ex.GetType().Name}). Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                    OllamaChatModel local = new OllamaChatModel(provider, "llama3");
                    chatModel = new OllamaChatAdapter(local, settings?.Culture);
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
                chatModel = new OllamaChatAdapter(chat, settings?.Culture);
            }
            IEmbeddingModel embed = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, embedName, provider);

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

    private static void ValidateSecrets(AskOptions? askOpts = null)
    {
        (string? endpoint, string? apiKey, ChatEndpointType _) = ChatConfig.ResolveWithOverrides(askOpts?.Endpoint, askOpts?.ApiKey, askOpts?.EndpointType);
        if (!string.IsNullOrWhiteSpace(endpoint) ^ !string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("[WARN] Only one of CHAT_ENDPOINT / CHAT_API_KEY is set; remote backend will be ignored.");
        }
    }

    private static void LogBackendSelection(string model, ChatRuntimeSettings settings, AskOptions? askOpts = null)
    {
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(askOpts?.Endpoint, askOpts?.ApiKey, askOpts?.EndpointType);
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine($"[INFO] Using remote backend: {endpointType} ({endpoint})");
        }
        else
        {
            Console.WriteLine($"[INFO] Using local backend: Ollama ({model})");
        }
    }

    /// <summary>
    /// Runs the ask command in voice mode with conversational interaction.
    /// </summary>
    private static async Task RunAskVoiceModeAsync(AskOptions o)
    {
        // Integrate with Ouroboros system
        await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
            "Starting voice interaction mode",
            "VoiceMode");

        var voiceService = VoiceModeExtensions.CreateVoiceService(
            voice: true,
            persona: o.Persona,
            voiceOnly: o.VoiceOnly,
            localTts: o.LocalTts,
            voiceLoop: o.VoiceLoop,
            model: o.Model,
            endpoint: o.Endpoint ?? "http://localhost:11434");

        await voiceService.InitializeAsync();

        // Show Ouroboros integration status
        var ouroborosCore = OuroborosCliIntegration.GetCore();
        if (ouroborosCore != null)
        {
            Console.WriteLine("[Voice Mode] ✓ Ouroboros system connected");
            Console.WriteLine($"[Voice Mode] ✓ Episodic memory: {(ouroborosCore.EpisodicMemory != null ? "enabled" : "disabled")}");
            Console.WriteLine($"[Voice Mode] ✓ Consciousness: {(ouroborosCore.Consciousness != null ? "enabled" : "disabled")}");
        }

        voiceService.PrintHeader("ASK");

        // Build the pipeline once
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
        var pipeline = CreateSemanticCliPipeline(o.Rag, o.Model, o.Embed, o.K, settings, o);

        await voiceService.SayAsync("Hey! I'm ready to answer your questions. What would you like to know?");

        // Initial question if provided
        if (!string.IsNullOrWhiteSpace(o.Question))
        {
            try
            {
                var result = await pipeline.Catch().Invoke(o.Question);
                result.Match(
                    success => voiceService.SayAsync(success).Wait(),
                    error => voiceService.SayAsync($"Hmm, I ran into an issue: {error.Message}").Wait());
            }
            catch (Exception ex)
            {
                await voiceService.SayAsync($"Sorry, something went wrong: {ex.Message}");
            }

            if (!o.VoiceLoop)
            {
                voiceService.Dispose();
                return;
            }
        }

        // Voice loop
        bool running = true;
        while (running)
        {
            var input = await voiceService.GetInputAsync("\n  You: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Exit commands
            if (IsExitCommand(input))
            {
                await voiceService.SayAsync("Goodbye! Feel free to ask me anything next time.");
                running = false;
                continue;
            }

            // Help
            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await voiceService.SayAsync("Just ask me any question! I can use RAG for context if you started with the rag flag. Say exit or goodbye to leave.");
                continue;
            }

            // Process question
            try
            {
                var result = await pipeline.Catch().Invoke(input);
                result.Match(
                    success => voiceService.SayAsync(success).Wait(),
                    error => voiceService.SayAsync($"I couldn't figure that out: {error.Message}").Wait());
            }
            catch (Exception ex)
            {
                await voiceService.SayAsync($"Oops, something went wrong: {ex.Message}");
            }
        }

        voiceService.Dispose();
    }

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }
}
