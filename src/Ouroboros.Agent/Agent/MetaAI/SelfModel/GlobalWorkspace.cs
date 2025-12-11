#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Global Workspace Implementation
// Phase 2: Shared working memory with attention policies
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI.SelfModel;

/// <summary>
/// Implementation of global workspace with attention-based management.
/// </summary>
public sealed class GlobalWorkspace : IGlobalWorkspace
{
    private readonly ConcurrentDictionary<Guid, WorkspaceItem> _items = new();
    private readonly ConcurrentQueue<WorkspaceBroadcast> _broadcasts = new();
    private readonly AttentionPolicy _policy;
    private readonly object _lock = new();

    public GlobalWorkspace(AttentionPolicy? policy = null)
    {
        _policy = policy ?? new AttentionPolicy(
            MaxWorkspaceSize: 100,
            MaxHighPriorityItems: 20,
            DefaultItemLifetime: TimeSpan.FromHours(1),
            MinAttentionThreshold: 0.3);
    }

    public WorkspaceItem AddItem(
        string content,
        WorkspacePriority priority,
        string source,
        List<string>? tags = null,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(source);

        TimeSpan effectiveLifetime = lifetime ?? _policy.DefaultItemLifetime;
        
        var item = new WorkspaceItem(
            Guid.NewGuid(),
            content,
            priority,
            source,
            DateTime.UtcNow,
            DateTime.UtcNow + effectiveLifetime,
            tags ?? new List<string>(),
            new Dictionary<string, object>());

        _items[item.Id] = item;

        // Broadcast high-priority items immediately
        if (priority >= WorkspacePriority.High)
        {
            BroadcastItem(item, "High priority item added");
        }

        // Apply attention policies to maintain workspace health
        ApplyAttentionPolicies();

        return item;
    }

    public List<WorkspaceItem> GetItems(WorkspacePriority minPriority = WorkspacePriority.Low)
    {
        return _items.Values
            .Where(i => i.Priority >= minPriority && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.GetAttentionWeight())
            .ToList();
    }

    public List<WorkspaceItem> GetHighPriorityItems()
    {
        return _items.Values
            .Where(i => i.Priority >= WorkspacePriority.High && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.GetAttentionWeight())
            .ToList();
    }

    public bool RemoveItem(Guid itemId)
    {
        return _items.TryRemove(itemId, out _);
    }

    public void BroadcastItem(WorkspaceItem item, string reason)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(reason);

        var broadcast = new WorkspaceBroadcast(item, reason, DateTime.UtcNow);
        _broadcasts.Enqueue(broadcast);

        // Keep only recent broadcasts
        while (_broadcasts.Count > 100)
        {
            _broadcasts.TryDequeue(out _);
        }
    }

    public List<WorkspaceBroadcast> GetRecentBroadcasts(int count = 10)
    {
        return _broadcasts
            .TakeLast(count)
            .OrderByDescending(b => b.BroadcastTime)
            .ToList();
    }

    public List<WorkspaceItem> SearchByTags(List<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return _items.Values
            .Where(i => i.Tags.Any(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase)) &&
                       i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.GetAttentionWeight())
            .ToList();
    }

    public void ApplyAttentionPolicies()
    {
        lock (_lock)
        {
            // Remove expired items
            List<Guid> expiredIds = _items.Values
                .Where(i => i.ExpiresAt <= DateTime.UtcNow)
                .Select(i => i.Id)
                .ToList();

            foreach (Guid id in expiredIds)
            {
                _items.TryRemove(id, out _);
            }

            // If workspace is too large, remove low-attention items
            if (_items.Count > _policy.MaxWorkspaceSize)
            {
                List<WorkspaceItem> sortedItems = _items.Values
                    .OrderBy(i => i.GetAttentionWeight())
                    .ToList();

                int toRemove = _items.Count - _policy.MaxWorkspaceSize;
                foreach (WorkspaceItem item in sortedItems.Take(toRemove))
                {
                    if (item.Priority < WorkspacePriority.High) // Never remove high-priority items
                    {
                        _items.TryRemove(item.Id, out _);
                    }
                }
            }

            // If too many high-priority items, broadcast a warning
            int highPriorityCount = _items.Values.Count(i => i.Priority >= WorkspacePriority.High);
            if (highPriorityCount > _policy.MaxHighPriorityItems)
            {
                var warningItem = new WorkspaceItem(
                    Guid.NewGuid(),
                    $"Attention overload: {highPriorityCount} high-priority items (limit: {_policy.MaxHighPriorityItems})",
                    WorkspacePriority.Critical,
                    "GlobalWorkspace",
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddMinutes(5),
                    new List<string> { "warning", "attention" },
                    new Dictionary<string, object>());

                BroadcastItem(warningItem, "Attention capacity warning");
            }
        }
    }

    public WorkspaceStatistics GetStatistics()
    {
        List<WorkspaceItem> activeItems = _items.Values
            .Where(i => i.ExpiresAt > DateTime.UtcNow)
            .ToList();

        int totalItems = activeItems.Count;
        int highPriorityItems = activeItems.Count(i => i.Priority >= WorkspacePriority.High);
        int criticalItems = activeItems.Count(i => i.Priority == WorkspacePriority.Critical);
        int expiredItems = _items.Count - totalItems;
        double averageAttentionWeight = activeItems.Any()
            ? activeItems.Average(i => i.GetAttentionWeight())
            : 0.0;

        Dictionary<string, int> itemsBySource = activeItems
            .GroupBy(i => i.Source)
            .ToDictionary(g => g.Key, g => g.Count());

        return new WorkspaceStatistics(
            totalItems,
            highPriorityItems,
            criticalItems,
            expiredItems,
            averageAttentionWeight,
            itemsBySource);
    }
}
