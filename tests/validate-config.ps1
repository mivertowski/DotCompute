#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates the DotCompute test filtering configuration.

.DESCRIPTION
    This script validates that all test filtering components are properly configured:
    - Test categories are properly defined
    - MSBuild properties are correctly set
    - Test scripts are functional
    - Hardware detection works

.PARAMETER Quick
    Run only basic validation checks (skip hardware detection)

.EXAMPLE
    ./validate-config.ps1
    Run full validation

.EXAMPLE
    ./validate-config.ps1 -Quick
    Run basic validation only
#>

param(
    [Parameter(Mandatory=$false)]
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir

function Write-ValidationResult {
    param([string]$Test, [bool]$Passed, [string]$Details = "")
    $status = if ($Passed) { "✅ PASS" } else { "❌ FAIL" }
    Write-Host "$status - $Test" -ForegroundColor $(if ($Passed) { "Green" } else { "Red" })
    if ($Details) {
        Write-Host "  Details: $Details" -ForegroundColor Gray
    }
}

function Test-FileExists {
    param([string]$Path, [string]$Description)
    $exists = Test-Path $Path
    Write-ValidationResult $Description $exists $Path
    return $exists
}

Write-Host "🔍 DotCompute Test Configuration Validation" -ForegroundColor Cyan
Write-Host "=" * 50

# Test 1: Required files exist
Write-Host "`n📁 File Structure Validation" -ForegroundColor Yellow
$allFilesExist = $true
$requiredFiles = @(
    @{Path = "$RootDir/.github/workflows/tests.yml"; Desc = "GitHub Actions workflow"},
    @{Path = "$ScriptDir/test-categories.props"; Desc = "MSBuild test properties"},
    @{Path = "$ScriptDir/TestCategories.cs"; Desc = "Test category definitions"},
    @{Path = "$ScriptDir/run-tests.ps1"; Desc = "PowerShell test script"},
    @{Path = "$ScriptDir/run-tests.sh"; Desc = "Bash test script"},
    @{Path = "$RootDir/Directory.Build.props"; Desc = "Global build properties"}
)

foreach ($file in $requiredFiles) {
    $exists = Test-FileExists $file.Path $file.Desc
    $allFilesExist = $allFilesExist -and $exists
}

# Test 2: Test categories validation
Write-Host "`n🏷️  Test Categories Validation" -ForegroundColor Yellow
try {
    $categoryFile = Get-Content "$ScriptDir/TestCategories.cs" -Raw
    $expectedCategories = @("Unit", "Integration", "Performance", "Hardware", "CUDA", "OpenCL", "DirectCompute", "Metal", "Mock")
    $categoriesFound = 0
    
    foreach ($category in $expectedCategories) {
        if ($categoryFile -match "public const string $category") {
            $categoriesFound++
        } else {
            Write-ValidationResult "Category '$category' definition" $false "Not found in TestCategories.cs"
        }
    }
    
    $categoriesValid = $categoriesFound -eq $expectedCategories.Count
    Write-ValidationResult "All required test categories defined" $categoriesValid "$categoriesFound/$($expectedCategories.Count) categories found"
} catch {
    Write-ValidationResult "Test categories file readable" $false $_.Exception.Message
    $categoriesValid = $false
}

# Test 3: MSBuild properties validation
Write-Host "`n⚙️  MSBuild Properties Validation" -ForegroundColor Yellow
try {
    $propsFile = Get-Content "$ScriptDir/test-categories.props" -Raw
    $requiredProperties = @("DefaultUnitTestFilter", "CudaTestFilter", "UnitTestCoverageThreshold")
    $propsFound = 0
    
    foreach ($prop in $requiredProperties) {
        if ($propsFile -match $prop) {
            $propsFound++
        } else {
            Write-ValidationResult "Property '$prop'" $false "Not found in test-categories.props"
        }
    }
    
    $propsValid = $propsFound -eq $requiredProperties.Count
    Write-ValidationResult "Required MSBuild properties defined" $propsValid "$propsFound/$($requiredProperties.Count) properties found"
} catch {
    Write-ValidationResult "MSBuild properties file readable" $false $_.Exception.Message
    $propsValid = $false
}

# Test 4: Directory.Build.props integration
Write-Host "`n🔗 Directory.Build.props Integration" -ForegroundColor Yellow
try {
    $buildProps = Get-Content "$RootDir/Directory.Build.props" -Raw
    
    $hasTestProjectDetection = $buildProps -match "IsTestProject.*\.Tests"
    Write-ValidationResult "Test project detection" $hasTestProjectDetection
    
    $hasCoverageConfig = $buildProps -match "CoverageThreshold"
    Write-ValidationResult "Coverage threshold configuration" $hasCoverageConfig
    
    $hasTestDependencies = $buildProps -match "Microsoft\.NET\.Test\.Sdk"
    Write-ValidationResult "Test dependencies configuration" $hasTestDependencies
    
    $buildPropsValid = $hasTestProjectDetection -and $hasCoverageConfig -and $hasTestDependencies
} catch {
    Write-ValidationResult "Directory.Build.props readable" $false $_.Exception.Message
    $buildPropsValid = $false
}

# Test 5: GitHub Actions workflow validation
Write-Host "`n🚀 GitHub Actions Workflow Validation" -ForegroundColor Yellow
try {
    $workflow = Get-Content "$RootDir/.github/workflows/tests.yml" -Raw
    
    $hasUnitTests = $workflow -match "unit-tests:"
    Write-ValidationResult "Unit tests job defined" $hasUnitTests
    
    $hasMockTests = $workflow -match "mock-hardware-tests:"
    Write-ValidationResult "Mock hardware tests job defined" $hasMockTests
    
    $hasHardwareTests = $workflow -match "hardware-tests:"
    Write-ValidationResult "Hardware tests job defined" $hasHardwareTests
    
    $hasCodecov = $workflow -match "codecov"
    Write-ValidationResult "Codecov integration configured" $hasCodecov
    
    $workflowValid = $hasUnitTests -and $hasMockTests -and $hasHardwareTests -and $hasCodecov
} catch {
    Write-ValidationResult "GitHub Actions workflow readable" $false $_.Exception.Message
    $workflowValid = $false
}

# Test 6: Test scripts validation
Write-Host "`n📜 Test Scripts Validation" -ForegroundColor Yellow
try {
    $psScript = Test-Path "$ScriptDir/run-tests.ps1"
    Write-ValidationResult "PowerShell test script exists" $psScript
    
    $bashScript = Test-Path "$ScriptDir/run-tests.sh"
    Write-ValidationResult "Bash test script exists" $bashScript
    
    if ($bashScript) {
        $bashExecutable = (Get-Item "$ScriptDir/run-tests.sh").UnixFileMode -match "x"
        Write-ValidationResult "Bash script is executable" $bashExecutable
    }
    
    $scriptsValid = $psScript -and $bashScript
} catch {
    Write-ValidationResult "Test scripts validation" $false $_.Exception.Message
    $scriptsValid = $false
}

# Test 7: Hardware detection (if not quick mode)
$hardwareValid = $true
if (-not $Quick) {
    Write-Host "`n🖥️  Hardware Detection Validation" -ForegroundColor Yellow
    
    # Test CUDA detection
    try {
        $cudaAvailable = $false
        if (Get-Command "nvidia-smi" -ErrorAction SilentlyContinue) {
            $nvidiaOutput = nvidia-smi --query-gpu=name --format=csv,noheader,nounits 2>$null
            $cudaAvailable = $LASTEXITCODE -eq 0
        }
        Write-ValidationResult "CUDA hardware detection" $true "Available: $cudaAvailable"
    } catch {
        Write-ValidationResult "CUDA detection script" $false $_.Exception.Message
        $hardwareValid = $false
    }
    
    # Test OpenCL detection
    try {
        $openclAvailable = $false
        if (Get-Command "clinfo" -ErrorAction SilentlyContinue) {
            clinfo -l >$null 2>&1
            $openclAvailable = $LASTEXITCODE -eq 0
        }
        Write-ValidationResult "OpenCL hardware detection" $true "Available: $openclAvailable"
    } catch {
        Write-ValidationResult "OpenCL detection script" $false $_.Exception.Message
        $hardwareValid = $false
    }
    
    # Test DirectCompute detection (Windows check)
    $directComputeAvailable = $IsWindows
    Write-ValidationResult "DirectCompute hardware detection" $true "Available: $directComputeAvailable"
    
    # Test Metal detection (macOS check)
    $metalAvailable = $IsMacOS
    Write-ValidationResult "Metal hardware detection" $true "Available: $metalAvailable"
}

# Test 8: .NET SDK and project validation
Write-Host "`n🔧 .NET SDK and Project Validation" -ForegroundColor Yellow
try {
    Push-Location $RootDir
    
    # Check .NET SDK version
    $dotnetVersion = dotnet --version 2>$null
    $dotnetAvailable = $LASTEXITCODE -eq 0
    Write-ValidationResult ".NET SDK available" $dotnetAvailable "Version: $dotnetVersion"
    
    if ($dotnetAvailable) {
        # Test restore
        dotnet restore >$null 2>&1
        $restoreSucceeded = $LASTEXITCODE -eq 0
        Write-ValidationResult "dotnet restore succeeds" $restoreSucceeded
        
        # Test build
        if ($restoreSucceeded) {
            dotnet build --no-restore --configuration Debug >$null 2>&1
            $buildSucceeded = $LASTEXITCODE -eq 0
            Write-ValidationResult "dotnet build succeeds" $buildSucceeded
        } else {
            Write-ValidationResult "dotnet build succeeds" $false "Restore failed"
            $buildSucceeded = $false
        }
        
        $dotnetValid = $restoreSucceeded -and $buildSucceeded
    } else {
        $dotnetValid = $false
    }
} catch {
    Write-ValidationResult ".NET validation" $false $_.Exception.Message
    $dotnetValid = $false
} finally {
    Pop-Location
}

# Summary
Write-Host "`n📊 Validation Summary" -ForegroundColor Cyan
Write-Host "=" * 30

$overallValid = $allFilesExist -and $categoriesValid -and $propsValid -and $buildPropsValid -and $workflowValid -and $scriptsValid -and $hardwareValid -and $dotnetValid

$validationResults = @(
    @{Name = "File Structure"; Valid = $allFilesExist},
    @{Name = "Test Categories"; Valid = $categoriesValid},
    @{Name = "MSBuild Properties"; Valid = $propsValid},
    @{Name = "Directory.Build.props"; Valid = $buildPropsValid},
    @{Name = "GitHub Workflow"; Valid = $workflowValid},
    @{Name = "Test Scripts"; Valid = $scriptsValid},
    @{Name = "Hardware Detection"; Valid = $hardwareValid},
    @{Name = ".NET SDK/Build"; Valid = $dotnetValid}
)

foreach ($result in $validationResults) {
    $status = if ($result.Valid) { "✅" } else { "❌" }
    Write-Host "$status $($result.Name)" -ForegroundColor $(if ($result.Valid) { "Green" } else { "Red" })
}

Write-Host "`nOverall Status: " -NoNewline
if ($overallValid) {
    Write-Host "✅ ALL VALIDATIONS PASSED" -ForegroundColor Green
    Write-Host "`n🎉 Your DotCompute test filtering configuration is ready!" -ForegroundColor Green
    Write-Host "You can now run tests using the provided scripts or GitHub Actions workflows." -ForegroundColor Gray
} else {
    Write-Host "❌ SOME VALIDATIONS FAILED" -ForegroundColor Red
    Write-Host "`n⚠️  Please fix the failed validations before using the test system." -ForegroundColor Yellow
}

if ($Quick) {
    Write-Host "`nℹ️  Hardware detection was skipped (--Quick mode). Run without --Quick for full validation." -ForegroundColor Blue
}

exit $(if ($overallValid) { 0 } else { 1 })