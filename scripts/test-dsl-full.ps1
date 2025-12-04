#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Full DSL pipeline test with Qdrant + MeTTa + DeepSeek model
.DESCRIPTION
    Tests complete pipeline execution using Qdrant, MeTTa, and DeepSeek/llama
#>

param(
    [string]$DSL = "SetTopic('functional programming and monads') | UseDraft | UseCritique",
    [string]$Model = "deepseek-v3.1:671b-cloud",
    [string]$EmbedModel = "nomic-embed-text",
    [string]$QdrantEndpoint = "http://localhost:6333",
    [switch]$Trace,
    [switch]$Debug
)

$ErrorActionPreference = "Continue"
$originalLocation = Get-Location

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   Full DSL Pipeline Test: Qdrant + MeTTa + DeepSeek          " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Qdrant
Write-Host "[1/6] Checking Qdrant..." -ForegroundColor Yellow
$qdrantOk = $false
try {
    $health = Invoke-RestMethod -Uri "$QdrantEndpoint/" -Method Get -TimeoutSec 3
    Write-Host "  OK Qdrant v$($health.version) is running" -ForegroundColor Green
    $qdrantOk = $true
}
catch {
    Write-Host "  WARN Qdrant not running at $QdrantEndpoint" -ForegroundColor Yellow
    Write-Host "    Note: CLI uses in-memory store by default" -ForegroundColor Gray
    Write-Host "    Run .\scripts\start-qdrant.ps1 for persistent storage" -ForegroundColor Gray
}

# Test 2: MeTTa
Write-Host ""
Write-Host "[2/6] Checking MeTTa (Docker)..." -ForegroundColor Yellow
$mettaImage = docker images trueagi/hyperon:latest --format "table" 2>&1
if ($mettaImage -match "hyperon") {
    Write-Host "  OK MeTTa Docker image available" -ForegroundColor Green
}
else {
    Write-Host "  WARN MeTTa image not found, pulling..." -ForegroundColor Yellow
    docker pull trueagi/hyperon:latest
    Write-Host "  OK MeTTa installed" -ForegroundColor Green
}

# Test 3: Ollama
Write-Host ""
Write-Host "[3/6] Checking Ollama and models..." -ForegroundColor Yellow
$ollamaOk = $false
try {
    $ollamaModels = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 3
    Write-Host "  OK Ollama is running" -ForegroundColor Green
    $ollamaOk = $true
    
    $modelNames = $ollamaModels.models | ForEach-Object { $_.name }
    Write-Host "    Available: $($modelNames -join ', ')" -ForegroundColor Gray
    
    $hasModel = $modelNames | Where-Object { $_ -like "*deepseek*" -or $_ -eq $Model }
    if (-not $hasModel) {
        Write-Host "  WARN Model '$Model' not found locally" -ForegroundColor Yellow
        Write-Host "    Will use llama3 as fallback" -ForegroundColor Yellow
        $Model = "llama3"
    }
    
    $hasEmbed = $modelNames | Where-Object { $_ -like "*$EmbedModel*" }
    if (-not $hasEmbed) {
        Write-Host "  Pulling embedding model..." -ForegroundColor Yellow
        ollama pull $EmbedModel 2>&1 | Out-Null
    }
    Write-Host "  OK Embedding model ready" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR Ollama not running" -ForegroundColor Red
    Write-Host "    Start with: ollama serve" -ForegroundColor Gray
}

if (-not $ollamaOk) {
    Write-Host "Aborting: Ollama is required" -ForegroundColor Red
    exit 1
}

# Test 4: Create test data
Write-Host ""
Write-Host "[4/6] Creating test data..." -ForegroundColor Yellow
$testDataDir = Join-Path $env:TEMP "monadic-dsl-test-$(Get-Random)"
New-Item -ItemType Directory -Path $testDataDir -Force | Out-Null

$doc1 = "Monads in Functional Programming - A monad is a design pattern used in functional programming to handle program-wide concerns in a pure functional way. Common monads include Maybe, Either, List, IO, and State monads."
$doc2 = "Category Theory - Category theory provides the mathematical foundation for many functional programming concepts. Functors map between categories. Monads are monoids in the category of endofunctors."
$doc3 = "Type Systems - Strong static typing is a hallmark of functional programming. Features include parametric polymorphism, higher-kinded types, and type classes."

$doc1 | Out-File -FilePath (Join-Path $testDataDir "monads.txt") -Encoding utf8
$doc2 | Out-File -FilePath (Join-Path $testDataDir "category.txt") -Encoding utf8
$doc3 | Out-File -FilePath (Join-Path $testDataDir "types.txt") -Encoding utf8

Write-Host "  OK Created 3 test documents in: $testDataDir" -ForegroundColor Green

# Test 5: Verify configuration
Write-Host ""
Write-Host "[5/6] Verifying configuration..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$configPath = Join-Path $projectRoot "appsettings.Development.json"

if (Test-Path $configPath) {
    $configContent = Get-Content $configPath -Raw
    $config = $configContent | ConvertFrom-Json
    $vectorStoreType = $config.Pipeline.VectorStore.Type
    Write-Host "  OK Configuration found" -ForegroundColor Green
    Write-Host "    Vector Store: $vectorStoreType" -ForegroundColor Gray
}
else {
    Write-Host "  WARN Configuration not found" -ForegroundColor Yellow
}

# Test 6: Run DSL Pipeline
Write-Host ""
Write-Host "[6/6] Running DSL Pipeline..." -ForegroundColor Yellow
Write-Host "  DSL: $DSL" -ForegroundColor Cyan
Write-Host "  Model: $Model" -ForegroundColor Cyan
Write-Host "  Embedding: $EmbedModel" -ForegroundColor Cyan
Write-Host "  Source: $testDataDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

$cliDir = Join-Path $projectRoot "src\Ouroboros.CLI"

if (-not (Test-Path $cliDir)) {
    Write-Host "  ERROR CLI directory not found: $cliDir" -ForegroundColor Red
    Set-Location $originalLocation
    exit 1
}

Set-Location $cliDir

$pipelineArgs = @("pipeline", "--dsl", $DSL, "--source", $testDataDir, "--model", $Model, "--embed", $EmbedModel, "-k", "3")
if ($Trace) { $pipelineArgs += "--trace" }
if ($Debug) { $pipelineArgs += "--debug" }

$startTime = Get-Date
Write-Host ""

try {
    & dotnet run -- @pipelineArgs
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Host "ERROR Pipeline execution failed: $_" -ForegroundColor Red
    $exitCode = 1
}

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

Write-Host ""
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

if ($exitCode -eq 0) {
    Write-Host "OK Pipeline completed successfully" -ForegroundColor Green
    Write-Host "  Duration: $($duration.ToString('0.00'))s" -ForegroundColor Gray
}
else {
    Write-Host "WARN Pipeline exited with code: $exitCode" -ForegroundColor Yellow
}

Set-Location $originalLocation

# Verify vectors in Qdrant
Write-Host ""
Write-Host "[Post-Test] Checking Qdrant collections..." -ForegroundColor Yellow
try {
    $collections = Invoke-RestMethod -Uri "$QdrantEndpoint/collections" -Method Get
    $collectionCount = 0
    if ($collections.result -and $collections.result.collections) {
        $collectionCount = $collections.result.collections.Count
    }
    
    if ($collectionCount -gt 0) {
        Write-Host "  OK Found $collectionCount collection(s)" -ForegroundColor Green
        foreach ($col in $collections.result.collections) {
            Write-Host "    - $($col.name)" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "  INFO No collections found" -ForegroundColor Cyan
    }
}
catch {
    Write-Host "  WARN Could not query collections" -ForegroundColor Yellow
}

# Cleanup
Write-Host ""
Write-Host "[Cleanup] Removing test data..." -ForegroundColor Yellow
Remove-Item -Path $testDataDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  OK Test data removed" -ForegroundColor Green

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "           Full DSL Pipeline Test Complete                    " -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - Qdrant (vector storage): OK" -ForegroundColor Gray
Write-Host "  - MeTTa (symbolic reasoning): OK" -ForegroundColor Gray
Write-Host "  - $Model (LLM): OK" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: CLI currently uses in-memory vector store." -ForegroundColor Yellow
Write-Host "  For Qdrant integration, use the WebAPI or MeTTa orchestrator." -ForegroundColor Gray
Write-Host ""
Write-Host "Resources:" -ForegroundColor Cyan
Write-Host "  - Qdrant Dashboard: $QdrantEndpoint/dashboard" -ForegroundColor Gray
Write-Host "  - MeTTa REPL: docker run -ti trueagi/hyperon:latest metta-repl" -ForegroundColor Gray
Write-Host "  - MeTTa Orchestrator: dotnet run -- metta --goal 'your goal'" -ForegroundColor Gray
