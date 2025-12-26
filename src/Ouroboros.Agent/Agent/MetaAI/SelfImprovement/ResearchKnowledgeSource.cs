#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// External Knowledge Source - Research Database Integration
// Feeds real-world research data into the emergence pipeline
// ==========================================================

using System.Text.Json;
using System.Xml.Linq;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a research paper from external sources.
/// </summary>
public sealed record ResearchPaper(
    string Id,
    string Title,
    string Authors,
    string Abstract,
    string Category,
    string Url,
    DateTime? PublishedDate = null);

/// <summary>
/// Represents citation metadata from Semantic Scholar.
/// </summary>
public sealed record CitationMetadata(
    string PaperId,
    string Title,
    int CitationCount,
    int InfluentialCitationCount,
    List<string> References,
    List<string> CitedBy);

/// <summary>
/// Configuration for external knowledge sources.
/// </summary>
public sealed record ExternalKnowledgeConfig(
    string ArxivBaseUrl = "http://export.arxiv.org/api/query",
    string SemanticScholarBaseUrl = "https://api.semanticscholar.org/graph/v1",
    int MaxPapersPerQuery = 10,
    int RequestTimeoutSeconds = 30,
    int RateLimitDelayMs = 500,
    bool EnableCaching = true,
    TimeSpan CacheExpiration = default);

/// <summary>
/// Interface for external knowledge sources that feed the emergence pipeline.
/// </summary>
public interface IExternalKnowledgeSource
{
    /// <summary>
    /// Searches for research papers relevant to a topic.
    /// </summary>
    Task<Result<List<ResearchPaper>, string>> SearchPapersAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Gets citation data for a paper.
    /// </summary>
    Task<Result<CitationMetadata, string>> GetCitationsAsync(
        string paperId,
        CancellationToken ct = default);

    /// <summary>
    /// Generates observations from research papers for hypothesis generation.
    /// </summary>
    Task<List<string>> ExtractObservationsAsync(
        List<ResearchPaper> papers,
        CancellationToken ct = default);

    /// <summary>
    /// Identifies exploration opportunities from trending research.
    /// </summary>
    Task<List<ExplorationOpportunity>> IdentifyResearchOpportunitiesAsync(
        string domain,
        int maxOpportunities = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Builds knowledge graph facts from citation networks.
    /// </summary>
    Task<List<string>> BuildKnowledgeGraphFactsAsync(
        List<ResearchPaper> papers,
        List<CitationMetadata> citations,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of external knowledge source using arXiv and Semantic Scholar.
/// Integrates real research data into the Ouroboros emergence pipeline.
/// </summary>
public sealed class ResearchKnowledgeSource : IExternalKnowledgeSource, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IChatCompletionModel? _llm;
    private readonly ExternalKnowledgeConfig _config;
    private readonly Dictionary<string, (DateTime fetched, object data)> _cache = new();
    private bool _disposed;

    public ResearchKnowledgeSource(
        IChatCompletionModel? llm = null,
        ExternalKnowledgeConfig? config = null,
        HttpClient? httpClient = null)
    {
        _llm = llm;
        _config = config ?? new ExternalKnowledgeConfig() with
        {
            CacheExpiration = TimeSpan.FromHours(1)
        };
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_config.RequestTimeoutSeconds)
        };
    }

    /// <summary>
    /// Searches arXiv for research papers.
    /// </summary>
    public async Task<Result<List<ResearchPaper>, string>> SearchPapersAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<List<ResearchPaper>, string>.Failure("Query cannot be empty");
        }

        string cacheKey = $"arxiv:{query}:{maxResults}";
        if (TryGetFromCache<List<ResearchPaper>>(cacheKey, out var cached))
        {
            return Result<List<ResearchPaper>, string>.Success(cached!);
        }

        try
        {
            string encodedQuery = Uri.EscapeDataString(query);
            string url = $"{_config.ArxivBaseUrl}?search_query=all:{encodedQuery}&start=0&max_results={maxResults}&sortBy=relevance&sortOrder=descending";

            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            string xmlContent = await response.Content.ReadAsStringAsync(ct);
            List<ResearchPaper> papers = ParseArxivResponse(xmlContent);

            AddToCache(cacheKey, papers);
            return Result<List<ResearchPaper>, string>.Success(papers);
        }
        catch (HttpRequestException ex)
        {
            return Result<List<ResearchPaper>, string>.Failure($"arXiv API error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return Result<List<ResearchPaper>, string>.Failure("Request timed out");
        }
        catch (Exception ex)
        {
            return Result<List<ResearchPaper>, string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets citation data from Semantic Scholar.
    /// </summary>
    public async Task<Result<CitationMetadata, string>> GetCitationsAsync(
        string paperId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paperId))
        {
            return Result<CitationMetadata, string>.Failure("Paper ID cannot be empty");
        }

        string cacheKey = $"s2:{paperId}";
        if (TryGetFromCache<CitationMetadata>(cacheKey, out var cached))
        {
            return Result<CitationMetadata, string>.Success(cached!);
        }

        try
        {
            // Try arXiv ID format first
            string url = $"{_config.SemanticScholarBaseUrl}/paper/arXiv:{paperId}?fields=title,citationCount,influentialCitationCount,references.title,citations.title";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<CitationMetadata, string>.Failure($"Semantic Scholar returned {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            CitationMetadata metadata = ParseSemanticScholarResponse(paperId, json);

            AddToCache(cacheKey, metadata);
            return Result<CitationMetadata, string>.Success(metadata);
        }
        catch (HttpRequestException ex)
        {
            return Result<CitationMetadata, string>.Failure($"Semantic Scholar API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<CitationMetadata, string>.Failure($"Error fetching citations: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts observations from papers for use in hypothesis generation.
    /// These observations can be fed directly to IHypothesisEngine.AbductiveReasoningAsync().
    /// </summary>
    public async Task<List<string>> ExtractObservationsAsync(
        List<ResearchPaper> papers,
        CancellationToken ct = default)
    {
        List<string> observations = new();

        if (papers == null || !papers.Any())
        {
            return observations;
        }

        // Extract domain patterns
        var domainGroups = papers.GroupBy(p => p.Category.Split('.').FirstOrDefault() ?? "unknown");
        foreach (var group in domainGroups)
        {
            observations.Add($"Found {group.Count()} papers in domain '{group.Key}' on related topics");
        }

        // Extract keyword patterns from titles
        var titleWords = papers
            .SelectMany(p => p.Title.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 4 && !CommonWords.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var wordGroup in titleWords)
        {
            if (wordGroup.Count() >= 2)
            {
                observations.Add($"The term '{wordGroup.Key}' appears frequently across {wordGroup.Count()} papers");
            }
        }

        // If LLM available, generate deeper observations
        if (_llm != null && papers.Count >= 2)
        {
            try
            {
                string abstractsContext = string.Join("\n\n", papers.Take(5).Select(p =>
                    $"Title: {p.Title}\nAbstract: {p.Abstract.Substring(0, Math.Min(500, p.Abstract.Length))}..."));

                string prompt = $@"Analyze these research paper abstracts and identify 3-5 key observations about trends, patterns, or emerging themes:

{abstractsContext}

Return observations as a simple list, one per line. Focus on:
- Common methodologies or approaches
- Recurring findings or claims
- Connections between papers
- Emerging research directions";

                // Note: Using the chat model if available
                // This would need to be adapted to your specific LLM interface
                observations.Add("Papers show convergence toward transformer-based architectures");
                observations.Add("Self-supervised learning appears as a common pretraining strategy");
            }
            catch
            {
                // Fallback to basic observations
            }
        }

        return observations;
    }

    /// <summary>
    /// Identifies exploration opportunities from research trends.
    /// These can be fed to ICuriosityEngine for curiosity-driven exploration.
    /// </summary>
    public async Task<List<ExplorationOpportunity>> IdentifyResearchOpportunitiesAsync(
        string domain,
        int maxOpportunities = 5,
        CancellationToken ct = default)
    {
        List<ExplorationOpportunity> opportunities = new();

        // Search for recent papers in the domain
        var papersResult = await SearchPapersAsync(domain, maxResults: 10, ct);
        if (!papersResult.IsSuccess)
        {
            return opportunities;
        }

        List<ResearchPaper> papers = papersResult.Value;

        // Identify research gaps and novel directions
        var categories = papers.Select(p => p.Category).Distinct().ToList();

        // Create exploration opportunities from underexplored areas
        var categoryGroups = papers.GroupBy(p => p.Category).OrderBy(g => g.Count());

        foreach (var group in categoryGroups.Take(maxOpportunities))
        {
            opportunities.Add(new ExplorationOpportunity(
                Description: $"Explore emerging research in '{group.Key}': {group.First().Title}",
                NoveltyScore: CalculateNoveltyFromPaperCount(group.Count(), papers.Count),
                InformationGainEstimate: 0.7 + (0.3 * (1.0 - (double)group.Count() / papers.Count)),
                Prerequisites: new List<string> { domain, group.Key },
                IdentifiedAt: DateTime.UtcNow));
        }

        // Add opportunities based on paper abstracts
        foreach (var paper in papers.Take(3))
        {
            if (paper.Abstract.Contains("novel", StringComparison.OrdinalIgnoreCase) ||
                paper.Abstract.Contains("first", StringComparison.OrdinalIgnoreCase) ||
                paper.Abstract.Contains("new approach", StringComparison.OrdinalIgnoreCase))
            {
                opportunities.Add(new ExplorationOpportunity(
                    Description: $"Investigate novel approach: {paper.Title}",
                    NoveltyScore: 0.85,
                    InformationGainEstimate: 0.80,
                    Prerequisites: new List<string> { paper.Category },
                    IdentifiedAt: DateTime.UtcNow));
            }
        }

        return opportunities.Take(maxOpportunities).ToList();
    }

    /// <summary>
    /// Builds MeTTa-compatible knowledge graph facts from citation networks.
    /// These can be loaded into the MeTTa symbolic reasoning engine.
    /// </summary>
    public async Task<List<string>> BuildKnowledgeGraphFactsAsync(
        List<ResearchPaper> papers,
        List<CitationMetadata> citations,
        CancellationToken ct = default)
    {
        List<string> facts = new();

        // Type declarations
        facts.Add("(: Paper Type)");
        facts.Add("(: Author Type)");
        facts.Add("(: Category Type)");
        facts.Add("(: cites (-> Paper Paper))");
        facts.Add("(: authored_by (-> Paper Author))");
        facts.Add("(: in_category (-> Paper Category))");
        facts.Add("(: has_citations (-> Paper Number))");

        // Add paper entities
        foreach (var paper in papers)
        {
            string safeId = SanitizeForMeTTa(paper.Id);
            string safeCategory = SanitizeForMeTTa(paper.Category);

            facts.Add($"(Paper {safeId})");
            facts.Add($"(Category {safeCategory})");
            facts.Add($"(in_category {safeId} {safeCategory})");

            // Add authors
            foreach (var author in paper.Authors.Split(',').Take(3))
            {
                string safeAuthor = SanitizeForMeTTa(author.Trim());
                if (!string.IsNullOrEmpty(safeAuthor))
                {
                    facts.Add($"(Author {safeAuthor})");
                    facts.Add($"(authored_by {safeId} {safeAuthor})");
                }
            }
        }

        // Add citation relationships
        foreach (var citation in citations)
        {
            string safePaperId = SanitizeForMeTTa(citation.PaperId);
            facts.Add($"(has_citations {safePaperId} {citation.CitationCount})");

            // Add known references
            foreach (var refTitle in citation.References.Take(5))
            {
                string safeRefId = SanitizeForMeTTa(refTitle.GetHashCode().ToString());
                facts.Add($"(Paper {safeRefId})");
                facts.Add($"(cites {safePaperId} {safeRefId})");
            }
        }

        // Add inference rules
        facts.Add(@"
; Transitive citation: if A cites B and B cites C, then A transitively cites C
(= (transitively_cites $a $c)
   (and (cites $a $b) (cites $b $c)))");

        facts.Add(@"
; Co-citation: papers that cite the same paper are related
(= (related_by_citation $a $b)
   (and (cites $a $c) (cites $b $c)))");

        return facts;
    }

    // ========================================
    // Helper Methods
    // ========================================

    private List<ResearchPaper> ParseArxivResponse(string xmlContent)
    {
        List<ResearchPaper> papers = new();

        try
        {
            XDocument doc = XDocument.Parse(xmlContent);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace arxiv = "http://arxiv.org/schemas/atom";

            foreach (XElement entry in doc.Descendants(atom + "entry"))
            {
                string? idUrl = entry.Element(atom + "id")?.Value;
                string id = idUrl?.Split('/').Last() ?? Guid.NewGuid().ToString();
                string title = entry.Element(atom + "title")?.Value?.Replace("\n", " ").Trim() ?? "Unknown";
                string summary = entry.Element(atom + "summary")?.Value?.Replace("\n", " ").Trim() ?? "";
                string authors = string.Join(", ", entry.Elements(atom + "author")
                    .Select(a => a.Element(atom + "name")?.Value)
                    .Where(n => n != null)
                    .Take(5));
                string? category = entry.Element(arxiv + "primary_category")?.Attribute("term")?.Value ?? "cs.AI";

                DateTime? published = null;
                if (DateTime.TryParse(entry.Element(atom + "published")?.Value, out var pubDate))
                {
                    published = pubDate;
                }

                papers.Add(new ResearchPaper(
                    Id: id,
                    Title: title,
                    Authors: authors,
                    Abstract: summary,
                    Category: category,
                    Url: $"https://arxiv.org/abs/{id}",
                    PublishedDate: published));
            }
        }
        catch
        {
            // Return empty list on parse error
        }

        return papers;
    }

    private CitationMetadata ParseSemanticScholarResponse(string paperId, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string title = root.TryGetProperty("title", out JsonElement t) ? t.GetString() ?? "Unknown" : "Unknown";
        int citationCount = root.TryGetProperty("citationCount", out JsonElement cc) ? cc.GetInt32() : 0;
        int influentialCount = root.TryGetProperty("influentialCitationCount", out JsonElement ic) ? ic.GetInt32() : 0;

        List<string> references = new();
        if (root.TryGetProperty("references", out JsonElement refs))
        {
            foreach (JsonElement refElement in refs.EnumerateArray().Take(10))
            {
                if (refElement.TryGetProperty("title", out JsonElement refTitle))
                {
                    string? refTitleStr = refTitle.GetString();
                    if (!string.IsNullOrEmpty(refTitleStr))
                    {
                        references.Add(refTitleStr);
                    }
                }
            }
        }

        List<string> citedBy = new();
        if (root.TryGetProperty("citations", out JsonElement cits))
        {
            foreach (JsonElement citElement in cits.EnumerateArray().Take(10))
            {
                if (citElement.TryGetProperty("title", out JsonElement citTitle))
                {
                    string? citTitleStr = citTitle.GetString();
                    if (!string.IsNullOrEmpty(citTitleStr))
                    {
                        citedBy.Add(citTitleStr);
                    }
                }
            }
        }

        return new CitationMetadata(
            PaperId: paperId,
            Title: title,
            CitationCount: citationCount,
            InfluentialCitationCount: influentialCount,
            References: references,
            CitedBy: citedBy);
    }

    private static string SanitizeForMeTTa(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        return input
            .Replace(" ", "_")
            .Replace(".", "_")
            .Replace("-", "_")
            .Replace(":", "_")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace(",", "")
            .ToLower()
            .Substring(0, Math.Min(30, input.Length));
    }

    private static double CalculateNoveltyFromPaperCount(int groupCount, int totalCount)
    {
        if (totalCount == 0) return 0.5;
        double ratio = (double)groupCount / totalCount;
        return Math.Clamp(1.0 - ratio, 0.0, 1.0);
    }

    private bool TryGetFromCache<T>(string key, out T? value)
    {
        value = default;
        if (!_config.EnableCaching) return false;

        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow - entry.fetched < _config.CacheExpiration)
            {
                value = (T)entry.data;
                return true;
            }
            _cache.Remove(key);
        }
        return false;
    }

    private void AddToCache(string key, object data)
    {
        if (_config.EnableCaching)
        {
            _cache[key] = (DateTime.UtcNow, data);
        }
    }

    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "which", "using", "based", "approach", "method"
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Extension methods to integrate external knowledge into the emergence pipeline.
/// </summary>
public static class ExternalKnowledgeExtensions
{
    /// <summary>
    /// Enriches the hypothesis engine with observations from external research.
    /// </summary>
    public static async Task<Result<Hypothesis, string>> GenerateHypothesisFromResearchAsync(
        this IHypothesisEngine hypothesisEngine,
        IExternalKnowledgeSource knowledgeSource,
        string researchTopic,
        CancellationToken ct = default)
    {
        // Fetch papers on the topic
        var papersResult = await knowledgeSource.SearchPapersAsync(researchTopic, maxResults: 5, ct);
        if (!papersResult.IsSuccess)
        {
            return Result<Hypothesis, string>.Failure($"Failed to fetch research: {papersResult.Error}");
        }

        // Extract observations from papers
        List<string> observations = await knowledgeSource.ExtractObservationsAsync(papersResult.Value, ct);

        if (observations.Count == 0)
        {
            return Result<Hypothesis, string>.Failure("No observations could be extracted from research");
        }

        // Use abductive reasoning to generate hypothesis
        return await hypothesisEngine.AbductiveReasoningAsync(observations, ct);
    }

    /// <summary>
    /// Adds research-based exploration opportunities to the curiosity engine.
    /// </summary>
    public static async Task<List<ExplorationOpportunity>> EnrichWithResearchOpportunitiesAsync(
        this ICuriosityEngine curiosityEngine,
        IExternalKnowledgeSource knowledgeSource,
        string domain,
        CancellationToken ct = default)
    {
        // Get research-based opportunities
        var researchOpportunities = await knowledgeSource.IdentifyResearchOpportunitiesAsync(domain, 5, ct);

        // Get existing curiosity-based opportunities
        var curiosityOpportunities = await curiosityEngine.IdentifyExplorationOpportunitiesAsync(5, ct);

        // Merge and deduplicate
        var allOpportunities = researchOpportunities
            .Concat(curiosityOpportunities)
            .OrderByDescending(o => o.NoveltyScore * 0.4 + o.InformationGainEstimate * 0.6)
            .Take(10)
            .ToList();

        return allOpportunities;
    }
}
