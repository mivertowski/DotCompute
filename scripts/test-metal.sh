#!/bin/bash

# Metal Test Execution Script
# Executes Metal-specific tests with proper hardware detection and scaling

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="$PROJECT_ROOT/TestResults/Metal"

# Test categories
UNIT_TESTS="Category=Unit&Category!=Hardware&Category!=GPU"
HARDWARE_TESTS="Category=Metal|Category=RequiresMetal"
PERFORMANCE_TESTS="Category=Performance&Category=Metal"
STRESS_TESTS="Category=Stress&Category=Metal"
ALL_METAL_TESTS="Category=Metal"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
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

# Hardware detection function
check_metal_support() {
    log_info "Checking Metal support..."
    
    # Check if running on macOS
    if [[ "$OSTYPE" != "darwin"* ]]; then
        log_error "Metal is only supported on macOS"
        return 1
    fi
    
    # Check macOS version
    local macos_version=$(sw_vers -productVersion)
    local macos_major=$(echo "$macos_version" | cut -d. -f1)
    local macos_minor=$(echo "$macos_version" | cut -d. -f2)
    
    log_info "macOS Version: $macos_version"
    
    if [[ $macos_major -lt 10 ]] || [[ $macos_major -eq 10 && $macos_minor -lt 13 ]]; then
        log_error "Metal compute shaders require macOS 10.13 (High Sierra) or later"
        return 1
    fi
    
    # Check architecture
    local arch=$(uname -m)
    log_info "Architecture: $arch"
    
    if [[ "$arch" == "arm64" ]]; then
        log_success "Running on Apple Silicon (unified memory architecture)"
    else
        log_info "Running on Intel Mac"
    fi
    
    return 0
}

# Memory scaling function
calculate_memory_scale() {
    local system_memory_gb
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS: Get total memory in bytes, convert to GB
        local memory_bytes=$(sysctl -n hw.memsize)
        system_memory_gb=$((memory_bytes / 1024 / 1024 / 1024))
    else
        # Fallback (shouldn't reach here for Metal tests)
        system_memory_gb=8
    fi
    
    log_info "System Memory: ${system_memory_gb}GB"
    
    # Scale test data based on available memory
    local scale_factor=1
    if [[ $system_memory_gb -ge 32 ]]; then
        scale_factor=4
    elif [[ $system_memory_gb -ge 16 ]]; then
        scale_factor=2
    elif [[ $system_memory_gb -lt 8 ]]; then
        scale_factor=1
        log_warning "Low system memory detected, using minimal test scale"
    fi
    
    log_info "Memory scale factor: ${scale_factor}x"
    echo "$scale_factor"
}

# Test execution function
run_test_category() {
    local category="$1"
    local filter="$2"
    local timeout="${3:-300000}"  # 5 minutes default
    
    log_info "Running $category tests..."
    log_info "Filter: $filter"
    
    local category_results_dir="$RESULTS_DIR/$category"
    mkdir -p "$category_results_dir"
    
    # Set environment variables for test scaling
    export DOTCOMPUTE_TEST_MEMORY_SCALE=$(calculate_memory_scale)
    export DOTCOMPUTE_TEST_TIMEOUT="$timeout"
    
    # Run tests with proper logging and coverage
    dotnet test \
        --filter "$filter" \
        --logger "trx;LogFileName=${category}_results.trx" \
        --logger "console;verbosity=normal" \
        --results-directory "$category_results_dir" \
        --collect:"XPlat Code Coverage" \
        --settings:"$PROJECT_ROOT/tests/test.runsettings" \
        -- TestRunParameters.Parameter\(name=\"TestTimeout\",value=\"$timeout\"\)
    
    local exit_code=$?
    
    if [[ $exit_code -eq 0 ]]; then
        log_success "$category tests completed successfully"
    else
        log_error "$category tests failed with exit code $exit_code"
    fi
    
    return $exit_code
}

# Performance baseline establishment
establish_performance_baselines() {
    log_info "Establishing Metal performance baselines..."
    
    local baseline_file="$RESULTS_DIR/baselines.json"
    local baseline_dir="$(dirname "$baseline_file")"
    mkdir -p "$baseline_dir"
    
    # Get system information
    local system_info=$(cat <<EOF
{
    "timestamp": "$(date -Iseconds)",
    "platform": "$(uname -s)",
    "architecture": "$(uname -m)", 
    "macOS_version": "$(sw_vers -productVersion)",
    "system_memory_gb": $(($(sysctl -n hw.memsize) / 1024 / 1024 / 1024)),
    "processor_name": "$(sysctl -n machdep.cpu.brand_string)",
    "is_apple_silicon": $([ "$(uname -m)" = "arm64" ] && echo "true" || echo "false")
}
EOF
)
    
    echo "$system_info" > "$baseline_file"
    log_success "Performance baselines saved to $baseline_file"
}

# Main execution function
main() {
    log_info "Starting Metal Test Execution Pipeline"
    log_info "Project Root: $PROJECT_ROOT"
    log_info "Results Directory: $RESULTS_DIR"
    
    # Create results directory
    mkdir -p "$RESULTS_DIR"
    
    # Check Metal support
    if ! check_metal_support; then
        log_error "Metal support check failed, aborting tests"
        exit 1
    fi
    
    # Establish performance baselines
    establish_performance_baselines
    
    local overall_success=0
    
    # Parse command line arguments
    local test_mode="${1:-all}"
    
    case "$test_mode" in
        "unit")
            log_info "Running unit tests only"
            run_test_category "Unit" "$UNIT_TESTS" 60000 || overall_success=1
            ;;
        "hardware")
            log_info "Running hardware tests only"
            run_test_category "Hardware" "$HARDWARE_TESTS" 300000 || overall_success=1
            ;;
        "performance")
            log_info "Running performance tests only"
            run_test_category "Performance" "$PERFORMANCE_TESTS" 600000 || overall_success=1
            ;;
        "stress")
            log_info "Running stress tests only"
            run_test_category "Stress" "$STRESS_TESTS" 1200000 || overall_success=1
            ;;
        "all"|*)
            log_info "Running all Metal tests"
            
            # Run tests in order of complexity
            run_test_category "Unit" "$UNIT_TESTS" 60000 || overall_success=1
            
            if [[ $overall_success -eq 0 ]]; then
                run_test_category "Hardware" "$HARDWARE_TESTS" 300000 || overall_success=1
            fi
            
            if [[ $overall_success -eq 0 ]]; then
                run_test_category "Performance" "$PERFORMANCE_TESTS" 600000 || overall_success=1
            fi
            
            if [[ $overall_success -eq 0 ]]; then
                run_test_category "Stress" "$STRESS_TESTS" 1200000 || overall_success=1
            fi
            ;;
    esac
    
    # Generate test report
    log_info "Generating test report..."
    
    local report_file="$RESULTS_DIR/test_report.md"
    
    cat > "$report_file" << EOF
# Metal Test Execution Report

**Generated:** $(date)
**Test Mode:** $test_mode
**Platform:** $(uname -s) $(uname -m)  
**macOS Version:** $(sw_vers -productVersion)
**Architecture:** $([ "$(uname -m)" = "arm64" ] && echo "Apple Silicon" || echo "Intel")

## Test Results

Test results are stored in: \`$RESULTS_DIR\`

### Test Categories Executed

- **Unit Tests**: Basic functionality without hardware requirements
- **Hardware Tests**: Metal-specific hardware integration tests
- **Performance Tests**: Benchmark and performance validation tests  
- **Stress Tests**: High-load and endurance testing

### System Configuration

- **Memory Scale Factor**: $(calculate_memory_scale)x
- **System Memory**: $(($(sysctl -n hw.memsize) / 1024 / 1024 / 1024))GB
- **Processor**: $(sysctl -n machdep.cpu.brand_string)

### Recommendations

1. Review individual test result files for detailed information
2. Check performance baselines against expected hardware capabilities  
3. Investigate any failed tests in the appropriate category directories
4. Consider running stress tests separately for extended validation

EOF
    
    log_success "Test report generated: $report_file"
    
    if [[ $overall_success -eq 0 ]]; then
        log_success "All Metal tests completed successfully"
    else
        log_error "Some Metal tests failed, check individual results"
    fi
    
    exit $overall_success
}

# Script help
show_help() {
    cat << EOF
Metal Test Execution Script

Usage: $0 [test_mode]

Test Modes:
    unit        - Run unit tests only
    hardware    - Run hardware-specific tests only  
    performance - Run performance tests only
    stress      - Run stress tests only
    all         - Run all test categories (default)

Requirements:
    - macOS 10.13 or later
    - .NET 9.0 SDK
    - Metal-capable hardware

Examples:
    $0              # Run all tests
    $0 unit         # Run only unit tests
    $0 hardware     # Run only hardware tests

EOF
}

# Check for help flag
if [[ "${1:-}" == "-h" ]] || [[ "${1:-}" == "--help" ]]; then
    show_help
    exit 0
fi

# Execute main function
main "$@"