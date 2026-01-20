// <copyright file="AgentPersonaTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Council;

/// <summary>
/// Tests for agent persona implementations.
/// </summary>
[Trait("Category", "Unit")]
public class AgentPersonaTests
{
    [Fact]
    public void OptimistAgent_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var agent = new OptimistAgent();

        // Assert
        agent.Name.Should().Be("Optimist");
        agent.Description.Should().NotBeNullOrEmpty();
        agent.ExpertiseWeight.Should().BeGreaterThan(0);
        agent.SystemPrompt.Should().Contain("Optimist");
    }

    [Fact]
    public void SecurityCynicAgent_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var agent = new SecurityCynicAgent();

        // Assert
        agent.Name.Should().Be("SecurityCynic");
        agent.Description.Should().Contain("risk");
        agent.ExpertiseWeight.Should().Be(1.0);
        agent.SystemPrompt.Should().Contain("Security");
    }

    [Fact]
    public void PragmatistAgent_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var agent = new PragmatistAgent();

        // Assert
        agent.Name.Should().Be("Pragmatist");
        agent.Description.Should().Contain("feasibility");
        agent.ExpertiseWeight.Should().BeGreaterThan(0);
        agent.SystemPrompt.Should().Contain("Pragmatist");
    }

    [Fact]
    public void TheoristAgent_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var agent = new TheoristAgent();

        // Assert
        agent.Name.Should().Be("Theorist");
        agent.Description.Should().Contain("mathematical");
        agent.ExpertiseWeight.Should().BeGreaterThan(0);
        agent.SystemPrompt.Should().Contain("Theorist");
    }

    [Fact]
    public void UserAdvocateAgent_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var agent = new UserAdvocateAgent();

        // Assert
        agent.Name.Should().Be("UserAdvocate");
        agent.Description.Should().Contain("user");
        agent.ExpertiseWeight.Should().BeGreaterThan(0);
        agent.SystemPrompt.Should().Contain("User Advocate");
    }

    [Fact]
    public void AllAgents_ShouldImplementIAgentPersona()
    {
        // Arrange
        var agents = new IAgentPersona[]
        {
            new OptimistAgent(),
            new SecurityCynicAgent(),
            new PragmatistAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        // Assert
        foreach (var agent in agents)
        {
            agent.Should().BeAssignableTo<IAgentPersona>();
            agent.Name.Should().NotBeNullOrEmpty();
            agent.Description.Should().NotBeNullOrEmpty();
            agent.SystemPrompt.Should().NotBeNullOrEmpty();
            agent.ExpertiseWeight.Should().BeInRange(0, 1);
        }
    }

    [Fact]
    public void AllAgents_ShouldHaveUniqueNames()
    {
        // Arrange
        var agents = new IAgentPersona[]
        {
            new OptimistAgent(),
            new SecurityCynicAgent(),
            new PragmatistAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        // Act
        var names = agents.Select(a => a.Name).ToList();

        // Assert
        names.Should().OnlyHaveUniqueItems();
    }
}
