using System.Diagnostics;
using System.Reactive.Linq;
using CommandLine;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Diagnostics;
using LangChainPipeline.Options;
using LangChainPipeline.Providers.SpeechToText;
using LangChainPipeline.Providers.TextToSpeech;
using Microsoft.Extensions.Hosting;
using Ouroboros.Application.Tools;
using Ouroboros.CLI;
using Ouroboros.Application.Services;
using IEmbeddingModel = LangChainPipeline.Domain.IEmbeddingModel;

namespace Ouroboros.CLI.Commands;

public static class PipelineCommands
{
    public static async Task RunPipelineAsync(PipelineOptions o)
    {
        if (o.Router.Equals("auto", StringComparison.OrdinalIgnoreCase)) Environment.SetEnvironmentVariable("MONADIC_ROUTER", "auto");
        if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, o.Stream);
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
                chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
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
}
