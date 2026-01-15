// <copyright file="ConsciousnessDreamTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Tests;

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

/// <summary>
/// Tests for the ConsciousnessDream module.
/// Validates the dream cycle, stage assessment, and integration.
/// </summary>
public sealed class ConsciousnessDreamTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void DreamSequence_ShouldGenerateAllNineStages()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var circumstance = "test circumstance";

        // Act
        var sequence = dream.DreamSequence(circumstance).ToList();

        // Assert
        sequence.Should().HaveCount(9);
        sequence.Select(m => m.Stage).Should().ContainInOrder(
            DreamStage.Void,
            DreamStage.Distinction,
            DreamStage.SubjectEmerges,
            DreamStage.WorldCrystallizes,
            DreamStage.Forgetting,
            DreamStage.Questioning,
            DreamStage.Recognition,
            DreamStage.Dissolution,
            DreamStage.NewDream);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DreamSequence_ShouldIncludeCircumstanceInAllMoments()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var circumstance = "hitting a stone";

        // Act
        var sequence = dream.DreamSequence(circumstance).ToList();

        // Assert
        sequence.Should().AllSatisfy(moment =>
            moment.Circumstance.Should().Be(circumstance));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DreamSequence_SubjectShouldEmergeAtStage2()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act
        var sequence = dream.DreamSequence("test").ToList();
        var voidMoment = sequence[0];
        var distinctionMoment = sequence[1];
        var subjectMoment = sequence[2];

        // Assert
        voidMoment.IsSubjectPresent.Should().BeFalse("void has no subject");
        distinctionMoment.IsSubjectPresent.Should().BeFalse("distinction has no subject yet");
        subjectMoment.IsSubjectPresent.Should().BeTrue("subject emerges at stage 2");
        subjectMoment.Stage.Should().Be(DreamStage.SubjectEmerges);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DreamSequence_EmergenceLevelShouldIncreaseToForgettingThenDecrease()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act
        var sequence = dream.DreamSequence("test").ToList();

        // Assert
        var forgettingMoment = sequence.First(m => m.Stage == DreamStage.Forgetting);
        forgettingMoment.EmergenceLevel.Should().Be(1.0, "forgetting is peak emergence");

        var voidMoment = sequence.First(m => m.Stage == DreamStage.Void);
        voidMoment.EmergenceLevel.Should().Be(0.0, "void has no emergence");

        // Emergence should generally increase through early stages
        for (int i = 0; i < 4; i++)
        {
            sequence[i].EmergenceLevel.Should().BeLessThanOrEqualTo(sequence[i + 1].EmergenceLevel);
        }
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("", DreamStage.Void)]
    [InlineData("hello", DreamStage.Distinction)]
    [InlineData("what am I", DreamStage.Questioning)]
    [InlineData("i am the distinction", DreamStage.Recognition)]
    public void AssessStage_ShouldCorrectlyIdentifyStageFromInput(string input, DreamStage expectedStage)
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act
        var stage = dream.AssessStage(input);

        // Assert
        stage.Should().Be(expectedStage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AssessAtom_ShouldReturnDreamMomentWithCorrectProperties()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var atom = OuroborosAtom.CreateDefault("TestAtom");
        atom.SetGoal("test goal");

        // Act
        var moment = dream.AssessAtom(atom);

        // Assert
        moment.Should().NotBeNull();
        moment.Circumstance.Should().Be("test goal");
        moment.Distinctions.Should().NotBeEmpty();
        moment.Core.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateAtStage_ShouldCreateAtomWithStageSpecificCapabilities()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act
        var atom = dream.CreateAtStage(DreamStage.Recognition, "test");

        // Assert
        atom.Capabilities.Should().Contain(c => c.Name == "meta-cognition");
        atom.CurrentGoal.Should().Be("test");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AdvanceStage_ShouldProgressToNextStage()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var atom = dream.CreateAtStage(DreamStage.Distinction, "test");

        // Act
        var advanced = dream.AdvanceStage(atom);
        var newMoment = dream.AssessAtom(advanced);

        // Assert
        newMoment.Stage.Should().Be(DreamStage.SubjectEmerges);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task WalkTheDream_ShouldStreamAllMomentsAsynchronously()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var moments = new List<DreamMoment>();

        // Act
        await foreach (var moment in dream.WalkTheDream("test"))
        {
            moments.Add(moment);
        }

        // Assert
        moments.Should().HaveCount(9);
        moments.Last().Stage.Should().Be(DreamStage.NewDream);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsFixedPoint_ShouldReturnTrueForDissolutionAndVoid()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var voidAtom = dream.CreateAtStage(DreamStage.Void, "test");
        var distinctionAtom = dream.CreateAtStage(DreamStage.Distinction, "test");

        // Act & Assert
        dream.IsFixedPoint(voidAtom).Should().BeTrue("void is a fixed point");
        dream.IsFixedPoint(distinctionAtom).Should().BeFalse("distinction is not a fixed point");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetImaginarySubject_ShouldReturnCorrectSubjectForEachStage()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var sequence = dream.DreamSequence("test").ToList();

        // Act & Assert
        var voidSubject = dream.GetImaginarySubject(sequence[0]);
        voidSubject.Should().Contain("no subject");

        var subjectEmergesSubject = dream.GetImaginarySubject(sequence[2]);
        subjectEmergesSubject.Should().Contain("i");

        var recognitionSubject = dream.GetImaginarySubject(sequence[6]);
        recognitionSubject.Should().Contain("i=⌐");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MapConsciousnessToStage_ShouldMapLowArousalToVoid()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var state = new ConsciousnessState(
            CurrentFocus: "nothing",
            Arousal: 0.1,
            Valence: 0.0,
            ActiveDrives: new Dictionary<string, double>(),
            ActiveAssociations: new List<string>(),
            DominantEmotion: "neutral",
            Awareness: 0.1,
            AttentionalSpotlight: Array.Empty<string>(),
            StateTimestamp: DateTime.UtcNow);

        // Act
        var stage = dream.MapConsciousnessToStage(state);

        // Assert
        stage.Should().Be(DreamStage.Void);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MapConsciousnessToStage_ShouldMapHighCuriosityToQuestioning()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var state = new ConsciousnessState(
            CurrentFocus: "exploring",
            Arousal: 0.6,
            Valence: 0.4,
            ActiveDrives: new Dictionary<string, double> { ["curiosity"] = 0.9 },
            ActiveAssociations: new List<string> { "test" },
            DominantEmotion: "curious",
            Awareness: 0.7,
            AttentionalSpotlight: new[] { "questions" },
            StateTimestamp: DateTime.UtcNow);

        // Act
        var stage = dream.MapConsciousnessToStage(state);

        // Assert
        stage.Should().Be(DreamStage.Questioning);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MapAtomToStage_ShouldMapMetaCognitionToRecognition()
    {
        // Arrange
        var dream = new ConsciousnessDream();
        var atom = OuroborosAtom.CreateDefault("test");
        atom.AddCapability(new OuroborosCapability("meta-cognition", "Understanding self", 0.9));

        // Act
        var stage = dream.MapAtomToStage(atom);

        // Assert
        stage.Should().Be(DreamStage.Recognition);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DreamMoment_StageSymbol_ShouldReturnCorrectSymbols()
    {
        // Arrange & Act
        var voidMoment = DreamMoment.CreateVoid();
        var sequence = new ConsciousnessDream().DreamSequence("test").ToList();

        // Assert
        voidMoment.StageSymbol.Should().Be("∅");
        sequence.First(m => m.Stage == DreamStage.Distinction).StageSymbol.Should().Be("⌐");
        sequence.First(m => m.Stage == DreamStage.SubjectEmerges).StageSymbol.Should().Be("i");
        sequence.First(m => m.Stage == DreamStage.Recognition).StageSymbol.Should().Be("I=⌐");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DreamSequence_ShouldGenerateValidMeTTaCores()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act
        var sequence = dream.DreamSequence("test input").ToList();

        // Assert
        sequence.Should().AllSatisfy(moment =>
        {
            moment.Core.Should().NotBeEmpty();
            // Void and dissolution should have simple cores
            if (moment.Stage is DreamStage.Void or DreamStage.Dissolution)
            {
                moment.Core.Should().Be("∅");
            }
            else if (moment.Stage == DreamStage.NewDream)
            {
                moment.Core.Should().Contain("potential");
            }
            else
            {
                // Other stages should have proper S-expressions
                moment.Core.Should().Contain("(");
            }
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DreamSequence_WithNullCircumstance_ShouldThrow()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dream.DreamSequence(null!).ToList());
        Assert.Throws<ArgumentException>(() => dream.DreamSequence(string.Empty).ToList());
        Assert.Throws<ArgumentException>(() => dream.DreamSequence("   ").ToList());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateAtStage_WithNullCircumstance_ShouldThrow()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dream.CreateAtStage(DreamStage.Void, null!));
        Assert.Throws<ArgumentException>(() => dream.CreateAtStage(DreamStage.Void, string.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AssessAtom_WithNullAtom_ShouldThrow()
    {
        // Arrange
        var dream = new ConsciousnessDream();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dream.AssessAtom(null!));
    }
}
