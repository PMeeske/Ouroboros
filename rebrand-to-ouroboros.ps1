# Rebrand Ouroboros to Ouroboros
# This script performs a comprehensive rebranding of the repository
# Run this from the Ouroboros root directory

param(
    [string]$RootDir = $PSScriptRoot,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$OldName = "Ouroboros"
$NewName = "Ouroboros"
$OldNameLower = "Ouroboros"
$NewNameLower = "ouroboros"

if (-not $RootDir) {
    $RootDir = Get-Location
}

Write-Host "=== Rebranding $OldName to $NewName ===" -ForegroundColor Cyan
Write-Host "Root directory: $RootDir" -ForegroundColor Gray
if ($WhatIf) {
    Write-Host "*** DRY RUN - No changes will be made ***" -ForegroundColor Yellow
}

# Step 1: Replace text content in all files
Write-Host "`n[Step 1] Replacing text content in files..." -ForegroundColor Yellow

$fileExtensions = @("*.cs", "*.csproj", "*.sln", "*.json", "*.xml", "*.xaml", "*.md", 
                    "*.yml", "*.yaml", "*.ps1", "*.sh", "*.py", "*.tf", "*.tfvars", 
                    "*.hcl", "*.config", "*.http", "*.txt", "*.manifest", "*.globalconfig",
                    "*.props", "*.targets", "*.feature")

$excludeDirs = @("bin", "obj", ".git", "node_modules", ".vs")

foreach ($ext in $fileExtensions) {
    $files = Get-ChildItem -Path $RootDir -Filter $ext -Recurse -File -ErrorAction SilentlyContinue |
             Where-Object { 
                 $path = $_.FullName
                 $excluded = $false
                 foreach ($dir in $excludeDirs) {
                     if ($path -match "\\$dir\\") { $excluded = $true; break }
                 }
                 -not $excluded
             }
    
    foreach ($file in $files) {
        try {
            $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
            if ($content -and ($content -match $OldName -or $content -match $OldNameLower)) {
                $newContent = $content -replace $OldName, $NewName
                $newContent = $newContent -replace $OldNameLower, $NewNameLower
                if ($content -ne $newContent) {
                    if (-not $WhatIf) {
                        Set-Content -Path $file.FullName -Value $newContent -NoNewline
                    }
                    Write-Host "  Updated: $($file.FullName)" -ForegroundColor Green
                }
            }
        } catch {
            Write-Host "  Error processing $($file.FullName): $_" -ForegroundColor Red
        }
    }
}

# Step 2: Rename files that contain the old name (files only, not directories yet)
Write-Host "`n[Step 2] Renaming files..." -ForegroundColor Yellow

$filesToRename = Get-ChildItem -Path $RootDir -Recurse -File -ErrorAction SilentlyContinue |
                 Where-Object { 
                     $_.Name -like "*$OldName*" -and 
                     $_.FullName -notmatch "\\bin\\" -and 
                     $_.FullName -notmatch "\\obj\\" -and 
                     $_.FullName -notmatch "\\.git\\"
                 }

foreach ($file in $filesToRename) {
    $newFileName = $file.Name -replace [regex]::Escape($OldName), $NewName
    if ($file.Name -ne $newFileName) {
        $newPath = Join-Path $file.DirectoryName $newFileName
        Write-Host "  File: $($file.Name) -> $newFileName" -ForegroundColor Green
        if (-not $WhatIf) {
            Rename-Item -Path $file.FullName -NewName $newFileName -Force
        }
    }
}

# Step 3: Rename directories from deepest to shallowest
Write-Host "`n[Step 3] Renaming directories..." -ForegroundColor Yellow

# Get all directories, sort by depth (deepest first)
$dirsToRename = Get-ChildItem -Path $RootDir -Recurse -Directory -ErrorAction SilentlyContinue |
                Where-Object { 
                    $_.Name -like "*$OldName*" -and 
                    $_.FullName -notmatch "\\bin\\" -and 
                    $_.FullName -notmatch "\\obj\\" -and 
                    $_.FullName -notmatch "\\.git\\"
                } |
                Sort-Object { $_.FullName.Split([IO.Path]::DirectorySeparatorChar).Count } -Descending

foreach ($dir in $dirsToRename) {
    if (Test-Path $dir.FullName) {
        $newDirName = $dir.Name -replace [regex]::Escape($OldName), $NewName
        if ($dir.Name -ne $newDirName) {
            Write-Host "  Dir: $($dir.FullName) -> $newDirName" -ForegroundColor Green
            if (-not $WhatIf) {
                Rename-Item -Path $dir.FullName -NewName $newDirName -Force
            }
        }
    }
}

Write-Host "`n=== Rebranding Complete! ===" -ForegroundColor Cyan
Write-Host @"

Post-rebranding steps:
1. Rename the root folder: Ouroboros -> Ouroboros
   Command: cd d:\project; Rename-Item -Path "Ouroboros" -NewName "Ouroboros"
   
2. Run 'dotnet build Ouroboros.sln' to verify the solution compiles

3. Update your git remote if the repository name changes:
   git remote set-url origin https://github.com/PMeeske/Ouroboros.git

4. Commit the changes:
   git add -A
   git commit -m "Rebrand Ouroboros to Ouroboros"

"@ -ForegroundColor Gray
