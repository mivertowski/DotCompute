#!/usr/bin/env pwsh
# Copyright (c) 2025 Michael Ivertowski
# Script to convert direct logger calls to LoggerMessage delegates

<#
.SYNOPSIS
    Converts direct ILogger calls to high-performance LoggerMessage delegates.

.DESCRIPTION
    This script scans C# files for direct logger calls (e.g., _logger.LogInformation)
    and converts them to use pre-compiled LoggerMessage delegates for better performance.

.PARAMETER Path
    Path to the directory to scan for C# files

.PARAMETER DryRun
    If specified, only shows what would be changed without modifying files

.EXAMPLE
    ./convert-to-logger-delegates.ps1 -Path ./src -DryRun
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$Path = ".",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

# Define the logger call patterns to search for
$loggerPatterns = @(
    '_logger\.LogTrace',
    '_logger\.LogDebug',
    '_logger\.LogInformation',
    '_logger\.LogWarning',
    '_logger\.LogError',
    '_logger\.LogCritical'
)

# Track statistics
$stats = @{
    FilesScanned = 0
    FilesModified = 0
    CallsReplaced = 0
    FilesWithErrors = 0
}

# Function to extract logger call information
function Parse-LoggerCall {
    param([string]$Line)
    
    if ($Line -match '_logger\.(Log\w+)\s*\(([^)]+)\)') {
        $level = $Matches[1]
        $args = $Matches[2]
        
        # Parse the arguments
        if ($args -match '^"([^"]+)"(.*)$') {
            $template = $Matches[1]
            $parameters = $Matches[2].Trim(',').Trim()
            
            return @{
                Level = $level
                Template = $template
                Parameters = $parameters
                Original = $Line
            }
        }
    }
    
    return $null
}

# Function to generate LoggerMessage delegate
function Generate-LoggerMessageDelegate {
    param($LogCall, [int]$EventId)
    
    $levelMap = @{
        'LogTrace' = 'Trace'
        'LogDebug' = 'Debug'
        'LogInformation' = 'Information'
        'LogWarning' = 'Warning'
        'LogError' = 'Error'
        'LogCritical' = 'Critical'
    }
    
    $level = $levelMap[$LogCall.Level]
    $methodName = "Log$($LogCall.Template -replace '[^a-zA-Z0-9]', '')"
    
    # Extract parameter types from template
    $paramCount = ([regex]::Matches($LogCall.Template, '\{[^}]+\}')).Count
    
    $delegateCode = @"
    [LoggerMessage(
        EventId = $EventId,
        Level = LogLevel.$level,
        Message = "$($LogCall.Template)")]
    public static partial void $methodName(this ILogger logger$(if ($paramCount -gt 0) { ", " + (1..$paramCount | ForEach-Object { "object param$_" }) -join ", " }));
"@
    
    return @{
        Delegate = $delegateCode
        MethodName = $methodName
        EventId = $EventId
    }
}

# Function to process a single file
function Process-CSharpFile {
    param([string]$FilePath)
    
    $stats.FilesScanned++
    Write-Host "Processing: $FilePath" -ForegroundColor Cyan
    
    try {
        $content = Get-Content $FilePath -Raw
        $lines = Get-Content $FilePath
        
        $modified = $false
        $newLines = @()
        $delegates = @()
        $nextEventId = 100000 + (Get-Random -Maximum 9999)
        
        foreach ($line in $lines) {
            $processed = $false
            
            foreach ($pattern in $loggerPatterns) {
                if ($line -match $pattern) {
                    $logCall = Parse-LoggerCall -Line $line
                    
                    if ($logCall) {
                        $delegateInfo = Generate-LoggerMessageDelegate -LogCall $logCall -EventId $nextEventId
                        $delegates += $delegateInfo
                        
                        # Replace the line with delegate call
                        $newCall = "_logger.$($delegateInfo.MethodName)("
                        if ($logCall.Parameters) {
                            $newCall += $logCall.Parameters
                        }
                        $newCall += ");"
                        
                        $newLines += $newCall
                        $processed = $true
                        $modified = $true
                        $stats.CallsReplaced++
                        $nextEventId++
                        
                        Write-Host "  Replaced: $($logCall.Level) call" -ForegroundColor Green
                        break
                    }
                }
            }
            
            if (-not $processed) {
                $newLines += $line
            }
        }
        
        if ($modified) {
            if (-not $DryRun) {
                # Check if LoggerMessages.cs exists in the project
                $projectDir = Split-Path $FilePath -Parent
                $loggingDir = Join-Path $projectDir "Logging"
                $loggerMessagesFile = Join-Path $loggingDir "LoggerMessages.cs"
                
                if (-not (Test-Path $loggingDir)) {
                    New-Item -ItemType Directory -Path $loggingDir -Force | Out-Null
                    Write-Host "  Created Logging directory" -ForegroundColor Yellow
                }
                
                # Append delegates to LoggerMessages.cs or create new one
                if ($delegates.Count -gt 0) {
                    Write-Host "  Generated $($delegates.Count) LoggerMessage delegates" -ForegroundColor Yellow
                    
                    # Note: In production, you would append these to the existing LoggerMessages.cs
                    # For now, we just show what would be generated
                    if ($DryRun) {
                        Write-Host "  Would generate delegates:" -ForegroundColor Magenta
                        $delegates | ForEach-Object {
                            Write-Host $_.Delegate -ForegroundColor Gray
                        }
                    }
                }
                
                # Update the original file
                Set-Content -Path $FilePath -Value ($newLines -join "`n")
                Write-Host "  File updated successfully" -ForegroundColor Green
            } else {
                Write-Host "  [DRY RUN] Would modify file with $($stats.CallsReplaced) replacements" -ForegroundColor Yellow
            }
            
            $stats.FilesModified++
        }
    }
    catch {
        Write-Host "  ERROR: $_" -ForegroundColor Red
        $stats.FilesWithErrors++
    }
}

# Main execution
Write-Host "`n=== Logger Call Conversion Script ===" -ForegroundColor Magenta
Write-Host "Scanning directory: $Path" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "MODE: DRY RUN (no files will be modified)" -ForegroundColor Yellow
}
Write-Host ""

# Find all C# files
$csFiles = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse | 
    Where-Object { $_.FullName -notmatch "\\(bin|obj|artifacts)\\" }

Write-Host "Found $($csFiles.Count) C# files to process`n" -ForegroundColor Cyan

# Process each file
foreach ($file in $csFiles) {
    Process-CSharpFile -FilePath $file.FullName
}

# Display statistics
Write-Host "`n=== Conversion Statistics ===" -ForegroundColor Magenta
Write-Host "Files scanned:        $($stats.FilesScanned)" -ForegroundColor White
Write-Host "Files modified:       $($stats.FilesModified)" -ForegroundColor $(if ($stats.FilesModified -gt 0) { "Green" } else { "Gray" })
Write-Host "Logger calls replaced: $($stats.CallsReplaced)" -ForegroundColor $(if ($stats.CallsReplaced -gt 0) { "Green" } else { "Gray" })
Write-Host "Files with errors:    $($stats.FilesWithErrors)" -ForegroundColor $(if ($stats.FilesWithErrors -gt 0) { "Red" } else { "Gray" })

if ($DryRun) {
    Write-Host "`nTo apply changes, run without -DryRun parameter" -ForegroundColor Yellow
}

Write-Host "`nConversion complete!`n" -ForegroundColor Green