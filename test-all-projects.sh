#!/bin/bash

echo "=== DotCompute Test Suite - Project by Project ==="
echo ""

projects=(
  "tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj"
  "tests/Unit/DotCompute.Abstractions.Tests/DotCompute.Abstractions.Tests.csproj"
  "tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj"
  "tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj"
  "tests/Unit/DotCompute.Runtime.Tests/DotCompute.Runtime.Tests.csproj"
  "tests/Unit/DotCompute.Generators.Tests/DotCompute.Generators.Tests.csproj"
  "tests/Unit/DotCompute.Linq.Tests/DotCompute.Linq.Tests.csproj"
  "tests/Unit/DotCompute.Algorithms.Tests/DotCompute.Algorithms.Tests.csproj"
)

total_passed=0
total_failed=0
total_skipped=0
total_tests=0

for proj in "${projects[@]}"; do
  name=$(basename "$proj" .csproj)
  echo "Testing: $name"
  result=$(dotnet test "$proj" --nologo --verbosity minimal 2>&1 | grep -E "Passed:|Failed:|Skipped:|Total:" | tail -1)
  echo "  $result"
  
  passed=$(echo "$result" | grep -oP 'Passed:\s+\K\d+' || echo "0")
  failed=$(echo "$result" | grep -oP 'Failed:\s+\K\d+' || echo "0")
  skipped=$(echo "$result" | grep -oP 'Skipped:\s+\K\d+' || echo "0")
  tests=$(echo "$result" | grep -oP 'Total:\s+\K\d+' || echo "0")
  
  total_passed=$((total_passed + passed))
  total_failed=$((total_failed + failed))
  total_skipped=$((total_skipped + skipped))
  total_tests=$((total_tests + tests))
  echo ""
done

echo "=== SUMMARY ==="
echo "Total Passed:  $total_passed"
echo "Total Failed:  $total_failed"
echo "Total Skipped: $total_skipped"
echo "Total Tests:   $total_tests"
percentage=$(python3 -c "print(f'{$total_passed/$total_tests*100:.2f}%')" 2>/dev/null || echo "N/A")
echo "Pass Rate:     $percentage"
