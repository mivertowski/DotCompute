#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Reorganizes the DotCompute solution structure following .NET best practices.

.DESCRIPTION
    This script performs a complete reorganization of the DotCompute solution from 
    its current scattered structure to a clean, maintainable structure with proper 
    separation of concerns.

.PARAMETER WhatIf
    Shows what would be done without actually performing the operations.

.PARAMETER Force
    Forces the operation even if backup already exists.

.PARAMETER BackupPath
    Path where to create backup of current structure. Defaults to ../DotCompute.backup

.EXAMPLE
    .\Reorganize-Solution.ps1 -WhatIf
    Shows what changes would be made without executing them.

.EXAMPLE
    .\Reorganize-Solution.ps1 -Force
    Performs the reorganization, overwriting any existing backup.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$WhatIf,
    [switch]$Force,
    [string]$BackupPath = "../DotCompute.backup"
)

# Set strict mode and error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Get script directory and solution root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Resolve-Path "$ScriptDir/../.."
$BackupPath = Resolve-Path $BackupPath -ErrorAction SilentlyContinue

Write-Host "DotCompute Solution Reorganization Script" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Solution Root: $SolutionRoot" -ForegroundColor Cyan
Write-Host "Backup Path: $BackupPath" -ForegroundColor Cyan

# Define project mappings
$ProjectMappings = @{
    # Core Projects
    "src/DotCompute.Abstractions" = "src/Core/DotCompute.Abstractions"
    "src/DotCompute.Core" = "src/Core/DotCompute.Core"
    "src/DotCompute.Memory" = "src/Core/DotCompute.Memory"
    
    # Extension Projects
    "src/DotCompute.Linq" = "src/Extensions/DotCompute.Linq"
    "src/DotCompute.Algorithms" = "src/Extensions/DotCompute.Algorithms"
    
    # Tool Projects
    "src/DotCompute.Generators" = "src/Tools/DotCompute.Generators"
    "src/DotCompute.Plugins" = "src/Tools/DotCompute.Plugins"
    
    # Runtime Projects
    "src/DotCompute.Runtime" = "src/Runtime/DotCompute.Runtime"
    
    # Backend Projects
    "plugins/backends/DotCompute.Backends.CPU" = "src/Backends/DotCompute.Backends.CPU"
    "plugins/backends/DotCompute.Backends.CUDA" = "src/Backends/DotCompute.Backends.CUDA"
    "plugins/backends/DotCompute.Backends.Metal" = "src/Backends/DotCompute.Backends.Metal"
    
    # Backend Test Projects
    "plugins/backends/DotCompute.Backends.CPU/tests" = "tests/Unit/DotCompute.Backends.CPU.Tests"
    "plugins/backends/DotCompute.Backends.Metal/tests" = "tests/Unit/DotCompute.Backends.Metal.Tests"
    
    # Sample Projects (examples to samples)
    "examples/ExpressionToDynamicKernel" = "samples/ExpressionToDynamicKernel"
    
    # Performance Tests
    "benchmarks/DotCompute.Benchmarks" = "tests/Performance/DotCompute.Benchmarks"
    
    # Hardware Tests (rename for consistency)
    "tests/Hardware/DotCompute.Hardware.Cuda.Tests" = "tests/Hardware/DotCompute.Hardware.CUDA.Tests"
}

# Define projects to merge (target = source1 + source2)
$ProjectMerges = @{
    "tests/Unit/DotCompute.Algorithms.Tests" = @("tests/Unit/DotCompute.Algorithms.Tests", "tests/DotCompute.Algorithms.Tests")
    "tests/Unit/DotCompute.Memory.Tests" = @("tests/Unit/DotCompute.Memory.Tests", "tests/DotCompute.Memory.Tests")
}

# Define directories to create
$DirectoriesToCreate = @(
    "src/Core",
    "src/Extensions", 
    "src/Backends",
    "src/Tools",
    "src/Runtime",
    "tests/Unit",
    "tests/Integration",
    "tests/Performance", 
    "tests/Hardware",
    "tests/Shared",
    "build/props",
    "build/targets"
)

# Define files/folders to delete
$ItemsToDelete = @(
    "CudaTest",  # Temporary test project
    "plugins"    # Will be empty after reorganization
)

function Write-Phase {
    param([string]$Message)
    Write-Host ""
    Write-Host "=== $Message ===" -ForegroundColor Yellow
}

function Test-PathExists {
    param([string]$Path)
    return Test-Path (Join-Path $SolutionRoot $Path)
}

function Move-ProjectSafely {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Description = ""
    )
    
    $SourcePath = Join-Path $SolutionRoot $Source
    $DestinationPath = Join-Path $SolutionRoot $Destination
    $DestinationDir = Split-Path $DestinationPath -Parent
    
    if (-not (Test-Path $SourcePath)) {
        Write-Warning "Source path does not exist: $Source"
        return $false
    }
    
    if ($Description) {
        Write-Host "  Moving $Description..." -ForegroundColor Gray
    }
    Write-Host "    $Source -> $Destination" -ForegroundColor DarkGray
    
    if ($PSCmdlet.ShouldProcess("$Source to $Destination", "Move Project")) {
        # Create destination directory if it doesn't exist
        if (-not (Test-Path $DestinationDir)) {
            New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
        }
        
        # Move the project
        Move-Item -Path $SourcePath -Destination $DestinationPath -Force
        return $true
    }
    
    return $false
}

function Merge-Projects {
    param(
        [string]$Target,
        [string[]]$Sources,
        [string]$Description = ""
    )
    
    $TargetPath = Join-Path $SolutionRoot $Target
    $TargetDir = Split-Path $TargetPath -Parent
    
    if ($Description) {
        Write-Host "  Merging $Description..." -ForegroundColor Gray
    }
    
    # Create target directory if needed
    if (-not (Test-Path $TargetDir)) {
        if ($PSCmdlet.ShouldProcess($TargetDir, "Create Directory")) {
            New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        }
    }
    
    $PrimarySource = $Sources[0]
    $PrimarySourcePath = Join-Path $SolutionRoot $PrimarySource
    
    # Move primary source to target if not already there
    if ($PrimarySource -ne $Target -and (Test-Path $PrimarySourcePath)) {
        Write-Host "    $PrimarySource -> $Target (primary)" -ForegroundColor DarkGray
        if ($PSCmdlet.ShouldProcess("$PrimarySource to $Target", "Move Primary Project")) {
            Move-Item -Path $PrimarySourcePath -Destination $TargetPath -Force
        }
    }
    
    # Merge additional sources
    for ($i = 1; $i -lt $Sources.Length; $i++) {
        $SourcePath = Join-Path $SolutionRoot $Sources[$i]
        if (Test-Path $SourcePath) {
            Write-Host "    Merging $($Sources[$i]) into $Target" -ForegroundColor DarkGray
            
            if ($PSCmdlet.ShouldProcess($Sources[$i], "Merge into $Target")) {
                # Copy unique files from source to target
                Get-ChildItem -Path $SourcePath -Recurse -File | ForEach-Object {
                    $RelativePath = $_.FullName.Substring($SourcePath.Length + 1)
                    $TargetFilePath = Join-Path $TargetPath $RelativePath
                    $TargetFileDir = Split-Path $TargetFilePath -Parent
                    
                    if (-not (Test-Path $TargetFileDir)) {
                        New-Item -ItemType Directory -Path $TargetFileDir -Force | Out-Null
                    }
                    
                    if (-not (Test-Path $TargetFilePath)) {
                        Copy-Item -Path $_.FullName -Destination $TargetFilePath -Force
                    } else {
                        Write-Warning "Skipping duplicate file: $RelativePath"
                    }
                }
                
                # Remove the source directory
                Remove-Item -Path $SourcePath -Recurse -Force
            }
        }
    }
}

function Remove-EmptyDirectories {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        return
    }
    
    Get-ChildItem -Path $Path -Directory -Recurse | 
        Sort-Object FullName -Descending |
        ForEach-Object {
            if (@(Get-ChildItem -Path $_.FullName -Force).Count -eq 0) {
                Write-Host "  Removing empty directory: $($_.Name)" -ForegroundColor DarkGray
                if ($PSCmdlet.ShouldProcess($_.FullName, "Remove Empty Directory")) {
                    Remove-Item -Path $_.FullName -Force
                }
            }
        }
}

# Main execution
try {
    Push-Location $SolutionRoot
    
    # Phase 1: Create Backup
    Write-Phase "Phase 1: Creating Backup"
    
    if ($BackupPath -and (Test-Path $BackupPath) -and -not $Force) {
        throw "Backup already exists at '$BackupPath'. Use -Force to overwrite."
    }
    
    if ($PSCmdlet.ShouldProcess($SolutionRoot, "Create Backup")) {
        if ($BackupPath -and (Test-Path $BackupPath)) {
            Remove-Item -Path $BackupPath -Recurse -Force
        }
        Write-Host "Creating backup at: $BackupPath"
        Copy-Item -Path $SolutionRoot -Destination $BackupPath -Recurse -Force
        Write-Host "Backup created successfully" -ForegroundColor Green
    }
    
    # Phase 2: Create New Directory Structure
    Write-Phase "Phase 2: Creating New Directory Structure"
    
    foreach ($dir in $DirectoriesToCreate) {
        $dirPath = Join-Path $SolutionRoot $dir
        if (-not (Test-Path $dirPath)) {
            Write-Host "  Creating directory: $dir" -ForegroundColor Gray
            if ($PSCmdlet.ShouldProcess($dir, "Create Directory")) {
                New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
            }
        }
    }
    
    # Phase 3: Move Projects According to Mappings
    Write-Phase "Phase 3: Moving Projects"
    
    # Group mappings by category for better progress tracking
    $CoreProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -like "src/Core/*" }
    $ExtensionProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -like "src/Extensions/*" }
    $ToolProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -like "src/Tools/*" }
    $RuntimeProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -like "src/Runtime/*" }
    $BackendProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -like "src/Backends/*" }
    $TestProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -like "tests/*" }
    $OtherProjects = $ProjectMappings.GetEnumerator() | Where-Object { $_.Value -notlike "src/*" -and $_.Value -notlike "tests/*" }
    
    # Move in dependency order
    Write-Host "Moving Core Projects..." -ForegroundColor Cyan
    foreach ($mapping in $CoreProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Core project"
    }
    
    Write-Host "Moving Extension Projects..." -ForegroundColor Cyan
    foreach ($mapping in $ExtensionProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Extension project"
    }
    
    Write-Host "Moving Tool Projects..." -ForegroundColor Cyan
    foreach ($mapping in $ToolProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Tool project"
    }
    
    Write-Host "Moving Runtime Projects..." -ForegroundColor Cyan
    foreach ($mapping in $RuntimeProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Runtime project"
    }
    
    Write-Host "Moving Backend Projects..." -ForegroundColor Cyan
    foreach ($mapping in $BackendProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Backend project"
    }
    
    Write-Host "Moving Test Projects..." -ForegroundColor Cyan
    foreach ($mapping in $TestProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Test project"
    }
    
    Write-Host "Moving Other Projects..." -ForegroundColor Cyan
    foreach ($mapping in $OtherProjects) {
        Move-ProjectSafely -Source $mapping.Key -Destination $mapping.Value -Description "Sample/Other project"
    }
    
    # Phase 4: Merge Duplicate Projects
    Write-Phase "Phase 4: Merging Duplicate Projects"
    
    foreach ($merge in $ProjectMerges.GetEnumerator()) {
        Merge-Projects -Target $merge.Key -Sources $merge.Value -Description "test projects"
    }
    
    # Phase 5: Clean Up
    Write-Phase "Phase 5: Cleaning Up"
    
    foreach ($item in $ItemsToDelete) {
        $itemPath = Join-Path $SolutionRoot $item
        if (Test-Path $itemPath) {
            Write-Host "  Removing: $item" -ForegroundColor Gray
            if ($PSCmdlet.ShouldProcess($item, "Delete")) {
                Remove-Item -Path $itemPath -Recurse -Force
            }
        }
    }
    
    # Remove empty directories
    Write-Host "Removing empty directories..." -ForegroundColor Gray
    Remove-EmptyDirectories -Path $SolutionRoot
    
    Write-Phase "Reorganization Complete!"
    Write-Host "Next steps:" -ForegroundColor Green
    Write-Host "1. Run Update-SolutionFile.ps1 to update the .sln file" -ForegroundColor White
    Write-Host "2. Run Update-ProjectReferences.ps1 to update project references" -ForegroundColor White  
    Write-Host "3. Run Validate-BuildConfiguration.ps1 to verify the build" -ForegroundColor White
    Write-Host "4. Test the solution build with 'dotnet build'" -ForegroundColor White
    
} catch {
    Write-Error "Reorganization failed: $($_.Exception.Message)"
    Write-Host "You can restore from backup at: $BackupPath" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Reorganization completed successfully!" -ForegroundColor Green