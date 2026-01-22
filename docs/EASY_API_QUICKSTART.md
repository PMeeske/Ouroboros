# Ouroboros.Easy - Quick Start Guide

## Overview

Ouroboros.Easy is a simplified fluent builder API that makes it easy to create and configure AI pipelines without needing deep knowledge of the underlying architecture. It provides a clean, intuitive interface for common AI tasks.

## Features

- ✅ **Simple Fluent API**: Method chaining for easy pipeline configuration
- ✅ **Voice Support**: Speech-to-text and text-to-speech capabilities
- ✅ **Multi-Language**: Support for English, German, French, Spanish, and more
- ✅ **German Cortana Voice**: Built-in support for Azure Neural TTS with German voices
- ✅ **Stage-based Processing**: Draft, Critique, Improve, and Summarize stages
- ✅ **Model Flexibility**: Works with any Ollama-compatible model

## Installation

Add a reference to the Ouroboros.Easy project:

```bash
dotnet add reference path/to/Ouroboros.Easy/Ouroboros.Easy.csproj
```

## Basic Usage

### Simple Text Pipeline

```csharp
using Ouroboros.Easy;

// Create a simple pipeline
PipelineResult result = await Pipeline.Create()
    .About("quantum computing")
    .Draft()
    .Critique()
    .Improve()
    .WithModel("llama3")
    .RunAsync();

if (result.IsSuccess)
{
    Console.WriteLine(result.Output);
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

### Customized Pipeline

```csharp
PipelineResult result = await Pipeline.Create()
    .About("Write a Python script to analyze CSV data")
    .Draft()
    .Critique()
    .Improve()
    .Summarize()
    .WithModel("llama3")
    .WithTemperature(0.7)
    .WithOllamaEndpoint("http://localhost:11434")
    .RunAsync();
```

## Voice-Enabled Pipelines

### English Voice Output

```csharp
using Ouroboros.Easy;
using Ouroboros.Providers.TextToSpeech;

// Create OpenAI TTS service
var ttsService = new OpenAiTextToSpeechService("your-api-key");

// Create voice-enabled pipeline
VoicePipelineResult result = await VoicePipeline.Create()
    .About("Explain machine learning in simple terms")
    .Draft()
    .Improve()
    .WithModel("llama3")
    .WithTextToSpeech(ttsService)
    .WithVoice("alloy")
    .RunAsync("output.mp3");

if (result.IsSuccess)
{
    Console.WriteLine($"Text: {result.TextOutput}");
    Console.WriteLine($"Audio saved to: {result.AudioPath}");
}
```

### German Voice (Cortana-like)

```csharp
using Ouroboros.Easy;

// Use built-in German voice support
VoicePipelineResult result = await VoicePipeline.Create()
    .About("Erkläre künstliche Intelligenz")
    .Draft()
    .Improve()
    .WithModel("llama3")
    .WithGermanVoice("your-azure-key", "westeurope")
    .RunAsync("output_de.mp3");

if (result.IsSuccess)
{
    Console.WriteLine($"Text: {result.TextOutput}");
    Console.WriteLine($"Audio saved to: {result.AudioPath}");
}
```

### Voice Input (Speech-to-Text)

```csharp
using Ouroboros.Easy;
using Ouroboros.Providers.SpeechToText;

// Create Whisper STT service
var sttService = new WhisperSpeechToTextService("your-openai-key");

// Create pipeline with voice input
VoicePipelineResult result = await VoicePipeline.Create()
    .WithSpeechToText(sttService)
    .WithLanguage("en")
    .AboutFromVoiceAsync("recording.wav")  // Transcribe audio file
    .Result  // Unwrap the Task
    .Draft()
    .Improve()
    .WithModel("llama3")
    .RunAsync();
```

## Multi-Language Support

```csharp
using Ouroboros.Easy.Localization;

// Set language for UI messages
MultiLanguageSupport.CurrentLanguage = "de";  // German
Console.WriteLine(MultiLanguageSupport.Get("WelcomeMessage"));
// Output: "Willkommen bei Ouroboros!"

// French
MultiLanguageSupport.CurrentLanguage = "fr";
Console.WriteLine(MultiLanguageSupport.Get("PipelineStarting"));
// Output: "Démarrage de l'exécution du pipeline..."

// Auto-detect system language
MultiLanguageSupport.UseSystemLanguage();

// Check supported languages
foreach (var lang in MultiLanguageSupport.SupportedLanguages)
{
    Console.WriteLine(lang);  // en, de, fr, es
}
```

## Available Pipeline Stages

| Stage | Description |
|-------|-------------|
| `Draft()` | Generates an initial response to the topic |
| `Critique()` | Analyzes and identifies weaknesses in the draft |
| `Improve()` | Generates an improved version based on critique |
| `Summarize()` | Creates a concise summary of the final output |

## Configuration Methods

### Basic Configuration
- `About(string)` - Sets the topic/question
- `WithModel(string)` - Sets the LLM model name
- `WithTemperature(double)` - Controls randomness (0.0-1.0)
- `WithOllamaEndpoint(string)` - Sets custom Ollama endpoint

### Advanced Configuration
- `WithTools(ToolRegistry)` - Provides custom tools
- `WithEmbedding(IEmbeddingModel)` - Sets custom embedding model

### Voice Configuration
- `WithLanguage(string)` - Sets language code ("en", "de", "fr", etc.)
- `WithVoice(string)` - Sets TTS voice name
- `WithSpeechToText(ISpeechToTextService)` - Configures STT
- `WithTextToSpeech(ITextToSpeechService)` - Configures TTS
- `WithGermanVoice(string, string)` - Quick setup for German Cortana-like voice
- `WithWhisperSpeechToText(string)` - Quick setup for Whisper STT

## DSL Export

You can export the pipeline configuration as a DSL string for inspection or customization:

```csharp
Pipeline pipeline = Pipeline.Create()
    .About("quantum computing")
    .Draft()
    .Critique()
    .WithModel("llama3")
    .WithTemperature(0.8);

string dsl = pipeline.ToDSL();
Console.WriteLine(dsl);
```

Output:
```
Pipeline:
  Topic: quantum computing
  Model: llama3
  Temperature: 0.8
  Endpoint: default (localhost:11434)
  Stages: draft -> critique
  Tools: default
  Embedding: default
```

## Supported Languages

### Voice Languages
- English (en)
- German (de)
- French (fr)
- Spanish (es)
- Italian (it)
- Portuguese (pt)
- Japanese (ja)
- Chinese (zh)
- And 90+ more via Whisper

### UI Languages
- English (en)
- German (de)
- French (fr)
- Spanish (es)

## Error Handling

```csharp
PipelineResult result = await Pipeline.Create()
    .About("topic")
    .Draft()
    .WithModel("llama3")
    .RunAsync();

if (result.IsSuccess)
{
    string output = result.GetOutputOrThrow();  // Safe access
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

## Examples

### Research Assistant

```csharp
var result = await Pipeline.Create()
    .About("Latest developments in quantum computing")
    .Draft()
    .Critique()
    .Improve()
    .Summarize()
    .WithModel("llama3")
    .WithTemperature(0.7)
    .RunAsync();
```

### Code Generation

```csharp
var result = await Pipeline.Create()
    .About("Generate a REST API in C# using minimal APIs")
    .Draft()
    .Critique()
    .Improve()
    .WithModel("codellama")
    .WithTemperature(0.3)  // Lower temperature for code
    .RunAsync();
```

### Multi-lingual Voice Assistant

```csharp
// German voice assistant
var germanResult = await VoicePipeline.Create()
    .About("Was ist maschinelles Lernen?")
    .Draft()
    .Improve()
    .WithModel("llama3")
    .WithGermanVoice("azure-key", "westeurope")
    .WithLanguage("de")
    .RunAsync("output_de.mp3");

// French voice assistant
var frenchResult = await VoicePipeline.Create()
    .About("Qu'est-ce que l'apprentissage automatique?")
    .Draft()
    .Improve()
    .WithModel("llama3")
    .WithTextToSpeech(azureTtsService)
    .WithVoice("fr-FR-DeniseNeural")
    .WithLanguage("fr")
    .RunAsync("output_fr.mp3");
```

## Requirements

- .NET 10.0 or later
- Ollama running locally or remotely
- Optional: OpenAI API key for Whisper STT or OpenAI TTS
- Optional: Azure subscription for Azure Neural TTS

## Performance Tips

1. **Temperature**: Use lower values (0.1-0.3) for deterministic tasks like code generation
2. **Stages**: Only enable stages you need - more stages = longer execution
3. **Model Selection**: Choose appropriate model size for your hardware
4. **Voice Generation**: Voice synthesis adds significant time - use only when needed

## Troubleshooting

### "Model must be set"
Make sure to call `.WithModel("model-name")` before `.RunAsync()`.

### "Topic must be specified"
Call `.About("your topic")` before running the pipeline.

### "At least one stage must be enabled"
Add at least one stage: `.Draft()`, `.Critique()`, `.Improve()`, or `.Summarize()`.

### Voice synthesis fails
- Check API keys are valid
- Verify network connectivity
- Ensure audio output path is writable
- Check TTS service quotas

## Next Steps

- Explore the full [Ouroboros documentation](../README.md)
- Check out the [examples directory](../examples/)
- Learn about [advanced pipeline features](../PIPELINE_GUIDE.md)
- Contribute to [Ouroboros development](../CONTRIBUTING.md)

## Support

For issues, questions, or contributions, please visit the [GitHub repository](https://github.com/PMeeske/Ouroboros).
