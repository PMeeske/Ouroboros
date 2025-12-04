using System.Collections.Concurrent;
using System.Text;

namespace Ouroboros.Android.Tests.Infrastructure;

/// <summary>
/// Factory for creating test instances of the MainPage, similar to WebApplicationFactory in ASP.NET Core
/// Provides a way to configure and customize the app for testing
/// </summary>
/// <typeparam name="TStartup">The startup configuration type</typeparam>
public class MauiApplicationFactory<TStartup> : IDisposable where TStartup : class, new()
{
    private readonly TStartup _startup;
    private readonly Dictionary<string, object> _services = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MauiApplicationFactory{TStartup}"/> class.
    /// </summary>
    public MauiApplicationFactory()
    {
        _startup = new TStartup();
    }

    /// <summary>
    /// Gets the services collection for dependency injection
    /// </summary>
    public IReadOnlyDictionary<string, object> Services => _services;

    /// <summary>
    /// Configures services for the test application
    /// </summary>
    public MauiApplicationFactory<TStartup> ConfigureServices(Action<Dictionary<string, object>> configure)
    {
        configure(_services);
        return this;
    }

    /// <summary>
    /// Creates a test server instance that simulates the MAUI app
    /// </summary>
    public TestMauiApplication CreateApplication()
    {
        return new TestMauiApplication(_services);
    }

    /// <summary>
    /// Creates a test client for interacting with the UI
    /// </summary>
    public TestMauiClient CreateClient()
    {
        var app = CreateApplication();
        return new TestMauiClient(app);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _services.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a test instance of the MAUI application
/// Similar to TestServer in ASP.NET Core
/// </summary>
public class TestMauiApplication : IDisposable
{
    private readonly Dictionary<string, object> _services;
    private readonly ConcurrentDictionary<string, TestPage> _pages = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMauiApplication"/> class.
    /// </summary>
    public TestMauiApplication(Dictionary<string, object> services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets a service by type name
    /// </summary>
    public T? GetService<T>() where T : class
    {
        var typeName = typeof(T).Name;
        return _services.TryGetValue(typeName, out var service) ? service as T : null;
    }

    /// <summary>
    /// Navigates to a specific page
    /// </summary>
    public TestPage NavigateToPage(string pageName)
    {
        return _pages.GetOrAdd(pageName, _ => CreatePage(pageName));
    }

    private TestPage CreatePage(string pageName)
    {
        return pageName switch
        {
            "MainPage" => new TestMainPage(_services),
            _ => throw new NotSupportedException($"Page '{pageName}' is not supported in tests")
        };
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var page in _pages.Values)
            {
                page.Dispose();
            }
            _pages.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Base class for test pages
/// </summary>
public abstract class TestPage : IDisposable
{
    protected readonly Dictionary<string, object> Services;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestPage"/> class.
    /// </summary>
    protected TestPage(Dictionary<string, object> services)
    {
        Services = services;
    }

    /// <summary>
    /// Gets the page output/content
    /// </summary>
    public abstract string GetContent();

    /// <summary>
    /// Gets whether the page is loaded and ready
    /// </summary>
    public abstract bool IsLoaded { get; }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            OnDispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Override this to perform cleanup
    /// </summary>
    protected virtual void OnDispose()
    {
    }
}

/// <summary>
/// Test implementation of MainPage
/// </summary>
public class TestMainPage : TestPage
{
    private readonly StringBuilder _outputHistory = new();
    private object? _cliExecutor;
    private object? _suggestionEngine;
    private bool _isLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMainPage"/> class.
    /// </summary>
    public TestMainPage(Dictionary<string, object> services) : base(services)
    {
        Initialize();
    }

    /// <summary>
    /// Gets whether the page is loaded and ready
    /// </summary>
    public override bool IsLoaded => _isLoaded;

    /// <summary>
    /// Gets the CLI executor instance
    /// </summary>
    public object? CliExecutor => _cliExecutor;

    /// <summary>
    /// Gets the suggestion engine instance
    /// </summary>
    public object? SuggestionEngine => _suggestionEngine;

    /// <summary>
    /// Gets the page output/content
    /// </summary>
    public override string GetContent()
    {
        return _outputHistory.ToString();
    }

    /// <summary>
    /// Executes a command in the test MainPage
    /// </summary>
    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (!_isLoaded)
        {
            throw new InvalidOperationException("Page is not loaded");
        }

        _outputHistory.AppendLine(command);
        _outputHistory.AppendLine();

        string result;
        if (_cliExecutor == null)
        {
            result = "Error: CLI executor not initialized. App may be in degraded state.";
        }
        else
        {
            try
            {
                // Simulate command execution
                result = Services.TryGetValue("CommandExecutor", out var executor) && executor is Func<string, Task<string>> func
                    ? await func(command)
                    : $"Executed: {command}";
            }
            catch (Exception ex)
            {
                result = $"Error executing command: {ex.Message}";
            }
        }

        _outputHistory.AppendLine(result);
        _outputHistory.AppendLine();
        _outputHistory.Append("> ");

        return result;
    }

    /// <summary>
    /// Gets the current text in the command entry field
    /// </summary>
    public string CommandEntryText { get; set; } = string.Empty;

    /// <summary>
    /// Simulates clicking a button
    /// </summary>
    public async Task ClickButtonAsync(string buttonText)
    {
        await Task.CompletedTask; // Simulate async operation
        
        switch (buttonText.ToLower())
        {
            case "execute":
                if (!string.IsNullOrWhiteSpace(CommandEntryText))
                {
                    await ExecuteCommandAsync(CommandEntryText);
                    CommandEntryText = string.Empty;
                }
                break;
            case "clear":
                _outputHistory.Clear();
                _outputHistory.AppendLine("Ouroboros CLI");
                _outputHistory.AppendLine();
                _outputHistory.Append("> ");
                break;
            default:
                // Quick command buttons
                CommandEntryText = buttonText;
                await ExecuteCommandAsync(buttonText);
                break;
        }
    }

    /// <summary>
    /// Simulates typing in the command entry
    /// </summary>
    public void TypeCommand(string text)
    {
        CommandEntryText = text;
    }

    /// <summary>
    /// Gets whether the suggestions frame is visible
    /// </summary>
    public bool IsSuggestionsVisible { get; private set; }

    /// <summary>
    /// Gets the current suggestions
    /// </summary>
    public List<string> Suggestions { get; } = new();

    private void Initialize()
    {
        _outputHistory.AppendLine("Ouroboros CLI v1.0");
        _outputHistory.AppendLine("Enhanced with AI-powered suggestions and Ollama integration");
        _outputHistory.AppendLine("Type 'help' to see available commands");
        _outputHistory.AppendLine();

        // Simulate MainPage initialization logic
        try
        {
            // Check if services provide a CLI executor factory
            if (Services.TryGetValue("CliExecutorFactory", out var factory) && factory is Func<string?, object> cliFactory)
            {
                _cliExecutor = cliFactory("/tmp/test_db.db");
            }
            else
            {
                // Default initialization
                _cliExecutor = new object();
            }

            // Try to initialize suggestion engine
            if (Services.TryGetValue("SuggestionEngineFactory", out var sugFactory) && sugFactory is Func<string, object> suggestionFactory)
            {
                try
                {
                    _suggestionEngine = suggestionFactory("/tmp/test_db.db");
                }
                catch (Exception ex)
                {
                    _suggestionEngine = null;
                    _outputHistory.AppendLine($"⚠ Suggestions unavailable: {ex.Message}");
                    _outputHistory.AppendLine();
                }
            }
        }
        catch (Exception ex)
        {
            _outputHistory.AppendLine($"⚠ Initialization error: {ex.Message}");
            _outputHistory.AppendLine("Some features may be unavailable.");
            _outputHistory.AppendLine();

            // Fallback initialization
            try
            {
                if (Services.TryGetValue("CliExecutorFactory", out var fallbackFactory) && fallbackFactory is Func<string?, object> fallbackFunc)
                {
                    _cliExecutor = fallbackFunc(null);
                }
                else
                {
                    _cliExecutor = new object();
                }
            }
            catch
            {
                _cliExecutor = null;
            }
        }

        _outputHistory.Append("> ");
        _isLoaded = true;
    }
}

/// <summary>
/// Client for interacting with the test MAUI application
/// Similar to HttpClient for TestServer in ASP.NET Core
/// </summary>
public class TestMauiClient : IDisposable
{
    private readonly TestMauiApplication _application;
    private TestPage? _currentPage;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMauiClient"/> class.
    /// </summary>
    public TestMauiClient(TestMauiApplication application)
    {
        _application = application;
    }

    /// <summary>
    /// Gets the current page
    /// </summary>
    public TestPage? CurrentPage => _currentPage;

    /// <summary>
    /// Navigates to a page
    /// </summary>
    public async Task<TestPage> NavigateAsync(string pageName)
    {
        await Task.CompletedTask; // Simulate async navigation
        _currentPage = _application.NavigateToPage(pageName);
        return _currentPage;
    }

    /// <summary>
    /// Navigates to MainPage
    /// </summary>
    public async Task<TestMainPage> NavigateToMainPageAsync()
    {
        var page = await NavigateAsync("MainPage");
        return (TestMainPage)page;
    }

    /// <summary>
    /// Gets the content of the current page
    /// </summary>
    public string GetPageContent()
    {
        if (_currentPage == null)
        {
            throw new InvalidOperationException("No page is currently loaded");
        }

        return _currentPage.GetContent();
    }

    /// <summary>
    /// Waits for the page to be fully loaded
    /// </summary>
    public async Task WaitForPageLoadAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var start = DateTime.UtcNow;

        while (_currentPage == null || !_currentPage.IsLoaded)
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Page failed to load within the specified timeout");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _application?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Default startup configuration for tests
/// </summary>
public class TestStartup
{
}

/// <summary>
/// Extension methods for test setup
/// </summary>
public static class TestMauiExtensions
{
    /// <summary>
    /// Configures a mock CLI executor factory
    /// </summary>
    public static MauiApplicationFactory<TStartup> WithMockCliExecutor<TStartup>(
        this MauiApplicationFactory<TStartup> factory,
        Func<string?, object> executorFactory) where TStartup : class, new()
    {
        return factory.ConfigureServices(services =>
        {
            services["CliExecutorFactory"] = executorFactory;
        });
    }

    /// <summary>
    /// Configures a mock suggestion engine factory
    /// </summary>
    public static MauiApplicationFactory<TStartup> WithMockSuggestionEngine<TStartup>(
        this MauiApplicationFactory<TStartup> factory,
        Func<string, object> engineFactory) where TStartup : class, new()
    {
        return factory.ConfigureServices(services =>
        {
            services["SuggestionEngineFactory"] = engineFactory;
        });
    }

    /// <summary>
    /// Configures a custom command executor
    /// </summary>
    public static MauiApplicationFactory<TStartup> WithCommandExecutor<TStartup>(
        this MauiApplicationFactory<TStartup> factory,
        Func<string, Task<string>> executor) where TStartup : class, new()
    {
        return factory.ConfigureServices(services =>
        {
            services["CommandExecutor"] = executor;
        });
    }

    /// <summary>
    /// Configures the factory to simulate initialization failures
    /// </summary>
    public static MauiApplicationFactory<TStartup> WithInitializationFailure<TStartup>(
        this MauiApplicationFactory<TStartup> factory,
        string errorMessage) where TStartup : class, new()
    {
        return factory.WithMockCliExecutor(_ => throw new Exception(errorMessage));
    }
}
