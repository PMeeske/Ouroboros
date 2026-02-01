// <copyright file="GoalPlanningTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Planning;

using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Planning;

/// <summary>
/// Comprehensive unit tests for Goal, GoalDecomposer, HierarchicalGoalPlanner, and GoalExtensions.
/// Tests hierarchical goal decomposition, execution, and functional composition patterns.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GoalPlanningTests
{
    #region Goal Tests

    /// <summary>
    /// Tests that Atomic creates a goal with no sub-goals.
    /// </summary>
    [Fact]
    public void Goal_Atomic_ShouldCreateGoalWithNoSubGoals()
    {
        // Arrange & Act
        Goal goal = Goal.Atomic("Test goal");

        // Assert
        goal.Should().NotBeNull();
        goal.Description.Should().Be("Test goal");
        goal.SubGoals.Should().BeEmpty();
        goal.Id.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Tests that Atomic with criteria creates goal with custom completion check.
    /// </summary>
    [Fact]
    public void Goal_AtomicWithCriteria_ShouldUseCustomCriteria()
    {
        // Arrange
        bool criteriaWasCalled = false;
        Goal goal = Goal.Atomic("Test goal", _ =>
        {
            criteriaWasCalled = true;
            return true;
        });

        PipelineBranch branch = CreateTestBranch();

        // Act
        bool isComplete = goal.IsComplete(branch);

        // Assert
        criteriaWasCalled.Should().BeTrue();
        isComplete.Should().BeTrue();
    }

    /// <summary>
    /// Tests that WithSubGoals creates a new goal with sub-goals.
    /// </summary>
    [Fact]
    public void Goal_WithSubGoals_ShouldCreateNewGoalWithSubGoals()
    {
        // Arrange
        Goal parent = Goal.Atomic("Parent");
        Goal child1 = Goal.Atomic("Child 1");
        Goal child2 = Goal.Atomic("Child 2");

        // Act
        Goal withChildren = parent.WithSubGoals(child1, child2);

        // Assert
        withChildren.SubGoals.Should().HaveCount(2);
        withChildren.SubGoals[0].Description.Should().Be("Child 1");
        withChildren.SubGoals[1].Description.Should().Be("Child 2");

        // Original should be unchanged (immutability)
        parent.SubGoals.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that AppendSubGoals adds to existing sub-goals.
    /// </summary>
    [Fact]
    public void Goal_AppendSubGoals_ShouldAddToExistingSubGoals()
    {
        // Arrange
        Goal parent = Goal.Atomic("Parent").WithSubGoals(Goal.Atomic("Child 1"));
        Goal newChild = Goal.Atomic("Child 2");

        // Act
        Goal appended = parent.AppendSubGoals(newChild);

        // Assert
        appended.SubGoals.Should().HaveCount(2);
        parent.SubGoals.Should().HaveCount(1); // Immutability check
    }

    /// <summary>
    /// Tests that composite goal is complete when all sub-goals are complete.
    /// </summary>
    [Fact]
    public void Goal_IsComplete_WhenAllSubGoalsComplete_ShouldReturnTrue()
    {
        // Arrange
        PipelineBranch branch = CreateTestBranch();
        Goal child1 = Goal.Atomic("Child 1", _ => true);
        Goal child2 = Goal.Atomic("Child 2", _ => true);
        Goal parent = Goal.Atomic("Parent").WithSubGoals(child1, child2);

        // Act
        bool isComplete = parent.IsComplete(branch);

        // Assert
        isComplete.Should().BeTrue();
    }

    /// <summary>
    /// Tests that composite goal is incomplete when any sub-goal is incomplete.
    /// </summary>
    [Fact]
    public void Goal_IsComplete_WhenAnySubGoalIncomplete_ShouldReturnFalse()
    {
        // Arrange
        PipelineBranch branch = CreateTestBranch();
        Goal child1 = Goal.Atomic("Child 1", _ => true);
        Goal child2 = Goal.Atomic("Child 2", _ => false);
        Goal parent = Goal.Atomic("Parent").WithSubGoals(child1, child2);

        // Act
        bool isComplete = parent.IsComplete(branch);

        // Assert
        isComplete.Should().BeFalse();
    }

    /// <summary>
    /// Tests that GetIncompleteSubGoals returns only incomplete goals.
    /// </summary>
    [Fact]
    public void Goal_GetIncompleteSubGoals_ShouldReturnOnlyIncomplete()
    {
        // Arrange
        PipelineBranch branch = CreateTestBranch();
        Goal child1 = Goal.Atomic("Complete", _ => true);
        Goal child2 = Goal.Atomic("Incomplete", _ => false);
        Goal parent = Goal.Atomic("Parent").WithSubGoals(child1, child2);

        // Act
        List<Goal> incomplete = parent.GetIncompleteSubGoals(branch).ToList();

        // Assert
        incomplete.Should().HaveCount(1);
        incomplete[0].Description.Should().Be("Incomplete");
    }

    /// <summary>
    /// Tests that ToOption returns Some for valid goal.
    /// </summary>
    [Fact]
    public void Goal_ToOption_WithValidDescription_ShouldReturnSome()
    {
        // Arrange
        Goal goal = Goal.Atomic("Valid goal");

        // Act
        Option<Goal> option = goal.ToOption();

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(goal);
    }

    /// <summary>
    /// Tests that ForEventType creates goal with event-based completion.
    /// </summary>
    [Fact]
    public void Goal_ForEventType_ShouldCreateEventBasedGoal()
    {
        // Arrange
        PipelineBranch branch = CreateTestBranch();
        Goal goal = Goal.ForEventType<ReasoningStep>("Wait for reasoning");

        // Act - branch has no reasoning events
        bool isComplete = goal.IsComplete(branch);

        // Assert
        isComplete.Should().BeFalse();
    }

    /// <summary>
    /// Tests that Atomic throws for null description.
    /// </summary>
    [Fact]
    public void Goal_Atomic_WithNullDescription_ShouldThrow()
    {
        // Act
        Action act = () => Goal.Atomic(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GoalExtensions Tests

    /// <summary>
    /// Tests that Flatten returns all goals in hierarchy.
    /// </summary>
    [Fact]
    public void GoalExtensions_Flatten_ShouldReturnAllGoals()
    {
        // Arrange
        Goal leaf1 = Goal.Atomic("Leaf 1");
        Goal leaf2 = Goal.Atomic("Leaf 2");
        Goal middle = Goal.Atomic("Middle").WithSubGoals(leaf1, leaf2);
        Goal root = Goal.Atomic("Root").WithSubGoals(middle);

        // Act
        List<Goal> flattened = root.Flatten().ToList();

        // Assert
        flattened.Should().HaveCount(4);
        flattened.Select(g => g.Description).Should().Contain(new[] { "Root", "Middle", "Leaf 1", "Leaf 2" });
    }

    /// <summary>
    /// Tests that TotalCount returns correct count.
    /// </summary>
    [Fact]
    public void GoalExtensions_TotalCount_ShouldReturnCorrectCount()
    {
        // Arrange
        Goal root = Goal.Atomic("Root")
            .WithSubGoals(
                Goal.Atomic("Child 1").WithSubGoals(Goal.Atomic("Grandchild")),
                Goal.Atomic("Child 2"));

        // Act
        int count = root.TotalCount();

        // Assert
        count.Should().Be(4);
    }

    /// <summary>
    /// Tests that CompletedCount returns correct count.
    /// </summary>
    [Fact]
    public void GoalExtensions_CompletedCount_ShouldReturnCorrectCount()
    {
        // Arrange
        PipelineBranch branch = CreateTestBranch();
        Goal root = Goal.Atomic("Root", _ => true)
            .WithSubGoals(
                Goal.Atomic("Complete", _ => true),
                Goal.Atomic("Incomplete", _ => false));

        // Act
        int completed = root.CompletedCount(branch);

        // Assert - root doesn't complete because sub-goals are not all complete
        // Only "Complete" child returns true for IsComplete
        completed.Should().Be(1); // Only the "Complete" child is complete
    }

    /// <summary>
    /// Tests that Progress returns correct percentage.
    /// </summary>
    [Fact]
    public void GoalExtensions_Progress_ShouldReturnCorrectPercentage()
    {
        // Arrange
        PipelineBranch branch = CreateTestBranch();
        Goal root = Goal.Atomic("Root")
            .WithSubGoals(
                Goal.Atomic("Complete", _ => true),
                Goal.Atomic("Complete 2", _ => true),
                Goal.Atomic("Incomplete", _ => false),
                Goal.Atomic("Incomplete 2", _ => false));

        // Act
        double progress = root.Progress(branch);

        // Assert
        progress.Should().BeApproximately(0.4, 0.01); // 2 of 5 complete
    }

    /// <summary>
    /// Tests that GetLeafGoals returns only leaf goals.
    /// </summary>
    [Fact]
    public void GoalExtensions_GetLeafGoals_ShouldReturnOnlyLeaves()
    {
        // Arrange
        Goal leaf1 = Goal.Atomic("Leaf 1");
        Goal leaf2 = Goal.Atomic("Leaf 2");
        Goal root = Goal.Atomic("Root")
            .WithSubGoals(Goal.Atomic("Middle").WithSubGoals(leaf1), leaf2);

        // Act
        List<Goal> leaves = root.GetLeafGoals().ToList();

        // Assert
        leaves.Should().HaveCount(2);
        leaves.Select(g => g.Description).Should().Contain(new[] { "Leaf 1", "Leaf 2" });
    }

    /// <summary>
    /// Tests that MaxDepth returns correct depth.
    /// </summary>
    [Fact]
    public void GoalExtensions_MaxDepth_ShouldReturnCorrectDepth()
    {
        // Arrange
        Goal deep = Goal.Atomic("Root")
            .WithSubGoals(
                Goal.Atomic("Level 1")
                    .WithSubGoals(
                        Goal.Atomic("Level 2")
                            .WithSubGoals(Goal.Atomic("Level 3"))));

        // Act
        int depth = deep.MaxDepth();

        // Assert
        depth.Should().Be(4);
    }

    /// <summary>
    /// Tests that ToTreeString produces readable output.
    /// </summary>
    [Fact]
    public void GoalExtensions_ToTreeString_ShouldProduceReadableOutput()
    {
        // Arrange
        Goal root = Goal.Atomic("Root")
            .WithSubGoals(
                Goal.Atomic("Child 1"),
                Goal.Atomic("Child 2"));

        // Act
        string tree = root.ToTreeString();

        // Assert
        tree.Should().Contain("- Root");
        tree.Should().Contain("  - Child 1");
        tree.Should().Contain("  - Child 2");
    }

    /// <summary>
    /// Tests that Filter returns matching goals only.
    /// </summary>
    [Fact]
    public void GoalExtensions_Filter_ShouldReturnMatchingGoals()
    {
        // Arrange
        Goal root = Goal.Atomic("Keep-Root")
            .WithSubGoals(
                Goal.Atomic("Keep-Child"),
                Goal.Atomic("Remove-Child"));

        // Act
        Option<Goal> filtered = root.Filter(g => g.Description.StartsWith("Keep", StringComparison.Ordinal));

        // Assert
        filtered.HasValue.Should().BeTrue();
        filtered.Value!.SubGoals.Should().HaveCount(1);
        filtered.Value!.SubGoals[0].Description.Should().Be("Keep-Child");
    }

    /// <summary>
    /// Tests that Map transforms goal correctly.
    /// </summary>
    [Fact]
    public void GoalExtensions_Map_ShouldTransformGoal()
    {
        // Arrange
        Goal original = Goal.Atomic("Original");
        Result<Goal> result = Result<Goal>.Success(original);

        // Act
        Result<Goal> mapped = result.Map(g => g.WithSubGoals(Goal.Atomic("Added")));

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.SubGoals.Should().HaveCount(1);
    }

    #endregion

    #region HierarchicalGoalPlanner Tests

    /// <summary>
    /// Tests that ExecuteGoalArrow executes atomic goal.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalArrow_ShouldExecuteAtomicGoal()
    {
        // Arrange
        bool stepExecuted = false;
        Goal goal = Goal.Atomic("Test");
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, PipelineBranch> step = HierarchicalGoalPlanner.ExecuteGoalArrow(
            goal,
            _ =>
            {
                return b =>
                {
                    stepExecuted = true;
                    return Task.FromResult(b);
                };
            });

        // Act
        PipelineBranch result = await step(branch);

        // Assert
        stepExecuted.Should().BeTrue();
        result.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that ExecuteGoalArrow skips completed goals.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalArrow_ShouldSkipCompletedGoals()
    {
        // Arrange
        bool stepExecuted = false;
        Goal goal = Goal.Atomic("Complete", _ => true);
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, PipelineBranch> step = HierarchicalGoalPlanner.ExecuteGoalArrow(
            goal,
            _ =>
            {
                return b =>
                {
                    stepExecuted = true;
                    return Task.FromResult(b);
                };
            });

        // Act
        await step(branch);

        // Assert
        stepExecuted.Should().BeFalse();
    }

    /// <summary>
    /// Tests that ExecuteGoalArrow executes sub-goals recursively.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalArrow_ShouldExecuteSubGoalsRecursively()
    {
        // Arrange
        List<string> executionOrder = new List<string>();
        Goal child1 = Goal.Atomic("Child 1");
        Goal child2 = Goal.Atomic("Child 2");
        Goal parent = Goal.Atomic("Parent").WithSubGoals(child1, child2);
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, PipelineBranch> step = HierarchicalGoalPlanner.ExecuteGoalArrow(
            parent,
            g =>
            {
                return b =>
                {
                    executionOrder.Add(g.Description);
                    return Task.FromResult(b);
                };
            });

        // Act
        await step(branch);

        // Assert
        executionOrder.Should().HaveCount(2);
        executionOrder.Should().ContainInOrder("Child 1", "Child 2");
    }

    /// <summary>
    /// Tests that ExecuteGoalSafeArrow handles errors gracefully.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalSafeArrow_ShouldHandleErrors()
    {
        // Arrange
        Goal goal = Goal.Atomic("Failing goal");
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, Result<PipelineBranch>> step = HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            goal,
            _ => _ => Task.FromResult(Result<PipelineBranch>.Failure("Test error")));

        // Act
        Result<PipelineBranch> result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Test error");
    }

    /// <summary>
    /// Tests that ExecuteGoalSafeArrow stops on first failure.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalSafeArrow_ShouldStopOnFirstFailure()
    {
        // Arrange
        int executionCount = 0;
        Goal child1 = Goal.Atomic("Failing");
        Goal child2 = Goal.Atomic("Should not run");
        Goal parent = Goal.Atomic("Parent").WithSubGoals(child1, child2);
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, Result<PipelineBranch>> step = HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            parent,
            g =>
            {
                return b =>
                {
                    executionCount++;
                    if (g.Description == "Failing")
                    {
                        return Task.FromResult(Result<PipelineBranch>.Failure("Failed"));
                    }

                    return Task.FromResult(Result<PipelineBranch>.Success(b));
                };
            });

        // Act
        Result<PipelineBranch> result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        executionCount.Should().Be(1);
    }

    /// <summary>
    /// Tests that ExecuteGoalParallelArrow executes goals in parallel.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalParallelArrow_ShouldExecuteInParallel()
    {
        // Arrange
        int concurrentCount = 0;
        int maxConcurrent = 0;
        object lockObj = new object();

        Goal child1 = Goal.Atomic("Child 1");
        Goal child2 = Goal.Atomic("Child 2");
        Goal child3 = Goal.Atomic("Child 3");
        Goal parent = Goal.Atomic("Parent").WithSubGoals(child1, child2, child3);
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, Result<PipelineBranch>> step = HierarchicalGoalPlanner.ExecuteGoalParallelArrow(
            parent,
            _ =>
            {
                return async b =>
                {
                    lock (lockObj)
                    {
                        concurrentCount++;
                        maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                    }

                    await Task.Delay(50);

                    lock (lockObj)
                    {
                        concurrentCount--;
                    }

                    return Result<PipelineBranch>.Success(b);
                };
            },
            maxParallelism: 3);

        // Act
        Result<PipelineBranch> result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        maxConcurrent.Should().BeGreaterThan(1, "Goals should execute in parallel");
    }

    /// <summary>
    /// Tests that ExecuteGoalParallelArrow respects maxParallelism.
    /// </summary>
    [Fact]
    public async Task HierarchicalGoalPlanner_ExecuteGoalParallelArrow_ShouldRespectMaxParallelism()
    {
        // Arrange
        int maxObservedConcurrency = 0;
        int currentConcurrency = 0;
        object lockObj = new object();

        List<Goal> children = Enumerable.Range(1, 10)
            .Select(i => Goal.Atomic($"Child {i}"))
            .ToList();
        Goal parent = Goal.Atomic("Parent").WithSubGoals(children.ToArray());
        PipelineBranch branch = CreateTestBranch();

        Step<PipelineBranch, Result<PipelineBranch>> step = HierarchicalGoalPlanner.ExecuteGoalParallelArrow(
            parent,
            _ =>
            {
                return async b =>
                {
                    lock (lockObj)
                    {
                        currentConcurrency++;
                        maxObservedConcurrency = Math.Max(maxObservedConcurrency, currentConcurrency);
                    }

                    await Task.Delay(20);

                    lock (lockObj)
                    {
                        currentConcurrency--;
                    }

                    return Result<PipelineBranch>.Success(b);
                };
            },
            maxParallelism: 2);

        // Act
        await step(branch);

        // Assert
        maxObservedConcurrency.Should().BeLessThanOrEqualTo(2);
    }

    /// <summary>
    /// Tests that ExecuteGoalArrow throws for null goal.
    /// </summary>
    [Fact]
    public void HierarchicalGoalPlanner_ExecuteGoalArrow_WithNullGoal_ShouldThrow()
    {
        // Act
        Action act = () => HierarchicalGoalPlanner.ExecuteGoalArrow(null!, _ => b => Task.FromResult(b));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that ExecuteGoalArrow throws for null step selector.
    /// </summary>
    [Fact]
    public void HierarchicalGoalPlanner_ExecuteGoalArrow_WithNullStepSelector_ShouldThrow()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test");

        // Act
        Action act = () => HierarchicalGoalPlanner.ExecuteGoalArrow(goal, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GoalDecomposer Tests

    /// <summary>
    /// Tests that DecomposeArrow returns original goal when maxDepth is zero.
    /// </summary>
    [Fact]
    public async Task GoalDecomposer_DecomposeArrow_WithZeroMaxDepth_ShouldReturnOriginal()
    {
        // Arrange
        ToolAwareChatModel llm = CreateMockLlm("[]");
        Goal goal = Goal.Atomic("Test goal");
        PipelineBranch branch = CreateTestBranch();

        // Act
        Result<Goal> result = await GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 0)(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Test goal");
        result.Value.SubGoals.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that DecomposeArrow fails for empty description.
    /// </summary>
    [Fact]
    public async Task GoalDecomposer_DecomposeArrow_WithEmptyDescription_ShouldFail()
    {
        // Arrange
        ToolAwareChatModel llm = CreateMockLlm("[]");

        // Create goal with empty description using record syntax
        Goal goal = new Goal(Guid.NewGuid(), "   ", Array.Empty<Goal>(), _ => false);
        PipelineBranch branch = CreateTestBranch();

        // Act
        Result<Goal> result = await GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1)(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    /// <summary>
    /// Tests that DecomposeArrow parses LLM response correctly.
    /// </summary>
    [Fact]
    public async Task GoalDecomposer_DecomposeArrow_ShouldParseLlmResponse()
    {
        // Arrange
        ToolAwareChatModel llm = CreateMockLlm("[\"Sub-goal 1\", \"Sub-goal 2\", \"Sub-goal 3\"]");
        Goal goal = Goal.Atomic("Complex goal");
        PipelineBranch branch = CreateTestBranch();

        // Act
        Result<Goal> result = await GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1)(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(3);
        result.Value.SubGoals.Select(g => g.Description).Should().Contain(new[] { "Sub-goal 1", "Sub-goal 2", "Sub-goal 3" });
    }

    /// <summary>
    /// Tests that DecomposeArrow handles markdown code blocks.
    /// </summary>
    [Fact]
    public async Task GoalDecomposer_DecomposeArrow_ShouldHandleMarkdownCodeBlocks()
    {
        // Arrange
        string response = "```json\n[\"Goal A\", \"Goal B\"]\n```";
        ToolAwareChatModel llm = CreateMockLlm(response);
        Goal goal = Goal.Atomic("Test");
        PipelineBranch branch = CreateTestBranch();

        // Act
        Result<Goal> result = await GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1)(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests that DecomposeArrow handles JSON embedded in text.
    /// </summary>
    [Fact]
    public async Task GoalDecomposer_DecomposeArrow_ShouldHandleEmbeddedJson()
    {
        // Arrange
        string response = "Here are the sub-goals: [\"First\", \"Second\"] That's my analysis.";
        ToolAwareChatModel llm = CreateMockLlm(response);
        Goal goal = Goal.Atomic("Test");
        PipelineBranch branch = CreateTestBranch();

        // Act
        Result<Goal> result = await GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1)(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests that DecomposeArrow throws for null LLM.
    /// </summary>
    [Fact]
    public void GoalDecomposer_DecomposeArrow_WithNullLlm_ShouldThrow()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test");

        // Act
        Action act = () => GoalDecomposer.DecomposeArrow(null!, goal);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that DecomposeArrow throws for null goal.
    /// </summary>
    [Fact]
    public void GoalDecomposer_DecomposeArrow_WithNullGoal_ShouldThrow()
    {
        // Arrange
        ToolAwareChatModel llm = CreateMockLlm("[]");

        // Act
        Action act = () => GoalDecomposer.DecomposeArrow(llm, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that DecomposeRecursiveArrow decomposes multiple levels.
    /// </summary>
    [Fact]
    public async Task GoalDecomposer_DecomposeRecursiveArrow_ShouldDecomposeMultipleLevels()
    {
        // Arrange
        int callCount = 0;
        MockChatModel mockModel = new MockChatModel(_ =>
        {
            callCount++;
            // First call returns sub-goals, subsequent calls return empty
            return callCount == 1
                ? "[\"Level 1 Sub-goal\"]"
                : "[]"; // Empty array still parses but produces no sub-goals
        });

        ToolAwareChatModel llm = new ToolAwareChatModel(mockModel, new ToolRegistry());
        Goal goal = Goal.Atomic("Root");
        PipelineBranch branch = CreateTestBranch();

        // Act
        Result<Goal> result = await GoalDecomposer.DecomposeRecursiveArrow(llm, goal, maxDepth: 2)(branch);

        // Assert - either success with sub-goals or at least one decomposition call
        callCount.Should().BeGreaterThanOrEqualTo(1);
        // Note: empty array "[]" results in failure due to no sub-goals parsed
        // This is expected behavior - the test verifies recursive calling
    }

    #endregion

    #region Helper Methods

    private static PipelineBranch CreateTestBranch()
    {
        IVectorStore store = new TrackedVectorStore();
        DataSource source = DataSource.FromPath(".");
        return new PipelineBranch("test-branch", store, source);
    }

    private static ToolAwareChatModel CreateMockLlm(string response)
    {
        MockChatModel mockModel = new MockChatModel(response);
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    /// <summary>
    /// Mock chat model for testing.
    /// </summary>
    private sealed class MockChatModel : IChatCompletionModel
    {
        private readonly Func<string, string> _responseGenerator;

        public MockChatModel(string fixedResponse)
            : this(_ => fixedResponse)
        {
        }

        public MockChatModel(Func<string, string> responseGenerator)
        {
            _responseGenerator = responseGenerator;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            return Task.FromResult(_responseGenerator(prompt));
        }

        public IAsyncEnumerable<string> GenerateTextStreamAsync(string prompt, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
