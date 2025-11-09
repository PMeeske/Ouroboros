using System.Text;
using MonadicPipeline.Android.Services;
using MonadicPipeline.Android.Views;

namespace MonadicPipeline.Android;

/// <summary>
/// Main page with enhanced CLI-like interface
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly CliExecutor _cliExecutor;
    private readonly StringBuilder _outputHistory;
    private readonly List<string> _commandHistory;
    private int _historyIndex;
    private CommandSuggestionEngine? _suggestionEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        
        // Initialize with database support
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "command_history.db");
        _cliExecutor = new CliExecutor(dbPath);
        
        _outputHistory = new StringBuilder();
        _commandHistory = new List<string>();
        _historyIndex = -1;
        
        _outputHistory.AppendLine("MonadicPipeline CLI v1.0");
        _outputHistory.AppendLine("Enhanced with AI-powered suggestions and Ollama integration");
        _outputHistory.AppendLine("Type 'help' to see available commands");
        _outputHistory.AppendLine();
        _outputHistory.Append("> ");
        UpdateOutput();
        
        // Initialize suggestion engine if available
        try
        {
            var historyService = new CommandHistoryService(dbPath);
            _suggestionEngine = new CommandSuggestionEngine(historyService);
        }
        catch
        {
            // Gracefully handle if suggestions aren't available
            _suggestionEngine = null;
        }

        // Load settings
        LoadSettings();
    }

    private void LoadSettings()
    {
        var endpoint = Preferences.Get("ollama_endpoint", "http://localhost:11434");
        _cliExecutor.OllamaEndpoint = endpoint;
    }

    private async void OnCommandEntered(object? sender, EventArgs e)
    {
        await ExecuteCommand();
    }

    private async void OnExecuteClicked(object? sender, EventArgs e)
    {
        await ExecuteCommand();
    }

    private async void OnCommandTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suggestionEngine == null || !Preferences.Get("auto_suggest", true))
        {
            SuggestionsFrame.IsVisible = false;
            return;
        }

        var text = e.NewTextValue?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            SuggestionsFrame.IsVisible = false;
            return;
        }

        try
        {
            var suggestions = await _suggestionEngine.GetSuggestionsAsync(text, 5);
            
            if (suggestions.Count > 0)
            {
                SuggestionsView.ItemsSource = suggestions;
                SuggestionsFrame.IsVisible = true;
            }
            else
            {
                SuggestionsFrame.IsVisible = false;
            }
        }
        catch
        {
            SuggestionsFrame.IsVisible = false;
        }
    }

    private void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CommandSuggestion suggestion)
        {
            CommandEntry.Text = suggestion.Command;
            SuggestionsFrame.IsVisible = false;
            SuggestionsView.SelectedItem = null;
        }
    }

    private void OnHistoryUpClicked(object? sender, EventArgs e)
    {
        if (_commandHistory.Count == 0)
        {
            return;
        }

        if (_historyIndex < _commandHistory.Count - 1)
        {
            _historyIndex++;
            CommandEntry.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
        }
    }

    private async void OnQuickCommand(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            CommandEntry.Text = button.Text;
            await ExecuteCommand();
        }
    }

    private async void OnModelsClicked(object? sender, EventArgs e)
    {
        try
        {
            var ollamaService = new OllamaService(_cliExecutor.OllamaEndpoint);
            var modelManager = new ModelManager(ollamaService);
            var modelManagerView = new ModelManagerView(modelManager);
            await Navigation.PushAsync(modelManagerView);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open model manager: {ex.Message}", "OK");
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsView = new SettingsView();
        settingsView.SettingsChanged += (s, args) =>
        {
            _cliExecutor.OllamaEndpoint = args.OllamaEndpoint;
            _outputHistory.AppendLine($"Settings updated: Endpoint = {args.OllamaEndpoint}");
            UpdateOutput();
        };
        await Navigation.PushAsync(settingsView);
    }

    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet(
            "Menu",
            "Cancel",
            null,
            "Help",
            "About",
            "Status",
            "Clear Screen",
            "Settings");

        switch (action)
        {
            case "Help":
                CommandEntry.Text = "help";
                await ExecuteCommand();
                break;
            case "About":
                CommandEntry.Text = "about";
                await ExecuteCommand();
                break;
            case "Status":
                CommandEntry.Text = "status";
                await ExecuteCommand();
                break;
            case "Clear Screen":
                CommandEntry.Text = "clear";
                await ExecuteCommand();
                break;
            case "Settings":
                OnSettingsClicked(sender, e);
                break;
        }
    }

    private async Task ExecuteCommand()
    {
        var command = CommandEntry.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        // Add command to history
        _outputHistory.AppendLine(command);
        _outputHistory.AppendLine();

        // Execute command
        var result = await _cliExecutor.ExecuteCommandAsync(command);

        // Handle special commands
        if (result == "CLEAR_SCREEN")
        {
            _outputHistory.Clear();
            _outputHistory.AppendLine("MonadicPipeline CLI");
            _outputHistory.AppendLine();
        }
        else
        {
            _outputHistory.AppendLine(result);
            _outputHistory.AppendLine();
        }

        _outputHistory.Append("> ");
        
        // Update UI
        UpdateOutput();
        CommandEntry.Text = string.Empty;

        // Scroll to bottom
        await Task.Delay(100);
        await OutputScrollView.ScrollToAsync(OutputLabel, ScrollToPosition.End, true);
    }

    private void UpdateOutput()
    {
        OutputLabel.Text = _outputHistory.ToString();
    }
}

