#!/bin/bash

# Hive Mind Fix Validation Script
# Runs targeted tests when error count drops significantly

PROJECT_DIR="/home/mivertowski/DotCompute/DotCompute"
LOG_DIR="$PROJECT_DIR/scripts/logs"

cd "$PROJECT_DIR"

echo "=== HIVE VALIDATION STARTING ==="
echo "Timestamp: $(date)"

# Count current errors
current_errors=$(dotnet build src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj --configuration Release --verbosity quiet 2>&1 | grep -c "error CS" || echo "0")

echo "Current build errors: $current_errors"

if [ "$current_errors" -gt 0 ]; then
    echo "⚠️  Build still has errors. Skipping tests."
    echo "Top remaining errors:"
    dotnet build src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj --configuration Release 2>&1 | \
    grep "error CS" | \
    awk -F: '{print $4}' | \
    sort | uniq -c | sort -nr | head -5
    exit 1
fi

echo "✅ Build successful! Running validation tests..."

# Test the LINQ extensions if build passes
echo "Testing DotCompute.Linq module..."
test_result=$(dotnet test tests/Unit/DotCompute.Linq.Tests/DotCompute.Linq.Tests.csproj --configuration Release --no-build --verbosity minimal 2>&1)
test_exit_code=$?

if [ $test_exit_code -eq 0 ]; then
    echo "✅ All LINQ tests passed!"
    echo "$test_result" | grep "Test Run"
else
    echo "❌ Some tests failed:"
    echo "$test_result" | grep -E "(Failed|Error)"
fi

# Test integration if available
if [ -d "tests/Integration/DotCompute.Linq.Integration.Tests" ]; then
    echo "Running integration tests..."
    integration_result=$(dotnet test tests/Integration/DotCompute.Linq.Integration.Tests/ --configuration Release --no-build --verbosity minimal 2>&1)
    integration_exit_code=$?
    
    if [ $integration_exit_code -eq 0 ]; then
        echo "✅ Integration tests passed!"
    else
        echo "❌ Integration tests failed:"
        echo "$integration_result" | grep -E "(Failed|Error)"
    fi
fi

# Log results
echo "$(date),validation,$current_errors,$test_exit_code" >> "$LOG_DIR/validation.csv"

echo "=== VALIDATION COMPLETE ==="

# Store results in memory for hive coordination
validation_data="{\"timestamp\": \"$(date -Iseconds)\", \"errors\": $current_errors, \"tests_passed\": $([ $test_exit_code -eq 0 ] && echo true || echo false), \"validation_status\": \"$([ $test_exit_code -eq 0 ] && echo 'passed' || echo 'failed')\"}"

# Use claude-flow to store validation results
npx claude-flow@alpha memory store "hive/linq/validation/latest" "$validation_data" --namespace hive || echo "Memory storage not available"

exit $test_exit_code