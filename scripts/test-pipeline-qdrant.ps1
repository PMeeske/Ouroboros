#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test pipeline execution with Qdrant vector store
.DESCRIPTION
    Runs a simple pipeline that ingests data and performs semantic search using Qdrant
.EXAMPLE
    .\test-pipeline-qdrant.ps1
#>

param(
    [string]$QdrantEndpoint = "http://localhost:6333"
)

$ErrorActionPreference = "Stop"

Write-Host "🧪 Testing Ouroboros with Qdrant" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Check Qdrant is running
Write-Host "`n1️⃣ Checking Qdrant status..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$QdrantEndpoint/" -Method Get
    Write-Host "OK Qdrant is running (v$($health.version))" -ForegroundColor Green
} catch {
    Write-Host "ERROR Qdrant is not running at $QdrantEndpoint" -ForegroundColor Red
    Write-Host "   Run: .\scripts\start-qdrant.ps1" -ForegroundColor Gray
    exit 1
}

# Navigate to CLI directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$cliDir = Join-Path $projectRoot "src\Ouroboros.CLI"

if (-not (Test-Path $cliDir)) {
    Write-Host "ERROR CLI directory not found: $cliDir" -ForegroundColor Red
    exit 1
}

Set-Location $cliDir

# Check if Ollama is running
Write-Host "`n2️⃣ Checking Ollama status..." -ForegroundColor Yellow
try {
    $ollama = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 3
    Write-Host "OK Ollama is running" -ForegroundColor Green
    
    # Check if required models are available
    $hasLlama3 = $ollama.models | Where-Object { $_.name -like "llama3*" }
    $hasEmbed = $ollama.models | Where-Object { $_.name -like "nomic-embed-text*" }
    
    if (-not $hasLlama3) {
        Write-Host "WARN llama3 model not found. Pulling it now..." -ForegroundColor Yellow
        Write-Host "   This may take a few minutes..." -ForegroundColor Gray
        ollama pull llama3
    }
    
    if (-not $hasEmbed) {
        Write-Host "WARN nomic-embed-text model not found. Pulling it now..." -ForegroundColor Yellow
        ollama pull nomic-embed-text
    }
} catch {
    Write-Host "ERROR Ollama is not running" -ForegroundColor Red
    Write-Host "   Start Ollama: docker compose up -d ollama" -ForegroundColor Gray
    Write-Host "   Or run: ollama serve" -ForegroundColor Gray
    exit 1
}

# Test 1: Simple ask command (no pipeline, just to verify setup)
Write-Host "`n3️⃣ Test 1: Simple question (verifying Ollama connectivity)..." -ForegroundColor Yellow
try {
    $result = dotnet run -- ask -q "Say 'Hello from Ouroboros!' in exactly 5 words" --model llama3 2>&1
    Write-Host $result -ForegroundColor Gray
    Write-Host "OK Simple ask completed" -ForegroundColor Green
} catch {
    Write-Host "ERROR Simple ask failed: $_" -ForegroundColor Red
    Write-Host "   Check that Ollama and llama3 model are working" -ForegroundColor Gray
}

# Test 2: Pipeline with Qdrant - Ingest and Query
Write-Host "`n4️⃣ Test 2: Pipeline DSL execution with Qdrant..." -ForegroundColor Yellow
Write-Host "   Creating test documents..." -ForegroundColor Gray

# Create a temporary test directory with sample documents
$testDataDir = Join-Path $env:TEMP "monadic-test-$(Get-Random)"
New-Item -ItemType Directory -Path $testDataDir | Out-Null

# Create sample documents
$docs = @(
    "Qdrant is a vector database that enables semantic search and similarity matching.",
    "Vector databases store embeddings which are numerical representations of text.",
    "Ouroboros uses functional programming patterns like monads and arrows.",
    "Docker containers provide isolated environments for running applications.",
    "The .NET runtime includes a powerful garbage collector for memory management."
)

for ($i = 0; $i -lt $docs.Count; $i++) {
    $docs[$i] | Out-File -FilePath (Join-Path $testDataDir "doc$i.txt") -Encoding utf8
}

Write-Host "   Created $($docs.Count) test documents in: $testDataDir" -ForegroundColor Gray

# Run pipeline: Ingest documents, then query
Write-Host "`n   Executing pipeline: Ingest documents -> Semantic search..." -ForegroundColor Gray
try {
    # Simple pipeline DSL that ingests and reasons
    $dsl = "SetTopic('Qdrant and vector databases') | UseDraft | UseCritique"
    
    Write-Host "   DSL: $dsl" -ForegroundColor Gray
    Write-Host "   Source: $testDataDir" -ForegroundColor Gray
    Write-Host "" -ForegroundColor Gray
    
    $pipelineResult = dotnet run -- pipeline `
        --dsl $dsl `
        --source $testDataDir `
        --model llama3 `
        --embed nomic-embed-text `
        -k 3 `
        --trace 2>&1
    
    Write-Host $pipelineResult -ForegroundColor Gray
    Write-Host "`nOK Pipeline execution completed" -ForegroundColor Green
    
} catch {
    Write-Host "ERROR Pipeline execution failed: $_" -ForegroundColor Red
}

# Check Qdrant for stored vectors
Write-Host "`n5️⃣ Verifying vectors in Qdrant..." -ForegroundColor Yellow
try {
    $collections = Invoke-RestMethod -Uri "$QdrantEndpoint/collections" -Method Get
    $collectionCount = $collections.result.collections.Count
    
    if ($collectionCount -gt 0) {
        Write-Host "OK Found $collectionCount collection(s) in Qdrant" -ForegroundColor Green
        foreach ($col in $collections.result.collections) {
            Write-Host "   - $($col.name): $($col.points_count) points, $($col.vectors_count) vectors" -ForegroundColor Gray
        }
    } else {
        Write-Host "INFO No collections found (InMemory mode may still be configured)" -ForegroundColor Yellow
        Write-Host "   To use Qdrant, ensure appsettings.Development.json has:" -ForegroundColor Gray
        Write-Host '   "VectorStore": { "Type": "Qdrant", ... }' -ForegroundColor Gray
    }
} catch {
    Write-Host "WARN Could not query Qdrant collections: $_" -ForegroundColor Yellow
}

# Cleanup
Write-Host "`n6️⃣ Cleaning up test data..." -ForegroundColor Yellow
try {
    Remove-Item -Path $testDataDir -Recurse -Force
    Write-Host "OK Test data removed" -ForegroundColor Green
} catch {
    Write-Host "WARN Could not remove test directory: $testDataDir" -ForegroundColor Yellow
}

Write-Host "`nPipeline test completed!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "   - View Qdrant dashboard: http://localhost:6333/dashboard" -ForegroundColor White
Write-Host "   - Try more complex DSL with pipeline command" -ForegroundColor White
Write-Host "   - Check collections in Qdrant dashboard" -ForegroundColor White
Write-Host "   - Run with --trace flag for detailed execution logs" -ForegroundColor White

# Return to original directory
Set-Location $projectRoot
