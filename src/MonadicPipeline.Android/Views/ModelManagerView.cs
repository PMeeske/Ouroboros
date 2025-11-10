using Microsoft.Maui.Controls;
using MonadicPipeline.Android.Services;

namespace MonadicPipeline.Android.Views;

/// <summary>
/// Model manager view for browsing and managing AI models
/// </summary>
public class ModelManagerView : ContentPage
{
    private readonly ModelManager _modelManager;
    private readonly ListView _modelListView;
    private readonly ActivityIndicator _loadingIndicator;
    private readonly Label _statusLabel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelManagerView"/> class.
    /// </summary>
    /// <param name="modelManager">The model manager service</param>
    public ModelManagerView(ModelManager modelManager)
    {
        _modelManager = modelManager;
        Title = "Model Manager";
        BackgroundColor = Color.FromRgb(30, 30, 30);

        _loadingIndicator = new ActivityIndicator
        {
            IsRunning = false,
            Color = Color.FromRgb(0, 255, 0),
            HorizontalOptions = LayoutOptions.Center
        };

        _statusLabel = new Label
        {
            Text = "Loading models...",
            TextColor = Color.FromRgb(0, 255, 0),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 10, 0, 10)
        };

        _modelListView = new ListView
        {
            BackgroundColor = Color.FromRgb(0, 0, 0),
            SeparatorColor = Color.FromRgb(0, 170, 0),
            HasUnevenRows = true
        };

        _modelListView.ItemTemplate = new DataTemplate(() =>
        {
            var nameLabel = new Label
            {
                TextColor = Color.FromRgb(0, 255, 0),
                FontAttributes = FontAttributes.Bold,
                FontSize = 16
            };
            nameLabel.SetBinding(Label.TextProperty, "Name");

            var sizeLabel = new Label
            {
                TextColor = Color.FromRgb(200, 200, 200),
                FontSize = 12
            };
            sizeLabel.SetBinding(Label.TextProperty, "FormattedSize");

            var recommendedLabel = new Label
            {
                Text = "⭐ Recommended",
                TextColor = Color.FromRgb(255, 215, 0),
                FontSize = 12,
                IsVisible = false
            };
            recommendedLabel.SetBinding(Label.IsVisibleProperty, "IsRecommended");

            var stack = new StackLayout
            {
                Padding = new Thickness(10),
                Children = { nameLabel, sizeLabel, recommendedLabel }
            };

            return new ViewCell { View = stack };
        });

        _modelListView.ItemSelected += OnModelSelected;

        var refreshButton = new Button
        {
            Text = "Refresh",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10)
        };
        refreshButton.Clicked += async (s, e) => await LoadModelsAsync();

        var recommendedButton = new Button
        {
            Text = "Show Recommended",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10)
        };
        recommendedButton.Clicked += OnShowRecommended;

        Content = new StackLayout
        {
            Children =
            {
                _statusLabel,
                _loadingIndicator,
                new StackLayout
                {
                    Orientation = StackOrientation.Horizontal,
                    Children = { refreshButton, recommendedButton }
                },
                _modelListView
            }
        };
    }

    /// <summary>
    /// Load models when page appears
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadModelsAsync();
    }

    private async Task LoadModelsAsync()
    {
        _loadingIndicator.IsRunning = true;
        _statusLabel.Text = "Loading models...";

        try
        {
            var models = await _modelManager.GetAvailableModelsAsync();
            _modelListView.ItemsSource = models;
            _statusLabel.Text = $"Found {models.Count} model(s)";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            await DisplayAlert("Error", $"Failed to load models: {ex.Message}", "OK");
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
        }
    }

    private async void OnModelSelected(object? sender, SelectedItemChangedEventArgs e)
    {
        if (e.SelectedItem is not ModelInfo model)
        {
            return;
        }

        var action = await DisplayActionSheet(
            $"Model: {model.Name}",
            "Cancel",
            "Delete",
            "Use for Chat");

        if (action == "Delete")
        {
            var confirm = await DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete {model.Name}?",
                "Yes",
                "No");

            if (confirm)
            {
                await DeleteModelAsync(model.Name);
            }
        }
        else if (action == "Use for Chat")
        {
            // Save the selected model as the preferred model
            Preferences.Set("preferred_model", model.Name);
            await DisplayAlert("Model Selected", $"'{model.Name}' is now your default model for chat.\n\nIt will be used for 'ask' commands.", "OK");
            
            // Navigate back to main page
            await Navigation.PopAsync();
        }

        _modelListView.SelectedItem = null;
    }

    private async void OnShowRecommended(object? sender, EventArgs e)
    {
        var recommended = _modelManager.GetRecommendedModels();
        var message = string.Join("\n\n", recommended.Select(m =>
            $"• {m.Name}\n  Parameters: {m.Parameters}\n  Memory: {m.EstimatedMemory}\n  Use: {m.UseCase}"));

        await DisplayAlert("Recommended Models", message, "OK");
    }

    private async Task DeleteModelAsync(string modelName)
    {
        try
        {
            await _modelManager.DeleteModelAsync(modelName);
            await DisplayAlert("Success", $"Model '{modelName}' deleted successfully", "OK");
            await LoadModelsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to delete model: {ex.Message}", "OK");
        }
    }
}
