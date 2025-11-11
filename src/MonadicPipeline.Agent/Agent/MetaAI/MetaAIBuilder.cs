// <copyright file="MetaAIBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Builder for configuring and creating Meta-AI v2 orchestrator instances.
/// Provides a fluent API following the builder pattern.
/// </summary>
public sealed class MetaAIBuilder
{
    private IChatCompletionModel? llm;
    private ToolRegistry? tools;
    private IMemoryStore? memory;
    private ISkillRegistry? skills;
    private IUncertaintyRouter? router;
    private ISafetyGuard? safety;
    private ISkillExtractor? skillExtractor;
    private IEmbeddingModel? embedding;
    private TrackedVectorStore? vectorStore;
    private double confidenceThreshold = 0.7;
    private PermissionLevel defaultPermissionLevel = PermissionLevel.Isolated;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithLLM(IChatCompletionModel llm)
    {
        this.llm = llm;
        return this;
    }

    /// <summary>
    /// Sets the tool registry.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithTools(ToolRegistry tools)
    {
        this.tools = tools;
        return this;
    }

    /// <summary>
    /// Sets the embedding model for semantic search.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithEmbedding(IEmbeddingModel embedding)
    {
        this.embedding = embedding;
        return this;
    }

    /// <summary>
    /// Sets the vector store for memory.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithVectorStore(TrackedVectorStore vectorStore)
    {
        this.vectorStore = vectorStore;
        return this;
    }

    /// <summary>
    /// Sets custom memory store.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithMemoryStore(IMemoryStore memory)
    {
        this.memory = memory;
        return this;
    }

    /// <summary>
    /// Sets custom skill registry.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithSkillRegistry(ISkillRegistry skills)
    {
        this.skills = skills;
        return this;
    }

    /// <summary>
    /// Sets custom uncertainty router.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithUncertaintyRouter(IUncertaintyRouter router)
    {
        this.router = router;
        return this;
    }

    /// <summary>
    /// Sets custom safety guard.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithSafetyGuard(ISafetyGuard safety)
    {
        this.safety = safety;
        return this;
    }

    /// <summary>
    /// Sets custom skill extractor.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithSkillExtractor(ISkillExtractor skillExtractor)
    {
        this.skillExtractor = skillExtractor;
        return this;
    }

    /// <summary>
    /// Sets the minimum confidence threshold for routing.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithConfidenceThreshold(double threshold)
    {
        this.confidenceThreshold = Math.Clamp(threshold, 0.0, 1.0);
        return this;
    }

    /// <summary>
    /// Sets the default permission level.
    /// </summary>
    /// <returns></returns>
    public MetaAIBuilder WithDefaultPermissionLevel(PermissionLevel level)
    {
        this.defaultPermissionLevel = level;
        return this;
    }

    /// <summary>
    /// Builds the Meta-AI orchestrator with configured components.
    /// Creates default implementations for any components not explicitly set.
    /// </summary>
    /// <returns></returns>
    public MetaAIPlannerOrchestrator Build()
    {
        // Validate required components
        if (this.llm == null)
        {
            throw new InvalidOperationException("LLM must be configured using WithLLM()");
        }

        // Create default implementations for optional components
        var tools = this.tools ?? ToolRegistry.CreateDefault();
        var memory = this.memory ?? new MemoryStore(this.embedding, this.vectorStore);
        var skills = this.skills ?? new SkillRegistry(this.embedding);

        // Safety guard is required first for router
        var safety = this.safety ?? new SafetyGuard(this.defaultPermissionLevel);

        // Router needs orchestrator - create a simple one if not provided
        IUncertaintyRouter router;
        if (this.router == null)
        {
            // Create a basic orchestrator for routing
            var basicOrchestrator = new SmartModelOrchestrator(tools, "default");
            router = new UncertaintyRouter(basicOrchestrator, this.confidenceThreshold);
        }
        else
        {
            router = this.router;
        }

        return new MetaAIPlannerOrchestrator(this.llm, tools, memory, skills, router, safety, this.skillExtractor);
    }

    /// <summary>
    /// Creates a builder with default configuration.
    /// </summary>
    /// <returns></returns>
    public static MetaAIBuilder CreateDefault()
    {
        return new MetaAIBuilder();
    }
}
