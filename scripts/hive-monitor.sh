#!/bin/bash

# Hive Mind Progress Monitor for DotCompute.Linq Build Fixes
# This script continuously monitors build progress and provides updates

PROJECT_DIR="/home/mivertowski/DotCompute/DotCompute"
LOG_DIR="$PROJECT_DIR/scripts/logs"
BASELINE_ERRORS=172

# Ensure log directory exists
mkdir -p "$LOG_DIR"

# Function to count current errors
count_errors() {
    cd "$PROJECT_DIR"
    dotnet build src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj --configuration Release --verbosity quiet 2>&1 | grep -c "error CS" || echo "0"
}

# Function to get detailed error breakdown
get_error_breakdown() {
    cd "$PROJECT_DIR"
    dotnet build src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj --configuration Release 2>&1 | \
    grep "error CS" | \
    awk -F: '{print $4}' | \
    sort | uniq -c | sort -nr | head -10
}

# Function to update progress
update_progress() {
    local current_errors=$1
    local fixed_count=$((BASELINE_ERRORS - current_errors))
    local progress_percent=$((fixed_count * 100 / BASELINE_ERRORS))
    
    echo "=== HIVE MIND PROGRESS UPDATE ==="
    echo "Timestamp: $(date)"
    echo "Starting errors: $BASELINE_ERRORS"
    echo "Current errors: $current_errors"
    echo "Fixed: $fixed_count"
    echo "Progress: $progress_percent%"
    echo "=================================="
    
    # Log to file
    echo "$(date),$current_errors,$fixed_count,$progress_percent" >> "$LOG_DIR/progress.csv"
}

# Function to check if ready for tests
check_test_readiness() {
    local current_errors=$1
    if [ "$current_errors" -lt 20 ]; then
        echo "🎯 READY FOR TESTING! Errors dropped below 20 threshold."
        return 0
    else
        echo "⚠️  Not ready for testing. Need to reduce errors below 20 (currently: $current_errors)"
        return 1
    fi
}

# Main monitoring loop
if [ "$1" = "continuous" ]; then
    echo "Starting continuous monitoring..."
    while true; do
        current_errors=$(count_errors)
        update_progress "$current_errors"
        
        if check_test_readiness "$current_errors"; then
            echo "🚀 Triggering test validation..."
            ./scripts/validate-fixes.sh
        fi
        
        echo "Waiting 60 seconds before next check..."
        sleep 60
    done
else
    # Single check
    current_errors=$(count_errors)
    update_progress "$current_errors"
    check_test_readiness "$current_errors"
    
    echo -e "\nTop error types:"
    get_error_breakdown
fi