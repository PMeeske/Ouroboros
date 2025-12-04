#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Launch Docker installation as Administrator
.DESCRIPTION
    This script opens a new PowerShell window as Administrator and runs the Docker installation
.EXAMPLE
    .\launch-docker-install.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "🐳 Docker Desktop Installation Launcher" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

$scriptPath = Join-Path $PSScriptRoot "install-docker.ps1"

if (-not (Test-Path $scriptPath)) {
    Write-Host "❌ install-docker.ps1 not found at: $scriptPath" -ForegroundColor Red
    exit 1
}

Write-Host "`n📋 This will:" -ForegroundColor Yellow
Write-Host "   1. Open PowerShell as Administrator" -ForegroundColor Gray
Write-Host "   2. Install Docker Desktop (v4.53.0)" -ForegroundColor Gray
Write-Host "   3. Prompt you to restart your computer" -ForegroundColor Gray
Write-Host ""

$confirm = Read-Host "Continue? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host "`n🚀 Launching installer as Administrator..." -ForegroundColor Green
Write-Host "   (You may see a UAC prompt - click Yes)" -ForegroundColor Gray

try {
    Start-Process powershell -Verb RunAs -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`""
    Write-Host "`n✅ Installation launched in administrator window" -ForegroundColor Green
    Write-Host "   Follow the prompts in the new window" -ForegroundColor Gray
} catch {
    Write-Host "`n❌ Failed to launch as administrator: $_" -ForegroundColor Red
    Write-Host "`n💡 Manual steps:" -ForegroundColor Yellow
    Write-Host "   1. Right-click PowerShell in Start Menu" -ForegroundColor Gray
    Write-Host "   2. Select 'Run as Administrator'" -ForegroundColor Gray
    Write-Host "   3. Run: cd '$PSScriptRoot'" -ForegroundColor Gray
    Write-Host "   4. Run: .\install-docker.ps1" -ForegroundColor Gray
}
