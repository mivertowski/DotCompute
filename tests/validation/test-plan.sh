#!/bin/bash

# DotCompute Test Execution Plan
# Orchestrates test execution with impact analysis

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
TEST_RESULTS_FILE="$RESULTS_DIR/test_results_$TIMESTAMP.json"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}=== DotCompute Test Execution Plan ===${NC}"
echo "Starting test run at $(date)"
echo ""

# Test execution order (fast to slow)
declare -A TEST_SUITES=(
    ["unit_abstractions"]="tests/Unit/DotCompute.Abstractions.Tests"
    ["unit_memory"]="tests/Unit/DotCompute.Memory.Tests"
    ["unit_core"]="tests/Unit/DotCompute.Core.Tests"
    ["unit_plugins"]="tests/Unit/DotCompute.Plugins.Tests"
    ["unit_cpu"]="tests/Unit/DotCompute.Backends.CPU.Tests"
    ["unit_basic"]="tests/Unit/DotCompute.BasicTests"
    ["integration_generators"]="tests/Integration/DotCompute.Generators.Integration.Tests"
    ["hardware_mock"]="tests/Hardware/DotCompute.Hardware.Mock.Tests"
    ["hardware_cuda"]="tests/Hardware/DotCompute.Hardware.Cuda.Tests"
)

# Test categories
FAST_TESTS=("unit_abstractions" "unit_memory")
MEDIUM_TESTS=("unit_core" "unit_plugins" "unit_cpu" "unit_basic")
SLOW_TESTS=("integration_generators")
HARDWARE_TESTS=("hardware_mock" "hardware_cuda")

# Statistics
total_tests=0
passed_tests=0
failed_tests=0
skipped_tests=0
total_duration=0

results='[]'

# Function to run a test suite
run_test_suite() {
    local suite_name="$1"
    local suite_path="$2"
    local full_path="$PROJECT_ROOT/$suite_path"

    if [ ! -d "$full_path" ]; then
        echo -e "${YELLOW}⊘ Suite not found: $suite_name${NC}"
        return 1
    fi

    echo -e "${BLUE}Running: ${NC}$suite_name"

    local test_output=$(mktemp)
    local start_time=$(date +%s)

    # Run tests with detailed output
    if dotnet test "$full_path" \
        --no-build \
        --configuration Release \
        --logger "trx;LogFileName=test_results.trx" \
        --verbosity quiet \
        > "$test_output" 2>&1; then

        local end_time=$(date +%s)
        local duration=$((end_time - start_time))

        # Parse test results
        local passed=$(grep -oP 'Passed:\s+\K\d+' "$test_output" || echo "0")
        local failed=$(grep -oP 'Failed:\s+\K\d+' "$test_output" || echo "0")
        local skipped=$(grep -oP 'Skipped:\s+\K\d+' "$test_output" || echo "0")
        local total=$(grep -oP 'Total:\s+\K\d+' "$test_output" || echo "0")

        echo -e "${GREEN}✓ Passed${NC} - $passed/$total tests in ${duration}s"

        # Add to results
        results=$(echo "$results" | jq \
            --arg name "$suite_name" \
            --arg path "$suite_path" \
            --argjson passed "$passed" \
            --argjson failed "$failed" \
            --argjson skipped "$skipped" \
            --argjson total "$total" \
            --argjson duration "$duration" \
            '. + [{
                "suite": $name,
                "path": $path,
                "status": "success",
                "passed": $passed,
                "failed": $failed,
                "skipped": $skipped,
                "total": $total,
                "duration": $duration,
                "timestamp": now
            }]')

        ((total_tests += total))
        ((passed_tests += passed))
        ((failed_tests += failed))
        ((skipped_tests += skipped))
        ((total_duration += duration))

        rm -f "$test_output"
        return 0
    else
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))

        echo -e "${RED}✗ Failed${NC}"
        echo "Error output:"
        tail -20 "$test_output" | sed 's/^/  /'

        # Add to results
        results=$(echo "$results" | jq \
            --arg name "$suite_name" \
            --arg path "$suite_path" \
            --arg error "$(cat "$test_output" | tail -50)" \
            --argjson duration "$duration" \
            '. + [{
                "suite": $name,
                "path": $path,
                "status": "failed",
                "error": $error,
                "duration": $duration,
                "timestamp": now
            }]')

        rm -f "$test_output"
        return 1
    fi
}

# Function to estimate test duration
estimate_duration() {
    local category="$1"

    case "$category" in
        "fast") echo "< 10s per suite" ;;
        "medium") echo "10-30s per suite" ;;
        "slow") echo "30s-2min per suite" ;;
        "hardware") echo "2-5min per suite" ;;
    esac
}

# Function to check hardware availability
check_hardware() {
    echo -e "${YELLOW}Checking hardware availability...${NC}"

    # Check for NVIDIA GPU
    if command -v nvidia-smi &> /dev/null; then
        echo -e "${GREEN}✓ NVIDIA GPU detected${NC}"
        nvidia-smi --query-gpu=name,driver_version,compute_cap --format=csv,noheader
        return 0
    else
        echo -e "${YELLOW}⊘ No NVIDIA GPU detected - hardware tests will be skipped${NC}"
        return 1
    fi
}

# Main test execution
echo -e "${YELLOW}Phase 1: Fast Unit Tests${NC}"
echo "Estimated duration: $(estimate_duration fast)"
echo ""

for suite in "${FAST_TESTS[@]}"; do
    run_test_suite "$suite" "${TEST_SUITES[$suite]}" || true
done

echo -e "\n${YELLOW}Phase 2: Medium Unit Tests${NC}"
echo "Estimated duration: $(estimate_duration medium)"
echo ""

for suite in "${MEDIUM_TESTS[@]}"; do
    run_test_suite "$suite" "${TEST_SUITES[$suite]}" || true
done

echo -e "\n${YELLOW}Phase 3: Integration Tests${NC}"
echo "Estimated duration: $(estimate_duration slow)"
echo ""

for suite in "${SLOW_TESTS[@]}"; do
    run_test_suite "$suite" "${TEST_SUITES[$suite]}" || true
done

echo -e "\n${YELLOW}Phase 4: Hardware Tests${NC}"
echo "Estimated duration: $(estimate_duration hardware)"

if check_hardware; then
    echo ""
    for suite in "${HARDWARE_TESTS[@]}"; do
        run_test_suite "$suite" "${TEST_SUITES[$suite]}" || true
    done
else
    echo -e "${YELLOW}Skipping hardware tests${NC}"
fi

# Generate summary
echo -e "\n${BLUE}=== Test Execution Summary ===${NC}"
echo "Total Tests: $total_tests"
echo -e "Passed: ${GREEN}$passed_tests${NC}"
echo -e "Failed: ${RED}$failed_tests${NC}"
echo -e "Skipped: ${YELLOW}$skipped_tests${NC}"
echo "Total Duration: ${total_duration}s"

# Calculate pass rate
if [ $total_tests -gt 0 ]; then
    pass_rate=$((passed_tests * 100 / total_tests))
    echo "Pass Rate: ${pass_rate}%"
fi

# Save results
summary=$(jq -n \
    --argjson total "$total_tests" \
    --argjson passed "$passed_tests" \
    --argjson failed "$failed_tests" \
    --argjson skipped "$skipped_tests" \
    --argjson duration "$total_duration" \
    --argjson suites "$results" \
    '{
        "timestamp": now,
        "summary": {
            "total_tests": $total,
            "passed": $passed,
            "failed": $failed,
            "skipped": $skipped,
            "duration": $duration,
            "pass_rate": ($passed / $total * 100)
        },
        "test_suites": $suites
    }')

echo "$summary" | jq . > "$TEST_RESULTS_FILE"
echo -e "\nResults saved to: $TEST_RESULTS_FILE"

# Exit with appropriate code
if [ $failed_tests -gt 0 ]; then
    exit 1
else
    exit 0
fi
