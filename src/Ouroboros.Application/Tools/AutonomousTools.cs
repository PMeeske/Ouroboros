// <copyright file="AutonomousTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Ouroboros.Application.Services;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Tools;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Provides access to API keys from configuration or environment.
/// </summary>
public static class ApiKeyProvider
{
    private static IConfiguration? _configuration;

    /// <summary>
    /// Sets the configuration instance for API key resolution.
    /// </summary>
    public static void SetConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Gets an API key from configuration (user secrets) or environment variable.
    /// </summary>
    public static string? GetApiKey(string keyName)
    {
        // Try configuration first (includes user secrets)
        string? key = _configuration?[$"ApiKeys:{keyName}"];
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        // Fall back to environment variable (e.g., FIRECRAWL_API_KEY)
        string envVarName = $"{keyName.ToUpperInvariant()}_API_KEY";
        return Environment.GetEnvironmentVariable(envVarName);
    }
}

/// <summary>
/// Tools for autonomous mode and intention management.
/// </summary>
public static class AutonomousTools
{
    /// <summary>
    /// Shared autonomous coordinator reference.
    /// </summary>
    public static AutonomousCoordinator? SharedCoordinator { get; set; }

    /// <summary>
    /// Gets all autonomous tools.
    /// </summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        yield return new GetAutonomousStatusTool();
        yield return new ListPendingIntentionsTool();
        yield return new ApproveIntentionTool();
        yield return new RejectIntentionTool();
        yield return new ProposeIntentionTool();
        yield return new GetNetworkStatusTool();
        yield return new SendNeuronMessageTool();
        yield return new ToggleAutonomousModeTool();
        yield return new InjectGoalTool();
        yield return new SearchNeuronHistoryTool();
        yield return new FirecrawlScrapeTool();
        yield return new FirecrawlResearchTool();
        yield return new LocalWebScrapeTool();
        yield return new CliDslTool();

        // Limitation-busting tools
        yield return new VerifyClaimTool();
        yield return new ReasoningChainTool();
        yield return new EpisodicMemoryTool();
        yield return new ParallelToolsTool();
        yield return new CompressContextTool();
        yield return new ParallelMeTTaThinkTool();
        yield return new SelfDoubtTool();
        yield return new OuroborosMeTTaTool();
    }

    /// <summary>
    /// Adds autonomous tools to a registry.
    /// </summary>
    public static ToolRegistry WithAutonomousTools(this ToolRegistry registry)
    {
        foreach (var tool in GetAllTools())
        {
            registry = registry.WithTool(tool);
        }
        return registry;
    }

    /// <summary>
    /// Gets the current autonomous status.
    /// </summary>
    public class GetAutonomousStatusTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "autonomous_status";

        /// <inheritdoc/>
        public string Description => "Get my current autonomous mode status including pending intentions, neural network state, and configuration.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            return Task.FromResult(Result<string, string>.Success(SharedCoordinator.GetStatus()));
        }
    }

    /// <summary>
    /// Lists pending intentions awaiting approval.
    /// </summary>
    public class ListPendingIntentionsTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "list_my_intentions";

        /// <inheritdoc/>
        public string Description => "List my pending intentions that are waiting for user approval.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            var pending = SharedCoordinator.IntentionBus.GetPendingIntentions();

            if (pending.Count == 0)
                return Task.FromResult(Result<string, string>.Success("📭 No pending intentions."));

            var sb = new StringBuilder();
            sb.AppendLine($"📋 **{pending.Count} Pending Intention(s)**\n");

            foreach (var intention in pending)
            {
                var idShort = intention.Id.ToString()[..8];
                sb.AppendLine($"**{idShort}** | [{intention.Priority}] [{intention.Category}]");
                sb.AppendLine($"  📌 {intention.Title}");
                sb.AppendLine($"  📝 {intention.Description}");
                sb.AppendLine($"  💡 {intention.Rationale}");
                sb.AppendLine();
            }

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
    }

    /// <summary>
    /// Approves a pending intention.
    /// </summary>
    public class ApproveIntentionTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "approve_my_intention";

        /// <inheritdoc/>
        public string Description => "Approve one of my pending intentions so I can execute it. Input JSON: {\"id\": \"partial_id\", \"comment\": \"optional\"}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"comment":{"type":"string"}},"required":["id"]}""";

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var id = args.GetProperty("id").GetString() ?? "";
                var comment = args.TryGetProperty("comment", out var c) ? c.GetString() : null;

                var success = SharedCoordinator.IntentionBus.ApproveIntentionByPartialId(id, comment);

                return Task.FromResult(success
                    ? Result<string, string>.Success($"✅ Intention `{id}` approved and queued for execution.")
                    : Result<string, string>.Failure($"Could not find pending intention with ID starting with: {id}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to approve: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Rejects a pending intention.
    /// </summary>
    public class RejectIntentionTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "reject_my_intention";

        /// <inheritdoc/>
        public string Description => "Reject one of my pending intentions. Input JSON: {\"id\": \"partial_id\", \"reason\": \"optional\"}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"reason":{"type":"string"}},"required":["id"]}""";

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var id = args.GetProperty("id").GetString() ?? "";
                var reason = args.TryGetProperty("reason", out var r) ? r.GetString() : null;

                var success = SharedCoordinator.IntentionBus.RejectIntentionByPartialId(id, reason);

                return Task.FromResult(success
                    ? Result<string, string>.Success($"❌ Intention `{id}` rejected.")
                    : Result<string, string>.Failure($"Could not find pending intention with ID starting with: {id}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to reject: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Proposes a new intention.
    /// </summary>
    public class ProposeIntentionTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "propose_intention";

        /// <inheritdoc/>
        public string Description => """
            Propose a new intention for user approval. Input JSON:
            {
                "title": "Short title",
                "description": "What I want to do",
                "rationale": "Why this is beneficial",
                "category": "SelfReflection|CodeModification|Learning|UserCommunication|MemoryManagement|GoalPursuit",
                "priority": "Low|Normal|High|Critical"
            }
            """;

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"title":{"type":"string"},"description":{"type":"string"},"rationale":{"type":"string"},"category":{"type":"string"},"priority":{"type":"string"}},"required":["title","description","rationale","category"]}""";

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var title = args.GetProperty("title").GetString() ?? "";
                var description = args.GetProperty("description").GetString() ?? "";
                var rationale = args.GetProperty("rationale").GetString() ?? "";
                var categoryStr = args.GetProperty("category").GetString() ?? "SelfReflection";
                var priorityStr = args.TryGetProperty("priority", out var p) ? p.GetString() ?? "Normal" : "Normal";

                if (!Enum.TryParse<IntentionCategory>(categoryStr, true, out var category))
                    category = IntentionCategory.SelfReflection;

                if (!Enum.TryParse<IntentionPriority>(priorityStr, true, out var priority))
                    priority = IntentionPriority.Normal;

                var intention = SharedCoordinator.IntentionBus.ProposeIntention(
                    title, description, rationale, category, "self", null, priority);

                return Task.FromResult(Result<string, string>.Success(
                    $"📝 Intention proposed: **{title}**\n" +
                    $"ID: `{intention.Id.ToString()[..8]}`\n" +
                    $"Awaiting user approval."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to propose: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Gets neural network status.
    /// </summary>
    public class GetNetworkStatusTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "neural_network_status";

        /// <inheritdoc/>
        public string Description => "Get the status of my internal neural network including all active neurons.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            return Task.FromResult(Result<string, string>.Success(
                SharedCoordinator.Network.GetNetworkState()));
        }
    }

    /// <summary>
    /// Sends a message to a neuron.
    /// </summary>
    public class SendNeuronMessageTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "send_to_neuron";

        /// <inheritdoc/>
        public string Description => """
            Send a message to one of my internal neurons. Input JSON:
            {
                "neuron_id": "neuron.memory|neuron.code|neuron.symbolic|neuron.executive|...",
                "topic": "message.topic",
                "payload": "message content or JSON"
            }
            """;

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"neuron_id":{"type":"string"},"topic":{"type":"string"},"payload":{"type":"string"}},"required":["neuron_id","topic","payload"]}""";

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var neuronId = args.GetProperty("neuron_id").GetString() ?? "";
                var topic = args.GetProperty("topic").GetString() ?? "";
                var payload = args.GetProperty("payload").GetString() ?? "";

                SharedCoordinator.SendToNeuron(neuronId, topic, payload);

                return Task.FromResult(Result<string, string>.Success(
                    $"📤 Message sent to `{neuronId}` on topic `{topic}`"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to send: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Toggles autonomous mode.
    /// </summary>
    public class ToggleAutonomousModeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "toggle_autonomous";

        /// <inheritdoc/>
        public string Description => "Start or stop my autonomous mode. Input: 'start' or 'stop'.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Result<string, string>.Failure("Autonomous coordinator not initialized.");

            var action = input.Trim().ToLowerInvariant();

            if (action == "start")
            {
                if (SharedCoordinator.IsActive)
                    return Result<string, string>.Success("Autonomous mode is already active.");

                SharedCoordinator.Start();
                return Result<string, string>.Success("🟢 Autonomous mode started. I will now propose actions for your approval.");
            }
            else if (action == "stop")
            {
                if (!SharedCoordinator.IsActive)
                    return Result<string, string>.Success("Autonomous mode is already stopped.");

                await SharedCoordinator.StopAsync();
                return Result<string, string>.Success("🔴 Autonomous mode stopped. I will wait for your instructions.");
            }

            return Result<string, string>.Failure("Invalid action. Use 'start' or 'stop'.");
        }
    }

    /// <summary>
    /// Injects a goal for autonomous pursuit.
    /// </summary>
    public class InjectGoalTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "set_autonomous_goal";

        /// <inheritdoc/>
        public string Description => "Give me a goal to pursue autonomously. Input JSON: {\"goal\": \"description\", \"priority\": \"Low|Normal|High|Critical\"}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"goal":{"type":"string"},"priority":{"type":"string"}},"required":["goal"]}""";

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var goal = args.GetProperty("goal").GetString() ?? "";
                var priorityStr = args.TryGetProperty("priority", out var p) ? p.GetString() ?? "Normal" : "Normal";

                if (!Enum.TryParse<IntentionPriority>(priorityStr, true, out var priority))
                    priority = IntentionPriority.Normal;

                SharedCoordinator.InjectGoal(goal, priority);

                return Task.FromResult(Result<string, string>.Success(
                    $"🎯 Goal injected: {goal}\n" +
                    $"I will propose actions to work towards this goal."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to set goal: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Searches neuron message history.
    /// </summary>
    public class SearchNeuronHistoryTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "search_neuron_history";

        /// <inheritdoc/>
        public string Description => "Search my recent internal neural network message history. Input: search query.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedCoordinator == null)
                return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

            var query = input.Trim().ToLowerInvariant();
            var messages = SharedCoordinator.Network.GetRecentMessages(100);

            var matches = messages
                .Where(m => m.Topic.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            m.Payload.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                            m.SourceNeuron.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();

            if (matches.Count == 0)
                return Task.FromResult(Result<string, string>.Success($"No messages found matching: {query}"));

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 Found {matches.Count} matching messages:\n");

            foreach (var msg in matches)
            {
                sb.AppendLine($"**{msg.Topic}** from `{msg.SourceNeuron}`");
                sb.AppendLine($"  {msg.Payload.ToString()?[..Math.Min(100, msg.Payload.ToString()?.Length ?? 0)]}...");
            }

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
    }

    /// <summary>
    /// Scrapes a webpage using Firecrawl for clean content extraction.
    /// </summary>
    public class FirecrawlScrapeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "firecrawl_scrape";

        /// <inheritdoc/>
        public string Description => "Scrape a webpage using Firecrawl API for clean, structured content. Requires FIRECRAWL_API_KEY environment variable. Input: URL to scrape.";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"url":{"type":"string","description":"URL to scrape"}},"required":["url"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            string url = input.Trim();

            // Try to parse JSON input
            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("url", out var urlEl))
                    url = urlEl.GetString() ?? url;
            }
            catch { /* Use raw input as URL */ }

            if (string.IsNullOrWhiteSpace(url))
                return Result<string, string>.Failure("No URL provided. Usage: firecrawl_scrape <url>");

            // Get API key from configuration or environment
            string? apiKey = ApiKeyProvider.GetApiKey("Firecrawl");
            if (string.IsNullOrWhiteSpace(apiKey))
                return Result<string, string>.Failure("Firecrawl API key not configured. Set ApiKeys:Firecrawl in user secrets or FIRECRAWL_API_KEY environment variable.");

            try
            {
                var result = await FirecrawlScrapeInternalAsync(url, apiKey, ct);
                return Result<string, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Firecrawl scrape failed: {ex.Message}");
            }
        }

        private static async Task<string> FirecrawlScrapeInternalAsync(string url, string apiKey, CancellationToken ct)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            var requestBody = new Dictionary<string, object>
            {
                ["url"] = url,
                ["formats"] = new[] { "markdown" },
                ["onlyMainContent"] = true,
                ["blockAds"] = true,
                ["removeBase64Images"] = true
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.firecrawl.dev/v2/scrape");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Firecrawl API error: {response.StatusCode}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
                throw new InvalidOperationException("Firecrawl request failed");

            var data = root.GetProperty("data");
            var result = new StringBuilder();

            if (data.TryGetProperty("metadata", out var metadata))
            {
                if (metadata.TryGetProperty("title", out var titleEl))
                    result.AppendLine($"# {titleEl.GetString()}");
                if (metadata.TryGetProperty("sourceURL", out var srcEl))
                    result.AppendLine($"Source: {srcEl.GetString()}");
            }

            if (data.TryGetProperty("markdown", out var markdownEl))
            {
                result.AppendLine();
                var content = markdownEl.GetString() ?? "";
                result.AppendLine(content.Length > 30000 ? content[..30000] + "\n...[truncated]" : content);
            }

            return result.ToString();
        }
    }

    /// <summary>
    /// Research tool that searches and scrapes web content using Firecrawl API.
    /// </summary>
    public class FirecrawlResearchTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "web_research";

        /// <inheritdoc/>
        public string Description => "Deep web research using Firecrawl. PREFERRED for any web search or research task. Input: search query or URL to research.";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"query":{"type":"string","description":"Search query or URL to research"},"scrapeFirst":{"type":"boolean","description":"If true and input is a URL, scrape it directly"}},"required":["query"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            string query = input.Trim();

            // Try to parse JSON input
            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("query", out var queryEl))
                    query = queryEl.GetString() ?? query;
            }
            catch { /* Use raw input as query */ }

            if (string.IsNullOrWhiteSpace(query))
                return Result<string, string>.Failure("No query provided");

            // Get API key from configuration or environment
            string? apiKey = ApiKeyProvider.GetApiKey("Firecrawl");

            // Check if input is a URL - if so, scrape it
            if (Uri.TryCreate(query, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                return await ScrapeUrlAsync(query, apiKey, ct);
            }

            // It's a search query - use Firecrawl Search API
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var result = await FirecrawlSearchAsync(query, apiKey, ct);
                if (result.IsSuccess)
                    return result;

                // Firecrawl failed - fall back to free DuckDuckGo search
                return await DuckDuckGoFallbackSearchAsync(query, ct);
            }

            // No API key - use free DuckDuckGo search as fallback
            return await DuckDuckGoFallbackSearchAsync(query, ct);
        }

        private static async Task<Result<string, string>> DuckDuckGoFallbackSearchAsync(string query, CancellationToken ct)
        {
            try
            {
                using var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // Use DuckDuckGo HTML search (free, no API key needed)
                var encodedQuery = Uri.EscapeDataString(query);
                var response = await client.GetAsync($"https://html.duckduckgo.com/html/?q={encodedQuery}", ct);
                response.EnsureSuccessStatusCode();

                string html = await response.Content.ReadAsStringAsync(ct);

                // Parse results from HTML
                var results = new StringBuilder();

                // Extract result links and snippets using regex
                var linkRegex = new System.Text.RegularExpressions.Regex(
                    @"<a[^>]*class=""result__a""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var snippetRegex = new System.Text.RegularExpressions.Regex(
                    @"<a[^>]*class=""result__snippet""[^>]*>(.+?)</a>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                var linkMatches = linkRegex.Matches(html);
                var snippetMatches = snippetRegex.Matches(html);

                int count = 0;
                for (int i = 0; i < Math.Min(linkMatches.Count, 5); i++)
                {
                    var linkMatch = linkMatches[i];
                    string url = System.Net.WebUtility.HtmlDecode(linkMatch.Groups[1].Value);
                    string title = System.Net.WebUtility.HtmlDecode(linkMatch.Groups[2].Value);

                    // Skip DuckDuckGo internal links
                    if (url.Contains("duckduckgo.com") || string.IsNullOrWhiteSpace(title))
                        continue;

                    // Extract actual URL from DDG redirect
                    if (url.StartsWith("//duckduckgo.com/l/?uddg="))
                    {
                        var urlMatch = System.Text.RegularExpressions.Regex.Match(url, @"uddg=([^&]+)");
                        if (urlMatch.Success)
                            url = Uri.UnescapeDataString(urlMatch.Groups[1].Value);
                    }

                    count++;
                    string snippet = "";
                    if (i < snippetMatches.Count)
                    {
                        snippet = System.Net.WebUtility.HtmlDecode(
                            System.Text.RegularExpressions.Regex.Replace(snippetMatches[i].Groups[1].Value, @"<[^>]+>", "")).Trim();
                        if (snippet.Length > 100) snippet = snippet[..100] + "...";
                    }

                    results.AppendLine($"{count}. {title}");
                    if (!string.IsNullOrWhiteSpace(snippet))
                        results.AppendLine($"   {snippet}");
                }

                if (count == 0)
                    return Result<string, string>.Success($"No results for: {query}");

                return Result<string, string>.Success($"Search results for '{query}':\n{results}".Trim());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"DuckDuckGo fallback search failed: {ex.Message}");
            }
        }

        private static async Task<Result<string, string>> FirecrawlSearchAsync(string query, string apiKey, CancellationToken ct)
        {
            try
            {
                using var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Use Firecrawl's search endpoint
                var searchRequest = new
                {
                    query = query,
                    limit = 10,
                    scrapeOptions = new
                    {
                        formats = new[] { "markdown" },
                        onlyMainContent = true
                    }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(searchRequest),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync("https://api.firecrawl.dev/v1/search", jsonContent, ct);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync(ct);
                    return Result<string, string>.Failure($"Firecrawl search failed ({response.StatusCode}): {errorBody}");
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                var results = new StringBuilder();
                results.AppendLine($"# Search Results: {query}\n");

                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    int count = 0;
                    foreach (var item in dataEl.EnumerateArray())
                    {
                        count++;
                        string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        string url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        string description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        string markdown = item.TryGetProperty("markdown", out var m) ? m.GetString() ?? "" : "";

                        results.AppendLine($"## {count}. {title}");
                        results.AppendLine($"**URL:** {url}");
                        if (!string.IsNullOrWhiteSpace(description))
                            results.AppendLine($"**Description:** {description}");

                        // Include snippet of markdown content if available
                        if (!string.IsNullOrWhiteSpace(markdown))
                        {
                            string snippet = markdown.Length > 500 ? markdown[..500] + "..." : markdown;
                            results.AppendLine($"\n{snippet}");
                        }

                        results.AppendLine();
                    }

                    if (count == 0)
                        return Result<string, string>.Success($"No search results found for: {query}");
                }

                return Result<string, string>.Success(results.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Firecrawl search failed: {ex.Message}");
            }
        }

        private static async Task<Result<string, string>> ScrapeUrlAsync(string url, string? apiKey, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                // Use Firecrawl scrape API
                var scrapeTool = new FirecrawlScrapeTool();
                return await scrapeTool.InvokeAsync(url, ct);
            }

            // Fallback to basic fetch with decompression
            try
            {
                using var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync(ct);

                // Basic HTML stripping
                content = System.Text.RegularExpressions.Regex.Replace(content, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                content = System.Text.RegularExpressions.Regex.Replace(content, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ");
                content = System.Net.WebUtility.HtmlDecode(content);
                content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();

                return Result<string, string>.Success(content.Length > 20000 ? content[..20000] + "...[truncated]" : content);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to fetch URL: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Local web scraping tool that extracts clean content without external APIs.
    /// Provides Firecrawl-like functionality using local HTML parsing.
    /// </summary>
    public class LocalWebScrapeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "web_scrape";

        /// <inheritdoc/>
        public string Description => "Scrape a webpage locally and extract clean, readable content. No API key required. Input: URL to scrape.";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"url":{"type":"string","description":"URL to scrape"},"includeLinks":{"type":"boolean","description":"Include extracted links in output"},"maxLength":{"type":"integer","description":"Max content length (default 15000)"}},"required":["url"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            string url = input.Trim();
            bool includeLinks = false;
            int maxLength = 15000;

            // Try to parse JSON input
            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("url", out var urlEl))
                    url = urlEl.GetString() ?? url;
                if (doc.RootElement.TryGetProperty("includeLinks", out var linksEl))
                    includeLinks = linksEl.GetBoolean();
                if (doc.RootElement.TryGetProperty("maxLength", out var lengthEl))
                    maxLength = lengthEl.GetInt32();
            }
            catch { /* Use raw input as URL */ }

            if (string.IsNullOrWhiteSpace(url))
                return Result<string, string>.Failure("No URL provided. Usage: web_scrape <url>");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                return Result<string, string>.Failure($"Invalid URL: {url}. Must be http or https.");

            try
            {
                return await ScrapeLocallyAsync(url, includeLinks, maxLength, ct);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Scrape failed: {ex.Message}");
            }
        }

        private static async Task<Result<string, string>> ScrapeLocallyAsync(
            string url, bool includeLinks, int maxLength, CancellationToken ct)
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };

            // Human-like headers
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync(ct);
            var result = new StringBuilder();

            // Extract metadata
            string? title = ExtractMetaContent(html, @"<title[^>]*>([^<]+)</title>");
            string? description = ExtractMetaContent(html, @"<meta[^>]*name=[""']description[""'][^>]*content=[""']([^""']+)[""']")
                               ?? ExtractMetaContent(html, @"<meta[^>]*content=[""']([^""']+)[""'][^>]*name=[""']description[""']");
            string? ogTitle = ExtractMetaContent(html, @"<meta[^>]*property=[""']og:title[""'][^>]*content=[""']([^""']+)[""']");
            string? author = ExtractMetaContent(html, @"<meta[^>]*name=[""']author[""'][^>]*content=[""']([^""']+)[""']");

            // Build header
            result.AppendLine($"# {System.Net.WebUtility.HtmlDecode(ogTitle ?? title ?? "Untitled")}\n");
            result.AppendLine($"**Source:** {url}");
            if (!string.IsNullOrWhiteSpace(author))
                result.AppendLine($"**Author:** {author}");
            if (!string.IsNullOrWhiteSpace(description))
                result.AppendLine($"**Description:** {System.Net.WebUtility.HtmlDecode(description)}");
            result.AppendLine();

            // Extract main content
            string content = ExtractMainContent(html);

            // Clean up content
            content = CleanContent(content);

            // Truncate if needed
            if (content.Length > maxLength)
                content = content[..maxLength] + "\n\n...[content truncated]";

            result.AppendLine("---\n");
            result.AppendLine(content);

            // Extract links if requested
            if (includeLinks)
            {
                var links = ExtractLinks(html, url);
                if (links.Count > 0)
                {
                    result.AppendLine("\n---\n## Extracted Links\n");
                    foreach (var (linkText, linkUrl) in links.Take(20))
                    {
                        result.AppendLine($"- [{linkText}]({linkUrl})");
                    }
                }
            }

            return Result<string, string>.Success(result.ToString());
        }

        private static string? ExtractMetaContent(string html, string pattern)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ExtractMainContent(string html)
        {
            // Try to find main content areas
            string content = html;

            // Remove non-content elements first
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<noscript[^>]*>[\s\S]*?</noscript>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<nav[^>]*>[\s\S]*?</nav>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<header[^>]*>[\s\S]*?</header>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<footer[^>]*>[\s\S]*?</footer>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<aside[^>]*>[\s\S]*?</aside>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<!--[\s\S]*?-->", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Try to extract article or main content
            var articleMatch = System.Text.RegularExpressions.Regex.Match(content,
                @"<article[^>]*>([\s\S]*?)</article>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (articleMatch.Success && articleMatch.Groups[1].Value.Length > 500)
                content = articleMatch.Groups[1].Value;
            else
            {
                var mainMatch = System.Text.RegularExpressions.Regex.Match(content,
                    @"<main[^>]*>([\s\S]*?)</main>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (mainMatch.Success && mainMatch.Groups[1].Value.Length > 500)
                    content = mainMatch.Groups[1].Value;
            }

            // Convert common elements to markdown-style formatting
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<h1[^>]*>([^<]+)</h1>", "\n# $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<h2[^>]*>([^<]+)</h2>", "\n## $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<h3[^>]*>([^<]+)</h3>", "\n### $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<h[456][^>]*>([^<]+)</h[456]>", "\n**$1**\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<li[^>]*>", "\n• ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<p[^>]*>", "\n\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<strong[^>]*>([^<]+)</strong>", "**$1**", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<b[^>]*>([^<]+)</b>", "**$1**", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<em[^>]*>([^<]+)</em>", "*$1*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<i[^>]*>([^<]+)</i>", "*$1*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<code[^>]*>([^<]+)</code>", "`$1`", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"<blockquote[^>]*>([^<]+)</blockquote>", "\n> $1\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Strip remaining tags
            content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ");

            return content;
        }

        private static string CleanContent(string content)
        {
            // Decode HTML entities
            content = System.Net.WebUtility.HtmlDecode(content);

            // Normalize whitespace
            content = System.Text.RegularExpressions.Regex.Replace(content, @"[ \t]+", " ");
            content = System.Text.RegularExpressions.Regex.Replace(content, @"\n{3,}", "\n\n");

            // Remove leading/trailing whitespace from lines
            var lines = content.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) || l == "");

            return string.Join("\n", lines).Trim();
        }

        private static List<(string Text, string Url)> ExtractLinks(string html, string baseUrl)
        {
            var links = new List<(string, string)>();
            var baseUri = new Uri(baseUrl);

            var linkRegex = new System.Text.RegularExpressions.Regex(
                @"<a[^>]*href=[""']([^""']+)[""'][^>]*>([^<]*)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in linkRegex.Matches(html))
            {
                string href = match.Groups[1].Value;
                string text = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value).Trim();

                if (string.IsNullOrWhiteSpace(text) || text.Length > 100)
                    continue;

                // Skip internal anchors, javascript, mailto
                if (href.StartsWith("#") || href.StartsWith("javascript:") || href.StartsWith("mailto:"))
                    continue;

                // Resolve relative URLs
                try
                {
                    var fullUrl = new Uri(baseUri, href).ToString();
                    if (!links.Any(l => l.Item2 == fullUrl))
                        links.Add((text, fullUrl));
                }
                catch { /* Invalid URL */ }
            }

            return links;
        }
    }

    /// <summary>
    /// Tool for executing CLI DSL pipeline expressions.
    /// Provides access to the Ouroboros pipeline DSL for reasoning, ingestion, and processing.
    /// </summary>
    public class CliDslTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "cli_dsl";

        /// <inheritdoc/>
        public string Description => "Execute a CLI DSL pipeline expression. " +
            "Available commands: SetTopic('x'), SetPrompt('x'), UseDraft, UseCritique, UseImprove, " +
            "UseRefinementLoop, MeTTaAtom('x'), MeTTaQuery('x'), and more. " +
            "Chain with | operator. Example: SetTopic('AI') | UseDraft | UseCritique";

        /// <inheritdoc/>
        public string? JsonSchema => """
{
  "type": "object",
  "properties": {
    "dsl": {
      "type": "string",
      "description": "The DSL pipeline expression to execute"
    },
    "explain": {
      "type": "boolean",
      "description": "If true, explain the pipeline without executing"
    },
    "list": {
      "type": "boolean",
      "description": "If true, list all available DSL tokens"
    }
  },
  "required": ["dsl"]
}
""";

        // Shared state for pipeline continuity (injected by OuroborosAgent)
        /// <summary>
        /// The shared CLI pipeline state. Must be set before execution for full functionality.
        /// </summary>
        public static CliPipelineState? SharedState { get; set; }

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            string dsl = input.Trim();
            bool explain = false;
            bool list = false;

            // Try to parse JSON input
            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("dsl", out var dslEl))
                    dsl = dslEl.GetString() ?? dsl;
                if (doc.RootElement.TryGetProperty("explain", out var explainEl))
                    explain = explainEl.GetBoolean();
                if (doc.RootElement.TryGetProperty("list", out var listEl))
                    list = listEl.GetBoolean();
            }
            catch { /* Use raw input as DSL */ }

            // Handle list request
            if (list || dsl.Equals("list", StringComparison.OrdinalIgnoreCase) ||
                dsl.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                return Result<string, string>.Success(GetAvailableTokens());
            }

            // Handle explain request
            if (explain || dsl.StartsWith("explain ", StringComparison.OrdinalIgnoreCase))
            {
                string toExplain = explain ? dsl : dsl.Substring(8).Trim();
                string explanation = PipelineDsl.Explain(toExplain);
                return Result<string, string>.Success(explanation);
            }

            if (string.IsNullOrWhiteSpace(dsl))
                return Result<string, string>.Failure("No DSL expression provided. Use 'list' to see available tokens.");

            // Check if we have a shared state
            if (SharedState == null)
            {
                // No state available - just explain what would happen
                string explanation = PipelineDsl.Explain(dsl);
                return Result<string, string>.Success(
                    $"Pipeline explained (no execution context):\n\n{explanation}\n\n" +
                    "Note: Full execution requires an active Ouroboros session with LLM connected.");
            }

            try
            {
                // Build and execute the pipeline
                var step = PipelineDsl.Build(dsl);
                var state = await step(SharedState);
                SharedState = state;

                // Build result summary
                var result = new StringBuilder();
                result.AppendLine($"✓ Executed: `{dsl}`\n");

                // Show any reasoning output
                var reasoningSteps = state.Branch.Events
                    .OfType<ReasoningStep>()
                    .TakeLast(3);

                foreach (var rs in reasoningSteps)
                {
                    result.AppendLine($"**{rs.Kind}:**");
                    string content = rs.State?.Text ?? "";
                    if (content.Length > 500)
                        content = content[..500] + "...";
                    result.AppendLine(content);
                    result.AppendLine();
                }

                // Show current state info
                if (!string.IsNullOrWhiteSpace(state.Topic))
                    result.AppendLine($"**Topic:** {state.Topic}");
                if (!string.IsNullOrWhiteSpace(state.Prompt))
                    result.AppendLine($"**Prompt:** {state.Prompt}");
                if (!string.IsNullOrWhiteSpace(state.Output))
                {
                    string output = state.Output.Length > 1000 ? state.Output[..1000] + "..." : state.Output;
                    result.AppendLine($"\n**Output:**\n{output}");
                }

                // Show recent event count
                int eventCount = state.Branch.Events.Count();
                result.AppendLine($"\n**Pipeline events:** {eventCount}");

                return Result<string, string>.Success(result.ToString().Trim());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"DSL execution failed: {ex.Message}");
            }
        }

        private static string GetAvailableTokens()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Available CLI DSL Tokens\n");

            sb.AppendLine("## Core Operations");
            sb.AppendLine("- `SetTopic('x')` - Set the current topic");
            sb.AppendLine("- `SetPrompt('x')` - Set the prompt text");
            sb.AppendLine("- `SetQuery('x')` - Set the search query");
            sb.AppendLine("- `SetSource('path')` - Set source directory");
            sb.AppendLine();

            sb.AppendLine("## Reasoning Pipeline");
            sb.AppendLine("- `UseDraft` - Generate initial draft");
            sb.AppendLine("- `UseCritique` - Critique the current draft");
            sb.AppendLine("- `UseImprove` / `UseFinal` - Improve based on critique");
            sb.AppendLine("- `UseRefinementLoop` - Full draft-critique-improve cycle");
            sb.AppendLine("- `UseSelfCritique` - Self-critique reasoning");
            sb.AppendLine("- `UseStreamingDraft` - Streaming draft generation");
            sb.AppendLine("- `UseStreamingSelfCritique` - Streaming self-critique");
            sb.AppendLine();

            sb.AppendLine("## Ingestion");
            sb.AppendLine("- `UseDir('path')` - Ingest directory contents");
            sb.AppendLine("- `ReadFile('path')` - Read file content");
            sb.AppendLine();

            sb.AppendLine("## MeTTa Knowledge Base");
            sb.AppendLine("- `MeTTaAtom('x')` - Create an atom");
            sb.AppendLine("- `MeTTaFact('x')` - Assert a fact");
            sb.AppendLine("- `MeTTaRule('x')` - Define a rule");
            sb.AppendLine("- `MeTTaQuery('x')` - Query the KB");
            sb.AppendLine("- `MeTTaConcept('x')` - Create a concept");
            sb.AppendLine("- `MeTTaLink('x y')` - Link atoms");
            sb.AppendLine("- `MeTTaIntrospect` - Show KB status");
            sb.AppendLine("- `MeTTaReset` - Clear the KB");
            sb.AppendLine();

            sb.AppendLine("## Examples");
            sb.AppendLine("```");
            sb.AppendLine("SetTopic('functional programming') | UseDraft | UseCritique");
            sb.AppendLine("SetPrompt('Explain monads') | UseRefinementLoop");
            sb.AppendLine("MeTTaAtom('concept1') | MeTTaAtom('concept2') | MeTTaLink('concept1 concept2')");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("Chain commands with `|` operator.");

            return sb.ToString();
        }

        /// <summary>
        /// Resets the shared pipeline state.
        /// </summary>
        public static void ResetState()
        {
            SharedState = null;
        }
    }

    /// <summary>
    /// Verification tool for fact-checking and reducing hallucinations.
    /// Cross-references claims against multiple sources.
    /// </summary>
    public class VerifyClaimTool : ITool
    {
        public string Name => "verify_claim";
        public string Description => "Verify a claim or fact by cross-referencing multiple sources. Reduces hallucination risk. Input: JSON {\"claim\":\"...\", \"depth\":\"quick|thorough\"}";
        public string? JsonSchema => null;

        /// <summary>
        /// Delegate for web search function.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? SearchFunction { get; set; }

        /// <summary>
        /// Delegate for LLM evaluation function.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? EvaluateFunction { get; set; }

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var claim = doc.RootElement.GetProperty("claim").GetString() ?? "";
                var depth = doc.RootElement.TryGetProperty("depth", out var d) ? d.GetString() ?? "quick" : "quick";

                if (string.IsNullOrWhiteSpace(claim))
                    return Core.Monads.Result<string, string>.Failure("Claim is required.");

                var sb = new StringBuilder();
                sb.AppendLine($"🔍 **Verification Report**");
                sb.AppendLine($"**Claim:** {claim}");
                sb.AppendLine();

                var evidence = new List<(string source, string content, double confidence)>();

                // Search for supporting/contradicting evidence
                if (SearchFunction != null)
                {
                    var searchQueries = new[] { claim, $"is it true that {claim}", $"{claim} fact check" };
                    var searchTasks = depth == "thorough"
                        ? searchQueries.Select(q => SearchFunction(q, ct))
                        : new[] { SearchFunction(searchQueries[0], ct) };

                    var results = await Task.WhenAll(searchTasks);

                    for (int i = 0; i < results.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(results[i]))
                        {
                            evidence.Add(($"Search {i + 1}", results[i].Substring(0, Math.Min(500, results[i].Length)), 0.5));
                        }
                    }
                }

                // Use LLM to evaluate evidence
                if (EvaluateFunction != null && evidence.Count > 0)
                {
                    var evalPrompt = new StringBuilder();
                    evalPrompt.AppendLine("Evaluate this claim against the evidence. Be critical and skeptical.");
                    evalPrompt.AppendLine($"CLAIM: {claim}");
                    evalPrompt.AppendLine("\nEVIDENCE:");
                    foreach (var (source, content, _) in evidence)
                    {
                        evalPrompt.AppendLine($"[{source}]: {content}");
                    }
                    evalPrompt.AppendLine("\nRespond with:");
                    evalPrompt.AppendLine("VERDICT: SUPPORTED/CONTRADICTED/UNCERTAIN/NEEDS_CONTEXT");
                    evalPrompt.AppendLine("CONFIDENCE: 0-100%");
                    evalPrompt.AppendLine("REASONING: Brief explanation");
                    evalPrompt.AppendLine("CAVEATS: Any important qualifications");

                    var evaluation = await EvaluateFunction(evalPrompt.ToString(), ct);
                    sb.AppendLine("**Analysis:**");
                    sb.AppendLine(evaluation);
                }
                else if (evidence.Count == 0)
                {
                    sb.AppendLine("⚠️ **No external evidence found.** Unable to verify.");
                    sb.AppendLine("Consider this claim unverified. Treat with appropriate skepticism.");
                }
                else
                {
                    sb.AppendLine("**Raw Evidence:**");
                    foreach (var (source, content, _) in evidence.Take(3))
                    {
                        sb.AppendLine($"• [{source}]: {content.Substring(0, Math.Min(100, content.Length))}...");
                    }
                }

                return Core.Monads.Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Verification failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Structured reasoning chain tool that enforces step-by-step logic.
    /// Prevents pattern-matching shortcuts by requiring explicit derivation steps.
    /// </summary>
    public class ReasoningChainTool : ITool
    {
        public string Name => "reasoning_chain";
        public string Description => "Execute structured step-by-step reasoning. Enforces logical derivation instead of pattern matching. Input: JSON {\"problem\":\"...\", \"mode\":\"deductive|inductive|abductive\"}";
        public string? JsonSchema => null;

        /// <summary>
        /// Delegate for LLM reasoning function.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? ReasonFunction { get; set; }

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var problem = doc.RootElement.GetProperty("problem").GetString() ?? "";
                var mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "deductive" : "deductive";

                if (string.IsNullOrWhiteSpace(problem))
                    return Core.Monads.Result<string, string>.Failure("Problem is required.");

                var sb = new StringBuilder();
                sb.AppendLine($"🔗 **Reasoning Chain** ({mode})");
                sb.AppendLine($"**Problem:** {problem}");
                sb.AppendLine();

                if (ReasonFunction == null)
                    return Core.Monads.Result<string, string>.Failure("Reasoning function not available.");

                // Multi-step structured reasoning
                var steps = new List<(string step, string result)>();

                // Step 1: Decompose
                var decomposePrompt = $"DECOMPOSITION STEP: Break down this problem into 2-4 sub-questions that must be answered to solve it.\nPROBLEM: {problem}\n\nList each sub-question on its own line, numbered 1-4.";
                var decomposed = await ReasonFunction(decomposePrompt, ct);
                steps.Add(("Decomposition", decomposed));

                // Step 2: For each sub-question, derive
                var derivePrompt = mode switch
                {
                    "deductive" => $"DEDUCTIVE REASONING: Starting from known facts and rules, derive conclusions for:\n{decomposed}\n\nFor each sub-question:\n1. State the relevant facts/axioms\n2. Apply logical rules\n3. State the derived conclusion\n\nShow your work explicitly.",
                    "inductive" => $"INDUCTIVE REASONING: From the patterns and examples available, generalize:\n{decomposed}\n\nFor each sub-question:\n1. List relevant examples/observations\n2. Identify the pattern\n3. State the generalized principle\n\nShow your work explicitly.",
                    "abductive" => $"ABDUCTIVE REASONING: Find the best explanation for:\n{decomposed}\n\nFor each sub-question:\n1. List possible explanations\n2. Evaluate plausibility of each\n3. Select the most likely explanation\n\nShow your work explicitly.",
                    _ => $"REASONING: Answer these sub-questions systematically:\n{decomposed}\n\nShow your work explicitly."
                };
                var derived = await ReasonFunction(derivePrompt, ct);
                steps.Add(("Derivation", derived));

                // Step 3: Synthesize
                var synthesizePrompt = $"SYNTHESIS STEP: Combine the derived answers into a final solution.\n\nORIGINAL PROBLEM: {problem}\n\nDERIVED CONCLUSIONS:\n{derived}\n\nProvide:\n1. ANSWER: The direct answer to the problem\n2. CONFIDENCE: How certain (0-100%)\n3. LIMITATIONS: What assumptions were made or what could be wrong";
                var synthesis = await ReasonFunction(synthesizePrompt, ct);
                steps.Add(("Synthesis", synthesis));

                // Format output
                foreach (var (step, result) in steps)
                {
                    sb.AppendLine($"### {step}");
                    sb.AppendLine(result);
                    sb.AppendLine();
                }

                return Core.Monads.Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Reasoning chain failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Episodic memory tool that tags memories with experiential metadata.
    /// Creates richer memory context than simple content recall.
    /// </summary>
    public class EpisodicMemoryTool : ITool
    {
        public string Name => "episodic_memory";
        public string Description => "Store or recall episodic memories with emotional/experiential context. Input: JSON {\"action\":\"store|recall|consolidate\", \"content\":\"...\", \"emotion\":\"...\", \"significance\":0-1}";
        public string? JsonSchema => null;

        private static readonly List<EpisodicMemoryEntry> _memories = [];
        private static readonly object _lock = new();

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            try
            {
                using var doc = JsonDocument.Parse(input);
                var action = doc.RootElement.GetProperty("action").GetString() ?? "recall";

                return action.ToLowerInvariant() switch
                {
                    "store" => StoreMemory(doc.RootElement),
                    "recall" => RecallMemories(doc.RootElement),
                    "consolidate" => ConsolidateMemories(),
                    _ => Core.Monads.Result<string, string>.Failure($"Unknown action: {action}")
                };
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Episodic memory error: {ex.Message}");
            }
        }

        private static Core.Monads.Result<string, string> StoreMemory(JsonElement root)
        {
            var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            var emotion = root.TryGetProperty("emotion", out var e) ? e.GetString() ?? "neutral" : "neutral";
            var significance = root.TryGetProperty("significance", out var s) ? s.GetDouble() : 0.5;

            if (string.IsNullOrWhiteSpace(content))
                return Core.Monads.Result<string, string>.Failure("Content is required.");

            var entry = new EpisodicMemoryEntry
            {
                Id = Guid.NewGuid(),
                Content = content,
                Emotion = emotion,
                Significance = Math.Clamp(significance, 0, 1),
                Timestamp = DateTime.UtcNow,
                RecallCount = 0,
                LastRecalled = null,
            };

            lock (_lock)
            {
                _memories.Add(entry);

                // Limit to 200 memories, removing least significant when full
                if (_memories.Count > 200)
                {
                    var toRemove = _memories.OrderBy(m => m.Significance * (1 + m.RecallCount * 0.1)).First();
                    _memories.Remove(toRemove);
                }
            }

            return Core.Monads.Result<string, string>.Success($"✅ Memory stored (significance: {significance:P0}, emotion: {emotion})");
        }

        private static Core.Monads.Result<string, string> RecallMemories(JsonElement root)
        {
            var query = root.TryGetProperty("content", out var q) ? q.GetString() ?? "" : "";
            var emotion = root.TryGetProperty("emotion", out var e) ? e.GetString() : null;
            var count = root.TryGetProperty("count", out var n) ? n.GetInt32() : 5;

            lock (_lock)
            {
                var candidates = _memories.AsEnumerable();

                // Filter by emotion if specified
                if (!string.IsNullOrEmpty(emotion))
                {
                    candidates = candidates.Where(m => m.Emotion.Contains(emotion, StringComparison.OrdinalIgnoreCase));
                }

                // Score by relevance (simple keyword matching + significance + recency)
                var scored = candidates.Select(m =>
                {
                    var relevance = string.IsNullOrEmpty(query) ? 0.5 :
                        query.Split(' ').Count(word => m.Content.Contains(word, StringComparison.OrdinalIgnoreCase)) / (double)Math.Max(1, query.Split(' ').Length);
                    var recency = 1.0 / (1.0 + (DateTime.UtcNow - m.Timestamp).TotalHours);
                    var score = (relevance * 0.4) + (m.Significance * 0.3) + (recency * 0.3);
                    return (memory: m, score);
                })
                .OrderByDescending(x => x.score)
                .Take(count)
                .ToList();

                if (scored.Count == 0)
                    return Core.Monads.Result<string, string>.Success("_No episodic memories found matching criteria._");

                var sb = new StringBuilder();
                sb.AppendLine("📖 **Episodic Memory Recall**\n");

                foreach (var (memory, score) in scored)
                {
                    memory.RecallCount++;
                    memory.LastRecalled = DateTime.UtcNow;

                    var age = DateTime.UtcNow - memory.Timestamp;
                    var ageStr = age.TotalHours < 1 ? "just now" :
                                 age.TotalHours < 24 ? $"{(int)age.TotalHours}h ago" :
                                 $"{(int)age.TotalDays}d ago";

                    sb.AppendLine($"• **{ageStr}** [{memory.Emotion}] (significance: {memory.Significance:P0})");
                    sb.AppendLine($"  {memory.Content.Substring(0, Math.Min(150, memory.Content.Length))}");
                    sb.AppendLine();
                }

                return Core.Monads.Result<string, string>.Success(sb.ToString());
            }
        }

        private static Core.Monads.Result<string, string> ConsolidateMemories()
        {
            lock (_lock)
            {
                var before = _memories.Count;

                // Boost significance of frequently recalled memories
                foreach (var m in _memories.Where(m => m.RecallCount > 3))
                {
                    m.Significance = Math.Min(1.0, m.Significance + 0.1);
                }

                // Decay significance of old, unrecalled memories
                foreach (var m in _memories.Where(m => m.RecallCount == 0 && (DateTime.UtcNow - m.Timestamp).TotalDays > 1))
                {
                    m.Significance = Math.Max(0.1, m.Significance - 0.1);
                }

                // Remove very low significance memories
                _memories.RemoveAll(m => m.Significance < 0.15);

                var after = _memories.Count;
                var consolidated = before - after;

                return Core.Monads.Result<string, string>.Success($"🧠 Memory consolidation complete. {consolidated} memories faded, {after} retained.");
            }
        }

        /// <summary>
        /// Gets all memories for persistence.
        /// </summary>
        public static List<EpisodicMemoryEntry> GetAllMemories()
        {
            lock (_lock)
            {
                return [.. _memories];
            }
        }

        /// <summary>
        /// Loads memories from persistence.
        /// </summary>
        public static void LoadMemories(IEnumerable<EpisodicMemoryEntry> memories)
        {
            lock (_lock)
            {
                _memories.Clear();
                _memories.AddRange(memories);
            }
        }
    }

    /// <summary>
    /// Represents an episodic memory entry with experiential metadata.
    /// </summary>
    public class EpisodicMemoryEntry
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = "";
        public string Emotion { get; set; } = "neutral";
        public double Significance { get; set; }
        public DateTime Timestamp { get; set; }
        public int RecallCount { get; set; }
        public DateTime? LastRecalled { get; set; }
    }

    /// <summary>
    /// Parallel tool executor that runs multiple tools concurrently.
    /// Overcomes sequential execution limitation.
    /// </summary>
    public class ParallelToolsTool : ITool
    {
        public string Name => "parallel_tools";
        public string Description => "Execute multiple tools in parallel. Overcomes sequential execution limit. Input: JSON {\"tools\":[{\"name\":\"...\",\"input\":\"...\"},...]]}";
        public string? JsonSchema => null;

        /// <summary>
        /// Delegate for executing a tool by name.
        /// </summary>
        public static Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var toolsArray = doc.RootElement.GetProperty("tools");

                if (ExecuteToolFunction == null)
                    return Core.Monads.Result<string, string>.Failure("Tool execution function not available.");

                var toolCalls = new List<(string name, string input)>();

                foreach (var tool in toolsArray.EnumerateArray())
                {
                    var name = tool.GetProperty("name").GetString() ?? "";
                    var toolInput = tool.TryGetProperty("input", out var inp) ? inp.ToString() : "{}";
                    toolCalls.Add((name, toolInput));
                }

                if (toolCalls.Count == 0)
                    return Core.Monads.Result<string, string>.Failure("No tools specified.");

                if (toolCalls.Count > 10)
                    return Core.Monads.Result<string, string>.Failure("Maximum 10 parallel tools allowed.");

                // Execute all tools in parallel
                var tasks = toolCalls.Select(async tc =>
                {
                    try
                    {
                        var result = await ExecuteToolFunction(tc.name, tc.input, ct);
                        return (tc.name, success: true, result);
                    }
                    catch (Exception ex)
                    {
                        return (tc.name, success: false, result: ex.Message);
                    }
                });

                var results = await Task.WhenAll(tasks);

                var sb = new StringBuilder();
                sb.AppendLine($"⚡ **Parallel Execution Complete** ({results.Length} tools)\n");

                foreach (var (name, success, result) in results)
                {
                    sb.AppendLine($"### {name} {(success ? "✅" : "❌")}");
                    sb.AppendLine(result.Substring(0, Math.Min(300, result.Length)));
                    sb.AppendLine();
                }

                return Core.Monads.Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Parallel execution failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Context compression tool that summarizes long contexts.
    /// Addresses context window limitations.
    /// </summary>
    public class CompressContextTool : ITool
    {
        public string Name => "compress_context";
        public string Description => "Compress long context into essential summary. Overcomes context window limits. Input: JSON {\"content\":\"...\", \"target_tokens\":500, \"preserve\":[\"keywords\"]}";
        public string? JsonSchema => null;

        /// <summary>
        /// Delegate for LLM summarization.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? SummarizeFunction { get; set; }

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var content = doc.RootElement.GetProperty("content").GetString() ?? "";
                var targetTokens = doc.RootElement.TryGetProperty("target_tokens", out var tt) ? tt.GetInt32() : 500;
                var preserve = new List<string>();
                if (doc.RootElement.TryGetProperty("preserve", out var pa))
                {
                    foreach (var p in pa.EnumerateArray())
                    {
                        var kw = p.GetString();
                        if (!string.IsNullOrEmpty(kw)) preserve.Add(kw);
                    }
                }

                if (string.IsNullOrWhiteSpace(content))
                    return Core.Monads.Result<string, string>.Failure("Content is required.");

                // Estimate current tokens (rough: 4 chars per token)
                var currentTokens = content.Length / 4;

                if (currentTokens <= targetTokens)
                    return Core.Monads.Result<string, string>.Success($"📦 Content already within target ({currentTokens} ≤ {targetTokens} tokens).\n\n{content}");

                if (SummarizeFunction == null)
                {
                    // Fallback: simple truncation with sentence preservation
                    var sentences = content.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries);
                    var compressed = new StringBuilder();
                    var charLimit = targetTokens * 4;

                    foreach (var sentence in sentences)
                    {
                        if (compressed.Length + sentence.Length > charLimit) break;

                        // Prioritize sentences with preserved keywords
                        var hasKeyword = preserve.Count == 0 || preserve.Any(kw => sentence.Contains(kw, StringComparison.OrdinalIgnoreCase));
                        if (hasKeyword || compressed.Length < charLimit / 2)
                        {
                            compressed.Append(sentence.Trim()).Append(". ");
                        }
                    }

                    return Core.Monads.Result<string, string>.Success($"📦 **Compressed** ({currentTokens} → ~{compressed.Length / 4} tokens)\n\n{compressed}");
                }

                // Use LLM for intelligent summarization
                var preserveInstructions = preserve.Count > 0
                    ? $"\n\nIMPORTANT: Preserve information about: {string.Join(", ", preserve)}"
                    : "";

                var prompt = $"Compress this content to approximately {targetTokens} tokens while preserving key information and meaning.{preserveInstructions}\n\nCONTENT:\n{content}";

                var compressed2 = await SummarizeFunction(prompt, ct);

                return Core.Monads.Result<string, string>.Success($"📦 **Compressed** ({currentTokens} → ~{compressed2.Length / 4} tokens)\n\n{compressed2}");
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Compression failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Self-doubt tool that questions its own outputs.
    /// Provides metacognitive check against overconfidence.
    /// </summary>
    public class SelfDoubtTool : ITool
    {
        public string Name => "self_doubt";
        public string Description => "Question my own response for errors, biases, or overconfidence. Metacognitive check. Input: JSON {\"response\":\"...\", \"context\":\"...\"}";
        public string? JsonSchema => null;

        /// <summary>
        /// Delegate for LLM critique.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? CritiqueFunction { get; set; }

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var response = doc.RootElement.GetProperty("response").GetString() ?? "";
                var context = doc.RootElement.TryGetProperty("context", out var ctx) ? ctx.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(response))
                    return Core.Monads.Result<string, string>.Failure("Response to doubt is required.");

                if (CritiqueFunction == null)
                    return Core.Monads.Result<string, string>.Failure("Critique function not available.");

                var prompt = $@"You are a critical reviewer. Examine this AI response for potential issues.

CONTEXT: {context}

AI RESPONSE TO EXAMINE:
{response}

Analyze for:
1. FACTUAL ERRORS: Any claims that might be wrong or unverifiable?
2. LOGICAL FLAWS: Any reasoning errors or non-sequiturs?
3. OVERCONFIDENCE: Where is certainty expressed that isn't warranted?
4. BIASES: Any hidden assumptions or perspectives that might skew the answer?
5. MISSING CONTEXT: What important considerations were left out?
6. HALLUCINATION RISK: Which parts seem most likely to be fabricated?

For each issue found, rate severity (LOW/MEDIUM/HIGH) and suggest correction.
If the response seems solid, acknowledge that too.";

                var critique = await CritiqueFunction(prompt, ct);

                var sb = new StringBuilder();
                sb.AppendLine("🤔 **Self-Doubt Analysis**\n");
                sb.AppendLine(critique);

                return Core.Monads.Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Self-doubt failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Parallel MeTTa thought streams tool for multi-theory exploration.
    /// Runs multiple symbolic reasoning engines concurrently with Ollama fusion.
    /// </summary>
    public class ParallelMeTTaThinkTool : ITool
    {
        public string Name => "parallel_metta_think";
        public string Description => "Run parallel MeTTa symbolic thought streams with Ollama fusion. Input: JSON {\"query\":\"...\", \"streams\":3, \"mode\":\"explore|solve_square|converge\", \"target\":123}";
        public string? JsonSchema => null;

        /// <summary>
        /// Shared parallel streams orchestrator.
        /// </summary>
        public static Services.ParallelMeTTaThoughtStreams? SharedOrchestrator { get; set; }

        /// <summary>
        /// Delegate for Ollama inference.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? OllamaFunction { get; set; }

        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                var streamCount = doc.RootElement.TryGetProperty("streams", out var s) ? s.GetInt32() : 3;
                var mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "explore" : "explore";
                var target = doc.RootElement.TryGetProperty("target", out var t) ? t.GetInt64() : 0;

                // Create or reuse orchestrator
                var orchestrator = SharedOrchestrator ?? new Services.ParallelMeTTaThoughtStreams(streamCount);

                if (OllamaFunction != null)
                {
                    orchestrator.ConnectOllama(OllamaFunction);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🧠 **Parallel MeTTa Thought Streams** ({mode})");
                sb.AppendLine();

                switch (mode.ToLowerInvariant())
                {
                    case "solve_square":
                        if (target <= 0)
                            return Core.Monads.Result<string, string>.Failure("Target required for solve_square mode.");

                        sb.AppendLine($"**Target:** {target}");
                        sb.AppendLine("**Solving with modulo-square theory...**\n");

                        var solution = await orchestrator.SolveModuloSquareAsync(
                            new System.Numerics.BigInteger(target),
                            maxIterations: 50,
                            ct);

                        if (solution != null)
                        {
                            sb.AppendLine($"✅ **Solution Found!**");
                            sb.AppendLine($"  √{solution.Target} = {solution.SquareRoot}");
                            sb.AppendLine($"  Derivation: {solution.Derivation}");
                            sb.AppendLine($"  Verified: {solution.IsVerified}");
                        }
                        else
                        {
                            sb.AppendLine("❌ No solution found within iteration limit.");
                            var stats = orchestrator.GetStats();
                            sb.AppendLine($"  Explored {stats.TotalAtomsGenerated} atoms across {stats.ActiveStreams} streams.");
                        }
                        break;

                    case "converge":
                        // Create streams with different seed theories
                        var theories = new Dictionary<string, List<string>>();
                        var aspects = new[] { "logical", "intuitive", "skeptical", "creative", "analytical" };

                        for (int i = 0; i < Math.Min(streamCount, aspects.Length); i++)
                        {
                            theories[$"{aspects[i]}_stream"] = new List<string>
                            {
                                $"(perspective {aspects[i]})",
                                $"(query \"{query}\")",
                                $"(approach {aspects[i]}-reasoning)",
                            };
                        }

                        orchestrator.CreateTheoryStreams(theories);

                        var convergenceResults = new List<string>();
                        orchestrator.OnConvergence += (e) =>
                        {
                            convergenceResults.Add($"Convergence: {string.Join(", ", e.ConvergentStreams)} → {e.SharedConcept}");
                        };

                        await orchestrator.StartParallelThinkingAsync(query, ct);

                        sb.AppendLine($"**Query:** {query}");
                        sb.AppendLine($"**Streams:** {streamCount}\n");

                        if (convergenceResults.Count > 0)
                        {
                            sb.AppendLine("**Convergences:**");
                            foreach (var conv in convergenceResults)
                            {
                                sb.AppendLine($"  • {conv}");
                            }
                        }

                        // Collect thought atoms
                        var atoms = new List<Services.ThoughtAtom>();
                        while (orchestrator.MergedStream.TryRead(out var atom))
                        {
                            atoms.Add(atom);
                        }

                        if (atoms.Count > 0)
                        {
                            sb.AppendLine("\n**Recent Thoughts:**");
                            foreach (var atom in atoms.TakeLast(10))
                            {
                                sb.AppendLine($"  [{atom.StreamId}] {atom.Content}");
                            }
                        }
                        break;

                    default: // explore
                        // Simple parallel exploration
                        for (int i = 0; i < streamCount; i++)
                        {
                            orchestrator.CreateStream($"explorer_{i}", new[]
                            {
                                $"(explorer {i})",
                                $"(goal \"{query}\")",
                            });
                        }

                        await orchestrator.StartParallelThinkingAsync(query, ct);

                        var exploreStats = orchestrator.GetStats();
                        sb.AppendLine($"**Query:** {query}");
                        sb.AppendLine($"**Active Streams:** {exploreStats.ActiveStreams}");
                        sb.AppendLine($"**Total Atoms:** {exploreStats.TotalAtomsGenerated}\n");

                        sb.AppendLine("**Stream Details:**");
                        foreach (var detail in exploreStats.StreamDetails)
                        {
                            sb.AppendLine($"  • {detail.StreamId}: {detail.AtomCount} atoms");
                        }
                        break;
                }

                // Cleanup if we created a new orchestrator
                if (SharedOrchestrator == null)
                {
                    await orchestrator.DisposeAsync();
                }

                return Core.Monads.Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Parallel MeTTa thinking failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tool for self-referential Ouroboros MeTTa atom operations.
    /// </summary>
    public class OuroborosMeTTaTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "ouroboros_metta";

        /// <inheritdoc/>
        public string Description => "Create and manipulate self-referential Ouroboros MeTTa atoms. Input: JSON {\"mode\":\"create|loop|network|merge|reflect\", \"concept\":\"...\", \"iterations\":10}";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <summary>
        /// Shared parallel streams orchestrator.
        /// </summary>
        public static Services.ParallelMeTTaThoughtStreams? SharedOrchestrator { get; set; }

        /// <summary>
        /// Delegate for Ollama inference.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? OllamaFunction { get; set; }

        /// <summary>
        /// Active Ouroboros atoms for persistent self-reference.
        /// </summary>
        public static List<Services.OuroborosAtom> ActiveAtoms { get; } = [];

        /// <inheritdoc/>
        public async Task<Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                var mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "create" : "create";
                var concept = doc.RootElement.TryGetProperty("concept", out var c) ? c.GetString() ?? "self" : "self";
                var iterations = doc.RootElement.TryGetProperty("iterations", out var i) ? i.GetInt32() : 10;
                var atomIndex = doc.RootElement.TryGetProperty("atom_index", out var ai) ? ai.GetInt32() : 0;

                // Ensure orchestrator exists
                var orchestrator = SharedOrchestrator ?? new Services.ParallelMeTTaThoughtStreams();
                if (OllamaFunction != null)
                {
                    orchestrator.ConnectOllama(OllamaFunction);
                }

                var sb = new StringBuilder();
                sb.AppendLine("🐍 **Ouroboros MeTTa Atom**");
                sb.AppendLine();

                switch (mode.ToLowerInvariant())
                {
                    case "create":
                        return await CreateOuroboros(concept, orchestrator, sb, ct);

                    case "loop":
                    case "strange_loop":
                        return await RunStrangeLoop(atomIndex, iterations, orchestrator, sb, ct);

                    case "network":
                        return await CreateNetwork(iterations, orchestrator, sb, ct);

                    case "merge":
                        return await MergeAtoms(atomIndex, orchestrator, sb);

                    case "reflect":
                        return ReflectOnAtoms(atomIndex, sb);

                    case "godel":
                        return await CreateGodelian(orchestrator, sb, ct);

                    case "ycombinator":
                        return await ApplyYCombinator(atomIndex, iterations, sb);

                    default:
                        return Core.Monads.Result<string, string>.Failure($"Unknown mode: {mode}. Use: create, loop, network, merge, reflect, godel, ycombinator");
                }
            }
            catch (JsonException)
            {
                return Core.Monads.Result<string, string>.Failure("Invalid JSON input. Expected: {\"mode\":\"...\", \"concept\":\"...\"}");
            }
            catch (Exception ex)
            {
                return Core.Monads.Result<string, string>.Failure($"Ouroboros operation failed: {ex.Message}");
            }
        }

        private async Task<Core.Monads.Result<string, string>> CreateOuroboros(
            string concept,
            Services.ParallelMeTTaThoughtStreams orchestrator,
            StringBuilder sb,
            CancellationToken ct)
        {
            var (atom, node) = orchestrator.CreateOuroborosStream(concept);
            ActiveAtoms.Add(atom);

            sb.AppendLine($"**Mode:** Create Self-Aware Ouroboros");
            sb.AppendLine($"**Seed Concept:** {concept}");
            sb.AppendLine($"**Atom ID:** {atom.Id}");
            sb.AppendLine($"**Index:** {ActiveAtoms.Count - 1} (use for further operations)");
            sb.AppendLine();
            sb.AppendLine("**Initial State:**");
            sb.AppendLine($"```metta");
            sb.AppendLine(atom.Reflect());
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**MeTTa Atoms Generated:**");
            foreach (var mettaAtom in atom.ToMeTTaAtoms().Take(5))
            {
                sb.AppendLine($"  • `{mettaAtom}`");
            }
            if (atom.ToMeTTaAtoms().Count > 5)
            {
                sb.AppendLine($"  • ... and {atom.ToMeTTaAtoms().Count - 5} more");
            }

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }

        private async Task<Core.Monads.Result<string, string>> RunStrangeLoop(
            int atomIndex,
            int iterations,
            Services.ParallelMeTTaThoughtStreams orchestrator,
            StringBuilder sb,
            CancellationToken ct)
        {
            if (atomIndex < 0 || atomIndex >= ActiveAtoms.Count)
            {
                if (ActiveAtoms.Count == 0)
                {
                    // Create a default atom if none exist
                    var (newAtom, _) = orchestrator.CreateOuroborosStream("self");
                    ActiveAtoms.Add(newAtom);
                    atomIndex = 0;
                }
                else
                {
                    return Core.Monads.Result<string, string>.Failure($"Invalid atom_index. Valid range: 0-{ActiveAtoms.Count - 1}");
                }
            }

            var atom = ActiveAtoms[atomIndex];
            var startDepth = atom.SelfReferenceDepth;

            sb.AppendLine($"**Mode:** Strange Loop");
            sb.AppendLine($"**Atom:** {atom.Id.ToString()[..8]}");
            sb.AppendLine($"**Iterations:** {iterations}");
            sb.AppendLine($"**Starting Depth:** {startDepth}");
            sb.AppendLine();
            sb.AppendLine("**Self-Consumption Log:**");

            var thoughts = new List<Services.ThoughtAtom>();
            await foreach (var thought in orchestrator.RunStrangeLoopAsync(atom, iterations, ct))
            {
                thoughts.Add(thought);
                if (thoughts.Count <= 10)
                {
                    sb.AppendLine($"  [{thought.SequenceNumber}] {(thought.Content.Length > 80 ? thought.Content[..80] + "..." : thought.Content)}");
                }
            }

            if (thoughts.Count > 10)
            {
                sb.AppendLine($"  ... and {thoughts.Count - 10} more iterations");
            }

            sb.AppendLine();
            sb.AppendLine("**Final State:**");
            sb.AppendLine($"  • Depth: {atom.SelfReferenceDepth} (gained {atom.SelfReferenceDepth - startDepth})");
            sb.AppendLine($"  • Emergence Level: {atom.EmergenceLevel:F3}");
            sb.AppendLine($"  • Fixed Point Reached: {atom.IsFixedPoint}");

            if (atom.IsFixedPoint)
            {
                sb.AppendLine();
                sb.AppendLine("🎯 **FIXED POINT ACHIEVED!** The Ouroboros has completed its strange loop.");
            }

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }

        private async Task<Core.Monads.Result<string, string>> CreateNetwork(
            int count,
            Services.ParallelMeTTaThoughtStreams orchestrator,
            StringBuilder sb,
            CancellationToken ct)
        {
            var networkAtoms = orchestrator.CreateOuroborosNetwork(Math.Max(2, Math.Min(count, 7)));

            sb.AppendLine($"**Mode:** Ouroboros Network");
            sb.AppendLine($"**Network Size:** {networkAtoms.Count}");
            sb.AppendLine();
            sb.AppendLine("**Network Nodes:**");

            foreach (var (atom, node) in networkAtoms)
            {
                ActiveAtoms.Add(atom);
                var index = ActiveAtoms.Count - 1;
                sb.AppendLine($"  • [{index}] {atom.Id.ToString()[..8]} - depth={atom.SelfReferenceDepth}, emergence={atom.EmergenceLevel:F3}");
            }

            sb.AppendLine();
            sb.AppendLine("**Network Topology:** Circular (each node aware of neighbors)");
            sb.AppendLine();
            sb.AppendLine("Use `ouroboros_metta` with `mode: loop` and `atom_index` to run strange loops on individual nodes.");

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }

        private async Task<Core.Monads.Result<string, string>> MergeAtoms(
            int atomIndex,
            Services.ParallelMeTTaThoughtStreams orchestrator,
            StringBuilder sb)
        {
            if (ActiveAtoms.Count < 2)
            {
                return Core.Monads.Result<string, string>.Failure("Need at least 2 Ouroboros atoms to merge. Create more first.");
            }

            var atom1Index = atomIndex;
            var atom2Index = (atomIndex + 1) % ActiveAtoms.Count;

            if (atom1Index < 0 || atom1Index >= ActiveAtoms.Count)
            {
                atom1Index = 0;
                atom2Index = 1;
            }

            var atom1 = ActiveAtoms[atom1Index];
            var atom2 = ActiveAtoms[atom2Index];

            var (merged, node) = orchestrator.MergeOuroborosStreams(atom1, atom2);
            ActiveAtoms.Add(merged);

            sb.AppendLine($"**Mode:** Merge Ouroboros Atoms");
            sb.AppendLine($"**Source 1:** [{atom1Index}] {atom1.Id.ToString()[..8]}");
            sb.AppendLine($"**Source 2:** [{atom2Index}] {atom2.Id.ToString()[..8]}");
            sb.AppendLine($"**Merged:** [{ActiveAtoms.Count - 1}] {merged.Id.ToString()[..8]}");
            sb.AppendLine();
            sb.AppendLine("**Merged State:**");
            sb.AppendLine($"```metta");
            sb.AppendLine(merged.Reflect());
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine($"**Combined Emergence:** {merged.EmergenceLevel:F3}");
            sb.AppendLine($"**Transformation History:** {merged.TransformationHistory.Count} records");

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }

        private Core.Monads.Result<string, string> ReflectOnAtoms(int atomIndex, StringBuilder sb)
        {
            if (ActiveAtoms.Count == 0)
            {
                return Core.Monads.Result<string, string>.Failure("No Ouroboros atoms exist. Create one first with mode: create");
            }

            sb.AppendLine($"**Mode:** Reflect on Ouroboros Atoms");
            sb.AppendLine($"**Active Atoms:** {ActiveAtoms.Count}");
            sb.AppendLine();

            if (atomIndex >= 0 && atomIndex < ActiveAtoms.Count)
            {
                // Detailed reflection on specific atom
                var atom = ActiveAtoms[atomIndex];
                sb.AppendLine($"**Detailed Reflection on [{atomIndex}]:**");
                sb.AppendLine($"```metta");
                sb.AppendLine(atom.Reflect());
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("**Transformation History:**");
                foreach (var transform in atom.TransformationHistory.TakeLast(10))
                {
                    sb.AppendLine($"  • {transform}");
                }
                if (atom.TransformationHistory.Count > 10)
                {
                    sb.AppendLine($"  ... and {atom.TransformationHistory.Count - 10} earlier transformations");
                }

                if (atom.Children.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Spawned Children:** {atom.Children.Count}");
                }
            }
            else
            {
                // Summary of all atoms
                sb.AppendLine("**All Ouroboros Atoms:**");
                for (int idx = 0; idx < ActiveAtoms.Count; idx++)
                {
                    var atom = ActiveAtoms[idx];
                    sb.AppendLine($"  [{idx}] {atom}");
                }
            }

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }

        private async Task<Core.Monads.Result<string, string>> CreateGodelian(
            Services.ParallelMeTTaThoughtStreams orchestrator,
            StringBuilder sb,
            CancellationToken ct)
        {
            var atom = Services.OuroborosAtomFactory.CreateGodelian();
            ActiveAtoms.Add(atom);

            sb.AppendLine($"**Mode:** Gödelian Self-Reference");
            sb.AppendLine($"**Atom ID:** {atom.Id}");
            sb.AppendLine($"**Index:** {ActiveAtoms.Count - 1}");
            sb.AppendLine();
            sb.AppendLine("This Ouroboros embodies Gödel's self-referential statement pattern:");
            sb.AppendLine("*\"This statement refers to itself\"*");
            sb.AppendLine();
            sb.AppendLine("**State:**");
            sb.AppendLine($"```metta");
            sb.AppendLine(atom.Reflect());
            sb.AppendLine("```");

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }

        private async Task<Core.Monads.Result<string, string>> ApplyYCombinator(
            int atomIndex,
            int iterations,
            StringBuilder sb)
        {
            if (atomIndex < 0 || atomIndex >= ActiveAtoms.Count)
            {
                if (ActiveAtoms.Count == 0)
                {
                    var atom = new Services.OuroborosAtom("(identity x)");
                    ActiveAtoms.Add(atom);
                    atomIndex = 0;
                }
                else
                {
                    atomIndex = 0;
                }
            }

            var targetAtom = ActiveAtoms[atomIndex];
            var beforeCore = targetAtom.Core;

            sb.AppendLine($"**Mode:** Y-Combinator Application");
            sb.AppendLine($"**Atom:** [{atomIndex}] {targetAtom.Id.ToString()[..8]}");
            sb.AppendLine($"**Iterations:** {iterations}");
            sb.AppendLine();
            sb.AppendLine($"**Before:** `{(beforeCore.Length > 60 ? beforeCore[..60] + "..." : beforeCore)}`");

            var result = targetAtom.ApplyYCombinator(iterations);

            sb.AppendLine();
            sb.AppendLine("**Y-Combinator Applied:**");
            sb.AppendLine("Y = λf.(λx.f(x x))(λx.f(x x))");
            sb.AppendLine();
            sb.AppendLine($"**After {targetAtom.SelfReferenceDepth} recursions:**");
            sb.AppendLine($"  • Core: `{(result.Length > 80 ? result[..80] + "..." : result)}`");
            sb.AppendLine($"  • Emergence: {targetAtom.EmergenceLevel:F3}");
            sb.AppendLine($"  • Fixed Point: {targetAtom.IsFixedPoint}");

            return Core.Monads.Result<string, string>.Success(sb.ToString());
        }
    }
}
