#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Convenience Layer - Simplified orchestrator usage
// ==========================================================


namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Convenience methods for quickly setting up and using Meta-AI orchestrators.
/// Provides presets and one-liner methods for common use cases.
/// </summary>
public static class MetaAIConvenience
{
    /// <summary>
    /// Creates a simple orchestrator with minimal configuration.
    /// Best for: Quick prototyping and simple tasks.
    /// </summary>
    public static Result<IMetaAIPlannerOrchestrator, string> CreateSimple(
        IChatCompletionModel llm)
    {
        try
        {
            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithConfidenceThreshold(0.5)
                .Build();

            return Result<IMetaAIPlannerOrchestrator, string>.Success(orchestrator);
        }
        catch (Exception ex)
        {
            return Result<IMetaAIPlannerOrchestrator, string>.Failure($"Failed to create simple orchestrator: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a standard orchestrator with common configurations.
    /// Best for: Most production use cases with basic safety and memory.
    /// </summary>
    public static Result<IMetaAIPlannerOrchestrator, string> CreateStandard(
        IChatCompletionModel llm,
        ToolRegistry tools,
        IEmbeddingModel embedding)
    {
        try
        {
            TrackedVectorStore vectorStore = new TrackedVectorStore();

            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithTools(tools)
                .WithEmbedding(embedding)
                .WithVectorStore(vectorStore)
                .WithConfidenceThreshold(0.7)
                .WithDefaultPermissionLevel(PermissionLevel.Isolated)
                .Build();

            return Result<IMetaAIPlannerOrchestrator, string>.Success(orchestrator);
        }
        catch (Exception ex)
        {
            return Result<IMetaAIPlannerOrchestrator, string>.Failure($"Failed to create standard orchestrator: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an advanced orchestrator with full features enabled.
    /// Best for: Complex workflows requiring uncertainty handling and skill learning.
    /// </summary>
    public static Result<IMetaAIPlannerOrchestrator, string> CreateAdvanced(
        IChatCompletionModel llm,
        ToolRegistry tools,
        IEmbeddingModel embedding,
        double confidenceThreshold = 0.8)
    {
        try
        {
            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithTools(tools)
                .WithEmbedding(embedding)
                .WithConfidenceThreshold(confidenceThreshold)
                .WithDefaultPermissionLevel(PermissionLevel.ReadOnly)
                .Build();

            return Result<IMetaAIPlannerOrchestrator, string>.Success(orchestrator);
        }
        catch (Exception ex)
        {
            return Result<IMetaAIPlannerOrchestrator, string>.Failure($"Failed to create advanced orchestrator: {ex.Message}");
        }
    }

    /// <summary>
    /// Quick one-liner to ask a question and get an answer.
    /// Automatically handles plan-execute-verify cycle.
    /// </summary>
    public static async Task<Result<string, string>> AskQuestion(
        this IMetaAIPlannerOrchestrator orchestrator,
        string question,
        Dictionary<string, object>? context = null)
    {
        // Plan
        Result<Plan, string> planResult = await orchestrator.PlanAsync(question, context);
        if (!planResult.IsSuccess)
            return Result<string, string>.Failure(planResult.Error);

        // Execute
        Result<ExecutionResult, string> execResult = await orchestrator.ExecuteAsync(planResult.Value);
        if (!execResult.IsSuccess)
            return Result<string, string>.Failure(execResult.Error);

        // Return final output
        return Result<string, string>.Success(execResult.Value.FinalOutput ?? "No output generated");
    }

    /// <summary>
    /// Quick one-liner to analyze text with automatic quality verification.
    /// </summary>
    public static async Task<Result<(string analysis, double quality), string>> AnalyzeText(
        this IMetaAIPlannerOrchestrator orchestrator,
        string text,
        string analysisGoal = "Analyze the following text")
    {
        Dictionary<string, object> context = new Dictionary<string, object> { ["text"] = text };

        // Plan
        Result<Plan, string> planResult = await orchestrator.PlanAsync(analysisGoal, context);
        if (!planResult.IsSuccess)
            return Result<(string, double), string>.Failure(planResult.Error);

        // Execute
        Result<ExecutionResult, string> execResult = await orchestrator.ExecuteAsync(planResult.Value);
        if (!execResult.IsSuccess)
            return Result<(string, double), string>.Failure(execResult.Error);

        // Verify
        Result<VerificationResult, string> verifyResult = await orchestrator.VerifyAsync(execResult.Value);
        if (!verifyResult.IsSuccess)
            return Result<(string, double), string>.Failure(verifyResult.Error);

        string output = execResult.Value.FinalOutput ?? "No analysis generated";
        double quality = verifyResult.Value.QualityScore;

        return Result<(string, double), string>.Success((output, quality));
    }

    /// <summary>
    /// Quick one-liner to generate code with quality assurance.
    /// </summary>
    public static async Task<Result<string, string>> GenerateCode(
        this IMetaAIPlannerOrchestrator orchestrator,
        string description,
        string language = "C#")
    {
        string goal = $"Generate {language} code that: {description}";
        Dictionary<string, object> context = new Dictionary<string, object>
        {
            ["language"] = language,
            ["description"] = description
        };

        return await orchestrator.AskQuestion(goal, context);
    }

    /// <summary>
    /// Executes a complete plan-execute-verify-learn cycle with automatic learning.
    /// </summary>
    public static async Task<Result<VerificationResult, string>> CompleteWorkflow(
        this IMetaAIPlannerOrchestrator orchestrator,
        string goal,
        Dictionary<string, object>? context = null,
        bool autoLearn = true)
    {
        // Plan
        Result<Plan, string> planResult = await orchestrator.PlanAsync(goal, context);
        if (!planResult.IsSuccess)
            return Result<VerificationResult, string>.Failure(planResult.Error);

        // Execute
        Result<ExecutionResult, string> execResult = await orchestrator.ExecuteAsync(planResult.Value);
        if (!execResult.IsSuccess)
            return Result<VerificationResult, string>.Failure(execResult.Error);

        // Verify
        Result<VerificationResult, string> verifyResult = await orchestrator.VerifyAsync(execResult.Value);
        if (!verifyResult.IsSuccess)
            return Result<VerificationResult, string>.Failure(verifyResult.Error);

        // Learn (if enabled)
        if (autoLearn && verifyResult.Value.Verified)
        {
            orchestrator.LearnFromExecution(verifyResult.Value);
        }

        return verifyResult;
    }

    /// <summary>
    /// Creates a batch processor for handling multiple tasks efficiently.
    /// </summary>
    public static async Task<List<Result<string, string>>> ProcessBatch(
        this IMetaAIPlannerOrchestrator orchestrator,
        IEnumerable<string> tasks,
        Dictionary<string, object>? sharedContext = null)
    {
        List<Result<string, string>> results = new List<Result<string, string>>();

        foreach (string task in tasks)
        {
            Result<string, string> result = await orchestrator.AskQuestion(task, sharedContext);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Quick preset: Research assistant orchestrator
    /// </summary>
    public static Result<IMetaAIPlannerOrchestrator, string> CreateResearchAssistant(
        IChatCompletionModel llm,
        ToolRegistry tools,
        IEmbeddingModel embedding)
    {
        try
        {
            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithTools(tools)
                .WithEmbedding(embedding)
                .WithConfidenceThreshold(0.75)
                .WithDefaultPermissionLevel(PermissionLevel.ReadOnly)
                .Build();

            return Result<IMetaAIPlannerOrchestrator, string>.Success(orchestrator);
        }
        catch (Exception ex)
        {
            return Result<IMetaAIPlannerOrchestrator, string>.Failure($"Failed to create research assistant: {ex.Message}");
        }
    }

    /// <summary>
    /// Quick preset: Code assistant orchestrator
    /// </summary>
    public static Result<IMetaAIPlannerOrchestrator, string> CreateCodeAssistant(
        IChatCompletionModel llm,
        ToolRegistry tools)
    {
        try
        {
            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithTools(tools)
                .WithConfidenceThreshold(0.8)
                .WithDefaultPermissionLevel(PermissionLevel.Isolated)
                .Build();

            return Result<IMetaAIPlannerOrchestrator, string>.Success(orchestrator);
        }
        catch (Exception ex)
        {
            return Result<IMetaAIPlannerOrchestrator, string>.Failure($"Failed to create code assistant: {ex.Message}");
        }
    }

    /// <summary>
    /// Quick preset: Interactive chat orchestrator
    /// </summary>
    public static Result<IMetaAIPlannerOrchestrator, string> CreateChatAssistant(
        IChatCompletionModel llm)
    {
        try
        {
            ToolRegistry tools = new ToolRegistry();

            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(llm)
                .WithTools(tools)
                .WithConfidenceThreshold(0.6)
                .WithDefaultPermissionLevel(PermissionLevel.Isolated)
                .Build();

            return Result<IMetaAIPlannerOrchestrator, string>.Success(orchestrator);
        }
        catch (Exception ex)
        {
            return Result<IMetaAIPlannerOrchestrator, string>.Failure($"Failed to create chat assistant: {ex.Message}");
        }
    }
}
