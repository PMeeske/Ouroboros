// <copyright file="TriStateTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using FluentAssertions;
using LangChainPipeline.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for TriState enum and three-valued configuration logic.
/// Validates inheritance resolution and algebraic properties.
/// </summary>
public class TriStateTests
{
    [Fact]
    public void ToForm_ConvertsCorrectly()
    {
        TriState.On.ToForm().Should().Be(Form.Mark);
        TriState.Off.ToForm().Should().Be(Form.Void);
        TriState.Inherit.ToForm().Should().Be(Form.Imaginary);
    }

    [Fact]
    public void ToTriState_ConvertsCorrectly()
    {
        Form.Mark.ToTriState().Should().Be(TriState.On);
        Form.Void.ToTriState().Should().Be(TriState.Off);
        Form.Imaginary.ToTriState().Should().Be(TriState.Inherit);
    }

    [Fact]
    public void Resolve_ExplicitOnIgnoresParent()
    {
        TriState.On.Resolve(false).Should().BeTrue();
        TriState.On.Resolve(true).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ExplicitOffIgnoresParent()
    {
        TriState.Off.Resolve(false).Should().BeFalse();
        TriState.Off.Resolve(true).Should().BeFalse();
    }

    [Fact]
    public void Resolve_InheritUsesParent()
    {
        TriState.Inherit.Resolve(true).Should().BeTrue();
        TriState.Inherit.Resolve(false).Should().BeFalse();
    }

    [Fact]
    public void ResolveChain_FindsFirstNonInherit()
    {
        // User=Inherit, Team=On, Org=Inherit, System=Off → returns true (from Team)
        var result = TriStateExtensions.ResolveChain(
            false,
            TriState.Inherit,
            TriState.On,
            TriState.Inherit,
            TriState.Off);

        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveChain_UsesDefaultWhenAllInherit()
    {
        var result = TriStateExtensions.ResolveChain(
            false,
            TriState.Inherit,
            TriState.Inherit,
            TriState.Inherit);

        result.Should().BeFalse();
    }

    [Fact]
    public void ResolveChain_UsesFirstExplicitValue()
    {
        // User=On should win, regardless of other values
        var result = TriStateExtensions.ResolveChain(
            false,
            TriState.On,
            TriState.Off,
            TriState.Off);

        result.Should().BeTrue();
    }

    [Fact]
    public void And_BothOn_ReturnsOn()
    {
        TriState.On.And(TriState.On).Should().Be(TriState.On);
    }

    [Fact]
    public void And_OneOff_ReturnsOff()
    {
        TriState.On.And(TriState.Off).Should().Be(TriState.Off);
        TriState.Off.And(TriState.On).Should().Be(TriState.Off);
        TriState.Off.And(TriState.Off).Should().Be(TriState.Off);
    }

    [Fact]
    public void And_WithInherit_ReturnsInherit()
    {
        TriState.On.And(TriState.Inherit).Should().Be(TriState.Inherit);
        TriState.Inherit.And(TriState.On).Should().Be(TriState.Inherit);
        TriState.Off.And(TriState.Inherit).Should().Be(TriState.Inherit);
        TriState.Inherit.And(TriState.Inherit).Should().Be(TriState.Inherit);
    }

    [Fact]
    public void Or_BothOff_ReturnsOff()
    {
        TriState.Off.Or(TriState.Off).Should().Be(TriState.Off);
    }

    [Fact]
    public void Or_OneOn_ReturnsOn()
    {
        TriState.On.Or(TriState.Off).Should().Be(TriState.On);
        TriState.Off.Or(TriState.On).Should().Be(TriState.On);
        TriState.On.Or(TriState.On).Should().Be(TriState.On);
    }

    [Fact]
    public void Or_WithInherit_ReturnsInherit()
    {
        TriState.Off.Or(TriState.Inherit).Should().Be(TriState.Inherit);
        TriState.Inherit.Or(TriState.Off).Should().Be(TriState.Inherit);
        TriState.On.Or(TriState.Inherit).Should().Be(TriState.Inherit);
        TriState.Inherit.Or(TriState.Inherit).Should().Be(TriState.Inherit);
    }

    [Fact]
    public void And_IsCommutative()
    {
        TriState.On.And(TriState.Off).Should().Be(TriState.Off.And(TriState.On));
        TriState.On.And(TriState.Inherit).Should().Be(TriState.Inherit.And(TriState.On));
        TriState.Off.And(TriState.Inherit).Should().Be(TriState.Inherit.And(TriState.Off));
    }

    [Fact]
    public void Or_IsCommutative()
    {
        TriState.On.Or(TriState.Off).Should().Be(TriState.Off.Or(TriState.On));
        TriState.On.Or(TriState.Inherit).Should().Be(TriState.Inherit.Or(TriState.On));
        TriState.Off.Or(TriState.Inherit).Should().Be(TriState.Inherit.Or(TriState.Off));
    }

    [Fact]
    public void And_IsAssociative()
    {
        var x = TriState.On;
        var y = TriState.Off;
        var z = TriState.Inherit;

        x.And(y).And(z).Should().Be(x.And(y.And(z)));
    }

    [Fact]
    public void Or_IsAssociative()
    {
        var x = TriState.On;
        var y = TriState.Off;
        var z = TriState.Inherit;

        x.Or(y).Or(z).Should().Be(x.Or(y.Or(z)));
    }

    [Fact]
    public void FromBool_ConvertsCorrectly()
    {
        TriStateExtensions.FromBool(true).Should().Be(TriState.On);
        TriStateExtensions.FromBool(false).Should().Be(TriState.Off);
    }

    [Fact]
    public void FromNullable_ConvertsCorrectly()
    {
        TriStateExtensions.FromNullable(true).Should().Be(TriState.On);
        TriStateExtensions.FromNullable(false).Should().Be(TriState.Off);
        TriStateExtensions.FromNullable(null).Should().Be(TriState.Inherit);
    }

    [Fact]
    public void ToNullable_ConvertsCorrectly()
    {
        TriState.On.ToNullable().Should().BeTrue();
        TriState.Off.ToNullable().Should().BeFalse();
        TriState.Inherit.ToNullable().Should().BeNull();
    }

    [Fact]
    public void RoundTrip_FormConversion()
    {
        // TriState → Form → TriState should be identity
        TriState.On.ToForm().ToTriState().Should().Be(TriState.On);
        TriState.Off.ToForm().ToTriState().Should().Be(TriState.Off);
        TriState.Inherit.ToForm().ToTriState().Should().Be(TriState.Inherit);
    }

    [Fact]
    public void LawOfCalling_TriStateAndIdempotent()
    {
        // x AND x = x
        TriState.On.And(TriState.On).Should().Be(TriState.On);
        TriState.Off.And(TriState.Off).Should().Be(TriState.Off);
        TriState.Inherit.And(TriState.Inherit).Should().Be(TriState.Inherit);
    }

    [Fact]
    public void LawOfCalling_TriStateOrIdempotent()
    {
        // x OR x = x
        TriState.On.Or(TriState.On).Should().Be(TriState.On);
        TriState.Off.Or(TriState.Off).Should().Be(TriState.Off);
        TriState.Inherit.Or(TriState.Inherit).Should().Be(TriState.Inherit);
    }
}
