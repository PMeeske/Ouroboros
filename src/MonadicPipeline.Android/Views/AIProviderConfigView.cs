using Microsoft.Maui.Controls;
using MonadicPipeline.Android.Services;

namespace MonadicPipeline.Android.Views;

/// <summary>
/// View for configuring AI providers
/// </summary>
public class AIProviderConfigView : ContentPage
{
    private readonly AIProviderService _providerService;
    private readonly Picker _providerPicker;
    private readonly Entry _endpointEntry;
    private readonly Entry _apiKeyEntry;
    private readonly Entry _modelEntry;
    private readonly Entry _organizationEntry;
    private readonly Entry _projectEntry;
    private readonly Entry _regionEntry;
    private readonly Entry _deploymentEntry;
    private readonly Slider _temperatureSlider;
    private readonly Label _temperatureLabel;
    private readonly Slider _maxTokensSlider;
    private readonly Label _maxTokensLabel;
    private readonly Switch _enabledSwitch;
    private readonly Label _statusLabel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConfigView"/> class.
    /// </summary>
    public AIProviderConfigView()
    {
        _providerService = new AIProviderService();
        
        Title = "AI Provider Configuration";
        BackgroundColor = Color.FromRgb(30, 30, 30);

        // Provider Selection
        var providerLabel = new Label
        {
            Text = "AI Provider",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 5)
        };

        _providerPicker = new Picker
        {
            Title = "Select Provider",
            TextColor = Color.FromRgb(0, 255, 0),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            Margin = new Thickness(10, 0, 10, 10)
        };

        foreach (AIProvider provider in Enum.GetValues(typeof(AIProvider)))
        {
            var config = AIProviderConfig.GetDefault(provider);
            _providerPicker.Items.Add(config.GetDisplayName());
        }

        _providerPicker.SelectedIndexChanged += OnProviderChanged;

        _statusLabel = new Label
        {
            TextColor = Color.FromRgb(200, 200, 200),
            FontSize = 12,
            Margin = new Thickness(10, 0, 10, 10)
        };

        // Endpoint
        var endpointLabel = CreateLabel("Endpoint URL");
        _endpointEntry = CreateEntry("https://api.example.com");

        // API Key
        var apiKeyLabel = CreateLabel("API Key");
        _apiKeyEntry = CreateEntry("Enter API key here", isPassword: true);

        // Model
        var modelLabel = CreateLabel("Default Model");
        _modelEntry = CreateEntry("model-name");

        // Organization ID (for OpenAI)
        var orgLabel = CreateLabel("Organization ID (Optional)");
        _organizationEntry = CreateEntry("org-id");

        // Project ID (for Google)
        var projectLabel = CreateLabel("Project ID (Optional)");
        _projectEntry = CreateEntry("project-id");

        // Region (for Azure)
        var regionLabel = CreateLabel("Region (Optional)");
        _regionEntry = CreateEntry("eastus");

        // Deployment (for Azure OpenAI)
        var deploymentLabel = CreateLabel("Deployment Name (Optional)");
        _deploymentEntry = CreateEntry("deployment-name");

        // Temperature
        var temperatureTitle = CreateLabel("Temperature");
        _temperatureLabel = new Label
        {
            Text = "0.7",
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(10, 0, 10, 5)
        };

        _temperatureSlider = new Slider
        {
            Minimum = 0,
            Maximum = 2,
            Value = 0.7,
            MinimumTrackColor = Color.FromRgb(0, 170, 0),
            MaximumTrackColor = Color.FromRgb(100, 100, 100),
            Margin = new Thickness(10, 0, 10, 10)
        };

        _temperatureSlider.ValueChanged += (s, e) =>
        {
            _temperatureLabel.Text = $"{e.NewValue:F2}";
        };

        // Max Tokens
        var maxTokensTitle = CreateLabel("Max Tokens");
        _maxTokensLabel = new Label
        {
            Text = "2000",
            TextColor = Color.FromRgb(200, 200, 200),
            Margin = new Thickness(10, 0, 10, 5)
        };

        _maxTokensSlider = new Slider
        {
            Minimum = 100,
            Maximum = 8000,
            Value = 2000,
            MinimumTrackColor = Color.FromRgb(0, 170, 0),
            MaximumTrackColor = Color.FromRgb(100, 100, 100),
            Margin = new Thickness(10, 0, 10, 10)
        };

        _maxTokensSlider.ValueChanged += (s, e) =>
        {
            _maxTokensLabel.Text = $"{(int)e.NewValue}";
        };

        // Enabled Switch
        var enabledLabel = CreateLabel("Enable this provider");
        _enabledSwitch = new Switch
        {
            OnColor = Color.FromRgb(0, 170, 0),
            IsToggled = true,
            Margin = new Thickness(10, 0, 10, 10)
        };

        // Buttons
        var buttonStack = new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(10, 30, 10, 10)
        };

        var saveButton = new Button
        {
            Text = "Save Configuration",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        saveButton.Clicked += OnSaveClicked;

        var testButton = new Button
        {
            Text = "Test Connection",
            BackgroundColor = Color.FromRgb(0, 100, 170),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        testButton.Clicked += OnTestClicked;

        buttonStack.Children.Add(saveButton);
        buttonStack.Children.Add(testButton);

        var setActiveButton = new Button
        {
            Text = "Set as Active Provider",
            BackgroundColor = Color.FromRgb(170, 100, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10, 0, 10, 10)
        };
        setActiveButton.Clicked += OnSetActiveClicked;

        Content = new ScrollView
        {
            Content = new StackLayout
            {
                Children =
                {
                    providerLabel,
                    _providerPicker,
                    _statusLabel,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    endpointLabel,
                    _endpointEntry,
                    apiKeyLabel,
                    _apiKeyEntry,
                    modelLabel,
                    _modelEntry,
                    orgLabel,
                    _organizationEntry,
                    projectLabel,
                    _projectEntry,
                    regionLabel,
                    _regionEntry,
                    deploymentLabel,
                    _deploymentEntry,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    temperatureTitle,
                    _temperatureLabel,
                    _temperatureSlider,
                    maxTokensTitle,
                    _maxTokensLabel,
                    _maxTokensSlider,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    enabledLabel,
                    _enabledSwitch,
                    buttonStack,
                    setActiveButton
                }
            }
        };

        // Load active provider
        LoadActiveProvider();
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextColor = Color.FromRgb(0, 255, 0),
            Margin = new Thickness(10, 10, 10, 5)
        };
    }

    private Entry CreateEntry(string placeholder, bool isPassword = false)
    {
        return new Entry
        {
            Placeholder = placeholder,
            PlaceholderColor = Color.FromRgb(128, 128, 128),
            TextColor = Color.FromRgb(0, 255, 0),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            IsPassword = isPassword,
            Margin = new Thickness(10, 0, 10, 10)
        };
    }

    private void LoadActiveProvider()
    {
        var activeProvider = _providerService.GetActiveProvider();
        _providerPicker.SelectedIndex = (int)activeProvider;
        LoadProviderConfig(activeProvider);
    }

    private void LoadProviderConfig(AIProvider provider)
    {
        var config = _providerService.GetProviderConfig(provider) 
                     ?? AIProviderConfig.GetDefault(provider);

        _endpointEntry.Text = config.Endpoint;
        _apiKeyEntry.Text = config.ApiKey ?? string.Empty;
        _modelEntry.Text = config.DefaultModel ?? string.Empty;
        _organizationEntry.Text = config.OrganizationId ?? string.Empty;
        _projectEntry.Text = config.ProjectId ?? string.Empty;
        _regionEntry.Text = config.Region ?? string.Empty;
        _deploymentEntry.Text = config.DeploymentName ?? string.Empty;
        _temperatureSlider.Value = config.Temperature ?? 0.7;
        _maxTokensSlider.Value = config.MaxTokens ?? 2000;
        _enabledSwitch.IsToggled = config.IsEnabled;

        var isActive = _providerService.GetActiveProvider() == provider;
        _statusLabel.Text = isActive ? "✓ Active Provider" : "Inactive";
        _statusLabel.TextColor = isActive ? Color.FromRgb(0, 255, 0) : Color.FromRgb(200, 200, 200);
    }

    private void OnProviderChanged(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex >= 0)
        {
            var provider = (AIProvider)_providerPicker.SelectedIndex;
            LoadProviderConfig(provider);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Error", "Please select a provider", "OK");
            return;
        }

        var provider = (AIProvider)_providerPicker.SelectedIndex;
        
        var config = new AIProviderConfig
        {
            Provider = provider,
            Endpoint = _endpointEntry.Text?.Trim() ?? string.Empty,
            ApiKey = _apiKeyEntry.Text?.Trim(),
            DefaultModel = _modelEntry.Text?.Trim(),
            OrganizationId = _organizationEntry.Text?.Trim(),
            ProjectId = _projectEntry.Text?.Trim(),
            Region = _regionEntry.Text?.Trim(),
            DeploymentName = _deploymentEntry.Text?.Trim(),
            Temperature = _temperatureSlider.Value,
            MaxTokens = (int)_maxTokensSlider.Value,
            IsEnabled = _enabledSwitch.IsToggled
        };

        var (isValid, error) = config.Validate();
        
        if (!isValid)
        {
            await DisplayAlert("Validation Error", error, "OK");
            return;
        }

        _providerService.SaveProviderConfig(config);
        await DisplayAlert("Success", $"{config.GetDisplayName()} configuration saved", "OK");
    }

    private async void OnTestClicked(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Error", "Please select a provider first", "OK");
            return;
        }

        var provider = (AIProvider)_providerPicker.SelectedIndex;
        var config = BuildConfigFromUI(provider);
        
        // Validate first
        var (isValid, error) = config.Validate();
        if (!isValid)
        {
            await DisplayAlert("Validation Error", error, "OK");
            return;
        }

        // Show loading
        _statusLabel.Text = "Testing connection...";
        _statusLabel.TextColor = Color.FromRgb(255, 165, 0); // Orange
        
        try
        {
            var testResult = await TestProviderConnectionAsync(config);
            
            if (testResult.Success)
            {
                _statusLabel.Text = $"✓ {testResult.Message}";
                _statusLabel.TextColor = Color.FromRgb(0, 255, 0); // Green
                await DisplayAlert("Connection Test", testResult.Message, "OK");
            }
            else
            {
                _statusLabel.Text = $"✗ {testResult.Message}";
                _statusLabel.TextColor = Color.FromRgb(255, 0, 0); // Red
                await DisplayAlert("Connection Failed", testResult.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"✗ Test failed: {ex.Message}";
            _statusLabel.TextColor = Color.FromRgb(255, 0, 0); // Red
            await DisplayAlert("Error", $"Connection test failed: {ex.Message}", "OK");
        }
    }

    private AIProviderConfig BuildConfigFromUI(AIProvider provider)
    {
        return new AIProviderConfig
        {
            Provider = provider,
            Endpoint = _endpointEntry.Text?.Trim() ?? string.Empty,
            ApiKey = _apiKeyEntry.Text?.Trim(),
            DefaultModel = _modelEntry.Text?.Trim(),
            OrganizationId = _organizationEntry.Text?.Trim(),
            ProjectId = _projectEntry.Text?.Trim(),
            Region = _regionEntry.Text?.Trim(),
            DeploymentName = _deploymentEntry.Text?.Trim(),
            Temperature = _temperatureSlider.Value,
            MaxTokens = (int)_maxTokensSlider.Value,
            IsEnabled = _enabledSwitch.IsToggled
        };
    }

    private async Task<TestConnectionResult> TestProviderConnectionAsync(AIProviderConfig config)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        
        try
        {
            switch (config.Provider)
            {
                case AIProvider.Ollama:
                    return await TestOllamaAsync(httpClient, config);
                    
                case AIProvider.OpenAI:
                    return await TestOpenAIAsync(httpClient, config);
                    
                case AIProvider.Anthropic:
                    return await TestAnthropicAsync(httpClient, config);
                    
                case AIProvider.Google:
                    return await TestGoogleAsync(httpClient, config);
                    
                case AIProvider.Meta:
                case AIProvider.Mistral:
                    return await TestOpenAICompatibleAsync(httpClient, config);
                    
                case AIProvider.Cohere:
                    return await TestCohereAsync(httpClient, config);
                    
                case AIProvider.HuggingFace:
                    return await TestHuggingFaceAsync(httpClient, config);
                    
                case AIProvider.AzureOpenAI:
                    return await TestAzureOpenAIAsync(httpClient, config);
                    
                default:
                    return new TestConnectionResult 
                    { 
                        Success = false, 
                        Message = $"Provider {config.Provider} testing not implemented" 
                    };
            }
        }
        finally
        {
            httpClient.Dispose();
        }
    }

    private async Task<TestConnectionResult> TestOllamaAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            var response = await client.GetAsync($"{config.Endpoint}/api/tags");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new TestConnectionResult 
                { 
                    Success = true, 
                    Message = $"Successfully connected to Ollama at {config.Endpoint}" 
                };
            }
            else
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = $"Connection failed with status code: {response.StatusCode}" 
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Cannot reach endpoint: {ex.Message}" 
            };
        }
        catch (TaskCanceledException)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = "Connection timeout - check endpoint and network" 
            };
        }
    }

    private async Task<TestConnectionResult> TestOpenAIAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            
            var response = await client.GetAsync($"{config.Endpoint}/models");
            
            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult 
                { 
                    Success = true, 
                    Message = "Successfully authenticated with OpenAI" 
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = "Authentication failed - check API key" 
                };
            }
            else
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = $"Connection failed: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    private async Task<TestConnectionResult> TestAnthropicAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            
            // Anthropic doesn't have a simple health check endpoint, so we verify the key format
            if (string.IsNullOrEmpty(config.ApiKey) || !config.ApiKey.StartsWith("sk-ant-"))
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = "Invalid API key format - should start with 'sk-ant-'" 
                };
            }
            
            return new TestConnectionResult 
            { 
                Success = true, 
                Message = "API key format is valid. Configuration saved." 
            };
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    private async Task<TestConnectionResult> TestGoogleAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            // Test with a simple models list request
            var url = $"{config.Endpoint}/models?key={config.ApiKey}";
            var response = await client.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult 
                { 
                    Success = true, 
                    Message = "Successfully authenticated with Google AI" 
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = "Authentication failed - check API key and project ID" 
                };
            }
            else
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = $"Connection failed: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    private async Task<TestConnectionResult> TestOpenAICompatibleAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            
            var response = await client.GetAsync($"{config.Endpoint}/models");
            
            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult 
                { 
                    Success = true, 
                    Message = $"Successfully connected to {config.Provider}" 
                };
            }
            else
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = $"Connection failed: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    private async Task<TestConnectionResult> TestCohereAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            
            // Cohere has a check-api-key endpoint
            var response = await client.GetAsync($"{config.Endpoint}/check-api-key");
            
            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult 
                { 
                    Success = true, 
                    Message = "Successfully authenticated with Cohere" 
                };
            }
            else
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = $"Authentication failed: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    private async Task<TestConnectionResult> TestHuggingFaceAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            
            // Verify API key format and endpoint
            if (string.IsNullOrEmpty(config.ApiKey) || !config.ApiKey.StartsWith("hf_"))
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = "Invalid API key format - should start with 'hf_'" 
                };
            }
            
            return new TestConnectionResult 
            { 
                Success = true, 
                Message = "API key format is valid. Configuration saved." 
            };
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    private async Task<TestConnectionResult> TestAzureOpenAIAsync(HttpClient client, AIProviderConfig config)
    {
        try
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", config.ApiKey);
            
            var url = $"{config.Endpoint}/openai/deployments?api-version=2023-05-15";
            var response = await client.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                return new TestConnectionResult 
                { 
                    Success = true, 
                    Message = "Successfully connected to Azure OpenAI" 
                };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = "Authentication failed - check API key" 
                };
            }
            else
            {
                return new TestConnectionResult 
                { 
                    Success = false, 
                    Message = $"Connection failed: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new TestConnectionResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }
}

/// <summary>
/// Result of connection test
/// </summary>
internal class TestConnectionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

    private async void OnSetActiveClicked(object? sender, EventArgs e)
    {
        if (_providerPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Error", "Please select a provider", "OK");
            return;
        }

        var provider = (AIProvider)_providerPicker.SelectedIndex;
        _providerService.SetActiveProvider(provider);
        
        LoadActiveProvider();
        await DisplayAlert("Success", $"{AIProviderConfig.GetDefault(provider).GetDisplayName()} is now the active provider", "OK");
    }
}
