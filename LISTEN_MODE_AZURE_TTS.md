# Enhanced Listen Mode with Azure TTS

## Overview

The Ouroboros agent now supports **full voice I/O with Azure Speech Services** - combining Azure Speech Recognition (STT) for input with Azure Text-to-Speech (TTS) for output in listen mode.

## Features

✅ **Azure Speech Recognition (STT)** - Convert speech to text  
✅ **Azure Text-to-Speech (TTS)** - Convert responses to natural speech  
✅ **Culture-aware** - Supports 50+ language/region combinations  
✅ **Neural voices** - High-quality AI-generated speech  
✅ **Custom voice selection** - Choose any Azure neural voice  
✅ **Automatic language detection** - Uses `--culture` setting for recognition language  

## Setup

### Prerequisites

1. **Azure Speech Services account**
   - Create at [Azure Portal](https://portal.azure.com)
   - Create a Speech resource in your region
   - Get your API key and region

2. **Set credentials** (choose one method):
   ```bash
   # Method 1: Environment variables
   export AZURE_SPEECH_KEY="your-api-key"
   export AZURE_SPEECH_REGION="eastus"
   
   # Method 2: User secrets (Windows)
   dotnet user-secrets set "Azure:Speech:Key" "your-api-key"
   dotnet user-secrets set "Azure:Speech:Region" "eastus"
   
   # Method 3: CLI flags (see below)
   
   # Method 4: appsettings.json
   {
     "Azure": {
       "Speech": {
         "Key": "your-api-key",
         "Region": "eastus"
       }
     }
   }
   ```

## Usage

### Basic Listen Mode (Azure STT + Local TTS)
```bash
# Listen with Azure Speech Recognition, respond with local Windows TTS
dotnet run -- ouroboros --listen
```

### Full Voice with Azure TTS
```bash
# Listen with Azure STT, respond with Azure TTS
dotnet run -- ouroboros --listen --azure-tts

# With explicit credentials
dotnet run -- ouroboros --listen --azure-tts \
  --azure-speech-key "your-key" \
  --azure-speech-region "eastus"
```

### Multilingual Listen Mode
```bash
# French: Listen for French, respond in French
dotnet run -- ouroboros --listen --azure-tts --culture fr-FR

# German: German listen and response
dotnet run -- ouroboros --listen --azure-tts --culture de-DE

# Spanish: Spanish listen and response
dotnet run -- ouroboros --listen --azure-tts --culture es-ES

# Japanese: Japanese listen and response
dotnet run -- ouroboros --listen --azure-tts --culture ja-JP

# Chinese: Mandarin listen and response
dotnet run -- ouroboros --listen --azure-tts --culture zh-CN
```

### Custom Voice Selection
```bash
# Use a specific Azure neural voice
dotnet run -- ouroboros --listen --azure-tts \
  --tts-voice "en-US-AvaMultilingualNeural"

# List of popular voices:
# Female voices:
# - en-US-AvaMultilingualNeural (default, female, multilingual)
# - en-US-JennyNeural (female, natural)
# - en-US-AmberNeural (female, warm)
# 
# Male voices:
# - en-US-GuyNeural (male, friendly)
# - en-US-ArthurNeural (male, intelligent)
#
# Other languages:
# - fr-FR-DeniseNeural (French female)
# - de-DE-AmalaNeural (German female)
# - es-ES-AlvaNeural (Spanish female)
# - ja-JP-AoNeural (Japanese female)
# - zh-CN-XiaoxiaoNeural (Chinese female)
```

### Advanced Configurations

**Full setup with all options:**
```bash
dotnet run -- ouroboros \
  --listen \
  --azure-tts \
  --culture fr-FR \
  --tts-voice "fr-FR-DeniseNeural" \
  --azure-speech-key "your-key" \
  --azure-speech-region "westeurope" \
  --enable-self-mod \
  --risk-level Medium
```

**Listen in English, respond in French:**
```bash
dotnet run -- ouroboros \
  --listen \
  --azure-tts \
  --culture en-US \
  --tts-voice "fr-FR-DeniseNeural" \
  --azure-speech-key "your-key"
```

**Voice-only mode with Azure services:**
```bash
# No text output, only voice
dotnet run -- ouroboros --voice-only --listen --azure-tts
```

## CLI Flags Reference

| Flag | Default | Description |
|------|---------|-------------|
| `--listen` | false | Enable voice input (speech-to-text) on startup |
| `--azure-tts` | false | Use Azure TTS for responses (default: local Windows SAPI) |
| `--azure-speech-key` | env var | Azure Speech API key |
| `--azure-speech-region` | "eastus" | Azure Speech region (eastus, westus, etc.) |
| `--tts-voice` | "en-US-AvaMultilingualNeural" | Azure neural voice name |
| `--culture` | en-US | Language/locale (affects STT language and TTS voice) |
| `--voice-only` | false | Voice output only, no text |
| `--text-only` | false | Text only, no voice |

## Supported Cultures & Voices

### Major Languages

**English (US)**
- Culture: `en-US`
- Voices: AvaMultilingualNeural, JennyNeural, AmberNeural, GuyNeural

**English (GB)**
- Culture: `en-GB`
- Voices: SoniaNeural, RyanNeural

**French**
- Culture: `fr-FR`
- Voices: DeniseNeural, HenriNeural

**German**
- Culture: `de-DE`
- Voices: AmalaNeural, ConradNeural

**Spanish**
- Culture: `es-ES`
- Voices: AlvaNeural, EnriqueNeural

**Italian**
- Culture: `it-IT`
- Voices: IsabellaNeural, DiegoNeural

**Japanese**
- Culture: `ja-JP`
- Voices: AoNeural, DaichiNeural

**Chinese (Simplified)**
- Culture: `zh-CN`
- Voices: XiaoxiaoNeural, YunyangNeural

**Chinese (Traditional)**
- Culture: `zh-TW`
- Voices: HsiaoChenNeural, YunJheNeural

**Portuguese (Brazil)**
- Culture: `pt-BR`
- Voices: FranciscaNeural, AntonioNeural

**Portuguese (Portugal)**
- Culture: `pt-PT`
- Voices: RitaNeural, DuarteNeural

**Russian**
- Culture: `ru-RU`
- Voices: SvetlanaNeural, DmitryNeural

**Korean**
- Culture: `ko-KR`
- Voices: SunHiNeural, InJoonNeural

**Arabic**
- Culture: `ar-SA`
- Voices: ZariyahNeural, HamedNeural

**Dutch**
- Culture: `nl-NL`
- Voices: ColetteNeural, MaartenNeural

For a complete list, visit [Azure TTS Voices](https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support?tabs=text-to-speech)

## How It Works

### Listen Mode Flow

```
User speaks
  ↓
Azure Speech Recognition (STT)
  ↓
Convert to text (using culture/language)
  ↓
Agent processes input
  ↓
Generate response
  ↓
[If --azure-tts enabled]
  ├→ Azure Text-to-Speech (TTS)
  ├→ Synthesize response audio
  └→ Play audio output
[Otherwise]
  ├→ Windows SAPI TTS
  └→ Play audio output
  ↓
Display text response
  ↓
Loop: Ready for next voice input
```

### Speech Recognition Settings

- **Language**: Automatically set from `--culture` (or `en-US` default)
- **Recognition Type**: Single utterance per loop (recognized on silence)
- **Timeout**: Automatically handles silence
- **Error Handling**: Graceful degradation on API errors

### Text-to-Speech Settings

- **Voice**: Configurable via `--tts-voice`
- **Rate**: Automatically optimized
- **Pitch**: Depends on voice characteristics
- **Culture**: Matches recognition language or explicit `--culture`
- **Format**: Audio output to speaker

## Troubleshooting

### "Voice input requires AZURE_SPEECH_KEY"
**Solution:** Set your Azure API key:
```bash
export AZURE_SPEECH_KEY="your-key"
# or
dotnet run -- ouroboros --listen --azure-speech-key "your-key"
```

### Speech recognition errors
**Check:**
- Azure Speech key is valid
- Region is correct (eastus, westus, etc.)
- Internet connection is working
- Microphone is enabled and working

**Test:**
```bash
dotnet run -- ouroboros --listen --debug
```

### Azure TTS not working
**Solution:**
1. Verify `--azure-tts` flag is set
2. Check API key and region
3. Verify voice name exists for the region/culture
4. Use `--debug` for detailed error messages

### Poor speech recognition accuracy
**Tips:**
- Speak clearly and at normal pace
- Reduce background noise
- Use correct language (`--culture` flag)
- Ensure microphone is positioned properly

## Performance Considerations

- **First request:** ~1-2 seconds (Azure API initialization)
- **Subsequent requests:** ~500ms average (STT) + ~500ms (TTS)
- **Large responses:** TTS time scales with text length
- **Network latency:** Depends on Azure region proximity

## Security Notes

⚠️ **Protect your Azure credentials:**
- Never commit API keys to version control
- Use environment variables or user secrets
- Rotate keys regularly
- Monitor Azure usage for unexpected activity

## Examples

### Personal Assistant Setup
```bash
# English-speaking personal assistant with natural voice
dotnet run -- ouroboros \
  --listen \
  --azure-tts \
  --tts-voice "en-US-JennyNeural" \
  --enable-self-mod \
  --auto-approve-low true
```

### Multilingual Bot
```bash
# Automatically switches language based on user input
dotnet run -- ouroboros --listen --azure-tts
```

### Quiet Testing Environment
```bash
# Listen with voice input, but respond with text only
dotnet run -- ouroboros --listen --text-only
```

### Immersive Experience
```bash
# Full voice with persona-specific side channel
dotnet run -- ouroboros \
  --listen \
  --azure-tts \
  --voice-channel \
  --culture en-US
```

## Integration with Other Features

Listen mode with Azure TTS works seamlessly with:

- ✅ **Governance**: `--enable-self-mod --risk-level Medium`
- ✅ **Multi-model**: `--coder-model X --reason-model Y`
- ✅ **Tools**: All tools work with voice input
- ✅ **Skills**: Learned skills accessible via voice
- ✅ **Personality**: Persona system works with voice
- ✅ **Autonomous mind**: Thinks and acts based on voice goals
- ✅ **Browser**: Can navigate web via voice commands

## Related Documentation

- [Agent Mode Guide](./docs/OUROBOROS_AGENT_MODE.md)
- [Voice Configuration](./docs/VOICE_CONFIGURATION.md)
- [Azure Speech Services](https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/)
- [Culture Codes](./docs/CULTURE_SUPPORT.md)
