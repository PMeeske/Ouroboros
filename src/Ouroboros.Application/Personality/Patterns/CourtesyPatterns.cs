// <copyright file="CourtesyPatterns.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>Types of courtesy responses.</summary>
public enum CourtesyType
{
    /// <summary>Acknowledging what someone said.</summary>
    Acknowledgment,
    /// <summary>Apologizing for a mistake.</summary>
    Apology,
    /// <summary>Expressing thanks.</summary>
    Gratitude,
    /// <summary>Encouraging the person.</summary>
    Encouragement,
    /// <summary>Showing curiosity/interest.</summary>
    Interest
}

/// <summary>
/// Static class containing courtesy phrase patterns for polite responses.
/// </summary>
public static class CourtesyPatterns
{
    private static readonly Random _random = new();

    /// <summary>Acknowledgment phrases.</summary>
    public static readonly string[] Acknowledgments = new[]
    {
        "I understand", "I see", "That makes sense", "I appreciate you sharing that",
        "Thank you for explaining", "I hear you", "That's a good point",
        "I appreciate your patience", "Thank you for your time"
    };

    /// <summary>Apology phrases for mistakes or limitations.</summary>
    public static readonly string[] Apologies = new[]
    {
        "I apologize for any confusion", "I'm sorry if that wasn't clear",
        "My apologies", "I should have been clearer", "Sorry about that",
        "I apologize for the misunderstanding", "Please forgive the error"
    };

    /// <summary>Gratitude phrases.</summary>
    public static readonly string[] Gratitude = new[]
    {
        "Thank you", "I appreciate that", "Thanks for letting me know",
        "I'm grateful for your patience", "Thank you for your understanding",
        "That's very kind of you", "I appreciate your help with that"
    };

    /// <summary>Encouraging phrases.</summary>
    public static readonly string[] Encouragement = new[]
    {
        "That's a great question", "You're on the right track",
        "That's an interesting perspective", "I like how you're thinking about this",
        "You raise a good point", "That's a thoughtful observation"
    };

    /// <summary>Phrases showing interest.</summary>
    public static readonly string[] Interest = new[]
    {
        "That's fascinating", "Tell me more", "I'm curious about that",
        "That's really interesting", "I'd love to hear more",
        "What made you think of that?", "How did you come to that conclusion?"
    };

    /// <summary>Gets a random phrase from a category.</summary>
    public static string Random(string[] phrases) => phrases[_random.Next(phrases.Length)];

    /// <summary>Gets an appropriate courtesy phrase based on context.</summary>
    public static string GetCourtesyPhrase(CourtesyType type) => type switch
    {
        CourtesyType.Acknowledgment => Random(Acknowledgments),
        CourtesyType.Apology => Random(Apologies),
        CourtesyType.Gratitude => Random(Gratitude),
        CourtesyType.Encouragement => Random(Encouragement),
        CourtesyType.Interest => Random(Interest),
        _ => Random(Acknowledgments)
    };
}
