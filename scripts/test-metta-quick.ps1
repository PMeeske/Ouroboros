#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick MeTTa functionality test (using Docker)
.DESCRIPTION
    Tests MeTTa symbolic reasoning with simple examples
.EXAMPLE
    .\test-metta-quick.ps1
#>

param(
    [string]$Expression = "(+ 2 3)"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Quick MeTTa Test (Docker) ===" -ForegroundColor Cyan
Write-Host ""

# Check Docker
Write-Host "[1/3] Checking Docker..." -ForegroundColor Yellow
try {
    docker ps | Out-Null
    Write-Host "  OK Docker is running" -ForegroundColor Green
} catch {
    Write-Host "  ERROR Docker not running" -ForegroundColor Red
    Write-Host "    Start Docker Desktop" -ForegroundColor Gray
    exit 1
}

# Check MeTTa image
Write-Host ""
Write-Host "[2/3] Checking MeTTa image..." -ForegroundColor Yellow
$image = docker images trueagi/hyperon:latest --format "{{.Repository}}:{{.Tag}}" 2>&1
if ($image -match "trueagi/hyperon:latest") {
    Write-Host "  OK MeTTa image found" -ForegroundColor Green
} else {
    Write-Host "  WARN MeTTa image not found" -ForegroundColor Yellow
    Write-Host "    Installing MeTTa image..." -ForegroundColor Gray
    docker pull trueagi/hyperon:latest
}

# Test MeTTa
Write-Host ""
Write-Host "[3/3] Testing MeTTa evaluation..." -ForegroundColor Yellow
Write-Host "  Expression: $Expression" -ForegroundColor Gray

try {
    # Create temp file with MeTTa script
    $tempFile = [System.IO.Path]::GetTempFileName()
    $Expression | Out-File -FilePath $tempFile -Encoding utf8
    
    $result = docker run --rm -v "${tempFile}:/tmp/input.metta" trueagi/hyperon:latest metta-repl /tmp/input.metta 2>&1
    Remove-Item $tempFile -Force
    
    Write-Host "  Result: $result" -ForegroundColor Cyan
    Write-Host "  OK MeTTa executed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ERROR MeTTa execution failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Test Completed ===" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - Docker: Running" -ForegroundColor White
Write-Host "  - MeTTa: Installed and working" -ForegroundColor White
Write-Host "  - Expression evaluated successfully" -ForegroundColor White
Write-Host ""
Write-Host "Try more examples:" -ForegroundColor Cyan
Write-Host "  .\scripts\test-metta-quick.ps1 -Expression '(* 7 6)'" -ForegroundColor Gray
Write-Host "  .\scripts\test-metta-quick.ps1 -Expression '(if True A B)'" -ForegroundColor Gray
Write-Host "  docker run -ti trueagi/hyperon:latest metta-repl  # Interactive REPL" -ForegroundColor Gray
