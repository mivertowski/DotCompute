#Requires -Version 5.1
<#
.SYNOPSIS
    Signs Windows executables, DLLs, and installers using Certum code signing certificate.

.DESCRIPTION
    Signs Windows binaries (EXE, DLL, MSI, MSIX, APPX) with the Certum code signing
    certificate stored on the smart card. No .pfx file needed - SignTool accesses
    the certificate directly from the Windows store.

.PARAMETER Path
    Path to file(s) to sign. Supports wildcards.

.PARAMETER Recursive
    Sign files recursively in subdirectories.

.EXAMPLE
    .\Sign-WindowsApp.ps1 -Path "C:\MyApp\bin\Release\*.exe"
    .\Sign-WindowsApp.ps1 -Path "C:\MyApp\bin\Release" -Recursive
    .\Sign-WindowsApp.ps1 -Path "C:\Installers\MyApp.msi"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Path,

    [switch]$Recursive
)

$ErrorActionPreference = "Stop"

# Configuration
$CertThumbprint = "ED3E3B2EFFAEF7C818EF159F6712F0FC64F590E7"
$TimestampServer = "http://time.certum.pl"
$DigestAlgorithm = "SHA256"
$RequiredCSP = "Microsoft Base Smart Card Crypto Provider"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Windows Application Signing" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find SignTool
$signtoolPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe"
    "$env:ProgramFiles\Windows Kits\10\bin\*\x64\signtool.exe"
)

$signtool = $null
foreach ($pattern in $signtoolPaths) {
    $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue |
             Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
             Select-Object -First 1
    if ($found) {
        $signtool = $found.FullName
        break
    }
}

if (-not $signtool) {
    Write-Host "ERROR: SignTool not found!" -ForegroundColor Red
    Write-Host "Install Windows SDK from: https://developer.microsoft.com/windows/downloads/windows-sdk/"
    exit 1
}

Write-Host "SignTool: $signtool" -ForegroundColor Gray

# Verify certificate
Write-Host "Checking certificate..." -ForegroundColor Yellow
$cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $CertThumbprint }

if (-not $cert) {
    Write-Host "ERROR: Certificate not found!" -ForegroundColor Red
    Write-Host "Ensure smart card is inserted and certificate is installed."
    exit 1
}

Write-Host "Certificate: $($cert.Subject)" -ForegroundColor Green
Write-Host "Valid until: $($cert.NotAfter)" -ForegroundColor Gray

# Check CSP - must be Microsoft Base Smart Card Crypto Provider
$certDetails = certutil -user -store My $CertThumbprint 2>&1 | Out-String
if ($certDetails -match "Provider = (.+)") {
    $currentCSP = $Matches[1].Trim()
    Write-Host "Provider:    $currentCSP" -ForegroundColor $(if ($currentCSP -eq $RequiredCSP) { "Green" } else { "Yellow" })

    if ($currentCSP -ne $RequiredCSP) {
        Write-Host ""
        Write-Host "WARNING: Certificate is linked to '$currentCSP'" -ForegroundColor Yellow
        Write-Host "Attempting to re-link to '$RequiredCSP'..." -ForegroundColor Cyan

        $repairResult = certutil -user -f -csp $RequiredCSP -repairstore My $CertThumbprint 2>&1
        if ($repairResult -match "Signature test passed") {
            Write-Host "SUCCESS: Certificate re-linked" -ForegroundColor Green
        } else {
            Write-Host "ERROR: Failed to re-link certificate" -ForegroundColor Red
            exit 1
        }
    }
}
Write-Host ""

# Find files to sign
$extensions = @("*.exe", "*.dll", "*.msi", "*.msix", "*.appx", "*.appxbundle", "*.msixbundle")

if (Test-Path $Path -PathType Container) {
    # Directory - find all signable files
    $searchParams = @{
        Path = $Path
        Include = $extensions
        File = $true
    }
    if ($Recursive) {
        $searchParams.Recurse = $true
    }
    $files = Get-ChildItem @searchParams
}
elseif (Test-Path $Path) {
    # Single file or wildcard
    $files = Get-ChildItem -Path $Path -File -ErrorAction SilentlyContinue
}
else {
    Write-Host "ERROR: Path not found: $Path" -ForegroundColor Red
    exit 1
}

if (-not $files -or $files.Count -eq 0) {
    Write-Host "No signable files found." -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($files.Count) file(s) to sign" -ForegroundColor Cyan
Write-Host ""

# Prompt
Write-Host "Press ENTER to start signing (smart card PIN may be required)..." -ForegroundColor Yellow
Read-Host | Out-Null

Write-Host ""

$successCount = 0
$failCount = 0

foreach ($file in $files) {
    Write-Host "Signing: $($file.Name)" -ForegroundColor Yellow

    # Build SignTool command
    $args = @(
        "sign"
        "/v"                              # Verbose
        "/sha1", $CertThumbprint          # Certificate thumbprint
        "/fd", $DigestAlgorithm           # File digest algorithm
        "/tr", $TimestampServer           # RFC 3161 timestamp server
        "/td", $DigestAlgorithm           # Timestamp digest algorithm
        $file.FullName
    )

    try {
        $result = & $signtool @args 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            Write-Host "  SUCCESS" -ForegroundColor Green
            $successCount++
        }
        else {
            # Check for specific errors
            $errorMsg = $result | Out-String
            if ($errorMsg -match "0xc0000225" -or $errorMsg -match "-1073741275") {
                Write-Host "  FAILED: Smart card key access error (crypto3 CSP issue)" -ForegroundColor Red
                Write-Host "  Try running on native Windows (not WSL)" -ForegroundColor Yellow
            }
            else {
                Write-Host "  FAILED: $errorMsg" -ForegroundColor Red
            }
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
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Signed:  $successCount" -ForegroundColor Green
Write-Host "Failed:  $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Gray" } else { "Red" })
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "Note: If signing fails with crypto3 CSP error, try:" -ForegroundColor Yellow
    Write-Host "  1. Run this script directly on Windows (not through WSL)"
    Write-Host "  2. Contact Certum about moving cert to Secure Profile (KSP)"
}

exit $(if ($failCount -eq 0) { 0 } else { 1 })
