#!/bin/bash
# Regression Detection Script
# Hive Mind - Tester Agent
# Run after each fix batch to detect regressions

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="$PROJECT_ROOT/TestResults"

echo "=================================="
echo "Regression Detection"
echo "=================================="
echo "Date: $(date -Iseconds)"
echo ""

# Check if baseline exists
if [ ! -f "$RESULTS_DIR/.baseline_established" ]; then
    echo "❌ ERROR: No baseline established"
    echo "Run: ./scripts/establish-test-baseline.sh"
    exit 1
fi

BASELINE_DATE=$(cat "$RESULTS_DIR/.baseline_established")
echo "Baseline from: $BASELINE_DATE"
echo ""

# Build check
echo "Verifying build..."
if ! dotnet build "$PROJECT_ROOT/DotCompute.sln" --no-restore > /dev/null 2>&1; then
    echo "❌ ERROR: Build failed. Fix compilation errors first."

    # Report build failure
    npx claude-flow@alpha hooks notify \
        --message "REGRESSION DETECTED: Build now fails (was passing at baseline)" \
        --level "error"

    exit 1
fi

echo "✅ Build successful"
echo ""

# Run current tests
echo "Running current test suite..."
dotnet test "$PROJECT_ROOT/DotCompute.sln" \
    --no-build \
    --verbosity normal \
    --logger "trx;LogFileName=current.trx" \
    --logger "json;LogFileName=current.json" \
    --results-directory "$RESULTS_DIR" \
    > "$RESULTS_DIR/current_output.txt" 2>&1

TEST_EXIT_CODE=$?

# Parse current results
CURRENT_TOTAL=$(grep -oP 'Total tests: \K\d+' "$RESULTS_DIR/current_output.txt" || echo "0")
CURRENT_PASSED=$(grep -oP 'Passed: \K\d+' "$RESULTS_DIR/current_output.txt" || echo "0")
CURRENT_FAILED=$(grep -oP 'Failed: \K\d+' "$RESULTS_DIR/current_output.txt" || echo "0")
CURRENT_SKIPPED=$(grep -oP 'Skipped: \K\d+' "$RESULTS_DIR/current_output.txt" || echo "0")

# Load baseline
BASELINE_DATA=$(npx claude-flow@alpha memory retrieve "hive/tester/baseline_results" || echo '{}')
BASELINE_PASSED=$(echo "$BASELINE_DATA" | jq -r '.test_counts.passed // 0')
BASELINE_FAILED=$(echo "$BASELINE_DATA" | jq -r '.test_counts.failed // 0')
BASELINE_TOTAL=$(echo "$BASELINE_DATA" | jq -r '.test_counts.total // 0')

echo "=================================="
echo "Comparison Results"
echo "=================================="
echo ""
echo "Metric          Baseline    Current     Delta"
echo "------------------------------------------------"
printf "Total           %-11s %-11s %+d\n" "$BASELINE_TOTAL" "$CURRENT_TOTAL" "$((CURRENT_TOTAL - BASELINE_TOTAL))"
printf "Passed          %-11s %-11s %+d\n" "$BASELINE_PASSED" "$CURRENT_PASSED" "$((CURRENT_PASSED - BASELINE_PASSED))"
printf "Failed          %-11s %-11s %+d\n" "$BASELINE_FAILED" "$CURRENT_FAILED" "$((CURRENT_FAILED - BASELINE_FAILED))"
printf "Skipped         %-11s %-11s %+d\n" "$BASELINE_SKIPPED" "$CURRENT_SKIPPED" "$((CURRENT_SKIPPED - BASELINE_SKIPPED))"
echo ""

# Detect regressions
REGRESSIONS_FOUND=0
REGRESSION_MESSAGE=""

# Check for reduced passing tests
if [ $CURRENT_PASSED -lt $BASELINE_PASSED ]; then
    REGRESSIONS_FOUND=1
    LOST_TESTS=$((BASELINE_PASSED - CURRENT_PASSED))
    REGRESSION_MESSAGE="${REGRESSION_MESSAGE}❌ REGRESSION: Lost $LOST_TESTS passing tests\n"
fi

# Check for increased failures
if [ $CURRENT_FAILED -gt $BASELINE_FAILED ]; then
    REGRESSIONS_FOUND=1
    NEW_FAILURES=$((CURRENT_FAILED - BASELINE_FAILED))
    REGRESSION_MESSAGE="${REGRESSION_MESSAGE}❌ REGRESSION: $NEW_FAILURES new test failures\n"
fi

# Check for reduced total tests
if [ $CURRENT_TOTAL -lt $BASELINE_TOTAL ]; then
    REGRESSIONS_FOUND=1
    MISSING_TESTS=$((BASELINE_TOTAL - CURRENT_TOTAL))
    REGRESSION_MESSAGE="${REGRESSION_MESSAGE}⚠️  WARNING: $MISSING_TESTS tests missing from suite\n"
fi

# Report results
if [ $REGRESSIONS_FOUND -eq 1 ]; then
    echo "=================================="
    echo "⚠️  REGRESSIONS DETECTED"
    echo "=================================="
    echo -e "$REGRESSION_MESSAGE"

    # Store regression report
    cat > "$RESULTS_DIR/regression_report.json" <<EOF
{
  "timestamp": "$(date -Iseconds)",
  "regressions_detected": true,
  "baseline": {
    "total": $BASELINE_TOTAL,
    "passed": $BASELINE_PASSED,
    "failed": $BASELINE_FAILED
  },
  "current": {
    "total": $CURRENT_TOTAL,
    "passed": $CURRENT_PASSED,
    "failed": $CURRENT_FAILED
  },
  "deltas": {
    "total": $((CURRENT_TOTAL - BASELINE_TOTAL)),
    "passed": $((CURRENT_PASSED - BASELINE_PASSED)),
    "failed": $((CURRENT_FAILED - BASELINE_FAILED))
  },
  "message": "$REGRESSION_MESSAGE"
}
EOF

    # Notify hive
    npx claude-flow@alpha hooks notify \
        --message "REGRESSION DETECTED: $REGRESSION_MESSAGE" \
        --level "error"

    # Store in memory
    npx claude-flow@alpha memory store "hive/tester/regression_log" \
        "$(cat $RESULTS_DIR/regression_report.json)" \
        --namespace "default"

    exit 1
else
    echo "=================================="
    echo "✅ NO REGRESSIONS DETECTED"
    echo "=================================="

    if [ $CURRENT_PASSED -gt $BASELINE_PASSED ]; then
        IMPROVEMENTS=$((CURRENT_PASSED - BASELINE_PASSED))
        echo "🎉 IMPROVEMENT: $IMPROVEMENTS more tests passing!"
    fi

    echo ""
    echo "All test metrics maintained or improved"

    # Store success report
    cat > "$RESULTS_DIR/regression_report.json" <<EOF
{
  "timestamp": "$(date -Iseconds)",
  "regressions_detected": false,
  "baseline": {
    "total": $BASELINE_TOTAL,
    "passed": $BASELINE_PASSED,
    "failed": $BASELINE_FAILED
  },
  "current": {
    "total": $CURRENT_TOTAL,
    "passed": $CURRENT_PASSED,
    "failed": $CURRENT_FAILED
  },
  "deltas": {
    "total": $((CURRENT_TOTAL - BASELINE_TOTAL)),
    "passed": $((CURRENT_PASSED - BASELINE_PASSED)),
    "failed": $((CURRENT_FAILED - BASELINE_FAILED))
  }
}
EOF

    # Update current results in memory
    npx claude-flow@alpha memory store "hive/tester/current_results" \
        "$(cat $RESULTS_DIR/regression_report.json)" \
        --namespace "default"

    exit 0
fi
