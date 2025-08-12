#!/usr/bin/env pwsh
# Bulk C# Code Fixer Script
# Applies common fixes to all .cs files in the project

param(
    [string]$Path = ".",
    [switch]$WhatIf = $false,
    [switch]$Verbose = $false
)

# Colors for output
$Red = "`e[31m"
$Green = "`e[32m"
$Yellow = "`e[33m"
$Blue = "`e[34m"
$Reset = "`e[0m"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = $Reset)
    Write-Host "$Color$Message$Reset"
}

function Write-Progress {
    param([string]$Message)
    Write-ColorOutput "🔄 $Message" $Blue
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✅ $Message" $Green
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠️  $Message" $Yellow
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "❌ $Message" $Red
}

# Statistics
$script:FilesProcessed = 0
$script:TotalFixes = 0
$script:FixTypes = @{
    "CA1852" = 0  # Sealed internal classes
    "CA1822" = 0  # Static methods
    "CS1503" = 0  # Logger type fixes
    "CS1998" = 0  # Async Task.CompletedTask
    "CS1061" = 0  # Missing method/property fixes
    "Other" = 0   # Other fixes
}

# Get all C# files
Write-Progress "Finding C# files in $Path..."
$csFiles = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse | Where-Object { 
    -not $_.FullName.Contains("bin") -and 
    -not $_.FullName.Contains("obj") -and
    -not $_.FullName.Contains(".g.cs") -and
    -not $_.FullName.Contains("AssemblyInfo.cs")
}

Write-ColorOutput "Found $($csFiles.Count) C# files to process." $Blue
Write-Host ""

function Apply-CA1852-Fix {
    param([string]$Content, [string]$FilePath)
    
    $fixes = 0
    # Fix internal classes that should be sealed
    $pattern = '(?m)^(\s*)(internal\s+class\s+\w+(?:\([^)]*\))?(?:\s*:\s*[^{]*)?)\s*$'
    $matches = [regex]::Matches($Content, $pattern)
    
    foreach ($match in $matches) {
        $indent = $match.Groups[1].Value
        $classDeclaration = $match.Groups[2].Value
        
        # Skip if already sealed or abstract
        if ($classDeclaration -match '\b(sealed|abstract)\b') {
            continue
        }
        
        # Skip if it's a base class (heuristic: has 'Base' in name or is inherited from)
        if ($classDeclaration -match '\bBase\b' -or $Content -match ":\s*$($classDeclaration -replace '.*class\s+(\w+).*', '$1')\b") {
            continue
        }
        
        $newDeclaration = $classDeclaration -replace '\binternal\s+class\b', 'internal sealed class'
        $Content = $Content -replace [regex]::Escape($match.Value), "$indent$newDeclaration"
        $fixes++
        
        if ($Verbose) {
            Write-ColorOutput "  CA1852: Sealed internal class in $FilePath" $Yellow
        }
    }
    
    $script:FixTypes["CA1852"] += $fixes
    return $Content
}

function Apply-CA1822-Fix {
    param([string]$Content, [string]$FilePath)
    
    $fixes = 0
    $lines = $Content -split "`n"
    $newLines = @()
    $inMethod = $false
    $methodIndent = ""
    $methodStartLine = 0
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Detect method start (simplified pattern)
        if ($line -match '^\s*(private|protected|internal|public)?\s*(static\s+)?.*\s+\w+\s*\([^)]*\)\s*(\{|$)' -and 
            $line -notmatch '\b(abstract|virtual|override|interface|delegate|event|property)\b' -and
            $line -notmatch '^\s*//' -and
            -not $line.Contains("static")) {
            
            $inMethod = $true
            $methodIndent = ($line -replace '^(\s*).*', '$1')
            $methodStartLine = $i
            $methodContent = ""
        }
        
        # Collect method content
        if ($inMethod) {
            $methodContent += $line + "`n"
            
            # End of method detection
            if ($line.Trim() -eq "}" -and $line.StartsWith($methodIndent)) {
                $inMethod = $false
                
                # Check if method uses 'this' or instance members
                if (-not ($methodContent -match '\bthis\b' -or 
                         $methodContent -match '\b_\w+\b' -or
                         $methodContent -match '\b[A-Z]\w*\(' -or
                         $methodContent -match '\bprotected\b' -or
                         $methodContent -match '\bvirtual\b' -or
                         $methodContent -match '\boverride\b')) {
                    
                    # Make method static
                    $methodFirstLine = $lines[$methodStartLine]
                    if ($methodFirstLine -notmatch '\bstatic\b') {
                        $lines[$methodStartLine] = $methodFirstLine -replace '(\s*(private|protected|internal|public)?\s*)', '$1static '
                        $fixes++
                        
                        if ($Verbose) {
                            Write-ColorOutput "  CA1822: Made method static in $FilePath at line $($methodStartLine + 1)" $Yellow
                        }
                    }
                }
            }
        }
        
        $newLines += $line
    }
    
    $script:FixTypes["CA1822"] += $fixes
    return ($newLines -join "`n")
}

function Apply-CS1503-Fix {
    param([string]$Content, [string]$FilePath)
    
    $fixes = 0
    
    # Fix logger type mismatches - generic logger to specific type
    $patterns = @(
        @{
            # ILogger<T> constructor parameter
            Pattern = '(ILogger<)([^>]+)(>.*?logger)'
            Replacement = { param($match) 
                $className = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
                return "$($match.Groups[1].Value)$className$($match.Groups[3].Value)"
            }
        },
        @{
            # ILogger field/property declarations
            Pattern = 'ILogger<[^>]+>\s+_logger'
            Replacement = { param($match)
                $className = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
                return "ILogger<$className> _logger"
            }
        }
    )
    
    foreach ($patternInfo in $patterns) {
        $matches = [regex]::Matches($Content, $patternInfo.Pattern)
        foreach ($match in $matches) {
            $replacement = & $patternInfo.Replacement $match
            $Content = $Content -replace [regex]::Escape($match.Value), $replacement
            $fixes++
            
            if ($Verbose) {
                Write-ColorOutput "  CS1503: Fixed logger type mismatch in $FilePath" $Yellow
            }
        }
    }
    
    $script:FixTypes["CS1503"] += $fixes
    return $Content
}

function Apply-CS1998-Fix {
    param([string]$Content, [string]$FilePath)
    
    $fixes = 0
    
    # Find async methods that don't use await
    $asyncMethods = [regex]::Matches($Content, '(?ms)(public|private|protected|internal)?\s*async\s+Task[^{]*\{[^}]*\}')
    
    foreach ($match in $asyncMethods) {
        $methodContent = $match.Value
        
        # Check if method contains await
        if ($methodContent -notmatch '\bawait\b' -and $methodContent -match '\{\s*(//[^\r\n]*)?\s*\}') {
            # Add await Task.CompletedTask to empty async methods
            $newMethodContent = $methodContent -replace '\{\s*(//[^\r\n]*)?\s*\}', '{ await Task.CompletedTask; }'
            $Content = $Content -replace [regex]::Escape($match.Value), $newMethodContent
            $fixes++
            
            if ($Verbose) {
                Write-ColorOutput "  CS1998: Added Task.CompletedTask to async method in $FilePath" $Yellow
            }
        }
        elseif ($methodContent -notmatch '\bawait\b' -and $methodContent -match '\{[^}]+\}') {
            # For non-empty methods without await, add return Task.CompletedTask at the end
            $newMethodContent = $methodContent -replace '(\s*)(\}\s*)$', "`$1return Task.CompletedTask;`n`$1`$2"
            if ($newMethodContent -ne $methodContent) {
                $Content = $Content -replace [regex]::Escape($match.Value), $newMethodContent
                $fixes++
                
                if ($Verbose) {
                    Write-ColorOutput "  CS1998: Added Task.CompletedTask return to async method in $FilePath" $Yellow
                }
            }
        }
    }
    
    $script:FixTypes["CS1998"] += $fixes
    return $Content
}

function Apply-CS1061-Fix {
    param([string]$Content, [string]$FilePath)
    
    $fixes = 0
    
    # Common missing method/property fixes
    $fixes_map = @{
        # Fix common property/method name issues
        '\.ConfigureAwait\(false\)' = '.ConfigureAwait(false)'
        '\.GetAwaiter\(\)\.GetResult\(\)' = '.GetAwaiter().GetResult()'
        
        # Fix collection initialization
        '\[\]' = '[]'
        'new\(\)' = 'new()'
        
        # Fix string comparisons
        '\.Equals\([^,)]+\)' = { param($match) $match.Value + ", StringComparison.Ordinal" }
    }
    
    foreach ($pattern in $fixes_map.Keys) {
        $replacement = $fixes_map[$pattern]
        if ($replacement -is [ScriptBlock]) {
            $matches = [regex]::Matches($Content, $pattern)
            foreach ($match in $matches) {
                $newValue = & $replacement $match
                $Content = $Content -replace [regex]::Escape($match.Value), $newValue
                $fixes++
            }
        } else {
            $originalContent = $Content
            $Content = $Content -replace $pattern, $replacement
            if ($Content -ne $originalContent) {
                $fixes++
            }
        }
    }
    
    if ($fixes -gt 0 -and $Verbose) {
        Write-ColorOutput "  CS1061: Fixed $fixes missing method/property issues in $FilePath" $Yellow
    }
    
    $script:FixTypes["CS1061"] += $fixes
    return $Content
}

function Apply-Additional-Fixes {
    param([string]$Content, [string]$FilePath)
    
    $fixes = 0
    
    # Remove unnecessary usings (basic cleanup)
    $unnecessaryUsings = @(
        'using System.Linq;'  # Only if no LINQ usage found
    )
    
    foreach ($using in $unnecessaryUsings) {
        if ($Content.Contains($using) -and $Content -notmatch '\.(Where|Select|First|Any|Count|OrderBy|GroupBy)\(') {
            $Content = $Content -replace [regex]::Escape($using), ''
            $fixes++
        }
    }
    
    # Fix spacing issues
    $Content = $Content -replace '\s+$', '' # Remove trailing whitespace
    $Content = $Content -replace '(?m)^\s*$\r?\n', '' # Remove empty lines
    
    # Ensure file ends with newline
    if (-not $Content.EndsWith("`n")) {
        $Content += "`n"
    }
    
    $script:FixTypes["Other"] += $fixes
    return $Content
}

function Process-File {
    param([System.IO.FileInfo]$File)
    
    try {
        Write-Progress "Processing $($File.Name)..."
        
        $originalContent = Get-Content -Path $File.FullName -Raw -Encoding UTF8
        if (-not $originalContent) {
            Write-Warning "Skipping empty file: $($File.Name)"
            return
        }
        
        $content = $originalContent
        
        # Apply all fixes
        $content = Apply-CA1852-Fix $content $File.FullName
        $content = Apply-CA1822-Fix $content $File.FullName
        $content = Apply-CS1503-Fix $content $File.FullName
        $content = Apply-CS1998-Fix $content $File.FullName
        $content = Apply-CS1061-Fix $content $File.FullName
        $content = Apply-Additional-Fixes $content $File.FullName
        
        # Write file if changed
        if ($content -ne $originalContent) {
            if (-not $WhatIf) {
                Set-Content -Path $File.FullName -Value $content -Encoding UTF8 -NoNewline
                Write-Success "Fixed $($File.Name)"
            } else {
                Write-ColorOutput "Would fix $($File.Name)" $Yellow
            }
            $script:TotalFixes++
        } else {
            if ($Verbose) {
                Write-ColorOutput "No changes needed for $($File.Name)" $Green
            }
        }
        
        $script:FilesProcessed++
    }
    catch {
        Write-Error "Error processing $($File.Name): $_"
    }
}

# Main processing
Write-Progress "Starting bulk fixes..."
Write-Host ""

foreach ($file in $csFiles) {
    Process-File $file
}

# Summary
Write-Host ""
Write-ColorOutput "===== BULK FIX SUMMARY =====" $Blue
Write-Host "Files processed: $script:FilesProcessed"
Write-Host "Total files with fixes: $script:TotalFixes"
Write-Host ""
Write-ColorOutput "Fix breakdown:" $Blue
foreach ($fixType in $script:FixTypes.Keys) {
    $count = $script:FixTypes[$fixType]
    if ($count -gt 0) {
        Write-Host "  $fixType`: $count fixes"
    }
}

if ($WhatIf) {
    Write-Host ""
    Write-ColorOutput "This was a dry run. Use -WhatIf:`$false to actually apply fixes." $Yellow
}

Write-Host ""
Write-Success "Bulk fix operation completed!"