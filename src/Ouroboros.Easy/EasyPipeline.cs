// <copyright file="EasyPipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Easy;

/// <summary>
/// Simplified fluent API for creating Ouroboros pipelines.
/// Provides an easy-to-use interface that internally delegates to the monadic core.
/// </summary>
public sealed class EasyPipeline
{
    private readonly string _topic;
    private readonly List<PipelineOperation> _operations;
    private string _modelName;
    private double _temperature;
    private int _contextDocuments;
    private IVectorStore? _vectorStore;
    private DataSource? _dataSource;
    private IEmbeddingModel? _embeddingModel;
    private ToolAwareChatModel? _chatModel;
    private ToolRegistry? _toolRegistry;

    private EasyPipeline(string topic)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _operations = new List<PipelineOperation>();
        _modelName = "llama3";
        _temperature = 0.7;
        _contextDocuments = 8;
    }

    /// <summary>
    /// Creates a new pipeline builder with the specified topic.
    /// </summary>
    /// <param name="topic">The topic or subject for the pipeline.</param>
    /// <returns>A new EasyPipeline instance.</returns>
    public static EasyPipeline Create(string topic) => new(topic);

    /// <summary>
    /// Adds a draft operation to the pipeline.
    /// Generates an initial response based on context and topic.
    /// </summary>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline Draft()
    {
        _operations.Add(PipelineOperation.Draft);
        return this;
    }

    /// <summary>
    /// Adds a critique operation to the pipeline.
    /// Analyzes and critiques the previous reasoning state.
    /// </summary>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline Critique()
    {
        _operations.Add(PipelineOperation.Critique);
        return this;
    }

    /// <summary>
    /// Adds an improve operation to the pipeline.
    /// Generates an improved version based on the critique.
    /// </summary>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline Improve()
    {
        _operations.Add(PipelineOperation.Improve);
        return this;
    }

    /// <summary>
    /// Adds a thinking operation to the pipeline.
    /// Generates a reasoning process before drafting.
    /// </summary>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline Think()
    {
        _operations.Add(PipelineOperation.Think);
        return this;
    }

    /// <summary>
    /// Adds a summarize operation to the pipeline.
    /// Creates a concise summary of the reasoning results.
    /// </summary>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline Summarize()
    {
        _operations.Add(PipelineOperation.Summarize);
        return this;
    }

    /// <summary>
    /// Configures the model to use for generation.
    /// </summary>
    /// <param name="modelName">The name of the model (e.g., "llama3", "gpt-4").</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithModel(string modelName)
    {
        _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
        return this;
    }

    /// <summary>
    /// Configures the temperature for generation.
    /// </summary>
    /// <param name="temperature">The temperature value (0.0 to 1.0).</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithTemperature(double temperature)
    {
        if (temperature < 0.0 || temperature > 1.0)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0.0 and 1.0");

        _temperature = temperature;
        return this;
    }

    /// <summary>
    /// Configures the number of context documents to retrieve for RAG.
    /// </summary>
    /// <param name="count">The number of documents to retrieve.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithContextDocuments(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Context document count must be at least 1");

        _contextDocuments = count;
        return this;
    }

    /// <summary>
    /// Configures the vector store for RAG operations.
    /// </summary>
    /// <param name="vectorStore">The vector store to use.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithVectorStore(IVectorStore vectorStore)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        return this;
    }

    /// <summary>
    /// Configures the data source for the pipeline.
    /// </summary>
    /// <param name="dataSource">The data source to use.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithDataSource(DataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        return this;
    }

    /// <summary>
    /// Configures the embedding model for RAG operations.
    /// </summary>
    /// <param name="embeddingModel">The embedding model to use.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithEmbeddingModel(IEmbeddingModel embeddingModel)
    {
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        return this;
    }

    /// <summary>
    /// Configures the chat model for generation.
    /// </summary>
    /// <param name="chatModel">The chat model to use.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithChatModel(ToolAwareChatModel chatModel)
    {
        _chatModel = chatModel ?? throw new ArgumentNullException(nameof(chatModel));
        return this;
    }

    /// <summary>
    /// Configures the tool registry for the pipeline.
    /// </summary>
    /// <param name="toolRegistry">The tool registry to use.</param>
    /// <returns>This pipeline instance for method chaining.</returns>
    public EasyPipeline WithTools(ToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        return this;
    }

    /// <summary>
    /// Executes the configured pipeline and returns the result.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The execution result containing the final reasoning state.</returns>
    public async Task<EasyPipelineResult> RunAsync(CancellationToken cancellationToken = default)
    {
        // Ensure required components are configured
        EnsureRequiredComponents();

        // Create initial pipeline branch
        PipelineBranch branch = new(_topic, _vectorStore!, _dataSource!);

        try
        {
            // Execute each operation in sequence
            foreach (PipelineOperation op in _operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                branch = await ExecuteOperationAsync(op, branch, cancellationToken);
            }

            // Extract final reasoning state
            ReasoningState? finalState = GetFinalReasoningState(branch);

            return new EasyPipelineResult(
                Success: true,
                Output: finalState?.Text ?? string.Empty,
                Branch: branch,
                Error: null);
        }
        catch (Exception ex)
        {
            return new EasyPipelineResult(
                Success: false,
                Output: string.Empty,
                Branch: branch,
                Error: ex.Message);
        }
    }

    /// <summary>
    /// Exports the pipeline configuration as a DSL-like string representation.
    /// This allows power users to understand the underlying pipeline structure.
    /// </summary>
    /// <returns>A string representation of the pipeline in DSL format.</returns>
    public string ToDSL()
    {
        var dslBuilder = new StringBuilder();
        dslBuilder.AppendLine($"Pipeline.About(\"{_topic}\")");

        foreach (PipelineOperation op in _operations)
        {
            dslBuilder.AppendLine($"  .{op}()");
        }

        dslBuilder.AppendLine($"  .WithModel(\"{_modelName}\")");
        dslBuilder.AppendLine($"  .WithTemperature({_temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        dslBuilder.AppendLine($"  .WithContextDocuments({_contextDocuments})");
        dslBuilder.AppendLine("  .RunAsync()");

        return dslBuilder.ToString();
    }

    /// <summary>
    /// Exposes the underlying Kleisli arrow composition for advanced users.
    /// This demonstrates the progression from Easy API to Core monadic architecture.
    /// </summary>
    /// <returns>A Step that represents the compiled pipeline as a Kleisli arrow.</returns>
    public Step<PipelineBranch, PipelineBranch> ToCore()
    {
        // Ensure required components are configured
        EnsureRequiredComponents();

        // Build the Kleisli arrow composition
        Step<PipelineBranch, PipelineBranch>? composedStep = null;

        foreach (PipelineOperation op in _operations)
        {
            Step<PipelineBranch, PipelineBranch> currentStep = op switch
            {
                PipelineOperation.Think => ReasoningArrows.ThinkingArrow(
                    _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
                PipelineOperation.Draft => ReasoningArrows.DraftArrow(
                    _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
                PipelineOperation.Critique => ReasoningArrows.CritiqueArrow(
                    _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
                PipelineOperation.Improve => ReasoningArrows.ImproveArrow(
                    _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
                PipelineOperation.Summarize => CreateSummarizeArrow(),
                _ => throw new InvalidOperationException($"Unknown operation: {op}")
            };

            composedStep = composedStep is null ? currentStep : composedStep.Then(currentStep);
        }

        return composedStep ?? (branch => Task.FromResult(branch));
    }

    private void EnsureRequiredComponents()
    {
        _vectorStore ??= new TrackedVectorStore();
        _dataSource ??= DataSource.FromPath(".");
        _toolRegistry ??= new ToolRegistry();

        if (_embeddingModel is null)
        {
            throw new InvalidOperationException(
                "Embedding model is required. Use WithEmbeddingModel() to configure it.");
        }

        if (_chatModel is null)
        {
            throw new InvalidOperationException(
                "Chat model is required. Use WithChatModel() to configure it.");
        }
    }

    private async Task<PipelineBranch> ExecuteOperationAsync(
        PipelineOperation operation,
        PipelineBranch branch,
        CancellationToken cancellationToken)
    {
        Step<PipelineBranch, PipelineBranch> arrow = operation switch
        {
            PipelineOperation.Think => ReasoningArrows.ThinkingArrow(
                _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
            PipelineOperation.Draft => ReasoningArrows.DraftArrow(
                _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
            PipelineOperation.Critique => ReasoningArrows.CritiqueArrow(
                _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
            PipelineOperation.Improve => ReasoningArrows.ImproveArrow(
                _chatModel!, _toolRegistry!, _embeddingModel!, _topic, _topic, _contextDocuments),
            PipelineOperation.Summarize => CreateSummarizeArrow(),
            _ => throw new InvalidOperationException($"Unknown operation: {operation}")
        };

        return await arrow(branch);
    }

    private Step<PipelineBranch, PipelineBranch> CreateSummarizeArrow()
    {
        return async branch =>
        {
            ReasoningState? finalState = GetFinalReasoningState(branch);
            if (finalState is null) return branch;

            string prompt = $"Summarize the following in a concise manner:\n\n{finalState.Text}";
            (string text, List<ToolExecution> toolCalls) = await _chatModel!.GenerateWithToolsAsync(prompt);

            return branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
        };
    }

    private static ReasoningState? GetFinalReasoningState(PipelineBranch branch)
    {
        return branch.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .LastOrDefault();
    }
}

/// <summary>
/// Represents the available pipeline operations.
/// </summary>
internal enum PipelineOperation
{
    Think,
    Draft,
    Critique,
    Improve,
    Summarize
}
