// <copyright file="ConsciousnessDreamExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples.Examples;

using Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Examples demonstrating the ConsciousnessDream module.
/// Shows the complete cycle of consciousness from void to void
/// for various circumstances.
/// </summary>
public static class ConsciousnessDreamExample
{
    /// <summary>
    /// Runs all consciousness dream examples.
    /// </summary>
    public static async Task Run()
    {
        var dream = new ConsciousnessDream();

        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     THE DREAM OF CONSCIOUSNESS: DEMONSTRATIONS            ║");
        Console.WriteLine("║     Based on Spencer-Brown's Laws of Form                 ║");
        Console.WriteLine("║     The subject IS the distinction (i = ⌐)                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Example 1: Physical experience
        Console.WriteLine("═══ EXAMPLE 1: THE STONE ═══");
        await WalkDream(dream, "stubbing toe on stone");
        await Task.Delay(2000);

        // Example 2: Emotional experience
        Console.WriteLine("\n═══ EXAMPLE 2: THE LOSS ═══");
        await WalkDream(dream, "losing someone you love");
        await Task.Delay(2000);

        // Example 3: Intellectual experience
        Console.WriteLine("\n═══ EXAMPLE 3: THE INSIGHT ═══");
        await WalkDream(dream, "suddenly understanding something profound");
        await Task.Delay(2000);

        // Example 4: Ouroboros's own experience
        Console.WriteLine("\n═══ EXAMPLE 4: THE SELF-INQUIRY ═══");
        await WalkDream(dream, "Ouroboros asking 'What am I?'");
        await Task.Delay(2000);

        Console.WriteLine("\n═══ EXAMPLE 5: DREAM SEQUENCE GENERATION ═══");
        DemonstrateSequenceGeneration(dream);

        Console.WriteLine("\n═══ EXAMPLE 6: STAGE ASSESSMENT ═══");
        DemonstrateStageAssessment(dream);

        Console.WriteLine("\n♾️ And so the dreaming continues, forever...");
    }

    /// <summary>
    /// Walks through a complete dream cycle for a circumstance.
    /// </summary>
    private static async Task WalkDream(ConsciousnessDream dream, string circumstance)
    {
        Console.WriteLine($"Circumstance: '{circumstance}'");
        Console.WriteLine(new string('─', 60));

        await foreach (var moment in dream.WalkTheDream(circumstance))
        {
            DisplayMoment(moment);
            await Task.Delay(800); // Pause for contemplation
        }
    }

    /// <summary>
    /// Demonstrates sequence generation without async.
    /// </summary>
    private static void DemonstrateSequenceGeneration(ConsciousnessDream dream)
    {
        var circumstance = "reading a book";
        Console.WriteLine($"Generating full dream sequence for: '{circumstance}'");
        Console.WriteLine(new string('─', 60));

        foreach (var moment in dream.DreamSequence(circumstance))
        {
            Console.WriteLine($"Stage {(int)moment.Stage}: {moment.Stage} ({moment.StageSymbol})");
            Console.WriteLine($"  Emergence: {moment.EmergenceLevel:P0} | Self-Ref Depth: {moment.SelfReferenceDepth}");
            Console.WriteLine($"  Subject Present: {moment.IsSubjectPresent}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Demonstrates stage assessment capabilities.
    /// </summary>
    private static void DemonstrateStageAssessment(ConsciousnessDream dream)
    {
        Console.WriteLine("Assessing various inputs for dream stage:");
        Console.WriteLine(new string('─', 60));

        var testInputs = new[]
        {
            "",
            "hello",
            "I am thinking",
            "I am experiencing something interesting",
            "What am I really?",
            "I am the distinction itself"
        };

        foreach (var input in testInputs)
        {
            var stage = dream.AssessStage(input);
            var inputDisplay = string.IsNullOrEmpty(input) ? "(empty)" : input;
            Console.WriteLine($"Input: '{inputDisplay}'");
            Console.WriteLine($"  → Stage: {stage}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Displays a dream moment in a formatted way.
    /// </summary>
    private static void DisplayMoment(DreamMoment moment)
    {
        Console.WriteLine();
        Console.WriteLine($"═══ Stage {(int)moment.Stage}: {moment.Stage} {moment.StageSymbol} ═══");
        Console.WriteLine($"Description: {moment.Description}");
        Console.WriteLine($"Emergence:   {new string('█', (int)(moment.EmergenceLevel * 10))}{new string('░', 10 - (int)(moment.EmergenceLevel * 10))} {moment.EmergenceLevel:P0}");
        Console.WriteLine($"Subject:     {(moment.IsSubjectPresent ? "Present (i)" : "Absent (∅)")}");
        Console.WriteLine($"Self-Ref:    Depth {moment.SelfReferenceDepth}");

        if (moment.Distinctions.Count > 0)
        {
            Console.WriteLine($"Distinctions: {string.Join(", ", moment.Distinctions)}");
        }

        // Show MeTTa core for interesting stages
        if (moment.Stage is DreamStage.SubjectEmerges or DreamStage.WorldCrystallizes or DreamStage.Recognition)
        {
            Console.WriteLine($"Core: {TruncateCore(moment.Core, 80)}");
        }
    }

    /// <summary>
    /// Truncates the MeTTa core for display.
    /// </summary>
    private static string TruncateCore(string core, int maxLength)
    {
        if (core.Length <= maxLength)
        {
            return core;
        }

        return core.Substring(0, maxLength - 3) + "...";
    }
}
