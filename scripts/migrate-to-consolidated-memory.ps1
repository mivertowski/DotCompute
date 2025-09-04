# PowerShell script to migrate to consolidated memory management
# Run from the DotCompute root directory

Write-Host "Starting migration to consolidated memory management..." -ForegroundColor Green

# Step 1: Backup original files
Write-Host "`nStep 1: Creating backup..." -ForegroundColor Yellow
$backupDir = "src/Backends/DotCompute.Backends.CUDA/Memory/Legacy"
if (!(Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
}

$filesToBackup = @(
    "CudaMemoryManager.cs",
    "CudaAsyncMemoryManager.cs", 
    "CudaAsyncMemoryManagerAdapter.cs",
    "CudaUnifiedMemoryManagerProduction.cs",
    "CudaMemoryBuffer.cs",
    "CudaUnifiedMemoryBuffer.cs",
    "SimpleCudaUnifiedMemoryBuffer.cs",
    "CudaRawMemoryBuffer.cs",
    "CudaUnifiedMemoryAdvanced.cs"
)

foreach ($file in $filesToBackup) {
    $source = "src/Backends/DotCompute.Backends.CUDA/Memory/$file"
    if (Test-Path $source) {
        Move-Item $source "$backupDir/$file" -Force
        Write-Host "  Backed up: $file" -ForegroundColor Gray
    }
}

# Step 2: Update CudaAccelerator.cs
Write-Host "`nStep 2: Updating CudaAccelerator..." -ForegroundColor Yellow
$acceleratorFile = "src/Backends/DotCompute.Backends.CUDA/CudaAccelerator.cs"
if (Test-Path $acceleratorFile) {
    $content = Get-Content $acceleratorFile -Raw
    
    # Replace old memory manager references
    $content = $content -replace 'using DotCompute\.Backends\.CUDA\.Memory;', 'using DotCompute.Backends.CUDA.Memory;'
    $content = $content -replace 'CudaMemoryManager', 'CudaMemoryManagerConsolidated'
    $content = $content -replace 'CudaAsyncMemoryManagerAdapter', 'CudaMemoryManagerConsolidated'
    $content = $content -replace 'Memory\.CudaMemoryManager', 'Memory.CudaMemoryManagerConsolidated'
    
    # Update constructor parameters
    $content = $content -replace 'out var memoryManager', 'out var memoryManager'
    $content = $content -replace 'new CudaAsyncMemoryManagerAdapter\(memoryManager\)', 'memoryManager'
    
    Set-Content $acceleratorFile $content
    Write-Host "  Updated CudaAccelerator.cs" -ForegroundColor Gray
}

# Step 3: Update Factory files
Write-Host "`nStep 3: Updating Factory files..." -ForegroundColor Yellow
$factoryFile = "src/Backends/DotCompute.Backends.CUDA/Factory/CudaAcceleratorFactory.cs"
if (Test-Path $factoryFile) {
    $content = Get-Content $factoryFile -Raw
    
    # Replace memory manager references
    $content = $content -replace 'CudaUnifiedMemoryManagerProduction', 'CudaMemoryManagerConsolidated'
    $content = $content -replace 'CudaAsyncMemoryManager', 'CudaMemoryManagerConsolidated'
    $content = $content -replace 'SimpleCudaUnifiedMemoryBuffer', 'CudaMemoryBufferConsolidated'
    
    Set-Content $factoryFile $content
    Write-Host "  Updated CudaAcceleratorFactory.cs" -ForegroundColor Gray
}

# Step 4: Update test files
Write-Host "`nStep 4: Updating test files..." -ForegroundColor Yellow
$testFiles = Get-ChildItem -Path "tests" -Filter "*.cs" -Recurse | Where-Object { $_.FullName -match "Cuda" }

foreach ($testFile in $testFiles) {
    $content = Get-Content $testFile.FullName -Raw
    $updated = $false
    
    # Check if file contains old references
    if ($content -match 'CudaMemoryManager|CudaUnifiedMemoryBuffer|SimpleCudaUnifiedMemoryBuffer') {
        $content = $content -replace 'CudaMemoryManager(?!Consolidated)', 'CudaMemoryManagerConsolidated'
        $content = $content -replace 'CudaUnifiedMemoryBuffer', 'CudaMemoryBufferConsolidated'
        $content = $content -replace 'SimpleCudaUnifiedMemoryBuffer', 'CudaMemoryBufferConsolidated'
        $content = $content -replace 'CudaRawMemoryBuffer(?!Consolidated)', 'CudaRawMemoryBufferConsolidated'
        
        Set-Content $testFile.FullName $content
        Write-Host "  Updated: $($testFile.Name)" -ForegroundColor Gray
        $updated = $true
    }
}

# Step 5: Update using statements across the project
Write-Host "`nStep 5: Updating using statements..." -ForegroundColor Yellow
$sourceFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

foreach ($sourceFile in $sourceFiles) {
    $content = Get-Content $sourceFile.FullName -Raw
    $updated = $false
    
    # Update references to old classes
    if ($content -match 'CudaMemoryBuffer|CudaUnifiedMemoryManagerProduction|SimpleCudaUnifiedMemoryBuffer') {
        $content = $content -replace 'CudaMemoryBuffer(?!Consolidated)', 'CudaMemoryBufferConsolidated'
        $content = $content -replace 'CudaUnifiedMemoryManagerProduction', 'CudaMemoryManagerConsolidated'
        $content = $content -replace 'SimpleCudaUnifiedMemoryBuffer', 'CudaMemoryBufferConsolidated'
        $content = $content -replace 'CudaAsyncMemoryManagerAdapter', 'CudaMemoryManagerConsolidated'
        
        Set-Content $sourceFile.FullName $content
        $updated = $true
    }
    
    if ($updated) {
        Write-Host "  Updated: $($sourceFile.Name)" -ForegroundColor Gray
    }
}

# Step 6: Create summary report
Write-Host "`nStep 6: Creating migration report..." -ForegroundColor Yellow
$reportPath = "docs/memory-migration-report.md"
$report = @"
# Memory Management Migration Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Summary
Successfully migrated CUDA memory management to consolidated implementation.

## Changes Made

### Replaced Classes
| Old Class | New Class | Purpose |
|-----------|-----------|---------|
| CudaMemoryManager | CudaMemoryManagerConsolidated | Primary memory manager |
| CudaAsyncMemoryManager | CudaMemoryManagerConsolidated | Merged into primary |
| CudaAsyncMemoryManagerAdapter | CudaMemoryManagerConsolidated | Adapter removed |
| CudaUnifiedMemoryManagerProduction | CudaMemoryManagerConsolidated | Merged into primary |
| CudaMemoryBuffer | CudaMemoryBufferConsolidated<T> | Type-safe buffer |
| CudaUnifiedMemoryBuffer | CudaMemoryBufferConsolidated<T> | Merged into primary |
| SimpleCudaUnifiedMemoryBuffer | CudaMemoryBufferConsolidated<T> | Merged into primary |
| CudaRawMemoryBuffer | CudaRawMemoryBufferConsolidated | Raw byte buffer |

### Legacy Files
All legacy files have been moved to: ``src/Backends/DotCompute.Backends.CUDA/Memory/Legacy/``

### Benefits
1. **Reduced Complexity**: From 9 classes to 3 classes
2. **Consistent API**: Single interface for all memory operations
3. **Better Performance**: Removed adapter indirection
4. **Clearer Naming**: Professional, descriptive names
5. **Unified Statistics**: Single source of truth for memory metrics

## Next Steps
1. Run all tests to verify functionality
2. Remove legacy files after verification
3. Update documentation
"@

Set-Content $reportPath $report
Write-Host "  Report saved to: $reportPath" -ForegroundColor Gray

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Migration completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Run: dotnet build" -ForegroundColor Gray
Write-Host "2. Run: dotnet test" -ForegroundColor Gray
Write-Host "3. Review changes and fix any compilation errors" -ForegroundColor Gray
Write-Host "4. Delete legacy files from: $backupDir" -ForegroundColor Gray