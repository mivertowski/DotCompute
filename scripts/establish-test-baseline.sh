#!/bin/bash
# Test Baseline Establishment Script
# Hive Mind - Tester Agent
# Run this after successful build to establish test baseline

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="$PROJECT_ROOT/TestResults"

echo "=================================="
echo "Test Baseline Establishment"
echo "=================================="
echo "Date: $(date -Iseconds)"
echo "Project: DotCompute"
echo ""

# Ensure clean state
echo "Cleaning previous test results..."
rm -rf "$RESULTS_DIR"
mkdir -p "$RESULTS_DIR"

# Build check
echo "Verifying build..."
if ! dotnet build "$PROJECT_ROOT/DotCompute.sln" --no-restore > /dev/null 2>&1; then
    echo "❌ ERROR: Build failed. Fix compilation errors first."
    exit 1
fi

echo "✅ Build successful"
echo ""

# Run test suite
echo "Running test suite..."
dotnet test "$PROJECT_ROOT/DotCompute.sln" \
    --no-build \
    --verbosity normal \
    --logger "trx;LogFileName=baseline.trx" \
    --logger "json;LogFileName=baseline.json" \
    --results-directory "$RESULTS_DIR" \
    > "$RESULTS_DIR/baseline_output.txt" 2>&1

TEST_EXIT_CODE=$?

# Parse results
echo ""
echo "=================================="
echo "Test Results Summary"
echo "=================================="

TOTAL=$(grep -oP 'Total tests: \K\d+' "$RESULTS_DIR/baseline_output.txt" || echo "0")
PASSED=$(grep -oP 'Passed: \K\d+' "$RESULTS_DIR/baseline_output.txt" || echo "0")
FAILED=$(grep -oP 'Failed: \K\d+' "$RESULTS_DIR/baseline_output.txt" || echo "0")
SKIPPED=$(grep -oP 'Skipped: \K\d+' "$RESULTS_DIR/baseline_output.txt" || echo "0")

echo "Total:   $TOTAL"
echo "Passed:  $PASSED"
echo "Failed:  $FAILED"
echo "Skipped: $SKIPPED"
echo ""

# Create JSON summary
cat > "$RESULTS_DIR/baseline_summary.json" <<EOF
{
  "timestamp": "$(date -Iseconds)",
  "build_status": "SUCCESS",
  "test_exit_code": $TEST_EXIT_CODE,
  "test_counts": {
    "total": $TOTAL,
    "passed": $PASSED,
    "failed": $FAILED,
    "skipped": $SKIPPED
  },
  "pass_rate": $(echo "scale=2; $PASSED * 100 / $TOTAL" | bc),
  "test_categories": {
    "unit": "executed",
    "integration": "executed",
    "hardware": "skipped_or_executed"
  },
  "baseline_files": {
    "trx": "baseline.trx",
    "json": "baseline.json",
    "output": "baseline_output.txt"
  }
}
EOF

# Store in hive memory
echo "Storing baseline in hive memory..."
BASELINE_DATA=$(cat "$RESULTS_DIR/baseline_summary.json")
npx claude-flow@alpha memory store "hive/tester/baseline_results" "$BASELINE_DATA" --namespace "default"

echo "✅ Baseline established and stored"
echo ""

# Create marker file
echo "$(date -Iseconds)" > "$RESULTS_DIR/.baseline_established"

echo "=================================="
echo "Baseline Establishment Complete"
echo "=================================="
echo "Results stored in: $RESULTS_DIR"
echo "Hive memory key: hive/tester/baseline_results"
echo ""

if [ $FAILED -gt 0 ]; then
    echo "⚠️  WARNING: $FAILED tests failed in baseline"
    echo "These failures will be tracked as known issues"
fi

exit 0
