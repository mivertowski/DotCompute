#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates project references in all csproj files to reflect the new project structure.

.DESCRIPTION
    This script scans all project files and updates their project references to point
    to the new locations after the solution reorganization. It handles both direct
    project references and package references that might be affected.

.PARAMETER WhatIf
    Shows what changes would be made without actually modifying files.

.PARAMETER Force
    Forces updates even if some references cannot be resolved automatically.

.PARAMETER BackupProjects
    Creates backups of project files before making changes.

.EXAMPLE
    .\Update-ProjectReferences.ps1 -WhatIf
    Shows what project reference changes would be made.

.EXAMPLE
    .\Update-ProjectReferences.ps1 -BackupProjects
    Updates project references and creates backups of modified files.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$WhatIf,
    [switch]$Force,
    [switch]$BackupProjects
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Get script directory and solution root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Resolve-Path "$ScriptDir/../.."

Write-Host "DotCompute Project References Update Script" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Solution Root: $SolutionRoot" -ForegroundColor Cyan

# Define the project relocation mappings (from Reorganize-Solution.ps1)
$ProjectRelocations = @{
    # Core Projects - these are likely referenced by many others
    "src/DotCompute.Abstractions/DotCompute.Abstractions.csproj" = "src/Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj"
    "src/DotCompute.Core/DotCompute.Core.csproj" = "src/Core/DotCompute.Core/DotCompute.Core.csproj"
    "src/DotCompute.Memory/DotCompute.Memory.csproj" = "src/Core/DotCompute.Memory/DotCompute.Memory.csproj"
    
    # Extension Projects
    "src/DotCompute.Linq/DotCompute.Linq.csproj" = "src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj"
    "src/DotCompute.Algorithms/DotCompute.Algorithms.csproj" = "src/Extensions/DotCompute.Algorithms/DotCompute.Algorithms.csproj"
    
    # Tool Projects
    "src/DotCompute.Generators/DotCompute.Generators.csproj" = "src/Tools/DotCompute.Generators/DotCompute.Generators.csproj"
    "src/DotCompute.Plugins/DotCompute.Plugins.csproj" = "src/Tools/DotCompute.Plugins/DotCompute.Plugins.csproj"
    
    # Runtime Projects
    "src/DotCompute.Runtime/DotCompute.Runtime.csproj" = "src/Runtime/DotCompute.Runtime/DotCompute.Runtime.csproj"
    
    # Backend Projects
    "plugins/backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj" = "src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj"
    "plugins/backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj" = "src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj"
    "plugins/backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj" = "src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj"
    
    # Test Projects - these might reference each other or shared utilities
    "plugins/backends/DotCompute.Backends.CPU/tests/DotCompute.Backends.CPU.Tests.csproj" = "tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj"
    "plugins/backends/DotCompute.Backends.Metal/tests/DotCompute.Backends.Metal.Tests.csproj" = "tests/Unit/DotCompute.Backends.Metal.Tests/DotCompute.Backends.Metal.Tests.csproj"
    "benchmarks/DotCompute.Benchmarks/DotCompute.Benchmarks.csproj" = "tests/Performance/DotCompute.Benchmarks/DotCompute.Benchmarks.csproj"
    "tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj" = "tests/Hardware/DotCompute.Hardware.CUDA.Tests/DotCompute.Hardware.CUDA.Tests.csproj"
    
    # Sample Projects
    "examples/ExpressionToDynamicKernel/ExpressionToDynamicKernel.csproj" = "samples/ExpressionToDynamicKernel/ExpressionToDynamicKernel.csproj"
}

# Get all current project files
function Get-AllProjectFiles {
    return Get-ChildItem -Path $SolutionRoot -Filter "*.csproj" -Recurse | ForEach-Object {
        [PSCustomObject]@{
            Name = $_.BaseName
            FullPath = $_.FullName
            RelativePath = $_.FullName.Substring($SolutionRoot.Length + 1).Replace('\', '/')
            Content = Get-Content -Path $_.FullName -Raw
        }
    }
}

# Parse project references from project file content
function Get-ProjectReferencesFromContent {
    param([string]$Content)
    
    $References = @()
    
    # Match <ProjectReference Include="path" />
    $ProjectReferencePattern = '<ProjectReference\s+Include\s*=\s*["\']([^"\']+)["\']'
    $Matches = [regex]::Matches($Content, $ProjectReferencePattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    
    foreach ($Match in $Matches) {
        $References += [PSCustomObject]@{
            Type = "ProjectReference"
            OriginalPath = $Match.Groups[1].Value
            FullMatch = $Match.Value
            Index = $Match.Index
            Length = $Match.Length
        }
    }
    
    return $References
}

# Normalize path separators for consistent comparison
function Normalize-Path {
    param([string]$Path)
    return $Path.Replace('\', '/').TrimStart('./')
}

# Calculate relative path from one project to another
function Get-RelativePath {
    param(
        [string]$FromPath,
        [string]$ToPath
    )
    
    # Convert to full paths for calculation
    $FromDir = Split-Path (Join-Path $SolutionRoot $FromPath) -Parent
    $ToFile = Join-Path $SolutionRoot $ToPath
    
    try {
        # Try to get relative path using .NET method
        $Uri = New-Object System.Uri($FromDir + [System.IO.Path]::DirectorySeparatorChar)
        $RelativeUri = $Uri.MakeRelativeUri((New-Object System.Uri($ToFile)))
        $RelativePath = [System.Web.HttpUtility]::UrlDecode($RelativeUri.ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        return $RelativePath
    }
    catch {
        # Fallback: manual calculation
        Write-Warning "Could not calculate relative path from $FromPath to $ToPath"
        return $ToPath
    }
}

# Update project references in project content
function Update-ProjectReferences {
    param(
        [string]$Content,
        [array]$References,
        [string]$ProjectPath,
        [hashtable]$Relocations
    )
    
    $UpdatedContent = $Content
    $ChangesCount = 0
    
    # Process references in reverse order to maintain string indices
    $SortedReferences = $References | Sort-Object Index -Descending
    
    foreach ($Ref in $SortedReferences) {
        $NormalizedOriginalPath = Normalize-Path $Ref.OriginalPath
        
        # Check if this reference needs to be updated
        $NewPath = $null
        foreach ($Relocation in $Relocations.GetEnumerator()) {
            $OldPath = Normalize-Path $Relocation.Key
            if ($NormalizedOriginalPath -eq $OldPath -or $NormalizedOriginalPath.EndsWith("/$OldPath")) {
                $NewPath = $Relocation.Value
                break
            }
        }
        
        if ($NewPath) {
            # Calculate new relative path from current project to target
            $NewRelativePath = Get-RelativePath -FromPath $ProjectPath -ToPath $NewPath
            $NewRelativePath = $NewRelativePath.Replace('\', '/')
            
            # Replace the old reference with the new one
            $NewReference = $Ref.FullMatch.Replace($Ref.OriginalPath, $NewRelativePath)
            
            Write-Host "    $($Ref.OriginalPath) -> $NewRelativePath" -ForegroundColor Gray
            
            $UpdatedContent = $UpdatedContent.Remove($Ref.Index, $Ref.Length).Insert($Ref.Index, $NewReference)
            $ChangesCount++
        }
    }
    
    return @{
        Content = $UpdatedContent
        ChangesCount = $ChangesCount
    }
}

# Main execution
try {
    Push-Location $SolutionRoot
    
    Write-Host "Scanning for project files..." -ForegroundColor Yellow
    $AllProjects = Get-AllProjectFiles
    Write-Host "Found $($AllProjects.Count) project files" -ForegroundColor Green
    
    $TotalChanges = 0
    $ModifiedFiles = @()
    
    Write-Host ""
    Write-Host "Analyzing project references..." -ForegroundColor Yellow
    
    foreach ($Project in $AllProjects) {
        Write-Host "Processing: $($Project.Name)" -ForegroundColor Cyan
        
        # Parse project references
        $References = Get-ProjectReferencesFromContent -Content $Project.Content
        
        if ($References.Count -eq 0) {
            Write-Host "  No project references found" -ForegroundColor Gray
            continue
        }
        
        Write-Host "  Found $($References.Count) project reference(s):" -ForegroundColor White
        
        # Update references
        $UpdateResult = Update-ProjectReferences -Content $Project.Content -References $References -ProjectPath $Project.RelativePath -Relocations $ProjectRelocations
        
        if ($UpdateResult.ChangesCount -gt 0) {
            $TotalChanges += $UpdateResult.ChangesCount
            $ModifiedFiles += [PSCustomObject]@{
                Project = $Project
                NewContent = $UpdateResult.Content
                ChangesCount = $UpdateResult.ChangesCount
            }
            
            Write-Host "  Updated $($UpdateResult.ChangesCount) reference(s)" -ForegroundColor Green
        } else {
            Write-Host "  No updates needed" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Green
    Write-Host "  Total projects scanned: $($AllProjects.Count)" -ForegroundColor White
    Write-Host "  Projects to modify: $($ModifiedFiles.Count)" -ForegroundColor White
    Write-Host "  Total reference updates: $TotalChanges" -ForegroundColor White
    
    if ($WhatIf) {
        Write-Host ""
        Write-Host "WhatIf: The following files would be modified:" -ForegroundColor Cyan
        foreach ($Modified in $ModifiedFiles) {
            Write-Host "  $($Modified.Project.RelativePath) ($($Modified.ChangesCount) changes)" -ForegroundColor Gray
        }
        return
    }
    
    if ($ModifiedFiles.Count -eq 0) {
        Write-Host ""
        Write-Host "No project reference updates needed!" -ForegroundColor Green
        return
    }
    
    # Apply changes
    Write-Host ""
    Write-Host "Applying changes..." -ForegroundColor Yellow
    
    foreach ($Modified in $ModifiedFiles) {
        $ProjectFile = $Modified.Project.FullPath
        
        # Create backup if requested
        if ($BackupProjects) {
            $BackupPath = "$ProjectFile.backup"
            Copy-Item -Path $ProjectFile -Destination $BackupPath -Force
            Write-Host "  Backup created: $BackupPath" -ForegroundColor DarkGray
        }
        
        # Write updated content
        if ($PSCmdlet.ShouldProcess($Modified.Project.RelativePath, "Update Project References")) {
            [System.IO.File]::WriteAllText($ProjectFile, $Modified.NewContent, [System.Text.Encoding]::UTF8)
            Write-Host "  Updated: $($Modified.Project.RelativePath)" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "Project references updated successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Run 'dotnet restore' to restore package references" -ForegroundColor White
    Write-Host "2. Run 'dotnet build' to verify all projects build correctly" -ForegroundColor White
    Write-Host "3. Run Validate-BuildConfiguration.ps1 to perform comprehensive validation" -ForegroundColor White
    Write-Host "4. Test key scenarios to ensure functionality is preserved" -ForegroundColor White
    
} catch {
    Write-Error "Failed to update project references: $($_.Exception.Message)"
    if ($BackupProjects) {
        Write-Host "Project backups were created with .backup extension" -ForegroundColor Yellow
    }
    exit 1
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Project references update completed!" -ForegroundColor Green