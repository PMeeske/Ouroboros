// <copyright file="AgentFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent;

using LangChainPipeline.Diagnostics;

/// <summary>
/// Simplified agent harness. The historical version orchestrated complex
/// tool-aware planning. For the restored build we provide a compact,
/// deterministic implementation that keeps the public surface intact while
/// remaining fully synchronous.
/// </summary>
public static class AgentFactory
{
    public static AgentInstance Create(
        string mode,
        IChatCompletionModel chatModel,
        ToolRegistry tools,
        bool debug,
        int maxSteps,
        bool ragEnabled,
        string embedModelName,
        bool jsonTools,
        bool stream)
    {
        return new AgentInstance(mode, chatModel, tools, maxSteps)
        {
            Debug = debug,
            RagEnabled = ragEnabled,
            EmbedModelName = embedModelName,
            JsonTools = jsonTools,
            Stream = stream,
        };
    }
}

public sealed class AgentInstance
{
    private readonly IChatCompletionModel chat;
    private readonly ToolRegistry tools;
    private readonly int maxSteps;

    internal AgentInstance(string mode, IChatCompletionModel chat, ToolRegistry tools, int maxSteps)
    {
        this.Mode = string.IsNullOrWhiteSpace(mode) ? "simple" : mode;
        this.chat = chat;
        this.tools = tools;
        this.maxSteps = Math.Max(1, maxSteps);
    }

    public string Mode { get; }

    public bool Debug { get; init; }

    public bool RagEnabled { get; init; }

    public string EmbedModelName { get; init; } = string.Empty;

    public bool JsonTools { get; init; }

    public bool Stream { get; init; }

    public async Task<string> RunAsync(string prompt, CancellationToken ct = default)
    {
        string current = prompt;
        var history = new List<string>();
        for (int i = 0; i < this.maxSteps; i++)
        {
            history.Add(current);
            Telemetry.RecordAgentIteration();
            string response = await this.chat.GenerateTextAsync(current, ct).ConfigureAwait(false);
            var (text, toolCalls) = await new ToolAwareChatModel(this.chat, this.tools).GenerateWithToolsAsync(response, ct).ConfigureAwait(false);
            foreach (var call in toolCalls)
            {
                Telemetry.RecordAgentToolCalls(1);
                Telemetry.RecordToolName(call.ToolName);
            }

            current = text;
            if (!current.Contains("[AGENT-CONTINUE]", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        Telemetry.RecordAgentRetry();
        return current;
    }
}
