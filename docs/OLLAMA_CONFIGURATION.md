# Ollama Performance Configuration Applied

## Device Specifications
- **CPU**: AMD Ryzen Z1 Extreme (16 logical processors)
- **RAM**: 12 GB (11.7 GB available)
- **GPU**: AMD Radeon Graphics (4 GB VRAM)
- **Ollama Version**: 0.13.0

## Optimized Settings Applied

### Core Performance Settings
| Variable | Value | Purpose |
|----------|-------|---------|
| `OLLAMA_NUM_GPU` | 1 | Enable GPU acceleration (AMD Radeon) |
| `OLLAMA_NUM_THREAD` | 12 | Use 75% of CPU threads (optimal for 16-core system) |
| `OLLAMA_MAX_LOADED_MODELS` | 2 | Keep up to 2 models in memory simultaneously |
| `OLLAMA_MAX_VRAM` | 3221225472 (3GB) | Safe VRAM allocation (leaves 1GB buffer for GPU) |
| `OLLAMA_NUM_PARALLEL` | 4 | Handle 4 concurrent requests |

### Additional Optimizations
| Variable | Value | Purpose |
|----------|-------|---------|
| `OLLAMA_FLASH_ATTENTION` | 1 | Enable flash attention for faster inference |
| `OLLAMA_KEEP_ALIVE` | 5m | Keep models loaded for 5 minutes after use |
| `OLLAMA_HOST` | 127.0.0.1:11434 | Local-only access (security) |

## Why These Settings?

### CPU Thread Count (12)
- Your Ryzen Z1 Extreme has 16 threads
- Using 75% (12 threads) leaves headroom for OS and other apps
- Prevents CPU throttling and maintains responsiveness

### VRAM Allocation (3GB)
- Your GPU has 4GB VRAM
- Allocating 3GB leaves 1GB for system/display
- Prevents GPU memory exhaustion and crashes

### Parallel Requests (4)
- Your 16-core CPU can handle multiple concurrent requests
- 4 parallel requests balances throughput with memory usage
- Each request uses ~2-3GB RAM depending on model

### Flash Attention
- Modern optimization technique for transformer models
- Reduces memory usage and increases speed
- Well-supported on AMD GPUs

## Recommended Models for Your Device

### Ultra-Fast (< 1GB) - Best for Quick Tasks
```bash
ollama pull qwen2.5:0.5b      # Already installed, 500MB, very fast
ollama pull tinyllama         # 637MB, great for simple tasks
```

### Balanced Performance (1-3GB) - General Purpose
```bash
ollama pull phi3:mini         # 2.3GB, Microsoft's efficient model
ollama pull gemma2:2b         # 1.6GB, Google's optimized model
ollama pull llama3.2:3b       # 2GB, Meta's compact model
```

### High Quality (3-5GB) - Best Results
```bash
ollama pull mistral:7b-q4     # 4.1GB quantized, excellent quality
ollama pull llama3:8b-q4      # 4.7GB quantized, top performance
```

### Embedding Models (Already Installed)
```bash
nomic-embed-text              # Already installed, for text embeddings
```

## Performance Expectations

### With Small Models (qwen2.5:0.5b, tinyllama)
- **Tokens/sec**: 80-150 tokens/second
- **Initial load**: < 2 seconds
- **Memory usage**: < 1GB RAM

### With Medium Models (phi3:mini, gemma2:2b)
- **Tokens/sec**: 40-80 tokens/second
- **Initial load**: 3-5 seconds
- **Memory usage**: 2-3GB RAM

### With Large Models (mistral:7b-q4)
- **Tokens/sec**: 20-40 tokens/second
- **Initial load**: 5-8 seconds
- **Memory usage**: 4-5GB RAM

## Testing Your Configuration

### Check Running Models
```bash
ollama ps
```

### Test a Model
```bash
ollama run qwen2.5:0.5b "Hello, how are you?"
```

### Check System Resource Usage
```powershell
Get-Process ollama | Select-Object ProcessName, WorkingSet, CPU
```

### Monitor Performance
```bash
ollama run qwen2.5:0.5b --verbose
```

## Troubleshooting

### If Models Run Slowly
1. Check if GPU is being used: Look for GPU utilization in Task Manager
2. Reduce `OLLAMA_NUM_PARALLEL` to 2
3. Try smaller models first

### If System Becomes Unresponsive
1. Reduce `OLLAMA_NUM_THREAD` to 8
2. Lower `OLLAMA_MAX_LOADED_MODELS` to 1
3. Use quantized (q4) models instead of full precision

### If Out of Memory Errors
1. Reduce `OLLAMA_MAX_VRAM` to 2147483648 (2GB)
2. Use smaller models
3. Close other GPU-intensive applications

## How to Modify Settings

To change any setting, use PowerShell:
```powershell
[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_THREAD", "8", "User")
Get-Process ollama | Stop-Process -Force
Start-Process "ollama" -ArgumentList "serve" -WindowStyle Hidden
```

Or edit and run: `scripts\apply-ollama-settings.ps1`

## Verification

Settings are persisted at the user level and will survive:
- System reboots
- Ollama service restarts
- Windows updates

To verify settings:
```powershell
[System.Environment]::GetEnvironmentVariable("OLLAMA_NUM_GPU", "User")
[System.Environment]::GetEnvironmentVariable("OLLAMA_NUM_THREAD", "User")
```

## Performance Tips

1. **Use Quantized Models**: q4 quantization reduces size by ~75% with minimal quality loss
2. **Preload Common Models**: Set `OLLAMA_KEEP_ALIVE` higher (e.g., "30m") for frequently used models
3. **Monitor Resource Usage**: Keep Task Manager open to watch RAM/GPU usage
4. **Close Other Apps**: When running large models, close browser tabs and other heavy apps
5. **Use Appropriate Context**: Smaller context windows are faster but hold less conversation history

## Integration with Your Ouroboros Project

Your `appsettings.json` files already point to:
- **Development**: `http://localhost:11434`
- **Production**: `http://ollama-service:11434`

The current configuration is optimal for local development with your pipeline.

---
**Configuration applied**: December 3, 2025
**Device**: AMD Ryzen Z1 Extreme Handheld/Laptop
**Status**: ✓ Active and optimized
