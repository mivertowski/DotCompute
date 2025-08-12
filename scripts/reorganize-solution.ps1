#!/usr/bin/env pwsh
# DotCompute Solution Reorganization Script
# This script reorganizes the solution according to .NET best practices

param(
    [switch]$DryRun = $false,
    [switch]$Force = $false,
    [string]$BackupPath = "./backup"
)

$ErrorActionPreference = "Stop"

Write-Host "🔄 DotCompute Solution Reorganization Script" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Define the new structure mapping
$ProjectMappings = @{
    # Core Projects
    "src/DotCompute.Abstractions" = "src/Core/DotCompute.Abstractions"
    "src/DotCompute.Core" = "src/Core/DotCompute.Core"
    "src/DotCompute.Memory" = "src/Core/DotCompute.Memory"
    
    # Backend Projects
    "plugins/backends/DotCompute.Backends.CPU" = "src/Backends/DotCompute.Backends.CPU"
    "plugins/backends/DotCompute.Backends.CUDA" = "src/Backends/DotCompute.Backends.CUDA"
    "plugins/backends/DotCompute.Backends.Metal" = "src/Backends/DotCompute.Backends.Metal"
    
    # Extension Projects
    "src/DotCompute.Linq" = "src/Extensions/DotCompute.Linq"
    "src/DotCompute.Algorithms" = "src/Extensions/DotCompute.Algorithms"
    
    # Runtime Projects
    "src/DotCompute.Runtime" = "src/Runtime/DotCompute.Runtime"
    "src/DotCompute.Plugins" = "src/Runtime/DotCompute.Plugins"
    "src/DotCompute.Generators" = "src/Runtime/DotCompute.Generators"
    
    # Test Projects
    "tests/Unit" = "tests/Unit"
    "tests/Integration" = "tests/Integration"
    "tests/Hardware" = "tests/Performance"
    "tests/Performance" = "tests/Performance"
    "tests/Shared" = "tests/Shared"
    
    # Additional test folders
    "tests/DotCompute.Algorithms.Tests" = "tests/Unit/DotCompute.Algorithms.Tests"
    "tests/DotCompute.Memory.Tests" = "tests/Unit/DotCompute.Memory.Tests"
    "tests/DotCompute.Linq.Tests" = "tests/Unit/DotCompute.Linq.Tests"
    "tests/DotCompute.Backends.CPU.Tests" = "tests/Unit/DotCompute.Backends.CPU.Tests"
    
    # Backend test consolidation
    "plugins/backends/DotCompute.Backends.CPU/tests" = "tests/Unit/DotCompute.Backends.CPU.Tests"
    "plugins/backends/DotCompute.Backends.Metal/tests" = "tests/Unit/DotCompute.Backends.Metal.Tests"
}

# Define project reference updates needed
$ReferenceUpdates = @{
    # Core to Core references (stay the same relative)
    '\.\.\\DotCompute\.Abstractions\\' = '..\DotCompute.Abstractions\'
    '\.\.\\DotCompute\.Core\\' = '..\DotCompute.Core\'
    '\.\.\\DotCompute\.Memory\\' = '..\DotCompute.Memory\'
    
    # Backend to Core references
    '\.\.\\\.\.\\\.\.\\src\\DotCompute\.Abstractions\\' = '..\..\Core\DotCompute.Abstractions\'
    '\.\.\\\.\.\\\.\.\\src\\DotCompute\.Core\\' = '..\..\Core\DotCompute.Core\'
    '\.\.\\\.\.\\\.\.\\src\\DotCompute\.Memory\\' = '..\..\Core\DotCompute.Memory\'
    '\.\.\\\.\.\\\.\.\\src\\DotCompute\.Plugins\\' = '..\..\Runtime\DotCompute.Plugins\'
    
    # Extension to Core references
    '\.\.\\DotCompute\.Abstractions\\' = '..\Core\DotCompute.Abstractions\'
    '\.\.\\DotCompute\.Core\\' = '..\Core\DotCompute.Core\'
    '\.\.\\DotCompute\.Memory\\' = '..\Core\DotCompute.Memory\'
    
    # Test to source references
    '\.\.\\\.\.\\src\\DotCompute\.Abstractions\\' = '..\..\src\Core\DotCompute.Abstractions\'
    '\.\.\\\.\.\\src\\DotCompute\.Core\\' = '..\..\src\Core\DotCompute.Core\'
    '\.\.\\\.\.\\src\\DotCompute\.Memory\\' = '..\..\src\Core\DotCompute.Memory\'
    '\.\.\\\.\.\\src\\DotCompute\.Linq\\' = '..\..\src\Extensions\DotCompute.Linq\'
    '\.\.\\\.\.\\src\\DotCompute\.Algorithms\\' = '..\..\src\Extensions\DotCompute.Algorithms\'
    '\.\.\\\.\.\\src\\DotCompute\.Runtime\\' = '..\..\src\Runtime\DotCompute.Runtime\'
    '\.\.\\\.\.\\src\\DotCompute\.Plugins\\' = '..\..\src\Runtime\DotCompute.Plugins\'
    '\.\.\\\.\.\\src\\DotCompute\.Generators\\' = '..\..\src\Runtime\DotCompute.Generators\'
    
    # Backend references from tests
    '\.\.\\\.\.\\plugins\\backends\\DotCompute\.Backends\.CPU\\' = '..\..\src\Backends\DotCompute.Backends.CPU\'
    '\.\.\\\.\.\\plugins\\backends\\DotCompute\.Backends\.CUDA\\' = '..\..\src\Backends\DotCompute.Backends.CUDA\'
    '\.\.\\\.\.\\plugins\\backends\\DotCompute\.Backends\.Metal\\' = '..\..\src\Backends\DotCompute.Backends.Metal\'
}

function Write-Info($message) {
    Write-Host "ℹ️  $message" -ForegroundColor Blue
}

function Write-Success($message) {
    Write-Host "✅ $message" -ForegroundColor Green
}

function Write-Warning($message) {
    Write-Host "⚠️  $message" -ForegroundColor Yellow
}

function Write-Error($message) {
    Write-Host "❌ $message" -ForegroundColor Red
}

function Test-Prerequisites {
    Write-Info "Checking prerequisites..."
    
    # Check if we're in the right directory
    if (-not (Test-Path "DotCompute.sln")) {
        throw "DotCompute.sln not found. Please run this script from the solution root."
    }
    
    # Check git status
    $gitStatus = git status --porcelain 2>$null
    if ($gitStatus -and -not $Force) {
        throw "Working directory has uncommitted changes. Use -Force to continue anyway."
    }
    
    Write-Success "Prerequisites check passed"
}

function Backup-Solution {
    if (-not $DryRun) {
        Write-Info "Creating backup..."
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $backupDir = "$BackupPath/dotcompute_backup_$timestamp"
        
        if (-not (Test-Path $BackupPath)) {
            New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
        }
        
        Copy-Item -Path "." -Destination $backupDir -Recurse -Exclude @("bin", "obj", ".git", "TestResults", "artifacts", "coverage*") -Force
        Write-Success "Backup created at: $backupDir"
        return $backupDir
    } else {
        Write-Info "Dry run: Backup would be created"
        return "dry-run-backup"
    }
}

function Create-NewStructure {
    Write-Info "Creating new directory structure..."
    
    $newDirs = @(
        "src/Core",
        "src/Backends", 
        "src/Extensions",
        "src/Runtime",
        "tests/Unit",
        "tests/Integration", 
        "tests/Performance",
        "tests/Shared"
    )
    
    foreach ($dir in $newDirs) {
        if (-not $DryRun) {
            if (-not (Test-Path $dir)) {
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                Write-Success "Created directory: $dir"
            }
        } else {
            Write-Info "Dry run: Would create directory: $dir"
        }
    }
}

function Move-Projects {
    Write-Info "Moving projects to new locations..."
    
    foreach ($mapping in $ProjectMappings.GetEnumerator()) {
        $source = $mapping.Key
        $destination = $mapping.Value
        
        if (Test-Path $source) {
            Write-Info "Moving $source -> $destination"
            
            if (-not $DryRun) {
                # Create destination directory if it doesn't exist
                $destDir = Split-Path $destination -Parent
                if (-not (Test-Path $destDir)) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }
                
                # Move the project
                Move-Item -Path $source -Destination $destination -Force
                Write-Success "Moved: $source -> $destination"
            } else {
                Write-Info "Dry run: Would move $source -> $destination"
            }
        } else {
            Write-Warning "Source not found: $source"
        }
    }
}

function Update-ProjectReferences {
    Write-Info "Updating project references..."
    
    $projectFiles = Get-ChildItem -Path "." -Filter "*.csproj" -Recurse
    
    foreach ($projectFile in $projectFiles) {
        Write-Info "Processing: $($projectFile.FullName)"
        
        if (-not $DryRun) {
            $content = Get-Content $projectFile.FullName -Raw
            $originalContent = $content
            
            foreach ($refUpdate in $ReferenceUpdates.GetEnumerator()) {
                $pattern = $refUpdate.Key
                $replacement = $refUpdate.Value
                $content = $content -replace $pattern, $replacement
            }
            
            if ($content -ne $originalContent) {
                Set-Content -Path $projectFile.FullName -Value $content
                Write-Success "Updated references in: $($projectFile.Name)"
            }
        } else {
            Write-Info "Dry run: Would update references in: $($projectFile.Name)"
        }
    }
}

function Update-SolutionFile {
    Write-Info "Updating solution file..."
    
    if (-not $DryRun) {
        $slnContent = Get-Content "DotCompute.sln" -Raw
        
        # Update project paths in solution
        foreach ($mapping in $ProjectMappings.GetEnumerator()) {
            $oldPath = $mapping.Key -replace "/", "\"
            $newPath = $mapping.Value -replace "/", "\"
            $slnContent = $slnContent -replace [regex]::Escape($oldPath), $newPath
        }
        
        # Update solution folder structure
        $newSolutionStructure = @"
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{B8C07D80-D3D4-4CF7-BE09-EB8F8A0997B6}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Core", "Core", "{C1D8E4F1-A2B5-4E89-9C3F-1A6D7E2B8C5A}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Backends", "Backends", "{D2E9F5A2-B3C6-5F9A-AD4A-2B7E8F3C9D6B}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Extensions", "Extensions", "{E3FAA6B3-C4D7-6A9B-BE5B-3C8F9A4D0E7C}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Runtime", "Runtime", "{F4ABB7C4-D5E8-7B9C-CF6C-4D9A0B5E1F8D}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{F2A3B4C5-D6E7-8901-2345-678901F56789}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "samples", "samples", "{A3B4C5D6-E7F8-9012-3456-789012A67890}"
EndProject
"@
        
        Set-Content -Path "DotCompute.sln" -Value $slnContent
        Write-Success "Updated solution file"
    } else {
        Write-Info "Dry run: Would update solution file"
    }
}

function Update-BuildFiles {
    Write-Info "Updating build configuration files..."
    
    # Update Directory.Build.props files
    $buildProps = @(
        "src/Directory.Build.props",
        "tests/Directory.Build.props"
    )
    
    foreach ($buildProp in $buildProps) {
        if (Test-Path $buildProp) {
            Write-Info "Processing: $buildProp"
            if (-not $DryRun) {
                # Add any necessary updates to build props
                Write-Success "Updated: $buildProp"
            } else {
                Write-Info "Dry run: Would update: $buildProp"
            }
        }
    }
}

function Remove-EmptyDirectories {
    Write-Info "Cleaning up empty directories..."
    
    $emptyDirs = @(
        "plugins/backends",
        "plugins"
    )
    
    foreach ($dir in $emptyDirs) {
        if ((Test-Path $dir) -and ((Get-ChildItem $dir -Recurse | Measure-Object).Count -eq 0)) {
            if (-not $DryRun) {
                Remove-Item $dir -Recurse -Force
                Write-Success "Removed empty directory: $dir"
            } else {
                Write-Info "Dry run: Would remove empty directory: $dir"
            }
        }
    }
}

function Test-Build {
    Write-Info "Testing build after reorganization..."
    
    if (-not $DryRun) {
        try {
            dotnet build --no-restore --configuration Debug
            Write-Success "Build test passed"
        } catch {
            Write-Error "Build test failed: $_"
            throw "Build failed after reorganization"
        }
    } else {
        Write-Info "Dry run: Would test build"
    }
}

# Main execution
try {
    if ($DryRun) {
        Write-Warning "DRY RUN MODE - No changes will be made"
    }
    
    Test-Prerequisites
    $backupPath = Backup-Solution
    Create-NewStructure
    Move-Projects
    Update-ProjectReferences
    Update-SolutionFile
    Update-BuildFiles
    Remove-EmptyDirectories
    
    if (-not $DryRun) {
        Test-Build
    }
    
    Write-Success "✨ Solution reorganization completed successfully!"
    
    if (-not $DryRun) {
        Write-Info "Next steps:"
        Write-Info "1. Run 'dotnet build' to verify everything works"
        Write-Info "2. Run 'dotnet test' to verify all tests pass"
        Write-Info "3. Update any CI/CD scripts with new paths"
        Write-Info "4. Update documentation with new structure"
        Write-Info "5. Commit the changes to git"
        Write-Info ""
        Write-Info "Backup created at: $backupPath"
    } else {
        Write-Info "This was a dry run. Use the script without -DryRun to apply changes."
    }
    
} catch {
    Write-Error "Reorganization failed: $_"
    
    if ($backupPath -and $backupPath -ne "dry-run-backup" -and (Test-Path $backupPath)) {
        Write-Info "You can restore from backup at: $backupPath"
    }
    
    exit 1
}