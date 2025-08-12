#!/usr/bin/env pwsh
# Copyright (c) 2025 Michael Ivertowski
# Licensed under the MIT License. See LICENSE file in the project root for license information.

<#
.SYNOPSIS
    Analyzes and fixes namespace alignment with folder structure in DotCompute solution.

.DESCRIPTION
    This script scans all C# files in the DotCompute solution to identify namespace misalignments
    and provides options to fix them automatically.

.PARAMETER Analyze
    Only analyze and report misalignments without making changes.

.PARAMETER Fix
    Fix all identified namespace misalignments.

.PARAMETER DryRun
    Show what changes would be made without actually making them.

.EXAMPLE
    ./namespace-alignment-analyzer.ps1 -Analyze
    
.EXAMPLE
    ./namespace-alignment-analyzer.ps1 -Fix

.EXAMPLE
    ./namespace-alignment-analyzer.ps1 -DryRun
#>

param(
    [switch]$Analyze,
    [switch]$Fix,
    [switch]$DryRun
)

# Set error action and script location
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir

Write-Host "DotCompute Namespace Alignment Analyzer" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

class NamespaceIssue {
    [string]$FilePath
    [string]$CurrentNamespace
    [string]$ExpectedNamespace
    [int]$LineNumber
    [string]$IssueType
    [string]$RelativePath
}

function Get-ExpectedNamespace {
    param(
        [string]$FilePath,
        [string]$SolutionRoot
    )
    
    # Convert file path to relative path from solution root
    $RelativePath = [System.IO.Path]::GetRelativePath($SolutionRoot, $FilePath)
    $RelativePath = $RelativePath -replace '\\', '/'
    
    # Define namespace mapping rules based on folder structure
    $NamespaceMappings = @{
        "src/DotCompute.Abstractions/" = "DotCompute.Abstractions"
        "src/DotCompute.Algorithms/" = "DotCompute.Algorithms"
        "src/DotCompute.Core/" = "DotCompute.Core"
        "src/DotCompute.Generators/" = "DotCompute.Generators"
        "src/DotCompute.Linq/" = "DotCompute.Linq"
        "src/DotCompute.Memory/" = "DotCompute.Memory"
        "src/DotCompute.Plugins/" = "DotCompute.Plugins"
        "src/DotCompute.Runtime/" = "DotCompute.Runtime"
        "plugins/backends/DotCompute.Backends.CPU/" = "DotCompute.Backends.CPU"
        "plugins/backends/DotCompute.Backends.CUDA/" = "DotCompute.Backends.CUDA"
        "plugins/backends/DotCompute.Backends.Metal/" = "DotCompute.Backends.Metal"
        "tests/" = "DotCompute.Tests"
        "benchmarks/" = "DotCompute.Benchmarks"
        "samples/" = "DotCompute.Samples"
        "examples/" = "DotCompute.Examples"
        "tools/" = "DotCompute.Tools"
    }
    
    # Find the best matching base namespace
    $BaseNamespace = ""
    $LongestMatch = 0
    
    foreach ($prefix in $NamespaceMappings.Keys) {
        if ($RelativePath.StartsWith($prefix) -and $prefix.Length -gt $LongestMatch) {
            $BaseNamespace = $NamespaceMappings[$prefix]
            $LongestMatch = $prefix.Length
        }
    }
    
    if ($BaseNamespace -eq "") {
        return $null
    }
    
    # Extract the relative path after the base
    $RemainingPath = $RelativePath.Substring($LongestMatch)
    
    # Remove filename and src/ prefix if present
    $DirectoryParts = @()
    $Parts = $RemainingPath -split '/'
    
    foreach ($Part in $Parts) {
        # Skip empty parts, file extensions, and common non-namespace directories
        if ($Part -and 
            -not $Part.Contains('.') -and 
            $Part -ne "src" -and 
            $Part -ne "tests" -and
            $Part -ne "bin" -and 
            $Part -ne "obj") {
            $DirectoryParts += $Part
        }
    }
    
    # Build final namespace
    if ($DirectoryParts.Count -gt 0) {
        return $BaseNamespace + "." + ($DirectoryParts -join ".")
    } else {
        return $BaseNamespace
    }
}

function Get-NamespaceFromFile {
    param(
        [string]$FilePath
    )
    
    try {
        $Lines = Get-Content $FilePath -ErrorAction Stop
        for ($i = 0; $i -lt $Lines.Count; $i++) {
            $Line = $Lines[$i].Trim()
            if ($Line -match '^namespace\s+([^;{]+)') {
                $Namespace = $Matches[1].Trim()
                # Remove trailing semicolon if present
                $Namespace = $Namespace -replace ';$', ''
                return @{
                    Namespace = $Namespace
                    LineNumber = $i + 1
                }
            }
        }
    }
    catch {
        Write-Warning "Failed to read file: $FilePath - $($_.Exception.Message)"
    }
    
    return $null
}

function Find-NamespaceIssues {
    param(
        [string]$SolutionRoot
    )
    
    Write-Host "Scanning for C# files..." -ForegroundColor Yellow
    
    # Find all C# files
    $CSharpFiles = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "*.cs" | 
        Where-Object { 
            $_.FullName -notlike "*\bin\*" -and 
            $_.FullName -notlike "*\obj\*" -and
            $_.FullName -notlike "*\.git\*" -and
            $_.Name -ne "GlobalAssemblyInfo.cs" -and
            $_.Name -ne "AssemblyInfo.cs"
        }
    
    Write-Host "Found $($CSharpFiles.Count) C# files to analyze" -ForegroundColor Green
    
    $Issues = @()
    $ProcessedCount = 0
    
    foreach ($File in $CSharpFiles) {
        $ProcessedCount++
        if ($ProcessedCount % 50 -eq 0) {
            Write-Host "Processed $ProcessedCount / $($CSharpFiles.Count) files..." -ForegroundColor Gray
        }
        
        $NamespaceInfo = Get-NamespaceFromFile $File.FullName
        if ($NamespaceInfo) {
            $ExpectedNamespace = Get-ExpectedNamespace $File.FullName $SolutionRoot
            
            if ($ExpectedNamespace -and $NamespaceInfo.Namespace -ne $ExpectedNamespace) {
                $Issue = [NamespaceIssue]::new()
                $Issue.FilePath = $File.FullName
                $Issue.CurrentNamespace = $NamespaceInfo.Namespace
                $Issue.ExpectedNamespace = $ExpectedNamespace
                $Issue.LineNumber = $NamespaceInfo.LineNumber
                $Issue.IssueType = "Misalignment"
                $Issue.RelativePath = [System.IO.Path]::GetRelativePath($SolutionRoot, $File.FullName)
                
                $Issues += $Issue
            }
        }
    }
    
    return $Issues
}

function Show-AnalysisReport {
    param(
        [NamespaceIssue[]]$Issues
    )
    
    Write-Host ""
    Write-Host "NAMESPACE ALIGNMENT ANALYSIS REPORT" -ForegroundColor Magenta
    Write-Host "===================================" -ForegroundColor Magenta
    Write-Host ""
    
    if ($Issues.Count -eq 0) {
        Write-Host "✅ All namespaces are properly aligned with folder structure!" -ForegroundColor Green
        return
    }
    
    Write-Host "Found $($Issues.Count) namespace alignment issues:" -ForegroundColor Red
    Write-Host ""
    
    # Group issues by expected namespace for better organization
    $GroupedIssues = $Issues | Group-Object ExpectedNamespace | Sort-Object Name
    
    foreach ($Group in $GroupedIssues) {
        Write-Host "Expected Namespace: $($Group.Name)" -ForegroundColor Cyan
        Write-Host "─".PadRight(50, '─') -ForegroundColor Gray
        
        foreach ($Issue in $Group.Group) {
            Write-Host "  📁 $($Issue.RelativePath)" -ForegroundColor White
            Write-Host "    Current:  $($Issue.CurrentNamespace)" -ForegroundColor Red
            Write-Host "    Expected: $($Issue.ExpectedNamespace)" -ForegroundColor Green
            Write-Host "    Line:     $($Issue.LineNumber)" -ForegroundColor Gray
            Write-Host ""
        }
    }
    
    # Summary statistics
    $UniqueNamespaces = ($Issues | Group-Object CurrentNamespace).Count
    $FilesAffected = $Issues.Count
    
    Write-Host "SUMMARY" -ForegroundColor Yellow
    Write-Host "-------" -ForegroundColor Yellow
    Write-Host "Files with issues: $FilesAffected" -ForegroundColor White
    Write-Host "Unique wrong namespaces: $UniqueNamespaces" -ForegroundColor White
}

function Fix-NamespaceIssues {
    param(
        [NamespaceIssue[]]$Issues,
        [bool]$DryRun = $false
    )
    
    if ($Issues.Count -eq 0) {
        Write-Host "No issues to fix!" -ForegroundColor Green
        return
    }
    
    Write-Host ""
    Write-Host "FIXING NAMESPACE ISSUES" -ForegroundColor Magenta
    Write-Host "=======================" -ForegroundColor Magenta
    Write-Host ""
    
    if ($DryRun) {
        Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
        Write-Host ""
    }
    
    $FixedCount = 0
    $FailedCount = 0
    
    foreach ($Issue in $Issues) {
        try {
            Write-Host "Fixing: $($Issue.RelativePath)" -ForegroundColor White
            Write-Host "  $($Issue.CurrentNamespace) → $($Issue.ExpectedNamespace)" -ForegroundColor Gray
            
            if (-not $DryRun) {
                $Content = Get-Content $Issue.FilePath -Raw
                
                # Replace the namespace declaration
                $OldPattern = "namespace\s+$([regex]::Escape($Issue.CurrentNamespace))"
                $NewNamespace = "namespace $($Issue.ExpectedNamespace)"
                
                $NewContent = $Content -replace $OldPattern, $NewNamespace
                
                # Write back to file
                Set-Content -Path $Issue.FilePath -Value $NewContent -NoNewline
            }
            
            $FixedCount++
        }
        catch {
            Write-Error "Failed to fix $($Issue.FilePath): $($_.Exception.Message)"
            $FailedCount++
        }
    }
    
    Write-Host ""
    Write-Host "RESULTS" -ForegroundColor Yellow
    Write-Host "-------" -ForegroundColor Yellow
    Write-Host "Fixed: $FixedCount" -ForegroundColor Green
    if ($FailedCount -gt 0) {
        Write-Host "Failed: $FailedCount" -ForegroundColor Red
    }
}

function Update-UsingStatements {
    param(
        [string]$SolutionRoot,
        [NamespaceIssue[]]$Issues,
        [bool]$DryRun = $false
    )
    
    if ($Issues.Count -eq 0) {
        return
    }
    
    Write-Host ""
    Write-Host "UPDATING USING STATEMENTS" -ForegroundColor Magenta
    Write-Host "=========================" -ForegroundColor Magenta
    Write-Host ""
    
    # Create mapping of old namespace to new namespace
    $NamespaceMapping = @{}
    foreach ($Issue in $Issues) {
        $NamespaceMapping[$Issue.CurrentNamespace] = $Issue.ExpectedNamespace
    }
    
    # Find all C# files to update using statements
    $CSharpFiles = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "*.cs" | 
        Where-Object { 
            $_.FullName -notlike "*\bin\*" -and 
            $_.FullName -notlike "*\obj\*" -and
            $_.FullName -notlike "*\.git\*"
        }
    
    $UpdatedFiles = 0
    
    foreach ($File in $CSharpFiles) {
        try {
            $Content = Get-Content $File.FullName -Raw
            $OriginalContent = $Content
            
            # Update using statements
            foreach ($OldNamespace in $NamespaceMapping.Keys) {
                $NewNamespace = $NamespaceMapping[$OldNamespace]
                $Content = $Content -replace "using\s+$([regex]::Escape($OldNamespace))", "using $NewNamespace"
            }
            
            # Only write if content changed
            if ($Content -ne $OriginalContent) {
                $RelativePath = [System.IO.Path]::GetRelativePath($SolutionRoot, $File.FullName)
                Write-Host "Updating using statements in: $RelativePath" -ForegroundColor Gray
                
                if (-not $DryRun) {
                    Set-Content -Path $File.FullName -Value $Content -NoNewline
                }
                $UpdatedFiles++
            }
        }
        catch {
            Write-Warning "Failed to update using statements in $($File.FullName): $($_.Exception.Message)"
        }
    }
    
    Write-Host "Updated using statements in $UpdatedFiles files" -ForegroundColor Green
}

# Main execution
try {
    # Validate parameters
    if (-not ($Analyze -or $Fix -or $DryRun)) {
        Write-Host "Please specify one of: -Analyze, -Fix, or -DryRun" -ForegroundColor Red
        exit 1
    }
    
    # Find all namespace issues
    $Issues = Find-NamespaceIssues $SolutionRoot
    
    # Show analysis report
    Show-AnalysisReport $Issues
    
    # Perform fixes if requested
    if ($Fix -or $DryRun) {
        Fix-NamespaceIssues $Issues $DryRun
        Update-UsingStatements $SolutionRoot $Issues $DryRun
        
        if (-not $DryRun) {
            Write-Host ""
            Write-Host "✅ Namespace alignment complete!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Next steps:" -ForegroundColor Yellow
            Write-Host "1. Build the solution to verify no compilation errors" -ForegroundColor White
            Write-Host "2. Run tests to ensure functionality is preserved" -ForegroundColor White
            Write-Host "3. Commit the namespace alignment changes" -ForegroundColor White
        }
    }
}
catch {
    Write-Error "Script execution failed: $($_.Exception.Message)"
    exit 1
}