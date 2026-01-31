// <copyright file="EthicsAuditLogTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Ethics;
using Xunit;

namespace Ouroboros.Tests.Tests.Ethics;

/// <summary>
/// Tests for the ethics audit log functionality.
/// </summary>
public sealed class EthicsAuditLogTests
{
    [Fact]
    public async Task LogEvaluationAsync_ShouldStoreEntry()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();
        var entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = "test-agent",
            UserId = "test-user",
            EvaluationType = "Action",
            Description = "Test action",
            Clearance = EthicalClearance.Permitted("Test clearance")
        };

        // Act
        await auditLog.LogEvaluationAsync(entry);

        // Assert
        var history = await auditLog.GetAuditHistoryAsync("test-agent");
        history.Should().ContainSingle();
        history.First().Should().Be(entry);
    }

    [Fact]
    public async Task LogViolationAttemptAsync_ShouldStoreEntry()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();
        var violations = new[]
        {
            new EthicalViolation
            {
                ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                Description = "Attempted harmful action",
                Severity = ViolationSeverity.Critical,
                Evidence = "Test evidence",
                AffectedParties = new[] { "Users" }
            }
        };

        // Act
        await auditLog.LogViolationAttemptAsync(
            "test-agent",
            "test-user",
            "Harmful action attempt",
            violations);

        // Assert
        var history = await auditLog.GetAuditHistoryAsync("test-agent");
        history.Should().ContainSingle();
        history.First().EvaluationType.Should().Be("ViolationAttempt");
        history.First().Clearance.Violations.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_WithTimeRange_ShouldFilterCorrectly()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();
        var now = DateTime.UtcNow;

        var oldEntry = new EthicsAuditEntry
        {
            Timestamp = now.AddHours(-2),
            AgentId = "test-agent",
            EvaluationType = "Action",
            Description = "Old action",
            Clearance = EthicalClearance.Permitted("Old")
        };

        var newEntry = new EthicsAuditEntry
        {
            Timestamp = now,
            AgentId = "test-agent",
            EvaluationType = "Action",
            Description = "New action",
            Clearance = EthicalClearance.Permitted("New")
        };

        await auditLog.LogEvaluationAsync(oldEntry);
        await auditLog.LogEvaluationAsync(newEntry);

        // Act
        var history = await auditLog.GetAuditHistoryAsync(
            "test-agent",
            startTime: now.AddHours(-1));

        // Assert
        history.Should().ContainSingle();
        history.First().Description.Should().Be("New action");
    }

    [Fact]
    public async Task GetAuditHistoryAsync_WithDifferentAgents_ShouldSeparateCorrectly()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();

        var agent1Entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = "agent-1",
            EvaluationType = "Action",
            Description = "Agent 1 action",
            Clearance = EthicalClearance.Permitted("Test")
        };

        var agent2Entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = "agent-2",
            EvaluationType = "Action",
            Description = "Agent 2 action",
            Clearance = EthicalClearance.Permitted("Test")
        };

        await auditLog.LogEvaluationAsync(agent1Entry);
        await auditLog.LogEvaluationAsync(agent2Entry);

        // Act
        var agent1History = await auditLog.GetAuditHistoryAsync("agent-1");
        var agent2History = await auditLog.GetAuditHistoryAsync("agent-2");

        // Assert
        agent1History.Should().ContainSingle();
        agent1History.First().Description.Should().Be("Agent 1 action");

        agent2History.Should().ContainSingle();
        agent2History.First().Description.Should().Be("Agent 2 action");
    }

    [Fact]
    public async Task GetAllEntries_ShouldReturnAllEntries()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();

        for (int i = 0; i < 5; i++)
        {
            var entry = new EthicsAuditEntry
            {
                Timestamp = DateTime.UtcNow,
                AgentId = $"agent-{i}",
                EvaluationType = "Action",
                Description = $"Action {i}",
                Clearance = EthicalClearance.Permitted($"Test {i}")
            };
            await auditLog.LogEvaluationAsync(entry);
        }

        // Act
        var allEntries = auditLog.GetAllEntries();

        // Assert
        allEntries.Should().HaveCount(5);
    }

    [Fact]
    public async Task AuditEntry_ShouldHaveUniqueIds()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();

        var entry1 = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = "test-agent",
            EvaluationType = "Action",
            Description = "Action 1",
            Clearance = EthicalClearance.Permitted("Test")
        };

        var entry2 = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = "test-agent",
            EvaluationType = "Action",
            Description = "Action 2",
            Clearance = EthicalClearance.Permitted("Test")
        };

        await auditLog.LogEvaluationAsync(entry1);
        await auditLog.LogEvaluationAsync(entry2);

        // Act
        var entries = auditLog.GetAllEntries();

        // Assert
        entries.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task LogEvaluationAsync_WithNullEntry_ShouldThrow()
    {
        // Arrange
        var auditLog = new InMemoryEthicsAuditLog();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await auditLog.LogEvaluationAsync(null!));
    }
}
