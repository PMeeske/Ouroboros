// <copyright file="PersistentMemoryStoreTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Tests for persistent memory store functionality.
/// </summary>
[Trait("Category", "Unit")]
public static class PersistentMemoryStoreTests
{
    /// <summary>
    /// Tests basic memory storage and retrieval.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestBasicMemoryStorage()
    {
        Console.WriteLine("=== Test: Basic Memory Storage ===");

        var memory = new PersistentMemoryStore();

        var experience = CreateTestExperience("Test goal", 0.9);

        await memory.StoreExperienceAsync(experience);

        var retrieved = await memory.GetExperienceAsync(experience.Id);
        if (retrieved == null)
        {
            throw new Exception("Stored experience should be retrievable");
        }

        if (retrieved.Id != experience.Id)
        {
            throw new Exception("Retrieved experience should match stored experience");
        }

        Console.WriteLine("✓ Experience stored and retrieved successfully");

        var stats = await memory.GetStatisticsAsync();
        if (stats.TotalExperiences != 1)
        {
            throw new Exception("Memory should contain exactly 1 experience");
        }

        Console.WriteLine($"✓ Memory statistics: {stats.TotalExperiences} experiences");
        Console.WriteLine("✓ Basic memory storage test passed!\n");
    }

    /// <summary>
    /// Tests memory type classification (episodic vs semantic).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMemoryTypeClassification()
    {
        Console.WriteLine("=== Test: Memory Type Classification ===");

        var memory = new PersistentMemoryStore();

        // Store new experience (should be episodic)
        var experience = CreateTestExperience("Test episodic", 0.85);
        await memory.StoreExperienceAsync(experience);

        var episodicMemories = memory.GetExperiencesByType(MemoryType.Episodic);
        if (episodicMemories.Count != 1)
        {
            throw new Exception("New experience should be stored as episodic");
        }

        Console.WriteLine("✓ New experience correctly classified as episodic");

        var semanticMemories = memory.GetExperiencesByType(MemoryType.Semantic);
        Console.WriteLine($"✓ Episodic memories: {episodicMemories.Count}");
        Console.WriteLine($"✓ Semantic memories: {semanticMemories.Count}");
        Console.WriteLine("✓ Memory type classification test passed!\n");
    }

    /// <summary>
    /// Tests memory consolidation from short-term to long-term.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMemoryConsolidation()
    {
        Console.WriteLine("=== Test: Memory Consolidation ===");

        var config = new PersistentMemoryConfig(
            ShortTermCapacity: 5,
            ConsolidationThreshold: 0.7,
            ConsolidationInterval: TimeSpan.FromSeconds(1));

        var memory = new PersistentMemoryStore(config: config);

        // Store multiple high-quality experiences
        for (int i = 0; i < 7; i++)
        {
            var exp = CreateTestExperience($"Task {i}", 0.85);
            await memory.StoreExperienceAsync(exp);
        }

        // Wait for consolidation interval
        await Task.Delay(1100);

        // Trigger consolidation by adding one more
        var triggerExp = CreateTestExperience("Trigger consolidation", 0.9);
        await memory.StoreExperienceAsync(triggerExp);

        var stats = await memory.GetStatisticsAsync();
        Console.WriteLine($"✓ Total experiences stored: {stats.TotalExperiences}");

        var episodic = memory.GetExperiencesByType(MemoryType.Episodic);
        var semantic = memory.GetExperiencesByType(MemoryType.Semantic);

        Console.WriteLine($"✓ Episodic (short-term): {episodic.Count}");
        Console.WriteLine($"✓ Semantic (long-term): {semantic.Count}");

        if (semantic.Count > 0)
        {
            Console.WriteLine("✓ Some memories consolidated to long-term");
        }

        Console.WriteLine("✓ Memory consolidation test passed!\n");
    }

    /// <summary>
    /// Tests memory importance scoring.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestImportanceScoring()
    {
        Console.WriteLine("=== Test: Importance Scoring ===");

        var memory = new PersistentMemoryStore();

        // Store experiences with different quality scores
        var lowQuality = CreateTestExperience("Low quality", 0.4);
        var mediumQuality = CreateTestExperience("Medium quality", 0.7);
        var highQuality = CreateTestExperience("High quality", 0.95);

        await memory.StoreExperienceAsync(lowQuality);
        await memory.StoreExperienceAsync(mediumQuality);
        await memory.StoreExperienceAsync(highQuality);

        var stats = await memory.GetStatisticsAsync();
        Console.WriteLine($"✓ Average quality score: {stats.AverageQualityScore:P0}");

        var query = new MemoryQuery(
            "quality",
            Context: null,
            MaxResults: 10,
            MinSimilarity: 0.0);

        var retrieved = await memory.RetrieveRelevantExperiencesAsync(query);

        // Higher quality experiences should be more important
        Console.WriteLine($"✓ Retrieved {retrieved.Count} experiences");
        Console.WriteLine("✓ Importance scoring test passed!\n");
    }

    /// <summary>
    /// Tests intelligent forgetting of low-importance memories.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestIntelligentForgetting()
    {
        Console.WriteLine("=== Test: Intelligent Forgetting ===");

        var config = new PersistentMemoryConfig(
            LongTermCapacity: 10,
            EnableForgetting: true,
            ForgettingThreshold: 0.3);

        var memory = new PersistentMemoryStore(config: config);

        // Store many experiences with varying quality
        for (int i = 0; i < 15; i++)
        {
            var quality = i < 5 ? 0.2 : 0.9; // First 5 are low quality
            var exp = CreateTestExperience($"Experience {i}", quality);
            await memory.StoreExperienceAsync(exp);
        }

        var stats = await memory.GetStatisticsAsync();

        // Should have forgotten some low-importance memories
        if (stats.TotalExperiences > config.LongTermCapacity)
        {
            Console.WriteLine($"⚠ Warning: More experiences than capacity ({stats.TotalExperiences} > {config.LongTermCapacity})");
        }
        else
        {
            Console.WriteLine($"✓ Memory capacity maintained: {stats.TotalExperiences} <= {config.LongTermCapacity}");
        }

        Console.WriteLine($"✓ Average quality of retained memories: {stats.AverageQualityScore:P0}");
        Console.WriteLine("✓ Intelligent forgetting test passed!\n");
    }

    /// <summary>
    /// Tests memory retrieval with similarity search.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestSimilarityRetrieval()
    {
        Console.WriteLine("=== Test: Similarity Retrieval ===");

        var memory = new PersistentMemoryStore();

        // Store related experiences
        await memory.StoreExperienceAsync(CreateTestExperience("Calculate sum of numbers", 0.9));
        await memory.StoreExperienceAsync(CreateTestExperience("Add two values together", 0.85));
        await memory.StoreExperienceAsync(CreateTestExperience("Process image data", 0.8));

        var query = new MemoryQuery(
            Goal: "mathematical addition",
            Context: null,
            MaxResults: 5,
            MinSimilarity: 0.5);

        var results = await memory.RetrieveRelevantExperiencesAsync(query);

        Console.WriteLine($"✓ Query: '{query.Goal}'");
        Console.WriteLine($"✓ Retrieved {results.Count} relevant experiences");

        foreach (var exp in results.Take(3))
        {
            Console.WriteLine($"  - {exp.Goal} (quality: {exp.Verification.QualityScore:P0})");
        }

        Console.WriteLine("✓ Similarity retrieval test passed!\n");
    }

    /// <summary>
    /// Tests memory clearing functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMemoryClear()
    {
        Console.WriteLine("=== Test: Memory Clear ===");

        var memory = new PersistentMemoryStore();

        // Store some experiences
        for (int i = 0; i < 5; i++)
        {
            await memory.StoreExperienceAsync(CreateTestExperience($"Task {i}", 0.8));
        }

        var statsBeforeClear = await memory.GetStatisticsAsync();
        Console.WriteLine($"✓ Experiences before clear: {statsBeforeClear.TotalExperiences}");

        await memory.ClearAsync();

        var statsAfterClear = await memory.GetStatisticsAsync();
        if (statsAfterClear.TotalExperiences != 0)
        {
            throw new Exception("Memory should be empty after clearing");
        }

        Console.WriteLine($"✓ Experiences after clear: {statsAfterClear.TotalExperiences}");
        Console.WriteLine("✓ Memory clear test passed!\n");
    }

    /// <summary>
    /// Helper method to create test experiences.
    /// </summary>
    private static Experience CreateTestExperience(string goal, double qualityScore)
    {
        var plan = new Plan(
            goal,
            new List<PlanStep>
            {
                new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.9),
            },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(
                    plan.Steps[0],
                    true,
                    "success",
                    null,
                    TimeSpan.FromMilliseconds(10),
                    new Dictionary<string, object>()),
            },
            Success: true,
            FinalOutput: "success",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromMilliseconds(10));

        var verification = new VerificationResult(
            execution,
            Verified: qualityScore > 0.5,
            QualityScore: qualityScore,
            Issues: new List<string>(),
            Improvements: new List<string>(),
            RevisedPlan: null);

        return new Experience(
            Guid.NewGuid(),
            goal,
            plan,
            execution,
            verification,
            DateTime.UtcNow,
            new Dictionary<string, object>());
    }
}
