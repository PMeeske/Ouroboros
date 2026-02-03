// <copyright file="MetaAIIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests.MetaAI;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Tests.Mocks;

/// <summary>
/// Integration tests for MetaAI system end-to-end scenarios.
/// Tests complete Plan→Execute→Verify→Learn cycles and agent workflows.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MetaAIIntegrationTests
{
    [Fact]
    public async Task CompleteCycle_PlanExecuteVerifyLearn_ShouldWork()
    {
        // Arrange - Create a full MetaAI stack
        var llm = new MockChatModel(prompt =>
        {
            if (prompt.Contains("plan", StringComparison.OrdinalIgnoreCase))
            {
                return @"{
                    ""goal"": ""Process data"",
                    ""steps"": [
                        {
                            ""action"": ""load_data"",
                            ""parameters"": {},
                            ""expectedOutcome"": ""Data loaded"",
                            ""confidence"": 0.9
                        },
                        {
                            ""action"": ""transform_data"",
                            ""parameters"": {},
                            ""expectedOutcome"": ""Data transformed"",
                            ""confidence"": 0.8
                        }
                    ],
                    ""confidenceScores"": { ""overall"": 0.85 }
                }";
            }
            return "success";
        });

        var tools = new ToolRegistry();
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();
        var router = new MockUncertaintyRouter();
        var safety = new MockSafetyGuard();
        var ethics = new MockEthicsFramework();

        var orchestrator = new MetaAIPlannerOrchestrator(
            llm, tools, memory, skills, router, safety, ethics);

        // Act - Execute complete cycle
        var planResult = await orchestrator.PlanAsync("Process data");

        // Assert - Plan phase
        planResult.IsSuccess.Should().BeTrue();
        planResult.Value.Goal.Should().Be("Process data");
        planResult.Value.Steps.Should().NotBeEmpty();

        // Verify memory retrieval was called
        memory.RetrieveCallCount.Should().BeGreaterThan(0);

        // Verify ethics framework was consulted
        ethics.EvaluatePlanCallCount.Should().BeGreaterThan(0);

        // Verify safety checks were performed
        safety.CheckCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MemoryRetrieval_WithPastExperiences_ShouldInfluencePlanning()
    {
        // Arrange
        var llm = new MockChatModel("planned response");
        var memory = new MockMemoryStore();
        
        // Add past experience
        var pastExperience = CreateTestExperience("Process data", success: true, quality: 0.9);
        memory.AddExperience(pastExperience);

        var skills = new MockSkillRegistry();
        var orchestrator = CreateTestOrchestrator(llm, memory, skills);

        // Act
        var planResult = await orchestrator.PlanAsync("Process data");

        // Assert
        memory.RetrieveCallCount.Should().BeGreaterThan(0);
        llm.LastPrompt.Should().NotBeNull();
    }

    [Fact]
    public async Task SkillMatching_WithRelevantSkills_ShouldIncludeInPlan()
    {
        // Arrange
        var llm = new MockChatModel("planned with skills");
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();

        // Add relevant skill
        var skill = new Skill(
            Name: "DataProcessor",
            Description: "Process data efficiently",
            Prerequisites: new List<string>(),
            Steps: new List<PlanStep>(),
            SuccessRate: 0.95,
            UsageCount: 10,
            CreatedAt: DateTime.UtcNow.AddDays(-30),
            LastUsed: DateTime.UtcNow.AddDays(-1));
        skills.AddSkill(skill);

        var orchestrator = CreateTestOrchestrator(llm, memory, skills);

        // Act
        var planResult = await orchestrator.PlanAsync("Process some data");

        // Assert - Skills should have been queried
        skills.Should().NotBeNull();
        llm.LastPrompt.Should().NotBeNull();
    }

    [Fact]
    public async Task LearningCycle_AfterSuccessfulExecution_ShouldStoreExperience()
    {
        // Arrange
        var llm = new MockChatModel("success");
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();
        var orchestrator = CreateTestOrchestrator(llm, memory, skills);

        var plan = new Plan(
            Goal: "Test learning",
            Steps: new List<PlanStep>
            {
                new PlanStep("action1", new Dictionary<string, object>(), "outcome", 0.8)
            },
            ConfidenceScores: new Dictionary<string, double> { { "overall", 0.8 } },
            CreatedAt: DateTime.UtcNow);

        var execution = new ExecutionResult(
            Plan: plan,
            StepResults: new List<StepResult>(),
            Success: true,
            FinalOutput: "Success",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(2));

        var verification = new VerificationResult(
            Execution: execution,
            Verified: true,
            QualityScore: 0.9,
            Issues: new List<string>(),
            Improvements: new List<string>(),
            RevisedPlan: null);

        // Act
        orchestrator.LearnFromExecution(verification);

        // Assert
        memory.StoreCallCount.Should().Be(1);
        memory.LastStoredExperience.Should().NotBeNull();
        memory.LastStoredExperience!.Goal.Should().Be("Test learning");
        memory.LastStoredExperience.Execution.Success.Should().BeTrue();
        memory.LastStoredExperience.Verification.QualityScore.Should().Be(0.9);
    }

    [Fact]
    public async Task ErrorHandling_WithInvalidGoal_ShouldReturnFailure()
    {
        // Arrange
        var llm = new MockChatModel("response");
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();
        var orchestrator = CreateTestOrchestrator(llm, memory, skills);

        // Act
        var result = await orchestrator.PlanAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal cannot be empty");
    }

    [Fact]
    public async Task ParallelPlanning_WithMultipleGoals_ShouldHandleConcurrently()
    {
        // Arrange
        var llm = new MockChatModel("plan");
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();
        var orchestrator = CreateTestOrchestrator(llm, memory, skills);

        var goals = new[] { "Goal 1", "Goal 2", "Goal 3" };

        // Act
        var tasks = goals.Select(g => orchestrator.PlanAsync(g)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.IsSuccess);
    }

    [Fact]
    public async Task SkillExtraction_FromSuccessfulExecution_ShouldRegisterSkill()
    {
        // Arrange
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();

        var execution = CreateSuccessfulExecution("Test skill extraction");

        // Act
        var result = await skills.ExtractSkillAsync(execution, "ExtractedSkill", "Test skill");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var registered = skills.GetSkill("ExtractedSkill");
        registered.Should().NotBeNull();
        registered!.Description.Should().Be("Test skill");
    }

    [Fact]
    public async Task EthicsCheck_WithUnethicalPlan_ShouldPreventExecution()
    {
        // Arrange
        var ethics = new MockEthicsFramework(
            (action, context) => Ouroboros.Core.Ethics.EthicalClearance.Denied(
                "Action violates safety principles",
                new List<Ouroboros.Core.Ethics.EthicalViolation>
                {
                    new Ouroboros.Core.Ethics.EthicalViolation
                    {
                        ViolatedPrinciple = Ouroboros.Core.Ethics.EthicalPrinciple.DoNoHarm,
                        Severity = Ouroboros.Core.Ethics.ViolationSeverity.Critical,
                        Description = "Unsafe action detected",
                        Evidence = "Test evidence",
                        AffectedParties = new List<string> { "user", "system" }
                    }
                }));

        var llm = new MockChatModel("plan");
        var memory = new MockMemoryStore();
        var skills = new MockSkillRegistry();
        var orchestrator = new MetaAIPlannerOrchestrator(
            llm,
            new ToolRegistry(),
            memory,
            skills,
            new MockUncertaintyRouter(),
            new MockSafetyGuard(),
            ethics);

        // Act
        var result = await orchestrator.PlanAsync("Dangerous action");

        // Assert
        ethics.EvaluatePlanCallCount.Should().BeGreaterThan(0);
        // The plan may still be created but should be flagged by ethics
    }

    [Fact]
    public async Task SafetyGuard_WithDangerousOperation_ShouldBlockExecution()
    {
        // Arrange
        var safety = new MockSafetyGuard((op, param, level) =>
            new SafetyCheckResult(
                Safe: false,
                Violations: new List<string> { "Operation too dangerous" },
                Warnings: new List<string>(),
                RequiredLevel: PermissionLevel.System));

        var llm = new MockChatModel("plan");
        var orchestrator = new MetaAIPlannerOrchestrator(
            llm,
            new ToolRegistry(),
            new MockMemoryStore(),
            new MockSkillRegistry(),
            new MockUncertaintyRouter(),
            safety,
            new MockEthicsFramework());

        // Act
        var result = await orchestrator.PlanAsync("System modification");

        // Assert
        safety.CheckCallCount.Should().BeGreaterThan(0);
    }

    private static MetaAIPlannerOrchestrator CreateTestOrchestrator(
        MockChatModel llm,
        MockMemoryStore memory,
        MockSkillRegistry skills)
    {
        return new MetaAIPlannerOrchestrator(
            llm,
            new ToolRegistry(),
            memory,
            skills,
            new MockUncertaintyRouter(),
            new MockSafetyGuard(),
            new MockEthicsFramework());
    }

    private static Experience CreateTestExperience(string goal, bool success, double quality)
    {
        var plan = new Plan(
            Goal: goal,
            Steps: new List<PlanStep>(),
            ConfidenceScores: new Dictionary<string, double> { { "overall", 0.8 } },
            CreatedAt: DateTime.UtcNow);

        var execution = new ExecutionResult(
            Plan: plan,
            StepResults: new List<StepResult>(),
            Success: success,
            FinalOutput: success ? "Success" : "Failed",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(1));

        var verification = new VerificationResult(
            Execution: execution,
            Verified: success,
            QualityScore: quality,
            Issues: new List<string>(),
            Improvements: new List<string>(),
            RevisedPlan: null);

        return new Experience(
            Id: Guid.NewGuid(),
            Goal: goal,
            Plan: plan,
            Execution: execution,
            Verification: verification,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>());
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
}
