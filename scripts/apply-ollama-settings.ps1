# Optimal Ollama Configuration for AMD Ryzen Z1 Extreme + AMD Radeon Graphics
# System: 12GB RAM, 4GB VRAM, 16 logical processors

Write-Host "Configuring Ollama for optimal performance..." -ForegroundColor Cyan

# Stop Ollama if running
Write-Host "Stopping Ollama..." -ForegroundColor Yellow
Get-Process ollama -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Set environment variables
Write-Host "Setting environment variables..." -ForegroundColor Yellow

[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_GPU", "1", "User")
Write-Host "  OLLAMA_NUM_GPU = 1" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_THREAD", "12", "User")
Write-Host "  OLLAMA_NUM_THREAD = 12" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_MAX_LOADED_MODELS", "2", "User")
Write-Host "  OLLAMA_MAX_LOADED_MODELS = 2" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_MAX_VRAM", "3221225472", "User")
Write-Host "  OLLAMA_MAX_VRAM = 3GB" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_KEEP_ALIVE", "5m", "User")
Write-Host "  OLLAMA_KEEP_ALIVE = 5m" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_HOST", "127.0.0.1:11434", "User")
Write-Host "  OLLAMA_HOST = 127.0.0.1:11434" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_FLASH_ATTENTION", "1", "User")
Write-Host "  OLLAMA_FLASH_ATTENTION = 1" -ForegroundColor Green

[System.Environment]::SetEnvironmentVariable("OLLAMA_NUM_PARALLEL", "4", "User")
Write-Host "  OLLAMA_NUM_PARALLEL = 4" -ForegroundColor Green

Write-Host "`nConfiguration Summary:" -ForegroundColor Cyan
Write-Host "  Device: AMD Ryzen Z1 Extreme (16 threads)" -ForegroundColor White
Write-Host "  RAM: 12 GB | GPU: AMD Radeon 4GB VRAM" -ForegroundColor White
Write-Host "  GPU Acceleration: Enabled" -ForegroundColor White
Write-Host "  CPU Threads: 12 (75% utilization)" -ForegroundColor White
Write-Host "  Max VRAM: 3GB" -ForegroundColor White
Write-Host "  Parallel Requests: 4" -ForegroundColor White

Write-Host "`nRestarting Ollama..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
Start-Process "ollama" -ArgumentList "serve" -WindowStyle Hidden
Start-Sleep -Seconds 5

Write-Host "`nTesting connection..." -ForegroundColor Yellow
try
{
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    Write-Host "Ollama is running!" -ForegroundColor Green
    Write-Host "`nInstalled models:" -ForegroundColor Cyan
    foreach ($model in $response.models)
    {
        Write-Host "  - $($model.name)" -ForegroundColor White
    }
}
catch
{
    Write-Host "Warning: Could not connect. Start manually with: ollama serve" -ForegroundColor Yellow
}

Write-Host "`nRecommended Models:" -ForegroundColor Cyan
Write-Host "  Fast (< 1GB): qwen2.5:0.5b, tinyllama" -ForegroundColor White
Write-Host "  Balanced (1-3GB): phi3:mini, gemma2:2b" -ForegroundColor White
Write-Host "  Quality (3-5GB): llama3.2:3b, mistral:7b-q4" -ForegroundColor White

Write-Host "`nConfiguration complete!" -ForegroundColor Green
