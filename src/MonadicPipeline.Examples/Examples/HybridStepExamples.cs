// ==========================================================
// Hybrid Sync/Async Step Demonstrations
// Shows integration between SyncStep and async Step systems
// ==========================================================

namespace LangChainPipeline.Examples;

/// <summary>
/// Demonstrates the hybrid sync/async step system
/// </summary>
public static class HybridStepExamples
{
    /// <summary>
    /// Example sync steps for demonstration
    /// </summary>
    public static class SyncSteps
    {
        public static readonly SyncStep<string, string> ToUpper = new((string s) => s.ToUpperInvariant());
        public static readonly SyncStep<string, int> GetLength = new((string s) => s.Length);
        public static readonly SyncStep<int, string> ToStringStep = new((int i) => i.ToString());
        public static readonly SyncStep<string, int> ParseInt = new((string s) => int.Parse(s));

        public static readonly SyncStep<int, string> FormatNumber =
            new((int n) => n > 100 ? $"Large: {n}" : $"Small: {n}");
    }

    /// <summary>
    /// Example async steps for demonstration
    /// </summary>
    public static class AsyncSteps
    {
        public static readonly Step<string, string> AsyncToUpper = async s =>
        {
            await Task.Delay(10); // Simulate async work
            return s.ToUpperInvariant();
        };

        public static readonly Step<int, string> AsyncFormat = async n =>
        {
            await Task.Delay(10); // Simulate async work
            return $"Async formatted: {n}";
        };

        public static readonly Step<string, string> NetworkCall = async s =>
        {
            await Task.Delay(50); // Simulate network delay
            return $"Network result for: {s}";
        };
    }

    /// <summary>
    /// Demonstrate pure sync step composition
    /// </summary>
    public static void DemonstrateSyncComposition()
    {
        Console.WriteLine("=== Sync Step Composition ===");

        // Pure sync composition
        var syncPipeline = SyncSteps.ToUpper
            .Pipe(SyncSteps.GetLength)
            .Pipe(SyncSteps.FormatNumber);

        var result = syncPipeline.Invoke("hello world");
        Console.WriteLine($"Sync result: {result}");

        // Map operation
        var mappedPipeline = SyncSteps.GetLength.Map(n => n * 2);
        var mappedResult = mappedPipeline.Invoke("test");
        Console.WriteLine($"Mapped result: {mappedResult}");

        // Error handling with TrySync
        var safeParse = SyncSteps.ParseInt.TrySync();
        var parseResult1 = safeParse.Invoke("42");
        var parseResult2 = safeParse.Invoke("not-a-number");

        Console.WriteLine($"Safe parse '42': {parseResult1}");
        Console.WriteLine($"Safe parse 'not-a-number': {parseResult2}");
    }

    /// <summary>
    /// Demonstrate hybrid sync/async composition
    /// </summary>
    public static async Task DemonstrateHybridComposition()
    {
        Console.WriteLine("\n=== Hybrid Sync/Async Composition ===");

        // Sync step followed by async step
        var hybridPipeline1 = SyncSteps.ToUpper.Then(AsyncSteps.NetworkCall);
        var result1 = await hybridPipeline1("hello hybrid");
        Console.WriteLine($"Sync->Async: {result1}");

        // Async step followed by sync step
        var hybridPipeline2 = AsyncSteps.AsyncToUpper.Then(SyncSteps.GetLength);
        var result2 = await hybridPipeline2("async to sync");
        Console.WriteLine($"Async->Sync: {result2}");

        // Complex hybrid composition
        var complexPipeline = SyncSteps.ToUpper
            .Then(AsyncSteps.NetworkCall)
            .Then(SyncSteps.GetLength)
            .Then(AsyncSteps.AsyncFormat);

        var complexResult = await complexPipeline("complex pipeline");
        Console.WriteLine($"Complex hybrid: {complexResult}");
    }

    /// <summary>
    /// Demonstrate conversion between sync and async
    /// </summary>
    public static async Task DemonstrateConversions()
    {
        Console.WriteLine("\n=== Sync/Async Conversions ===");

        // Sync to async conversion
        var syncAsAsync = SyncSteps.ToUpper.ToAsync();
        var asyncResult = await syncAsAsync("converted to async");
        Console.WriteLine($"Sync->Async conversion: {asyncResult}");

        // Implicit conversion in composition
        Step<string, int> implicitPipeline = SyncSteps.ToUpper  // Implicitly converted
            .Then(AsyncSteps.NetworkCall)
            .Then(SyncSteps.GetLength);  // Composed with sync step

        var implicitResult = await implicitPipeline("implicit conversions");
        Console.WriteLine($"Implicit conversion: {implicitResult}");
    }

    /// <summary>
    /// Demonstrate monadic operations with sync steps
    /// </summary>
    public static void DemonstrateMonadicSync()
    {
        Console.WriteLine("\n=== Monadic Sync Operations ===");

        // Option-based sync operations
        var optionPipeline = SyncSteps.ParseInt.TryOption(n => n > 0);

        var optionResult1 = optionPipeline.Invoke("42");
        var optionResult2 = optionPipeline.Invoke("-5");
        var optionResult3 = optionPipeline.Invoke("not-a-number");

        Console.WriteLine($"Option parse '42': {optionResult1}");
        Console.WriteLine($"Option parse '-5': {optionResult2}");
        Console.WriteLine($"Option parse 'not-a-number': {optionResult3}");

        // Result-based error handling
        var safeParseAndFormat = SyncSteps.ParseInt
            .TrySync()
            .Map(result => result.Map(n => $"Parsed: {n}"));

        var safeResult1 = safeParseAndFormat.Invoke("123");
        var safeResult2 = safeParseAndFormat.Invoke("invalid");

        Console.WriteLine($"Safe result '123': {safeResult1}");
        Console.WriteLine($"Safe result 'invalid': {safeResult2}");
    }

    /// <summary>
    /// Demonstrate integration with contextual steps
    /// </summary>
    public static async Task DemonstrateContextualIntegration()
    {
        Console.WriteLine("\n=== Contextual Step Integration ===");

        // Create a context
        var context = new { Prefix = "Context", Multiplier = 3 };

        // Sync step that uses context
        var contextualSync = ContextualStep.LiftPure<string, string, object>(
            s => $"{context.Prefix}: {s}",
            "Applied context prefix");

        // Mixed contextual pipeline
        var contextualPipeline = contextualSync
            .Then(ContextualStep.FromPure<string, string, object>(AsyncSteps.NetworkCall, "Network call"));

        var (contextualResult, logs) = await contextualPipeline("contextual input", context);

        Console.WriteLine($"Contextual result: {contextualResult}");
        Console.WriteLine($"Logs: [{string.Join(", ", logs)}]");
    }

    /// <summary>
    /// Run all hybrid demonstrations
    /// </summary>
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
