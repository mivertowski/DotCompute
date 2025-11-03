#!/bin/bash
# Wave 6 Verification Build Script
# Runs incremental builds to check progress without full rebuild

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_LOG="/tmp/wave6_verify_$(date +%s).txt"

echo "=== Wave 6 Verification Build ===" | tee "$BUILD_LOG"
echo "Time: $(date '+%Y-%m-%d %H:%M:%S')" | tee -a "$BUILD_LOG"
echo "" | tee -a "$BUILD_LOG"

cd "$PROJECT_ROOT"

# Run build with warning tracking
dotnet build DotCompute.sln --configuration Release --no-incremental 2>&1 | tee -a "$BUILD_LOG"

# Count warnings
TOTAL_WARNINGS=$(grep -c "warning" "$BUILD_LOG" || echo "0")
ERRORS=$(grep -c "error" "$BUILD_LOG" || echo "0")

echo "" | tee -a "$BUILD_LOG"
echo "=== Build Summary ===" | tee -a "$BUILD_LOG"
echo "Warnings: $TOTAL_WARNINGS" | tee -a "$BUILD_LOG"
echo "Errors: $ERRORS" | tee -a "$BUILD_LOG"

# Store results
npx claude-flow@alpha memory store wave6-build-status "Warnings:$TOTAL_WARNINGS|Errors:$ERRORS|Time:$(date '+%H:%M:%S')"

# Update progress log for monitoring
cp "$BUILD_LOG" /tmp/wave6_progress.txt

if [ "$ERRORS" -gt 0 ]; then
    echo "❌ Build has errors!" | tee -a "$BUILD_LOG"
    exit 1
else
    echo "✅ Build succeeded" | tee -a "$BUILD_LOG"
fi

echo "" | tee -a "$BUILD_LOG"
echo "Log: $BUILD_LOG" | tee -a "$BUILD_LOG"
