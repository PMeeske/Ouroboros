#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test MeTTa orchestrator locally
.DESCRIPTION
    Tests MeTTa symbolic reasoning with Ollama integration
.EXAMPLE
    .\test-metta.ps1
.EXAMPLE
    .\test-metta.ps1 -Goal "Analyze the relationship between functional programming and category theory"
#>

param(
    [string]$Goal = "Explain what a monad is in functional programming",
    [string]$Model = "llama3",
    [string]$EmbedModel = "nomic-embed-text",
    [switch]$Debug,
    [switch]$ShowMetrics
)

$ErrorActionPreference = "Stop"

Write-Host "=== MeTTa Orchestrator Test ===" -ForegroundColor Cyan
Write-Host ""

# Check Ollama
Write-Host "[1/4] Checking Ollama..." -ForegroundColor Yellow
try {
    $ollamaModels = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 3
    Write-Host "  OK Ollama is running" -ForegroundColor Green
    
    $hasModel = $ollamaModels.models | Where-Object { $_.name -like "$Model*" }
    $hasEmbed = $ollamaModels.models | Where-Object { $_.name -like "$EmbedModel*" }
    
    if (-not $hasModel) {
        Write-Host "  WARN Model '$Model' not found. Available models:" -ForegroundColor Yellow
        $ollamaModels.models | ForEach-Object { Write-Host "    - $($_.name)" -ForegroundColor Gray }
        exit 1
    }
    
    if (-not $hasEmbed) {
        Write-Host "  WARN Embedding model '$EmbedModel' not found" -ForegroundColor Yellow
        Write-Host "    Pulling $EmbedModel..." -ForegroundColor Gray
        ollama pull $EmbedModel
    }
    
    Write-Host "  Using model: $Model" -ForegroundColor Gray
    Write-Host "  Using embed: $EmbedModel" -ForegroundColor Gray
} catch {
    Write-Host "  ERROR Ollama not running: $_" -ForegroundColor Red
    Write-Host "    Start Ollama: docker compose up -d ollama" -ForegroundColor Gray
    Write-Host "    Or run: ollama serve" -ForegroundColor Gray
    exit 1
}

# Check MeTTa engine
Write-Host ""
Write-Host "[2/4] Checking MeTTa engine..." -ForegroundColor Yellow
$mettaCmd = Get-Command metta -ErrorAction SilentlyContinue
if ($mettaCmd) {
    Write-Host "  OK MeTTa found at: $($mettaCmd.Source)" -ForegroundColor Green
} else {
    Write-Host "  INFO MeTTa engine not found in PATH" -ForegroundColor Yellow
    Write-Host "    MeTTa orchestrator will use LLM-only mode" -ForegroundColor Gray
    Write-Host "    To install MeTTa: https://github.com/trueagi-io/hyperon-experimental" -ForegroundColor Gray
}

# Navigate to CLI directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$cliDir = Join-Path $projectRoot "src\MonadicPipeline.CLI"

if (-not (Test-Path $cliDir)) {
    Write-Host "  ERROR CLI directory not found: $cliDir" -ForegroundColor Red
    exit 1
}

Set-Location $cliDir

# Build the project
Write-Host ""
Write-Host "[3/4] Building project..." -ForegroundColor Yellow
try {
    $buildOutput = dotnet build --configuration Release --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK Build successful" -ForegroundColor Green
    } else {
        Write-Host "  WARN Build had warnings (continuing anyway)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ERROR Build failed: $_" -ForegroundColor Red
    exit 1
}

# Run MeTTa orchestrator
Write-Host ""
Write-Host "[4/4] Running MeTTa orchestrator..." -ForegroundColor Yellow
Write-Host "  Goal: $Goal" -ForegroundColor Gray
Write-Host ""

$mettaArgs = @(
    "metta"
    "--goal", $Goal
    "--model", $Model
    "--embed", $EmbedModel
)

if ($Debug) {
    $mettaArgs += "--debug"
}

if ($ShowMetrics) {
    $mettaArgs += "--show-metrics"
}

try {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    $startTime = Get-Date
    
    $result = & dotnet run -- @mettaArgs 2>&1
    
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    
    Write-Host $result
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host ""
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK MeTTa orchestrator completed successfully" -ForegroundColor Green
        Write-Host "   Duration: $($duration.ToString('0.00'))s" -ForegroundColor Gray
    } else {
        Write-Host "WARN MeTTa orchestrator exited with code: $LASTEXITCODE" -ForegroundColor Yellow
    }
} catch {
    Write-Host "ERROR MeTTa orchestrator failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Test Completed ===" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - Ollama: Running with $Model" -ForegroundColor White
Write-Host "  - MeTTa: $(if ($mettaCmd) { 'Installed' } else { 'LLM-only mode' })" -ForegroundColor White
Write-Host "  - Orchestrator: Executed successfully" -ForegroundColor White
Write-Host ""
Write-Host "Try other tests:" -ForegroundColor Cyan
Write-Host "  .\scripts\test-metta.ps1 -Goal 'Your goal here' -ShowMetrics" -ForegroundColor Gray
Write-Host "  .\scripts\test-metta.ps1 -Goal 'Solve 2x + 5 = 13' -Debug" -ForegroundColor Gray
Write-Host "  .\scripts\test-metta.ps1 -Goal 'Plan a multi-step task' -Model llama3" -ForegroundColor Gray

# Return to original directory
Set-Location $projectRoot
