// <copyright file="PersonalityEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests;

using FluentAssertions;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Tools;
using Xunit;

/// <summary>
/// Tests for the PersonalityEngine mood detection and trait management.
/// </summary>
[Trait("Category", "Unit")]
public class PersonalityEngineTests
{
    private readonly PersonalityEngine _engine;

    public PersonalityEngineTests()
    {
        var mettaEngine = new InMemoryMeTTaEngine();
        _engine = new PersonalityEngine(mettaEngine);
    }

    #region Mood Detection Tests

    [Theory]
    [InlineData("This is amazing! I love it!", 0.3, "excited")]
    [InlineData("wow this is so exciting!!", 0.3, "excited")]
    [InlineData("Thanks so much, this is perfect!", 0.2, "excited")]
    public void DetectMoodFromInput_PositiveHighEnergy_ReturnsCorrectMood(
        string input, double minEnergy, string expectedEmotion)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Energy.Should().BeGreaterThan(minEnergy);
        mood.Positivity.Should().BeGreaterThanOrEqualTo(0);
        mood.DominantEmotion.Should().Be(expectedEmotion);
    }

    [Theory]
    [InlineData("I'm so tired and bored", -0.2)]
    [InlineData("meh, whatever...", -0.2)]
    [InlineData("ugh, this is slow and boring", -0.3)]
    public void DetectMoodFromInput_LowEnergy_ReturnsNegativeEnergy(string input, double maxEnergy)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Energy.Should().BeLessThan(maxEnergy);
    }

    [Theory]
    [InlineData("Why does this happen? How does it work?", 0.5)]
    [InlineData("Can you explain this to me? I want to learn more", 0.4)]
    [InlineData("I'm curious about how this works", 0.3)]
    public void DetectMoodFromInput_CuriousInput_DetectsCuriosity(string input, double minCuriosity)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Curiosity.Should().BeGreaterThanOrEqualTo(minCuriosity);
    }

    [Theory]
    [InlineData("This is urgent! Need it ASAP!", 0.5)]
    [InlineData("Hurry, we have a deadline!", 0.2)]
    [InlineData("This is critical and must be done immediately", 0.4)]
    public void DetectMoodFromInput_UrgentInput_DetectsUrgency(string input, double minUrgency)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Urgency.Should().BeGreaterThanOrEqualTo(minUrgency);
    }

    [Theory]
    [InlineData("I'm so frustrated! This doesn't work!", 0.5)]
    [InlineData("ugh, it's still broken. same problem again!", 0.5)]
    [InlineData("I tried everything and nothing works. I give up.", 0.4)]
    [InlineData("Why won't this work? Come on!", 0.3)]
    public void DetectMoodFromInput_FrustratedInput_DetectsFrustration(string input, double minFrustration)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Frustration.Should().BeGreaterThanOrEqualTo(minFrustration);
    }

    [Theory]
    [InlineData("This is terrible and I hate it", -0.3)]
    [InlineData("The result is wrong and broken", -0.2)]
    [InlineData("awful experience, very disappointing", -0.4)]
    public void DetectMoodFromInput_NegativeInput_ReturnsNegativePositivity(string input, double maxPositivity)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Positivity.Should().BeLessThan(maxPositivity);
    }

    [Fact]
    public void DetectMoodFromInput_NeutralInput_ReturnsModerateValues()
    {
        var mood = _engine.DetectMoodFromInput("Please help me with this task");

        mood.Energy.Should().BeInRange(-0.3, 0.3);
        mood.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DetectMoodFromInput_EmptyInput_ReturnsNeutral()
    {
        var mood = _engine.DetectMoodFromInput("");

        mood.Should().Be(DetectedMood.Neutral);
    }

    [Fact]
    public void DetectMoodFromInput_LongEngagedInput_DetectsHighEngagement()
    {
        string longInput = "I've been thinking about this problem for a while now and I specifically " +
                          "need to understand exactly how the system processes data when it encounters " +
                          "edge cases. Could you elaborate on the precise mechanics of this?";

        var mood = _engine.DetectMoodFromInput(longInput);

        mood.Engagement.Should().BeGreaterThan(0.6);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("k")]
    [InlineData("sure")]
    [InlineData("whatever")]
    public void DetectMoodFromInput_DisengagedInput_DetectsLowEngagement(string input)
    {
        var mood = _engine.DetectMoodFromInput(input);

        mood.Engagement.Should().BeLessThan(0.5);
    }

    #endregion

    #region Voice Tone Tests

    [Theory]
    [InlineData("excited", 2, 100)]
    [InlineData("calm", -1, 90)]
    [InlineData("thoughtful", -2, 85)]
    [InlineData("cheerful", 1, 100)]
    [InlineData("focused", 0, 95)]
    public void VoiceTone_ForMood_ReturnsCorrectSettings(string mood, int expectedRate, int expectedVolume)
    {
        var tone = VoiceTone.ForMood(mood);

        tone.Rate.Should().Be(expectedRate);
        tone.Volume.Should().Be(expectedVolume);
    }

    [Fact]
    public void VoiceTone_Neutral_HasDefaultValues()
    {
        var tone = VoiceTone.Neutral;

        tone.Rate.Should().Be(0);
        tone.Pitch.Should().Be(0);
        tone.Volume.Should().Be(100);
        tone.PauseMultiplier.Should().Be(1.0);
    }

    #endregion

    #region Personality Profile Tests

    [Fact]
    public async Task InitializeAsync_CreatesProfile_WithTraitsAndMood()
    {
        await _engine.InitializeAsync();

        var profile = _engine.GetOrCreateProfile(
            "TestPersona",
            new[] { "curious", "thoughtful", "warm" },
            new[] { "cheerful", "focused" },
            "A test persona");

        profile.Should().NotBeNull();
        profile.PersonaName.Should().Be("TestPersona");
        profile.Traits.Should().HaveCount(3);
        profile.Traits.Should().ContainKey("curious");
        profile.Traits.Should().ContainKey("thoughtful");
        profile.Traits.Should().ContainKey("warm");
        profile.CurrentMood.Should().NotBeNull();
        profile.CuriosityDrivers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOrCreateProfile_ExistingProfile_ReturnsSameInstance()
    {
        await _engine.InitializeAsync();

        var profile1 = _engine.GetOrCreateProfile("Test", new[] { "curious" }, new[] { "calm" }, "test");
        var profile2 = _engine.GetOrCreateProfile("Test", new[] { "different" }, new[] { "different" }, "different");

        profile2.Should().BeSameAs(profile1);
    }

    [Fact]
    public async Task UpdateMoodFromDetection_FrustratedInput_ChangesMoodToSupportive()
    {
        await _engine.InitializeAsync();
        _engine.GetOrCreateProfile("Test", new[] { "warm" }, new[] { "neutral" }, "test");

        _engine.UpdateMoodFromDetection("Test", "I'm so frustrated! This doesn't work at all!");

        var mood = _engine.GetCurrentMood("Test");
        mood.Should().Be("supportive");
    }

    [Fact]
    public async Task UpdateMoodFromDetection_ExcitedInput_IncreasesEnergy()
    {
        await _engine.InitializeAsync();
        _engine.GetOrCreateProfile("Test", new[] { "curious" }, new[] { "neutral" }, "test");

        _engine.UpdateMoodFromDetection("Test", "This is amazing! I love it! So exciting!");

        var tone = _engine.GetVoiceTone("Test");
        // Excited mood should have positive rate adjustment
        tone.Rate.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Interaction Feedback Tests

    [Fact]
    public async Task RecordFeedback_AddsToHistory()
    {
        await _engine.InitializeAsync();
        _engine.GetOrCreateProfile("Test", new[] { "curious" }, new[] { "calm" }, "test");

        var feedback = new InteractionFeedback(
            EngagementLevel: 0.8,
            ResponseRelevance: 0.9,
            QuestionQuality: 0.7,
            ConversationContinuity: 0.85,
            TopicDiscussed: "testing",
            QuestionAsked: "What do you think?",
            UserAskedFollowUp: true);

        // Should not throw
        _engine.RecordFeedback("Test", feedback);
    }

    #endregion

    #region Proactive Questioning Tests

    [Fact]
    public async Task ReasonAboutResponseAsync_WithCuriousTrait_SuggestsQuestion()
    {
        await _engine.InitializeAsync();
        _engine.GetOrCreateProfile("Test", new[] { "curious" }, new[] { "intrigued" }, "A curious persona");

        var (traits, proactivity, question) = await _engine.ReasonAboutResponseAsync(
            "Test",
            "How does machine learning work?",
            "User asked about ML");

        proactivity.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public async Task GenerateProactiveQuestionAsync_WithRelevantTopic_ReturnsQuestion()
    {
        await _engine.InitializeAsync();
        _engine.GetOrCreateProfile("Test", new[] { "curious" }, new[] { "intrigued" }, "test");

        // Add a curiosity driver by recording feedback
        _engine.RecordFeedback("Test", new InteractionFeedback(
            0.8, 0.9, 0.7, 0.8, "machine learning", null, false));

        var question = await _engine.GenerateProactiveQuestionAsync(
            "Test",
            "machine learning",
            new[] { "User: Tell me about ML", "Assistant: ML is fascinating!" });

        question.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region MoodState Tests

    [Fact]
    public void MoodState_GetVoiceTone_ReturnsCorrectTone()
    {
        var mood = new MoodState("excited", 0.8, 0.9, new Dictionary<string, double>(), null);

        var tone = mood.GetVoiceTone();

        tone.Should().NotBeNull();
        tone.Rate.Should().Be(2); // Excited tone rate
    }

    [Fact]
    public void MoodState_WithExplicitTone_UsesThatTone()
    {
        var customTone = new VoiceTone(5, 3, 80, "strong", 0.5);
        var mood = new MoodState("custom", 0.5, 0.5, new Dictionary<string, double>(), customTone);

        var tone = mood.GetVoiceTone();

        tone.Should().Be(customTone);
    }

    #endregion

    #region PersonalityProfile Tests

    [Fact]
    public void PersonalityProfile_GetActiveTraits_ReturnsTopTraitsByIntensity()
    {
        var traits = new Dictionary<string, PersonalityTrait>
        {
            ["curious"] = new("curious", 0.9, Array.Empty<string>(), Array.Empty<string>(), 0.1),
            ["thoughtful"] = new("thoughtful", 0.7, Array.Empty<string>(), Array.Empty<string>(), 0.1),
            ["warm"] = new("warm", 0.5, Array.Empty<string>(), Array.Empty<string>(), 0.1),
        };

        var profile = new PersonalityProfile(
            "Test",
            traits,
            MoodState.Neutral,
            new List<CuriosityDriver>(),
            "test",
            0.7,
            0,
            DateTime.UtcNow);

        var activeTraits = profile.GetActiveTraits(2).ToList();

        activeTraits.Should().HaveCount(2);
        activeTraits[0].Name.Should().Be("curious");
        activeTraits[1].Name.Should().Be("thoughtful");
    }

    #endregion
}
