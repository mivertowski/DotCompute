# Build script for DotCompute CI/CD Pipeline (Windows)
# Usage: .\build.ps1 [-Configuration Release] [-SkipTests $false]

param(
    [string]$Configuration = "Release",
    [bool]$SkipTests = $false
)

$ErrorActionPreference = "Stop"

$BuildNumber = $env:GITHUB_RUN_NUMBER ?? "local"
$BranchName = $env:GITHUB_REF_NAME ?? "local"

Write-Host "🚀 DotCompute Build Script" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Skip Tests: $SkipTests" -ForegroundColor Cyan
Write-Host "Build Number: $BuildNumber" -ForegroundColor Cyan
Write-Host "Branch: $BranchName" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Yellow

# Set up directories
New-Item -ItemType Directory -Force -Path "artifacts/packages" | Out-Null
New-Item -ItemType Directory -Force -Path "artifacts/coverage" | Out-Null
New-Item -ItemType Directory -Force -Path "artifacts/reports" | Out-Null

# Restore dependencies
Write-Host "📦 Restoring dependencies..." -ForegroundColor Blue
dotnet restore --locked-mode

# GitVersion (if available)
$gitVersionExists = Get-Command dotnet-gitversion -ErrorAction SilentlyContinue
if ($gitVersionExists) {
    Write-Host "🏷️  Determining version..." -ForegroundColor Blue
    $Version = dotnet gitversion /showVariable NuGetVersionV2
    $AssemblyVersion = dotnet gitversion /showVariable AssemblySemVer
    $FileVersion = dotnet gitversion /showVariable AssemblySemFileVer
    $InformationalVersion = dotnet gitversion /showVariable InformationalVersion
    
    Write-Host "Version: $Version" -ForegroundColor Green
    Write-Host "Assembly Version: $AssemblyVersion" -ForegroundColor Green
    Write-Host "File Version: $FileVersion" -ForegroundColor Green
    Write-Host "Informational Version: $InformationalVersion" -ForegroundColor Green
} else {
    Write-Host "⚠️  GitVersion not found, using default versioning" -ForegroundColor Yellow
    $Version = "0.1.0-local.$BuildNumber"
    $AssemblyVersion = "0.1.0"
    $FileVersion = "0.1.0.$BuildNumber"
    $InformationalVersion = "0.1.0-local.$BuildNumber+$BranchName"
}

# Build solution
Write-Host "🔨 Building solution..." -ForegroundColor Blue
dotnet build `
    --configuration $Configuration `
    --no-restore `
    --verbosity minimal `
    -p:Version="$Version" `
    -p:AssemblyVersion="$AssemblyVersion" `
    -p:FileVersion="$FileVersion" `
    -p:InformationalVersion="$InformationalVersion" `
    -p:TreatWarningsAsErrors=false `
    -p:ContinuousIntegrationBuild=true

# Run tests (if not skipped)
if (-not $SkipTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Blue
    dotnet test `
        --configuration $Configuration `
        --no-build `
        --verbosity minimal `
        --logger trx `
        --logger "console;verbosity=minimal" `
        --collect:"XPlat Code Coverage" `
        --results-directory ./artifacts/coverage/ `
        --settings coverlet.runsettings `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
        
    Write-Host "✅ Tests completed" -ForegroundColor Green
} else {
    Write-Host "⏭️  Tests skipped" -ForegroundColor Yellow
}

# Create NuGet packages
Write-Host "📦 Creating NuGet packages..." -ForegroundColor Blue
dotnet pack `
    --configuration $Configuration `
    --no-build `
    --verbosity minimal `
    --output ./artifacts/packages/ `
    -p:PackageVersion="$Version" `
    -p:AssemblyVersion="$AssemblyVersion" `
    -p:FileVersion="$FileVersion" `
    -p:InformationalVersion="$InformationalVersion"

# Display package information
Write-Host "📋 Created packages:" -ForegroundColor Blue
Get-ChildItem ./artifacts/packages/

$packageCount = (Get-ChildItem ./artifacts/packages/*.nupkg).Count

Write-Host "✅ Build completed successfully!" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "Packages: $packageCount" -ForegroundColor Green

if (-not $SkipTests) {
    Write-Host "Test Results: ./artifacts/coverage/" -ForegroundColor Green
}