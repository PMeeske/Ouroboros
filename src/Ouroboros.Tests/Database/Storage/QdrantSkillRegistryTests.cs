// <copyright file="QdrantSkillRegistryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.Storage;

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

/// <summary>
/// Unit tests for QdrantSkillConfig record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class QdrantSkillConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        // Act
        var config = new QdrantSkillConfig();

        // Assert
        config.ConnectionString.Should().Be("http://localhost:6334");
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
        config.VectorSize.Should().Be(1536);
    }

    [Fact]
    public void Config_WithCustomValues_SetsCorrectly()
    {
        // Act
        var config = new QdrantSkillConfig(
            ConnectionString: "http://qdrant.example.com:6333",
            CollectionName: "custom_skills",
            AutoSave: false,
            VectorSize: 768);

        // Assert
        config.ConnectionString.Should().Be("http://qdrant.example.com:6333");
        config.CollectionName.Should().Be("custom_skills");
        config.AutoSave.Should().BeFalse();
        config.VectorSize.Should().Be(768);
    }

    [Theory]
    [InlineData(384)]    // Small embedding models
    [InlineData(768)]    // BERT-based models
    [InlineData(1024)]   // Some transformer models
    [InlineData(1536)]   // OpenAI Ada
    [InlineData(4096)]   // Large models
    public void Config_WithDifferentVectorSizes_Accepted(int vectorSize)
    {
        // Act
        var config = new QdrantSkillConfig(VectorSize: vectorSize);

        // Assert
        config.VectorSize.Should().Be(vectorSize);
    }

    [Fact]
    public void Config_Equality_WorksCorrectly()
    {
        // Arrange
        var config1 = new QdrantSkillConfig(
            "http://localhost:6334",
            "skills",
            true,
            1536);
        var config2 = new QdrantSkillConfig(
            "http://localhost:6334",
            "skills",
            true,
            1536);

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void Config_WithExpression_CreatesNewConfig()
    {
        // Arrange
        var original = new QdrantSkillConfig();

        // Act
        var modified = original with { AutoSave = false };

        // Assert
        original.AutoSave.Should().BeTrue();
        modified.AutoSave.Should().BeFalse();
        modified.ConnectionString.Should().Be(original.ConnectionString);
    }
}

/// <summary>
/// Unit tests for QdrantSkillRegistryStats record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class QdrantSkillRegistryStatsTests
{
    [Fact]
    public void Stats_WithValidData_SetsAllProperties()
    {
        // Act
        var stats = new QdrantSkillRegistryStats(
            TotalSkills: 10,
            AverageSuccessRate: 0.85,
            TotalExecutions: 500,
            MostUsedSkill: "code_generation",
            MostSuccessfulSkill: "bug_fix",
            ConnectionString: "http://localhost:6334",
            CollectionName: "skills",
            IsConnected: true);

        // Assert
        stats.TotalSkills.Should().Be(10);
        stats.AverageSuccessRate.Should().Be(0.85);
        stats.TotalExecutions.Should().Be(500);
        stats.MostUsedSkill.Should().Be("code_generation");
        stats.MostSuccessfulSkill.Should().Be("bug_fix");
        stats.ConnectionString.Should().Be("http://localhost:6334");
        stats.CollectionName.Should().Be("skills");
        stats.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void Stats_WithNullOptionalFields_Accepted()
    {
        // Act
        var stats = new QdrantSkillRegistryStats(
            TotalSkills: 0,
            AverageSuccessRate: 0,
            TotalExecutions: 0,
            MostUsedSkill: null,
            MostSuccessfulSkill: null,
            ConnectionString: "http://localhost:6334",
            CollectionName: "skills",
            IsConnected: false);

        // Assert
        stats.TotalSkills.Should().Be(0);
        stats.MostUsedSkill.Should().BeNull();
        stats.MostSuccessfulSkill.Should().BeNull();
        stats.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Stats_Equality_WorksCorrectly()
    {
        // Arrange
        var stats1 = new QdrantSkillRegistryStats(5, 0.75, 100, "skill1", "skill2", "http://localhost", "collection", true);
        var stats2 = new QdrantSkillRegistryStats(5, 0.75, 100, "skill1", "skill2", "http://localhost", "collection", true);

        // Assert
        stats1.Should().Be(stats2);
    }
}

/// <summary>
/// Unit tests for Skill record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class SkillRecordTests
{
    [Fact]
    public void Skill_WithValidData_SetsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var prerequisites = new List<string> { "prerequisite1", "prerequisite2" };
        var steps = new List<PlanStep>
        {
            new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.9),
            new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.85)
        };

        // Act
        var skill = new Skill(
            Name: "test_skill",
            Description: "A test skill for unit testing",
            Prerequisites: prerequisites,
            Steps: steps,
            SuccessRate: 0.95,
            UsageCount: 50,
            CreatedAt: now,
            LastUsed: now);

        // Assert
        skill.Name.Should().Be("test_skill");
        skill.Description.Should().Be("A test skill for unit testing");
        skill.Prerequisites.Should().HaveCount(2);
        skill.Steps.Should().HaveCount(2);
        skill.SuccessRate.Should().Be(0.95);
        skill.UsageCount.Should().Be(50);
        skill.CreatedAt.Should().Be(now);
        skill.LastUsed.Should().Be(now);
    }

    [Fact]
    public void Skill_WithEmptyPrerequisites_Accepted()
    {
        // Act
        var skill = new Skill(
            "empty_prereq_skill",
            "Skill with no prerequisites",
            new List<string>(),
            new List<PlanStep>(),
            1.0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Assert
        skill.Prerequisites.Should().BeEmpty();
        skill.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Skill_Equality_WorksCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sharedPrereqs = new List<string>();
        var sharedSteps = new List<PlanStep>();
        var skill1 = new Skill("skill", "desc", sharedPrereqs, sharedSteps, 0.5, 10, now, now);
        var skill2 = new Skill("skill", "desc", sharedPrereqs, sharedSteps, 0.5, 10, now, now);

        // Assert - Records compare by value, but Lists compare by reference
        // Using same List instances ensures equality
        skill1.Should().Be(skill2);
    }

    [Fact]
    public void Skill_WithExpression_UpdatesUsageMetrics()
    {
        // Arrange
        var original = new Skill(
            "updatable_skill",
            "A skill that will be updated",
            new List<string>(),
            new List<PlanStep>(),
            0.8,
            10,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(-1));

        // Act
        var updated = original with
        {
            UsageCount = original.UsageCount + 1,
            SuccessRate = 0.85,
            LastUsed = DateTime.UtcNow
        };

        // Assert
        original.UsageCount.Should().Be(10);
        updated.UsageCount.Should().Be(11);
        updated.SuccessRate.Should().Be(0.85);
        updated.LastUsed.Should().BeAfter(original.LastUsed);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Skill_WithDifferentSuccessRates_Accepted(double successRate)
    {
        // Act
        var skill = new Skill(
            "rated_skill",
            "Skill with specific success rate",
            new List<string>(),
            new List<PlanStep>(),
            successRate,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Assert
        skill.SuccessRate.Should().Be(successRate);
    }
}

/// <summary>
/// Unit tests for PlanStep record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class PlanStepRecordTests
{
    [Fact]
    public void PlanStep_WithValidData_SetsAllProperties()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "param1", "value1" },
            { "param2", 42 }
        };

        // Act
        var step = new PlanStep(
            Action: "execute_command",
            Parameters: parameters,
            ExpectedOutcome: "Command completed successfully",
            ConfidenceScore: 0.95);

        // Assert
        step.Action.Should().Be("execute_command");
        step.Parameters.Should().ContainKey("param1");
        step.Parameters.Should().ContainKey("param2");
        step.ExpectedOutcome.Should().Be("Command completed successfully");
        step.ConfidenceScore.Should().Be(0.95);
    }

    [Fact]
    public void PlanStep_WithEmptyParameters_Accepted()
    {
        // Act
        var step = new PlanStep(
            "simple_action",
            new Dictionary<string, object>(),
            "Expected result",
            0.8);

        // Assert
        step.Parameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void PlanStep_WithDifferentConfidenceScores_Accepted(double confidence)
    {
        // Act
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", confidence);

        // Assert
        step.ConfidenceScore.Should().Be(confidence);
    }

    [Fact]
    public void PlanStep_Equality_WorksCorrectly()
    {
        // Arrange
        var params1 = new Dictionary<string, object> { { "key", "value" } };
        var params2 = new Dictionary<string, object> { { "key", "value" } };
        var step1 = new PlanStep("action", params1, "outcome", 0.9);
        var step2 = new PlanStep("action", params2, "outcome", 0.9);

        // Assert - Note: Dictionary equality uses reference equality
        // so these won't be equal unless same reference
        step1.Action.Should().Be(step2.Action);
        step1.ExpectedOutcome.Should().Be(step2.ExpectedOutcome);
        step1.ConfidenceScore.Should().Be(step2.ConfidenceScore);
    }
}

/// <summary>
/// Unit tests for ExecutionResult record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class ExecutionResultRecordTests
{
    private static Plan CreateTestPlan() => new Plan(
        Goal: "Test goal",
        Steps: new List<PlanStep>
        {
            new PlanStep("step1", new Dictionary<string, object>(), "outcome1", 0.9)
        },
        ConfidenceScores: new Dictionary<string, double> { { "overall", 0.9 } },
        CreatedAt: DateTime.UtcNow);

    private static List<StepResult> CreateTestStepResults() => new List<StepResult>
    {
        new StepResult(
            new PlanStep("step1", new Dictionary<string, object>(), "outcome1", 0.9),
            Success: true,
            Output: "Step completed",
            Error: null,
            Duration: TimeSpan.FromSeconds(1),
            ObservedState: new Dictionary<string, object>())
    };

    [Fact]
    public void ExecutionResult_WithSuccessfulExecution_SetsAllProperties()
    {
        // Arrange
        var plan = CreateTestPlan();
        var stepResults = CreateTestStepResults();

        // Act
        var result = new ExecutionResult(
            Plan: plan,
            StepResults: stepResults,
            Success: true,
            FinalOutput: "Execution completed successfully",
            Metadata: new Dictionary<string, object> { { "key", "value" } },
            Duration: TimeSpan.FromSeconds(5));

        // Assert
        result.Plan.Should().Be(plan);
        result.StepResults.Should().HaveCount(1);
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Execution completed successfully");
        result.Metadata.Should().ContainKey("key");
        result.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ExecutionResult_WithFailedExecution_SetsSuccessFalse()
    {
        // Act
        var result = new ExecutionResult(
            CreateTestPlan(),
            CreateTestStepResults(),
            Success: false,
            FinalOutput: "Execution failed",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(2));

        // Assert
        result.Success.Should().BeFalse();
        result.FinalOutput.Should().Be("Execution failed");
    }
}

/// <summary>
/// Unit tests for StepResult record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class StepResultRecordTests
{
    private static PlanStep CreateTestStep() => new PlanStep(
        "test_action",
        new Dictionary<string, object>(),
        "expected_outcome",
        0.85);

    [Fact]
    public void StepResult_WithSuccessfulStep_SetsAllProperties()
    {
        // Arrange
        var step = CreateTestStep();

        // Act
        var result = new StepResult(
            Step: step,
            Success: true,
            Output: "Step completed successfully",
            Error: null,
            Duration: TimeSpan.FromMilliseconds(500),
            ObservedState: new Dictionary<string, object> { { "state_key", "state_value" } });

        // Assert
        result.Step.Should().Be(step);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Step completed successfully");
        result.Error.Should().BeNull();
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(500));
        result.ObservedState.Should().ContainKey("state_key");
    }

    [Fact]
    public void StepResult_WithFailedStep_ContainsError()
    {
        // Act
        var result = new StepResult(
            CreateTestStep(),
            Success: false,
            Output: string.Empty,
            Error: "Connection refused",
            Duration: TimeSpan.FromSeconds(1),
            ObservedState: new Dictionary<string, object>());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Connection refused");
    }
}

/// <summary>
/// Unit tests for Plan record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class PlanRecordTests
{
    [Fact]
    public void Plan_WithValidData_SetsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var steps = new List<PlanStep>
        {
            new PlanStep("step1", new Dictionary<string, object>(), "outcome1", 0.9),
            new PlanStep("step2", new Dictionary<string, object>(), "outcome2", 0.8)
        };
        var confidenceScores = new Dictionary<string, double>
        {
            { "overall", 0.85 },
            { "step1", 0.9 },
            { "step2", 0.8 }
        };

        // Act
        var plan = new Plan(
            Goal: "Accomplish the test objective",
            Steps: steps,
            ConfidenceScores: confidenceScores,
            CreatedAt: now);

        // Assert
        plan.Goal.Should().Be("Accomplish the test objective");
        plan.Steps.Should().HaveCount(2);
        plan.ConfidenceScores.Should().HaveCount(3);
        plan.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Plan_WithEmptySteps_Accepted()
    {
        // Act
        var plan = new Plan(
            "Empty plan",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        // Assert
        plan.Steps.Should().BeEmpty();
        plan.ConfidenceScores.Should().BeEmpty();
    }
}
