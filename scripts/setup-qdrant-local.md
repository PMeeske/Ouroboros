# Local Qdrant Testing Setup Guide

## Prerequisites
Since Docker is not available on this system, you have several options:

## Option 1: Install Docker Desktop (Recommended)
1. Download Docker Desktop from: https://www.docker.com/products/docker-desktop/
2. Install and restart your computer
3. Run: `docker compose up -d qdrant` from the Ouroboros directory
4. Access Qdrant at: http://localhost:6333

## Option 2: Run Qdrant Binary (No Docker)
1. Download Qdrant binary from: https://github.com/qdrant/qdrant/releases
2. Extract and run: `qdrant.exe`
3. Qdrant will start on http://localhost:6333

## Option 3: Use WSL2 with Docker
1. Install WSL2: `wsl --install`
2. Install Docker in WSL2
3. Run Qdrant from WSL2

## Option 4: Use InMemory Vector Store (Already Configured)
Your `appsettings.Development.json` already uses InMemory vector store, which works for testing without external dependencies.

## Option 5: Use Qdrant Cloud (Free Tier)
1. Sign up at: https://cloud.qdrant.io/
2. Create a free cluster
3. Update `appsettings.Development.json` with the connection string

## Verify Qdrant Installation

Once Qdrant is running, test connectivity:

```powershell
# Test health endpoint
Invoke-WebRequest -Uri "http://localhost:6333/health" -UseBasicParsing

# View collections
Invoke-WebRequest -Uri "http://localhost:6333/collections" -UseBasicParsing
```

## Next Steps
After Qdrant is running, use the test script: `.\scripts\test-qdrant.ps1`
