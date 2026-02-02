#Requires -Version 5.1
<#
.SYNOPSIS
    Signs all NuGet packages in C:\temp\packages using Certum code signing certificate.

.DESCRIPTION
    Signs .nupkg and .snupkg files with the Certum code signing certificate
    and timestamps them using Certum's timestamp server.

.PARAMETER PackageDirectory
    Directory containing packages to sign. Default: C:\temp\packages

.EXAMPLE
    .\Sign-NuGetPackages.ps1
    .\Sign-NuGetPackages.ps1 -PackageDirectory "C:\mypackages"
#>

param(
    [string]$PackageDirectory = "C:\temp\packages"
)

$ErrorActionPreference = "Stop"

# Configuration
$CertFingerprint = "2A305DCC2250AAC86CCBA31A7C392E4AA2AB72EF852700851E3C03B9F615B45D"  # SHA-256
$CertThumbprint = "ED3E3B2EFFAEF7C818EF159F6712F0FC64F590E7"  # SHA-1
$TimestampServer = "http://time.certum.pl"
$RequiredCSP = "Microsoft Base Smart Card Crypto Provider"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DotCompute NuGet Package Signing" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verify certificate is available
Write-Host "Checking certificate..." -ForegroundColor Yellow
$cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $CertThumbprint }

if (-not $cert) {
    Write-Host "ERROR: Certificate not found in CurrentUser\My store!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure:"
    Write-Host "  1. Smart card is inserted"
    Write-Host "  2. Certificate is installed via proCertum CardManager"
    Write-Host "  3. Run: certutil -user -repairstore My $CertThumbprint"
    exit 1
}

Write-Host "Certificate: $($cert.Subject)" -ForegroundColor Green
Write-Host "Thumbprint:  $($cert.Thumbprint)" -ForegroundColor Gray
Write-Host "Valid until: $($cert.NotAfter)" -ForegroundColor Gray
Write-Host "Private Key: $($cert.HasPrivateKey)" -ForegroundColor $(if ($cert.HasPrivateKey) { "Green" } else { "Red" })

# Check CSP - must be Microsoft Base Smart Card Crypto Provider
$certDetails = certutil -user -store My $CertThumbprint 2>&1 | Out-String
if ($certDetails -match "Provider = (.+)") {
    $currentCSP = $Matches[1].Trim()
    Write-Host "Provider:    $currentCSP" -ForegroundColor $(if ($currentCSP -eq $RequiredCSP) { "Green" } else { "Yellow" })

    if ($currentCSP -ne $RequiredCSP) {
        Write-Host ""
        Write-Host "WARNING: Certificate is linked to '$currentCSP'" -ForegroundColor Yellow
        Write-Host "This CSP has known compatibility issues with NuGet signing." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Attempting to re-link to '$RequiredCSP'..." -ForegroundColor Cyan

        $repairResult = certutil -user -f -csp $RequiredCSP -repairstore My $CertThumbprint 2>&1
        if ($repairResult -match "Signature test passed") {
            Write-Host "SUCCESS: Certificate re-linked to $RequiredCSP" -ForegroundColor Green
        } else {
            Write-Host "ERROR: Failed to re-link certificate" -ForegroundColor Red
            Write-Host $repairResult
            exit 1
        }
    }
}
Write-Host ""

if (-not $cert.HasPrivateKey) {
    Write-Host "ERROR: Certificate does not have private key linked!" -ForegroundColor Red
    Write-Host "Run: certutil -user -f -csp '$RequiredCSP' -repairstore My $CertThumbprint"
    exit 1
}

# Find packages
if (-not (Test-Path $PackageDirectory)) {
    Write-Host "ERROR: Directory not found: $PackageDirectory" -ForegroundColor Red
    exit 1
}

$packages = Get-ChildItem -Path $PackageDirectory -Include "*.nupkg", "*.snupkg" -File
if (-not $packages -or $packages.Count -eq 0) {
    Write-Host "ERROR: No packages found in $PackageDirectory" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($packages.Count) package(s) to sign" -ForegroundColor Cyan
Write-Host "Timestamp server: $TimestampServer" -ForegroundColor Gray
Write-Host ""

# Check for NuGet CLI
$nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
if (-not $nuget) {
    Write-Host "ERROR: nuget.exe not found in PATH" -ForegroundColor Red
    Write-Host "Install via: choco install nuget.commandline"
    exit 1
}

Write-Host "Using NuGet: $($nuget.Source)" -ForegroundColor Gray
Write-Host ""

# Prompt for confirmation
Write-Host "Press ENTER to start signing, or Ctrl+C to cancel..." -ForegroundColor Yellow
Read-Host | Out-Null

Write-Host ""
Write-Host "Starting package signing..." -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($package in $packages) {
    Write-Host "Signing: $($package.Name)" -ForegroundColor Yellow

    try {
        $result = & nuget.exe sign $package.FullName `
            -CertificateFingerprint $CertFingerprint `
            -CertificateStoreName "My" `
            -CertificateStoreLocation "CurrentUser" `
            -Timestamper $TimestampServer `
            -Overwrite 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  SUCCESS" -ForegroundColor Green
            $successCount++
        }
        else {
            Write-Host "  FAILED: $result" -ForegroundColor Red
            $failCount++
        }
    }
    catch {
        Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Signing Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed:     $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Gray" } else { "Red" })
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "All packages signed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To publish to NuGet.org:" -ForegroundColor Cyan
    Write-Host "  dotnet nuget push $PackageDirectory\*.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY"
    exit 0
}
else {
    Write-Host "Some packages failed to sign." -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  - Smart card PIN not entered (check for popup)"
    Write-Host "  - crypto3 CSP compatibility issue (see KNOWN ISSUE in sign-packages.sh)"
    Write-Host "  - Certificate not properly linked to smart card key"
    exit 1
}
