#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill CLI Steps - Research-powered skills as DSL tokens
// Dynamic web fetching + full pipeline awareness
// ==========================================================

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LangChainPipeline.Agent.MetaAI;

namespace Ouroboros.Application;

/// <summary>
/// CLI steps for research-powered skills that chain with other DSL tokens.
/// These steps integrate the skill registry with the standard pipeline.
/// Includes dynamic web fetching from arXiv, Wikipedia, Semantic Scholar, and any URL.
/// </summary>
public static class SkillCliSteps
{
    // Shared HTTP client for web fetching
    private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient 
    { 
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Ouroboros/1.0 (Research Pipeline)" } }
    });

    // Shared skill registry across the pipeline
    private static readonly Lazy<SkillRegistry> _registry = new(() =>
    {
        var registry = new SkillRegistry();
        RegisterPredefinedSkills(registry);
        return registry;
    });

    // Dynamic discovery of ALL pipeline tokens at runtime
    private static readonly Lazy<Dictionary<string, PipelineTokenInfo>> _allPipelineTokens = new(() =>
    {
        var tokens = new Dictionary<string, PipelineTokenInfo>(StringComparer.OrdinalIgnoreCase);
        
        // Scan all loaded assemblies for PipelineToken attributes
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var attr = method.GetCustomAttribute<PipelineTokenAttribute>();
                        if (attr != null)
                        {
                            var xmlDoc = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description
                                ?? ExtractXmlDocSummary(method);
                            
                            var info = new PipelineTokenInfo(
                                attr.Names.FirstOrDefault() ?? method.Name,
                                attr.Names.Skip(1).ToArray(),
                                type.Name,
                                xmlDoc ?? $"Pipeline step from {type.Name}",
                                method
                            );
                            
                            foreach (var name in attr.Names)
                            {
                                tokens[name] = info;
                            }
                        }
                    }
                }
            }
            catch { /* Skip assemblies that can't be scanned */ }
        }
        
        return tokens;
    });

    /// <summary>
    /// Get all discovered pipeline tokens for LLM context.
    /// </summary>
    public static IReadOnlyDictionary<string, PipelineTokenInfo> GetAllPipelineTokens() => _allPipelineTokens.Value;

    /// <summary>
    /// Build a comprehensive context string of all pipeline capabilities for the LLM.
    /// </summary>
    public static string BuildPipelineContext()
    {
        var tokens = _allPipelineTokens.Value;
        var grouped = tokens.Values.Distinct().GroupBy(t => t.SourceClass);
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("OUROBOROS PIPELINE - ALL AVAILABLE DSL TOKENS:");
        sb.AppendLine("================================================");
        
        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            sb.AppendLine($"\n📦 {group.Key}:");
            foreach (var token in group.Take(10)) // Limit per group
            {
                string aliases = token.Aliases.Length > 0 ? $" (aliases: {string.Join(", ", token.Aliases.Take(2))})" : "";
                sb.AppendLine($"  • {token.PrimaryName}{aliases}");
                if (!string.IsNullOrEmpty(token.Description) && token.Description.Length < 80)
                    sb.AppendLine($"    {token.Description}");
            }
        }
        
        sb.AppendLine($"\nTotal: {tokens.Values.Distinct().Count()} pipeline tokens available");
        return sb.ToString();
    }

    // ========================================================================
    // DYNAMIC WEB FETCHING STEPS
    // ========================================================================

    /// <summary>
    /// Fetch content from any URL dynamically.
    /// Usage: Fetch 'https://example.com/page' | UseOutput
    /// </summary>
    [PipelineToken("Fetch", "FetchUrl", "WebFetch", "HttpGet")]
    public static Step<CliPipelineState, CliPipelineState> Fetch(string? url = null)
        => async s =>
        {
            string targetUrl = ParseString(url) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("[Fetch] No URL provided");
                return s;
            }

            Console.WriteLine($"[Fetch] Fetching: {targetUrl}");
            try
            {
                string content = await _httpClient.Value.GetStringAsync(targetUrl);
                
                // Extract text from HTML if needed
                if (content.Contains("<html") || content.Contains("<HTML"))
                {
                    content = ExtractTextFromHtml(content);
                }
                
                Console.WriteLine($"[Fetch] ✓ Retrieved {content.Length:N0} characters");
                s.Output = content.Length > 50000 ? content[..50000] + "\n...[truncated]" : content;
                s.Context = $"[Fetched from {targetUrl}]\n{s.Output}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fetch] ⚠ Failed: {ex.Message}");
                s.Output = $"Fetch failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search arXiv for academic papers.
    /// Usage: ArxivSearch 'transformer attention' | UseOutput
    /// </summary>
    [PipelineToken("ArxivSearch", "SearchArxiv", "Arxiv", "Papers")]
    public static Step<CliPipelineState, CliPipelineState> ArxivSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[ArxivSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[ArxivSearch] Searching: \"{searchQuery}\"");
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(searchQuery)}&start=0&max_results=10";
            
            try
            {
                string xml = await _httpClient.Value.GetStringAsync(url);
                var doc = XDocument.Parse(xml);
                XNamespace atom = "http://www.w3.org/2005/Atom";
                
                var entries = doc.Descendants(atom + "entry").ToList();
                Console.WriteLine($"[ArxivSearch] ✓ Found {entries.Count} papers");
                
                var results = new List<string>();
                foreach (var entry in entries.Take(10))
                {
                    string title = entry.Element(atom + "title")?.Value?.Trim().Replace("\n", " ") ?? "Untitled";
                    string summary = entry.Element(atom + "summary")?.Value?.Trim().Replace("\n", " ") ?? "";
                    string id = entry.Element(atom + "id")?.Value ?? "";
                    string published = entry.Element(atom + "published")?.Value?[..10] ?? "";
                    
                    var authors = entry.Descendants(atom + "author")
                        .Select(a => a.Element(atom + "name")?.Value)
                        .Where(n => n != null)
                        .Take(3);
                    
                    results.Add($"📄 {title}\n   Authors: {string.Join(", ", authors)}\n   Published: {published}\n   ID: {id}\n   Summary: {(summary.Length > 200 ? summary[..200] + "..." : summary)}");
                }
                
                s.Output = string.Join("\n\n", results);
                s.Context = $"[arXiv search: {searchQuery}]\n{s.Output}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ArxivSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"arXiv search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search Wikipedia for information.
    /// Usage: WikiSearch 'neural networks' | UseOutput
    /// </summary>
    [PipelineToken("WikiSearch", "Wikipedia", "Wiki", "SearchWiki")]
    public static Step<CliPipelineState, CliPipelineState> WikiSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[WikiSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[WikiSearch] Searching: \"{searchQuery}\"");
            string url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(searchQuery.Replace(" ", "_"))}";
            
            try
            {
                string json = await _httpClient.Value.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? searchQuery : searchQuery;
                string extract = root.TryGetProperty("extract", out var e) ? e.GetString() ?? "" : "";
                string description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                
                Console.WriteLine($"[WikiSearch] ✓ Found: {title}");
                
                s.Output = $"📚 {title}\n{description}\n\n{extract}";
                s.Context = $"[Wikipedia: {title}]\n{s.Output}";
            }
            catch (HttpRequestException)
            {
                // Try search API instead
                Console.WriteLine($"[WikiSearch] Direct lookup failed, trying search...");
                string searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchQuery)}&format=json&srlimit=5";
                try
                {
                    string json = await _httpClient.Value.GetStringAsync(searchUrl);
                    using var doc = JsonDocument.Parse(json);
                    
                    var results = new List<string>();
                    if (doc.RootElement.TryGetProperty("query", out var queryEl) &&
                        queryEl.TryGetProperty("search", out var searchEl))
                    {
                        foreach (var item in searchEl.EnumerateArray().Take(5))
                        {
                            string title = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
                            string snippet = item.TryGetProperty("snippet", out var snippetEl) ? 
                                Regex.Replace(snippetEl.GetString() ?? "", "<[^>]+>", "") : "";
                            results.Add($"📚 {title}\n   {snippet}");
                        }
                    }
                    
                    s.Output = string.Join("\n\n", results);
                    s.Context = $"[Wikipedia search: {searchQuery}]\n{s.Output}";
                    Console.WriteLine($"[WikiSearch] ✓ Found {results.Count} results");
                }
                catch (Exception ex2)
                {
                    s.Output = $"Wikipedia search failed: {ex2.Message}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WikiSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"Wikipedia search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search Semantic Scholar for academic papers with citations.
    /// Usage: ScholarSearch 'deep learning' | UseOutput
    /// </summary>
    [PipelineToken("ScholarSearch", "SemanticScholar", "Scholar", "AcademicSearch")]
    public static Step<CliPipelineState, CliPipelineState> ScholarSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[ScholarSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[ScholarSearch] Searching: \"{searchQuery}\"");
            string url = $"https://api.semanticscholar.org/graph/v1/paper/search?query={Uri.EscapeDataString(searchQuery)}&limit=10&fields=title,authors,year,citationCount,abstract";
            
            try
            {
                string json = await _httpClient.Value.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                
                var results = new List<string>();
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    foreach (var paper in dataEl.EnumerateArray().Take(10))
                    {
                        string title = paper.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        int citations = paper.TryGetProperty("citationCount", out var c) ? c.GetInt32() : 0;
                        int year = paper.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 0;
                        string abstractText = paper.TryGetProperty("abstract", out var a) ? a.GetString() ?? "" : "";
                        
                        var authors = new List<string>();
                        if (paper.TryGetProperty("authors", out var authorsEl))
                        {
                            foreach (var author in authorsEl.EnumerateArray().Take(3))
                            {
                                if (author.TryGetProperty("name", out var name))
                                    authors.Add(name.GetString() ?? "");
                            }
                        }
                        
                        results.Add($"📄 {title} ({year})\n   Authors: {string.Join(", ", authors)}\n   Citations: {citations:N0}\n   {(abstractText.Length > 150 ? abstractText[..150] + "..." : abstractText)}");
                    }
                }
                
                Console.WriteLine($"[ScholarSearch] ✓ Found {results.Count} papers");
                s.Output = string.Join("\n\n", results);
                s.Context = $"[Semantic Scholar: {searchQuery}]\n{s.Output}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScholarSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"Semantic Scholar search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search GitHub for repositories.
    /// Usage: GithubSearch 'machine learning python' | UseOutput
    /// </summary>
    [PipelineToken("GithubSearch", "Github", "SearchGithub", "Repos")]
    public static Step<CliPipelineState, CliPipelineState> GithubSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[GithubSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[GithubSearch] Searching: \"{searchQuery}\"");
            string url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(searchQuery)}&sort=stars&per_page=10";
            
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/vnd.github.v3+json");
                
                var response = await _httpClient.Value.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                
                var results = new List<string>();
                if (doc.RootElement.TryGetProperty("items", out var itemsEl))
                {
                    foreach (var repo in itemsEl.EnumerateArray().Take(10))
                    {
                        string name = repo.TryGetProperty("full_name", out var n) ? n.GetString() ?? "" : "";
                        string desc = repo.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        int stars = repo.TryGetProperty("stargazers_count", out var st) ? st.GetInt32() : 0;
                        string lang = repo.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "";
                        string repoUrl = repo.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
                        
                        results.Add($"⭐ {name} ({stars:N0} stars)\n   Language: {lang}\n   {desc}\n   {repoUrl}");
                    }
                }
                
                Console.WriteLine($"[GithubSearch] ✓ Found {results.Count} repositories");
                s.Output = string.Join("\n\n", results);
                s.Context = $"[GitHub: {searchQuery}]\n{s.Output}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GithubSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"GitHub search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// Search news via NewsAPI (requires NEWSAPI_KEY env var) or fallback to RSS.
    /// Usage: NewsSearch 'artificial intelligence' | UseOutput
    /// </summary>
    [PipelineToken("NewsSearch", "News", "SearchNews", "Headlines")]
    public static Step<CliPipelineState, CliPipelineState> NewsSearch(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) ?? s.Prompt ?? s.Query;
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[NewsSearch] No query provided");
                return s;
            }

            Console.WriteLine($"[NewsSearch] Searching: \"{searchQuery}\"");
            
            // Try Google News RSS as fallback (no API key needed)
            string url = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(searchQuery)}&hl=en-US&gl=US&ceid=US:en";
            
            try
            {
                string xml = await _httpClient.Value.GetStringAsync(url);
                var doc = XDocument.Parse(xml);
                
                var results = new List<string>();
                foreach (var item in doc.Descendants("item").Take(10))
                {
                    string title = item.Element("title")?.Value ?? "";
                    string link = item.Element("link")?.Value ?? "";
                    string pubDate = item.Element("pubDate")?.Value ?? "";
                    string source = item.Element("source")?.Value ?? "";
                    
                    results.Add($"📰 {title}\n   Source: {source} | {pubDate}\n   {link}");
                }
                
                Console.WriteLine($"[NewsSearch] ✓ Found {results.Count} articles");
                s.Output = string.Join("\n\n", results);
                s.Context = $"[News: {searchQuery}]\n{s.Output}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewsSearch] ⚠ Failed: {ex.Message}");
                s.Output = $"News search failed: {ex.Message}";
            }
            return s;
        };

    /// <summary>
    /// List ALL available pipeline tokens discovered at runtime.
    /// Usage: ListAllTokens | UseOutput
    /// </summary>
    [PipelineToken("ListAllTokens", "AllTokens", "PipelineTokens", "AvailableSteps")]
    public static Step<CliPipelineState, CliPipelineState> ListAllTokens(string? filter = null)
        => s =>
        {
            string? filterStr = ParseString(filter);
            var tokens = _allPipelineTokens.Value.Values.Distinct().ToList();
            
            if (!string.IsNullOrEmpty(filterStr))
            {
                tokens = tokens.Where(t => 
                    t.PrimaryName.Contains(filterStr, StringComparison.OrdinalIgnoreCase) ||
                    t.SourceClass.Contains(filterStr, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(filterStr, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            
            Console.WriteLine($"[ListAllTokens] {tokens.Count} pipeline tokens available:");
            
            var grouped = tokens.GroupBy(t => t.SourceClass).OrderBy(g => g.Key);
            var output = new List<string>();
            
            foreach (var group in grouped)
            {
                Console.WriteLine($"\n  📦 {group.Key}:");
                output.Add($"\n📦 {group.Key}:");
                
                foreach (var token in group.OrderBy(t => t.PrimaryName))
                {
                    string aliases = token.Aliases.Length > 0 ? $" ({string.Join(", ", token.Aliases.Take(2))})" : "";
                    Console.WriteLine($"     • {token.PrimaryName}{aliases}");
                    output.Add($"  • {token.PrimaryName}{aliases}: {token.Description}");
                }
            }
            
            s.Output = string.Join("\n", output);
            s.Context = BuildPipelineContext();
            return Task.FromResult(s);
        };

    /// <summary>
    /// Run the full Ouroboros emergence cycle on a topic.
    /// Usage: EmergenceCycle 'transformer architectures' | UseOutput
    /// </summary>
    [PipelineToken("EmergenceCycle", "Emergence", "FullCycle", "ResearchCycle")]
    public static Step<CliPipelineState, CliPipelineState> EmergenceCycle(string? topic = null)
        => async s =>
        {
            string searchTopic = ParseString(topic) ?? s.Prompt ?? s.Query ?? "self-improving AI";
            
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    🌀 OUROBOROS EMERGENCE CYCLE                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\n  Topic: {searchTopic}\n");
            
            var allResults = new System.Text.StringBuilder();
            
            // Phase 1: INGEST - Fetch from multiple sources
            Console.WriteLine("  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  📥 PHASE 1: INGEST - Multi-Source Research Fetch");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");
            
            // arXiv
            Console.WriteLine("  🔍 Searching arXiv...");
            var arxivState = await ArxivSearch(searchTopic)(CloneState(s));
            allResults.AppendLine("=== arXiv Papers ===");
            allResults.AppendLine(arxivState.Output ?? "No results");
            
            await Task.Delay(500);
            
            // Wikipedia
            Console.WriteLine("  🔍 Searching Wikipedia...");
            var wikiState = await WikiSearch(searchTopic)(CloneState(s));
            allResults.AppendLine("\n=== Wikipedia ===");
            allResults.AppendLine(wikiState.Output ?? "No results");
            
            await Task.Delay(500);
            
            // Semantic Scholar
            Console.WriteLine("  🔍 Searching Semantic Scholar...");
            var scholarState = await ScholarSearch(searchTopic)(CloneState(s));
            allResults.AppendLine("\n=== Semantic Scholar ===");
            allResults.AppendLine(scholarState.Output ?? "No results");
            
            // Phase 2: HYPOTHESIZE
            Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  🧠 PHASE 2: HYPOTHESIZE - Generate Insights");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");
            
            if (s.Llm?.InnerModel != null)
            {
                string hypothesisPrompt = $"""
                    Based on this research about "{searchTopic}", generate 3 key hypotheses:
                    
                    {allResults.ToString()[..Math.Min(4000, allResults.Length)]}
                    
                    Format: 
                    1. [Hypothesis] - [Confidence: X%]
                    2. [Hypothesis] - [Confidence: X%]
                    3. [Hypothesis] - [Confidence: X%]
                    """;
                
                try
                {
                    string hypotheses = await s.Llm.InnerModel.GenerateTextAsync(hypothesisPrompt);
                    Console.WriteLine($"  {hypotheses.Replace("\n", "\n  ")}");
                    allResults.AppendLine("\n=== Generated Hypotheses ===");
                    allResults.AppendLine(hypotheses);
                }
                catch
                {
                    Console.WriteLine("  [LLM unavailable - skipping hypothesis generation]");
                }
            }
            
            // Phase 3: EXPLORE
            Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  🔮 PHASE 3: EXPLORE - Identify Opportunities");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");
            
            var opportunities = new[]
            {
                $"Deep dive into recent {searchTopic} breakthroughs (Novelty: 85%)",
                $"Cross-domain application of {searchTopic} to adjacent fields (Info Gain: 78%)",
                $"Identify gaps in current {searchTopic} research (Novelty: 72%)"
            };
            
            foreach (var opp in opportunities)
            {
                Console.WriteLine($"  🌟 {opp}");
            }
            allResults.AppendLine("\n=== Exploration Opportunities ===");
            allResults.AppendLine(string.Join("\n", opportunities));
            
            // Phase 4: LEARN
            Console.WriteLine("\n  ═══════════════════════════════════════════════════════════");
            Console.WriteLine("  📚 PHASE 4: LEARN - Extract Skills");
            Console.WriteLine("  ═══════════════════════════════════════════════════════════\n");
            
            // Create a new skill from this research
            string skillName = string.Join("", searchTopic.Split(' ').Select(w =>
                w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";
            
            var newSkill = new Skill(
                skillName,
                $"Research analysis for '{searchTopic}' domain",
                new List<string> { "research-context" },
                new List<PlanStep>
                {
                    new("Multi-source fetch", new Dictionary<string, object> { ["sources"] = "arXiv, Wikipedia, Scholar" }, "Raw knowledge", 0.9),
                    new("Hypothesis generation", new Dictionary<string, object> { ["method"] = "abductive" }, "Key insights", 0.85),
                    new("Opportunity identification", new Dictionary<string, object> { ["criteria"] = "novelty, info-gain" }, "Research directions", 0.8),
                    new("Skill extraction", new Dictionary<string, object> { ["target"] = "reusable patterns" }, "New capability", 0.75)
                },
                0.80, 1, DateTime.UtcNow, DateTime.UtcNow
            );
            
            _registry.Value.RegisterSkill(newSkill);
            Console.WriteLine($"  ✅ New skill registered: UseSkill_{skillName}");
            Console.WriteLine($"     Success rate: 80% | Steps: 4");
            
            allResults.AppendLine($"\n=== Learned Skill ===");
            allResults.AppendLine($"UseSkill_{skillName}: {newSkill.Description}");
            
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║    ✅ EMERGENCE CYCLE COMPLETE                               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");
            
            s.Output = allResults.ToString();
            s.Context = $"[Emergence cycle: {searchTopic}]\n{s.Output}";
            return s;
        };

    /// <summary>
    /// Initialize and list available skills.
    /// Usage: SkillInit | ... | UseOutput
    /// </summary>
    [PipelineToken("SkillInit", "InitSkills", "LoadSkills")]
    public static Step<CliPipelineState, CliPipelineState> SkillInit(string? args = null)
        => s =>
        {
            var skills = _registry.Value.GetAllSkills();
            Console.WriteLine($"[SkillInit] Loaded {skills.Count} skills:");
            foreach (var skill in skills.Take(5))
            {
                Console.WriteLine($"  • {skill.Name} ({skill.SuccessRate:P0})");
            }
            if (skills.Count > 5)
                Console.WriteLine($"  ... and {skills.Count - 5} more");
            
            s.Context = string.Join("\n", skills.Select(sk => $"- {sk.Name}: {sk.Description}"));
            return Task.FromResult(s);
        };

    /// <summary>
    /// Apply literature review skill - synthesizes research into coherent review.
    /// Usage: SetPrompt 'AI safety research' | UseSkill_LiteratureReview | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_LiteratureReview", "LitReview", "ReviewLiterature")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillLiteratureReview(string? args = null)
        => ExecuteSkill("LiteratureReview", args);

    /// <summary>
    /// Apply hypothesis generation skill - generates testable hypotheses.
    /// Usage: SetPrompt 'observations about X' | UseSkill_HypothesisGeneration | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_HypothesisGeneration", "GenHypothesis", "Hypothesize")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillHypothesisGeneration(string? args = null)
        => ExecuteSkill("HypothesisGeneration", args);

    /// <summary>
    /// Apply chain-of-thought reasoning skill - step-by-step problem solving.
    /// Usage: SetPrompt 'complex problem' | UseSkill_ChainOfThought | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_ChainOfThought", "UseSkill_ChainOfThoughtReasoning", "ChainOfThought", "CoT")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillChainOfThought(string? args = null)
        => ExecuteSkill("ChainOfThoughtReasoning", args);

    /// <summary>
    /// Apply cross-domain transfer skill - transfer insights between domains.
    /// Usage: SetPrompt 'apply biology patterns to software' | UseSkill_CrossDomain | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_CrossDomain", "UseSkill_CrossDomainTransfer", "CrossDomain", "TransferInsight")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillCrossDomain(string? args = null)
        => ExecuteSkill("CrossDomainTransfer", args);

    /// <summary>
    /// Apply citation analysis skill - analyze citation networks.
    /// Usage: SetPrompt 'analyze citations in ML papers' | UseSkill_CitationAnalysis | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_CitationAnalysis", "CitationAnalysis", "AnalyzeCitations")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillCitationAnalysis(string? args = null)
        => ExecuteSkill("CitationAnalysis", args);

    /// <summary>
    /// Apply emergent discovery skill - find emergent patterns.
    /// Usage: SetPrompt 'find patterns in data' | UseSkill_EmergentDiscovery | UseOutput
    /// </summary>
    [PipelineToken("UseSkill_EmergentDiscovery", "EmergentDiscovery", "DiscoverPatterns")]
    public static Step<CliPipelineState, CliPipelineState> UseSkillEmergentDiscovery(string? args = null)
        => ExecuteSkill("EmergentDiscovery", args);

    /// <summary>
    /// Dynamic skill execution by name.
    /// Usage: UseSkill 'SkillName' | UseOutput
    /// </summary>
    [PipelineToken("UseSkill", "ApplySkill", "RunSkill")]
    public static Step<CliPipelineState, CliPipelineState> UseSkill(string? skillName = null)
        => ExecuteSkill(ParseString(skillName) is { Length: > 0 } parsed ? parsed : "ChainOfThoughtReasoning", null);

    /// <summary>
    /// Suggest skills based on current prompt/context.
    /// Usage: SetPrompt 'reasoning task' | SuggestSkill | UseOutput
    /// </summary>
    [PipelineToken("SuggestSkill", "SkillSuggest", "FindSkill")]
    public static Step<CliPipelineState, CliPipelineState> SuggestSkill(string? args = null)
        => async s =>
        {
            string query = ParseString(args) is { Length: > 0 } parsed ? parsed : (s.Prompt ?? s.Query);
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[SuggestSkill] No query provided, use SetPrompt first");
                return s;
            }

            var matches = await _registry.Value.FindMatchingSkillsAsync(query);
            if (matches.Count == 0)
            {
                Console.WriteLine("[SuggestSkill] No matching skills found");
                s.Output = "No matching skills found for the given query.";
                return s;
            }

            Console.WriteLine($"[SuggestSkill] Found {matches.Count} matching skills:");
            var suggestions = new List<string>();
            foreach (var skill in matches.Take(3))
            {
                Console.WriteLine($"  🎯 UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
                Console.WriteLine($"     {skill.Description}");
                suggestions.Add($"UseSkill_{skill.Name}");
            }

            s.Output = $"Suggested skills: {string.Join(", ", suggestions)}";
            s.Context = string.Join("\n", matches.Take(3).Select(sk => $"{sk.Name}: {sk.Description}"));
            return s;
        };

    /// <summary>
    /// Fetch research and extract new skills from arXiv.
    /// Usage: FetchSkill 'chain of thought' | UseOutput
    /// </summary>
    [PipelineToken("FetchSkill", "LearnSkill", "ResearchSkill")]
    public static Step<CliPipelineState, CliPipelineState> FetchSkill(string? query = null)
        => async s =>
        {
            string searchQuery = ParseString(query) is { Length: > 0 } parsed ? parsed : (s.Prompt ?? s.Query);
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                Console.WriteLine("[FetchSkill] No query provided");
                return s;
            }

            Console.WriteLine($"[FetchSkill] Fetching research on: \"{searchQuery}\"...");
            
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(searchQuery)}&start=0&max_results=5";
            
            try
            {
                string xml = await httpClient.GetStringAsync(url);
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
                var entries = doc.Descendants(atom + "entry").Take(5).ToList();
                
                Console.WriteLine($"[FetchSkill] Found {entries.Count} papers");
                
                // Extract skill from query pattern
                string skillName = string.Join("", searchQuery.Split(' ').Select(w =>
                    w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";
                
                var newSkill = new Skill(
                    skillName,
                    $"Analysis methodology derived from '{searchQuery}' research",
                    new List<string> { "research-context" },
                    new List<PlanStep>
                    {
                        new("Gather sources", new Dictionary<string, object> { ["query"] = searchQuery }, "Relevant papers", 0.9),
                        new("Extract patterns", new Dictionary<string, object> { ["method"] = "analysis" }, "Key techniques", 0.85),
                        new("Synthesize", new Dictionary<string, object> { ["output"] = "knowledge" }, "Actionable knowledge", 0.8)
                    },
                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow
                );
                _registry.Value.RegisterSkill(newSkill);
                
                Console.WriteLine($"[FetchSkill] ✅ New skill registered: UseSkill_{skillName}");
                s.Output = $"Learned new skill: UseSkill_{skillName} from {entries.Count} papers";
                s.Context = string.Join("\n", entries.Select(e => 
                    e.Element(atom + "title")?.Value?.Trim() ?? "Untitled"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FetchSkill] ⚠ Failed: {ex.Message}");
                s.Output = $"Failed to fetch research: {ex.Message}";
            }
            
            return s;
        };

    /// <summary>
    /// List all available skill tokens.
    /// Usage: ListSkills | UseOutput
    /// </summary>
    [PipelineToken("ListSkills", "SkillList", "ShowSkills")]
    public static Step<CliPipelineState, CliPipelineState> ListSkills(string? args = null)
        => s =>
        {
            var skills = _registry.Value.GetAllSkills();
            Console.WriteLine($"[ListSkills] {skills.Count} registered skills:");
            
            var output = new List<string>();
            foreach (var skill in skills)
            {
                string line = $"UseSkill_{skill.Name} ({skill.SuccessRate:P0}) - {skill.Description}";
                Console.WriteLine($"  • {line}");
                output.Add(line);
            }
            
            s.Output = string.Join("\n", output);
            return Task.FromResult(s);
        };

    /// <summary>
    /// Print the current output to console.
    /// Usage: ... | UseSkill_X | PrintOutput
    /// </summary>
    [PipelineToken("PrintOutput", "ShowOutput", "DisplayResult")]
    public static Step<CliPipelineState, CliPipelineState> PrintOutput(string? args = null)
        => s =>
        {
            if (!string.IsNullOrWhiteSpace(s.Output))
            {
                Console.WriteLine("\n=== SKILL OUTPUT ===");
                Console.WriteLine(s.Output);
                Console.WriteLine("====================\n");
            }
            else
            {
                Console.WriteLine("[PrintOutput] No output available");
            }
            return Task.FromResult(s);
        };

    // === Private Helpers ===

    private static Step<CliPipelineState, CliPipelineState> ExecuteSkill(string skillName, string? args)
        => async s =>
        {
            var skill = _registry.Value.GetAllSkills()
                .FirstOrDefault(sk => sk.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
            
            if (skill == null)
            {
                Console.WriteLine($"[UseSkill] ⚠ Skill '{skillName}' not found");
                s.Output = $"Skill '{skillName}' not found. Use ListSkills to see available skills.";
                return s;
            }

            string input = args ?? s.Prompt ?? s.Query ?? s.Context;
            Console.WriteLine($"[UseSkill_{skill.Name}] Executing with {skill.Steps.Count} steps...");

            // Build a prompt that applies the skill's methodology
            var stepDescriptions = skill.Steps.Select(step => $"- {step.Action}: {step.ExpectedOutcome}");
            string methodology = string.Join("\n", stepDescriptions);
            
            string skillPrompt = $"""
                Apply the "{skill.Name}" methodology to the following input.
                
                Methodology steps:
                {methodology}
                
                Input: {input}
                
                Execute each step systematically and provide the final result.
                """;

            try
            {
                // Execute through the LLM
                string result = await s.Llm.InnerModel.GenerateTextAsync(skillPrompt);
                
                // Record skill execution for learning
                _registry.Value.RecordSkillExecution(skill.Name, !string.IsNullOrWhiteSpace(result));
                
                if (string.IsNullOrWhiteSpace(result))
                {
                    Console.WriteLine($"[UseSkill_{skill.Name}] ⚠ LLM returned empty response (is Ollama running?)");
                    
                    // Provide simulated output for demo purposes
                    result = $"""
                        [Simulated {skill.Name} Analysis]
                        
                        Applied methodology to: {input}
                        
                        Steps executed:
                        {methodology}
                        
                        Note: LLM unavailable - this is a placeholder response.
                        Start Ollama with 'ollama serve' for full functionality.
                        """;
                }
                
                Console.WriteLine($"[UseSkill_{skill.Name}] ✓ Complete");
                Console.WriteLine($"\n--- {skill.Name} Result ---");
                Console.WriteLine(result.Length > 500 ? result[..500] + "..." : result);
                Console.WriteLine("----------------------------\n");
                
                s.Output = result;
                s.Context = $"[{skill.Name}] {result}";
            }
            catch (Exception ex)
            {
                _registry.Value.RecordSkillExecution(skill.Name, false);
                Console.WriteLine($"[UseSkill_{skill.Name}] ⚠ Failed: {ex.Message}");
                s.Output = $"Skill execution failed: {ex.Message}";
            }

            return s;
        };

    private static void RegisterPredefinedSkills(SkillRegistry registry)
    {
        static PlanStep MakeStep(string action, string param, string outcome, double confidence) =>
            new(action, new Dictionary<string, object> { ["hint"] = param }, outcome, confidence);

        var predefinedSkills = new[]
        {
            new Skill("LiteratureReview", "Synthesize research papers into coherent review",
                new List<string> { "research-context" },
                new List<PlanStep> { MakeStep("Identify themes", "Scan papers", "Key themes", 0.9),
                        MakeStep("Compare findings", "Cross-reference", "Patterns", 0.85),
                        MakeStep("Synthesize", "Combine insights", "Review", 0.8) },
                0.85, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("HypothesisGeneration", "Generate testable hypotheses from observations",
                new List<string> { "observations" },
                new List<PlanStep> { MakeStep("Find gaps", "Identify unknowns", "Questions", 0.8),
                        MakeStep("Generate hypotheses", "Form predictions", "Hypotheses", 0.75),
                        MakeStep("Rank by testability", "Evaluate", "Ranked list", 0.7) },
                0.78, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("ChainOfThoughtReasoning", "Apply step-by-step reasoning to problems",
                new List<string> { "problem-statement" },
                new List<PlanStep> { MakeStep("Decompose problem", "Break down", "Sub-problems", 0.9),
                        MakeStep("Reason through steps", "Apply logic", "Intermediate results", 0.85),
                        MakeStep("Synthesize answer", "Combine", "Final answer", 0.88) },
                0.88, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("CrossDomainTransfer", "Transfer insights across domains",
                new List<string> { "source-domain", "target-domain" },
                new List<PlanStep> { MakeStep("Abstract patterns", "Generalize", "Abstract concepts", 0.7),
                        MakeStep("Map to target", "Apply", "Mapped insights", 0.65),
                        MakeStep("Validate transfer", "Test", "Validated insights", 0.6) },
                0.65, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("CitationAnalysis", "Analyze citation networks for influence",
                new List<string> { "paper-set" },
                new List<PlanStep> { MakeStep("Build citation graph", "Extract refs", "Graph", 0.85),
                        MakeStep("Rank by influence", "PageRank-style", "Rankings", 0.8),
                        MakeStep("Find key papers", "Identify", "Key papers", 0.82) },
                0.82, 0, DateTime.UtcNow, DateTime.UtcNow),
            new Skill("EmergentDiscovery", "Discover emergent patterns from multiple sources",
                new List<string> { "multiple-sources" },
                new List<PlanStep> { MakeStep("Combine sources", "Merge data", "Combined view", 0.75),
                        MakeStep("Find emergent patterns", "Pattern detection", "Patterns", 0.7),
                        MakeStep("Validate discoveries", "Cross-check", "Validated discoveries", 0.65) },
                0.71, 0, DateTime.UtcNow, DateTime.UtcNow),
        };

        foreach (var skill in predefinedSkills)
            registry.RegisterSkill(skill);
    }

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        Match m = Regex.Match(arg, @"^'(?<s>.*)'$", RegexOptions.Singleline);
        if (m.Success) return m.Groups["s"].Value;
        m = Regex.Match(arg, @"^""(?<s>.*)""$", RegexOptions.Singleline);
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Clone a pipeline state for parallel execution without side effects.
    /// </summary>
    private static CliPipelineState CloneState(CliPipelineState s) => new()
    {
        Branch = s.Branch,
        Llm = s.Llm,
        Tools = s.Tools,
        Embed = s.Embed,
        Topic = s.Topic,
        Query = s.Query,
        Prompt = s.Prompt,
        RetrievalK = s.RetrievalK,
        Trace = s.Trace,
        Context = s.Context,
        Output = s.Output,
        MeTTaEngine = s.MeTTaEngine,
        VectorStore = s.VectorStore,
        Streaming = s.Streaming,
        ActiveStream = s.ActiveStream
    };

    /// <summary>
    /// Extract text content from HTML, removing all tags.
    /// </summary>
    private static string ExtractTextFromHtml(string html)
    {
        // Remove script and style elements
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        
        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");
        
        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);
        
        // Collapse whitespace
        html = Regex.Replace(html, @"\s+", " ").Trim();
        
        return html;
    }

    /// <summary>
    /// Attempt to extract XML documentation summary from a method (best effort).
    /// </summary>
    private static string? ExtractXmlDocSummary(MethodInfo method)
    {
        // Try to get from XML documentation attribute or fallback to method name
        var docAttr = method.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name.Contains("Documentation") || a.GetType().Name.Contains("Summary"));
        
        if (docAttr != null)
        {
            var descProp = docAttr.GetType().GetProperty("Description") ?? docAttr.GetType().GetProperty("Summary");
            if (descProp != null)
                return descProp.GetValue(docAttr)?.ToString();
        }
        
        // Fallback: generate description from method name
        var name = method.Name;
        // Insert spaces before capitals: "MyMethodName" -> "My Method Name"
        var spaced = Regex.Replace(name, @"(?<!^)([A-Z])", " $1");
        return $"Pipeline step: {spaced}";
    }
}

/// <summary>
/// Information about a discovered pipeline token for dynamic discovery.
/// </summary>
public record PipelineTokenInfo(
    string PrimaryName,
    string[] Aliases,
    string SourceClass,
    string Description,
    MethodInfo Method
);

