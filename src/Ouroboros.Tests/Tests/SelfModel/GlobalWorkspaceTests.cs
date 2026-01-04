using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.Tests.SelfModel;

/// <summary>
/// Tests for GlobalWorkspace implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GlobalWorkspaceTests
{
    [Fact]
    public void AddItem_Should_CreateWorkspaceItem()
    {
        // Arrange
        var workspace = new GlobalWorkspace();

        // Act
        WorkspaceItem item = workspace.AddItem(
            "Test content",
            WorkspacePriority.Normal,
            "TestSource",
            new List<string> { "test", "example" });

        // Assert
        Assert.NotNull(item);
        Assert.Equal("Test content", item.Content);
        Assert.Equal(WorkspacePriority.Normal, item.Priority);
        Assert.Equal("TestSource", item.Source);
        Assert.Contains("test", item.Tags);
    }

    [Fact]
    public void GetItems_Should_ReturnItemsOrderedByAttentionWeight()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Low priority", WorkspacePriority.Low, "Source1");
        workspace.AddItem("High priority", WorkspacePriority.High, "Source2");
        workspace.AddItem("Critical priority", WorkspacePriority.Critical, "Source3");

        // Act
        List<WorkspaceItem> items = workspace.GetItems();

        // Assert
        Assert.Equal(3, items.Count);
        // Critical should be first (highest attention weight)
        Assert.Equal(WorkspacePriority.Critical, items[0].Priority);
    }

    [Fact]
    public void GetHighPriorityItems_Should_FilterByPriority()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Low", WorkspacePriority.Low, "Source");
        workspace.AddItem("High", WorkspacePriority.High, "Source");
        workspace.AddItem("Critical", WorkspacePriority.Critical, "Source");

        // Act
        List<WorkspaceItem> highPriorityItems = workspace.GetHighPriorityItems();

        // Assert
        Assert.Equal(2, highPriorityItems.Count);
        Assert.All(highPriorityItems, item => Assert.True(item.Priority >= WorkspacePriority.High));
    }

    [Fact]
    public void BroadcastItem_Should_AddToBroadcastHistory()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        WorkspaceItem item = workspace.AddItem(
            "Important announcement",
            WorkspacePriority.Critical,
            "System");

        // Act
        workspace.BroadcastItem(item, "Critical update");
        List<WorkspaceBroadcast> broadcasts = workspace.GetRecentBroadcasts();

        // Assert
        Assert.NotEmpty(broadcasts);
        Assert.Contains(broadcasts, b => b.Item.Content == "Important announcement");
    }

    [Fact]
    public void SearchByTags_Should_FindMatchingItems()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Item 1", WorkspacePriority.Normal, "Source", new List<string> { "alpha", "beta" });
        workspace.AddItem("Item 2", WorkspacePriority.Normal, "Source", new List<string> { "gamma" });
        workspace.AddItem("Item 3", WorkspacePriority.Normal, "Source", new List<string> { "alpha", "delta" });

        // Act
        List<WorkspaceItem> results = workspace.SearchByTags(new List<string> { "alpha" });

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, item => Assert.Contains("alpha", item.Tags));
    }

    [Fact]
    public void ApplyAttentionPolicies_Should_RemoveExpiredItems()
    {
        // Arrange
        var policy = new AttentionPolicy(
            MaxWorkspaceSize: 100,
            MaxHighPriorityItems: 20,
            DefaultItemLifetime: TimeSpan.FromMilliseconds(1), // Very short lifetime
            MinAttentionThreshold: 0.3);

        var workspace = new GlobalWorkspace(policy);
        workspace.AddItem("Test item", WorkspacePriority.Normal, "Source");

        // Act
        Thread.Sleep(10); // Wait for item to expire
        workspace.ApplyAttentionPolicies();
        List<WorkspaceItem> items = workspace.GetItems();

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public void GetStatistics_Should_ReturnAccurateMetrics()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Item 1", WorkspacePriority.Low, "Source1");
        workspace.AddItem("Item 2", WorkspacePriority.High, "Source1");
        workspace.AddItem("Item 3", WorkspacePriority.Critical, "Source2");

        // Act
        WorkspaceStatistics stats = workspace.GetStatistics();

        // Assert
        Assert.Equal(3, stats.TotalItems);
        Assert.Equal(2, stats.HighPriorityItems);
        Assert.Equal(1, stats.CriticalItems);
        Assert.Equal(2, stats.ItemsBySource.Count);
    }

    [Fact]
    public void RemoveItem_Should_RemoveItemFromWorkspace()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        WorkspaceItem item = workspace.AddItem("Test", WorkspacePriority.Normal, "Source");

        // Act
        bool removed = workspace.RemoveItem(item.Id);
        List<WorkspaceItem> items = workspace.GetItems();

        // Assert
        Assert.True(removed);
        Assert.Empty(items);
    }
}
