#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install Docker Desktop and set up Qdrant
.DESCRIPTION
    This script installs Docker Desktop using winget and helps you start Qdrant
.EXAMPLE
    .\install-docker.ps1
#>

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

Write-Host "🐳 Docker Desktop Installation Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Check if Docker is already installed
Write-Host "`n1️⃣ Checking for existing Docker installation..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
if ($dockerCmd) {
    Write-Host "✅ Docker is already installed!" -ForegroundColor Green
    Write-Host "   Version: $(docker --version)" -ForegroundColor Gray
    
    $continue = Read-Host "`nDocker is already installed. Continue anyway? (y/N)"
    if ($continue -ne 'y' -and $continue -ne 'Y') {
        Write-Host "Exiting..." -ForegroundColor Yellow
        exit 0
    }
} else {
    Write-Host "❌ Docker is not installed" -ForegroundColor Yellow
}

# Install Docker Desktop using winget
Write-Host "`n2️⃣ Installing Docker Desktop..." -ForegroundColor Yellow
Write-Host "   This may take several minutes..." -ForegroundColor Gray

try {
    winget install Docker.DockerDesktop --accept-package-agreements --accept-source-agreements
    Write-Host "✅ Docker Desktop installed successfully!" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to install Docker Desktop: $_" -ForegroundColor Red
    Write-Host "`n💡 Manual installation:" -ForegroundColor Yellow
    Write-Host "   1. Visit: https://www.docker.com/products/docker-desktop/" -ForegroundColor Gray
    Write-Host "   2. Download and install Docker Desktop for Windows" -ForegroundColor Gray
    Write-Host "   3. Restart your computer" -ForegroundColor Gray
    exit 1
}

Write-Host "`n⚠️  IMPORTANT: You must restart your computer for Docker to work properly" -ForegroundColor Yellow
Write-Host "After restart, run: .\scripts\start-qdrant.ps1" -ForegroundColor Cyan

$restart = Read-Host "`nRestart now? (y/N)"
if ($restart -eq 'y' -or $restart -eq 'Y') {
    Write-Host "Restarting in 10 seconds... (Press Ctrl+C to cancel)" -ForegroundColor Yellow
    Start-Sleep -Seconds 10
    Restart-Computer -Force
} else {
    Write-Host "`n✋ Please restart your computer manually before using Docker" -ForegroundColor Yellow
}
