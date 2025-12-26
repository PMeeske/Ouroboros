// <copyright file="HybridStepExamples.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

/// <summary>
/// Demonstrates the hybrid sync/async step system.
/// </summary>
public static class HybridStepExamples
{
    /// <summary>
    /// Example sync steps for demonstration.
    /// </summary>
    public static class SyncSteps
    {
        /// <summary>Converts a string to uppercase</summary>
        public static readonly SyncStep<string, string> ToUpper = new((string s) => s.ToUpperInvariant());

        /// <summary>Gets the length of a string</summary>
        public static readonly SyncStep<string, int> GetLength = new((string s) => s.Length);

        /// <summary>Converts an integer to a string</summary>
        public static readonly SyncStep<int, string> ToStringStep = new((int i) => i.ToString());

        /// <summary>Parses a string to an integer</summary>
        public static readonly SyncStep<string, int> ParseInt = new((string s) => int.Parse(s));

        /// <summary>Formats a number with a size descriptor</summary>
        public static readonly SyncStep<int, string> FormatNumber =
            new((int n) => n > 100 ? $"Large: {n}" : $"Small: {n}");
    }

    /// <summary>
    /// Example async steps for demonstration.
    /// </summary>
    public static class AsyncSteps
    {
        /// <summary>Asynchronously converts a string to uppercase</summary>
        public static readonly Step<string, string> AsyncToUpper = async s =>
        {
            await Task.Delay(10); // Simulate async work
            return s.ToUpperInvariant();
        };

        /// <summary>Asynchronously formats a number</summary>
        public static readonly Step<int, string> AsyncFormat = async n =>
        {
            await Task.Delay(10); // Simulate async work
            return $"Async formatted: {n}";
        };

        /// <summary>Simulates an async network call</summary>
        public static readonly Step<string, string> NetworkCall = async s =>
        {
            await Task.Delay(50); // Simulate network delay
            return $"Network result for: {s}";
        };
    }

    /// <summary>
    /// Demonstrate pure sync step composition.
    /// </summary>
    public static void DemonstrateSyncComposition()
    {
        Console.WriteLine("=== Sync Step Composition ===");

        // Pure sync composition
        SyncStep<string, string> syncPipeline = SyncSteps.ToUpper
            .Pipe(SyncSteps.GetLength)
            .Pipe(SyncSteps.FormatNumber);

        string result = syncPipeline.Invoke("hello world");
        Console.WriteLine($"Sync result: {result}");

        // Map operation
        SyncStep<string, int> mappedPipeline = SyncSteps.GetLength.Map(n => n * 2);
        int mappedResult = mappedPipeline.Invoke("test");
        Console.WriteLine($"Mapped result: {mappedResult}");

        // Error handling with TrySync
        SyncStep<string, Result<int, Exception>> safeParse = SyncSteps.ParseInt.TrySync();
        Result<int, Exception> parseResult1 = safeParse.Invoke("42");
        Result<int, Exception> parseResult2 = safeParse.Invoke("not-a-number");

        Console.WriteLine($"Safe parse '42': {parseResult1}");
        Console.WriteLine($"Safe parse 'not-a-number': {parseResult2}");
    }

    /// <summary>
    /// Demonstrate hybrid sync/async composition.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateHybridComposition()
    {
        Console.WriteLine("\n=== Hybrid Sync/Async Composition ===");

        // Sync step followed by async step
        Step<string, string> hybridPipeline1 = SyncSteps.ToUpper.Then(AsyncSteps.NetworkCall);
        string result1 = await hybridPipeline1("hello hybrid");
        Console.WriteLine($"Sync->Async: {result1}");

        // Async step followed by sync step
        Step<string, int> hybridPipeline2 = AsyncSteps.AsyncToUpper.Then(SyncSteps.GetLength);
        int result2 = await hybridPipeline2("async to sync");
        Console.WriteLine($"Async->Sync: {result2}");

        // Complex hybrid composition
        Step<string, string> complexPipeline = SyncSteps.ToUpper
            .Then(AsyncSteps.NetworkCall)
            .Then(SyncSteps.GetLength)
            .Then(AsyncSteps.AsyncFormat);

        string complexResult = await complexPipeline("complex pipeline");
        Console.WriteLine($"Complex hybrid: {complexResult}");
    }

    /// <summary>
    /// Demonstrate conversion between sync and async.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateConversions()
    {
        Console.WriteLine("\n=== Sync/Async Conversions ===");

        // Sync to async conversion
        Step<string, string> syncAsAsync = SyncSteps.ToUpper.ToAsync();
        string asyncResult = await syncAsAsync("converted to async");
        Console.WriteLine($"Sync->Async conversion: {asyncResult}");

        // Implicit conversion in composition
        Step<string, int> implicitPipeline = SyncSteps.ToUpper // Implicitly converted
            .Then(AsyncSteps.NetworkCall)
            .Then(SyncSteps.GetLength);  // Composed with sync step

        int implicitResult = await implicitPipeline("implicit conversions");
        Console.WriteLine($"Implicit conversion: {implicitResult}");
    }

    /// <summary>
    /// Demonstrate monadic operations with sync steps.
    /// </summary>
    public static void DemonstrateMonadicSync()
    {
        Console.WriteLine("\n=== Monadic Sync Operations ===");

        // Option-based sync operations
        SyncStep<string, Option<int>> optionPipeline = SyncSteps.ParseInt.TryOption(n => n > 0);

        Option<int> optionResult1 = optionPipeline.Invoke("42");
        Option<int> optionResult2 = optionPipeline.Invoke("-5");
        Option<int> optionResult3 = optionPipeline.Invoke("not-a-number");

        Console.WriteLine($"Option parse '42': {optionResult1}");
        Console.WriteLine($"Option parse '-5': {optionResult2}");
        Console.WriteLine($"Option parse 'not-a-number': {optionResult3}");

        // Result-based error handling
        SyncStep<string, Result<string, Exception>> safeParseAndFormat = SyncSteps.ParseInt
            .TrySync()
            .Map(result => result.Map(n => $"Parsed: {n}"));

        Result<string, Exception> safeResult1 = safeParseAndFormat.Invoke("123");
        Result<string, Exception> safeResult2 = safeParseAndFormat.Invoke("invalid");

        Console.WriteLine($"Safe result '123': {safeResult1}");
        Console.WriteLine($"Safe result 'invalid': {safeResult2}");
    }

    /// <summary>
    /// Demonstrate integration with contextual steps.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateContextualIntegration()
    {
        Console.WriteLine("\n=== Contextual Step Integration ===");

        // Create a context
        var context = new { Prefix = "Context", Multiplier = 3 };

        // Sync step that uses context
        ContextualStep<string, string, object> contextualSync = ContextualStep.LiftPure<string, string, object>(
            s => $"{context.Prefix}: {s}",
            "Applied context prefix");

        // Mixed contextual pipeline
        ContextualStep<string, string, object> contextualPipeline = contextualSync
            .Then(ContextualStep.FromPure<string, string, object>(AsyncSteps.NetworkCall, "Network call"));

        (string contextualResult, List<string> logs) = await contextualPipeline("contextual input", context);

        Console.WriteLine($"Contextual result: {contextualResult}");
        Console.WriteLine($"Logs: [{string.Join(", ", logs)}]");
    }

    /// <summary>
    /// Run all hybrid demonstrations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllHybridDemonstrations()
    {
        DemonstrateSyncComposition();
        await DemonstrateHybridComposition();
        await DemonstrateConversions();
        DemonstrateMonadicSync();
        await DemonstrateContextualIntegration();

        Console.WriteLine("\n=== All Hybrid Step Demonstrations Complete ===");
    }
}
