using System.Text;

namespace MonadicPipeline.Android.Services;

/// <summary>
/// Enhanced service to execute CLI commands within the Android app with full Ollama integration
/// </summary>
public class CliExecutor
{
    private readonly OllamaService _ollamaService;
    private readonly ModelManager _modelManager;
    private readonly CommandExecutor _commandExecutor;
    private readonly CommandHistoryService? _historyService;
    private readonly CommandSuggestionEngine? _suggestionEngine;
    
    private string? _currentModel;
    private DateTime _lastModelUse;
    private readonly TimeSpan _modelUnloadDelay = TimeSpan.FromMinutes(5);
    private Timer? _modelUnloadTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliExecutor"/> class.
    /// </summary>
    /// <param name="databasePath">Optional path to SQLite database for history</param>
    public CliExecutor(string? databasePath = null)
    {
        _ollamaService = new OllamaService("http://localhost:11434");
        _modelManager = new ModelManager(_ollamaService);
        _commandExecutor = new CommandExecutor(requiresRoot: false);
        
        if (!string.IsNullOrEmpty(databasePath))
        {
            try
            {
                _historyService = new CommandHistoryService(databasePath);
                _suggestionEngine = new CommandSuggestionEngine(_historyService);
            }
            catch
            {
                // Gracefully handle database initialization failures
                _historyService = null;
                _suggestionEngine = null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the Ollama endpoint URL
    /// </summary>
    public string OllamaEndpoint
    {
        get => _ollamaService.BaseUrl;
        set => _ollamaService.BaseUrl = value;
    }

    /// <summary>
    /// Execute a CLI command and return the output
    /// </summary>
    public async Task<string> ExecuteCommandAsync(string command)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return "Error: Empty command";
            }

            // Add to history
            if (_historyService != null)
            {
                await _historyService.AddCommandAsync(command);
            }

            var parts = ParseCommand(command);
            var cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;

            return cmd switch
            {
                "help" => GetHelpText(),
                "version" => GetVersionInfo(),
                "about" => GetAboutInfo(),
                "ask" => await ExecuteAskAsync(parts),
                "config" => ExecuteConfigCommand(parts),
                "models" => await GetModelsInfoAsync(),
                "pull" => await ExecutePullAsync(parts),
                "delete" => await ExecuteDeleteAsync(parts),
                "status" => await GetStatusInfoAsync(),
                "hints" => GetEfficiencyHints(),
                "suggest" => await GetSuggestionsAsync(parts),
                "history" => await GetHistoryAsync(parts),
                "shell" => await ExecuteShellAsync(parts),
                "ollama" => await ExecuteOllamaCommandAsync(parts),
                "ping" => await TestConnectionAsync(),
                "clear" => "CLEAR_SCREEN",
                "exit" or "quit" => "Use the back button to exit the app",
                _ => await HandleUnknownCommandAsync(cmd, parts)
            };
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }

    private string GetHelpText()
    {
        return @"Available Commands:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

System Commands:
  help         - Show this help message
  version      - Show version information
  about        - About MonadicPipeline
  clear        - Clear the screen
  exit/quit    - Exit instructions

Ollama Configuration:
  config       - Configure Ollama endpoint
               Usage: config <endpoint>
               Example: config http://192.168.1.100:11434
  status       - Show current status and loaded model
  ping         - Test connection to Ollama

Model Management:
  models       - List available models from Ollama
  pull         - Download a model from Ollama
               Usage: pull <model-name>
               Example: pull tinyllama
  delete       - Delete a model
               Usage: delete <model-name>

AI Interaction:
  ask          - Ask a question using AI
               Usage: ask <your question>
               Example: ask What is functional programming?

Intelligence & History:
  suggest      - Get command suggestions
               Usage: suggest [partial command]
  history      - Show recent command history
               Usage: history [count]
  hints        - Get efficiency hints for mobile CLI

Advanced:
  shell        - Execute native shell command
               Usage: shell <command>
               Example: shell ls -la
  ollama       - Manage Ollama service (if supported)
               Usage: ollama <start|stop|status>

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Recommended small models for Android:
  • tinyllama (1.1B) - Very fast
  • phi (2.7B) - Good balance
  • qwen:0.5b (0.5B) - Ultra lightweight
  • gemma:2b (2B) - Capable and efficient
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }

    private string GetVersionInfo()
    {
        return @"MonadicPipeline CLI v1.0.0
.NET 8.0
Platform: Android (MAUI)
LangChain: 0.17.0
Ollama: Integrated

Built with functional programming principles
and category theory foundations.";
    }

    private string GetAboutInfo()
    {
        return @"MonadicPipeline
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

A sophisticated functional programming-based 
AI pipeline system built on LangChain.

Features:
• Monadic Composition
• Kleisli Arrows  
• Type-Safe Pipelines
• Event Sourcing
• Vector Storage
• AI Orchestration
• Ollama Integration
• Automatic Model Management

Developed by Adaptive Systems Inc.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

GitHub: PMeeske/MonadicPipeline
License: Open Source";
    }

    private string ExecuteConfigCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            return $@"Current Ollama endpoint: {OllamaEndpoint}

Usage: config <endpoint>
Example: config http://192.168.1.100:11434

Note: Endpoint should point to an Ollama server
accessible from this device.";
        }

        var newEndpoint = parts[1];
        if (!newEndpoint.StartsWith("http"))
        {
            return "Error: Endpoint must start with http:// or https://";
        }

        OllamaEndpoint = newEndpoint;
        
        return $@"✓ Endpoint configured: {OllamaEndpoint}

To use the AI features:
1. Ensure Ollama is running on this server
2. Pull a model: pull tinyllama
3. Ask questions: ask <question>";
    }

    private async Task<string> GetModelsInfoAsync()
    {
        try
        {
            var models = await _modelManager.GetAvailableModelsAsync();
            
            if (models.Count == 0)
            {
                return $@"Ollama Endpoint: {OllamaEndpoint}

No models found. Pull a model first:
  pull tinyllama

Or check connection with: ping";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Ollama Endpoint: {OllamaEndpoint}");
            sb.AppendLine();
            sb.AppendLine("Available Models:");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var model in models)
            {
                sb.AppendLine($"• {model.Name}");
                sb.AppendLine($"  Size: {model.FormattedSize}");
                if (model.IsRecommended)
                {
                    sb.AppendLine("  ⭐ Recommended for mobile");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("Use 'ask <question>' to chat with a model");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $@"Error listing models: {ex.Message}

Recommended Models for Android:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

tinyllama (1.1B parameters)
  • Very fast responses
  • Low memory usage (~1.1GB)
  • Best for: Quick questions, simple tasks

phi (2.7B parameters)
  • Good reasoning capabilities  
  • Moderate memory usage (~2.7GB)
  • Best for: Code help, explanations

qwen:0.5b (0.5B parameters)
  • Ultra lightweight
  • Minimal memory (~0.5GB)
  • Best for: Basic queries, testing

gemma:2b (2B parameters)
  • Capable and efficient
  • Moderate memory (~2GB)
  • Best for: General purpose

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

To pull a model:
  pull tinyllama

Then verify with: models";
        }
    }

    private async Task<string> ExecutePullAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            return @"Usage: pull <model-name>
Examples:
  pull tinyllama
  pull phi
  pull qwen:0.5b

This will download the model from Ollama.
Use 'models' to see recommended models.";
        }

        var modelName = parts[1];
        
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pulling model: {modelName}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("This may take a few minutes...");
            sb.AppendLine();

            await _modelManager.PullModelAsync(modelName, progress =>
            {
                // Progress updates could be streamed in a real implementation
            });

            sb.AppendLine();
            sb.AppendLine($"✓ Model '{modelName}' pulled successfully!");
            sb.AppendLine();
            sb.AppendLine("Try it out:");
            sb.AppendLine($"  ask What is functional programming?");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $@"Error pulling model: {ex.Message}

Note: Make sure:
1. Ollama server is running
2. You're connected to the network
3. The model name is correct

Use 'models' to see available models.";
        }
    }

    private async Task<string> ExecuteDeleteAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            return @"Usage: delete <model-name>
Example: delete tinyllama

This will remove the model from Ollama.
Use 'models' to see available models.";
        }

        var modelName = parts[1];
        
        try
        {
            await _modelManager.DeleteModelAsync(modelName);
            return $@"✓ Model '{modelName}' deleted successfully!

Use 'models' to see remaining models.";
        }
        catch (Exception ex)
        {
            return $"Error deleting model: {ex.Message}";
        }
    }

    private async Task<string> GetStatusInfoAsync()
    {
        var status = new StringBuilder();
        status.AppendLine("System Status:");
        status.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        status.AppendLine($"Ollama Endpoint: {OllamaEndpoint}");
        
        // Test connection
        var connected = await _ollamaService.TestConnectionAsync();
        status.AppendLine($"Connection: {(connected ? "✓ Connected" : "✗ Failed")}");
        
        if (connected)
        {
            try
            {
                var models = await _modelManager.GetAvailableModelsAsync();
                status.AppendLine($"Available Models: {models.Count}");
            }
            catch
            {
                status.AppendLine("Available Models: Unknown");
            }
        }
        
        if (_currentModel != null)
        {
            status.AppendLine($"Current Model: {_currentModel}");
            var timeSinceUse = DateTime.UtcNow - _lastModelUse;
            status.AppendLine($"Last Use: {timeSinceUse.TotalMinutes:F1} minutes ago");
            status.AppendLine($"Auto-unload: {_modelUnloadDelay.TotalMinutes} minutes");
        }
        else
        {
            status.AppendLine("Current Model: Not loaded");
            status.AppendLine("Tip: Use 'ask' to automatically load a model");
        }
        
        status.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        if (!connected)
        {
            status.AppendLine("\nTo get started:");
            status.AppendLine("1. Configure endpoint: config <url>");
            status.AppendLine("2. Pull model: pull tinyllama");
            status.AppendLine("3. Ask questions: ask <question>");
        }
        else
        {
            status.AppendLine("\nTip: Use 'models' to see available models");
        }
        
        return status.ToString();
    }

    private string GetEfficiencyHints()
    {
        return @"Efficiency Hints for Mobile CLI:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Model Selection:
  • Use smallest model that meets your needs
  • tinyllama: Quick answers, simple tasks
  • phi: Code snippets, explanations
  • gemma:2b: More complex reasoning
  
Memory Management:
  • Models run on Ollama server (not device)
  • Only connection data uses device memory
  • Use 'clear' to free UI memory periodically
  
Network Tips:
  • Configure local Ollama server on WiFi
  • Example: config http://192.168.1.x:11434
  • Avoid mobile data for frequent queries
  • Keep questions concise for faster responses
  
Battery Optimization:
  • Smaller models = less network = longer battery
  • Close app when done to save power
  • Use device power saver for extended sessions
  
Server Setup:
  • Run Ollama on a local PC or server
  • Access via local network (no internet needed)
  • Share server across multiple devices
  • Pull models once, use everywhere
  
Best Practices:
  • Keep questions focused and specific
  • Use 'hints' for task-specific guidance
  • Monitor network usage in device settings
  • Test with 'ping' before long queries
  
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }

    private async Task<string> ExecuteAskAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            return "Usage: ask <your question>\nExample: ask What is functional programming?";
        }

        var question = string.Join(" ", parts.Skip(1));
        
        try
        {
            // Auto-select smallest model if none loaded
            if (_currentModel == null)
            {
                _currentModel = await _modelManager.GetSmallestAvailableModelAsync();
                
                if (_currentModel == null)
                {
                    return @"No models available. Pull a model first:
  pull tinyllama

Then try your question again.";
                }
            }
            
            _lastModelUse = DateTime.UtcNow;
            ResetUnloadTimer();

            var sb = new StringBuilder();
            sb.AppendLine($"Q: {question}");
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Using model: {_currentModel}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();

            // Generate response
            var response = await _ollamaService.GenerateAsync(_currentModel, question);
            
            sb.AppendLine("A: " + response);
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Model will auto-unload after {_modelUnloadDelay.TotalMinutes} minutes of inactivity");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $@"Error generating response: {ex.Message}

Troubleshooting:
1. Check connection: ping
2. Verify endpoint: status
3. List models: models
4. Pull a model: pull tinyllama";
        }
    }

    private void ResetUnloadTimer()
    {
        _modelUnloadTimer?.Dispose();
        _modelUnloadTimer = new Timer(_ =>
        {
            // Unload model if not used for configured time
            if (_currentModel != null && DateTime.UtcNow - _lastModelUse >= _modelUnloadDelay)
            {
                _currentModel = null;
                _modelUnloadTimer?.Dispose();
                _modelUnloadTimer = null;
            }
        }, null, _modelUnloadDelay, TimeSpan.FromMinutes(1));
    }

    private async Task<string> GetSuggestionsAsync(string[] parts)
    {
        if (_suggestionEngine == null)
        {
            return "Suggestion engine not available (database not initialized).";
        }

        var partialCommand = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
        
        try
        {
            var suggestions = await _suggestionEngine.GetSuggestionsAsync(partialCommand, 10);
            
            if (suggestions.Count == 0)
            {
                return "No suggestions found. Type 'help' to see available commands.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Command Suggestions:");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var suggestion in suggestions)
            {
                sb.AppendLine($"• {suggestion.Command}");
                if (!string.IsNullOrEmpty(suggestion.Description))
                {
                    sb.AppendLine($"  {suggestion.Description}");
                }
                sb.AppendLine($"  Confidence: {suggestion.Confidence:P0} | Source: {suggestion.Source}");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting suggestions: {ex.Message}";
        }
    }

    private async Task<string> GetHistoryAsync(string[] parts)
    {
        if (_historyService == null)
        {
            return "Command history not available (database not initialized).";
        }

        var count = 20;
        if (parts.Length > 1 && int.TryParse(parts[1], out var requestedCount))
        {
            count = Math.Min(requestedCount, 100);
        }

        try
        {
            var history = await _historyService.GetRecentHistoryAsync(count);
            
            if (history.Count == 0)
            {
                return "No command history yet.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Recent Command History (last {history.Count}):");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var entry in history)
            {
                sb.AppendLine($"[{entry.ExecutedAt:yyyy-MM-dd HH:mm:ss}] {entry.Command}");
            }
            
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("\nUse 'suggest' to get command suggestions based on history.");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving history: {ex.Message}";
        }
    }

    private async Task<string> ExecuteShellAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            return @"Usage: shell <command>
Examples:
  shell ls -la
  shell ps aux
  shell df -h

⚠️  Warning: Shell commands run with app permissions only.
Root access is not available by default.";
        }

        var shellCommand = string.Join(" ", parts.Skip(1));
        
        // Validate command
        var validation = _commandExecutor.ValidateCommand(shellCommand);
        if (!validation.IsValid)
        {
            return $"⚠️  Command validation failed: {validation.Message}";
        }

        try
        {
            var result = await _commandExecutor.ExecuteAsync(shellCommand);
            
            var sb = new StringBuilder();
            sb.AppendLine($"$ {shellCommand}");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            if (!string.IsNullOrEmpty(result.Output))
            {
                sb.AppendLine(result.Output);
            }
            
            if (!string.IsNullOrEmpty(result.Error))
            {
                sb.AppendLine("STDERR:");
                sb.AppendLine(result.Error);
            }
            
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"Exit Code: {result.ExitCode}");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error executing shell command: {ex.Message}";
        }
    }

    private async Task<string> ExecuteOllamaCommandAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            return @"Usage: ollama <start|stop|status|restart>
Examples:
  ollama status
  ollama start
  ollama stop

Note: Ollama service management requires Termux or similar.
This feature may not work on all devices.";
        }

        var subCommand = parts[1].ToLowerInvariant();
        
        return subCommand switch
        {
            "status" => await GetOllamaServiceStatusAsync(),
            "start" => await StartOllamaServiceAsync(),
            "stop" => await StopOllamaServiceAsync(),
            "restart" => await RestartOllamaServiceAsync(),
            _ => $"Unknown ollama command: {subCommand}\nUse: ollama <start|stop|status|restart>"
        };
    }

    private async Task<string> GetOllamaServiceStatusAsync()
    {
        var connected = await _ollamaService.TestConnectionAsync();
        
        if (connected)
        {
            return @"✓ Ollama service is running and accessible

Use 'models' to see available models.";
        }
        else
        {
            return $@"✗ Cannot connect to Ollama service at {OllamaEndpoint}

Possible reasons:
1. Ollama is not running
2. Wrong endpoint configured (use 'config' to fix)
3. Network connection issues

To start Ollama on Termux:
  ollama serve";
        }
    }

    private async Task<string> StartOllamaServiceAsync()
    {
        return @"⚠️  Ollama service management not fully implemented yet.

To start Ollama manually:
1. Install Termux from F-Droid
2. Install Ollama in Termux:
   pkg install ollama
3. Start the service:
   ollama serve

Then configure this app:
  config http://localhost:11434";
    }

    private async Task<string> StopOllamaServiceAsync()
    {
        return @"⚠️  Ollama service management not fully implemented yet.

To stop Ollama manually:
1. Find the Ollama process:
   ps aux | grep ollama
2. Kill the process:
   kill <pid>";
    }

    private async Task<string> RestartOllamaServiceAsync()
    {
        return @"⚠️  Ollama service management not fully implemented yet.

To restart Ollama manually:
1. Stop the service (see 'ollama stop')
2. Start it again (see 'ollama start')";
    }

    private async Task<string> TestConnectionAsync()
    {
        try
        {
            var connected = await _ollamaService.TestConnectionAsync();
            
            if (connected)
            {
                return $"✓ Successfully connected to Ollama at {OllamaEndpoint}";
            }
            else
            {
                return $"✗ Cannot connect to Ollama at {OllamaEndpoint}\n\nCheck endpoint with: config";
            }
        }
        catch (Exception ex)
        {
            return $"✗ Connection test failed: {ex.Message}";
        }
    }

    private async Task<string> HandleUnknownCommandAsync(string cmd, string[] parts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Unknown command: {cmd}");
        sb.AppendLine();
        
        // Try to suggest similar commands
        if (_suggestionEngine != null)
        {
            try
            {
                var suggestions = await _suggestionEngine.GetSuggestionsAsync(cmd, 3);
                if (suggestions.Count > 0)
                {
                    sb.AppendLine("Did you mean:");
                    foreach (var suggestion in suggestions)
                    {
                        sb.AppendLine($"  • {suggestion.Command}");
                    }
                    sb.AppendLine();
                }
            }
            catch
            {
                // Ignore suggestion errors
            }
        }
        
        sb.AppendLine("Type 'help' for available commands");
        
        return sb.ToString();
    }

    private string[] ParseCommand(string command)
    {
        // Simple command parser - split by spaces but respect quotes
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (var c in command)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts.ToArray();
    }
}
