$CertFingerprint = "06406CF467075EDDED9D2FF6D0EF813DC08D6D726A2354C7FE5F7CFA94E9EC59"
$CertStoreName = "My"
$CertStoreLocation = "CurrentUser"
$Timestamper = "http://time.certum.pl"
$NupkgDir = "nupkgs"

$PackageDir = Join-Path $PWD.Path $NupkgDir

Write-Host "DotCompute v0.2.0-alpha Package Signing" -ForegroundColor Blue
Write-Host ""

if (-not (Test-Path $PackageDir)) {
    Write-Host "Error: Directory not found!" -ForegroundColor Red
    exit 1
}

$Packages = Get-ChildItem -Path $PackageDir -Filter "*.nupkg" -File
$Packages += Get-ChildItem -Path $PackageDir -Filter "*.snupkg" -File

if ($Packages.Count -eq 0) {
    Write-Host "Error: No packages found!" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($Packages.Count) package(s) to sign" -ForegroundColor Green
Write-Host "Certificate SHA-256: $CertFingerprint"
Write-Host "Timestamper: $Timestamper"
Write-Host ""

$SignedCount = 0
$FailedCount = 0

foreach ($Package in $Packages) {
    $PackageName = $Package.Name
    Write-Host "Signing: $PackageName" -ForegroundColor Blue

    dotnet nuget sign $Package.FullName --certificate-fingerprint $CertFingerprint --certificate-store-name $CertStoreName --certificate-store-location $CertStoreLocation --timestamper $Timestamper --overwrite 2>&1 | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] $PackageName" -ForegroundColor Green
        $SignedCount++
    } else {
        Write-Host "[FAILED] $PackageName" -ForegroundColor Red
        $FailedCount++
    }
}

Write-Host ""
Write-Host "Signing Summary" -ForegroundColor Blue
Write-Host "Successfully signed: $SignedCount" -ForegroundColor Green
Write-Host "Failed: $FailedCount" -ForegroundColor $(if ($FailedCount -gt 0) { "Red" } else { "Green" })

if ($FailedCount -eq 0) {
    Write-Host "All packages signed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some packages failed to sign." -ForegroundColor Red
    exit 1
}
