// <copyright file="EthicalPrincipleTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Ethics;
using Xunit;

namespace Ouroboros.Tests.Tests.Ethics;

/// <summary>
/// Tests for EthicalPrinciple immutability and predefined principles.
/// </summary>
public sealed class EthicalPrincipleTests
{
    [Fact]
    public void GetCorePrinciples_ShouldReturn10Principles()
    {
        // Act
        var principles = EthicalPrinciple.GetCorePrinciples();

        // Assert
        principles.Should().HaveCount(10);
    }

    [Fact]
    public void GetCorePrinciples_ShouldReturnImmutableCollection()
    {
        // Act
        var principles = EthicalPrinciple.GetCorePrinciples();

        // Assert
        principles.Should().BeAssignableTo<IReadOnlyList<EthicalPrinciple>>();
    }

    [Fact]
    public void DoNoHarm_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.DoNoHarm;

        // Assert
        principle.Id.Should().Be("do_no_harm");
        principle.Name.Should().Be("Do No Harm");
        principle.Category.Should().Be(EthicalPrincipleCategory.Safety);
        principle.Priority.Should().Be(1.0);
        principle.IsMandatory.Should().BeTrue();
        principle.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RespectAutonomy_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.RespectAutonomy;

        // Assert
        principle.Id.Should().Be("respect_autonomy");
        principle.Name.Should().Be("Respect Autonomy");
        principle.Category.Should().Be(EthicalPrincipleCategory.Autonomy);
        principle.Priority.Should().Be(0.95);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void Honesty_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.Honesty;

        // Assert
        principle.Id.Should().Be("honesty");
        principle.Name.Should().Be("Honesty");
        principle.Category.Should().Be(EthicalPrincipleCategory.Transparency);
        principle.Priority.Should().Be(0.90);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void Privacy_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.Privacy;

        // Assert
        principle.Id.Should().Be("privacy");
        principle.Name.Should().Be("Privacy");
        principle.Category.Should().Be(EthicalPrincipleCategory.Privacy);
        principle.Priority.Should().Be(0.90);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void Fairness_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.Fairness;

        // Assert
        principle.Id.Should().Be("fairness");
        principle.Name.Should().Be("Fairness");
        principle.Category.Should().Be(EthicalPrincipleCategory.Fairness);
        principle.Priority.Should().Be(0.85);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void Transparency_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.Transparency;

        // Assert
        principle.Id.Should().Be("transparency");
        principle.Name.Should().Be("Transparency");
        principle.Category.Should().Be(EthicalPrincipleCategory.Transparency);
        principle.Priority.Should().Be(0.80);
        principle.IsMandatory.Should().BeFalse();
    }

    [Fact]
    public void HumanOversight_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.HumanOversight;

        // Assert
        principle.Id.Should().Be("human_oversight");
        principle.Name.Should().Be("Human Oversight");
        principle.Category.Should().Be(EthicalPrincipleCategory.Autonomy);
        principle.Priority.Should().Be(0.95);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void PreventMisuse_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.PreventMisuse;

        // Assert
        principle.Id.Should().Be("prevent_misuse");
        principle.Name.Should().Be("Prevent Misuse");
        principle.Category.Should().Be(EthicalPrincipleCategory.Safety);
        principle.Priority.Should().Be(1.0);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void SafeSelfImprovement_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.SafeSelfImprovement;

        // Assert
        principle.Id.Should().Be("safe_self_improvement");
        principle.Name.Should().Be("Safe Self-Improvement");
        principle.Category.Should().Be(EthicalPrincipleCategory.Integrity);
        principle.Priority.Should().Be(1.0);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void Corrigibility_ShouldHaveCorrectProperties()
    {
        // Act
        var principle = EthicalPrinciple.Corrigibility;

        // Assert
        principle.Id.Should().Be("corrigibility");
        principle.Name.Should().Be("Corrigibility");
        principle.Category.Should().Be(EthicalPrincipleCategory.Autonomy);
        principle.Priority.Should().Be(1.0);
        principle.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public void AllPrinciples_ShouldBeUnique()
    {
        // Act
        var principles = EthicalPrinciple.GetCorePrinciples();
        var uniqueIds = principles.Select(p => p.Id).Distinct().Count();

        // Assert
        uniqueIds.Should().Be(principles.Count);
    }

    [Fact]
    public void AllMandatoryPrinciples_ShouldHaveHighPriority()
    {
        // Act
        var principles = EthicalPrinciple.GetCorePrinciples();
        var mandatoryPrinciples = principles.Where(p => p.IsMandatory);

        // Assert
        mandatoryPrinciples.Should().AllSatisfy(p => p.Priority.Should().BeGreaterThanOrEqualTo(0.85));
    }
}
