#!/bin/bash

# Coverage Analyzer - Analyzes code coverage without running tests
# This script provides a baseline analysis of the codebase structure

set -e

echo "🔍 Coverage Analyzer - Static Analysis"
echo "====================================="

# Define source directories
SOURCE_DIRS=(
    "src/DotCompute.Core"
    "src/DotCompute.Abstractions"
    "src/DotCompute.Memory"
    "src/DotCompute.Plugins"
    "src/DotCompute.Generators"
    "plugins/backends/DotCompute.Backends.CPU"
    "plugins/backends/DotCompute.Backends.CUDA"
    "plugins/backends/DotCompute.Backends.Metal"
)

# Define test directories
TEST_DIRS=(
    "tests/DotCompute.Memory.Tests"
    "tests/DotCompute.SharedTestUtilities"
    "plugins/backends/DotCompute.Backends.CPU/tests"
    "plugins/backends/DotCompute.Backends.Metal/tests"
)

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "📊 Codebase Structure Analysis"
echo "============================="

total_source_files=0
total_source_lines=0
total_test_files=0
total_test_lines=0

# Analyze source directories
echo -e "${BLUE}📁 Source Code Analysis:${NC}"
for dir in "${SOURCE_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        cs_files=$(find "$dir" -name "*.cs" | grep -v bin | grep -v obj | wc -l)
        cs_lines=$(find "$dir" -name "*.cs" | grep -v bin | grep -v obj | xargs wc -l 2>/dev/null | tail -1 | awk '{print $1}' || echo 0)
        
        echo "   $dir: $cs_files files, $cs_lines lines"
        total_source_files=$((total_source_files + cs_files))
        total_source_lines=$((total_source_lines + cs_lines))
    else
        echo "   $dir: Not found"
    fi
done

echo ""
echo -e "${BLUE}🧪 Test Code Analysis:${NC}"
for dir in "${TEST_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        test_files=$(find "$dir" -name "*.cs" | grep -v bin | grep -v obj | wc -l)
        test_lines=$(find "$dir" -name "*.cs" | grep -v bin | grep -v obj | xargs wc -l 2>/dev/null | tail -1 | awk '{print $1}' || echo 0)
        
        echo "   $dir: $test_files files, $test_lines lines"
        total_test_files=$((total_test_files + test_files))
        total_test_lines=$((total_test_lines + test_lines))
    else
        echo "   $dir: Not found"
    fi
done

echo ""
echo "📈 Coverage Baseline Analysis"
echo "============================="

# Calculate test-to-source ratio
if [ $total_source_lines -gt 0 ]; then
    test_ratio=$(echo "scale=2; $total_test_lines * 100 / $total_source_lines" | bc -l)
    echo -e "Test-to-Source Ratio: ${GREEN}${test_ratio}%${NC} (Target: >50%)"
else
    echo -e "Test-to-Source Ratio: ${RED}N/A${NC} (No source code found)"
fi

# Calculate file ratios
if [ $total_source_files -gt 0 ]; then
    file_ratio=$(echo "scale=2; $total_test_files * 100 / $total_source_files" | bc -l)
    echo -e "Test-to-Source Files: ${GREEN}${file_ratio}%${NC} (Target: >30%)"
else
    echo -e "Test-to-Source Files: ${RED}N/A${NC} (No source files found)"
fi

echo ""
echo "🎯 Coverage Analysis by Component"
echo "================================"

# Analyze each component
for dir in "${SOURCE_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        component=$(basename "$dir")
        
        # Count source files and lines
        source_files=$(find "$dir" -name "*.cs" | grep -v bin | grep -v obj | wc -l)
        source_lines=$(find "$dir" -name "*.cs" | grep -v bin | grep -v obj | xargs wc -l 2>/dev/null | tail -1 | awk '{print $1}' || echo 0)
        
        # Find corresponding test directory
        test_dir=""
        test_files=0
        test_lines=0
        
        # Map source to test directories
        case "$dir" in
            "src/DotCompute.Memory")
                test_dir="tests/DotCompute.Memory.Tests"
                ;;
            "plugins/backends/DotCompute.Backends.CPU")
                test_dir="plugins/backends/DotCompute.Backends.CPU/tests"
                ;;
            "plugins/backends/DotCompute.Backends.Metal")
                test_dir="plugins/backends/DotCompute.Backends.Metal/tests"
                ;;
            *)
                # Look for tests in tests directory
                test_dir="tests/$component.Tests"
                ;;
        esac
        
        if [ -d "$test_dir" ]; then
            test_files=$(find "$test_dir" -name "*.cs" | grep -v bin | grep -v obj | wc -l)
            test_lines=$(find "$test_dir" -name "*.cs" | grep -v bin | grep -v obj | xargs wc -l 2>/dev/null | tail -1 | awk '{print $1}' || echo 0)
        fi
        
        # Calculate coverage estimate
        if [ $source_lines -gt 0 ]; then
            coverage_estimate=$(echo "scale=1; $test_lines * 100 / $source_lines" | bc -l)
            
            if (( $(echo "$coverage_estimate >= 80" | bc -l) )); then
                status="${GREEN}GOOD${NC}"
            elif (( $(echo "$coverage_estimate >= 50" | bc -l) )); then
                status="${YELLOW}FAIR${NC}"
            else
                status="${RED}POOR${NC}"
            fi
            
            echo -e "   $component: ${coverage_estimate}% coverage estimate - $status"
        else
            echo -e "   $component: ${RED}No source code${NC}"
        fi
    fi
done

echo ""
echo "🔍 Uncovered Areas Analysis"
echo "=========================="

# Find source files without corresponding tests
echo -e "${YELLOW}Components without tests:${NC}"
for dir in "${SOURCE_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        component=$(basename "$dir")
        test_found=false
        
        for test_dir in "${TEST_DIRS[@]}"; do
            if [[ "$test_dir" == *"$component"* ]]; then
                test_found=true
                break
            fi
        done
        
        if [ "$test_found" = false ]; then
            echo "   • $component"
        fi
    fi
done

echo ""
echo "📋 Coverage Improvement Recommendations"
echo "======================================"

echo -e "${GREEN}High Priority:${NC}"
echo "   1. Fix compilation errors in DotCompute.Memory.Tests"
echo "   2. Create test projects for untested components"
echo "   3. Implement unit tests for core functionality"

echo -e "${YELLOW}Medium Priority:${NC}"
echo "   4. Add integration tests for multi-component scenarios"
echo "   5. Implement performance tests with coverage"
echo "   6. Add edge case and error handling tests"

echo -e "${BLUE}Low Priority:${NC}"
echo "   7. Create benchmark tests with coverage tracking"
echo "   8. Add stress tests for memory components"
echo "   9. Implement cross-platform compatibility tests"

echo ""
echo "🏆 Coverage Targets"
echo "=================="
echo -e "Line Coverage:   ${GREEN}95%${NC} (Current estimate: TBD)"
echo -e "Branch Coverage: ${GREEN}90%${NC} (Current estimate: TBD)"
echo -e "Method Coverage: ${GREEN}95%${NC} (Current estimate: TBD)"
echo -e "Class Coverage:  ${GREEN}90%${NC} (Current estimate: TBD)"

echo ""
echo "🔧 Next Steps"
echo "============"
echo "1. Fix test compilation errors"
echo "2. Run: dotnet test --collect:\"XPlat Code Coverage\""
echo "3. Generate reports: reportgenerator -reports:coverage/*.xml -targetdir:coverage/report"
echo "4. Run: dotnet run --project tools/coverage-validator.csproj"

echo ""
echo "🎉 Coverage Analysis Complete"
echo "============================"