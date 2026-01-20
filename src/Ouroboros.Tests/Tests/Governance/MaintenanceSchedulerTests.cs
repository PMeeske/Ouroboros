// <copyright file="MaintenanceSchedulerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Governance;

namespace Ouroboros.Tests.Governance;

/// <summary>
/// Tests for the Maintenance Scheduler.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
[Trait("Category", "Unit")]
public class MaintenanceSchedulerTests
{
    [Fact]
    public void ScheduleTask_WithValidTask_ShouldSucceed()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();
        var task = new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = "Test Task",
            Description = "A test maintenance task",
            TaskType = MaintenanceTaskType.Custom,
            Schedule = TimeSpan.FromHours(1),
            Execute = _ => Task.FromResult(Result<object>.Success("Done"))
        };

        // Act
        var result = scheduler.ScheduleTask(task);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithSuccessfulTask_ShouldComplete()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();
        var executed = false;
        var task = new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = "Test Task",
            Description = "A test task",
            TaskType = MaintenanceTaskType.Custom,
            Schedule = TimeSpan.FromHours(1),
            Execute = _ =>
            {
                executed = true;
                return Task.FromResult(Result<object>.Success("Success"));
            }
        };

        // Act
        var result = await scheduler.ExecuteTaskAsync(task);

        // Assert
        result.IsSuccess.Should().BeTrue();
        executed.Should().BeTrue();
        result.Value.Status.Should().Be(MaintenanceStatus.Completed);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithFailingTask_ShouldRecordFailure()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();
        var task = new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = "Failing Task",
            Description = "A task that fails",
            TaskType = MaintenanceTaskType.Custom,
            Schedule = TimeSpan.FromHours(1),
            Execute = _ => Task.FromResult(Result<object>.Failure("Task failed"))
        };

        // Act
        var result = await scheduler.ExecuteTaskAsync(task);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(MaintenanceStatus.Failed);
        result.Value.ResultMessage.Should().Be("Task failed");
    }

    [Fact]
    public void CreateAlert_WithValidAlert_ShouldSucceed()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();
        var alert = new AnomalyAlert
        {
            MetricName = "cpu_usage",
            Description = "CPU usage exceeded threshold",
            Severity = AlertSeverity.Warning,
            ExpectedValue = "< 80%",
            ObservedValue = "95%"
        };

        // Act
        var result = scheduler.CreateAlert(alert);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(alert);
    }

    [Fact]
    public void GetAlerts_WhenEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();

        // Act
        var alerts = scheduler.GetAlerts();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public void GetAlerts_WithUnresolvedFilter_ShouldReturnOnlyUnresolved()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();
        var unresolvedAlert = new AnomalyAlert
        {
            MetricName = "metric1",
            Description = "Unresolved issue",
            Severity = AlertSeverity.Error,
            IsResolved = false
        };
        var resolvedAlert = new AnomalyAlert
        {
            MetricName = "metric2",
            Description = "Resolved issue",
            Severity = AlertSeverity.Warning,
            IsResolved = true
        };

        scheduler.CreateAlert(unresolvedAlert);
        scheduler.CreateAlert(resolvedAlert);

        // Act
        var alerts = scheduler.GetAlerts(unresolvedOnly: true);

        // Assert
        alerts.Should().ContainSingle();
        alerts.First().IsResolved.Should().BeFalse();
    }

    [Fact]
    public void GetHistory_WhenEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();

        // Act
        var history = scheduler.GetHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistory_AfterExecution_ShouldContainExecution()
    {
        // Arrange
        var scheduler = new MaintenanceScheduler();
        var task = new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = "Test Task",
            Description = "Test",
            TaskType = MaintenanceTaskType.Custom,
            Schedule = TimeSpan.FromHours(1),
            Execute = _ => Task.FromResult(Result<object>.Success("Done"))
        };

        // Act
        await scheduler.ExecuteTaskAsync(task);
        var history = scheduler.GetHistory();

        // Assert
        history.Should().ContainSingle();
        history.First().Task.Name.Should().Be("Test Task");
    }

    [Fact]
    public void CreateCompactionTask_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var task = MaintenanceScheduler.CreateCompactionTask(
            "Test Compaction",
            TimeSpan.FromHours(24),
            _ => Task.FromResult(Result<CompactionResult>.Success(new CompactionResult
            {
                SnapshotsCompacted = 10,
                BytesSaved = 1024
            })));

        // Assert
        task.Name.Should().Be("Test Compaction");
        task.TaskType.Should().Be(MaintenanceTaskType.Compaction);
        task.Schedule.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void CreateArchivingTask_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var task = MaintenanceScheduler.CreateArchivingTask(
            "Test Archiving",
            TimeSpan.FromHours(24),
            TimeSpan.FromDays(30),
            (age, ct) => Task.FromResult(Result<ArchiveResult>.Success(new ArchiveResult
            {
                SnapshotsArchived = 5,
                ArchiveLocation = "/tmp/archive"
            })));

        // Assert
        task.Name.Should().Be("Test Archiving");
        task.TaskType.Should().Be(MaintenanceTaskType.Archiving);
    }

    [Fact]
    public void CreateAnomalyDetectionTask_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var task = MaintenanceScheduler.CreateAnomalyDetectionTask(
            "Test Anomaly Detection",
            TimeSpan.FromHours(1),
            _ => Task.FromResult(Result<AnomalyDetectionResult>.Success(new AnomalyDetectionResult
            {
                Anomalies = Array.Empty<AnomalyAlert>()
            })));

        // Assert
        task.Name.Should().Be("Test Anomaly Detection");
        task.TaskType.Should().Be(MaintenanceTaskType.AnomalyDetection);
    }
}
