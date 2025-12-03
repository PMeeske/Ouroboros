#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start Qdrant vector database locally
.DESCRIPTION
    This script starts Qdrant using Docker Compose and verifies it's working
.PARAMETER TestAfterStart
    Run tests after starting Qdrant
.EXAMPLE
    .\start-qdrant.ps1
.EXAMPLE
    .\start-qdrant.ps1 -TestAfterStart
#>

param(
    [switch]$TestAfterStart
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "🚀 Starting Qdrant Vector Database" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

# Check if Docker is running
Write-Host "`n1️⃣ Checking Docker status..." -ForegroundColor Yellow
try {
    $dockerVersion = docker --version
    Write-Host "✅ Docker is installed: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is not installed or not in PATH" -ForegroundColor Red
    Write-Host "`n💡 Install Docker Desktop:" -ForegroundColor Yellow
    Write-Host "   Run: .\scripts\install-docker.ps1" -ForegroundColor Gray
    Write-Host "   Or visit: https://www.docker.com/products/docker-desktop/" -ForegroundColor Gray
    exit 1
}

# Check if Docker daemon is running
Write-Host "`n2️⃣ Checking Docker daemon..." -ForegroundColor Yellow
try {
    docker ps | Out-Null
    Write-Host "✅ Docker daemon is running" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker daemon is not running" -ForegroundColor Red
    Write-Host "`n💡 Please start Docker Desktop:" -ForegroundColor Yellow
    Write-Host "   1. Open Docker Desktop from Start Menu" -ForegroundColor Gray
    Write-Host "   2. Wait for Docker to start (whale icon in system tray)" -ForegroundColor Gray
    Write-Host "   3. Run this script again" -ForegroundColor Gray
    exit 1
}

# Navigate to project root
Set-Location $projectRoot

# Check if Qdrant is already running
Write-Host "`n3️⃣ Checking for existing Qdrant container..." -ForegroundColor Yellow
$existingContainer = docker ps -a --filter "name=qdrant" --format "{{.Names}}"
if ($existingContainer) {
    Write-Host "⚠️  Qdrant container already exists: $existingContainer" -ForegroundColor Yellow
    $status = docker ps --filter "name=qdrant" --format "{{.Status}}"
    if ($status) {
        Write-Host "   Status: Running ✅" -ForegroundColor Green
        Write-Host "`n   Qdrant is already running at http://localhost:6333" -ForegroundColor Cyan
        Write-Host "   Dashboard: http://localhost:6333/dashboard" -ForegroundColor Cyan
        
        if ($TestAfterStart) {
            Write-Host "`n🧪 Running tests..." -ForegroundColor Yellow
            & "$scriptDir\test-qdrant.ps1"
        }
        exit 0
    } else {
        Write-Host "   Status: Stopped 🛑" -ForegroundColor Yellow
        Write-Host "   Starting existing container..." -ForegroundColor Gray
        docker start qdrant
        Start-Sleep -Seconds 3
    }
} else {
    # Start Qdrant using docker compose
    Write-Host "`n4️⃣ Starting Qdrant with Docker Compose..." -ForegroundColor Yellow
    try {
        docker compose up -d qdrant
        Write-Host "✅ Qdrant container started" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to start Qdrant: $_" -ForegroundColor Red
        exit 1
    }
}

# Wait for Qdrant to be ready
Write-Host "`n5️⃣ Waiting for Qdrant to be ready..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$ready = $false

while ($attempt -lt $maxAttempts -and -not $ready) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:6333/" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $ready = $true
        }
    } catch {
        # Still waiting
    }
    
    if (-not $ready) {
        Write-Host "." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds 1
        $attempt++
    }
}

Write-Host ""

if ($ready) {
    Write-Host "✅ Qdrant is ready!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Qdrant might not be ready yet. Check logs with: docker logs qdrant" -ForegroundColor Yellow
}

# Display connection info
Write-Host "`n📊 Qdrant Connection Info:" -ForegroundColor Cyan
Write-Host "   HTTP API: http://localhost:6333" -ForegroundColor White
Write-Host "   gRPC API: http://localhost:6334" -ForegroundColor White
Write-Host "   Dashboard: http://localhost:6333/dashboard" -ForegroundColor White
Write-Host "   Health: http://localhost:6333/health" -ForegroundColor White

# Show logs command
Write-Host "`n📝 Useful Commands:" -ForegroundColor Cyan
Write-Host "   View logs: docker logs qdrant" -ForegroundColor Gray
Write-Host "   Stop Qdrant: docker stop qdrant" -ForegroundColor Gray
Write-Host "   Remove Qdrant: docker compose down -v" -ForegroundColor Gray
Write-Host "   Test Qdrant: .\scripts\test-qdrant.ps1" -ForegroundColor Gray

# Run tests if requested
if ($TestAfterStart -and $ready) {
    Write-Host "`n🧪 Running tests..." -ForegroundColor Yellow
    & "$scriptDir\test-qdrant.ps1"
}

Write-Host "`n✨ Qdrant is ready to use!" -ForegroundColor Green
Write-Host "`n💡 Next step: Change 'Type' to 'Qdrant' in appsettings.Development.json" -ForegroundColor Cyan
