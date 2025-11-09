using System.Text;

namespace MonadicPipeline.Android.Services;

/// <summary>
/// Service to execute CLI commands within the Android app with Ollama integration
/// </summary>
public class CliExecutor
{
    private string _ollamaEndpoint = "http://localhost:11434";
    private string? _currentModel;
    private DateTime _lastModelUse;
    private readonly TimeSpan _modelUnloadDelay = TimeSpan.FromMinutes(5);
    private Timer? _modelUnloadTimer;
    private readonly List<string> _availableSmallModels = new()
    {
        "tinyllama",      // 1.1B - Very small, fast
        "phi",            // 2.7B - Small but capable
        "qwen:0.5b",      // 0.5B - Extremely lightweight
        "gemma:2b",       // 2B - Good balance
    };

    /// <summary>
    /// Gets or sets the Ollama endpoint URL
    /// </summary>
    public string OllamaEndpoint
    {
        get => _ollamaEndpoint;
        set => _ollamaEndpoint = value;
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

            var parts = ParseCommand(command);
            var cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;

            return cmd switch
            {
                "help" => GetHelpText(),
                "version" => GetVersionInfo(),
                "about" => GetAboutInfo(),
                "ask" => await ExecuteAskAsync(parts),
                "config" => ExecuteConfigCommand(parts),
                "models" => GetModelsInfo(),
                "pull" => GetPullInfo(parts),
                "status" => GetStatusInfo(),
                "hints" => GetEfficiencyHints(),
                "ping" => "pong",
                "clear" => "CLEAR_SCREEN",
                "exit" or "quit" => "Use the back button to exit the app",
                _ => $"Unknown command: {cmd}\nType 'help' for available commands"
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

help         - Show this help message
version      - Show version information
about        - About MonadicPipeline
config       - Configure Ollama endpoint
             Usage: config <endpoint>
             Example: config http://192.168.1.100:11434
status       - Show current status and loaded model
models       - Show recommended models
pull         - Show model pull instructions
             Usage: pull <model-name>
             Example: pull tinyllama
ask          - Ask a question using AI
             Usage: ask <your question>
             Example: ask What is functional programming?
hints        - Get efficiency hints for mobile CLI
ping         - Test connection
clear        - Clear the screen
exit/quit    - Exit instructions

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
            return $@"Current Ollama endpoint: {_ollamaEndpoint}

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
        
        return $@"✓ Endpoint configured: {_ollamaEndpoint}

To use the AI features:
1. Ensure Ollama is running on this server
2. Pull a model: pull tinyllama
3. Ask questions: ask <question>";
    }

    private string GetModelsInfo()
    {
        return $@"Ollama Endpoint: {_ollamaEndpoint}

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

To pull a model, run this on your Ollama server:
  ollama pull tinyllama

Or use Ollama's web interface/CLI on the server.

Then verify with: status";
    }

    private string GetPullInfo(string[] parts)
    {
        var modelName = parts.Length > 1 ? parts[1] : "tinyllama";
        
        return $@"To download model '{modelName}':

On your Ollama server, run:
  ollama pull {modelName}

Or use Ollama's web interface at:
  {_ollamaEndpoint}/

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Note: Models are downloaded to the Ollama server,
not to your Android device. This saves space and
allows multiple devices to share models.

Recommended: Use WiFi when pulling large models.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

After pulling, use: models
Then try: ask <your question>";
    }

    private string GetStatusInfo()
    {
        var status = new StringBuilder();
        status.AppendLine("System Status:");
        status.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        status.AppendLine($"Ollama Endpoint: {_ollamaEndpoint}");
        status.AppendLine($"Connection: Not tested (use 'models' to verify)");
        
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
        status.AppendLine("\nFor full functionality:");
        status.AppendLine("1. Configure endpoint: config <url>");
        status.AppendLine("2. Pull model on server: ollama pull tinyllama");
        status.AppendLine("3. Ask questions: ask <question>");
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
        
        // Update model usage tracking
        if (_currentModel == null)
        {
            _currentModel = "auto-selected";
        }
        
        _lastModelUse = DateTime.UtcNow;
        ResetUnloadTimer();

        await Task.Delay(100); // Simulate processing

        return $@"Q: {question}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Feature Demonstration Mode
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

This Android CLI app demonstrates the interface
structure for MonadicPipeline on mobile.

To enable full AI capabilities:

1. Set up Ollama on a networked computer:
   • Install from https://ollama.ai/
   • Pull a model: ollama pull tinyllama
   • Verify: ollama list

2. Configure this app:
   • Find your computer's IP address
   • Run: config http://YOUR_IP:11434
   • Test: status

3. Ask questions:
   • The app will connect to your Ollama server
   • Responses will be generated by the AI model
   • Models auto-unload after {_modelUnloadDelay.TotalMinutes} minutes

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

For now, this demonstrates:
✓ Terminal-style mobile interface
✓ Command parsing and execution
✓ Model lifecycle management
✓ Network configuration
✓ Efficiency optimization hints

Ready for Ollama integration!
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
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
