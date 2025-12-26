#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Global Workspace Interface
// Phase 2: Shared working memory with attention policies
// ==========================================================

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Priority level for workspace items.
/// </summary>
public enum WorkspacePriority
{
    /// <summary>Low priority - background information</summary>
    Low = 0,
    
    /// <summary>Normal priority - standard working memory</summary>
    Normal = 1,
    
    /// <summary>High priority - important information requiring attention</summary>
    High = 2,
    
    /// <summary>Critical priority - urgent information</summary>
    Critical = 3
}

/// <summary>
/// Represents an item in the global workspace.
/// </summary>
public sealed record WorkspaceItem(
    Guid Id,
    string Content,
    WorkspacePriority Priority,
    string Source,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    List<string> Tags,
    Dictionary<string, object> Metadata)
{
    /// <summary>
    /// Gets the attention weight based on priority, recency, and expiration.
    /// </summary>
    public double GetAttentionWeight()
    {
        double priorityWeight = (int)Priority / 3.0; // 0.0 to 1.0
        double recencyWeight = 1.0 - Math.Min(1.0, (DateTime.UtcNow - CreatedAt).TotalHours / 24.0);
        double urgencyWeight = ExpiresAt < DateTime.UtcNow.AddHours(1) ? 1.0 : 0.0;
        
        return (priorityWeight * 0.5) + (recencyWeight * 0.3) + (urgencyWeight * 0.2);
    }
}

/// <summary>
/// Attention policy configuration.
/// </summary>
public sealed record AttentionPolicy(
    int MaxWorkspaceSize,
    int MaxHighPriorityItems,
    TimeSpan DefaultItemLifetime,
    double MinAttentionThreshold);

/// <summary>
/// Workspace broadcast event.
/// </summary>
public sealed record WorkspaceBroadcast(
    WorkspaceItem Item,
    string BroadcastReason,
    DateTime BroadcastTime);

/// <summary>
/// Interface for global workspace management.
/// Implements shared working memory with attention-based policies.
/// </summary>
public interface IGlobalWorkspace
{
    /// <summary>
    /// Adds an item to the workspace.
    /// </summary>
    /// <param name="content">Item content</param>
    /// <param name="priority">Priority level</param>
    /// <param name="source">Source of the item</param>
    /// <param name="tags">Tags for categorization</param>
    /// <param name="lifetime">Optional custom lifetime</param>
    /// <returns>The created workspace item</returns>
    WorkspaceItem AddItem(
        string content,
        WorkspacePriority priority,
        string source,
        List<string>? tags = null,
        TimeSpan? lifetime = null);

    /// <summary>
    /// Gets items currently in the workspace, ordered by attention weight.
    /// </summary>
    /// <param name="minPriority">Minimum priority filter</param>
    /// <returns>List of workspace items ordered by attention weight</returns>
    List<WorkspaceItem> GetItems(WorkspacePriority minPriority = WorkspacePriority.Low);

    /// <summary>
    /// Gets high-priority items requiring immediate attention.
    /// </summary>
    /// <returns>List of high-priority items</returns>
    List<WorkspaceItem> GetHighPriorityItems();

    /// <summary>
    /// Removes an item from the workspace.
    /// </summary>
    /// <param name="itemId">Item ID to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveItem(Guid itemId);

    /// <summary>
    /// Broadcasts a high-priority item to all listeners.
    /// </summary>
    /// <param name="item">Item to broadcast</param>
    /// <param name="reason">Reason for broadcast</param>
    void BroadcastItem(WorkspaceItem item, string reason);

    /// <summary>
    /// Gets recent broadcasts.
    /// </summary>
    /// <param name="count">Number of broadcasts to retrieve</param>
    /// <returns>List of recent broadcasts</returns>
    List<WorkspaceBroadcast> GetRecentBroadcasts(int count = 10);

    /// <summary>
    /// Searches workspace items by tags.
    /// </summary>
    /// <param name="tags">Tags to search for</param>
    /// <returns>Items matching any of the tags</returns>
    List<WorkspaceItem> SearchByTags(List<string> tags);

    /// <summary>
    /// Cleans up expired items and applies attention policies.
    /// </summary>
    void ApplyAttentionPolicies();

    /// <summary>
    /// Gets workspace statistics.
    /// </summary>
    /// <returns>Current workspace statistics</returns>
    WorkspaceStatistics GetStatistics();
}

/// <summary>
/// Statistics about the global workspace.
/// </summary>
public sealed record WorkspaceStatistics(
    int TotalItems,
    int HighPriorityItems,
    int CriticalItems,
    int ExpiredItems,
    double AverageAttentionWeight,
    Dictionary<string, int> ItemsBySource);
