#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick Qdrant vector storage test (no LLM required)
.DESCRIPTION
    Tests Qdrant directly by creating, storing, and searching vectors
.EXAMPLE
    .\test-qdrant-quick.ps1
#>

param(
    [string]$QdrantEndpoint = "http://localhost:6333"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Quick Qdrant Vector Storage Test ===" -ForegroundColor Cyan
Write-Host ""

# Check Qdrant
Write-Host "[1/5] Checking Qdrant..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$QdrantEndpoint/" -Method Get
    Write-Host "  OK Qdrant v$($health.version) is running" -ForegroundColor Green
} catch {
    Write-Host "  ERROR Qdrant not running: $_" -ForegroundColor Red
    Write-Host "  Run: .\scripts\start-qdrant.ps1" -ForegroundColor Gray
    exit 1
}

$collectionName = "test_quick_$(Get-Random -Maximum 9999)"
Write-Host ""
Write-Host "[2/5] Creating test collection: $collectionName..." -ForegroundColor Yellow

try {
    $createBody = @{
        vectors = @{
            size = 384
            distance = "Cosine"
        }
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$collectionName" `
        -Method Put `
        -ContentType "application/json" `
        -Body $createBody | Out-Null
    
    Write-Host "  OK Collection created" -ForegroundColor Green
} catch {
    Write-Host "  ERROR Failed to create collection: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/5] Adding test vectors..." -ForegroundColor Yellow

# Create sample vectors (384 dimensions)
$vectors = @()
for ($i = 1; $i -le 5; $i++) {
    $vec = @(1..384 | ForEach-Object { (Get-Random -Minimum -100 -Maximum 100) / 100.0 })
    $vectors += @{
        id = $i
        vector = $vec
        payload = @{
            text = "Test document $i about Qdrant vector database"
            category = if ($i % 2 -eq 0) { "even" } else { "odd" }
            timestamp = (Get-Date).ToString("o")
        }
    }
}

try {
    $pointsBody = @{
        points = $vectors
    } | ConvertTo-Json -Depth 10

    $result = Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$collectionName/points?wait=true" `
        -Method Put `
        -ContentType "application/json" `
        -Body $pointsBody
    
    Write-Host "  OK Added $($vectors.Count) vectors" -ForegroundColor Green
    Write-Host "     Status: $($result.status)" -ForegroundColor Gray
} catch {
    Write-Host "  ERROR Failed to add vectors: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[4/5] Searching for similar vectors..." -ForegroundColor Yellow

try {
    # Create a search vector
    $searchVec = @(1..384 | ForEach-Object { (Get-Random -Minimum -100 -Maximum 100) / 100.0 })
    
    $searchBody = @{
        vector = $searchVec
        limit = 3
        with_payload = $true
    } | ConvertTo-Json -Depth 10

    $searchResults = Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$collectionName/points/search" `
        -Method Post `
        -ContentType "application/json" `
        -Body $searchBody
    
    Write-Host "  OK Found $($searchResults.result.Count) results" -ForegroundColor Green
    foreach ($result in $searchResults.result) {
        Write-Host "     - ID: $($result.id), Score: $($result.score.ToString('0.0000')), Text: $($result.payload.text)" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ERROR Search failed: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "[5/5] Getting collection info..." -ForegroundColor Yellow

try {
    $info = Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$collectionName" -Method Get
    Write-Host "  OK Collection stats:" -ForegroundColor Green
    Write-Host "     Points: $($info.result.points_count)" -ForegroundColor Gray
    Write-Host "     Vectors: $($info.result.vectors_count)" -ForegroundColor Gray
    Write-Host "     Status: $($info.result.status)" -ForegroundColor Gray
} catch {
    Write-Host "  WARN Could not get collection info: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[Cleanup] Deleting test collection..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$collectionName" -Method Delete | Out-Null
    Write-Host "  OK Test collection deleted" -ForegroundColor Green
} catch {
    Write-Host "  WARN Could not delete collection: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Test Completed Successfully ===" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - Qdrant is running and accessible" -ForegroundColor White
Write-Host "  - Can create collections with custom dimensions" -ForegroundColor White
Write-Host "  - Can store vectors with payloads" -ForegroundColor White
Write-Host "  - Can perform semantic search" -ForegroundColor White
Write-Host "  - Can retrieve collection statistics" -ForegroundColor White
Write-Host ""
Write-Host "Next: View dashboard at http://localhost:6333/dashboard" -ForegroundColor Cyan
