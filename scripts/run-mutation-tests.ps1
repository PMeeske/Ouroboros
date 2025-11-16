[CmdletBinding()]
param(
    [string]$ConfigurationPath = "stryker-config.json",
    [switch]$OpenReport,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$StrykerArgs
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repositoryRoot

try {
    Write-Host "Restoring local dotnet tools..." -ForegroundColor Cyan
    dotnet tool restore | Out-Default

    $arguments = @("stryker", "--config-file", $ConfigurationPath)
    if ($StrykerArgs) {
        $arguments += $StrykerArgs
    }

    Write-Host "Running mutation tests: dotnet $($arguments -join ' ')" -ForegroundColor Cyan
    dotnet @arguments
}
finally {
    Pop-Location
}

if ($OpenReport) {
    $outputRoot = Join-Path $repositoryRoot "StrykerOutput"
    if (Test-Path $outputRoot) {
        $latestRun = Get-ChildItem -Path $outputRoot -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latestRun) {
            $reportPath = Join-Path $latestRun.FullName "reports/mutation-report.html"
            if (Test-Path $reportPath) {
                Write-Host "Opening mutation report: $reportPath" -ForegroundColor Cyan
                Start-Process $reportPath
            }
            else {
                Write-Warning "No HTML report found for latest Stryker run."
            }
        }
        else {
            Write-Warning "No StrykerOutput directories were found."
        }
    }
    else {
        Write-Warning "StrykerOutput directory not found. Run mutation tests first."
    }
}
