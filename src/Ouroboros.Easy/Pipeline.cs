namespace Ouroboros.Easy;

/// <summary>
/// Simplified fluent builder API for creating and configuring Ouroboros AI pipelines.
/// This is the primary entry point for developers new to Ouroboros who want a quick and easy way
/// to get started with AI orchestration.
/// </summary>
/// <example>
/// <code>
/// // Simple usage
/// var result = await Pipeline.Create()
///     .About("quantum computing")
///     .Draft()
///     .Critique()
///     .Improve()
///     .WithModel("llama3")
///     .RunAsync();
///     
/// // Advanced usage with custom tools
/// var result = await Pipeline.Create()
///     .About("Write a Python script to analyze CSV data")
///     .Draft()
///     .Critique()
///     .Improve()
///     .WithModel("llama3")
///     .WithTemperature(0.7)
///     .WithTools(myToolRegistry)
///     .RunAsync();
/// </code>
/// </example>
public sealed class Pipeline
{
    private string? _topic;
    private string? _modelName;
    private double _temperature = 0.7;
    private string? _ollamaEndpoint;
    private ToolRegistry? _tools;
    private IEmbeddingModel? _embedding;
    private bool _includeDraft = false;
    private bool _includeCritique = false;
    private bool _includeImprove = false;
    private bool _includeSummarize = false;

    private Pipeline()
    {
    }

    /// <summary>
    /// Creates a new pipeline builder instance.
    /// </summary>
    /// <returns>A new Pipeline builder for fluent configuration.</returns>
    public static Pipeline Create()
    {
        return new Pipeline();
    }

    /// <summary>
    /// Sets the topic or question for the pipeline to process.
    /// </summary>
    /// <param name="topic">The topic, question, or prompt to process.</param>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline About(string topic)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        return this;
    }

    /// <summary>
    /// Enables the draft stage - generates an initial response to the topic.
    /// </summary>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline Draft()
    {
        _includeDraft = true;
        return this;
    }

    /// <summary>
    /// Enables the critique stage - analyzes and identifies weaknesses in the draft.
    /// </summary>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline Critique()
    {
        _includeCritique = true;
        return this;
    }

    /// <summary>
    /// Enables the improve stage - generates an improved version based on critique.
    /// </summary>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline Improve()
    {
        _includeImprove = true;
        return this;
    }

    /// <summary>
    /// Enables the summarize stage - creates a concise summary of the final output.
    /// </summary>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline Summarize()
    {
        _includeSummarize = true;
        return this;
    }

    /// <summary>
    /// Sets the language model to use.
    /// </summary>
    /// <param name="modelName">The name of the model (e.g., "llama3", "mistral", "phi3").</param>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline WithModel(string modelName)
    {
        _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
        return this;
    }

    /// <summary>
    /// Sets the temperature for the language model (controls randomness).
    /// </summary>
    /// <param name="temperature">Temperature value between 0.0 (deterministic) and 1.0 (creative). Default is 0.7.</param>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline WithTemperature(double temperature)
    {
        _temperature = Math.Clamp(temperature, 0.0, 1.0);
        return this;
    }

    /// <summary>
    /// Sets the Ollama endpoint URL for model inference.
    /// </summary>
    /// <param name="endpoint">The Ollama API endpoint URL (e.g., "http://localhost:11434").</param>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline WithOllamaEndpoint(string endpoint)
    {
        _ollamaEndpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        return this;
    }

    /// <summary>
    /// Sets custom tools for the pipeline to use.
    /// </summary>
    /// <param name="tools">The tool registry containing available tools.</param>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline WithTools(ToolRegistry tools)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        return this;
    }

    /// <summary>
    /// Sets a custom embedding model for semantic operations.
    /// </summary>
    /// <param name="embedding">The embedding model to use.</param>
    /// <returns>The pipeline builder for method chaining.</returns>
    public Pipeline WithEmbedding(IEmbeddingModel embedding)
    {
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        return this;
    }

    /// <summary>
    /// Executes the configured pipeline and returns the result.
    /// </summary>
    /// <returns>A result containing the pipeline output or an error message.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
    public async Task<PipelineResult> RunAsync()
    {
        // Validate configuration
        if (string.IsNullOrWhiteSpace(_topic))
        {
            return PipelineResult.Failure("Topic must be set using About()");
        }

        if (string.IsNullOrWhiteSpace(_modelName))
        {
            return PipelineResult.Failure("Model must be set using WithModel()");
        }

        if (!_includeDraft && !_includeCritique && !_includeImprove && !_includeSummarize)
        {
            return PipelineResult.Failure("At least one stage must be enabled (Draft, Critique, Improve, or Summarize)");
        }

        try
        {
            // Create the language model using LangChain's Ollama provider
            string endpoint = _ollamaEndpoint ?? "http://localhost:11434";
            LangChain.Providers.Ollama.OllamaProvider provider = new(endpoint);
            LangChain.Providers.Ollama.OllamaChatModel ollamaModel = new(provider, _modelName);
            
            // Wrap in our adapter
            IChatCompletionModel llm = new OllamaChatAdapter(ollamaModel);

            // Create default tools if not provided
            ToolRegistry tools = _tools ?? ToolRegistry.CreateDefault();

            // Create the orchestrator using the existing MetaAIBuilder
            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithTools(tools)
                .WithConfidenceThreshold(0.7)
                .WithDefaultPermissionLevel(PermissionLevel.Isolated)
                .Build();

            // Build the goal/prompt based on enabled stages
            string goal = BuildGoal();

            // Execute the pipeline using the orchestrator
            Result<string, string> result = await orchestrator.AskQuestion(goal);

            if (result.IsSuccess)
            {
                return PipelineResult.Success(result.Value);
            }
            else
            {
                return PipelineResult.Failure(result.Error);
            }
        }
        catch (Exception ex)
        {
            return PipelineResult.Failure($"Pipeline execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the underlying DSL representation of the pipeline configuration.
    /// This allows power users to see and customize the generated pipeline structure.
    /// </summary>
    /// <returns>A string representation of the pipeline in DSL format.</returns>
    public string ToDSL()
    {
        List<string> stages = new List<string>();

        if (_includeDraft) stages.Add("draft");
        if (_includeCritique) stages.Add("critique");
        if (_includeImprove) stages.Add("improve");
        if (_includeSummarize) stages.Add("summarize");

        string stagesStr = stages.Count > 0 ? string.Join(" -> ", stages) : "none";

        return $@"Pipeline:
  Topic: {_topic ?? "not set"}
  Model: {_modelName ?? "not set"}
  Temperature: {_temperature}
  Endpoint: {_ollamaEndpoint ?? "default (localhost:11434)"}
  Stages: {stagesStr}
  Tools: {(_tools != null ? "custom" : "default")}
  Embedding: {(_embedding != null ? "custom" : "default")}";
    }

    private string BuildGoal()
    {
        List<string> instructions = new List<string>();

        if (_includeDraft)
        {
            instructions.Add("First, generate an initial draft response");
        }

        if (_includeCritique)
        {
            instructions.Add("Then, critique the response and identify areas for improvement");
        }

        if (_includeImprove)
        {
            instructions.Add("Next, generate an improved version incorporating the feedback");
        }

        if (_includeSummarize)
        {
            instructions.Add("Finally, provide a concise summary of the key points");
        }

        string workflow = instructions.Count > 0 
            ? $"\n\nWorkflow:\n{string.Join("\n", instructions)}" 
            : "";

        return $"{_topic}{workflow}";
    }
}

/// <summary>
/// Represents the result of a pipeline execution.
/// </summary>
public sealed class PipelineResult
{
    /// <summary>
    /// Gets whether the pipeline execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the output from the pipeline, or null if execution failed.
    /// </summary>
    public string? Output { get; }

    /// <summary>
    /// Gets the error message if execution failed, or null if successful.
    /// </summary>
    public string? Error { get; }

    private PipelineResult(bool isSuccess, string? output, string? error)
    {
        IsSuccess = isSuccess;
        Output = output;
        Error = error;
    }

    internal static PipelineResult Success(string output)
    {
        return new PipelineResult(true, output, null);
    }

    internal static PipelineResult Failure(string error)
    {
        return new PipelineResult(false, null, error);
    }

    /// <summary>
    /// Returns the output if successful, otherwise throws an exception with the error message.
    /// </summary>
    public string GetOutputOrThrow()
    {
        if (!IsSuccess)
        {
            throw new InvalidOperationException($"Pipeline execution failed: {Error}");
        }

        return Output!;
    }
}
