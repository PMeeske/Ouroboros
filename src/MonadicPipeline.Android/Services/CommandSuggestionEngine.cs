namespace MonadicPipeline.Android.Services;

/// <summary>
/// Service for providing intelligent command suggestions
/// </summary>
public class CommandSuggestionEngine
{
    private readonly CommandHistoryService _historyService;
    private readonly Dictionary<string, List<string>> _commandParameters;
    private readonly Dictionary<string, string> _commandDescriptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandSuggestionEngine"/> class.
    /// </summary>
    /// <param name="historyService">Command history service</param>
    public CommandSuggestionEngine(CommandHistoryService historyService)
    {
        _historyService = historyService;
        _commandParameters = InitializeCommandParameters();
        _commandDescriptions = InitializeCommandDescriptions();
    }

    /// <summary>
    /// Get command suggestions based on partial input
    /// </summary>
    /// <param name="partialCommand">Partial command text</param>
    /// <param name="maxSuggestions">Maximum suggestions to return</param>
    /// <returns>List of suggested commands</returns>
    public async Task<List<CommandSuggestion>> GetSuggestionsAsync(string partialCommand, int maxSuggestions = 5)
    {
        var suggestions = new List<CommandSuggestion>();

        if (string.IsNullOrWhiteSpace(partialCommand))
        {
            // Return most frequent commands from history
            var stats = await _historyService.GetCommandStatisticsAsync();
            var topCommands = stats.OrderByDescending(kvp => kvp.Value)
                .Take(maxSuggestions)
                .Select(kvp => new CommandSuggestion
                {
                    Command = kvp.Key,
                    Description = _commandDescriptions.GetValueOrDefault(kvp.Key, string.Empty),
                    Confidence = 0.8,
                    Source = "history"
                });

            suggestions.AddRange(topCommands);
        }
        else
        {
            // Fuzzy match against known commands
            var parts = partialCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return suggestions;
            }

            var commandName = parts[0].ToLowerInvariant();

            // Check for exact or prefix matches
            foreach (var (cmd, desc) in _commandDescriptions)
            {
                if (cmd.StartsWith(commandName, StringComparison.OrdinalIgnoreCase))
                {
                    var confidence = cmd.Equals(commandName, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.9;
                    suggestions.Add(new CommandSuggestion
                    {
                        Command = cmd,
                        Description = desc,
                        Confidence = confidence,
                        Source = "builtin"
                    });
                }
            }

            // Add fuzzy matches from history
            if (parts.Length == 1)
            {
                var historyMatches = await _historyService.SearchHistoryAsync(commandName, 10);
                foreach (var entry in historyMatches)
                {
                    var similarity = CalculateSimilarity(commandName, entry.Command);
                    if (similarity > 0.5)
                    {
                        suggestions.Add(new CommandSuggestion
                        {
                            Command = entry.Command,
                            Description = "From history",
                            Confidence = similarity * 0.7,
                            Source = "history"
                        });
                    }
                }
            }

            // Suggest parameters for known commands
            if (parts.Length > 1 && _commandParameters.ContainsKey(commandName))
            {
                var parameters = _commandParameters[commandName];
                var currentParam = parts[^1];

                foreach (var param in parameters)
                {
                    if (param.StartsWith(currentParam, StringComparison.OrdinalIgnoreCase))
                    {
                        var fullCommand = string.Join(" ", parts.Take(parts.Length - 1)) + " " + param;
                        suggestions.Add(new CommandSuggestion
                        {
                            Command = fullCommand,
                            Description = $"Parameter for {commandName}",
                            Confidence = 0.85,
                            Source = "parameter"
                        });
                    }
                }
            }
        }

        return suggestions
            .OrderByDescending(s => s.Confidence)
            .Take(maxSuggestions)
            .ToList();
    }

    /// <summary>
    /// Get parameter suggestions for a command
    /// </summary>
    /// <param name="command">The command name</param>
    /// <returns>List of parameter suggestions</returns>
    public List<string> GetParameterSuggestions(string command)
    {
        var cmdName = command.Split(' ')[0].ToLowerInvariant();
        return _commandParameters.GetValueOrDefault(cmdName, new List<string>());
    }

    /// <summary>
    /// Calculate similarity between two strings (Levenshtein distance based)
    /// </summary>
    private double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0;
        }

        var maxLength = Math.Max(a.Length, b.Length);
        if (maxLength == 0)
        {
            return 1;
        }

        var distance = LevenshteinDistance(a.ToLowerInvariant(), b.ToLowerInvariant());
        return 1.0 - (distance / (double)maxLength);
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    private int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (var i = 0; i <= n; d[i, 0] = i++) { }
        for (var j = 0; j <= m; d[0, j] = j++) { }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = b[j - 1] == a[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    /// <summary>
    /// Initialize command parameters dictionary
    /// </summary>
    private Dictionary<string, List<string>> InitializeCommandParameters()
    {
        return new Dictionary<string, List<string>>
        {
            ["config"] = new List<string> { "http://localhost:11434", "http://192.168.1.100:11434" },
            ["pull"] = new List<string> { "tinyllama", "phi", "qwen:0.5b", "gemma:2b", "llama2" },
            ["ask"] = new List<string> { "What is", "How do", "Explain", "Tell me about" },
            ["shell"] = new List<string> { "ls", "ps", "df", "top", "free" },
            ["ollama"] = new List<string> { "start", "stop", "status", "restart" }
        };
    }

    /// <summary>
    /// Initialize command descriptions dictionary
    /// </summary>
    private Dictionary<string, string> InitializeCommandDescriptions()
    {
        return new Dictionary<string, string>
        {
            ["help"] = "Show available commands",
            ["version"] = "Show version information",
            ["about"] = "About MonadicPipeline",
            ["config"] = "Configure Ollama endpoint",
            ["status"] = "Show current status",
            ["models"] = "List available models",
            ["pull"] = "Download a model",
            ["ask"] = "Ask AI a question",
            ["hints"] = "Show efficiency hints",
            ["ping"] = "Test connection",
            ["clear"] = "Clear the screen",
            ["exit"] = "Exit the app",
            ["quit"] = "Exit the app",
            ["history"] = "Show command history",
            ["shell"] = "Execute shell command",
            ["ollama"] = "Manage Ollama service"
        };
    }
}

/// <summary>
/// Command suggestion with metadata
/// </summary>
public class CommandSuggestion
{
    /// <summary>
    /// Gets or sets the suggested command
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the suggestion source
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
