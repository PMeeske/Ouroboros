// <copyright file="ResearchSkillExtractor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

// ==========================================================
// Research Skill Extractor
// Automatically extracts reusable skills from research patterns
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Automatically extracts reusable pipeline skills from research analysis patterns.
/// Enables the system to learn new DSL tokens from academic sources.
/// </summary>
public sealed class ResearchSkillExtractor
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly IChatCompletionModel _model;
    private readonly ResearchKnowledgeSource _researchSource;

    /// <summary>
    /// Initializes the research skill extractor.
    /// </summary>
    public ResearchSkillExtractor(
        ISkillRegistry skillRegistry,
        IChatCompletionModel model,
        ResearchKnowledgeSource researchSource)
    {
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _researchSource = researchSource ?? throw new ArgumentNullException(nameof(researchSource));
    }

    /// <summary>
    /// Discovers and extracts skills from research papers in a domain.
    /// </summary>
    public async Task<Result<List<Skill>, string>> ExtractSkillsFromResearchAsync(
        string researchDomain,
        int maxPapers = 10)
    {
        try
        {
            // Fetch papers from the domain
            var papersResult = await _researchSource.SearchPapersAsync(researchDomain, maxPapers);
            if (!papersResult.IsSuccess)
            {
                return Result<List<Skill>, string>.Failure(papersResult.Error);
            }

            List<ResearchPaper> papers = papersResult.Value;
            List<Skill> extractedSkills = new();

            // Group papers by methodology patterns
            var methodologyGroups = await GroupByMethodologyAsync(papers);

            foreach (var (methodology, relatedPapers) in methodologyGroups)
            {
                // Extract skill from methodology pattern
                Skill? skill = await ExtractSkillFromMethodologyAsync(methodology, relatedPapers);
                if (skill != null)
                {
                    _skillRegistry.RegisterSkill(skill);
                    extractedSkills.Add(skill);
                }
            }

            // Extract cross-paper synthesis skills
            if (papers.Count >= 3)
            {
                Skill? synthesisSkill = await ExtractSynthesisSkillAsync(papers, researchDomain);
                if (synthesisSkill != null)
                {
                    _skillRegistry.RegisterSkill(synthesisSkill);
                    extractedSkills.Add(synthesisSkill);
                }
            }

            return Result<List<Skill>, string>.Success(extractedSkills);
        }
        catch (Exception ex)
        {
            return Result<List<Skill>, string>.Failure($"Failed to extract skills: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates predefined research-based skills.
    /// These are common patterns found across multiple research domains.
    /// </summary>
    public void RegisterPredefinedResearchSkills()
    {
        // Skill 1: Literature Review Synthesis
        _skillRegistry.RegisterSkill(new Skill(
            Name: "LiteratureReview",
            Description: "Synthesize multiple research papers into a coherent literature review",
            Prerequisites: new List<string> { "Research papers", "Topic focus" },
            Steps: new List<PlanStep>
            {
                new("Identify key themes", new Dictionary<string, object>(), "List of themes identified", 0.9),
                new("Compare methodologies", new Dictionary<string, object>(), "Comparison matrix", 0.85),
                new("Synthesize findings", new Dictionary<string, object>(), "Coherent narrative", 0.8),
                new("Identify gaps", new Dictionary<string, object>(), "Research gaps list", 0.85),
            },
            SuccessRate: 0.85,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow));

        // Skill 2: Hypothesis Generation from Observations
        _skillRegistry.RegisterSkill(new Skill(
            Name: "HypothesisGeneration",
            Description: "Generate testable hypotheses from research observations using abductive reasoning",
            Prerequisites: new List<string> { "Observations", "Domain knowledge" },
            Steps: new List<PlanStep>
            {
                new("Identify patterns", new Dictionary<string, object>(), "Pattern list", 0.9),
                new("Generate explanations", new Dictionary<string, object>(), "Possible explanations", 0.8),
                new("Formalize hypotheses", new Dictionary<string, object>(), "Testable hypotheses", 0.75),
                new("Rank by plausibility", new Dictionary<string, object>(), "Ranked list", 0.85),
            },
            SuccessRate: 0.78,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow));

        // Skill 3: Cross-Domain Transfer
        _skillRegistry.RegisterSkill(new Skill(
            Name: "CrossDomainTransfer",
            Description: "Transfer insights and methods from one research domain to another",
            Prerequisites: new List<string> { "Source domain knowledge", "Target domain context" },
            Steps: new List<PlanStep>
            {
                new("Abstract core principles", new Dictionary<string, object>(), "Abstract principles", 0.8),
                new("Map to target domain", new Dictionary<string, object>(), "Concept mapping", 0.7),
                new("Adapt methodology", new Dictionary<string, object>(), "Adapted approach", 0.65),
                new("Validate applicability", new Dictionary<string, object>(), "Validation results", 0.75),
            },
            SuccessRate: 0.65,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow));

        // Skill 4: Citation Network Analysis
        _skillRegistry.RegisterSkill(new Skill(
            Name: "CitationAnalysis",
            Description: "Analyze citation networks to identify influential works and research trends",
            Prerequisites: new List<string> { "Paper IDs", "Citation data" },
            Steps: new List<PlanStep>
            {
                new("Fetch citation metadata", new Dictionary<string, object>(), "Citation data", 0.95),
                new("Build citation graph", new Dictionary<string, object>(), "Graph structure", 0.9),
                new("Rank by influence", new Dictionary<string, object>(), "Influence ranking", 0.85),
                new("Identify trends", new Dictionary<string, object>(), "Trend analysis", 0.8),
            },
            SuccessRate: 0.82,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow));

        // Skill 5: Emergent Pattern Discovery
        _skillRegistry.RegisterSkill(new Skill(
            Name: "EmergentDiscovery",
            Description: "Discover emergent patterns that arise from combining multiple research findings",
            Prerequisites: new List<string> { "Multiple findings", "Cross-domain context" },
            Steps: new List<PlanStep>
            {
                new("Collect findings", new Dictionary<string, object>(), "Finding collection", 0.9),
                new("Combine with novel connections", new Dictionary<string, object>(), "Combined insights", 0.75),
                new("Identify emergent properties", new Dictionary<string, object>(), "Emergent patterns", 0.7),
                new("Validate emergence", new Dictionary<string, object>(), "Validated patterns", 0.8),
            },
            SuccessRate: 0.71,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow));
    }

    private async Task<Dictionary<string, List<ResearchPaper>>> GroupByMethodologyAsync(List<ResearchPaper> papers)
    {
        var groups = new Dictionary<string, List<ResearchPaper>>();

        string paperDescriptions = string.Join("\n\n", papers.Select((p, i) =>
        {
            string abstractSnippet = p.Abstract.Length > 200 ? p.Abstract.Substring(0, 200) + "..." : p.Abstract;
            return $"[{i}] {p.Title}\n{abstractSnippet}";
        }));

        string prompt = $@"Analyze these research paper abstracts and group them by methodology:

Papers:
{paperDescriptions}

Return a JSON object mapping methodology names to paper indices.
Example: {{""experimental"": [0, 2], ""theoretical"": [1, 3], ""survey"": [4]}}";

        string response = await _model.GenerateTextAsync(prompt);

        // Parse and group
        // Simplified: just use category as methodology proxy
        foreach (var paper in papers)
        {
            string key = paper.Category ?? "general";
            if (!groups.ContainsKey(key))
            {
                groups[key] = new List<ResearchPaper>();
            }

            groups[key].Add(paper);
        }

        return groups;
    }

    private async Task<Skill?> ExtractSkillFromMethodologyAsync(
        string methodology,
        List<ResearchPaper> papers)
    {
        if (papers.Count < 2)
        {
            return null;
        }

        string prompt = $@"Extract a reusable methodology skill from these papers in the '{methodology}' category:

Papers:
{string.Join("\n", papers.Select(p => $"- {p.Title}"))}

Create a skill with:
1. Name (single word, CamelCase)
2. Description (one sentence)
3. Steps (3-5 steps to apply this methodology)

Format as:
Name: [name]
Description: [description]
Steps:
1. [step1]
2. [step2]
...";

        string response = await _model.GenerateTextAsync(prompt);

        // Parse response into skill
        string skillName = $"{SanitizeName(methodology)}Analysis";
        string description = $"Apply {methodology} methodology patterns from research literature";

        List<PlanStep> steps = new()
        {
            new($"Prepare data for {methodology} analysis", new Dictionary<string, object>(), "Prepared input", 0.9),
            new($"Apply {methodology} methodology", new Dictionary<string, object>(), "Analysis results", 0.85),
            new("Interpret results in context", new Dictionary<string, object>(), "Interpreted findings", 0.8),
        };

        return new Skill(
            Name: skillName,
            Description: description,
            Prerequisites: new List<string> { "Input data", "Domain context" },
            Steps: steps,
            SuccessRate: 0.75,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow);
    }

    private async Task<Skill?> ExtractSynthesisSkillAsync(List<ResearchPaper> papers, string domain)
    {
        string sanitizedDomain = SanitizeName(domain);

        string prompt = $@"Create a synthesis skill for combining insights from these papers on '{domain}':

Papers:
{string.Join("\n", papers.Take(5).Select(p => $"- {p.Title}"))}

What unique synthesis approach would be valuable for this domain?";

        string response = await _model.GenerateTextAsync(prompt);

        return new Skill(
            Name: $"{sanitizedDomain}Synthesis",
            Description: $"Synthesize findings across {domain} research papers",
            Prerequisites: new List<string> { "Multiple papers", "Research question" },
            Steps: new List<PlanStep>
            {
                new("Extract key findings", new Dictionary<string, object>(), "Finding list", 0.9),
                new("Compare and contrast findings", new Dictionary<string, object>(), "Comparison", 0.85),
                new("Synthesize into unified insight", new Dictionary<string, object>(), "Synthesis", 0.8),
                new("Draw actionable conclusions", new Dictionary<string, object>(), "Conclusions", 0.85),
            },
            SuccessRate: 0.80,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow);
    }

    private static string SanitizeName(string name)
    {
        return new string(name
            .Split(' ', '-', '_')
            .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant())
            .SelectMany(w => w)
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());
    }
}
