// <copyright file="PriorityModulatorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Affect;

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;
using Xunit;

/// <summary>
/// Tests for PriorityModulator - Phase 3 Affective Dynamics.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PriorityModulatorTests
{
    [Fact]
    public void AddTask_CreatesTask()
    {
        // Arrange
        var modulator = new PriorityModulator();

        // Act
        var task = modulator.AddTask("Test Task", "A test task", 0.5);

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be("Test Task");
        task.BasePriority.Should().Be(0.5);
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public void AddTask_ClampsPriority()
    {
        // Arrange
        var modulator = new PriorityModulator();

        // Act
        var task1 = modulator.AddTask("Task1", "desc", 1.5);
        var task2 = modulator.AddTask("Task2", "desc", -0.5);

        // Assert
        task1.BasePriority.Should().Be(1.0);
        task2.BasePriority.Should().Be(0.0);
    }

    [Fact]
    public void GetNextTask_ReturnsHighestPriority()
    {
        // Arrange
        var modulator = new PriorityModulator();
        modulator.AddTask("Low", "desc", 0.2);
        modulator.AddTask("High", "desc", 0.9);
        modulator.AddTask("Medium", "desc", 0.5);

        // Act
        var next = modulator.GetNextTask();

        // Assert
        next.Should().NotBeNull();
        next!.Name.Should().Be("High");
    }

    [Fact]
    public void GetTasks_ReturnsAllPendingTasks()
    {
        // Arrange
        var modulator = new PriorityModulator();
        modulator.AddTask("Task1", "desc", 0.5);
        modulator.AddTask("Task2", "desc", 0.6);
        var completed = modulator.AddTask("Task3", "desc", 0.7);
        modulator.UpdateTaskStatus(completed.Id, TaskStatus.Completed);

        // Act
        var pending = modulator.GetTasks(includeDone: false);
        var all = modulator.GetTasks(includeDone: true);

        // Assert
        pending.Should().HaveCount(2);
        all.Should().HaveCount(3);
    }

    [Fact]
    public void UpdateTaskStatus_ChangesStatus()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var task = modulator.AddTask("Test", "desc", 0.5);

        // Act
        modulator.UpdateTaskStatus(task.Id, TaskStatus.InProgress);
        var updated = modulator.GetTasks(includeDone: true).First(t => t.Id == task.Id);

        // Assert
        updated.Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public void RemoveTask_RemovesFromQueue()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var task = modulator.AddTask("Test", "desc", 0.5);

        // Act
        modulator.RemoveTask(task.Id);
        var tasks = modulator.GetTasks(includeDone: true);

        // Assert
        tasks.Should().NotContain(t => t.Id == task.Id);
    }

    [Fact]
    public async Task AppraiseTaskAsync_ReturnsAppraisal()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var task = modulator.AddTask("Research Task", "explore new methods", 0.5);
        var state = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.3, 0.7, 0.8, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        var appraisal = await modulator.AppraiseTaskAsync(task.Id, state);

        // Assert
        appraisal.Should().NotBeNull();
        appraisal.OpportunityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AppraiseTaskAsync_UrgentTask_HasHigherThreat()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var urgentTask = modulator.AddTask(
            "Urgent Task",
            "must be done now",
            0.9,
            dueAt: DateTime.UtcNow.AddHours(-1)); // Overdue

        var normalTask = modulator.AddTask(
            "Normal Task",
            "can wait",
            0.5,
            dueAt: DateTime.UtcNow.AddDays(7));

        var state = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.5, 0.5, 0.3, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        var urgentAppraisal = await modulator.AppraiseTaskAsync(urgentTask.Id, state);
        var normalAppraisal = await modulator.AppraiseTaskAsync(normalTask.Id, state);

        // Assert
        urgentAppraisal.ThreatLevel.Should().BeGreaterThan(normalAppraisal.ThreatLevel);
        urgentAppraisal.UrgencyFactor.Should().BeGreaterThan(normalAppraisal.UrgencyFactor);
    }

    [Fact]
    public void ModulatePriorities_AdjustsBasedOnState()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var task = modulator.AddTask("High Priority", "important", 0.9);
        var initialPriority = task.ModulatedPriority;

        var stressedState = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.9, 0.5, 0.3, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        modulator.ModulatePriorities(stressedState);
        var modulatedTask = modulator.GetTasks().First(t => t.Id == task.Id);

        // Assert
        modulatedTask.ModulatedPriority.Should().NotBe(initialPriority);
    }

    [Fact]
    public void ModulatePriorities_HighCuriosity_BoostsExploratoryTasks()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var exploratoryTask = modulator.AddTask("Explore new ideas", "research and explore", 0.5);
        var regularTask = modulator.AddTask("Regular work", "standard processing", 0.5);

        var curiousState = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.2, 0.7, 0.9, 0.6, DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        modulator.ModulatePriorities(curiousState);
        var exploratory = modulator.GetTasks().First(t => t.Id == exploratoryTask.Id);
        var regular = modulator.GetTasks().First(t => t.Id == regularTask.Id);

        // Assert
        exploratory.ModulatedPriority.Should().BeGreaterThan(regular.ModulatedPriority);
    }

    [Fact]
    public void PrioritizeByThreat_ReordersTasks()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var lowThreat = modulator.AddTask("Low Threat", "safe task", 0.8);
        var highThreat = modulator.AddTask("High Threat", "risky task", 0.3,
            dueAt: DateTime.UtcNow.AddHours(-1)); // Overdue

        var state = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.7, 0.4, 0.3, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        // Appraise both tasks
        _ = modulator.AppraiseTaskAsync(lowThreat.Id, state).GetAwaiter().GetResult();
        _ = modulator.AppraiseTaskAsync(highThreat.Id, state).GetAwaiter().GetResult();

        // Act
        modulator.PrioritizeByThreat();
        var tasks = modulator.GetTasks();

        // Assert
        var high = tasks.First(t => t.Id == highThreat.Id);
        var low = tasks.First(t => t.Id == lowThreat.Id);
        high.ModulatedPriority.Should().BeGreaterThan(low.ModulatedPriority);
    }

    [Fact]
    public void PrioritizeByOpportunity_ReordersTasks()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var lowOpportunity = modulator.AddTask("Boring", "routine work", 0.5);
        var highOpportunity = modulator.AddTask("Learn new skill", "explore and research", 0.5);

        var state = new AffectiveState(
            Guid.NewGuid(), 0.3, 0.2, 0.8, 0.9, 0.6, DateTime.UtcNow, new Dictionary<string, object>());

        // Appraise both tasks
        _ = modulator.AppraiseTaskAsync(lowOpportunity.Id, state).GetAwaiter().GetResult();
        _ = modulator.AppraiseTaskAsync(highOpportunity.Id, state).GetAwaiter().GetResult();

        // Act
        modulator.PrioritizeByOpportunity();
        var tasks = modulator.GetTasks();

        // Assert
        var high = tasks.First(t => t.Id == highOpportunity.Id);
        var low = tasks.First(t => t.Id == lowOpportunity.Id);
        high.ModulatedPriority.Should().BeGreaterThan(low.ModulatedPriority);
    }

    [Fact]
    public void GetStatistics_ReturnsValidStats()
    {
        // Arrange
        var modulator = new PriorityModulator();
        modulator.AddTask("Task1", "desc", 0.5);
        modulator.AddTask("Task2", "desc", 0.7);
        var completed = modulator.AddTask("Task3", "desc", 0.3);
        modulator.UpdateTaskStatus(completed.Id, TaskStatus.Completed);

        // Act
        var stats = modulator.GetStatistics();

        // Assert
        stats.TotalTasks.Should().Be(3);
        stats.PendingTasks.Should().Be(2);
        stats.CompletedTasks.Should().Be(1);
        stats.AverageBasePriority.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void GetNextTask_SkipsCompletedTasks()
    {
        // Arrange
        var modulator = new PriorityModulator();
        var highPriority = modulator.AddTask("High", "desc", 0.9);
        modulator.UpdateTaskStatus(highPriority.Id, TaskStatus.Completed);
        modulator.AddTask("Medium", "desc", 0.5);

        // Act
        var next = modulator.GetNextTask();

        // Assert
        next.Should().NotBeNull();
        next!.Name.Should().Be("Medium");
    }
}
