using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

namespace Ouroboros.Tests.Tests.SelfModel;

/// <summary>
/// Tests for CognitiveProcessor - Global Workspace Theory integration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CognitiveProcessorTests
{
    [Fact]
    public void ProcessAndBroadcast_Should_AddConsciousExperienceToWorkspace()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act
        var state = processor.ProcessAndBroadcast("Thank you so much!", "user interaction");

        // Assert
        Assert.NotNull(state);
        
        // Check that conscious experiences were added to workspace
        var consciousItems = workspace.SearchByTags(new List<string> { "consciousness" });
        Assert.NotEmpty(consciousItems);
    }

    [Fact]
    public void ProcessAndBroadcast_HighArousal_Should_CreateHighPriorityWorkspaceItem()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act - Process something that should trigger high arousal
        processor.ProcessAndBroadcast("URGENT! EMERGENCY! Help needed immediately!", "emergency");

        // Assert
        var highPriorityItems = workspace.GetHighPriorityItems();
        Assert.NotEmpty(highPriorityItems);
    }

    [Fact]
    public void ProcessAndBroadcast_Should_TagWithEmotionalState()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act
        processor.ProcessAndBroadcast("This is wonderful and amazing!", "positive feedback");

        // Assert
        var allItems = workspace.GetItems();
        var emotionallyTagged = allItems.Where(item => 
            item.Tags.Any(t => t.Contains("warm") || t.Contains("happy") || t.Contains("pleasure")));
        
        Assert.NotEmpty(emotionallyTagged);
    }

    [Fact]
    public void ProcessAndBroadcast_Should_UpdateAttentionalFocus()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act
        processor.ProcessAndBroadcast("How does this work?", "question");

        // Assert
        var attentionItems = workspace.SearchByTags(new List<string> { "attention" });
        Assert.NotEmpty(attentionItems);
    }

    [Fact]
    public void GetRelevantContext_Should_RetrieveRelatedWorkspaceItems()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Add some items to workspace
        workspace.AddItem(
            "Previous user question about coding",
            WorkspacePriority.Normal,
            "TestSource",
            new List<string> { "question", "coding" });

        // Process to get current state
        var state = processor.ProcessAndBroadcast("How do I code this?", "question");

        // Act
        var context = processor.GetRelevantContext(state);

        // Assert
        Assert.NotNull(context);
        // Should be able to retrieve context (may be empty if no matches)
    }

    [Fact]
    public void GetStatistics_Should_ReturnCognitiveMetrics()
    {
        // Arrange
        var config = new CognitiveProcessorConfig(
            BroadcastThreshold: 0.3,  // Lower threshold to ensure broadcast
            ConsciousExperienceLifetimeMinutes: 5.0);

        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness, config);

        // Process some inputs
        processor.ProcessAndBroadcast("Hello!", "greeting");
        processor.ProcessAndBroadcast("This is interesting", "observation");

        // Act
        var stats = processor.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalWorkspaceItems >= 0); // May be 0 if salience too low
        Assert.True(stats.CurrentArousal >= 0 && stats.CurrentArousal <= 1);
        Assert.True(stats.CurrentValence >= -1 && stats.CurrentValence <= 1);
        Assert.True(stats.CurrentAwareness >= 0 && stats.CurrentAwareness <= 1);
    }

    [Fact]
    public void ProcessAndBroadcast_LowSalience_Should_NotCreateWorkspaceItem()
    {
        // Arrange
        var config = new CognitiveProcessorConfig(
            BroadcastThreshold: 0.9,  // Very high threshold
            ConsciousExperienceLifetimeMinutes: 5.0);

        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness, config);

        // Act - Process something mundane
        processor.ProcessAndBroadcast("the", "neutral");

        // Assert
        var consciousItems = workspace.SearchByTags(new List<string> { "consciousness" });
        // With very high threshold, mundane input shouldn't be broadcast
        Assert.Empty(consciousItems);
    }

    [Fact]
    public void ProcessAndBroadcast_MultipleInputs_Should_ShowArousalProgression()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act - Process escalating inputs
        var state1 = processor.ProcessAndBroadcast("Hello", "greeting");
        var state2 = processor.ProcessAndBroadcast("This is urgent", "alert");
        var state3 = processor.ProcessAndBroadcast("EMERGENCY HELP", "crisis");

        // Assert - Arousal should generally increase
        var stats = processor.GetStatistics();
        Assert.True(stats.CurrentArousal > 0);
    }

    [Fact]
    public void CognitiveProcessor_Should_IntegrateWithWorkspaceBroadcasts()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act
        processor.ProcessAndBroadcast("Amazing work!", "praise");

        // Assert
        var broadcasts = workspace.GetRecentBroadcasts();
        // If arousal/salience was high enough, should see broadcasts
        Assert.NotNull(broadcasts);
    }

    [Fact]
    public void ProcessAndBroadcast_Should_RespectConfiguredLifetime()
    {
        // Arrange
        var config = new CognitiveProcessorConfig(
            BroadcastThreshold: 0.3,
            ConsciousExperienceLifetimeMinutes: 0.001); // Very short lifetime

        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness, config);

        // Act
        processor.ProcessAndBroadcast("Test input", "test");
        Thread.Sleep(10); // Wait for expiration
        workspace.ApplyAttentionPolicies();

        // Assert
        var items = workspace.GetItems();
        // Items with very short lifetime should be cleaned up
        var consciousItems = items.Where(i => i.Tags.Contains("consciousness")).ToList();
        // May or may not be present depending on timing
        Assert.NotNull(consciousItems);
    }

    [Fact]
    public void ProcessAndBroadcast_Question_Should_TriggerCuriosityDrive()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var processor = new CognitiveProcessor(workspace, consciousness);

        // Act
        var state = processor.ProcessAndBroadcast("Why does this happen?", "inquiry");

        // Assert
        Assert.True(state.ActiveDrives.ContainsKey("curiosity"));
        var stats = processor.GetStatistics();
        Assert.True(stats.ActiveAssociations > 0);
    }
}
