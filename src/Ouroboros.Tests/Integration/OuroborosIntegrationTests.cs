// <copyright file="OuroborosIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Integration;
using Xunit;

/// <summary>
/// Integration tests for Ouroboros full system integration.
/// </summary>
public sealed class OuroborosIntegrationTests
{
    [Fact]
    public void EventBus_Should_PublishAndSubscribe()
    {
        var eventBus = new EventBus();
        var received = new List<GoalExecutedEvent>();
        eventBus.Subscribe<GoalExecutedEvent>().Subscribe(e => received.Add(e));

        eventBus.Publish(new GoalExecutedEvent(
            Guid.NewGuid(), DateTime.UtcNow, "Test", "Goal", true, TimeSpan.Zero));

        received.Should().ContainSingle();
    }

    [Fact]
    public void OuroborosBuilder_Should_Build()
    {
        var services = new ServiceCollection();
        var builder = new OuroborosBuilder(services);
        builder.WithEpisodicMemory().Build();
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void ExecutionConfig_Should_HaveDefaults()
    {
        var config = ExecutionConfig.Default;
        config.UseEpisodicMemory.Should().BeTrue();
        config.MaxPlanningDepth.Should().Be(10);
    }
}
