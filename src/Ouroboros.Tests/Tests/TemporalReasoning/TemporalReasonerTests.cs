// <copyright file="TemporalReasonerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TemporalReasoning;

using FluentAssertions;
using Ouroboros.Agent.TemporalReasoning;
using Xunit;

/// <summary>
/// Comprehensive tests for TemporalReasoner implementation.
/// Tests Allen interval relations, temporal queries, causal inference, timeline construction, and constraint satisfaction.
/// </summary>
[Trait("Category", "Unit")]
public class TemporalReasonerTests
{
    private readonly TemporalReasoner reasoner;

    public TemporalReasonerTests()
    {
        this.reasoner = new TemporalReasoner();
    }

    #region Helper Methods

    private static TemporalEvent CreateEvent(
        string type,
        string description,
        DateTime start,
        DateTime? end = null,
        Dictionary<string, object>? properties = null,
        List<string>? participants = null)
    {
        return new TemporalEvent(
            Guid.NewGuid(),
            type,
            description,
            start,
            end,
            (properties ?? new Dictionary<string, object>()).AsReadOnly(),
            (participants ?? new List<string>()).AsReadOnly());
    }

    #endregion

    #region Allen Interval Relation Tests

    [Fact]
    public async Task GetRelationAsync_BeforeRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Meeting", "Team standup", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 9, 30, 0));
        var event2 = CreateEvent("Meeting", "Client call", new DateTime(2024, 1, 1, 10, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Before);
        result.Value.Event1.Should().Be(event1);
        result.Value.Event2.Should().Be(event2);
        result.Value.Confidence.Should().Be(1.0);
    }

    [Fact]
    public async Task GetRelationAsync_MeetsRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Task A", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 10, 0, 0));
        var event2 = CreateEvent("Task", "Task B", new DateTime(2024, 1, 1, 10, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Meets);
    }

    [Fact]
    public async Task GetRelationAsync_OverlapsRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Task A", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 10, 30, 0));
        var event2 = CreateEvent("Task", "Task B", new DateTime(2024, 1, 1, 10, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Overlaps);
    }

    [Fact]
    public async Task GetRelationAsync_DuringRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Short task", new DateTime(2024, 1, 1, 10, 0, 0), new DateTime(2024, 1, 1, 10, 30, 0));
        var event2 = CreateEvent("Task", "Long task", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.During);
    }

    [Fact]
    public async Task GetRelationAsync_ContainsRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Long task", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));
        var event2 = CreateEvent("Task", "Short task", new DateTime(2024, 1, 1, 10, 0, 0), new DateTime(2024, 1, 1, 10, 30, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Contains);
    }

    [Fact]
    public async Task GetRelationAsync_StartsRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Task A", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 10, 0, 0));
        var event2 = CreateEvent("Task", "Task B", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Starts);
    }

    [Fact]
    public async Task GetRelationAsync_FinishesRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Task A", new DateTime(2024, 1, 1, 10, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));
        var event2 = CreateEvent("Task", "Task B", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Finishes);
    }

    [Fact]
    public async Task GetRelationAsync_EqualsRelation_ReturnsCorrectRelation()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Task A", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));
        var event2 = CreateEvent("Task", "Task B", new DateTime(2024, 1, 1, 9, 0, 0), new DateTime(2024, 1, 1, 11, 0, 0));

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, event2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationType.Should().Be(TemporalRelationType.Equals);
    }

    [Fact]
    public async Task GetRelationAsync_NullEvent1_ReturnsFailure()
    {
        // Arrange
        var event2 = CreateEvent("Task", "Task B", DateTime.Now);

        // Act
        var result = await this.reasoner.GetRelationAsync(null!, event2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Event1 cannot be null");
    }

    [Fact]
    public async Task GetRelationAsync_NullEvent2_ReturnsFailure()
    {
        // Arrange
        var event1 = CreateEvent("Task", "Task A", DateTime.Now);

        // Act
        var result = await this.reasoner.GetRelationAsync(event1, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Event2 cannot be null");
    }

    #endregion

    #region Temporal Query Tests

    [Fact]
    public async Task QueryEventsAsync_WithTimeRange_ReturnsFilteredEvents()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 9, 0, 0);
        var events = new List<TemporalEvent>
        {
            CreateEvent("Meeting", "Morning standup", baseTime),
            CreateEvent("Meeting", "Lunch meeting", baseTime.AddHours(3)),
            CreateEvent("Meeting", "Afternoon sync", baseTime.AddHours(6)),
        };

        // Build timeline first to store events
        var timeline = this.reasoner.ConstructTimeline(events);
        timeline.IsSuccess.Should().BeTrue();

        // Act
        var query = new TemporalQuery(
            After: baseTime.AddHours(2),
            Before: baseTime.AddHours(7),
            Duration: null,
            EventType: null,
            RelationTo: null,
            RelatedEventId: null);

        var result = await this.reasoner.QueryEventsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(e => e.Description == "Lunch meeting");
        result.Value.Should().Contain(e => e.Description == "Afternoon sync");
    }

    [Fact]
    public async Task QueryEventsAsync_WithEventType_ReturnsFilteredEvents()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 9, 0, 0);
        var events = new List<TemporalEvent>
        {
            CreateEvent("Meeting", "Team sync", baseTime),
            CreateEvent("Task", "Code review", baseTime.AddHours(1)),
            CreateEvent("Meeting", "Client call", baseTime.AddHours(2)),
        };

        var timeline = this.reasoner.ConstructTimeline(events);
        timeline.IsSuccess.Should().BeTrue();

        // Act
        var query = new TemporalQuery(
            After: null,
            Before: null,
            Duration: null,
            EventType: "Meeting",
            RelationTo: null,
            RelatedEventId: null);

        var result = await this.reasoner.QueryEventsAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(e => e.EventType.Should().Be("Meeting"));
    }

    [Fact]
    public async Task QueryEventsAsync_NullQuery_ReturnsFailure()
    {
        // Act
        var result = await this.reasoner.QueryEventsAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Query cannot be null");
    }

    #endregion

    #region Causal Inference Tests

    [Fact]
    public async Task InferCausalityAsync_WithValidEvents_ReturnsCausalRelations()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 9, 0, 0);
        var events = new List<TemporalEvent>
        {
            CreateEvent("Action", "User login", baseTime),
            CreateEvent("Action", "File download started", baseTime.AddMinutes(5)),
            CreateEvent("Action", "File download completed", baseTime.AddMinutes(10)),
        };

        // Act
        var result = await this.reasoner.InferCausalityAsync(events);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().AllSatisfy(r =>
        {
            r.Cause.Should().NotBeNull();
            r.Effect.Should().NotBeNull();
            r.CausalStrength.Should().BeInRange(0.0, 1.0);
        });
    }

    [Fact]
    public async Task InferCausalityAsync_EmptyEvents_ReturnsFailure()
    {
        // Act
        var result = await this.reasoner.InferCausalityAsync(new List<TemporalEvent>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task InferCausalityAsync_NullEvents_ReturnsFailure()
    {
        // Act
        var result = await this.reasoner.InferCausalityAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    #endregion

    #region Future Event Prediction Tests

    [Fact]
    public async Task PredictFutureEventsAsync_WithHistory_ReturnsPredictions()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 9, 0, 0);
        var history = new List<TemporalEvent>
        {
            CreateEvent("Meeting", "Daily standup", baseTime),
            CreateEvent("Meeting", "Daily standup", baseTime.AddDays(1)),
            CreateEvent("Meeting", "Daily standup", baseTime.AddDays(2)),
        };

        // Act
        var result = await this.reasoner.PredictFutureEventsAsync(history, TimeSpan.FromDays(2));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().AllSatisfy(p =>
        {
            p.EventType.Should().NotBeNullOrEmpty();
            p.Confidence.Should().BeInRange(0.0, 1.0);
            p.PredictedTime.Should().BeAfter(baseTime.AddDays(2));
        });
    }

    [Fact]
    public async Task PredictFutureEventsAsync_EmptyHistory_ReturnsFailure()
    {
        // Act
        var result = await this.reasoner.PredictFutureEventsAsync(new List<TemporalEvent>(), TimeSpan.FromDays(1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("History cannot be null or empty");
    }

    [Fact]
    public async Task PredictFutureEventsAsync_NegativeHorizon_ReturnsFailure()
    {
        // Arrange
        var history = new List<TemporalEvent>
        {
            CreateEvent("Event", "Test", DateTime.Now),
        };

        // Act
        var result = await this.reasoner.PredictFutureEventsAsync(history, TimeSpan.FromHours(-1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Horizon must be positive");
    }

    #endregion

    #region Timeline Construction Tests

    [Fact]
    public void ConstructTimeline_WithValidEvents_ReturnsTimeline()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 9, 0, 0);
        var events = new List<TemporalEvent>
        {
            CreateEvent("Meeting", "Morning sync", baseTime.AddHours(2)),
            CreateEvent("Task", "Code review", baseTime),
            CreateEvent("Meeting", "Client call", baseTime.AddHours(4)),
        };

        // Act
        var result = this.reasoner.ConstructTimeline(events);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().HaveCount(3);
        result.Value.Events.Should().BeInAscendingOrder(e => e.StartTime);
        result.Value.Relations.Should().NotBeEmpty();
        result.Value.EarliestTime.Should().Be(baseTime);
        result.Value.EventsByType.Should().ContainKey("Meeting");
        result.Value.EventsByType.Should().ContainKey("Task");
    }

    [Fact]
    public void ConstructTimeline_EmptyEvents_ReturnsFailure()
    {
        // Act
        var result = this.reasoner.ConstructTimeline(new List<TemporalEvent>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public void ConstructTimeline_NullEvents_ReturnsFailure()
    {
        // Act
        var result = this.reasoner.ConstructTimeline(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    #endregion

    #region Constraint Satisfaction Tests

    [Fact]
    public async Task CheckConstraintSatisfiabilityAsync_ConsistentConstraints_ReturnsTrue()
    {
        // Arrange
        var event1Id = Guid.NewGuid();
        var event2Id = Guid.NewGuid();
        var constraints = new List<TemporalConstraint>
        {
            new TemporalConstraint(event1Id, event2Id, TemporalRelationType.Before),
        };

        // Act
        var result = await this.reasoner.CheckConstraintSatisfiabilityAsync(constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConstraintSatisfiabilityAsync_InconsistentConstraints_ReturnsFalse()
    {
        // Arrange
        var event1Id = Guid.NewGuid();
        var event2Id = Guid.NewGuid();
        var constraints = new List<TemporalConstraint>
        {
            new TemporalConstraint(event1Id, event2Id, TemporalRelationType.Before),
            new TemporalConstraint(event1Id, event2Id, TemporalRelationType.After),
        };

        // Act
        var result = await this.reasoner.CheckConstraintSatisfiabilityAsync(constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task CheckConstraintSatisfiabilityAsync_NullConstraints_ReturnsFailure()
    {
        // Act
        var result = await this.reasoner.CheckConstraintSatisfiabilityAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Constraints cannot be null");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullWorkflow_TimelineQueryPredictInfer_WorksEndToEnd()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 9, 0, 0);
        var events = new List<TemporalEvent>
        {
            CreateEvent("Meeting", "Daily standup", baseTime),
            CreateEvent("Task", "Code review", baseTime.AddHours(1)),
            CreateEvent("Meeting", "Client sync", baseTime.AddHours(3)),
            CreateEvent("Task", "Bug fix", baseTime.AddHours(4)),
            CreateEvent("Meeting", "Team retrospective", baseTime.AddHours(6)),
        };

        // Act & Assert - Construct Timeline
        var timelineResult = this.reasoner.ConstructTimeline(events);
        timelineResult.IsSuccess.Should().BeTrue();
        timelineResult.Value.Events.Should().HaveCount(5);

        // Act & Assert - Query Events
        var queryResult = await this.reasoner.QueryEventsAsync(
            new TemporalQuery(After: baseTime.AddHours(2), Before: null, Duration: null, EventType: "Meeting", RelationTo: null, RelatedEventId: null));
        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.Should().HaveCount(2);

        // Act & Assert - Infer Causality
        var causalityResult = await this.reasoner.InferCausalityAsync(events);
        causalityResult.IsSuccess.Should().BeTrue();
        causalityResult.Value.Should().NotBeEmpty();

        // Act & Assert - Predict Future Events
        var predictionResult = await this.reasoner.PredictFutureEventsAsync(events, TimeSpan.FromDays(1));
        predictionResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Check Relations
        var relationResult = await this.reasoner.GetRelationAsync(events[0], events[1]);
        relationResult.IsSuccess.Should().BeTrue();
        relationResult.Value.RelationType.Should().Be(TemporalRelationType.Before);
    }

    #endregion
}
