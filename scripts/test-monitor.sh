#!/bin/bash
# Continuous build monitoring script for DotCompute housekeeping hive
# Tests agent monitoring build status and error reduction

BASELINE_CS_ERRORS=540
BASELINE_TOTAL_ERRORS=4572
LOG_DIR="/tmp/dotcompute-test-logs"

mkdir -p "$LOG_DIR"

echo "============================================"
echo "DotCompute Build Monitor - $(date)"
echo "============================================"
echo ""
echo "Baseline: $BASELINE_CS_ERRORS CS errors, $BASELINE_TOTAL_ERRORS total errors"
echo ""

# Run build and capture output
BUILD_LOG="$LOG_DIR/build-$(date +%Y%m%d-%H%M%S).log"
dotnet build DotCompute.sln --configuration Release 2>&1 | tee "$BUILD_LOG"

# Count errors
CS_ERRORS=$(grep "error CS" "$BUILD_LOG" | wc -l)
CA_ERRORS=$(grep "error CA" "$BUILD_LOG" | wc -l)
XFIX_ERRORS=$(grep "error XFIX" "$BUILD_LOG" | wc -l)
TOTAL_ERRORS=$(grep -E "(error|warning)" "$BUILD_LOG" | wc -l)

echo ""
echo "============================================"
echo "Build Status Summary"
echo "============================================"
echo "CS Errors:   $CS_ERRORS (baseline: $BASELINE_CS_ERRORS)"
echo "CA Errors:   $CA_ERRORS"
echo "XFIX Errors: $XFIX_ERRORS"
echo "Total:       $TOTAL_ERRORS (baseline: $BASELINE_TOTAL_ERRORS)"
echo ""

# Calculate reduction
CS_REDUCTION=$((BASELINE_CS_ERRORS - CS_ERRORS))
TOTAL_REDUCTION=$((BASELINE_TOTAL_ERRORS - TOTAL_ERRORS))

if [ $CS_ERRORS -lt $BASELINE_CS_ERRORS ]; then
    echo "✅ CS Errors reduced by $CS_REDUCTION ($(( CS_REDUCTION * 100 / BASELINE_CS_ERRORS ))%)"
else
    echo "⚠️  CS Errors unchanged or increased"
fi

if [ $TOTAL_ERRORS -lt $BASELINE_TOTAL_ERRORS ]; then
    echo "✅ Total errors reduced by $TOTAL_REDUCTION ($(( TOTAL_REDUCTION * 100 / BASELINE_TOTAL_ERRORS ))%)"
else
    echo "⚠️  Total errors unchanged or increased"
fi

echo ""

# Top error types
echo "Top CS Errors:"
grep "error CS" "$BUILD_LOG" | sed 's/.*error //' | cut -d':' -f1 | sort | uniq -c | sort -rn | head -5

echo ""
echo "Top CA Errors:"
grep "error CA" "$BUILD_LOG" | sed 's/.*error //' | cut -d':' -f1 | sort | uniq -c | sort -rn | head -5

echo ""
echo "============================================"
echo "Log saved to: $BUILD_LOG"
echo "============================================"

# Notify hive
npx claude-flow@alpha hooks notify --message "Build check: $CS_ERRORS CS errors (-$CS_REDUCTION), $TOTAL_ERRORS total (-$TOTAL_REDUCTION)"

exit 0
