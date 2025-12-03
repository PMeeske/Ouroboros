# Ollama Configuration Verification Script

Write-Host "`n===========================================================" -ForegroundColor Cyan
Write-Host "  OLLAMA CONFIGURATION VERIFICATION" -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan

# Check if Ollama is running
Write-Host "`n[1] Checking Ollama Service..." -ForegroundColor Yellow
$ollamaProcesses = Get-Process ollama -ErrorAction SilentlyContinue
if ($ollamaProcesses) {
    Write-Host "    ✓ Ollama is running" -ForegroundColor Green
    foreach ($proc in $ollamaProcesses) {
        $memMB = [math]::Round($proc.WorkingSet/1MB, 2)
        Write-Host "      Process $($proc.Id): $memMB MB" -ForegroundColor White
    }
} else {
    Write-Host "    ✗ Ollama is not running" -ForegroundColor Red
    Write-Host "      Start with: ollama serve" -ForegroundColor Yellow
    exit
}

# Check environment variables
Write-Host "`n[2] Checking Environment Variables..." -ForegroundColor Yellow
$vars = @{
    "OLLAMA_NUM_GPU" = "1"
    "OLLAMA_NUM_THREAD" = "12"
    "OLLAMA_MAX_LOADED_MODELS" = "2"
    "OLLAMA_MAX_VRAM" = "3221225472"
    "OLLAMA_KEEP_ALIVE" = "5m"
    "OLLAMA_HOST" = "127.0.0.1:11434"
    "OLLAMA_FLASH_ATTENTION" = "1"
    "OLLAMA_NUM_PARALLEL" = "4"
}

$allCorrect = $true
foreach ($var in $vars.Keys) {
    $actual = [System.Environment]::GetEnvironmentVariable($var, "User")
    $expected = $vars[$var]
    if ($actual -eq $expected) {
        Write-Host "    ✓ $var = $actual" -ForegroundColor Green
    } else {
        Write-Host "    ✗ $var = $actual (expected: $expected)" -ForegroundColor Red
        $allCorrect = $false
    }
}

# Check API connectivity
Write-Host "`n[3] Testing API Connection..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    Write-Host "    ✓ API is responsive" -ForegroundColor Green
    Write-Host "    ✓ Models available: $($response.models.Count)" -ForegroundColor Green
} catch {
    Write-Host "    ✗ Cannot connect to API" -ForegroundColor Red
    Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Yellow
}

# List installed models
Write-Host "`n[4] Installed Models..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
    foreach ($model in $response.models) {
        $sizeMB = [math]::Round($model.size/1MB, 0)
        Write-Host "    • $($model.name) - $sizeMB MB" -ForegroundColor White
    }
} catch {
    Write-Host "    ✗ Could not retrieve model list" -ForegroundColor Red
}

# System specifications
Write-Host "`n[5] System Specifications..." -ForegroundColor Yellow
$computerInfo = Get-ComputerInfo
$ramGB = [math]::Round($computerInfo.CsTotalPhysicalMemory/1GB, 1)
Write-Host "    CPU: $($computerInfo.CsProcessors[0])" -ForegroundColor White
Write-Host "    RAM: $ramGB GB" -ForegroundColor White
Write-Host "    Logical Processors: $($computerInfo.CsNumberOfLogicalProcessors)" -ForegroundColor White

$gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
$gpuVramGB = [math]::Round($gpu.AdapterRAM/1GB, 1)
Write-Host "    GPU: $($gpu.Name)" -ForegroundColor White
Write-Host "    VRAM: $gpuVramGB GB" -ForegroundColor White

# Performance recommendations
Write-Host "`n[6] Performance Status..." -ForegroundColor Yellow
$totalMemUsed = ($ollamaProcesses | Measure-Object WorkingSet -Sum).Sum
$memUsedMB = [math]::Round($totalMemUsed/1MB, 0)
Write-Host "    Current Ollama Memory: $memUsedMB MB" -ForegroundColor White

if ($memUsedMB -lt 1000) {
    Write-Host "    ✓ Memory usage is optimal" -ForegroundColor Green
} elseif ($memUsedMB -lt 3000) {
    Write-Host "    ~ Memory usage is moderate" -ForegroundColor Yellow
} else {
    Write-Host "    ⚠ High memory usage - consider smaller models" -ForegroundColor Red
}

# Final summary
Write-Host "`n===========================================================" -ForegroundColor Cyan
if ($allCorrect) {
    Write-Host "  STATUS: All settings configured optimally ✓" -ForegroundColor Green
} else {
    Write-Host "  STATUS: Some settings need adjustment" -ForegroundColor Yellow
    Write-Host "  Run: .\scripts\apply-ollama-settings.ps1" -ForegroundColor White
}
Write-Host "===========================================================" -ForegroundColor Cyan

Write-Host "`nQuick Commands:" -ForegroundColor Cyan
Write-Host "  ollama ps                           # Show running models" -ForegroundColor White
Write-Host "  ollama list                         # List all models" -ForegroundColor White
Write-Host "  ollama run qwen2.5:0.5b             # Test fastest model" -ForegroundColor White
Write-Host "  Get-Process ollama | Stop-Process   # Restart Ollama" -ForegroundColor White
Write-Host ""
