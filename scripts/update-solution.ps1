# PowerShell script to update solution file with new test structure

$solutionFile = "DotCompute.sln"

# Read the solution file
$content = Get-Content $solutionFile -Raw

# Update test project paths
$replacements = @{
    'tests\\DotCompute.SharedTestUtilities\\' = 'tests\Shared\DotCompute.Tests.Common\'
    'tests\\DotCompute.TestDoubles\\' = 'tests\Shared\DotCompute.Tests.Mocks\'
    'tests\\DotCompute.TestImplementations\\' = 'tests\Shared\DotCompute.Tests.Implementations\'
    'tests\\DotCompute.BasicTests\\' = 'tests\Unit\DotCompute.BasicTests\'
    'tests\\DotCompute.Abstractions.Tests\\' = 'tests\Unit\DotCompute.Abstractions.Tests\'
    'tests\\DotCompute.Core.Tests\\' = 'tests\Unit\DotCompute.Core.Tests\'
    'tests\\DotCompute.Memory.Tests\\' = 'tests\Unit\DotCompute.Memory.Tests\'
    'tests\\DotCompute.Plugins.Tests\\' = 'tests\Unit\DotCompute.Plugins.Tests\'
    'tests\\DotCompute.Generators.Tests\\' = 'tests\Unit\DotCompute.Generators.Tests\'
    'tests\\DotCompute.Algorithms.Tests\\' = 'tests\Unit\DotCompute.Algorithms.Tests\'
    'tests\\DotCompute.Integration.Tests\\' = 'tests\Integration\DotCompute.Integration.Tests\'
    'tests\\DotCompute.Hardware.Tests\\' = 'tests\Hardware\DotCompute.Hardware.Mock.Tests\'
    'tests\\DotCompute.Hardware.RealTests\\' = 'tests\Hardware\DotCompute.Hardware.Cuda.Tests\'
}

# Update project names
$nameReplacements = @{
    '"DotCompute.SharedTestUtilities"' = '"DotCompute.Tests.Common"'
    '"DotCompute.TestDoubles"' = '"DotCompute.Tests.Mocks"'
    '"DotCompute.TestImplementations"' = '"DotCompute.Tests.Implementations"'
    '"DotCompute.Hardware.Tests"' = '"DotCompute.Hardware.Mock.Tests"'
    '"DotCompute.Hardware.RealTests"' = '"DotCompute.Hardware.Cuda.Tests"'
}

# Apply replacements
foreach ($key in $replacements.Keys) {
    $content = $content -replace [regex]::Escape($key), $replacements[$key]
}

foreach ($key in $nameReplacements.Keys) {
    $content = $content -replace [regex]::Escape($key), $nameReplacements[$key]
}

# Save the updated solution file
Set-Content -Path $solutionFile -Value $content -NoNewline

Write-Host "Solution file updated successfully!"