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
        await DisplayAlert("Test Connection", "Connection testing will be implemented with the actual API integration.", "OK");
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
