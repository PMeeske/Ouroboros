using Microsoft.Maui.Controls;
using MonadicPipeline.Android.Services;
using System.Text;

namespace MonadicPipeline.Android.Views;

/// <summary>
/// View for symbolic reasoning and knowledge base management
/// </summary>
public class SymbolicReasoningView : ContentPage
{
    private readonly SymbolicReasoningEngine _reasoningEngine;
    private readonly Editor _knowledgeBaseEditor;
    private readonly Entry _subjectEntry;
    private readonly Entry _predicateEntry;
    private readonly Entry _objectEntry;
    private readonly Label _statusLabel;
    private readonly ListView _factsListView;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicReasoningView"/> class.
    /// </summary>
    public SymbolicReasoningView()
    {
        _reasoningEngine = new SymbolicReasoningEngine();
        
        Title = "Symbolic Reasoning";
        BackgroundColor = Color.FromRgb(30, 30, 30);

        // Status Label
        _statusLabel = new Label
        {
            Text = "Knowledge Base: 0 facts",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 10)
        };

        // Add Fact Section
        var addFactLabel = new Label
        {
            Text = "Add Fact (Subject Predicate Object)",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 10, 10, 5)
        };

        _subjectEntry = CreateEntry("Subject (e.g., 'Cat')");
        _predicateEntry = CreateEntry("Predicate (e.g., 'is-a')");
        _objectEntry = CreateEntry("Object (e.g., 'Animal')");

        var addButton = new Button
        {
            Text = "Add Fact",
            BackgroundColor = Color.FromRgb(0, 170, 0),
            TextColor = Colors.White,
            Margin = new Thickness(10)
        };
        addButton.Clicked += OnAddFactClicked;

        // Quick Add Examples
        var examplesLabel = new Label
        {
            Text = "Quick Examples:",
            TextColor = Color.FromRgb(200, 200, 200),
            FontSize = 12,
            Margin = new Thickness(10, 10, 10, 5)
        };

        var exampleStack = new HorizontalStackLayout
        {
            Spacing = 5,
            Margin = new Thickness(10, 0, 10, 10)
        };

        var example1Button = CreateExampleButton("Cat→Animal");
        example1Button.Clicked += (s, e) => AddExampleFact("Cat", "is-a", "Animal");

        var example2Button = CreateExampleButton("Animal→Living");
        example2Button.Clicked += (s, e) => AddExampleFact("Animal", "is-a", "Living");

        var example3Button = CreateExampleButton("Dog→Animal");
        example3Button.Clicked += (s, e) => AddExampleFact("Dog", "is-a", "Animal");

        exampleStack.Children.Add(example1Button);
        exampleStack.Children.Add(example2Button);
        exampleStack.Children.Add(example3Button);

        // Actions
        var actionsLabel = new Label
        {
            Text = "Reasoning Actions:",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 5)
        };

        var actionsStack = new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(10, 0, 10, 10)
        };

        var inferButton = new Button
        {
            Text = "Infer New Facts",
            BackgroundColor = Color.FromRgb(0, 100, 170),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        inferButton.Clicked += OnInferClicked;

        var exportButton = new Button
        {
            Text = "Export KB",
            BackgroundColor = Color.FromRgb(100, 0, 170),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        exportButton.Clicked += OnExportClicked;

        var clearButton = new Button
        {
            Text = "Clear All",
            BackgroundColor = Color.FromRgb(170, 0, 0),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        clearButton.Clicked += OnClearClicked;

        actionsStack.Children.Add(inferButton);
        actionsStack.Children.Add(exportButton);
        actionsStack.Children.Add(clearButton);

        // Facts List
        var factsLabel = new Label
        {
            Text = "Knowledge Base Facts:",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 5)
        };

        _factsListView = new ListView
        {
            BackgroundColor = Color.FromRgb(0, 0, 0),
            SeparatorColor = Color.FromRgb(0, 170, 0),
            HeightRequest = 300
        };

        _factsListView.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label
            {
                TextColor = Color.FromRgb(0, 255, 0),
                FontFamily = "Courier New",
                FontSize = 12,
                Padding = new Thickness(10, 5)
            };
            label.SetBinding(Label.TextProperty, ".");
            return new ViewCell { View = label };
        });

        // Knowledge Base Editor
        var kbEditorLabel = new Label
        {
            Text = "Knowledge Base Export:",
            TextColor = Color.FromRgb(0, 255, 0),
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 20, 10, 5)
        };

        _knowledgeBaseEditor = new Editor
        {
            BackgroundColor = Color.FromRgb(0, 0, 0),
            TextColor = Color.FromRgb(0, 255, 0),
            FontFamily = "Courier New",
            FontSize = 10,
            HeightRequest = 200,
            IsReadOnly = true,
            Margin = new Thickness(10, 0, 10, 10)
        };

        Content = new ScrollView
        {
            Content = new StackLayout
            {
                Children =
                {
                    _statusLabel,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    addFactLabel,
                    _subjectEntry,
                    _predicateEntry,
                    _objectEntry,
                    addButton,
                    examplesLabel,
                    exampleStack,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    actionsLabel,
                    actionsStack,
                    new BoxView { HeightRequest = 1, Color = Color.FromRgb(100, 100, 100), Margin = new Thickness(10, 10) },
                    factsLabel,
                    _factsListView,
                    kbEditorLabel,
                    _knowledgeBaseEditor
                }
            }
        };

        UpdateDisplay();
    }

    private Entry CreateEntry(string placeholder)
    {
        return new Entry
        {
            Placeholder = placeholder,
            PlaceholderColor = Color.FromRgb(128, 128, 128),
            TextColor = Color.FromRgb(0, 255, 0),
            BackgroundColor = Color.FromRgb(0, 0, 0),
            Margin = new Thickness(10, 0, 10, 5)
        };
    }

    private Button CreateExampleButton(string text)
    {
        return new Button
        {
            Text = text,
            FontSize = 10,
            BackgroundColor = Color.FromRgb(0, 50, 0),
            TextColor = Colors.White,
            Padding = new Thickness(10, 5)
        };
    }

    private async void OnAddFactClicked(object? sender, EventArgs e)
    {
        var subject = _subjectEntry.Text?.Trim();
        var predicate = _predicateEntry.Text?.Trim();
        var obj = _objectEntry.Text?.Trim();

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(predicate) || string.IsNullOrEmpty(obj))
        {
            await DisplayAlert("Error", "Please fill in all fields", "OK");
            return;
        }

        AddExampleFact(subject, predicate, obj);
    }

    private async void AddExampleFact(string subject, string predicate, string obj)
    {
        _reasoningEngine.AddFact(subject, predicate, obj);
        
        _subjectEntry.Text = string.Empty;
        _predicateEntry.Text = string.Empty;
        _objectEntry.Text = string.Empty;

        UpdateDisplay();
        await DisplayAlert("Success", $"Added: {subject} {predicate} {obj}", "OK");
    }

    private async void OnInferClicked(object? sender, EventArgs e)
    {
        var newFacts = _reasoningEngine.Infer();
        
        if (newFacts.Count > 0)
        {
            var message = new StringBuilder();
            message.AppendLine($"Inferred {newFacts.Count} new fact(s):");
            message.AppendLine();
            
            foreach (var fact in newFacts.Take(10))
            {
                message.AppendLine($"• {fact}");
            }

            if (newFacts.Count > 10)
            {
                message.AppendLine($"... and {newFacts.Count - 10} more");
            }

            await DisplayAlert("Inference Complete", message.ToString(), "OK");
        }
        else
        {
            await DisplayAlert("Inference Complete", "No new facts inferred.", "OK");
        }

        UpdateDisplay();
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        var exported = _reasoningEngine.ExportKnowledgeBase();
        _knowledgeBaseEditor.Text = exported;
        
        await DisplayAlert("Exported", "Knowledge base exported to the text area below.", "OK");
    }

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Clear Knowledge Base",
            "Are you sure you want to clear all facts?",
            "Yes",
            "No");

        if (confirm)
        {
            _reasoningEngine.Clear();
            UpdateDisplay();
            await DisplayAlert("Cleared", "Knowledge base cleared.", "OK");
        }
    }

    private void UpdateDisplay()
    {
        var facts = _reasoningEngine.GetAllFacts();
        _statusLabel.Text = $"Knowledge Base: {facts.Count} fact(s)";
        
        _factsListView.ItemsSource = facts.Select(f => f.ToString()).ToList();
        
        if (facts.Count > 0)
        {
            _knowledgeBaseEditor.Text = _reasoningEngine.ExportKnowledgeBase();
        }
        else
        {
            _knowledgeBaseEditor.Text = "Knowledge base is empty. Add facts to begin reasoning.";
        }
    }
}
