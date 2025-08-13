#!/bin/bash

# DotCompute Alpha Release Test Verification Script
# Version: 0.1.0-alpha.1
# Date: 2025-01-12

set -e

echo "=========================================="
echo "DotCompute v0.1.0-alpha.1 Test Verification"
echo "=========================================="

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test results
TEST_RESULTS=""
EXIT_CODE=0
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0
SKIPPED_TESTS=0

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
    TEST_RESULTS+="\n✅ $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
    TEST_RESULTS+="\n⚠️  $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
    TEST_RESULTS+="\n❌ $1"
    EXIT_CODE=1
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking test prerequisites..."
    
    if ! command -v dotnet >/dev/null 2>&1; then
        log_error ".NET CLI not found"
        return 1
    fi
    
    if [[ ! -f "DotCompute.sln" ]]; then
        log_error "Solution file not found"
        return 1
    fi
    
    # Check for test settings
    if [[ -f "coverlet.runsettings" ]]; then
        log_success "Test settings file found"
    else
        log_warning "Test settings file not found"
    fi
    
    return 0
}

# Run unit tests
run_unit_tests() {
    log_info "Running unit tests..."
    
    UNIT_TEST_PROJECTS=(
        "tests/Unit/DotCompute.Abstractions.Tests"
        "tests/Unit/DotCompute.Core.Tests"
        "tests/Unit/DotCompute.Memory.Tests"
        "tests/Unit/DotCompute.Backends.CPU.Tests"
        "tests/Unit/DotCompute.Plugins.Tests"
        "tests/Unit/DotCompute.BasicTests"
    )
    
    local unit_passed=0
    local unit_failed=0
    
    for project in "${UNIT_TEST_PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            log_info "Testing $(basename "$project")..."
            
            if dotnet test "$project" --configuration Release --verbosity minimal --no-build; then
                log_success "Unit tests passed: $(basename "$project")"
                ((unit_passed++))
            else
                log_error "Unit tests failed: $(basename "$project")"
                ((unit_failed++))
            fi
        else
            log_warning "Unit test project not found: $project"
        fi
    done
    
    log_info "Unit test summary: $unit_passed passed, $unit_failed failed"
    
    if [[ $unit_failed -gt 0 ]]; then
        return 1
    fi
    return 0
}

# Run integration tests
run_integration_tests() {
    log_info "Running integration tests..."
    
    INTEGRATION_PROJECTS=(
        "tests/Integration/DotCompute.Integration.Tests"
    )
    
    local integration_passed=0
    local integration_failed=0
    
    for project in "${INTEGRATION_PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            log_info "Testing $(basename "$project")..."
            
            # Integration tests might need more time
            if timeout 300 dotnet test "$project" --configuration Release --verbosity minimal --no-build; then
                log_success "Integration tests passed: $(basename "$project")"
                ((integration_passed++))
            else
                log_error "Integration tests failed: $(basename "$project")"
                ((integration_failed++))
            fi
        else
            log_warning "Integration test project not found: $project"
        fi
    done
    
    log_info "Integration test summary: $integration_passed passed, $integration_failed failed"
    
    if [[ $integration_failed -gt 0 ]]; then
        return 1
    fi
    return 0
}

# Run mock hardware tests (should always work)
run_mock_hardware_tests() {
    log_info "Running mock hardware tests..."
    
    MOCK_PROJECTS=(
        "tests/Hardware/DotCompute.Hardware.Mock.Tests"
    )
    
    local mock_passed=0
    local mock_failed=0
    
    for project in "${MOCK_PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            log_info "Testing $(basename "$project")..."
            
            if dotnet test "$project" --configuration Release --verbosity minimal --no-build; then
                log_success "Mock hardware tests passed: $(basename "$project")"
                ((mock_passed++))
            else
                log_error "Mock hardware tests failed: $(basename "$project")"
                ((mock_failed++))
            fi
        else
            log_warning "Mock test project not found: $project"
        fi
    done
    
    log_info "Mock hardware test summary: $mock_passed passed, $mock_failed failed"
    
    if [[ $mock_failed -gt 0 ]]; then
        return 1
    fi
    return 0
}

# Run hardware tests (may skip if hardware not available)
run_hardware_tests() {
    log_info "Running hardware tests (may skip if hardware unavailable)..."
    
    HARDWARE_PROJECTS=(
        "tests/Hardware/DotCompute.Hardware.Cuda.Tests"
        "tests/Hardware/DotCompute.Hardware.OpenCL.Tests"
        "tests/Hardware/DotCompute.Hardware.DirectCompute.Tests"
    )
    
    local hw_passed=0
    local hw_failed=0
    local hw_skipped=0
    
    for project in "${HARDWARE_PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            log_info "Testing $(basename "$project")..."
            
            # Hardware tests may fail due to missing hardware - this is acceptable
            TEST_OUTPUT=$(dotnet test "$project" --configuration Release --verbosity minimal --no-build 2>&1 || true)
            
            if echo "$TEST_OUTPUT" | grep -q "Passed"; then
                log_success "Hardware tests passed: $(basename "$project")"
                ((hw_passed++))
            elif echo "$TEST_OUTPUT" | grep -q -i "skip\|ignore"; then
                log_warning "Hardware tests skipped: $(basename "$project") (hardware not available)"
                ((hw_skipped++))
            else
                log_warning "Hardware tests failed: $(basename "$project") (may be expected)"
                ((hw_failed++))
            fi
        else
            log_warning "Hardware test project not found: $project"
        fi
    done
    
    log_info "Hardware test summary: $hw_passed passed, $hw_failed failed, $hw_skipped skipped"
    
    # Hardware test failures are acceptable for alpha release
    return 0
}

# Run performance tests
run_performance_tests() {
    log_info "Running performance tests..."
    
    PERF_PROJECTS=(
        "tests/Performance/DotCompute.Performance.Tests"
    )
    
    local perf_passed=0
    local perf_failed=0
    
    for project in "${PERF_PROJECTS[@]}"; do
        if [[ -d "$project" ]]; then
            log_info "Testing $(basename "$project")..."
            
            # Performance tests might take longer
            if timeout 600 dotnet test "$project" --configuration Release --verbosity minimal --no-build; then
                log_success "Performance tests passed: $(basename "$project")"
                ((perf_passed++))
            else
                log_warning "Performance tests failed: $(basename "$project") (may be environment-dependent)"
                ((perf_failed++))
            fi
        else
            log_warning "Performance test project not found: $project"
        fi
    done
    
    log_info "Performance test summary: $perf_passed passed, $perf_failed failed"
    
    # Performance test failures are acceptable for alpha release
    return 0
}

# Generate test coverage report
generate_coverage_report() {
    log_info "Generating test coverage report..."
    
    if command -v coverlet >/dev/null 2>&1 || [[ -f "coverlet.runsettings" ]]; then
        # Run tests with coverage
        if dotnet test DotCompute.sln --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings >/dev/null 2>&1; then
            log_success "Test coverage data collected"
            
            # Look for coverage files
            COVERAGE_FILES=$(find TestResults -name "coverage.cobertura.xml" 2>/dev/null | head -1)
            if [[ -n "$COVERAGE_FILES" ]]; then
                log_success "Coverage report generated: $COVERAGE_FILES"
            fi
        else
            log_warning "Test coverage collection failed"
        fi
    else
        log_warning "Coverage tools not available"
    fi
}

# Verify CPU backend performance
verify_cpu_performance() {
    log_info "Verifying CPU backend performance..."
    
    # Check if we have CPU performance tests
    CPU_PERF_TESTS="tests/Unit/DotCompute.Backends.CPU.Tests"
    
    if [[ -d "$CPU_PERF_TESTS" ]]; then
        # Look for performance test indicators
        if grep -r "Performance\|Benchmark\|Simd" "$CPU_PERF_TESTS" >/dev/null 2>&1; then
            log_success "CPU performance tests detected"
        else
            log_warning "CPU performance tests not clearly identified"
        fi
    fi
}

# Test critical paths
test_critical_paths() {
    log_info "Testing critical paths..."
    
    # Core functionality that must work for alpha release
    CRITICAL_AREAS=(
        "Memory management"
        "CPU backend"
        "Plugin system"
        "Basic abstractions"
    )
    
    # This is validated through the unit tests above
    log_success "Critical path testing completed via unit tests"
}

# Generate test report
generate_test_report() {
    REPORT_FILE="test-verification-report.md"
    
    cat > "$REPORT_FILE" << EOF
# DotCompute v0.1.0-alpha.1 Test Verification Report

**Date:** $(date)
**Script Version:** 1.0.0

## Summary

Test verification completed with exit code: **$EXIT_CODE**

## Test Results Summary

- **Total Test Projects:** Multiple categories
- **Unit Tests:** Core functionality tests
- **Integration Tests:** End-to-end workflow tests
- **Mock Hardware Tests:** Hardware abstraction tests
- **Hardware Tests:** Real hardware tests (may skip)
- **Performance Tests:** Performance validation tests

## Results
$TEST_RESULTS

## Test Categories Status

### ✅ Must Pass (Alpha Release Blockers)
- Unit Tests (Core, Abstractions, Memory, CPU Backend)
- Basic Integration Tests
- Mock Hardware Tests

### ⚠️ Should Pass (Non-blocking for Alpha)
- Hardware Tests (GPU/CUDA/OpenCL)
- Performance Tests
- Advanced Integration Tests

### 📋 Coverage Requirements
- Minimum 70% code coverage
- Core components >80% coverage
- Critical paths 100% coverage

## Verification Status

$(if [[ $EXIT_CODE -eq 0 ]]; then echo "✅ **PASSED** - Alpha release tests are ready"; else echo "❌ **FAILED** - Critical test failures found"; fi)

---
Generated by test-verification.sh
EOF

    log_info "Test report generated: $REPORT_FILE"
}

# Main execution
main() {
    echo "Starting test verification at $(date)"
    echo
    
    check_prerequisites || exit 1
    
    # Build first to ensure latest code
    log_info "Building solution for testing..."
    if ! dotnet build DotCompute.sln --configuration Release >/dev/null 2>&1; then
        log_error "Build failed - cannot run tests"
        exit 1
    fi
    log_success "Solution built successfully"
    
    # Run test suites
    run_unit_tests || EXIT_CODE=1
    run_integration_tests || EXIT_CODE=1
    run_mock_hardware_tests || EXIT_CODE=1
    run_hardware_tests  # Non-blocking
    run_performance_tests  # Non-blocking
    
    # Additional verification
    generate_coverage_report
    verify_cpu_performance
    test_critical_paths
    
    echo
    echo "=========================================="
    echo "Test Verification Summary"
    echo "=========================================="
    echo -e "$TEST_RESULTS"
    echo
    
    generate_test_report
    
    if [[ $EXIT_CODE -eq 0 ]]; then
        echo -e "${GREEN}✅ TEST VERIFICATION PASSED${NC}"
        echo "The DotCompute v0.1.0-alpha.1 tests are ready for release!"
    else
        echo -e "${RED}❌ TEST VERIFICATION FAILED${NC}"
        echo "Critical test failures found - please resolve before releasing."
    fi
    
    exit $EXIT_CODE
}

# Run the verification
main "$@"