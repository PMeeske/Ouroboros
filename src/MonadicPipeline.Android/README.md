# MonadicPipeline Android App

A minimal CLI interface for MonadicPipeline on Android with integrated Ollama support.

## Features

- **Terminal-Style Interface**: Familiar CLI experience on mobile
- **Ollama Integration**: Connect to local or remote Ollama servers
- **Automatic Model Management**: Models are loaded on-demand and auto-unloaded after 5 minutes of inactivity
- **Small Model Support**: Optimized for lightweight models (tinyllama, phi, qwen, gemma)
- **Efficiency Hints**: Built-in guidance for optimal mobile usage
- **Standalone Operation**: Download models as needed from Ollama

## Requirements

- Android device running API level 21 (Android 5.0) or higher
- Access to an Ollama server (local network or remote)
- Internet connection for downloading models

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

- `help` - Show all available commands
- `version` - Display version information
- `about` - About MonadicPipeline
- `config <endpoint>` - Configure Ollama endpoint
- `status` - Show current system status
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

### Key Components

1. **CliExecutor** (`Services/CliExecutor.cs`)
   - Handles command parsing and execution
   - Manages Ollama provider lifecycle
   - Implements automatic model unloading
   - Provides efficiency hints

2. **MainPage** (`MainPage.xaml` / `MainPage.xaml.cs`)
   - Terminal-style UI with scrollable output
   - Command input and execution
   - History management

### Model Management

The app implements intelligent model lifecycle management:

- **Lazy Loading**: Models are only loaded when needed (first `ask` command)
- **Auto-Unloading**: Models are automatically unloaded after 5 minutes of inactivity
- **Memory Efficiency**: Only one model is kept in memory at a time
- **Smart Selection**: Automatically selects smallest available model if none specified

### Network Configuration

The app supports flexible Ollama endpoint configuration:

- Default: `http://localhost:11434` (for local testing)
- Configure any network-accessible Ollama server
- Supports both WiFi and mobile data connections
- Connection testing built-in

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
