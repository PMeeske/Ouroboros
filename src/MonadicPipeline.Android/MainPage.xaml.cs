using System.Text;
using MonadicPipeline.Android.Services;

namespace MonadicPipeline.Android;

/// <summary>
/// Main page with CLI-like interface
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly CliExecutor _cliExecutor;
    private readonly StringBuilder _outputHistory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        _cliExecutor = new CliExecutor();
        _outputHistory = new StringBuilder();
        _outputHistory.AppendLine("MonadicPipeline CLI");
        _outputHistory.AppendLine("Type 'help' to see available commands");
        _outputHistory.AppendLine();
        _outputHistory.Append("> ");
        UpdateOutput();
    }

    private async void OnCommandEntered(object? sender, EventArgs e)
    {
        await ExecuteCommand();
    }

    private async void OnExecuteClicked(object? sender, EventArgs e)
    {
        await ExecuteCommand();
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

