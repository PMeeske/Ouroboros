#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install MeTTa (Hyperon) symbolic reasoning engine
.DESCRIPTION
    Installs MeTTa via Python pip package or Docker
.PARAMETER Method
    Installation method: pip (default), docker, or rust
.EXAMPLE
    .\install-metta.ps1
.EXAMPLE
    .\install-metta.ps1 -Method docker
#>

param(
    [ValidateSet("pip", "docker", "rust")]
    [string]$Method = "pip"
)

$ErrorActionPreference = "Stop"

Write-Host "=== MeTTa (Hyperon) Installation ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "About MeTTa:" -ForegroundColor Yellow
Write-Host "  MeTTa (Meta Type Talk) is a symbolic AI language" -ForegroundColor Gray
Write-Host "  Part of OpenCog Hyperon project" -ForegroundColor Gray
Write-Host "  GitHub: https://github.com/trueagi-io/hyperon-experimental" -ForegroundColor Gray
Write-Host ""

switch ($Method) {
    "pip" {
        Write-Host "[Method: Python Pip]" -ForegroundColor Cyan
        Write-Host ""
        
        # Check Python
        Write-Host "[1/3] Checking Python..." -ForegroundColor Yellow
        try {
            $pythonCmd = "python3"
            $pythonTest = Get-Command python3 -ErrorAction SilentlyContinue
            if (-not $pythonTest) {
                $pythonCmd = "python"
                $pythonTest = Get-Command python -ErrorAction SilentlyContinue
            }
            if (-not $pythonTest) {
                throw "Python not found"
            }
            
            $pythonVersion = & $pythonCmd --version 2>&1
            Write-Host "  OK $pythonVersion (using $pythonCmd)" -ForegroundColor Green
            
            # Check version (need 3.8+)
            if ($pythonVersion -match "Python (\d+)\.(\d+)") {
                $major = [int]$matches[1]
                $minor = [int]$matches[2]
                if ($major -lt 3 -or ($major -eq 3 -and $minor -lt 8)) {
                    Write-Host "  ERROR Python 3.8+ required" -ForegroundColor Red
                    exit 1
                }
            }
        } catch {
            Write-Host "  ERROR Python not found" -ForegroundColor Red
            Write-Host "    Install Python 3.8+ from: https://www.python.org/downloads/" -ForegroundColor Gray
            exit 1
        }
        
        # Check pip
        Write-Host ""
        Write-Host "[2/3] Checking pip..." -ForegroundColor Yellow
        try {
            $pipVersion = & $pythonCmd -m pip --version 2>&1
            Write-Host "  OK $pipVersion" -ForegroundColor Green
        } catch {
            Write-Host "  ERROR pip not found" -ForegroundColor Red
            Write-Host "    Install pip: python -m ensurepip --upgrade" -ForegroundColor Gray
            exit 1
        }
        
        # Install hyperon
        Write-Host ""
        Write-Host "[3/3] Installing hyperon package..." -ForegroundColor Yellow
        Write-Host "  This may take several minutes..." -ForegroundColor Gray
        
        try {
            $output = & $pythonCmd -m pip install hyperon 2>&1
            
            if ($LASTEXITCODE -ne 0) {
                if ($output -match "externally-managed-environment") {
                    Write-Host "  WARN Python environment is externally managed" -ForegroundColor Yellow
                    Write-Host ""
                    Write-Host "  Options:" -ForegroundColor Cyan
                    Write-Host "    1. Create virtual environment (recommended):" -ForegroundColor White
                    Write-Host "       python -m venv metta-env" -ForegroundColor Gray
                    Write-Host "       .\metta-env\Scripts\Activate.ps1" -ForegroundColor Gray
                    Write-Host "       python -m pip install hyperon" -ForegroundColor Gray
                    Write-Host ""
                    Write-Host "    2. Install with --break-system-packages:" -ForegroundColor White
                    Write-Host "       python -m pip install hyperon --break-system-packages" -ForegroundColor Gray
                    exit 1
                }
                throw "Installation failed"
            }
            
            Write-Host "  OK hyperon installed successfully" -ForegroundColor Green
        } catch {
            Write-Host "  ERROR Installation failed: $_" -ForegroundColor Red
            Write-Host $output -ForegroundColor Gray
            exit 1
        }
        
        # Verify installation
        Write-Host ""
        Write-Host "[Verify] Testing metta-py command..." -ForegroundColor Yellow
        $mettaPy = Get-Command metta-py -ErrorAction SilentlyContinue
        if ($mettaPy) {
            Write-Host "  OK metta-py found at: $($mettaPy.Source)" -ForegroundColor Green
        } else {
            Write-Host "  WARN metta-py not found in PATH" -ForegroundColor Yellow
            Write-Host "    You may need to restart your terminal or add Python Scripts to PATH" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "Installation Complete!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Usage:" -ForegroundColor Cyan
        Write-Host "  Run REPL: metta-py" -ForegroundColor White
        Write-Host "  Run script: metta-py script.metta" -ForegroundColor White
        Write-Host "  Python import: python -c 'import hyperon'" -ForegroundColor White
    }
    
    "docker" {
        Write-Host "[Method: Docker]" -ForegroundColor Cyan
        Write-Host ""
        
        # Check Docker
        Write-Host "[1/2] Checking Docker..." -ForegroundColor Yellow
        try {
            $dockerVersion = docker --version
            Write-Host "  OK $dockerVersion" -ForegroundColor Green
        } catch {
            Write-Host "  ERROR Docker not found" -ForegroundColor Red
            Write-Host "    Run: .\scripts\install-docker.ps1" -ForegroundColor Gray
            exit 1
        }
        
        # Pull image
        Write-Host ""
        Write-Host "[2/2] Pulling MeTTa Docker image..." -ForegroundColor Yellow
        Write-Host "  This may take several minutes..." -ForegroundColor Gray
        
        try {
            docker pull trueagi/hyperon:latest
            Write-Host "  OK Image pulled successfully" -ForegroundColor Green
        } catch {
            Write-Host "  ERROR Failed to pull image: $_" -ForegroundColor Red
            exit 1
        }
        
        Write-Host ""
        Write-Host "Installation Complete!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Usage:" -ForegroundColor Cyan
        Write-Host "  Run container: docker run -ti trueagi/hyperon:latest" -ForegroundColor White
        Write-Host "  Run REPL: docker run -ti trueagi/hyperon:latest metta-repl" -ForegroundColor White
        Write-Host "  Run Python REPL: docker run -ti trueagi/hyperon:latest metta-py" -ForegroundColor White
    }
    
    "rust" {
        Write-Host "[Method: Rust (Manual Build)]" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "This method requires:" -ForegroundColor Yellow
        Write-Host "  - Rust (latest stable)" -ForegroundColor Gray
        Write-Host "  - Python 3.8+" -ForegroundColor Gray
        Write-Host "  - CMake 3.24+" -ForegroundColor Gray
        Write-Host "  - GCC/MSVC compiler" -ForegroundColor Gray
        Write-Host "  - Git" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Installation steps:" -ForegroundColor Cyan
        Write-Host "  1. Install Rust: https://www.rust-lang.org/tools/install" -ForegroundColor White
        Write-Host "  2. Clone repo:" -ForegroundColor White
        Write-Host "     git clone https://github.com/trueagi-io/hyperon-experimental.git" -ForegroundColor Gray
        Write-Host "  3. Build:" -ForegroundColor White
        Write-Host "     cd hyperon-experimental" -ForegroundColor Gray
        Write-Host "     cargo build --release" -ForegroundColor Gray
        Write-Host "  4. Run:" -ForegroundColor White
        Write-Host "     cargo run --bin metta-repl" -ForegroundColor Gray
        Write-Host ""
        Write-Host "For detailed instructions, see:" -ForegroundColor Yellow
        Write-Host "  https://github.com/trueagi-io/hyperon-experimental#manual-installation" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  - Run test: .\scripts\test-metta.ps1" -ForegroundColor White
Write-Host "  - Learn MeTTa: https://metta-lang.dev/" -ForegroundColor White
Write-Host "  - Examples: https://github.com/trueagi-io/metta-examples" -ForegroundColor White
