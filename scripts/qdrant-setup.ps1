# qdrant-setup.ps1 - Convenience script for setting up and managing Qdrant vector store locally (Windows)
# Usage: .\scripts\qdrant-setup.ps1 [command]

param(
    [Parameter(Position=0)]
    [string]$Command = "help",
    
    [Parameter(Position=1)]
    [string]$Parameter
)

# Configuration
$QdrantHttpPort = 6333
$QdrantGrpcPort = 6334
$QdrantConnectionString = "http://localhost:$QdrantHttpPort"
$DefaultCollection = "pipeline_vectors"

# Helper functions
function Write-Header {
    param([string]$Message)
    Write-Host "========================================" -ForegroundColor Blue
    Write-Host $Message -ForegroundColor Blue
    Write-Host "========================================" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Cyan
}

# Check if Docker is installed
function Test-Docker {
    try {
        $null = docker --version
        Write-Success "Docker is installed"
        return $true
    }
    catch {
        Write-Error "Docker is not installed. Please install Docker Desktop first."
        return $false
    }
}

# Check if docker-compose is installed
function Test-DockerCompose {
    try {
        $null = docker-compose --version
        Write-Success "Docker Compose is installed"
        return $true
    }
    catch {
        Write-Error "Docker Compose is not installed. Please install Docker Compose first."
        return $false
    }
}

# Check if Qdrant is running
function Test-QdrantRunning {
    try {
        $containers = docker ps | Select-String "qdrant"
        return $null -ne $containers
    }
    catch {
        return $false
    }
}

# Check if Qdrant is healthy
function Test-QdrantHealthy {
    try {
        $response = Invoke-WebRequest -Uri "$QdrantConnectionString/health" -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

# Start Qdrant
function Start-Qdrant {
    Write-Header "Starting Qdrant Vector Database"
    
    if (-not (Test-Docker)) { exit 1 }
    if (-not (Test-DockerCompose)) { exit 1 }
    
    if (Test-QdrantRunning) {
        Write-Warning "Qdrant is already running"
    }
    else {
        Write-Info "Starting Qdrant container..."
        docker-compose up -d qdrant
        
        Write-Info "Waiting for Qdrant to become healthy..."
        $maxRetries = 30
        $retryCount = 0
        
        while ($retryCount -lt $maxRetries) {
            if (Test-QdrantHealthy) {
                Write-Success "Qdrant is running and healthy!"
                break
            }
            Start-Sleep -Seconds 1
            $retryCount++
            Write-Host "." -NoNewline
        }
        Write-Host ""
        
        if ($retryCount -eq $maxRetries) {
            Write-Error "Qdrant failed to start within 30 seconds"
            Write-Info "Check logs with: docker logs qdrant"
            exit 1
        }
    }
    
    Write-Success "Qdrant HTTP API: $QdrantConnectionString"
    Write-Success "Qdrant gRPC API: localhost:$QdrantGrpcPort"
    Write-Success "Qdrant Dashboard: $QdrantConnectionString/dashboard"
}

# Stop Qdrant
function Stop-Qdrant {
    Write-Header "Stopping Qdrant Vector Database"
    
    if (Test-QdrantRunning) {
        Write-Info "Stopping Qdrant container..."
        docker-compose stop qdrant
        Write-Success "Qdrant stopped"
    }
    else {
        Write-Warning "Qdrant is not running"
    }
}

# Restart Qdrant
function Restart-Qdrant {
    Write-Header "Restarting Qdrant Vector Database"
    Stop-Qdrant
    Start-Sleep -Seconds 2
    Start-Qdrant
}

# Check Qdrant status
function Get-QdrantStatus {
    Write-Header "Qdrant Status"
    
    if (Test-QdrantRunning) {
        Write-Success "Qdrant container is running"
        
        if (Test-QdrantHealthy) {
            Write-Success "Qdrant is healthy and responding"
            
            # Get version
            try {
                $response = Invoke-RestMethod -Uri "$QdrantConnectionString/" -Method Get
                $version = $response.version
                Write-Info "Qdrant version: $version"
            }
            catch {
                Write-Info "Qdrant version: unknown"
            }
            
            # Get collections
            Write-Info "Fetching collections..."
            try {
                $collections = Invoke-RestMethod -Uri "$QdrantConnectionString/collections" -Method Get
                
                if ($collections.result.collections.Count -eq 0) {
                    Write-Info "No collections found"
                }
                else {
                    Write-Info "Collections:"
                    foreach ($collection in $collections.result.collections) {
                        Write-Host "  - $($collection.name)"
                    }
                }
            }
            catch {
                Write-Warning "Could not fetch collections"
            }
        }
        else {
            Write-Error "Qdrant is not responding to health checks"
        }
    }
    else {
        Write-Error "Qdrant container is not running"
        Write-Info "Start Qdrant with: .\scripts\qdrant-setup.ps1 start"
    }
}

# View Qdrant logs
function Get-QdrantLogs {
    Write-Header "Qdrant Logs"
    
    if (Test-QdrantRunning) {
        docker logs qdrant --tail 50 --follow
    }
    else {
        Write-Error "Qdrant is not running"
        exit 1
    }
}

# Configure MonadicPipeline to use Qdrant
function Set-PipelineConfiguration {
    Write-Header "Configuring MonadicPipeline for Qdrant"
    
    # Check if .env exists
    if (-not (Test-Path .env)) {
        Write-Info "Creating .env file from .env.example..."
        Copy-Item .env.example .env
    }
    
    # Read .env file
    $envContent = Get-Content .env
    
    # Update or add configuration
    $updated = $false
    $newContent = @()
    
    foreach ($line in $envContent) {
        if ($line -match "^PIPELINE__VectorStore__Type=") {
            $newContent += "PIPELINE__VectorStore__Type=Qdrant"
            $updated = $true
        }
        elseif ($line -match "^.*PIPELINE__VectorStore__ConnectionString=") {
            $newContent += "PIPELINE__VectorStore__ConnectionString=$QdrantConnectionString"
        }
        elseif ($line -match "^.*PIPELINE__VectorStore__DefaultCollection=") {
            $newContent += "PIPELINE__VectorStore__DefaultCollection=$DefaultCollection"
        }
        else {
            $newContent += $line
        }
    }
    
    # Add missing entries
    if (-not $updated) {
        $newContent += "PIPELINE__VectorStore__Type=Qdrant"
        $newContent += "PIPELINE__VectorStore__ConnectionString=$QdrantConnectionString"
        $newContent += "PIPELINE__VectorStore__DefaultCollection=$DefaultCollection"
    }
    
    # Write back to file
    $newContent | Set-Content .env
    
    Write-Success "Configuration updated!"
    Write-Info "Current Qdrant settings in .env:"
    Write-Host "  Type: Qdrant"
    Write-Host "  Connection: $QdrantConnectionString"
    Write-Host "  Collection: $DefaultCollection"
}

# List collections
function Get-QdrantCollections {
    Write-Header "Qdrant Collections"
    
    if (-not (Test-QdrantHealthy)) {
        Write-Error "Qdrant is not running or not healthy"
        Write-Info "Start Qdrant with: .\scripts\qdrant-setup.ps1 start"
        exit 1
    }
    
    try {
        $response = Invoke-RestMethod -Uri "$QdrantConnectionString/collections" -Method Get
        
        if ($response.result.collections.Count -eq 0) {
            Write-Info "No collections found"
        }
        else {
            foreach ($collection in $response.result.collections) {
                $vectors = if ($collection.vectors_count) { $collection.vectors_count } else { 0 }
                $points = if ($collection.points_count) { $collection.points_count } else { 0 }
                Write-Host "  $($collection.name): $vectors vectors, $points points"
            }
        }
    }
    catch {
        Write-Error "Failed to fetch collections: $_"
        exit 1
    }
}

# Delete a collection
function Remove-QdrantCollection {
    param([string]$CollectionName)
    
    if ([string]::IsNullOrEmpty($CollectionName)) {
        Write-Error "Collection name required"
        Write-Host "Usage: .\scripts\qdrant-setup.ps1 delete-collection <collection-name>"
        exit 1
    }
    
    Write-Header "Deleting Collection: $CollectionName"
    
    if (-not (Test-QdrantHealthy)) {
        Write-Error "Qdrant is not running or not healthy"
        exit 1
    }
    
    Write-Warning "Are you sure you want to delete collection '$CollectionName'? (y/N)"
    $confirm = Read-Host
    
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Info "Deletion cancelled"
        exit 0
    }
    
    try {
        $response = Invoke-RestMethod -Uri "$QdrantConnectionString/collections/$CollectionName" -Method Delete
        
        if ($response.status -eq "ok") {
            Write-Success "Collection '$CollectionName' deleted successfully"
        }
        else {
            Write-Error "Failed to delete collection"
            Write-Host ($response | ConvertTo-Json)
            exit 1
        }
    }
    catch {
        Write-Error "Failed to delete collection: $_"
        exit 1
    }
}

# Full setup
function Invoke-FullSetup {
    Write-Header "Full Qdrant Setup for MonadicPipeline"
    
    Start-Qdrant
    Write-Host ""
    Set-PipelineConfiguration
    Write-Host ""
    
    Write-Success "Setup complete!"
    Write-Info ""
    Write-Info "Next steps:"
    Write-Info "  1. Run the application: dotnet run --project src\MonadicPipeline.CLI\MonadicPipeline.CLI.csproj"
    Write-Info "  2. Access Qdrant dashboard: $QdrantConnectionString/dashboard"
    Write-Info "  3. Check status: .\scripts\qdrant-setup.ps1 status"
}

# Clean all data
function Clear-QdrantData {
    Write-Header "Clean Qdrant Data"
    
    Write-Error "⚠️  WARNING: This will delete ALL Qdrant data including collections and vectors!"
    Write-Warning "Are you absolutely sure? Type 'DELETE' to confirm:"
    $confirm = Read-Host
    
    if ($confirm -ne "DELETE") {
        Write-Info "Cleanup cancelled"
        exit 0
    }
    
    Write-Info "Stopping Qdrant..."
    docker-compose stop qdrant
    
    Write-Info "Removing Qdrant volume..."
    docker-compose down -v qdrant
    
    Write-Success "All Qdrant data has been deleted"
    Write-Info "Start fresh with: .\scripts\qdrant-setup.ps1 setup"
}

# Show help
function Show-Help {
    @"
Qdrant Vector Store Setup & Management Script (Windows)

Usage: .\scripts\qdrant-setup.ps1 [command]

Commands:
  setup                    Full setup: start Qdrant and configure MonadicPipeline
  start                    Start Qdrant container
  stop                     Stop Qdrant container
  restart                  Restart Qdrant container
  status                   Check Qdrant status and show info
  logs                     View Qdrant logs (follow mode)
  configure                Configure MonadicPipeline to use Qdrant
  list                     List all collections
  delete-collection <name> Delete a specific collection
  clean                    Delete all Qdrant data (WARNING: destructive!)
  help                     Show this help message

Examples:
  .\scripts\qdrant-setup.ps1 setup          # Complete setup
  .\scripts\qdrant-setup.ps1 start          # Start Qdrant only
  .\scripts\qdrant-setup.ps1 status         # Check status
  .\scripts\qdrant-setup.ps1 logs           # View logs
  .\scripts\qdrant-setup.ps1 list           # List collections
  .\scripts\qdrant-setup.ps1 delete-collection my_vectors  # Delete collection

Qdrant URLs (when running):
  - HTTP API:  $QdrantConnectionString
  - gRPC API:  localhost:$QdrantGrpcPort
  - Dashboard: $QdrantConnectionString/dashboard

Documentation:
  - Quick Start: docs\VECTOR_STORES_QUICKSTART.md
  - Full Guide:  docs\VECTOR_STORES.md
"@
}

# Main script logic
switch ($Command.ToLower()) {
    "setup" {
        Invoke-FullSetup
    }
    "start" {
        Start-Qdrant
    }
    "stop" {
        Stop-Qdrant
    }
    "restart" {
        Restart-Qdrant
    }
    "status" {
        Get-QdrantStatus
    }
    "logs" {
        Get-QdrantLogs
    }
    "configure" {
        Set-PipelineConfiguration
    }
    "list" {
        Get-QdrantCollections
    }
    "delete-collection" {
        Remove-QdrantCollection -CollectionName $Parameter
    }
    "clean" {
        Clear-QdrantData
    }
    "help" {
        Show-Help
    }
    default {
        Write-Error "Unknown command: $Command"
        Write-Host ""
        Show-Help
        exit 1
    }
}
