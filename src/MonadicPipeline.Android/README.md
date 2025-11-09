# MonadicPipeline Android App

A powerful CLI interface for MonadicPipeline on Android with integrated AI provider support, Ollama management, and symbolic reasoning capabilities.

## Features

### Core Features
- **Terminal-Style Interface**: Familiar CLI experience on mobile with syntax highlighting
- **Multi-Provider AI Support**: Connect to Ollama, OpenAI, Anthropic, Google AI, Meta LLaMA, and more
- **Ollama Integration**: Full Ollama API support with authentication
- **Automatic Model Management**: Models are loaded on-demand and auto-unloaded after 5 minutes of inactivity
- **Intelligent Command Suggestions**: AI-powered auto-completion based on history and context
- **Symbolic Reasoning Engine**: Logic programming with facts, rules, and inference
- **Standalone Operation**: Fully offline-capable with local storage
- **Security**: Secure credential storage using Android SecureStorage

### Advanced Features
- **Native Shell Execution**: Run shell commands directly on Android
- **Command History**: SQLite-based persistent history with search
- **Fuzzy Matching**: Smart command suggestions with Levenshtein distance
- **Quick Actions**: One-tap access to common commands
- **Model Manager**: Visual interface for browsing and managing AI models
- **Settings Management**: Configure multiple AI providers with custom parameters
- **Knowledge Base**: Build and query logical knowledge bases
- **Streaming Responses**: Real-time AI response streaming

## Supported AI Providers

1. **Ollama** (Local) - Self-hosted models with authentication
2. **OpenAI** - GPT-3.5, GPT-4, and other OpenAI models
3. **Anthropic** - Claude 3 Haiku, Sonnet, Opus
4. **Google AI** - Gemini Pro and PaLM models
5. **Meta** - LLaMA models via Together.ai
6. **Cohere** - Command and other Cohere models
7. **Mistral AI** - Mistral models
8. **Hugging Face** - Inference API access
9. **Azure OpenAI** - Enterprise OpenAI deployment

## Requirements

- Android device running API level 21 (Android 5.0) or higher
- Internet connection for cloud AI providers
- Optional: Local Ollama server for self-hosted AI
- Permissions: Internet, Storage, Network State

## Getting the APK

### Option 1: Download from GitHub Actions (Easiest)

The Android APK is automatically built by CI/CD and available as an artifact:

1. Go to the [Actions tab](../../actions/workflows/android-build.yml) in this repository
2. Click on the latest successful workflow run
3. Download the `monadic-pipeline-android-apk` artifact
4. Extract and install the APK on your Android device

### Option 2: Build Locally

#### Prerequisites

1. Install .NET 8.0 SDK or later
2. Install .NET MAUI workload:
   ```bash
   dotnet workload install maui-android
   ```

#### Build Steps

1. Navigate to the project directory:
   ```bash
   cd src/MonadicPipeline.Android
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the APK:
   ```bash
   dotnet build -c Release -f net8.0-android
   ```

4. The APK will be located at:
   ```
   bin/Release/net8.0-android/com.adaptivesystems.monadicpipeline-Signed.apk
   ```

**Note:** The Android project is built separately from the main solution to avoid requiring MAUI workloads in all CI environments.

### Deploy to Device

To install directly on a connected Android device:

```bash
dotnet build -c Release -f net8.0-android -t:Install
```

## Usage

### Initial Setup

1. Launch the app
2. Configure your Ollama endpoint:
   ```
   config http://YOUR_SERVER_IP:11434
   ```
   Example: `config http://192.168.1.100:11434`

3. List available models:
   ```
   models
   ```

4. Pull a small model (recommended for mobile):
   ```
   pull tinyllama
   ```

### Available Commands

#### System Commands
- `help` - Show all available commands with descriptions
- `version` - Display version information
- `about` - About MonadicPipeline
- `clear` - Clear the terminal screen
- `exit` / `quit` - Exit instructions

#### Configuration
- `config <endpoint>` - Configure Ollama endpoint
  - Example: `config http://192.168.1.100:11434`
- `status` - Show current system status and connection
- `ping` - Test connection to configured endpoint

#### Model Management
- `models` - List all available models from active provider
- `pull <model>` - Download a model from Ollama
  - Example: `pull tinyllama`
- `delete <model>` - Delete a model
  - Example: `delete tinyllama`

#### AI Interaction
- `ask <question>` - Ask a question using AI
  - Example: `ask What is functional programming?`
  - Uses the active AI provider and model
  - Supports streaming responses

#### Intelligence & History
- `suggest [partial]` - Get intelligent command suggestions
  - Uses fuzzy matching and command history
  - Learns from usage patterns
- `history [count]` - Show recent command history
  - Example: `history 50`
  - Stored persistently in SQLite

#### Advanced Commands
- `shell <command>` - Execute native shell command
  - Example: `shell ls -la`
  - Includes safety validation
- `ollama <start|stop|status>` - Manage Ollama service (if supported)
- `hints` - Get efficiency hints for mobile CLI usage

## Quick Start Guide

### 1. Basic Ollama Setup

```bash
# On first launch
> config http://192.168.1.100:11434
✓ Endpoint configured

> status
System Status:
Connection: ✓ Connected
Available Models: 2

> models
Available Models:
• tinyllama (Size: 637 MB) ⭐ Recommended
• phi (Size: 1.6 GB) ⭐ Recommended

> ask What is functional programming?
[AI generates response using tinyllama]
```

### 2. Using Multiple AI Providers

```bash
# Configure OpenAI via UI
Settings > Configure AI Providers > OpenAI
- API Key: sk-...
- Model: gpt-3.5-turbo
- Save and Set as Active

> ask Explain monads in Haskell
[AI generates response using OpenAI]
```

### 3. Symbolic Reasoning

```bash
# Via UI: Settings > Symbolic Reasoning
Add Facts:
- Cat is-a Animal
- Animal is-a Living
- Dog is-a Animal

Execute Inference:
Inferred: Cat is-a Living
Inferred: Dog is-a Living

# Use with AI
> ask What do we know about cats?
[AI uses knowledge base context in response]
```

### 4. Command History and Suggestions

```bash
# Type partial command
> mo
[Shows suggestions: models, ...]

# Navigate history
Press ↑ button to cycle through previous commands

# View history
> history 20
[Shows last 20 commands]

# Get suggestions
> suggest pull
Suggestions:
• pull tinyllama
• pull phi
• pull qwen:0.5b
```
- `models` - List available models from Ollama
- `pull <model>` - Download a model from Ollama
- `ask <question>` - Ask a question using AI
- `hints` - Get efficiency tips for mobile usage
- `ping` - Test connection
- `clear` - Clear the screen
- `exit/quit` - Exit instructions

### Recommended Small Models

For optimal performance on mobile devices:

- **tinyllama** (1.1B params) - Very fast, good for quick questions
- **phi** (2.7B params) - Better reasoning, still efficient
- **qwen:0.5b** (0.5B params) - Ultra lightweight
- **gemma:2b** (2B params) - Good balance of capability and efficiency

### Example Session

```
> config http://192.168.1.100:11434
✓ Endpoint configured: http://192.168.1.100:11434

> pull tinyllama
Pulling model: tinyllama
...

> models
Available Models:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
• tinyllama
  Size: 637.4 MB
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

> ask What is functional programming?
Q: What is functional programming?

A: Functional programming is a programming paradigm...

(Model will auto-unload after 5 minutes of inactivity)

> hints
[Shows efficiency hints for mobile CLI usage]
```

## Architecture

### Core Components

#### Services Layer

1. **CliExecutor** (`Services/CliExecutor.cs`)
   - Main command dispatcher and execution engine
   - Integrates all services for unified command handling
   - Manages AI model lifecycle (auto-load/unload)

2. **CommandExecutor** (`Services/CommandExecutor.cs`)
   - Native Android shell command execution
   - Support for root and non-root operations
   - Real-time output streaming
   - Command validation and safety checks

3. **OllamaService** (`Services/OllamaService.cs`)
   - Complete Ollama API client implementation
   - Authentication support (Bearer token, Basic auth)
   - Model operations: list, pull, delete, generate
   - Streaming response handling

4. **OllamaAuthService** (`Services/OllamaAuthService.cs`)
   - Secure credential storage using Android SecureStorage
   - Support for API key and Basic authentication
   - Automatic authentication application

5. **UnifiedAIService** (`Services/UnifiedAIService.cs`)
   - Single interface for all AI providers
   - Provider-specific request formatting
   - Streaming response handling across providers
   - Integration with symbolic reasoning

6. **AIProviderService** (`Services/AIProviderService.cs`)
   - Multi-provider configuration management
   - Active provider selection
   - Settings persistence with SharedPreferences
   - Validation and defaults

7. **ModelManager** (`Services/ModelManager.cs`)
   - AI model discovery and management
   - Recommended models for mobile devices
   - Model size and parameter tracking
   - Availability checking

8. **CommandHistoryService** (`Services/CommandHistoryService.cs`)
   - SQLite-based command history persistence
   - Search and query capabilities
   - Statistics and frequency analysis
   - Automatic cleanup of old entries

9. **CommandSuggestionEngine** (`Services/CommandSuggestionEngine.cs`)
   - Intelligent command completion
   - Fuzzy matching with Levenshtein distance
   - History-based learning
   - Context-aware parameter suggestions

10. **SymbolicReasoningEngine** (`Services/SymbolicReasoningEngine.cs`)
    - Logic programming engine
    - Knowledge base with facts (Subject-Predicate-Object)
    - Inference rules (forward chaining)
    - Pattern matching with variables
    - Built-in reasoning rules (transitivity, inheritance)
    - Query evaluation and unification

#### UI Layer

1. **MainPage** (`MainPage.xaml` / `MainPage.xaml.cs`)
   - Terminal-style interface
   - Auto-suggestion overlay
   - Command history navigation (↑ button)
   - Quick action buttons
   - Real-time output display

2. **ModelManagerView** (`Views/ModelManagerView.cs`)
   - Visual model browser
   - Model details and sizes
   - Delete and manage models
   - Recommended models highlighting

3. **SettingsView** (`Views/SettingsView.cs`)
   - Ollama endpoint configuration
   - Auto-suggest toggles
   - Command history settings
   - Navigation to AI providers and reasoning

4. **AIProviderConfigView** (`Views/AIProviderConfigView.cs`)
   - Configure all AI providers
   - Provider-specific settings
   - API key management
   - Temperature and token limits
   - Active provider selection

5. **SymbolicReasoningView** (`Views/SymbolicReasoningView.cs`)
   - Interactive knowledge base editor
   - Add facts (Subject-Predicate-Object)
   - Execute inference
   - View inferred facts
   - Export/import knowledge base

### Data Flow

```
User Input (MainPage)
    ↓
CliExecutor (Command Routing)
    ↓
Service Layer (CommandExecutor, OllamaService, UnifiedAIService, etc.)
    ↓
Storage/API (SQLite, SecureStorage, HTTP APIs)
    ↓
Response Processing
    ↓
UI Update (MainPage output display)
```

### Storage Architecture

- **SQLite**: Command history, persistent data
- **SecureStorage**: API keys, credentials
- **SharedPreferences**: Settings, configurations
- **File System**: Model storage (via Ollama)

## Performance Tips

### Battery Optimization
- Use smaller models (tinyllama, phi) for longer battery life
- Close the app when not in use
- Enable device power saver mode for extended sessions

### Network Optimization
- Pull models when connected to WiFi
- Configure local Ollama server on your network for best performance
- Keep questions concise to reduce response time

### Memory Management
- Models auto-unload after 5 minutes of inactivity
- Use `clear` command to free UI memory
- Restart app if experiencing memory issues

## Troubleshooting

### "Error listing models"
- Ensure Ollama is running on the configured endpoint
- Check network connectivity
- Verify the endpoint URL is correct
- Try: `config http://YOUR_SERVER_IP:11434`

### "No model loaded"
- Pull a model first: `pull tinyllama`
- Check available models: `models`
- Ensure Ollama server is accessible

### Connection Issues
- Verify WiFi connection
- Check firewall settings on Ollama server
- Test endpoint with: `status`

## Development

### Project Structure

```
MonadicPipeline.Android/
├── MainPage.xaml           # UI layout
├── MainPage.xaml.cs        # UI code-behind
├── Services/
│   └── CliExecutor.cs      # CLI command execution
├── Platforms/
│   └── Android/
│       └── AndroidManifest.xml  # Permissions
└── MonadicPipeline.Android.csproj
```

### Adding New Commands

1. Add command handler in `CliExecutor.cs`
2. Update `ExecuteCommandAsync` switch statement
3. Add to help text in `GetHelpText()`

## License

Open Source - See main repository LICENSE file

## Links

- **Main Repository**: https://github.com/PMeeske/MonadicPipeline
- **Ollama**: https://ollama.ai/
- **.NET MAUI**: https://dotnet.microsoft.com/apps/maui

## Credits

Developed by Adaptive Systems Inc. as part of the MonadicPipeline project.
