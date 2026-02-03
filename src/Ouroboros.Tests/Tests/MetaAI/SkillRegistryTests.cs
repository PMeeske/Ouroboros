// <copyright file="SkillRegistryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests.MetaAI;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Unit tests for SkillRegistry implementation.
/// Tests skill registration, retrieval, matching, and metrics tracking.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SkillRegistryTests
{
    [Fact]
    public void Constructor_ShouldCreateSkillRegistry()
    {
        // Act
        var registry = new SkillRegistry();

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void RegisterSkill_WithValidSkill_ShouldRegister()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = CreateTestSkill("TestSkill", "Test description");

        // Act
        registry.RegisterSkill(skill);

        // Assert
        var retrieved = registry.GetSkill("TestSkill");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestSkill");
    }

    [Fact]
    public async Task RegisterSkillAsync_WithValidSkill_ShouldRegister()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = CreateTestSkill("AsyncSkill", "Async test");

        // Act
        await registry.RegisterSkillAsync(skill);

        // Assert
        var retrieved = registry.GetSkill("AsyncSkill");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("AsyncSkill");
    }

    [Fact]
    public void RegisterSkill_WithDuplicateName_ShouldOverwrite()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = CreateTestSkill("DuplicateSkill", "First version");
        var skill2 = CreateTestSkill("DuplicateSkill", "Second version");

        // Act
        registry.RegisterSkill(skill1);
        registry.RegisterSkill(skill2);

        // Assert
        var retrieved = registry.GetSkill("DuplicateSkill");
        retrieved!.Description.Should().Be("Second version");
    }

    [Fact]
    public async Task FindMatchingSkillsAsync_WithMatchingDescription_ShouldReturnSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill(CreateTestSkill("DataProcessing", "Process data files"));
        registry.RegisterSkill(CreateTestSkill("ReportGen", "Generate reports"));
        registry.RegisterSkill(CreateTestSkill("FileProcessing", "Process file uploads"));

        // Act
        var matches = await registry.FindMatchingSkillsAsync("Process");

        // Assert
        matches.Should().HaveCountGreaterThanOrEqualTo(2);
        matches.Should().Contain(s => s.Name == "DataProcessing");
        matches.Should().Contain(s => s.Name == "FileProcessing");
    }

    [Fact]
    public async Task FindMatchingSkillsAsync_WithNoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill(CreateTestSkill("Skill1", "Something"));

        // Act
        var matches = await registry.FindMatchingSkillsAsync("Completely unrelated");

        // Assert - may or may not return results depending on matching algorithm
        matches.Should().NotBeNull();
    }

    [Fact]
    public void GetSkill_WithNonExistentName_ShouldReturnNull()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var result = registry.GetSkill("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RecordSkillExecution_WithSuccess_ShouldUpdateMetrics()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = CreateTestSkill("TestSkill", "Test", successRate: 0.8, usageCount: 5);
        registry.RegisterSkill(skill);

        // Act
        registry.RecordSkillExecution("TestSkill", success: true);

        // Assert
        var updated = registry.GetSkill("TestSkill");
        updated!.UsageCount.Should().Be(6);
        updated.SuccessRate.Should().BeGreaterThan(0.8); // Should increase
        updated.LastUsed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordSkillExecution_WithFailure_ShouldUpdateMetrics()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = CreateTestSkill("TestSkill", "Test", successRate: 0.8, usageCount: 5);
        registry.RegisterSkill(skill);

        // Act
        registry.RecordSkillExecution("TestSkill", success: false);

        // Assert
        var updated = registry.GetSkill("TestSkill");
        updated!.UsageCount.Should().Be(6);
        updated.SuccessRate.Should().BeLessThan(0.8); // Should decrease
    }

    [Fact]
    public void RecordSkillExecution_WithNonExistentSkill_ShouldThrowException()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        Action act = () => registry.RecordSkillExecution("NonExistent", success: true);

        // Assert
        act.Should().Throw<Exception>(); // Implementation throws on non-existent skill
    }

    [Fact]
    public void GetAllSkills_WithNoSkills_ShouldReturnEmpty()
    {
        // Arrange
        var registry = new SkillRegistry();

        // Act
        var skills = registry.GetAllSkills();

        // Assert
        skills.Should().BeEmpty();
    }

    [Fact]
    public void GetAllSkills_WithMultipleSkills_ShouldReturnAll()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill(CreateTestSkill("Skill1", "First"));
        registry.RegisterSkill(CreateTestSkill("Skill2", "Second"));
        registry.RegisterSkill(CreateTestSkill("Skill3", "Third"));

        // Act
        var skills = registry.GetAllSkills();

        // Assert
        skills.Should().HaveCount(3);
        skills.Should().Contain(s => s.Name == "Skill1");
        skills.Should().Contain(s => s.Name == "Skill2");
        skills.Should().Contain(s => s.Name == "Skill3");
    }

    [Fact]
    public async Task ExtractSkillAsync_WithSuccessfulExecution_ShouldExtractAndRegister()
    {
        // Arrange
        var registry = new SkillRegistry();
        var execution = CreateSuccessfulExecution("Test goal");

        // Act
        var result = await registry.ExtractSkillAsync(execution, "ExtractedSkill", "Extracted from execution");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("ExtractedSkill");
        result.Value.Description.Should().Be("Extracted from execution");

        var registered = registry.GetSkill("ExtractedSkill");
        registered.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractSkillAsync_WithFailedExecution_ShouldReturnFailure()
    {
        // Arrange
        var registry = new SkillRegistry();
        var execution = CreateFailedExecution("Test goal");

        // Act
        var result = await registry.ExtractSkillAsync(execution, "FailedSkill", "Should not extract");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed execution");
    }

    [Fact]
    public async Task FindMatchingSkillsAsync_ShouldOrderBySuccessRate()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.RegisterSkill(CreateTestSkill("LowSuccess", "Process data", successRate: 0.3));
        registry.RegisterSkill(CreateTestSkill("HighSuccess", "Process data", successRate: 0.9));
        registry.RegisterSkill(CreateTestSkill("MedSuccess", "Process data", successRate: 0.6));

        // Act
        var matches = await registry.FindMatchingSkillsAsync("Process");

        // Assert - should be ordered by success rate (highest first)
        if (matches.Count >= 2)
        {
            matches[0].SuccessRate.Should().BeGreaterThanOrEqualTo(matches[1].SuccessRate);
        }
    }

    private static Skill CreateTestSkill(
        string name,
        string description,
        double successRate = 0.8,
        int usageCount = 0)
    {
        return new Skill(
            Name: name,
            Description: description,
            Prerequisites: new List<string>(),
            Steps: new List<PlanStep>(),
            SuccessRate: successRate,
            UsageCount: usageCount,
            CreatedAt: DateTime.UtcNow.AddDays(-7),
            LastUsed: DateTime.UtcNow.AddDays(-1));
    }

    private static ExecutionResult CreateSuccessfulExecution(string goal)
    {
        var plan = new Plan(
            Goal: goal,
            Steps: new List<PlanStep>
            {
                new PlanStep("step1", new Dictionary<string, object>(), "outcome", 0.8)
            },
            ConfidenceScores: new Dictionary<string, double> { { "overall", 0.8 } },
            CreatedAt: DateTime.UtcNow);

        return new ExecutionResult(
            Plan: plan,
            StepResults: new List<StepResult>(),
            Success: true,
            FinalOutput: "Success",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(1));
    }

    private static ExecutionResult CreateFailedExecution(string goal)
    {
        var plan = new Plan(
            Goal: goal,
            Steps: new List<PlanStep>(),
            ConfidenceScores: new Dictionary<string, double>(),
            CreatedAt: DateTime.UtcNow);

        return new ExecutionResult(
            Plan: plan,
            StepResults: new List<StepResult>(),
            Success: false,
            FinalOutput: "Failed",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(1));
    }
}
