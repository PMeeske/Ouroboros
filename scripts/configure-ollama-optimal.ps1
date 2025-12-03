# Optimal Ollama Configuration for AMD Ryzen Z1 Extreme + AMD Radeon Graphics
# System: 12GB RAM, 4GB VRAM, 16 logical processors

Write-Host "Configuring Ollama for optimal performance on this device..." -ForegroundColor Cyan

# Stop Ollama if running
Write-Host "`nStopping Ollama service..." -ForegroundColor Yellow
Get-Process ollama -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Set system environment variables (persistent)
Write-Host "`nSetting optimal environment variables..." -ForegroundColor Yellow

# GPU Acceleration Settings
[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_GPU", "1", "User")
Write-Host "âœ“ OLLAMA_NUM_GPU = 1 (enable GPU acceleration)" -ForegroundColor Green

# Thread Configuration (80% of logical processors for optimal performance)
[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_THREAD", "12", "User")
Write-Host "âœ“ OLLAMA_NUM_THREAD = 12 (optimal for 16-core CPU)" -ForegroundColor Green

# Context Window (balanced for 12GB RAM)
[System.Environment]::SetEnvironmentVariable("OLLAMA_MAX_LOADED_MODELS", "2", "User")
Write-Host "âœ“ OLLAMA_MAX_LOADED_MODELS = 2 (keep 2 models in memory)" -ForegroundColor Green

# Memory allocation (leave ~4GB for system)
[System.Environment]::SetEnvironmentVariable("OLLAMA_MAX_VRAM", "3221225472", "User")
Write-Host "âœ“ OLLAMA_MAX_VRAM = 3GB (optimal VRAM usage for 4GB GPU)" -ForegroundColor Green

# Connection settings
[System.Environment]::SetEnvironmentVariable("OLLAMA_KEEP_ALIVE", "5m", "User")
Write-Host "âœ“ OLLAMA_KEEP_ALIVE = 5m (keep model loaded for 5 minutes)" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_HOST", "127.0.0.1:11434", "User")
Write-Host "âœ“ OLLAMA_HOST = 127.0.0.1:11434 (local access only)" -ForegroundColor Green

# Performance tuning
[System.Environment]::SetEnvironmentVariable("OLLAMA_FLASH_ATTENTION", "1", "User")
Write-Host "âœ“ OLLAMA_FLASH_ATTENTION = 1 (enable flash attention for speed)" -ForegroundColor Green

# Parallel request handling (for your 16-core CPU)
[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_PARALLEL", "4", "User")
Write-Host "âœ“ OLLAMA_NUM_PARALLEL = 4 (handle 4 parallel requests)" -ForegroundColor Green

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Configuration Summary:" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Device: AMD Ryzen Z1 Extreme (16 threads)" -ForegroundColor White
Write-Host "RAM: 12 GB | GPU: AMD Radeon 4GB VRAM" -ForegroundColor White
Write-Host "`nOptimized Settings:" -ForegroundColor White
Write-Host "  â€¢ GPU Acceleration: Enabled (AMD Radeon)" -ForegroundColor White
Write-Host "  â€¢ CPU Threads: 12 (75% of available)" -ForegroundColor White
Write-Host "  â€¢ Max VRAM: 3GB (safe allocation)" -ForegroundColor White
Write-Host "  â€¢ Parallel Requests: 4" -ForegroundColor White
Write-Host "  â€¢ Models in Memory: 2" -ForegroundColor White
Write-Host "  â€¢ Flash Attention: Enabled" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Cyan

# Restart Ollama with new settings
Write-Host "`nRestarting Ollama with new configuration..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

# Start Ollama
Start-Process "ollama" -ArgumentList "serve" -WindowStyle Hidden

Write-Host "`nWaiting for Ollama to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Test connection
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    Write-Host "âœ“ Ollama is running successfully!" -ForegroundColor Green
    Write-Host "`nInstalled models:" -ForegroundColor Cyan
    $response.models | ForEach-Object { Write-Host "  â€¢ $($_.name)" -ForegroundColor White }
} catch {
    Write-Host "âš  Warning: Could not connect to Ollama. You may need to start it manually." -ForegroundColor Yellow
    Write-Host "Run: ollama serve" -ForegroundColor White
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Recommended Models for Your Device:" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Fast and Efficient (< 1GB):" -ForegroundColor Yellow
Write-Host "  ollama pull qwen2.5:0.5b     # Already installed, ultra-fast" -ForegroundColor White
Write-Host "  ollama pull tinyllama        # 637MB, very fast responses" -ForegroundColor White
Write-Host "`nBalanced Performance (1-3GB):" -ForegroundColor Yellow
Write-Host "  ollama pull phi3:mini        # 2.3GB, good quality" -ForegroundColor White
Write-Host "  ollama pull gemma2:2b        # 1.6GB, excellent efficiency" -ForegroundColor White
Write-Host "`nBest Quality (3-5GB):" -ForegroundColor Yellow
Write-Host "  ollama pull llama3.2:3b      # 2GB, great balance" -ForegroundColor White
Write-Host "  ollama pull mistral:7b-q4    # 4.1GB quantized, high quality" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host "`nConfiguration complete!" -ForegroundColor Green
Write-Host "Changes will persist across restarts." -ForegroundColor White

