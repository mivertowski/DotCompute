#!/usr/bin/env pwsh
<#
.SYNOPSIS
    PowerShell script to run DotCompute tests with filtering and coverage reporting.

.DESCRIPTION
    This script provides comprehensive test execution with support for:
    - Test category filtering
    - Coverage generation and reporting
    - Hardware capability detection
    - Report formatting and output

.PARAMETER Category
    Test category to run (Unit, Integration, Performance, Hardware, Mock, All)

.PARAMETER Configuration
    Build configuration (Debug, Release). Default: Debug

.PARAMETER Coverage
    Generate code coverage reports. Default: true

.PARAMETER Hardware
    Specific hardware backend to test (CUDA, OpenCL, DirectCompute, Metal)

.PARAMETER Parallel
    Run tests in parallel. Default: true

.PARAMETER Timeout
    Test timeout in seconds. Default: 300

.PARAMETER OutputFormat
    Test output format (trx, xml, json). Default: trx

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    ./run-tests.ps1 -Category Unit
    Run only unit tests

.EXAMPLE
    ./run-tests.ps1 -Category Hardware -Hardware CUDA
    Run CUDA hardware tests

.EXAMPLE
    ./run-tests.ps1 -Category All -Coverage $false
    Run all tests without coverage
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Unit", "Integration", "Performance", "Hardware", "Mock", "All")]
    [string]$Category = "Unit",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory=$false)]
    [bool]$Coverage = $true,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("CUDA", "OpenCL", "DirectCompute", "Metal")]
    [string]$Hardware = "",
    
    [Parameter(Mandatory=$false)]
    [bool]$Parallel = $true,
    
    [Parameter(Mandatory=$false)]
    [int]$Timeout = 300,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("trx", "xml", "json")]
    [string]$OutputFormat = "trx",
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Script configuration
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$TestResultsDir = Join-Path $RootDir "TestResults"
$CoverageDir = Join-Path $TestResultsDir "Coverage"

# Ensure directories exist
New-Item -ItemType Directory -Force -Path $TestResultsDir | Out-Null
New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null

# Logging function
function Write-Log {
    param([string]$Message, [string]$Level = "Info")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "Error" { "Red" }
        "Warning" { "Yellow" }
        "Success" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# Hardware detection functions
function Test-CudaAvailability {
    try {
        $null = Get-Command "nvidia-smi" -ErrorAction Stop
        $result = nvidia-smi --query-gpu=name --format=csv,noheader,nounits 2>$null
        return $LASTEXITCODE -eq 0 -and $result
    } catch {
        return $false
    }
}

function Test-OpenCLAvailability {
    try {
        $null = Get-Command "clinfo" -ErrorAction Stop
        $result = clinfo -l 2>$null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Test-DirectComputeAvailability {
    return $IsWindows
}

function Test-MetalAvailability {
    return $IsMacOS
}

# Get test filter based on category
function Get-TestFilter {
    param([string]$Category, [string]$Hardware)
    
    switch ($Category.ToLower()) {
        "unit" { 
            return "Category!=Hardware&Category!=GPU&Category!=CUDA&Category!=OpenCL&Category!=DirectCompute&Category!=Metal&Category!=Performance"
        }
        "integration" { 
            return "Category=Integration&Category!=Hardware"
        }
        "performance" { 
            return "Category=Performance&Category!=Hardware"
        }
        "mock" { 
            return "Category=Mock"
        }
        "hardware" { 
            if ($Hardware) {
                return "Category=$Hardware|Category=GPU"
            } else {
                return "Category=Hardware|Category=GPU|Category=CUDA|Category=OpenCL|Category=DirectCompute|Category=Metal"
            }
        }
        "all" { 
            return ""
        }
        default { 
            throw "Unknown category: $Category"
        }
    }
}

# Validate hardware availability
function Test-HardwareRequirements {
    param([string]$Category, [string]$Hardware)
    
    if ($Category -ne "Hardware" -and -not $Hardware) {
        return $true
    }
    
    $hardwareToTest = if ($Hardware) { @($Hardware) } else { @("CUDA", "OpenCL", "DirectCompute", "Metal") }
    $available = @()
    
    foreach ($hw in $hardwareToTest) {
        $isAvailable = switch ($hw) {
            "CUDA" { Test-CudaAvailability }
            "OpenCL" { Test-OpenCLAvailability }
            "DirectCompute" { Test-DirectComputeAvailability }
            "Metal" { Test-MetalAvailability }
            default { $false }
        }
        
        if ($isAvailable) {
            $available += $hw
            Write-Log "$hw hardware detected" "Success"
        } else {
            Write-Log "$hw hardware not available" "Warning"
        }
    }
    
    if ($Hardware -and $Hardware -notin $available) {
        Write-Log "Requested hardware '$Hardware' is not available" "Error"
        return $false
    }
    
    if ($Category -eq "Hardware" -and $available.Count -eq 0) {
        Write-Log "No hardware backends available for hardware tests" "Warning"
        return $false
    }
    
    return $true
}

# Main execution
try {
    Write-Log "Starting DotCompute test execution" "Info"
    Write-Log "Category: $Category, Configuration: $Configuration, Coverage: $Coverage" "Info"
    
    if ($Hardware) {
        Write-Log "Hardware: $Hardware" "Info"
    }
    
    # Validate hardware requirements
    if (-not (Test-HardwareRequirements -Category $Category -Hardware $Hardware)) {
        Write-Log "Hardware requirements not met. Exiting." "Error"
        exit 1
    }
    
    # Build the solution
    Write-Log "Building solution..." "Info"
    Push-Location $RootDir
    
    try {
        dotnet restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed"
        }
        
        dotnet build --no-restore --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed"
        }
        
        Write-Log "Build completed successfully" "Success"
    } finally {
        Pop-Location
    }
    
    # Prepare test command
    $testFilter = Get-TestFilter -Category $Category -Hardware $Hardware
    $testCommand = @(
        "test",
        "--no-build",
        "--configuration", $Configuration,
        "--logger", "$OutputFormat;LogFileName=TestResults.$OutputFormat",
        "--results-directory", $TestResultsDir
    )
    
    if ($testFilter) {
        $testCommand += @("--filter", $testFilter)
    }
    
    if ($Coverage) {
        $testCommand += @(
            "--collect:XPlat Code Coverage",
            "--",
            "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover"
        )
    }
    
    if ($Parallel) {
        $testCommand += @("--parallel")
    }
    
    # Set timeout
    $env:VSTEST_HOST_DEBUG = "0"
    $env:DOTNET_CLI_UI_LANGUAGE = "en"
    
    Write-Log "Running tests with filter: '$testFilter'" "Info"
    Write-Log "Test command: dotnet $($testCommand -join ' ')" "Info"
    
    # Execute tests
    Push-Location $RootDir
    try {
        $testProcess = Start-Process -FilePath "dotnet" -ArgumentList $testCommand -Wait -PassThru -NoNewWindow
        $exitCode = $testProcess.ExitCode
    } finally {
        Pop-Location
    }
    
    # Process results
    if ($exitCode -eq 0) {
        Write-Log "All tests passed!" "Success"
    } else {
        Write-Log "Some tests failed (exit code: $exitCode)" "Warning"
    }
    
    # Generate coverage report if requested
    if ($Coverage) {
        Write-Log "Generating coverage report..." "Info"
        
        $coverageFiles = Get-ChildItem -Path $TestResultsDir -Filter "*.opencover.xml" -Recurse
        if ($coverageFiles.Count -gt 0) {
            try {
                # Install reportgenerator if not available
                if (-not (Get-Command "reportgenerator" -ErrorAction SilentlyContinue)) {
                    Write-Log "Installing ReportGenerator tool..." "Info"
                    dotnet tool install --global dotnet-reportgenerator-globaltool
                }
                
                # Generate HTML report
                $coverageInput = ($coverageFiles.FullName -join ";")
                reportgenerator -reports:$coverageInput -targetdir:$CoverageDir -reporttypes:"Html;Cobertura;TextSummary"
                
                Write-Log "Coverage report generated in: $CoverageDir" "Success"
                
                # Display coverage summary
                $summaryFile = Join-Path $CoverageDir "Summary.txt"
                if (Test-Path $summaryFile) {
                    Write-Log "Coverage Summary:" "Info"
                    Get-Content $summaryFile | Write-Host
                }
            } catch {
                Write-Log "Failed to generate coverage report: $_" "Warning"
            }
        } else {
            Write-Log "No coverage files found" "Warning"
        }
    }
    
    # Display test results summary
    $trxFiles = Get-ChildItem -Path $TestResultsDir -Filter "*.trx" -Recurse
    if ($trxFiles.Count -gt 0) {
        Write-Log "Test result files:" "Info"
        foreach ($trxFile in $trxFiles) {
            Write-Log "  - $($trxFile.FullName)" "Info"
        }
    }
    
    Write-Log "Test execution completed" "Success"
    exit $exitCode
    
} catch {
    Write-Log "Error during test execution: $_" "Error"
    Write-Log $_.ScriptStackTrace "Error"
    exit 1
}