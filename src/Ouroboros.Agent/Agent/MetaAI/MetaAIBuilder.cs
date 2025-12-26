#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Builder - Fluent API for orchestrator configuration
// ==========================================================


namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Builder for configuring and creating Meta-AI v2 orchestrator instances.
/// Provides a fluent API following the builder pattern.
/// </summary>
public sealed class MetaAIBuilder
{
    private IChatCompletionModel? _llm;
    private ToolRegistry? _tools;
    private IMemoryStore? _memory;
    private ISkillRegistry? _skills;
    private IUncertaintyRouter? _router;
    private ISafetyGuard? _safety;
    private ISkillExtractor? _skillExtractor;
    private IEmbeddingModel? _embedding;
    private TrackedVectorStore? _vectorStore;
    private double _confidenceThreshold = 0.7;
    private PermissionLevel _defaultPermissionLevel = PermissionLevel.Isolated;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    public MetaAIBuilder WithLLM(IChatCompletionModel llm)
    {
        _llm = llm;
        return this;
    }

    /// <summary>
    /// Sets the tool registry.
    /// </summary>
    public MetaAIBuilder WithTools(ToolRegistry tools)
    {
        _tools = tools;
        return this;
    }

    /// <summary>
    /// Sets the embedding model for semantic search.
    /// </summary>
    public MetaAIBuilder WithEmbedding(IEmbeddingModel embedding)
    {
        _embedding = embedding;
        return this;
    }

    /// <summary>
    /// Sets the vector store for memory.
    /// </summary>
    public MetaAIBuilder WithVectorStore(TrackedVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
        return this;
    }

    /// <summary>
    /// Sets custom memory store.
    /// </summary>
    public MetaAIBuilder WithMemoryStore(IMemoryStore memory)
    {
        _memory = memory;
        return this;
    }

    /// <summary>
    /// Sets custom skill registry.
    /// </summary>
    public MetaAIBuilder WithSkillRegistry(ISkillRegistry skills)
    {
        _skills = skills;
        return this;
    }

    /// <summary>
    /// Sets custom uncertainty router.
    /// </summary>
    public MetaAIBuilder WithUncertaintyRouter(IUncertaintyRouter router)
    {
        _router = router;
        return this;
    }

    /// <summary>
    /// Sets custom safety guard.
    /// </summary>
    public MetaAIBuilder WithSafetyGuard(ISafetyGuard safety)
    {
        _safety = safety;
        return this;
    }

    /// <summary>
    /// Sets custom skill extractor.
    /// </summary>
    public MetaAIBuilder WithSkillExtractor(ISkillExtractor skillExtractor)
    {
        _skillExtractor = skillExtractor;
        return this;
    }

    /// <summary>
    /// Sets the minimum confidence threshold for routing.
    /// </summary>
    public MetaAIBuilder WithConfidenceThreshold(double threshold)
    {
        _confidenceThreshold = Math.Clamp(threshold, 0.0, 1.0);
        return this;
    }

    /// <summary>
    /// Sets the default permission level.
    /// </summary>
    public MetaAIBuilder WithDefaultPermissionLevel(PermissionLevel level)
    {
        _defaultPermissionLevel = level;
        return this;
    }

    /// <summary>
    /// Builds the Meta-AI orchestrator with configured components.
    /// Creates default implementations for any components not explicitly set.
    /// </summary>
    public MetaAIPlannerOrchestrator Build()
    {
        // Validate required components
        if (_llm == null)
            throw new InvalidOperationException("LLM must be configured using WithLLM()");

        // Create default implementations for optional components
        ToolRegistry tools = _tools ?? ToolRegistry.CreateDefault();
        IMemoryStore memory = _memory ?? new MemoryStore(_embedding, _vectorStore);
        ISkillRegistry skills = _skills ?? new SkillRegistry(_embedding);

        // Safety guard is required first for router
        ISafetyGuard safety = _safety ?? new SafetyGuard(_defaultPermissionLevel);

        // Router needs orchestrator - create a simple one if not provided
        IUncertaintyRouter router;
        if (_router == null)
        {
            // Create a basic orchestrator for routing
            SmartModelOrchestrator basicOrchestrator = new SmartModelOrchestrator(tools, "default");
            router = new UncertaintyRouter(basicOrchestrator, _confidenceThreshold);
        }
        else
        {
            router = _router;
        }

        return new MetaAIPlannerOrchestrator(_llm, tools, memory, skills, router, safety, _skillExtractor);
    }

    /// <summary>
    /// Creates a builder with default configuration.
    /// </summary>
    public static MetaAIBuilder CreateDefault()
    {
        return new MetaAIBuilder();
    }
}
