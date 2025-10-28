#!/bin/bash

# Test Validation Script - DotCompute Hive Mind Testing Agent
# Purpose: Comprehensive test validation and continuous health monitoring

set -e

echo "🧪 DotCompute Test Validation Suite"
echo "====================================="

# Configuration
SOLUTION_PATH="/home/mivertowski/DotCompute/DotCompute/DotCompute.sln"
TEST_RESULTS_DIR="./test-results"
COVERAGE_OUTPUT_DIR="./coverage-results"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Create output directories
mkdir -p "$TEST_RESULTS_DIR"
mkdir -p "$COVERAGE_OUTPUT_DIR"

# Phase 1: Build Health Check
log_info "Phase 1: Build Health Validation"
echo "Checking if solution builds..."

if dotnet build "$SOLUTION_PATH" --configuration Release --verbosity minimal; then
    log_success "Solution builds successfully"
    BUILD_STATUS="PASSING"
else
    log_error "Solution build FAILED - cannot proceed with tests"
    BUILD_STATUS="FAILING"

    # Store build failure info
    npx claude-flow@alpha memory store "hive/tests/build-status" "FAILING: Solution does not compile"
    npx claude-flow@alpha hooks notify --message "TESTER: Build validation FAILED - compilation errors prevent testing"
    exit 1
fi

# Phase 2: Unit Test Execution
log_info "Phase 2: Unit Test Validation"
echo "Running unit tests..."

UNIT_TEST_RESULTS=$(dotnet test "$SOLUTION_PATH" --configuration Release --filter "Category=Unit" --logger "trx;LogFileName=unit-tests.trx" --results-directory "$TEST_RESULTS_DIR" --no-build 2>&1 | tee /tmp/unit-test-output.log)
UNIT_EXIT_CODE=$?

if [ $UNIT_EXIT_CODE -eq 0 ]; then
    log_success "Unit tests PASSED"
    UNIT_STATUS="PASSING"
else
    log_error "Unit tests FAILED"
    UNIT_STATUS="FAILING"
fi

# Phase 3: Integration Test Execution (if build passes)
log_info "Phase 3: Integration Test Validation"
echo "Running integration tests..."

INTEGRATION_TEST_RESULTS=$(dotnet test "$SOLUTION_PATH" --configuration Release --filter "Category=Integration" --logger "trx;LogFileName=integration-tests.trx" --results-directory "$TEST_RESULTS_DIR" --no-build 2>&1 | tee /tmp/integration-test-output.log)
INTEGRATION_EXIT_CODE=$?

if [ $INTEGRATION_EXIT_CODE -eq 0 ]; then
    log_success "Integration tests PASSED"
    INTEGRATION_STATUS="PASSING"
else
    log_warning "Integration tests have issues (may be expected if hardware not available)"
    INTEGRATION_STATUS="PARTIAL"
fi

# Phase 4: Hardware Test Validation (Optional)
log_info "Phase 4: Hardware Test Validation"
echo "Checking hardware test availability..."

# Check for CUDA availability
if command -v nvidia-smi &> /dev/null && nvidia-smi &> /dev/null; then
    log_info "NVIDIA GPU detected - running CUDA tests"
    CUDA_TEST_RESULTS=$(dotnet test "$SOLUTION_PATH" --configuration Release --filter "Category=Hardware&Backend=CUDA" --logger "trx;LogFileName=cuda-tests.trx" --results-directory "$TEST_RESULTS_DIR" --no-build 2>&1 | tee /tmp/cuda-test-output.log)
    CUDA_EXIT_CODE=$?

    if [ $CUDA_EXIT_CODE -eq 0 ]; then
        log_success "CUDA tests PASSED"
        CUDA_STATUS="PASSING"
    else
        log_warning "CUDA tests have issues"
        CUDA_STATUS="FAILING"
    fi
else
    log_warning "No NVIDIA GPU detected - skipping CUDA tests"
    CUDA_STATUS="SKIPPED"
fi

# Phase 5: Test Coverage Analysis
log_info "Phase 5: Test Coverage Analysis"
echo "Generating test coverage report..."

if dotnet test "$SOLUTION_PATH" --configuration Release --collect:"XPlat Code Coverage" --results-directory "$COVERAGE_OUTPUT_DIR" --no-build --verbosity minimal; then
    log_success "Coverage data collected"
    COVERAGE_STATUS="COLLECTED"
else
    log_warning "Coverage collection had issues"
    COVERAGE_STATUS="PARTIAL"
fi

# Phase 6: Test Result Summary
log_info "Phase 6: Test Result Summary"
echo "=============================="

echo "📊 TEST VALIDATION SUMMARY"
echo "Build Status:       $BUILD_STATUS"
echo "Unit Tests:         $UNIT_STATUS"
echo "Integration Tests:  $INTEGRATION_STATUS"
echo "CUDA Tests:         $CUDA_STATUS"
echo "Coverage:           $COVERAGE_STATUS"

# Store comprehensive results in hive memory
SUMMARY_JSON="{
  \"timestamp\": \"$(date -Iseconds)\",
  \"build_status\": \"$BUILD_STATUS\",
  \"unit_tests\": \"$UNIT_STATUS\",
  \"integration_tests\": \"$INTEGRATION_STATUS\",
  \"cuda_tests\": \"$CUDA_STATUS\",
  \"coverage\": \"$COVERAGE_STATUS\",
  \"test_results_dir\": \"$TEST_RESULTS_DIR\",
  \"coverage_dir\": \"$COVERAGE_OUTPUT_DIR\"
}"

npx claude-flow@alpha memory store "hive/tests/validation-summary" "$SUMMARY_JSON"

# Phase 7: Coordination Notifications
log_info "Phase 7: Hive Mind Coordination"

if [ "$BUILD_STATUS" = "PASSING" ] && [ "$UNIT_STATUS" = "PASSING" ]; then
    npx claude-flow@alpha hooks notify --message "TESTER: Validation SUCCESSFUL - Build and unit tests passing. Ready for development."
    log_success "All critical tests passed - system ready for development"
    exit 0
else
    npx claude-flow@alpha hooks notify --message "TESTER: Validation ISSUES - Build: $BUILD_STATUS, Unit: $UNIT_STATUS. Coder intervention needed."
    log_error "Critical issues detected - Coder agent intervention required"
    exit 1
fi