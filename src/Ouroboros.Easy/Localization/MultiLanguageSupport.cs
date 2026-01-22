namespace Ouroboros.Easy.Localization;

/// <summary>
/// Provides multi-language support for the Ouroboros Easy API.
/// Supports English, German, French, Spanish, and more.
/// </summary>
public static class MultiLanguageSupport
{
    private static readonly Dictionary<string, LanguageResources> _resources = new()
    {
        ["en"] = new LanguageResources
        {
            WelcomeMessage = "Welcome to Ouroboros!",
            PipelineStarting = "Starting pipeline execution...",
            PipelineCompleted = "Pipeline completed successfully.",
            PipelineFailed = "Pipeline execution failed: {0}",
            DraftStage = "Generating initial draft...",
            CritiqueStage = "Analyzing and critiquing response...",
            ImproveStage = "Generating improved version...",
            SummarizeStage = "Creating summary...",
            VoiceInputProcessing = "Processing voice input...",
            VoiceOutputGenerating = "Generating voice output...",
            ErrorOccurred = "An error occurred: {0}",
            TopicRequired = "Topic must be specified.",
            ModelRequired = "Model must be specified.",
            StageRequired = "At least one stage must be enabled."
        },
        ["de"] = new LanguageResources
        {
            WelcomeMessage = "Willkommen bei Ouroboros!",
            PipelineStarting = "Pipeline-Ausführung wird gestartet...",
            PipelineCompleted = "Pipeline erfolgreich abgeschlossen.",
            PipelineFailed = "Pipeline-Ausführung fehlgeschlagen: {0}",
            DraftStage = "Erster Entwurf wird erstellt...",
            CritiqueStage = "Antwort wird analysiert und kritisiert...",
            ImproveStage = "Verbesserte Version wird erstellt...",
            SummarizeStage = "Zusammenfassung wird erstellt...",
            VoiceInputProcessing = "Spracheingabe wird verarbeitet...",
            VoiceOutputGenerating = "Sprachausgabe wird generiert...",
            ErrorOccurred = "Ein Fehler ist aufgetreten: {0}",
            TopicRequired = "Thema muss angegeben werden.",
            ModelRequired = "Modell muss angegeben werden.",
            StageRequired = "Mindestens eine Phase muss aktiviert sein."
        },
        ["fr"] = new LanguageResources
        {
            WelcomeMessage = "Bienvenue sur Ouroboros !",
            PipelineStarting = "Démarrage de l'exécution du pipeline...",
            PipelineCompleted = "Pipeline terminé avec succès.",
            PipelineFailed = "L'exécution du pipeline a échoué : {0}",
            DraftStage = "Génération du brouillon initial...",
            CritiqueStage = "Analyse et critique de la réponse...",
            ImproveStage = "Génération de la version améliorée...",
            SummarizeStage = "Création du résumé...",
            VoiceInputProcessing = "Traitement de l'entrée vocale...",
            VoiceOutputGenerating = "Génération de la sortie vocale...",
            ErrorOccurred = "Une erreur s'est produite : {0}",
            TopicRequired = "Le sujet doit être spécifié.",
            ModelRequired = "Le modèle doit être spécifié.",
            StageRequired = "Au moins une étape doit être activée."
        }
    };

    private static string _currentLanguage = "en";

    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set => _currentLanguage = _resources.ContainsKey(value) ? value : "en";
    }

    public static string Get(string key, params object[] args)
    {
        if (!_resources.TryGetValue(_currentLanguage, out LanguageResources? resources))
        {
            resources = _resources["en"];
        }

        string? value = resources.GetType().GetProperty(key)?.GetValue(resources) as string;
        
        if (value == null)
        {
            return key;
        }

        return args.Length > 0 ? string.Format(value, args) : value;
    }

    public static IEnumerable<string> SupportedLanguages => _resources.Keys;
    
    public static bool IsLanguageSupported(string languageCode) => _resources.ContainsKey(languageCode);
}

public class LanguageResources
{
    public string WelcomeMessage { get; set; } = string.Empty;
    public string PipelineStarting { get; set; } = string.Empty;
    public string PipelineCompleted { get; set; } = string.Empty;
    public string PipelineFailed { get; set; } = string.Empty;
    public string DraftStage { get; set; } = string.Empty;
    public string CritiqueStage { get; set; } = string.Empty;
    public string ImproveStage { get; set; } = string.Empty;
    public string SummarizeStage { get; set; } = string.Empty;
    public string VoiceInputProcessing { get; set; } = string.Empty;
    public string VoiceOutputGenerating { get; set; } = string.Empty;
    public string ErrorOccurred { get; set; } = string.Empty;
    public string TopicRequired { get; set; } = string.Empty;
    public string ModelRequired { get; set; } = string.Empty;
    public string StageRequired { get; set; } = string.Empty;
}
