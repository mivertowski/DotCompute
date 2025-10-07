#!/bin/bash
# Regression detection script
# Compares current build against baseline and previous runs

BASELINE_LOG="/tmp/build-baseline.log"
CURRENT_LOG="/tmp/build-current-$(date +%Y%m%d-%H%M%S).log"
MEMORY_DB=".swarm/memory.db"

echo "============================================"
echo "Regression Detector"
echo "============================================"
echo ""

# Run current build
echo "Running build..."
dotnet build DotCompute.sln --configuration Release 2>&1 | tee "$CURRENT_LOG"

# Extract error categories from baseline
if [ ! -f "$BASELINE_LOG" ]; then
    echo "⚠️  Baseline log not found at $BASELINE_LOG"
    echo "Creating new baseline..."
    cp "$CURRENT_LOG" "$BASELINE_LOG"
    exit 0
fi

echo ""
echo "Comparing against baseline..."
echo ""

# Count errors
BASELINE_CS=$(grep -c "error CS" "$BASELINE_LOG" 2>/dev/null || echo 0)
CURRENT_CS=$(grep -c "error CS" "$CURRENT_LOG" 2>/dev/null || echo 0)

BASELINE_CA=$(grep -c "error CA" "$BASELINE_LOG" 2>/dev/null || echo 0)
CURRENT_CA=$(grep -c "error CA" "$CURRENT_LOG" 2>/dev/null || echo 0)

BASELINE_TOTAL=$(grep -cE "(error|warning)" "$BASELINE_LOG" 2>/dev/null || echo 0)
CURRENT_TOTAL=$(grep -cE "(error|warning)" "$CURRENT_LOG" 2>/dev/null || echo 0)

# Calculate changes
CS_DIFF=$((CURRENT_CS - BASELINE_CS))
CA_DIFF=$((CURRENT_CA - BASELINE_CA))
TOTAL_DIFF=$((CURRENT_TOTAL - BASELINE_TOTAL))

echo "CS Errors:    $CURRENT_CS (baseline: $BASELINE_CS, change: $CS_DIFF)"
echo "CA Errors:    $CURRENT_CA (baseline: $BASELINE_CA, change: $CA_DIFF)"
echo "Total:        $CURRENT_TOTAL (baseline: $BASELINE_TOTAL, change: $TOTAL_DIFF)"
echo ""

# Detect regressions (new errors introduced)
REGRESSION=0

if [ "$CS_DIFF" -gt 0 ]; then
    echo "🚨 REGRESSION: CS errors increased by $CS_DIFF"
    echo ""
    echo "New CS error types:"
    comm -13 \
        <(grep "error CS" "$BASELINE_LOG" | sed 's/.*error //' | cut -d':' -f1 | sort -u) \
        <(grep "error CS" "$CURRENT_LOG" | sed 's/.*error //' | cut -d':' -f1 | sort -u)
    REGRESSION=1
fi

if [ "$CA_DIFF" -gt 0 ]; then
    echo "⚠️  WARNING: CA analyzer errors increased by $CA_DIFF"
    REGRESSION=1
fi

if [ "$CS_DIFF" -lt 0 ]; then
    echo "✅ IMPROVEMENT: CS errors decreased by ${CS_DIFF#-}"
fi

if [ "$CA_DIFF" -lt 0 ]; then
    echo "✅ IMPROVEMENT: CA errors decreased by ${CA_DIFF#-}"
fi

if [ "$TOTAL_DIFF" -lt 0 ]; then
    echo "✅ IMPROVEMENT: Total errors decreased by ${TOTAL_DIFF#-}"
fi

echo ""
echo "============================================"

# Notify hive
if [ "$REGRESSION" -eq 1 ]; then
    npx claude-flow@alpha hooks notify --message "🚨 REGRESSION DETECTED: CS +$CS_DIFF, Total +$TOTAL_DIFF"
    exit 1
else
    npx claude-flow@alpha hooks notify --message "✅ No regressions: CS $CS_DIFF, Total $TOTAL_DIFF"
    exit 0
fi
