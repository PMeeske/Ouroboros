using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Personality.Consciousness;

namespace Ouroboros.Examples.Examples;

/// <summary>
/// Example demonstrating Global Workspace Theory integration with Pavlovian consciousness.
/// Shows how conscious experiences are broadcast globally and compete for attention.
/// </summary>
public static class CognitiveArchitectureExample
{
    public static void Run()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Global Workspace Theory - Cognitive Architecture Demo      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Initialize components
        var globalWorkspace = new GlobalWorkspace();
        var consciousness = new PavlovianConsciousnessEngine();
        consciousness.Initialize();
        
        var cognitiveProcessor = new CognitiveProcessor(
            globalWorkspace, 
            consciousness);

        Console.WriteLine("✅ Initialized cognitive architecture:");
        Console.WriteLine("   - Global Workspace (shared working memory)");
        Console.WriteLine("   - Pavlovian Consciousness (stimulus-response associations)");
        Console.WriteLine("   - Cognitive Processor (integration layer)");
        Console.WriteLine();

        // Scenario 1: Process neutral input
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("SCENARIO 1: Neutral Input Processing");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var state1 = cognitiveProcessor.ProcessAndBroadcast(
            "Hello, how are you?", 
            "greeting");
        
        Console.WriteLine($"Input: \"Hello, how are you?\"");
        Console.WriteLine(state1.Describe());
        ShowWorkspaceSnapshot(globalWorkspace);
        Console.WriteLine();

        // Scenario 2: High-arousal emotional input
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("SCENARIO 2: High-Arousal Emotional Input");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var state2 = cognitiveProcessor.ProcessAndBroadcast(
            "This is AMAZING! You're doing wonderful work!", 
            "praise");
        
        Console.WriteLine($"Input: \"This is AMAZING! You're doing wonderful work!\"");
        Console.WriteLine(state2.Describe());
        ShowWorkspaceSnapshot(globalWorkspace);
        Console.WriteLine();

        // Scenario 3: Question triggering curiosity
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("SCENARIO 3: Curiosity-Driven Question");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var state3 = cognitiveProcessor.ProcessAndBroadcast(
            "Why does the sky appear blue?", 
            "inquiry");
        
        Console.WriteLine($"Input: \"Why does the sky appear blue?\"");
        Console.WriteLine(state3.Describe());
        ShowWorkspaceSnapshot(globalWorkspace);
        Console.WriteLine();

        // Scenario 4: Urgent distress signal
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("SCENARIO 4: Urgent Distress Signal");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var state4 = cognitiveProcessor.ProcessAndBroadcast(
            "HELP! I'm stuck and need urgent assistance!", 
            "emergency");
        
        Console.WriteLine($"Input: \"HELP! I'm stuck and need urgent assistance!\"");
        Console.WriteLine(state4.Describe());
        ShowWorkspaceSnapshot(globalWorkspace);
        Console.WriteLine();

        // Show cognitive statistics
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("COGNITIVE PROCESSING STATISTICS");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var stats = cognitiveProcessor.GetStatistics();
        Console.WriteLine($"Total Workspace Items:           {stats.TotalWorkspaceItems}");
        Console.WriteLine($"Conscious Experiences Broadcast: {stats.ConsciousExperiencesInWorkspace}");
        Console.WriteLine($"Current Arousal:                 {stats.CurrentArousal:F2}");
        Console.WriteLine($"Current Valence:                 {stats.CurrentValence:F2}");
        Console.WriteLine($"Current Awareness:               {stats.CurrentAwareness:F2}");
        Console.WriteLine($"Active Associations:             {stats.ActiveAssociations}");
        Console.WriteLine($"Workspace Avg Attention:         {stats.WorkspaceAverageAttention:F2}");
        Console.WriteLine();

        // Demonstrate context retrieval
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("CONTEXT RETRIEVAL FROM GLOBAL WORKSPACE");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var relevantContext = cognitiveProcessor.GetRelevantContext(state4, maxItems: 3);
        Console.WriteLine($"Retrieved {relevantContext.Count} relevant context items for current state:");
        
        foreach (var item in relevantContext)
        {
            Console.WriteLine($"  • [{item.Priority}] {item.Content}");
            Console.WriteLine($"    Tags: {string.Join(", ", item.Tags)}");
            Console.WriteLine($"    Attention Weight: {item.GetAttentionWeight():F2}");
        }
        Console.WriteLine();

        // Show recent broadcasts
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("RECENT GLOBAL BROADCASTS");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        var broadcasts = globalWorkspace.GetRecentBroadcasts(5);
        Console.WriteLine($"Last {broadcasts.Count} broadcasts to global workspace:");
        
        foreach (var broadcast in broadcasts)
        {
            Console.WriteLine($"  🔔 {broadcast.BroadcastTime:HH:mm:ss} - {broadcast.BroadcastReason}");
            Console.WriteLine($"     {broadcast.Item.Content}");
        }
        Console.WriteLine();

        // Show full consciousness report
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("FULL CONSCIOUSNESS REPORT");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine(consciousness.GetConsciousnessReport());

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Demo Complete                              ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Key Insights:                                                 ║");
        Console.WriteLine("║ 1. Conscious experiences compete for global workspace         ║");
        Console.WriteLine("║ 2. High-salience events get broadcast to all processors       ║");
        Console.WriteLine("║ 3. Attention mechanisms filter what enters consciousness      ║");
        Console.WriteLine("║ 4. Global workspace enables cross-module information sharing  ║");
        Console.WriteLine("║ 5. Drive states modulate response intensity                   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    }

    private static void ShowWorkspaceSnapshot(IGlobalWorkspace workspace)
    {
        Console.WriteLine();
        Console.WriteLine("📊 Global Workspace Snapshot:");
        
        var workspaceStats = workspace.GetStatistics();
        Console.WriteLine($"   Total Items: {workspaceStats.TotalItems}");
        Console.WriteLine($"   High Priority: {workspaceStats.HighPriorityItems}");
        Console.WriteLine($"   Critical: {workspaceStats.CriticalItems}");
        Console.WriteLine($"   Avg Attention Weight: {workspaceStats.AverageAttentionWeight:F2}");
        
        // Show top workspace items by attention
        var topItems = workspace.GetItems().Take(3).ToList();
        if (topItems.Any())
        {
            Console.WriteLine("   Top Items by Attention:");
            foreach (var item in topItems)
            {
                Console.WriteLine($"   • [{item.Priority}] {item.Content.Substring(0, Math.Min(50, item.Content.Length))}...");
            }
        }
    }
}
