#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates the DotCompute.sln file to reflect the new project structure.

.DESCRIPTION
    This script regenerates the solution file with the new project locations,
    proper solution folders, and maintains all project configurations.

.PARAMETER SolutionPath
    Path to the solution file. Defaults to DotCompute.sln in the current directory.

.PARAMETER WhatIf
    Shows what would be changed without making modifications.

.PARAMETER BackupOriginal
    Creates a backup of the original solution file before making changes.

.EXAMPLE
    .\Update-SolutionFile.ps1 -WhatIf
    Shows what changes would be made to the solution file.

.EXAMPLE
    .\Update-SolutionFile.ps1 -BackupOriginal
    Updates the solution file and creates a backup of the original.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SolutionPath = "DotCompute.sln",
    [switch]$WhatIf,
    [switch]$BackupOriginal
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Get script directory and solution root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Resolve-Path "$ScriptDir/../.."
$SolutionFullPath = Join-Path $SolutionRoot $SolutionPath

Write-Host "DotCompute Solution File Update Script" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host "Solution File: $SolutionFullPath" -ForegroundColor Cyan

if (-not (Test-Path $SolutionFullPath)) {
    throw "Solution file not found: $SolutionFullPath"
}

# Create backup if requested
if ($BackupOriginal) {
    $BackupPath = "$SolutionFullPath.backup"
    Write-Host "Creating backup: $BackupPath" -ForegroundColor Yellow
    Copy-Item -Path $SolutionFullPath -Destination $BackupPath -Force
}

# Define solution folder structure
$SolutionFolders = @{
    "src" = @{
        "Core" = @()
        "Extensions" = @()
        "Backends" = @()
        "Tools" = @()
        "Runtime" = @()
    }
    "tests" = @{
        "Unit" = @()
        "Integration" = @()
        "Performance" = @() 
        "Hardware" = @()
        "Shared" = @()
    }
    "samples" = @()
    "tools" = @()
}

# Find all project files in the new structure
function Find-AllProjects {
    param([string]$RootPath)
    
    $Projects = @()
    
    Get-ChildItem -Path $RootPath -Filter "*.csproj" -Recurse | ForEach-Object {
        $RelativePath = $_.FullName.Substring($RootPath.Length + 1).Replace('\', '/')
        $ProjectName = $_.BaseName
        $Directory = Split-Path $RelativePath -Parent
        
        $Projects += [PSCustomObject]@{
            Name = $ProjectName
            RelativePath = $RelativePath
            Directory = $Directory
            FullPath = $_.FullName
            Guid = [Guid]::NewGuid().ToString("B").ToUpper()
        }
    }
    
    return $Projects
}

# Categorize projects into solution folders
function Get-SolutionFolderForProject {
    param([string]$ProjectPath)
    
    $PathParts = $ProjectPath.Split('/')
    
    # Map directory structure to solution folders
    if ($PathParts[0] -eq "src") {
        switch ($PathParts[1]) {
            "Core" { return "src\Core" }
            "Extensions" { return "src\Extensions" }
            "Backends" { return "src\Backends" }
            "Tools" { return "src\Tools" }
            "Runtime" { return "src\Runtime" }
            default { return "src" }
        }
    } elseif ($PathParts[0] -eq "tests") {
        switch ($PathParts[1]) {
            "Unit" { return "tests\Unit" }
            "Integration" { return "tests\Integration" }
            "Performance" { return "tests\Performance" }
            "Hardware" { return "tests\Hardware" }
            "Shared" { return "tests\Shared" }
            default { return "tests" }
        }
    } elseif ($PathParts[0] -eq "samples") {
        return "samples"
    } elseif ($PathParts[0] -eq "tools") {
        return "tools"
    } else {
        return $null # Root level
    }
}

# Generate new solution content
function Generate-SolutionContent {
    param(
        [array]$Projects,
        [hashtable]$SolutionFolders
    )
    
    $Content = @()
    
    # Solution header
    $Content += "Microsoft Visual Studio Solution File, Format Version 12.00"
    $Content += "# Visual Studio Version 17"
    $Content += "VisualStudioVersion = 17.9.0.0"
    $Content += "MinimumVisualStudioVersion = 10.0.40219.1"
    
    # Create solution folder GUIDs
    $FolderGuids = @{}
    function Add-SolutionFolder {
        param([string]$Path, [string]$ParentPath = $null)
        
        if (-not $FolderGuids.ContainsKey($Path)) {
            $FolderGuids[$Path] = [Guid]::NewGuid().ToString("B").ToUpper()
            $FolderName = Split-Path $Path -Leaf
            $Content += "Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"$FolderName`", `"$FolderName`", `"$($FolderGuids[$Path])`""
            $Content += "EndProject"
        }
    }
    
    # Add solution folders
    Add-SolutionFolder "src"
    Add-SolutionFolder "src\Core"
    Add-SolutionFolder "src\Extensions"  
    Add-SolutionFolder "src\Backends"
    Add-SolutionFolder "src\Tools"
    Add-SolutionFolder "src\Runtime"
    Add-SolutionFolder "tests"
    Add-SolutionFolder "tests\Unit"
    Add-SolutionFolder "tests\Integration"
    Add-SolutionFolder "tests\Performance"
    Add-SolutionFolder "tests\Hardware"
    Add-SolutionFolder "tests\Shared"
    Add-SolutionFolder "samples"
    Add-SolutionFolder "tools"
    
    # Add solution items folder for configuration files
    Add-SolutionFolder "Solution Items"
    $Content += "Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"Solution Items`", `"Solution Items`", `"$($FolderGuids['Solution Items'])`""
    $Content += "`tProjectSection(SolutionItems) = preProject"
    $Content += "`t`t.editorconfig = .editorconfig"
    $Content += "`t`t.gitignore = .gitignore"
    $Content += "`t`tDirectory.Build.props = Directory.Build.props"
    $Content += "`t`tDirectory.Build.targets = Directory.Build.targets"
    $Content += "`t`tglobal.json = global.json"
    $Content += "`t`tLICENSE = LICENSE"
    $Content += "`t`tREADME.md = README.md"
    $Content += "`tEndProjectSection"
    $Content += "EndProject"
    
    # Add projects
    foreach ($Project in $Projects | Sort-Object Directory, Name) {
        $ProjectPath = $Project.RelativePath.Replace('/', '\')
        $Content += "Project(`"{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`") = `"$($Project.Name)`", `"$ProjectPath`", `"$($Project.Guid)`""
        $Content += "EndProject"
    }
    
    # Global sections
    $Content += "Global"
    
    # Solution configuration platforms
    $Content += "`tGlobalSection(SolutionConfigurationPlatforms) = preSolution"
    $Content += "`t`tDebug|Any CPU = Debug|Any CPU"
    $Content += "`t`tDebug|x64 = Debug|x64"  
    $Content += "`t`tDebug|x86 = Debug|x86"
    $Content += "`t`tRelease|Any CPU = Release|Any CPU"
    $Content += "`t`tRelease|x64 = Release|x64"
    $Content += "`t`tRelease|x86 = Release|x86"
    $Content += "`tEndGlobalSection"
    
    # Project configuration platforms
    $Content += "`tGlobalSection(ProjectConfigurationPlatforms) = postSolution"
    foreach ($Project in $Projects) {
        $ProjectGuid = $Project.Guid
        $Configurations = @("Debug|Any CPU", "Debug|x64", "Debug|x86", "Release|Any CPU", "Release|x64", "Release|x86")
        
        foreach ($Config in $Configurations) {
            $Content += "`t`t$ProjectGuid.$Config.ActiveCfg = $Config"
            $Content += "`t`t$ProjectGuid.$Config.Build.0 = $Config"
        }
    }
    $Content += "`tEndGlobalSection"
    
    # Solution properties
    $Content += "`tGlobalSection(SolutionProperties) = preSolution"
    $Content += "`t`tHideSolutionNode = FALSE"
    $Content += "`tEndGlobalSection"
    
    # Nested projects (solution folder hierarchy)
    $Content += "`tGlobalSection(NestedProjects) = preSolution"
    
    # Nest subfolder solution folders under parent folders
    $Content += "`t`t$($FolderGuids['src\Core']) = $($FolderGuids['src'])"
    $Content += "`t`t$($FolderGuids['src\Extensions']) = $($FolderGuids['src'])"
    $Content += "`t`t$($FolderGuids['src\Backends']) = $($FolderGuids['src'])"
    $Content += "`t`t$($FolderGuids['src\Tools']) = $($FolderGuids['src'])"
    $Content += "`t`t$($FolderGuids['src\Runtime']) = $($FolderGuids['src'])"
    $Content += "`t`t$($FolderGuids['tests\Unit']) = $($FolderGuids['tests'])"
    $Content += "`t`t$($FolderGuids['tests\Integration']) = $($FolderGuids['tests'])"
    $Content += "`t`t$($FolderGuids['tests\Performance']) = $($FolderGuids['tests'])"
    $Content += "`t`t$($FolderGuids['tests\Hardware']) = $($FolderGuids['tests'])"
    $Content += "`t`t$($FolderGuids['tests\Shared']) = $($FolderGuids['tests'])"
    
    # Nest projects under their solution folders
    foreach ($Project in $Projects) {
        $SolutionFolder = Get-SolutionFolderForProject $Project.Directory
        if ($SolutionFolder -and $FolderGuids.ContainsKey($SolutionFolder)) {
            $Content += "`t`t$($Project.Guid) = $($FolderGuids[$SolutionFolder])"
        }
    }
    
    $Content += "`tEndGlobalSection"
    
    # Extensibility globals
    $Content += "`tGlobalSection(ExtensibilityGlobals) = postSolution"
    $Content += "`t`tSolutionGuid = {C5D6E7F8-A9B0-1234-5678-901234C89012}"
    $Content += "`tEndGlobalSection"
    
    $Content += "EndGlobal"
    
    return $Content
}

# Main execution
try {
    Push-Location $SolutionRoot
    
    Write-Host "Scanning for project files..." -ForegroundColor Yellow
    $AllProjects = Find-AllProjects -RootPath $SolutionRoot
    
    Write-Host "Found $($AllProjects.Count) project files:" -ForegroundColor Green
    $AllProjects | Sort-Object Directory, Name | ForEach-Object {
        $SolutionFolder = Get-SolutionFolderForProject $_.Directory
        $FolderDisplay = if ($SolutionFolder) { " [$SolutionFolder]" } else { " [Root]" }
        Write-Host "  $($_.RelativePath)$FolderDisplay" -ForegroundColor Gray
    }
    
    if ($WhatIf) {
        Write-Host ""
        Write-Host "WhatIf: Would generate new solution file with the above projects." -ForegroundColor Cyan
        Write-Host "WhatIf: Solution folders would be organized as:" -ForegroundColor Cyan
        Write-Host "  src/" -ForegroundColor Gray
        Write-Host "    Core/" -ForegroundColor Gray
        Write-Host "    Extensions/" -ForegroundColor Gray
        Write-Host "    Backends/" -ForegroundColor Gray
        Write-Host "    Tools/" -ForegroundColor Gray
        Write-Host "    Runtime/" -ForegroundColor Gray
        Write-Host "  tests/" -ForegroundColor Gray
        Write-Host "    Unit/" -ForegroundColor Gray
        Write-Host "    Integration/" -ForegroundColor Gray
        Write-Host "    Performance/" -ForegroundColor Gray
        Write-Host "    Hardware/" -ForegroundColor Gray
        Write-Host "    Shared/" -ForegroundColor Gray
        Write-Host "  samples/" -ForegroundColor Gray
        Write-Host "  tools/" -ForegroundColor Gray
        return
    }
    
    Write-Host ""
    Write-Host "Generating new solution file..." -ForegroundColor Yellow
    $NewSolutionContent = Generate-SolutionContent -Projects $AllProjects -SolutionFolders $SolutionFolders
    
    if ($PSCmdlet.ShouldProcess($SolutionFullPath, "Update Solution File")) {
        # Write new solution file with UTF-8 BOM (required for Visual Studio)
        $Encoding = New-Object System.Text.UTF8Encoding($true)
        [System.IO.File]::WriteAllLines($SolutionFullPath, $NewSolutionContent, $Encoding)
        
        Write-Host "Solution file updated successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Summary:" -ForegroundColor Green
        Write-Host "  Projects: $($AllProjects.Count)" -ForegroundColor White
        Write-Host "  Solution Folders: 12" -ForegroundColor White
        Write-Host "  Configurations: 6 (Debug/Release x AnyCPU/x64/x86)" -ForegroundColor White
        
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Yellow
        Write-Host "1. Open the solution in Visual Studio to verify structure" -ForegroundColor White
        Write-Host "2. Run 'dotnet restore' to restore package references" -ForegroundColor White
        Write-Host "3. Run 'dotnet build' to verify all projects build correctly" -ForegroundColor White
        Write-Host "4. Run Update-ProjectReferences.ps1 if needed" -ForegroundColor White
    }
    
} catch {
    Write-Error "Failed to update solution file: $($_.Exception.Message)"
    if ($BackupOriginal -and (Test-Path "$SolutionFullPath.backup")) {
        Write-Host "Original solution file backed up to: $SolutionFullPath.backup" -ForegroundColor Yellow
    }
    exit 1
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Solution file update completed!" -ForegroundColor Green