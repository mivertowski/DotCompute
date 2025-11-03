#!/bin/bash

# Analyze CA1852 (Can Be Sealed) and CA2213 (Missing Disposal) warnings
# This script statically analyzes the code without requiring a full build

echo "=== CA1852: Internal Classes That Can Be Sealed ==="
echo

# Find internal classes in test files that aren't sealed or abstract
find tests/ -name "*.cs" -type f -exec grep -H "internal class " {} \; | \
  grep -v "abstract class" | \
  grep -v "sealed class" | \
  sed 's/:.*internal class /: /' | \
  sed 's/\s*:.*//' | \
  head -50

echo
echo "=== CA2213: Test Classes with IDisposable Fields ==="
echo

# Find test classes that have disposable fields but don't implement IDisposable
for file in $(find tests/ -name "*Tests.cs" -type f); do
  # Check if file has disposable fields (common ones)
  if grep -q "_accelerator\|_context\|_stream\|_buffer\|_kernel\|_device" "$file"; then
    # Check if it implements IDisposable
    if ! grep -q "IDisposable" "$file"; then
      echo "$file"
    fi
  fi
done | head -50

echo
echo "=== Summary ==="
echo "Run this to get accurate counts:"
echo "dotnet build DotCompute.sln --configuration Release 2>&1 | grep -E 'warning (CA1852|CA2213)' | wc -l"
