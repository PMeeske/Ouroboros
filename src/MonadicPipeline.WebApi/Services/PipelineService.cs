using System.Diagnostics;

namespace LangChainPipeline.WebApi.Services;

/// <summary>
/// Service for executing AI pipeline operations.
/// Reuses core logic from the CLI implementation.
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Executes a question-answer operation with optional RAG (Retrieval Augmented Generation)
    /// </summary>
    /// <param name="request">The ask request containing the question and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated answer text</returns>
    Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a pipeline defined by DSL (Domain Specific Language)
    /// </summary>
    /// <param name="request">The pipeline request containing the DSL and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pipeline execution result</returns>
    Task<string> ExecutePipelineAsync(PipelineRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of pipeline service
/// </summary>
public sealed class PipelineService : IPipelineService
{
    /// <inheritdoc/>
    public async Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        var modelName = request.Model ?? "llama3";
        var embedName = "nomic-embed-text";
        var withRag = request.UseRag;
        var sourcePath = request.SourcePath ?? Environment.CurrentDirectory;
        var k = 3;
        var question = request.Question;

        var settings = new ChatRuntimeSettings(
            request.Temperature ?? 0.7f,
            request.MaxTokens ?? 2048,
            120,
            false);

        var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(request.Endpoint, request.ApiKey);
        var provider = new OllamaProvider();
        IChatCompletionModel chatModel;

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                chatModel = CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
            }
            catch
            {
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

        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(chatModel, tools);
        var store = new TrackedVectorStore();

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
                    var resp = await embed.CreateEmbeddingsAsync(text, cancellationToken);
                    await store.AddAsync(new[]
                    {
                        new Vector
                        {
                            Id = (idx + 1).ToString(),
                            Text = text,
                            Embedding = resp
                        }
                    }, cancellationToken);
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
                    }, cancellationToken);
                }
            }
        }

        if (!withRag)
        {
            var (text, _) = await llm.GenerateWithToolsAsync($"Answer the following question clearly and concisely.\nQuestion: {{q}}".Replace("{q}", question), cancellationToken);
            return text;
        }
        else
        {
            var qEmb = await embed.CreateEmbeddingsAsync(question, cancellationToken);
            var hits = await store.GetSimilarDocumentsAsync(qEmb, k, cancellationToken);
            var ctx = string.Join("\n- ", hits.Select(h => h.PageContent));
            var prompt = $"Use the following context to answer.\nContext:\n- {ctx}\n\nQuestion: {{q}}".Replace("{q}", question);
            var (ragText, _) = await llm.GenerateWithToolsAsync(prompt, cancellationToken);
            return ragText;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExecutePipelineAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        var modelName = request.Model ?? "llama3";
        var embedName = "nomic-embed-text";
        var dsl = request.Dsl;
        var sourcePath = Environment.CurrentDirectory;
        var k = 3;
        var trace = request.Debug;

        var settings = new ChatRuntimeSettings(
            request.Temperature ?? 0.7f,
            request.MaxTokens ?? 2048,
            120,
            false);

        var (endpoint, apiKey, endpointType) = ChatConfig.ResolveWithOverrides(request.Endpoint, request.ApiKey);
        var provider = new OllamaProvider();
        IChatCompletionModel chatModel;

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                chatModel = CreateRemoteChatModel(endpoint, apiKey, modelName, settings, endpointType);
            }
            catch
            {
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

        var tools = new ToolRegistry();
        var resolvedSource = string.IsNullOrWhiteSpace(sourcePath) ? Environment.CurrentDirectory : Path.GetFullPath(sourcePath);
        if (!Directory.Exists(resolvedSource))
        {
            Directory.CreateDirectory(resolvedSource);
        }
        var branch = new PipelineBranch("webapi", new TrackedVectorStore(), DataSource.FromPath(resolvedSource));

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = null!,
            Tools = tools,
            Embed = embed,
            RetrievalK = k,
            Trace = trace
        };

        tools = tools.WithPipelineSteps(state);
        var llm = new ToolAwareChatModel(chatModel, tools);
        state.Llm = llm;
        state.Tools = tools;

        var step = PipelineDsl.Build(dsl);
        state = await step(state);

        var last = state.Branch.Events.OfType<ReasoningStep>().LastOrDefault();
        if (last is not null)
        {
            return last.State.Text;
        }

        return "Pipeline executed but no result found.";
    }

    private static IChatCompletionModel CreateRemoteChatModel(string endpoint, string apiKey, string modelName, ChatRuntimeSettings? settings, ChatEndpointType endpointType)
    {
        return endpointType switch
        {
            ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey, modelName, settings),
            ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings),
            ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings),
            _ => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings)
        };
    }

    private static IEmbeddingModel CreateEmbeddingModel(string? endpoint, string? apiKey, ChatEndpointType endpointType, string embedName, OllamaProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            return endpointType switch
            {
                ChatEndpointType.OllamaCloud => new OllamaCloudEmbeddingModel(endpoint, apiKey, embedName),
                _ => new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embedName))
            };
        }
        return new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embedName));
    }
}
