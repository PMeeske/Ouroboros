using System.Diagnostics;
using System.Reactive.Linq;
using CommandLine;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Diagnostics;
using Ouroboros.Options;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Microsoft.Extensions.Hosting;
using Ouroboros.Application.Tools;
using Ouroboros.CLI;
using Ouroboros.Application.Services;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.CLI.Commands;

public static class PipelineCommands
{
    public static async Task RunPipelineAsync(PipelineOptions o)
    {
        // Voice mode integration
        if (o.Voice)
        {
            await RunPipelineVoiceModeAsync(o);
            return;
        }

        if (o.Router.Equals("auto", StringComparison.OrdinalIgnoreCase)) Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
        if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream, o.Culture);
        await RunPipelineDslAsync(o.Dsl, o.Model, o.Embed, o.Source, o.K, o.Trace, settings, o);
    }

    public static async Task RunPipelineDslAsync(string dsl, string modelName, string embedName, string sourcePath, int k, bool trace, ChatRuntimeSettings? settings = null, PipelineOptions? pipelineOpts = null)
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
                return new OllamaChatAdapter(m, settings?.Culture);
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
                chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
            }
            catch (Exception ex) when (pipelineOpts is not null && !pipelineOpts.StrictModel && ex.Message.Contains("Invalid model", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[WARN] Remote model '{modelName}' invalid. Falling back to local 'llama3'. Use --strict-model to disable fallback.");
                OllamaChatModel local = new OllamaChatModel(provider, "llama3");
                chatModel = new OllamaChatAdapter(local, settings?.Culture);
            }
            catch (Exception ex) when (pipelineOpts is not null && !pipelineOpts.StrictModel)
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
            catch { /* ignore and use defaults */ }
            chatModel = new OllamaChatAdapter(chat, settings?.Culture); // adapter added below
        }
        IEmbeddingModel embed = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, embedName, provider);

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

            if (trace)
            {
                Console.WriteLine("\n=== PIPELINE EVENTS ===");
                foreach (var evt in state.Branch.Events)
                {
                    Console.WriteLine($"- {evt.Kind}: {evt}");
                }
            }

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

    /// <summary>
    /// Runs the pipeline in voice mode with conversational interaction.
    /// </summary>
    private static async Task RunPipelineVoiceModeAsync(PipelineOptions o)
    {
        var voiceService = VoiceModeExtensions.CreateVoiceService(
            voice: true,
            persona: o.Persona,
            voiceOnly: o.VoiceOnly,
            localTts: o.LocalTts,
            voiceLoop: o.VoiceLoop,
            model: o.Model,
            endpoint: o.Endpoint ?? "http://localhost:11434");

        await voiceService.InitializeAsync();
        voiceService.PrintHeader("PIPELINE DSL");

        // Build context for available tokens
        var allTokens = SkillCliSteps.GetAllPipelineTokens();
        var tokenList = allTokens.Keys.Take(10).ToList();

        await voiceService.SayAsync($"Pipeline mode ready! I know {allTokens.Count} DSL tokens. Try something like 'SetPrompt hello | UseDraft' or say 'tokens' to see available steps.");

        // Initial DSL if provided
        if (!string.IsNullOrWhiteSpace(o.Dsl))
        {
            ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
            await voiceService.SayAsync($"Running your pipeline: {o.Dsl}");
            await RunPipelineDslAsync(o.Dsl, o.Model, o.Embed, o.Source, o.K, o.Trace, settings, o);
            await voiceService.SayAsync("Pipeline complete!");

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
            var input = await voiceService.GetInputAsync("\n  Pipeline: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (IsExitCommand(input))
            {
                await voiceService.SayAsync("Goodbye! Your pipelines will be here when you return.");
                running = false;
                continue;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await voiceService.SayAsync("Enter a pipeline DSL like 'SetPrompt hello | UseDraft | UseOutput'. Use pipe symbols to chain steps. Say 'tokens' to see available steps.");
                continue;
            }

            if (input.Equals("tokens", StringComparison.OrdinalIgnoreCase))
            {
                var samples = string.Join(", ", tokenList);
                await voiceService.SayAsync($"Available tokens include: {samples}, and {allTokens.Count - 10} more.");
                continue;
            }

            // Check if it looks like a pipeline (contains | or starts with capital letter token)
            if (input.Contains('|') || allTokens.ContainsKey(input.Split(' ')[0]))
            {
                try
                {
                    ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
                    await voiceService.SayAsync($"Executing pipeline...");
                    await RunPipelineDslAsync(input, o.Model, o.Embed, o.Source, o.K, o.Trace, settings, o);
                    await voiceService.SayAsync("Pipeline complete!");
                }
                catch (Exception ex)
                {
                    await voiceService.SayAsync($"Pipeline error: {ex.Message}");
                }
            }
            else
            {
                await voiceService.SayAsync("That doesn't look like a pipeline. Try 'SetPrompt hello | UseDraft' or say 'tokens' for help.");
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
