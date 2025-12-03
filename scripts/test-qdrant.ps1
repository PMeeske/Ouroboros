#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test Qdrant vector store functionality locally
.DESCRIPTION
    This script tests Qdrant connectivity and basic operations
.PARAMETER QdrantEndpoint
    Qdrant endpoint URL (default: http://localhost:6333)
.EXAMPLE
    .\test-qdrant.ps1
.EXAMPLE
    .\test-qdrant.ps1 -QdrantEndpoint "https://your-cluster.qdrant.io"
#>

param(
    [string]$QdrantEndpoint = "http://localhost:6333",
    [string]$CollectionName = "test_collection"
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Testing Qdrant at: $QdrantEndpoint" -ForegroundColor Cyan

# Test 1: Health Check
Write-Host "`n1️⃣ Testing health endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$QdrantEndpoint/" -Method Get
    Write-Host "✅ Health check passed" -ForegroundColor Green
    Write-Host "   Version: $($health.version)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Health check failed: $_" -ForegroundColor Red
    Write-Host "   Make sure Qdrant is running at $QdrantEndpoint" -ForegroundColor Yellow
    Write-Host "   Options:" -ForegroundColor Yellow
    Write-Host "   - Run: docker compose up -d qdrant" -ForegroundColor Gray
    Write-Host "   - Download binary: https://github.com/qdrant/qdrant/releases" -ForegroundColor Gray
    Write-Host "   - Use Qdrant Cloud: https://cloud.qdrant.io/" -ForegroundColor Gray
    exit 1
}

# Test 2: List Collections
Write-Host "`n2️⃣ Listing existing collections..." -ForegroundColor Yellow
try {
    $collections = Invoke-RestMethod -Uri "$QdrantEndpoint/collections" -Method Get
    $collectionCount = $collections.result.collections.Count
    Write-Host "OK Found $collectionCount collection(s)" -ForegroundColor Green
    if ($collectionCount -gt 0) {
        $collections.result.collections | ForEach-Object {
            Write-Host "   - $($_.name) (vectors: $($_.vectors_count))" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "⚠️ Could not list collections: $_" -ForegroundColor Yellow
}

# Test 3: Create Test Collection
Write-Host "`n3️⃣ Creating test collection: $CollectionName..." -ForegroundColor Yellow
try {
    # Delete if exists
    try {
        Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$CollectionName" -Method Delete | Out-Null
        Write-Host "   Deleted existing collection" -ForegroundColor Gray
    } catch {
        # Collection doesn't exist, that's fine
    }

    $createBody = @{
        vectors = @{
            size = 384  # nomic-embed-text dimension
            distance = "Cosine"
        }
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$CollectionName" `
        -Method Put `
        -ContentType "application/json" `
        -Body $createBody | Out-Null
    
    Write-Host "✅ Collection created successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create collection: $_" -ForegroundColor Red
    exit 1
}

# Test 4: Add Test Vectors
Write-Host "`n4️⃣ Adding test vectors..." -ForegroundColor Yellow
try {
    $testVectors = @{
        points = @(
            @{
                id = 1
                vector = @(1..384 | ForEach-Object { Get-Random -Minimum -1.0 -Maximum 1.0 })
                payload = @{
                    text = "Test document 1"
                    source = "test"
                    timestamp = (Get-Date).ToString("o")
                }
            },
            @{
                id = 2
                vector = @(1..384 | ForEach-Object { Get-Random -Minimum -1.0 -Maximum 1.0 })
                payload = @{
                    text = "Test document 2"
                    source = "test"
                    timestamp = (Get-Date).ToString("o")
                }
            }
        )
    } | ConvertTo-Json -Depth 10

    Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$CollectionName/points?wait=true" `
        -Method Put `
        -ContentType "application/json" `
        -Body $testVectors | Out-Null
    
    Write-Host "✅ Added 2 test vectors" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to add vectors: $_" -ForegroundColor Red
    exit 1
}

# Test 5: Search Vectors
Write-Host "`n5️⃣ Testing vector search..." -ForegroundColor Yellow
try {
    $searchQuery = @{
        vector = @(1..384 | ForEach-Object { Get-Random -Minimum -1.0 -Maximum 1.0 })
        limit = 2
        with_payload = $true
    } | ConvertTo-Json -Depth 10

    $results = Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$CollectionName/points/search" `
        -Method Post `
        -ContentType "application/json" `
        -Body $searchQuery
    
    Write-Host "✅ Search completed successfully" -ForegroundColor Green
    Write-Host "   Found $($results.result.Count) results:" -ForegroundColor Gray
    $results.result | ForEach-Object {
        Write-Host "   - Score: $($_.score.ToString('0.0000')), Text: $($_.payload.text)" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ Search failed: $_" -ForegroundColor Red
    exit 1
}

# Test 6: Get Collection Info
Write-Host "`n6️⃣ Getting collection info..." -ForegroundColor Yellow
try {
    $info = Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$CollectionName" -Method Get
    Write-Host "✅ Collection info:" -ForegroundColor Green
    Write-Host "   Vectors count: $($info.result.vectors_count)" -ForegroundColor Gray
    Write-Host "   Points count: $($info.result.points_count)" -ForegroundColor Gray
    Write-Host "   Status: $($info.result.status)" -ForegroundColor Gray
} catch {
    Write-Host "⚠️ Could not get collection info: $_" -ForegroundColor Yellow
}

# Test 7: Cleanup
Write-Host "`n7️⃣ Cleaning up test collection..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "$QdrantEndpoint/collections/$CollectionName" -Method Delete | Out-Null
    Write-Host "✅ Test collection deleted" -ForegroundColor Green
} catch {
    Write-Host "⚠️ Could not delete test collection: $_" -ForegroundColor Yellow
}

Write-Host "`n✨ All tests completed successfully!" -ForegroundColor Green
Write-Host "`n📝 Next steps:" -ForegroundColor Cyan
Write-Host "   1. Update appsettings.Development.json:" -ForegroundColor White
Write-Host '      "VectorStore": {' -ForegroundColor Gray
Write-Host '        "Type": "Qdrant",' -ForegroundColor Gray
Write-Host "        `"ConnectionString`": `"$QdrantEndpoint`"" -ForegroundColor Gray
Write-Host '      }' -ForegroundColor Gray
Write-Host "   2. Run your application with Qdrant support" -ForegroundColor White
Write-Host "   3. Monitor Qdrant dashboard: $QdrantEndpoint/dashboard" -ForegroundColor White
