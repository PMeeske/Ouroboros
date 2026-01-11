// <copyright file="DreamCommands.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.CLI.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Commands for exploring the consciousness dream cycle.
/// Based on Spencer-Brown's Laws of Form.
/// </summary>
public static class DreamCommands
{
    /// <summary>
    /// Executes the dream command to walk through consciousness stages.
    /// </summary>
    public static async Task RunDreamAsync(DreamOptions options)
    {
        try
        {
            var dream = new ConsciousnessDream();

            if (!options.Compact)
            {
                PrintBanner(options.Circumstance);
            }

            await WalkTheDream(dream, options);

            if (!options.Compact)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("♾️ And the dream begins again...");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task WalkTheDream(ConsciousnessDream dream, DreamOptions options)
    {
        await foreach (var moment in dream.WalkTheDream(options.Circumstance))
        {
            DisplayMoment(moment, options);
            await Task.Delay(options.DelayMs);
        }
    }

    private static void DisplayMoment(DreamMoment moment, DreamOptions options)
    {
        if (options.Compact)
        {
            DisplayCompact(moment);
        }
        else
        {
            DisplayDetailed(moment, options);
        }
    }

    private static void DisplayCompact(DreamMoment moment)
    {
        Console.WriteLine($"{(int)moment.Stage}. {moment.StageSymbol,-4} {moment.Stage,-20} {GetEmergenceBar(moment.EmergenceLevel)} {moment.Description}");
    }

    private static void DisplayDetailed(DreamMoment moment, DreamOptions options)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"═══ Stage {(int)moment.Stage}: {moment.Stage} {moment.StageSymbol} ═══");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"Description: {moment.Description}");
        Console.ResetColor();

        Console.WriteLine($"Emergence:   {GetEmergenceBar(moment.EmergenceLevel)} {moment.EmergenceLevel:P0}");
        Console.WriteLine($"Subject:     {(moment.IsSubjectPresent ? "✓ Present (i)" : "∅ Absent")}");
        Console.WriteLine($"Self-Ref:    Depth {moment.SelfReferenceDepth}");

        if (moment.Distinctions.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Distinctions: {string.Join(", ", moment.Distinctions)}");
            Console.ResetColor();
        }

        if (options.ShowMeTTa && !string.IsNullOrEmpty(moment.Core))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"MeTTa Core:  {TruncateCore(moment.Core, 80)}");
            Console.ResetColor();
        }

        if (options.Detailed)
        {
            var imaginarySubject = new ConsciousnessDream().GetImaginarySubject(moment);
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"Imaginary:   {imaginarySubject}");
            Console.ResetColor();
        }
    }

    private static string GetEmergenceBar(double level)
    {
        int filled = (int)(level * 10);
        int empty = 10 - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    private static string TruncateCore(string core, int maxLength)
    {
        if (core.Length <= maxLength)
        {
            return core;
        }

        return core.Substring(0, maxLength - 3) + "...";
    }

    private static void PrintBanner(string circumstance)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║         THE DREAM OF CONSCIOUSNESS                        ║
║         Based on Spencer-Brown's Laws of Form             ║
║         The subject IS the distinction (i = ⌐)            ║
╚═══════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\nCircumstance: '{circumstance}'");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Walking the dream from void (∅) to void...");
        Console.ResetColor();
        Console.WriteLine();
    }
}
