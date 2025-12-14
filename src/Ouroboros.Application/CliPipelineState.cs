#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChainPipeline.Network;

namespace Ouroboros.Application;

public sealed class CliPipelineState
{
    public required PipelineBranch Branch { get; set; }
    public required ToolAwareChatModel Llm { get; set; }
    public required ToolRegistry Tools { get; set; }
    // Generalized embedding model (was OllamaEmbeddingModel)
    public required IEmbeddingModel Embed { get; set; }

    public string Topic { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int RetrievalK { get; set; } = 8;
    public bool Trace { get; set; } = false;

    // Extended chain state (for new DSL style retrieval + template + llm steps)
    public List<string> Retrieved { get; } = [];
    public string Context { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;

    // MeTTa Engine State
    public Ouroboros.Tools.MeTTa.IMeTTaEngine? MeTTaEngine { get; set; }

    // Vector Store (IOC-based, can be InMemory, Qdrant, Pinecone, etc.)
    public IVectorStore? VectorStore { get; set; }

    // Streaming infrastructure
    public StreamingContext? Streaming { get; set; }
    public IObservable<object>? ActiveStream { get; set; }

    // Network State Tracking (optional, for reifying Steps into MerkleDag)
    public NetworkStateTracker? NetworkTracker { get; set; }

    public CliPipelineState WithBranch(PipelineBranch branch)
    {
        this.Branch = branch;

        // Auto-update network tracker if enabled
        this.NetworkTracker?.UpdateBranch(branch);

        return this;
    }

    /// <summary>
    /// Enables network state tracking for this pipeline state.
    /// All branch updates will automatically be reified into the MerkleDag.
    /// </summary>
    public CliPipelineState WithNetworkTracking()
    {
        this.NetworkTracker ??= new NetworkStateTracker();
        this.NetworkTracker.TrackBranch(this.Branch);
        return this;
    }

    /// <summary>
    /// Gets a summary of the network state if tracking is enabled.
    /// </summary>
    public string? GetNetworkStateSummary()
    {
        return this.NetworkTracker?.GetStateSummary();
    }

    /// <summary>
    /// Projects the current branch to a GlobalNetworkState snapshot.
    /// </summary>
    public GlobalNetworkState? ProjectNetworkState()
    {
        if (this.NetworkTracker == null)
        {
            return null;
        }

        return this.NetworkTracker.CreateSnapshot();
    }
}
