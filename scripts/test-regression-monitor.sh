#!/bin/bash

# Test Regression Monitor - DotCompute Hive Mind Testing Agent
# Purpose: Monitor for test regressions during development and alert team

set -e

echo "🔍 DotCompute Test Regression Monitor"
echo "======================================"

# Configuration
BASELINE_FILE=".swarm/test-baseline.json"
CURRENT_RESULTS_FILE=".swarm/test-current.json"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Function to run quick smoke tests
run_smoke_tests() {
    log_info "Running smoke tests..."

    # Quick compilation check
    local build_result
    if build_result=$(dotnet build DotCompute.sln --configuration Release --verbosity minimal 2>&1); then
        echo "✅ Build: PASSING"
        BUILD_STATUS="PASSING"
    else
        echo "❌ Build: FAILING"
        BUILD_STATUS="FAILING"
        echo "$build_result" | head -20
    fi

    # Quick unit test sample
    if [ "$BUILD_STATUS" = "PASSING" ]; then
        local unit_test_result
        if unit_test_result=$(timeout 60 dotnet test --filter "Category=Unit" --no-build --verbosity minimal 2>&1); then
            echo "✅ Unit Tests: PASSING"
            UNIT_STATUS="PASSING"
        else
            echo "❌ Unit Tests: FAILING"
            UNIT_STATUS="FAILING"
            echo "$unit_test_result" | head -10
        fi
    else
        UNIT_STATUS="SKIPPED"
    fi
}

# Function to detect regressions
detect_regressions() {
    log_info "Analyzing for regressions..."

    # Create current test status
    CURRENT_STATUS="{
        \"timestamp\": \"$(date -Iseconds)\",
        \"build_status\": \"$BUILD_STATUS\",
        \"unit_status\": \"$UNIT_STATUS\",
        \"commit_hash\": \"$(git rev-parse HEAD 2>/dev/null || echo 'unknown')\",
        \"branch\": \"$(git branch --show-current 2>/dev/null || echo 'unknown')\"
    }"

    echo "$CURRENT_STATUS" > "$CURRENT_RESULTS_FILE"

    # Compare with baseline if it exists
    if [ -f "$BASELINE_FILE" ]; then
        local baseline_build=$(grep -o '"build_status": "[^"]*"' "$BASELINE_FILE" | cut -d'"' -f4)
        local baseline_unit=$(grep -o '"unit_status": "[^"]*"' "$BASELINE_FILE" | cut -d'"' -f4)

        log_info "Baseline: Build=$baseline_build, Unit=$baseline_unit"
        log_info "Current:  Build=$BUILD_STATUS, Unit=$UNIT_STATUS"

        # Detect regressions
        REGRESSION_DETECTED=false

        if [ "$baseline_build" = "PASSING" ] && [ "$BUILD_STATUS" = "FAILING" ]; then
            log_error "🚨 BUILD REGRESSION DETECTED!"
            REGRESSION_DETECTED=true
        fi

        if [ "$baseline_unit" = "PASSING" ] && [ "$UNIT_STATUS" = "FAILING" ]; then
            log_error "🚨 UNIT TEST REGRESSION DETECTED!"
            REGRESSION_DETECTED=true
        fi

        if [ "$REGRESSION_DETECTED" = true ]; then
            # Alert the hive mind
            npx claude-flow@alpha hooks notify --message "🚨 TESTER: REGRESSION DETECTED! Build: $baseline_build→$BUILD_STATUS, Unit: $baseline_unit→$UNIT_STATUS"
            npx claude-flow@alpha memory store "hive/tests/regression-alert" "REGRESSION: Baseline build=$baseline_build unit=$baseline_unit vs Current build=$BUILD_STATUS unit=$UNIT_STATUS at $(date)"

            log_error "Regression alert sent to hive mind!"
            return 1
        else
            log_success "No regressions detected"
        fi
    else
        log_warning "No baseline found - establishing current state as baseline"
        cp "$CURRENT_RESULTS_FILE" "$BASELINE_FILE"
    fi

    return 0
}

# Function to update baseline on successful runs
update_baseline() {
    if [ "$BUILD_STATUS" = "PASSING" ] && [ "$UNIT_STATUS" = "PASSING" ]; then
        log_info "Updating baseline with successful results"
        cp "$CURRENT_RESULTS_FILE" "$BASELINE_FILE"
        npx claude-flow@alpha memory store "hive/tests/baseline-update" "Updated baseline: build=PASSING unit=PASSING at $(date)"
    fi
}

# Main execution
main() {
    # Create .swarm directory if it doesn't exist
    mkdir -p .swarm

    log_info "Starting regression monitoring..."

    # Run smoke tests
    run_smoke_tests

    # Detect regressions
    if detect_regressions; then
        log_success "Regression check completed successfully"
        update_baseline
        exit 0
    else
        log_error "Regressions detected - intervention required"
        exit 1
    fi
}

# Handle script arguments
case "${1:-monitor}" in
    "monitor")
        main
        ;;
    "reset-baseline")
        log_info "Resetting baseline..."
        rm -f "$BASELINE_FILE"
        log_success "Baseline reset"
        ;;
    "show-status")
        if [ -f "$CURRENT_RESULTS_FILE" ]; then
            log_info "Current test status:"
            cat "$CURRENT_RESULTS_FILE" | jq . 2>/dev/null || cat "$CURRENT_RESULTS_FILE"
        else
            log_warning "No current status available"
        fi
        ;;
    *)
        echo "Usage: $0 [monitor|reset-baseline|show-status]"
        echo "  monitor        - Run regression monitoring (default)"
        echo "  reset-baseline - Reset the test baseline"
        echo "  show-status    - Show current test status"
        exit 1
        ;;
esac