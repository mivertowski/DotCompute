#!/bin/bash
# RTX 2000 Ada Generation Hardware Test Runner
# Comprehensive hardware validation and performance testing script

set -e

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
TEST_PROJECT="$PROJECT_ROOT/tests/Hardware/DotCompute.Hardware.RTX2000.Tests"
RESULTS_DIR="$PROJECT_ROOT/hardware-test-results"
BASELINE_FILE="$RESULTS_DIR/rtx2000_baseline.json"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}RTX 2000 Ada Generation Hardware Test Suite${NC}"
echo "=============================================="

# Check prerequisites
check_prerequisites() {
    echo -e "${BLUE}Checking prerequisites...${NC}"
    
    # Check if CUDA is available
    if command -v nvidia-smi &> /dev/null; then
        echo "✓ NVIDIA driver found"
        nvidia-smi --query-gpu=name,memory.total,compute_cap --format=csv,noheader,nounits
    else
        echo -e "${RED}✗ NVIDIA driver not found${NC}"
        exit 1
    fi
    
    # Check CUDA toolkit
    if [ -d "/usr/local/cuda" ] || [ -d "/opt/cuda" ] || command -v nvcc &> /dev/null; then
        echo "✓ CUDA toolkit found"
        if command -v nvcc &> /dev/null; then
            nvcc --version | grep "release"
        fi
    else
        echo -e "${YELLOW}⚠ CUDA toolkit not found - some tests may be skipped${NC}"
    fi
    
    # Check .NET
    if command -v dotnet &> /dev/null; then
        echo "✓ .NET runtime found"
        dotnet --version
    else
        echo -e "${RED}✗ .NET runtime not found${NC}"
        exit 1
    fi
    
    echo ""
}

# Create results directory
setup_results_dir() {
    mkdir -p "$RESULTS_DIR"
    echo "Results will be saved to: $RESULTS_DIR"
    echo ""
}

# Build test project
build_tests() {
    echo -e "${BLUE}Building RTX 2000 test project...${NC}"
    cd "$TEST_PROJECT"
    
    if dotnet build --configuration Release; then
        echo -e "${GREEN}✓ Build successful${NC}"
    else
        echo -e "${RED}✗ Build failed${NC}"
        exit 1
    fi
    echo ""
}

# Run hardware validation tests
run_hardware_validation() {
    echo -e "${BLUE}Running hardware validation tests...${NC}"
    
    local test_results="$RESULTS_DIR/hardware_validation_$(date +%Y%m%d_%H%M%S).xml"
    
    dotnet test \
        --configuration Release \
        --logger "trx;LogFileName=$test_results" \
        --filter "Category=HardwareValidation" \
        --verbosity normal
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Hardware validation tests passed${NC}"
    else
        echo -e "${YELLOW}⚠ Some hardware validation tests failed - check results${NC}"
    fi
    echo ""
}

# Run NVRTC compilation tests
run_nvrtc_tests() {
    echo -e "${BLUE}Running NVRTC compilation tests...${NC}"
    
    local test_results="$RESULTS_DIR/nvrtc_tests_$(date +%Y%m%d_%H%M%S).xml"
    
    dotnet test \
        --configuration Release \
        --logger "trx;LogFileName=$test_results" \
        --filter "Category=NVRTC" \
        --verbosity normal
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ NVRTC compilation tests passed${NC}"
    else
        echo -e "${YELLOW}⚠ Some NVRTC tests failed - check results${NC}"
    fi
    echo ""
}

# Run performance benchmarks
run_performance_benchmarks() {
    echo -e "${BLUE}Running performance benchmarks...${NC}"
    
    local test_results="$RESULTS_DIR/performance_benchmarks_$(date +%Y%m%d_%H%M%S).xml"
    
    # Set higher timeout for performance tests
    dotnet test \
        --configuration Release \
        --logger "trx;LogFileName=$test_results" \
        --filter "Category=Performance" \
        --verbosity normal \
        -- RunConfiguration.TestSessionTimeout=600000
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Performance benchmarks completed${NC}"
    else
        echo -e "${YELLOW}⚠ Some performance benchmarks failed - check results${NC}"
    fi
    echo ""
}

# Run integration tests
run_integration_tests() {
    echo -e "${BLUE}Running end-to-end integration tests...${NC}"
    
    local test_results="$RESULTS_DIR/integration_tests_$(date +%Y%m%d_%H%M%S).xml"
    
    dotnet test \
        --configuration Release \
        --logger "trx;LogFileName=$test_results" \
        --filter "Category=Integration" \
        --verbosity normal
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Integration tests passed${NC}"
    else
        echo -e "${YELLOW}⚠ Some integration tests failed - check results${NC}"
    fi
    echo ""
}

# Run stress tests (optional)
run_stress_tests() {
    echo -e "${BLUE}Running stress tests (this may take several minutes)...${NC}"
    read -p "Run stress tests? This will heavily load the GPU for several minutes. [y/N]: " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        local test_results="$RESULTS_DIR/stress_tests_$(date +%Y%m%d_%H%M%S).xml"
        
        dotnet test \
            --configuration Release \
            --logger "trx;LogFileName=$test_results" \
            --filter "Category=StressTest" \
            --verbosity normal \
            -- RunConfiguration.TestSessionTimeout=1800000  # 30 minutes
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ Stress tests completed successfully${NC}"
        else
            echo -e "${YELLOW}⚠ Some stress tests failed - this may indicate thermal throttling or stability issues${NC}"
        fi
    else
        echo "Stress tests skipped"
    fi
    echo ""
}

# Generate summary report
generate_summary() {
    echo -e "${BLUE}Generating test summary...${NC}"
    
    local summary_file="$RESULTS_DIR/test_summary_$(date +%Y%m%d_%H%M%S).txt"
    
    cat > "$summary_file" << EOF
RTX 2000 Ada Generation Hardware Test Summary
============================================
Date: $(date)
System: $(uname -a)
GPU: $(nvidia-smi --query-gpu=name --format=csv,noheader,nounits)
Driver Version: $(nvidia-smi --query-gpu=driver_version --format=csv,noheader,nounits)
CUDA Version: $(nvidia-smi --query-gpu=cuda_version --format=csv,noheader,nounits)

Test Results:
EOF
    
    # Count test result files
    local validation_count=$(find "$RESULTS_DIR" -name "*hardware_validation*.xml" -newer "$PROJECT_ROOT" 2>/dev/null | wc -l)
    local nvrtc_count=$(find "$RESULTS_DIR" -name "*nvrtc_tests*.xml" -newer "$PROJECT_ROOT" 2>/dev/null | wc -l)
    local performance_count=$(find "$RESULTS_DIR" -name "*performance_benchmarks*.xml" -newer "$PROJECT_ROOT" 2>/dev/null | wc -l)
    local integration_count=$(find "$RESULTS_DIR" -name "*integration_tests*.xml" -newer "$PROJECT_ROOT" 2>/dev/null | wc -l)
    local stress_count=$(find "$RESULTS_DIR" -name "*stress_tests*.xml" -newer "$PROJECT_ROOT" 2>/dev/null | wc -l)
    
    cat >> "$summary_file" << EOF
- Hardware Validation: $validation_count test run(s)
- NVRTC Compilation: $nvrtc_count test run(s)
- Performance Benchmarks: $performance_count test run(s)
- Integration Tests: $integration_count test run(s)
- Stress Tests: $stress_count test run(s)

All detailed results are available in: $RESULTS_DIR

EOF
    
    # Check for baseline file
    if [ -f "$BASELINE_FILE" ]; then
        echo "Performance baseline exists: $BASELINE_FILE" >> "$summary_file"
    else
        echo "No performance baseline found - consider running baseline creation" >> "$summary_file"
    fi
    
    echo "Summary saved to: $summary_file"
    cat "$summary_file"
}

# Create performance baseline
create_baseline() {
    echo -e "${BLUE}Creating performance baseline...${NC}"
    read -p "Create new performance baseline? This will overwrite existing baseline. [y/N]: " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        dotnet test \
            --configuration Release \
            --filter "FullyQualifiedName~SavePerformanceBaseline" \
            --verbosity normal
        
        if [ -f "$BASELINE_FILE" ]; then
            echo -e "${GREEN}✓ Performance baseline created: $BASELINE_FILE${NC}"
        else
            echo -e "${YELLOW}⚠ Baseline creation may have failed - check test output${NC}"
        fi
    else
        echo "Baseline creation skipped"
    fi
    echo ""
}

# Main execution
main() {
    check_prerequisites
    setup_results_dir
    build_tests
    
    # Run test suites
    run_hardware_validation
    run_nvrtc_tests
    run_performance_benchmarks
    run_integration_tests
    run_stress_tests
    
    # Generate reports
    generate_summary
    create_baseline
    
    echo -e "${GREEN}RTX 2000 Ada Generation hardware testing completed!${NC}"
    echo "Check $RESULTS_DIR for detailed results and reports."
}

# Handle script arguments
case "${1:-}" in
    "validation")
        check_prerequisites
        setup_results_dir
        build_tests
        run_hardware_validation
        ;;
    "nvrtc")
        check_prerequisites
        setup_results_dir
        build_tests
        run_nvrtc_tests
        ;;
    "performance")
        check_prerequisites
        setup_results_dir
        build_tests
        run_performance_benchmarks
        ;;
    "integration")
        check_prerequisites
        setup_results_dir
        build_tests
        run_integration_tests
        ;;
    "stress")
        check_prerequisites
        setup_results_dir
        build_tests
        run_stress_tests
        ;;
    "baseline")
        check_prerequisites
        setup_results_dir
        build_tests
        create_baseline
        ;;
    "help"|"-h"|"--help")
        echo "Usage: $0 [test-type]"
        echo ""
        echo "Test types:"
        echo "  validation   - Run hardware validation tests only"
        echo "  nvrtc        - Run NVRTC compilation tests only"
        echo "  performance  - Run performance benchmarks only"
        echo "  integration  - Run integration tests only"
        echo "  stress       - Run stress tests only"
        echo "  baseline     - Create performance baseline only"
        echo "  (no args)    - Run full test suite"
        echo ""
        echo "Examples:"
        echo "  $0                    # Run full test suite"
        echo "  $0 validation         # Run only validation tests"
        echo "  $0 performance        # Run only performance benchmarks"
        ;;
    *)
        main
        ;;
esac