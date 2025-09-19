#!/bin/bash

# Continuous Validation Pipeline - DotCompute Hive Mind Testing Agent
# Purpose: Automated continuous testing pipeline for development workflow

set -e

echo "🔄 DotCompute Continuous Validation Pipeline"
echo "============================================="

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PIPELINE_STATE_FILE=".swarm/pipeline-state.json"
WATCH_INTERVAL=30  # seconds

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_pipeline() { echo -e "${PURPLE}[PIPELINE]${NC} $1"; }

# Initialize pipeline state
init_pipeline() {
    mkdir -p .swarm
    local initial_state="{
        \"status\": \"initializing\",
        \"last_run\": \"$(date -Iseconds)\",
        \"last_commit\": \"$(git rev-parse HEAD 2>/dev/null || echo 'unknown')\",
        \"consecutive_passes\": 0,
        \"consecutive_failures\": 0,
        \"total_runs\": 0
    }"
    echo "$initial_state" > "$PIPELINE_STATE_FILE"
    log_pipeline "Pipeline state initialized"
}

# Update pipeline state
update_pipeline_state() {
    local status="$1"
    local current_commit="$(git rev-parse HEAD 2>/dev/null || echo 'unknown')"

    if [ -f "$PIPELINE_STATE_FILE" ]; then
        local last_commit=$(grep -o '"last_commit": "[^"]*"' "$PIPELINE_STATE_FILE" | cut -d'"' -f4)
        local consecutive_passes=$(grep -o '"consecutive_passes": [0-9]*' "$PIPELINE_STATE_FILE" | cut -d':' -f2 | tr -d ' ')
        local consecutive_failures=$(grep -o '"consecutive_failures": [0-9]*' "$PIPELINE_STATE_FILE" | cut -d':' -f2 | tr -d ' ')
        local total_runs=$(grep -o '"total_runs": [0-9]*' "$PIPELINE_STATE_FILE" | cut -d':' -f2 | tr -d ' ')

        # Update counters
        total_runs=$((total_runs + 1))

        if [ "$status" = "passing" ]; then
            consecutive_passes=$((consecutive_passes + 1))
            consecutive_failures=0
        else
            consecutive_failures=$((consecutive_failures + 1))
            consecutive_passes=0
        fi

        # Create new state
        local new_state="{
            \"status\": \"$status\",
            \"last_run\": \"$(date -Iseconds)\",
            \"last_commit\": \"$current_commit\",
            \"consecutive_passes\": $consecutive_passes,
            \"consecutive_failures\": $consecutive_failures,
            \"total_runs\": $total_runs,
            \"commit_changed\": $([ "$current_commit" != "$last_commit" ] && echo "true" || echo "false")
        }"

        echo "$new_state" > "$PIPELINE_STATE_FILE"
    fi
}

# Execute validation cycle
run_validation_cycle() {
    log_pipeline "Starting validation cycle #$(date +%s)"

    # Stage 1: Quick build check
    log_info "Stage 1: Build validation"
    if timeout 120 dotnet build DotCompute.sln --configuration Release --verbosity minimal > /dev/null 2>&1; then
        log_success "✅ Build PASSED"
        BUILD_RESULT="PASS"
    else
        log_error "❌ Build FAILED"
        BUILD_RESULT="FAIL"
        # Alert immediately on build failures
        npx claude-flow@alpha hooks notify --message "🚨 PIPELINE: Build FAILED - immediate intervention required"
        update_pipeline_state "failing"
        return 1
    fi

    # Stage 2: Fast unit tests (subset)
    log_info "Stage 2: Fast unit test subset"
    local fast_test_result
    if fast_test_result=$(timeout 60 dotnet test --filter "TestCategory!=Slow&TestCategory!=Hardware" --no-build --verbosity minimal --configuration Release 2>&1); then
        local test_count=$(echo "$fast_test_result" | grep -o "Passed: [0-9]*" | head -1 | cut -d' ' -f2)
        log_success "✅ Fast tests PASSED ($test_count tests)"
        FAST_TEST_RESULT="PASS"
    else
        log_error "❌ Fast tests FAILED"
        FAST_TEST_RESULT="FAIL"
        echo "$fast_test_result" | tail -10
    fi

    # Stage 3: Static analysis (if available)
    log_info "Stage 3: Static analysis"
    if command -v dotnet-format &> /dev/null; then
        if dotnet format --verify-no-changes --verbosity minimal > /dev/null 2>&1; then
            log_success "✅ Code format OK"
            FORMAT_RESULT="PASS"
        else
            log_warning "⚠️  Code format issues detected"
            FORMAT_RESULT="WARN"
        fi
    else
        log_info "dotnet-format not available, skipping"
        FORMAT_RESULT="SKIP"
    fi

    # Determine overall result
    if [ "$BUILD_RESULT" = "PASS" ] && [ "$FAST_TEST_RESULT" = "PASS" ]; then
        log_success "🎉 Validation cycle PASSED"
        update_pipeline_state "passing"

        # Store success in hive memory
        npx claude-flow@alpha memory store "hive/tests/pipeline-success" "Validation passed at $(date -Iseconds): Build=PASS, FastTests=PASS, Format=$FORMAT_RESULT"

        return 0
    else
        log_error "💥 Validation cycle FAILED"
        update_pipeline_state "failing"

        # Alert on failures
        npx claude-flow@alpha hooks notify --message "PIPELINE: Validation FAILED - Build=$BUILD_RESULT, Tests=$FAST_TEST_RESULT"

        return 1
    fi
}

# Full validation run (deeper testing)
run_full_validation() {
    log_pipeline "Running FULL validation suite"

    # Use the comprehensive test validation script
    if [ -f "$SCRIPT_DIR/test-validation.sh" ]; then
        if "$SCRIPT_DIR/test-validation.sh"; then
            log_success "🏆 FULL validation PASSED"
            npx claude-flow@alpha memory store "hive/tests/full-validation" "Full validation PASSED at $(date -Iseconds)"
            return 0
        else
            log_error "💥 FULL validation FAILED"
            npx claude-flow@alpha hooks notify --message "🚨 PIPELINE: FULL validation FAILED - comprehensive issues detected"
            return 1
        fi
    else
        log_warning "Full validation script not found, running basic tests"
        run_validation_cycle
    fi
}

# Watch mode for continuous monitoring
watch_mode() {
    log_pipeline "Starting continuous watch mode (interval: ${WATCH_INTERVAL}s)"

    local last_commit=""
    local watch_count=0

    while true; do
        watch_count=$((watch_count + 1))
        current_commit="$(git rev-parse HEAD 2>/dev/null || echo 'unknown')"

        log_pipeline "Watch cycle #$watch_count (commit: ${current_commit:0:8})"

        # Check if code changed
        if [ "$current_commit" != "$last_commit" ]; then
            log_info "🔄 Code change detected, running validation"
            run_validation_cycle
        else
            log_info "📊 No changes, running quick health check"
            # Quick health check without full validation
            if timeout 30 dotnet build DotCompute.sln --configuration Release --verbosity minimal > /dev/null 2>&1; then
                log_info "✅ Health check OK"
            else
                log_warning "❌ Health check failed, running full validation"
                run_validation_cycle
            fi
        fi

        last_commit="$current_commit"

        # Show pipeline statistics
        if [ -f "$PIPELINE_STATE_FILE" ]; then
            local status=$(grep -o '"status": "[^"]*"' "$PIPELINE_STATE_FILE" | cut -d'"' -f4)
            local passes=$(grep -o '"consecutive_passes": [0-9]*' "$PIPELINE_STATE_FILE" | cut -d':' -f2 | tr -d ' ')
            local failures=$(grep -o '"consecutive_failures": [0-9]*' "$PIPELINE_STATE_FILE" | cut -d':' -f2 | tr -d ' ')

            echo "📈 Pipeline: Status=$status, Passes=$passes, Failures=$failures"
        fi

        # Sleep until next cycle
        log_info "💤 Sleeping for ${WATCH_INTERVAL}s..."
        sleep "$WATCH_INTERVAL"
    done
}

# Pipeline control functions
show_status() {
    if [ -f "$PIPELINE_STATE_FILE" ]; then
        log_info "Current pipeline status:"
        cat "$PIPELINE_STATE_FILE" | jq . 2>/dev/null || cat "$PIPELINE_STATE_FILE"
    else
        log_warning "No pipeline state found"
    fi
}

reset_pipeline() {
    log_info "Resetting pipeline state"
    rm -f "$PIPELINE_STATE_FILE"
    init_pipeline
    log_success "Pipeline reset complete"
}

# Main command handling
main() {
    cd "$PROJECT_ROOT"

    case "${1:-cycle}" in
        "init")
            init_pipeline
            ;;
        "cycle")
            run_validation_cycle
            ;;
        "full")
            run_full_validation
            ;;
        "watch")
            init_pipeline
            watch_mode
            ;;
        "status")
            show_status
            ;;
        "reset")
            reset_pipeline
            ;;
        *)
            echo "DotCompute Continuous Validation Pipeline"
            echo "Usage: $0 [command]"
            echo ""
            echo "Commands:"
            echo "  init    - Initialize pipeline state"
            echo "  cycle   - Run single validation cycle (default)"
            echo "  full    - Run comprehensive validation"
            echo "  watch   - Start continuous monitoring"
            echo "  status  - Show pipeline status"
            echo "  reset   - Reset pipeline state"
            echo ""
            echo "Examples:"
            echo "  $0 cycle          # Quick validation"
            echo "  $0 full           # Comprehensive validation"
            echo "  $0 watch          # Continuous monitoring"
            exit 1
            ;;
    esac
}

# Execute main function with all arguments
main "$@"