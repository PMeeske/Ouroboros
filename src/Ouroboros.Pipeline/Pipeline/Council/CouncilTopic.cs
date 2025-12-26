// <copyright file="CouncilTopic.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents a topic for council debate with all necessary context and constraints.
/// </summary>
/// <param name="Question">The main question or decision to be debated.</param>
/// <param name="Background">Background context and relevant information for the debate.</param>
/// <param name="Constraints">List of constraints or requirements that must be considered.</param>
public sealed record CouncilTopic(
    string Question,
    string Background,
    IReadOnlyList<string> Constraints)
{
    /// <summary>
    /// Creates a simple council topic with just a question.
    /// </summary>
    /// <param name="question">The question to debate.</param>
    /// <returns>A new CouncilTopic with empty background and constraints.</returns>
    public static CouncilTopic Simple(string question) =>
        new(question, string.Empty, []);

    /// <summary>
    /// Creates a council topic with question and background.
    /// </summary>
    /// <param name="question">The question to debate.</param>
    /// <param name="background">Background context for the debate.</param>
    /// <returns>A new CouncilTopic with specified question and background.</returns>
    public static CouncilTopic WithBackground(string question, string background) =>
        new(question, background, []);
}
