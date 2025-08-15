# PowerShell script for running different test suites
param(
    [Parameter(Position=0)]
    [ValidateSet("quick", "integration", "hardware", "performance", "all", "ci")]
    [string]$TestMode = "quick",
    
    [switch]$NoBuild,
    [switch]$Verbose,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Running DotCompute Tests - Mode: $TestMode" -ForegroundColor Cyan

# Build if not skipped
if (-not $NoBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# Set verbosity
$verbosity = if ($Verbose) { "detailed" } else { "minimal" }

# Define test filters
$filters = @{
    "quick" = "Category=Unit|Category=Mock|Category=CI"
    "integration" = "Category=Integration&Category!=Hardware&Category!=Stress"
    "hardware" = "Category=Hardware|Category=CudaRequired|Category=RTX2000|Category=OpenCLRequired"
    "performance" = "Category=Performance|Category=Benchmark|Category=Stress"
    "ci" = "Category!=Hardware&Category!=Stress&Category!=Performance&Category!=HardwareRequired&Category!=CudaRequired"
    "all" = ""
}

$filter = $filters[$TestMode]
$filterArg = if ($filter) { "--filter", "`"$filter`"" } else { @() }

# Run tests
Write-Host "Executing tests with filter: $filter" -ForegroundColor Green

$testArgs = @(
    "test",
    "--no-build",
    "--configuration", $Configuration,
    "--logger", "console;verbosity=$verbosity",
    "--logger", "trx;LogFilePrefix=$TestMode",
    "--results-directory", "./TestResults",
    "--collect:`"XPlat Code Coverage`"",
    "--parallel"
) + $filterArg

if ($TestMode -eq "quick") {
    $testArgs += "--blame-hang-timeout", "60s"
} elseif ($TestMode -eq "hardware") {
    $testArgs += "--blame-hang-timeout", "300s"
} else {
    $testArgs += "--blame-hang-timeout", "120s"
}

# Add parallel execution settings for quick tests
if ($TestMode -eq "quick" -or $TestMode -eq "ci") {
    $testArgs += "-p:ParallelizeTestCollections=true"
    $testArgs += "/p:ConcurrentBuild=true"
}

& dotnet $testArgs

$exitCode = $LASTEXITCODE

# Generate summary
if (Test-Path "./TestResults") {
    $trxFiles = Get-ChildItem -Path "./TestResults" -Filter "*.trx" -Recurse
    Write-Host "`nTest Results Summary:" -ForegroundColor Cyan
    foreach ($trx in $trxFiles) {
        Write-Host "  - $($trx.Name)" -ForegroundColor Gray
    }
}

# Check for coverage files
$coverageFiles = Get-ChildItem -Path "./TestResults" -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue
if ($coverageFiles) {
    Write-Host "`nCode Coverage files generated: $($coverageFiles.Count)" -ForegroundColor Green
}

exit $exitCode