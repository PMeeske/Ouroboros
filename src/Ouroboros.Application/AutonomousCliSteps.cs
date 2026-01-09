// <copyright file="AutonomousCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Application.Tools;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Pipeline;

namespace Ouroboros.Application;

/// <summary>
/// CLI pipeline steps for autonomous mode management.
/// </summary>
public static class AutonomousCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // STATUS & INFO
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the current autonomous mode status.
    /// </summary>
    [PipelineToken("AutonomousStatus")]
    public static async Task<string> AutonomousStatus()
    {
        var tool = new AutonomousTools.GetAutonomousStatusTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Gets the neural network status.
    /// </summary>
    [PipelineToken("NeuralNetworkStatus")]
    public static async Task<string> NeuralNetworkStatus()
    {
        var tool = new AutonomousTools.GetNetworkStatusTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INTENTION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lists pending intentions.
    /// </summary>
    [PipelineToken("ListIntentions")]
    public static async Task<string> ListIntentions()
    {
        var tool = new AutonomousTools.ListPendingIntentionsTool();
        var result = await tool.InvokeAsync("", CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Approves an intention by partial ID.
    /// </summary>
    /// <param name="partialId">First few characters of the intention ID.</param>
    /// <param name="comment">Optional approval comment.</param>
    [PipelineToken("ApproveIntention")]
    public static async Task<string> ApproveIntention(string partialId, string? comment = null)
    {
        var tool = new AutonomousTools.ApproveIntentionTool();
        var input = comment != null
            ? $"{{\"id\":\"{partialId}\",\"comment\":\"{comment}\"}}"
            : $"{{\"id\":\"{partialId}\"}}";
        var result = await tool.InvokeAsync(input, CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Rejects an intention by partial ID.
    /// </summary>
    /// <param name="partialId">First few characters of the intention ID.</param>
    /// <param name="reason">Optional rejection reason.</param>
    [PipelineToken("RejectIntention")]
    public static async Task<string> RejectIntention(string partialId, string? reason = null)
    {
        var tool = new AutonomousTools.RejectIntentionTool();
        var input = reason != null
            ? $"{{\"id\":\"{partialId}\",\"reason\":\"{reason}\"}}"
            : $"{{\"id\":\"{partialId}\"}}";
        var result = await tool.InvokeAsync(input, CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Proposes a new intention.
    /// </summary>
    /// <param name="title">Short title for the intention.</param>
    /// <param name="description">Description of what to do.</param>
    /// <param name="rationale">Why this is beneficial.</param>
    /// <param name="category">SelfReflection|CodeModification|Learning|UserCommunication|MemoryManagement|GoalPursuit</param>
    /// <param name="priority">Low|Normal|High|Critical</param>
    [PipelineToken("ProposeIntention")]
    public static async Task<string> ProposeIntention(
        string title,
        string description,
        string rationale,
        string category = "SelfReflection",
        string priority = "Normal")
    {
        var tool = new AutonomousTools.ProposeIntentionTool();
        var input = $"{{\"title\":\"{EscapeJson(title)}\",\"description\":\"{EscapeJson(description)}\",\"rationale\":\"{EscapeJson(rationale)}\",\"category\":\"{category}\",\"priority\":\"{priority}\"}}";
        var result = await tool.InvokeAsync(input, CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Approves all low-risk intentions automatically.
    /// </summary>
    [PipelineToken("ApproveAllSafe")]
    public static Task<string> ApproveAllSafe()
    {
        if (AutonomousTools.SharedCoordinator == null)
            return Task.FromResult("Error: Autonomous coordinator not initialized.");

        var count = AutonomousTools.SharedCoordinator.IntentionBus.ApproveAllLowRisk();
        return Task.FromResult($"✅ Auto-approved {count} low-risk intentions.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MODE CONTROL
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts autonomous mode.
    /// </summary>
    [PipelineToken("StartAutonomous")]
    public static async Task<string> StartAutonomous()
    {
        var tool = new AutonomousTools.ToggleAutonomousModeTool();
        var result = await tool.InvokeAsync("start", CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Stops autonomous mode.
    /// </summary>
    [PipelineToken("StopAutonomous")]
    public static async Task<string> StopAutonomous()
    {
        var tool = new AutonomousTools.ToggleAutonomousModeTool();
        var result = await tool.InvokeAsync("stop", CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GOALS & NEURON COMMUNICATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Injects a goal for autonomous pursuit.
    /// </summary>
    /// <param name="goal">The goal description.</param>
    /// <param name="priority">Low|Normal|High|Critical</param>
    [PipelineToken("SetGoal")]
    public static async Task<string> SetGoal(string goal, string priority = "Normal")
    {
        var tool = new AutonomousTools.InjectGoalTool();
        var input = $"{{\"goal\":\"{EscapeJson(goal)}\",\"priority\":\"{priority}\"}}";
        var result = await tool.InvokeAsync(input, CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Sends a message to a specific neuron.
    /// </summary>
    /// <param name="neuronId">The neuron ID (e.g., neuron.memory, neuron.code).</param>
    /// <param name="topic">The message topic.</param>
    /// <param name="payload">The message payload.</param>
    [PipelineToken("SendToNeuron")]
    public static async Task<string> SendToNeuron(string neuronId, string topic, string payload)
    {
        var tool = new AutonomousTools.SendNeuronMessageTool();
        var input = $"{{\"neuron_id\":\"{neuronId}\",\"topic\":\"{topic}\",\"payload\":\"{EscapeJson(payload)}\"}}";
        var result = await tool.InvokeAsync(input, CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    /// <summary>
    /// Searches recent neural network message history.
    /// </summary>
    /// <param name="query">The search query.</param>
    [PipelineToken("SearchNeuronHistory")]
    public static async Task<string> SearchNeuronHistory(string query)
    {
        var tool = new AutonomousTools.SearchNeuronHistoryTool();
        var result = await tool.InvokeAsync(query, CancellationToken.None);
        return result.Match(s => s, e => $"Error: {e}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DIRECT COORDINATOR ACCESS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or initializes the autonomous coordinator.
    /// </summary>
    /// <param name="pushBasedMode">Whether to enable push-based mode.</param>
    /// <param name="autoApproveSelfReflection">Whether to auto-approve self-reflection.</param>
    [PipelineToken("InitAutonomous")]
    public static Task<string> InitAutonomous(
        bool pushBasedMode = true,
        bool autoApproveSelfReflection = true)
    {
        if (AutonomousTools.SharedCoordinator != null)
            return Task.FromResult("Autonomous coordinator already initialized.");

        var config = new AutonomousConfiguration
        {
            PushBasedMode = pushBasedMode,
            AutoApproveSelfReflection = autoApproveSelfReflection,
            AutoApproveMemoryOps = true,
            AutoApproveLowRisk = false,
            EnableProactiveCommunication = true,
            EnableCodeModification = true,
        };

        AutonomousTools.SharedCoordinator = new AutonomousCoordinator(config);
        return Task.FromResult(
            $"✅ Autonomous coordinator initialized.\n" +
            $"   Push-based mode: {pushBasedMode}\n" +
            $"   Auto-approve reflection: {autoApproveSelfReflection}\n" +
            $"   Use StartAutonomous() to begin.");
    }

    /// <summary>
    /// Gets the intention bus summary.
    /// </summary>
    [PipelineToken("IntentionBusSummary")]
    public static Task<string> IntentionBusSummary()
    {
        if (AutonomousTools.SharedCoordinator == null)
            return Task.FromResult("Error: Autonomous coordinator not initialized.");

        return Task.FromResult(AutonomousTools.SharedCoordinator.IntentionBus.GetSummary());
    }

    /// <summary>
    /// Processes a command string (e.g., /approve, /reject, /intentions).
    /// </summary>
    /// <param name="command">The command string starting with /.</param>
    [PipelineToken("ProcessCommand")]
    public static Task<string> ProcessCommand(string command)
    {
        if (AutonomousTools.SharedCoordinator == null)
            return Task.FromResult("Error: Autonomous coordinator not initialized.");

        var processed = AutonomousTools.SharedCoordinator.ProcessCommand(command);
        return Task.FromResult(processed
            ? "Command processed successfully."
            : "Not a recognized command. Use /help for available commands.");
    }

    /// <summary>
    /// Shows help for autonomous commands.
    /// </summary>
    [PipelineToken("AutonomousHelp")]
    public static Task<string> AutonomousHelp()
    {
        return Task.FromResult("""
            🐍 **Ouroboros Autonomous Mode - CLI Commands**

            **Initialization:**
            • `InitAutonomous()` - Initialize the autonomous coordinator
            • `StartAutonomous()` - Start autonomous mode
            • `StopAutonomous()` - Stop autonomous mode

            **Status:**
            • `AutonomousStatus()` - Get overall status
            • `NeuralNetworkStatus()` - Get neural network status
            • `IntentionBusSummary()` - Get intention bus summary

            **Intention Management:**
            • `ListIntentions()` - List pending intentions
            • `ApproveIntention('id')` - Approve an intention
            • `RejectIntention('id', 'reason')` - Reject an intention
            • `ApproveAllSafe()` - Auto-approve low-risk intentions
            • `ProposeIntention(title, desc, rationale, category, priority)` - Create intention

            **Goals & Neurons:**
            • `SetGoal('goal', 'priority')` - Set an autonomous goal
            • `SendToNeuron('neuron.memory', 'topic', 'payload')` - Message a neuron
            • `SearchNeuronHistory('query')` - Search message history

            **Slash Commands (in ProcessCommand):**
            • `/approve <id>` - Approve intention
            • `/reject <id> [reason]` - Reject intention
            • `/intentions` - List pending
            • `/approve-all-safe` - Auto-approve safe
            • `/network` - Network status
            • `/help` - Show help
            """);
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}
