# NuGet Package Publishing Script (PowerShell)
# Publishes all DotCompute v0.2.0-alpha packages to NuGet.org
#
# Requirements:
# - All packages must be signed
# - NuGet API key must be configured
# - dotnet SDK 9.0+ installed
#
# Setup NuGet API Key:
#   Set environment variable: $env:NUGET_API_KEY = "your-api-key-here"
#   Or use -ApiKey parameter when running this script

param(
    [string]$ApiKey = $env:NUGET_API_KEY,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [switch]$SkipDuplicate = $true,
    [switch]$NoSymbols = $true,  # Skip symbol packages by default (no debug symbols in release)
    [switch]$DryRun = $false
)

# Configuration
$NupkgDir = "nupkgs"
$PackageDir = Join-Path $PWD.Path $NupkgDir

Write-Host "========================================" -ForegroundColor Blue
Write-Host "  DotCompute v0.2.0-alpha Package Publishing" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue
Write-Host ""

# Check if nupkgs directory exists
if (-not (Test-Path $PackageDir)) {
    Write-Host "Error: Directory not found!" -ForegroundColor Red
    exit 1
}

# Warn if API key not set
if ([string]::IsNullOrEmpty($ApiKey) -and -not $DryRun) {
    Write-Host "Warning: API key not provided!" -ForegroundColor Yellow
    Write-Host "The command will use the API key stored in dotnet configuration." -ForegroundColor Yellow
    Write-Host "If not configured, the push will fail." -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "Press ENTER to continue, or Ctrl+C to cancel"
}

# Get all packages
$MainPackages = Get-ChildItem -Path $PackageDir -Filter "*.nupkg" -File
$SymbolPackages = @()

if (-not $NoSymbols) {
    $SymbolPackages = Get-ChildItem -Path $PackageDir -Filter "*.snupkg" -File
}

$TotalPackages = $MainPackages.Count + $SymbolPackages.Count

if ($TotalPackages -eq 0) {
    Write-Host "Error: No packages found!" -ForegroundColor Red
    exit 1
}

Write-Host "Found $TotalPackages package(s) to publish" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration:"
Write-Host "  NuGet Source: $Source"
Write-Host "  Skip Duplicate: $SkipDuplicate"
Write-Host "  Publish Symbols: $(-not $NoSymbols)"
Write-Host "  Dry Run: $DryRun"
Write-Host ""

# Final confirmation
if (-not $DryRun) {
    Write-Host "WARNING: This will publish packages to NuGet.org!" -ForegroundColor Yellow
    Write-Host "WARNING: Published packages cannot be deleted, only unlisted." -ForegroundColor Yellow
    Write-Host ""
    $confirmation = Read-Host "Type 'YES' to confirm publication"

    if ($confirmation -ne "YES") {
        Write-Host "Publication cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "Starting package publication..." -ForegroundColor Blue
Write-Host ""

# Publish packages
$PublishedCount = 0
$SkippedCount = 0
$FailedCount = 0

function Publish-Package {
    param($Package, $IsSymbol = $false)

    $PackageName = $Package.Name
    $PackageType = if ($IsSymbol) { "symbols" } else { "package" }
    Write-Host "Publishing $PackageType`: $PackageName" -ForegroundColor Blue

    if ($DryRun) {
        Write-Host "[DRY RUN] Would publish: $PackageName" -ForegroundColor Cyan
        return "success"
    }

    $PushArgs = @(
        "nuget", "push", $Package.FullName,
        "--source", $Source
    )

    if ($SkipDuplicate) {
        $PushArgs += "--skip-duplicate"
    }

    if (-not $IsSymbol -and $NoSymbols) {
        $PushArgs += "--no-symbols"
    }

    if (-not [string]::IsNullOrEmpty($ApiKey)) {
        $PushArgs += "--api-key", $ApiKey
    }

    try {
        $Output = & dotnet $PushArgs 2>&1
        $ExitCode = $LASTEXITCODE

        if ($ExitCode -eq 0) {
            if ($Output -match "already exists") {
                Write-Host "[SKIPPED] Already exists: $PackageName" -ForegroundColor Yellow
                return "skipped"
            } else {
                Write-Host "[OK] $PackageName" -ForegroundColor Green
                return "success"
            }
        } elseif ($ExitCode -eq 409 -or $Output -match "409") {
            Write-Host "[SKIPPED] Already exists: $PackageName" -ForegroundColor Yellow
            return "skipped"
        } else {
            Write-Host "[FAILED] $PackageName" -ForegroundColor Red
            Write-Host $Output -ForegroundColor DarkRed
            return "failed"
        }
    }
    catch {
        Write-Host "[FAILED] $PackageName" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor DarkRed
        return "failed"
    }
}

# Publish main packages
foreach ($Package in $MainPackages) {
    $Result = Publish-Package -Package $Package

    switch ($Result) {
        "success" { $PublishedCount++ }
        "skipped" { $SkippedCount++ }
        "failed" { $FailedCount++ }
    }
    Write-Host ""
}

# Publish symbol packages
if (-not $NoSymbols) {
    foreach ($Package in $SymbolPackages) {
        $Result = Publish-Package -Package $Package -IsSymbol $true

        switch ($Result) {
            "success" { $PublishedCount++ }
            "skipped" { $SkippedCount++ }
            "failed" { $FailedCount++ }
        }
        Write-Host ""
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Blue
Write-Host "  Publishing Summary" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue
Write-Host "Successfully published: $PublishedCount" -ForegroundColor Green
Write-Host "Skipped (duplicates): $SkippedCount" -ForegroundColor Yellow
Write-Host "Failed: $FailedCount" -ForegroundColor $(if ($FailedCount -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($FailedCount -eq 0) {
    Write-Host "Publication completed successfully!" -ForegroundColor Green

    if (-not $DryRun) {
        Write-Host ""
        Write-Host "View your packages at:"
        Write-Host "  https://www.nuget.org/profiles/YOUR_PROFILE"
        Write-Host ""
        Write-Host "It may take a few minutes for packages to appear in search."
    }
    exit 0
} else {
    Write-Host "Some packages failed to publish." -ForegroundColor Red
    exit 1
}
