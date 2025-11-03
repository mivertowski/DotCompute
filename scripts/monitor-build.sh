#!/bin/bash
# Agent 20 - Build Health Monitor Script
# Continuous monitoring for Round 4 warning elimination

ROUND_START=90
TIMESTAMP=$(date +"%Y-%m-%d %H:%M:%S")

echo "========================================"
echo "AGENT 20 - BUILD HEALTH MONITOR"
echo "========================================"
echo "Timestamp: $TIMESTAMP"
echo ""

# Run clean build
echo "Running clean build..."
dotnet clean DotCompute.sln --configuration Release > /dev/null 2>&1

# Build and capture output
BUILD_OUTPUT=$(dotnet build DotCompute.sln --configuration Release --no-incremental 2>&1)
BUILD_EXIT=$?

# Extract metrics
WARNINGS=$(echo "$BUILD_OUTPUT" | grep "Warning(s)" | tail -1 | grep -oE '[0-9]+ Warning' | grep -oE '[0-9]+')
ERRORS=$(echo "$BUILD_OUTPUT" | grep "Error(s)" | tail -1 | grep -oE '[0-9]+ Error' | grep -oE '[0-9]+')

# Default to 0 if empty
WARNINGS=${WARNINGS:-0}
ERRORS=${ERRORS:-0}

# Calculate progress
DELTA=$((ROUND_START - WARNINGS))
PROGRESS_PCT=$(awk "BEGIN {printf \"%.1f\", (1 - $WARNINGS/$ROUND_START) * 100}")

# Build status
if [ $BUILD_EXIT -eq 0 ] && [ $ERRORS -eq 0 ]; then
    BUILD_STATUS="✅ SUCCESS"
else
    BUILD_STATUS="❌ FAILED"
fi

# Display summary
echo "Round 4 Progress:"
echo "- Start: $ROUND_START warnings"
echo "- Current: $WARNINGS warnings (-$DELTA)"
echo "- Progress: $PROGRESS_PCT%"
echo "- Build Status: $BUILD_STATUS"
echo "- Errors: $ERRORS (must be 0)"
echo ""

# Category breakdown
echo "Category Breakdown (Top 15):"
echo "$BUILD_OUTPUT" | grep -E "warning (CS|CA|IDE|xUnit)" | \
    sed 's/.*warning \([^:]*\):.*/\1/' | \
    sort | uniq -c | sort -rn | head -15 | \
    awk '{printf "  %3d  %s\n", $1, $2}'
echo ""

# Quality gates
echo "Quality Gates:"
if [ $BUILD_EXIT -eq 0 ]; then
    echo "✅ Build Passing"
else
    echo "❌ Build Failed"
fi

if [ $ERRORS -eq 0 ]; then
    echo "✅ Zero Errors"
else
    echo "❌ Errors Detected: $ERRORS"
fi

if [ $WARNINGS -le $ROUND_START ]; then
    echo "✅ Warnings Decreasing"
else
    echo "❌ Warnings Increased!"
fi

echo ""

# Agent activity check
echo "Agent Activity:"
echo "- Agent 16 (xUnit): Monitoring..."
echo "- Agent 17 (CA1307): Monitoring..."
echo "- Agent 18 (CA2016): Monitoring..."
echo "- Agent 19 (CA Analysis): Monitoring..."
echo ""

# Recommendations
echo "Recommendations:"
if [ $ERRORS -gt 0 ]; then
    echo "⚠️  CRITICAL: Build errors detected - immediate attention required"
fi

if [ $WARNINGS -gt $ROUND_START ]; then
    echo "⚠️  WARNING: Warning count increased - review recent changes"
fi

if [ $WARNINGS -eq 0 ]; then
    echo "🎉 MISSION ACCOMPLISHED: All warnings eliminated!"
elif [ $WARNINGS -lt 10 ]; then
    echo "💪 Almost there! Only $WARNINGS warnings remaining"
elif [ $WARNINGS -lt 30 ]; then
    echo "📈 Good progress! Continue with current strategy"
else
    echo "📊 Steady progress - $WARNINGS warnings to go"
fi

echo ""
echo "========================================"
exit $BUILD_EXIT
