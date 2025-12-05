// Ouroboros CLI - Research Skills DSL Application
using System.Net.Http;
using System.Xml.Linq;

// Handle --help / -h flag
if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
{
    ShowCliUsage();
    return;
}

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         🐍 OUROBOROS CLI - Research-Powered DSL Pipeline                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize components
using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
var skillRegistry = new SkillRegistry();
var dslExtension = new SkillBasedDslExtension(skillRegistry);

// Register predefined research skills
Console.WriteLine("  🔧 Initializing research skill registry...\n");
skillRegistry.RegisterPredefinedSkills();
dslExtension.RefreshSkillTokens();

// Show available commands
ShowHelp();

// Main CLI loop
Console.WriteLine();
while (true)
{
    Console.Write("  ouroboros> ");
    string? input = Console.ReadLine()?.Trim();
    
    if (string.IsNullOrEmpty(input)) continue;
    
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\n  👋 Goodbye!\n");
        break;
    }
    
    if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
    {
        ShowHelp();
        continue;
    }
    
    if (input.Equals("skills", StringComparison.OrdinalIgnoreCase))
    {
        ShowSkills(skillRegistry);
        continue;
    }
    
    if (input.Equals("tokens", StringComparison.OrdinalIgnoreCase))
    {
        ShowTokens(dslExtension);
        continue;
    }
    
    if (input.StartsWith("fetch ", StringComparison.OrdinalIgnoreCase))
    {
        string query = input.Substring(6).Trim();
        await FetchResearchAsync(httpClient, query, skillRegistry, dslExtension);
        continue;
    }
    
    if (input.StartsWith("run ", StringComparison.OrdinalIgnoreCase))
    {
        string pipeline = input.Substring(4).Trim();
        await ExecutePipelineAsync(pipeline, skillRegistry);
        continue;
    }
    
    if (input.StartsWith("suggest ", StringComparison.OrdinalIgnoreCase))
    {
        string goal = input.Substring(8).Trim();
        SuggestSkills(goal, skillRegistry);
        continue;
    }
    
    Console.WriteLine($"     ⚠ Unknown command: {input}. Type 'help' for commands.\n");
}

// === Helper Methods ===

void ShowHelp()
{
    Console.WriteLine("  ┌──────────────────────────────────────────────────────────────────────────┐");
    Console.WriteLine("  │  📚 AVAILABLE COMMANDS                                                  │");
    Console.WriteLine("  ├──────────────────────────────────────────────────────────────────────────┤");
    Console.WriteLine("  │  help                    - Show this help message                       │");
    Console.WriteLine("  │  skills                  - List all registered skills                   │");
    Console.WriteLine("  │  tokens                  - List available DSL tokens                    │");
    Console.WriteLine("  │  fetch <query>           - Fetch research & extract new skills          │");
    Console.WriteLine("  │  run <pipeline>          - Execute a DSL pipeline                       │");
    Console.WriteLine("  │  suggest <goal>          - Get skill suggestions for a goal             │");
    Console.WriteLine("  │  exit                    - Exit the CLI                                 │");
    Console.WriteLine("  └──────────────────────────────────────────────────────────────────────────┘");
    Console.WriteLine();
    Console.WriteLine("  📝 EXAMPLE PIPELINES:");
    Console.WriteLine("     run SetPrompt \"analyze AI\" | UseSkill_LiteratureReview | UseOutput");
    Console.WriteLine("     run SetPrompt \"math problem\" | UseSkill_ChainOfThoughtReasoning");
}

void ShowSkills(SkillRegistry registry)
{
    Console.WriteLine("\n  📚 REGISTERED SKILLS:");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────────────");
    
    foreach (var skill in registry.GetAllSkills())
    {
        Console.WriteLine($"     🔧 {skill.Name,-28} ({skill.SuccessRate:P0})");
        Console.WriteLine($"        {skill.Description}");
        Console.WriteLine($"        Steps: {string.Join(" → ", skill.Steps.Select(s => s.Action.Split(' ')[0]))}");
        Console.WriteLine();
    }
}

void ShowTokens(SkillBasedDslExtension ext)
{
    Console.WriteLine("\n  🏷️ AVAILABLE DSL TOKENS:");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────────────");
    Console.WriteLine("     Built-in: SetPrompt, UseDraft, UseCritique, UseRevise, UseOutput");
    Console.WriteLine();
    Console.WriteLine("     Skill-based (auto-generated from research):");
    
    foreach (var token in ext.GetTokenNames())
        Console.WriteLine($"        • {token}");
    Console.WriteLine();
}

async Task FetchResearchAsync(HttpClient client, string query, SkillRegistry registry, SkillBasedDslExtension ext)
{
    Console.WriteLine($"\n  🔍 Fetching research on: \"{query}\"...\n");
    
    try
    {
        string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(query)}&max_results=5";
        string xml = await client.GetStringAsync(url);
        XDocument doc = XDocument.Parse(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace arxivNs = "http://arxiv.org/schemas/atom";
        
        var papers = new List<(string Title, string Category)>();
        
        foreach (var entry in doc.Descendants(atom + "entry"))
        {
            string title = entry.Element(atom + "title")?.Value?.Replace("\n", " ").Trim() ?? "";
            string cat = entry.Element(arxivNs + "primary_category")?.Attribute("term")?.Value ?? "cs.AI";
            papers.Add((title, cat));
        }
        
        Console.WriteLine($"  📄 Found {papers.Count} papers:");
        foreach (var (title, cat) in papers.Take(3))
            Console.WriteLine($"     • [{cat}] {title.Substring(0, Math.Min(55, title.Length))}...");
        
        // Extract methodology and create new skill
        if (papers.Count > 0)
        {
            string skillName = SanitizeName(query) + "Analysis";
            
            var skill = new Skill(
                Name: skillName,
                Description: $"Apply {query} methodology patterns",
                Prerequisites: new List<string> { "Input context" },
                Steps: new List<PlanStep>
                {
                    new("Analyze input", new(), "Analysis", 0.9),
                    new($"Apply {query} patterns", new(), "Patterns", 0.85),
                    new("Synthesize results", new(), "Output", 0.8),
                },
                SuccessRate: 0.75,
                UsageCount: 0,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow);
            
            registry.RegisterSkill(skill);
            ext.RefreshSkillTokens();
            
            Console.WriteLine($"\n  ✅ New skill extracted: UseSkill_{skillName}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ⚠ Error: {ex.Message}");
    }
    Console.WriteLine();
}

async Task ExecutePipelineAsync(string pipeline, SkillRegistry registry)
{
    Console.WriteLine($"\n  ▶ Executing: {pipeline}\n");
    
    var steps = pipeline.Split('|').Select(s => s.Trim()).ToList();
    string currentOutput = "";
    
    foreach (var step in steps)
    {
        Console.WriteLine($"  📍 {step}");
        
        if (step.StartsWith("SetPrompt", StringComparison.OrdinalIgnoreCase))
        {
            int start = step.IndexOf('"') + 1;
            int end = step.LastIndexOf('"');
            if (start > 0 && end > start)
            {
                currentOutput = step.Substring(start, end - start);
                Console.WriteLine($"     ✓ Prompt: \"{currentOutput}\"");
            }
        }
        else if (step.StartsWith("UseSkill_", StringComparison.OrdinalIgnoreCase))
        {
            string skillName = step.Split(' ')[0].Replace("UseSkill_", "");
            var skill = registry.GetSkill(skillName);
            
            if (skill != null)
            {
                Console.WriteLine($"     🔧 Executing: {skillName}");
                foreach (var planStep in skill.Steps)
                {
                    Console.WriteLine($"        → {planStep.Action}");
                    await Task.Delay(200);
                }
                currentOutput = $"[{skillName} result for: {currentOutput}]";
                Console.WriteLine($"     ✓ Complete");
                registry.RecordExecution(skillName, true);
            }
            else
            {
                Console.WriteLine($"     ⚠ Skill not found: {skillName}");
            }
        }
        else if (step.Equals("UseOutput", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"     📤 {currentOutput}");
        }
        Console.WriteLine();
    }
    Console.WriteLine("  ✅ Pipeline complete\n");
}

void SuggestSkills(string goal, SkillRegistry registry)
{
    Console.WriteLine($"\n  💡 Suggestions for: \"{goal}\"\n");
    
    var matches = registry.GetAllSkills()
        .Where(s => s.Description.Contains(goal, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(goal, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(s => s.SuccessRate)
        .Take(3)
        .ToList();
    
    if (matches.Count == 0)
        matches = registry.GetAllSkills().OrderByDescending(s => s.SuccessRate).Take(3).ToList();
    
    foreach (var skill in matches)
    {
        Console.WriteLine($"     🎯 UseSkill_{skill.Name} ({skill.SuccessRate:P0})");
        Console.WriteLine($"        {skill.Description}");
    }
    
    if (matches.Count > 0)
        Console.WriteLine($"\n  Example: run SetPrompt \"{goal}\" | UseSkill_{matches[0].Name} | UseOutput\n");
}

string SanitizeName(string name) => new string(name.Split(' ', '-').Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..].ToLower() : "").SelectMany(w => w).Where(char.IsLetterOrDigit).ToArray());

// === Types ===
record Skill(string Name, string Description, List<string> Prerequisites, List<PlanStep> Steps, double SuccessRate, int UsageCount, DateTime CreatedAt, DateTime LastUsed);
record PlanStep(string Action, Dictionary<string, object> Parameters, string ExpectedOutcome, double ConfidenceScore);

class SkillRegistry
{
    private readonly Dictionary<string, Skill> _skills = new(StringComparer.OrdinalIgnoreCase);
    public void RegisterSkill(Skill skill) => _skills[skill.Name] = skill;
    public Skill? GetSkill(string name) => _skills.TryGetValue(name, out var s) ? s : null;
    public IEnumerable<Skill> GetAllSkills() => _skills.Values;
    public void RecordExecution(string name, bool success) { if (_skills.TryGetValue(name, out var s)) _skills[name] = s with { UsageCount = s.UsageCount + 1, LastUsed = DateTime.UtcNow }; }
    
    public void RegisterPredefinedSkills()
    {
        RegisterSkill(new("LiteratureReview", "Synthesize research papers into coherent review", new() { "Papers" }, new() { new("Identify themes", new(), "Themes", 0.9), new("Compare findings", new(), "Comparison", 0.85), new("Synthesize", new(), "Review", 0.8) }, 0.85, 0, DateTime.UtcNow, DateTime.UtcNow));
        RegisterSkill(new("HypothesisGeneration", "Generate testable hypotheses from observations", new() { "Observations" }, new() { new("Find patterns", new(), "Patterns", 0.9), new("Generate explanations", new(), "Hypotheses", 0.8), new("Rank", new(), "Ranked", 0.85) }, 0.78, 0, DateTime.UtcNow, DateTime.UtcNow));
        RegisterSkill(new("ChainOfThoughtReasoning", "Apply step-by-step reasoning to problems", new() { "Problem" }, new() { new("Decompose", new(), "Sub-problems", 0.9), new("Reason step-by-step", new(), "Chain", 0.85), new("Synthesize answer", new(), "Solution", 0.8) }, 0.88, 0, DateTime.UtcNow, DateTime.UtcNow));
        RegisterSkill(new("CrossDomainTransfer", "Transfer insights across domains", new() { "Source", "Target" }, new() { new("Abstract principles", new(), "Principles", 0.8), new("Map to target", new(), "Mapping", 0.7), new("Validate", new(), "Validation", 0.75) }, 0.65, 0, DateTime.UtcNow, DateTime.UtcNow));
        RegisterSkill(new("CitationAnalysis", "Analyze citation networks for influence", new() { "Papers" }, new() { new("Build graph", new(), "Graph", 0.9), new("Rank influence", new(), "Rankings", 0.85), new("Find trends", new(), "Trends", 0.8) }, 0.82, 0, DateTime.UtcNow, DateTime.UtcNow));
        RegisterSkill(new("EmergentDiscovery", "Discover emergent patterns from sources", new() { "Findings" }, new() { new("Combine insights", new(), "Combined", 0.8), new("Find emergence", new(), "Patterns", 0.7), new("Validate", new(), "Validated", 0.75) }, 0.71, 0, DateTime.UtcNow, DateTime.UtcNow));
    }
}

class SkillBasedDslExtension
{
    private readonly SkillRegistry _registry;
    private readonly List<string> _tokens = new();
    public SkillBasedDslExtension(SkillRegistry r) => _registry = r;
    public void RefreshSkillTokens() { _tokens.Clear(); foreach (var s in _registry.GetAllSkills()) _tokens.Add($"UseSkill_{s.Name}"); }
    public IEnumerable<string> GetTokenNames() => _tokens;
}
