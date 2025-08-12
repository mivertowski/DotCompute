#!/bin/bash

# Check Coverage Thresholds for DotCompute
# This script validates that coverage meets minimum requirements and provides actionable feedback

set -euo pipefail

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
COVERAGE_FILE="${1:-$PROJECT_ROOT/coverage-reports/coverage.cobertura.xml}"

# Coverage thresholds by project type
declare -A THRESHOLDS=(
    ["DotCompute.Core"]=90
    ["DotCompute.Algorithms"]=85
    ["DotCompute.Memory"]=85
    ["DotCompute.Linq"]=85
    ["DotCompute.Abstractions"]=90
    ["DotCompute.Backends.CUDA"]=85
    ["DotCompute.Backends.CPU"]=85
    ["DotCompute.Backends.Metal"]=85
    ["DotCompute.Plugins"]=85
    ["DotCompute.Runtime"]=85
    ["DotCompute.Generators"]=80
    ["overall"]=85
)

# Function to log with timestamp and styling
log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Function to parse XML and extract coverage data
parse_coverage_xml() {
    local xml_file="$1"
    python3 -c "
import xml.etree.ElementTree as ET
import sys

try:
    tree = ET.parse('$xml_file')
    root = tree.getroot()
    
    # Overall coverage
    overall_line_rate = float(root.get('line-rate', 0)) * 100
    overall_branch_rate = float(root.get('branch-rate', 0)) * 100
    
    print(f'OVERALL_LINE_COVERAGE={overall_line_rate:.2f}')
    print(f'OVERALL_BRANCH_COVERAGE={overall_branch_rate:.2f}')
    
    # Package-level coverage
    for package in root.findall('.//package'):
        name = package.get('name', 'Unknown')
        line_rate = float(package.get('line-rate', 0)) * 100
        branch_rate = float(package.get('branch-rate', 0)) * 100
        
        # Clean up package name to match project names
        clean_name = name.replace('/', '.').replace('\\\\', '.')
        print(f'PKG_{clean_name}_LINE_COVERAGE={line_rate:.2f}')
        print(f'PKG_{clean_name}_BRANCH_COVERAGE={branch_rate:.2f}')
        
        # Find classes with low coverage
        for cls in package.findall('.//class'):
            cls_name = cls.get('name', 'Unknown')
            cls_line_rate = float(cls.get('line-rate', 0)) * 100
            if cls_line_rate < 50:  # Flag very low coverage classes
                print(f'LOW_COVERAGE_CLASS={cls_name}:{cls_line_rate:.1f}')
                
except Exception as e:
    print(f'ERROR=Failed to parse coverage file: {e}', file=sys.stderr)
    sys.exit(1)
"
}

# Function to check if coverage meets threshold
check_threshold() {
    local actual="$1"
    local threshold="$2"
    local name="$3"
    
    if (( $(echo "$actual >= $threshold" | bc -l) )); then
        log "${GREEN}✅ $name: ${actual}% (>= ${threshold}%)${NC}"
        return 0
    else
        local deficit=$(echo "$threshold - $actual" | bc -l)
        log "${RED}❌ $name: ${actual}% (< ${threshold}%, deficit: ${deficit}%)${NC}"
        return 1
    fi
}

# Function to generate improvement suggestions
generate_suggestions() {
    local component="$1"
    local current_coverage="$2"
    local target_coverage="$3"
    
    cat << EOF

### Improvement Suggestions for $component

**Current Coverage:** ${current_coverage}%
**Target Coverage:** ${target_coverage}%
**Gap:** $(echo "$target_coverage - $current_coverage" | bc -l)%

#### Action Items:
1. **Review uncovered code:** Focus on critical business logic and error paths
2. **Add edge case tests:** Test boundary conditions and error scenarios
3. **Improve test assertions:** Ensure tests validate expected behavior thoroughly
4. **Consider integration tests:** Add tests that cover component interactions

#### Common areas to improve:
- Exception handling and error paths
- Configuration and initialization code
- Edge cases and boundary conditions
- Async/await patterns and cancellation
- Resource cleanup and disposal patterns

EOF
}

# Main execution
main() {
    log "${BLUE}${BOLD}🎯 DotCompute Coverage Threshold Checker${NC}"
    echo "=========================================="
    
    # Validate coverage file exists
    if [[ ! -f "$COVERAGE_FILE" ]]; then
        log "${RED}❌ Coverage file not found: $COVERAGE_FILE${NC}"
        echo ""
        echo "Please run coverage collection first:"
        echo "  ./scripts/run-coverage.sh"
        exit 1
    fi
    
    log "${BLUE}📊 Analyzing coverage file: $(basename "$COVERAGE_FILE")${NC}"
    
    # Parse coverage data
    coverage_data=$(parse_coverage_xml "$COVERAGE_FILE")
    if [[ $? -ne 0 ]]; then
        log "${RED}❌ Failed to parse coverage data${NC}"
        exit 1
    fi
    
    # Extract coverage values
    eval "$coverage_data"
    
    # Check if variables were set
    if [[ -z "${OVERALL_LINE_COVERAGE:-}" ]]; then
        log "${RED}❌ Could not extract coverage data${NC}"
        exit 1
    fi
    
    log "${BLUE}📈 Coverage Analysis Results:${NC}"
    echo ""
    
    # Track pass/fail status
    failed_checks=0
    total_checks=0
    suggestions_file="$PROJECT_ROOT/coverage-reports/improvement-suggestions.md"
    
    echo "# Coverage Improvement Suggestions" > "$suggestions_file"
    echo "" >> "$suggestions_file"
    echo "**Generated:** $(date '+%Y-%m-%d %H:%M:%S')" >> "$suggestions_file"
    echo "" >> "$suggestions_file"
    
    # Check overall coverage
    echo "${BOLD}Overall Coverage:${NC}"
    ((total_checks++))
    if ! check_threshold "$OVERALL_LINE_COVERAGE" "${THRESHOLDS[overall]}" "Overall Line Coverage"; then
        ((failed_checks++))
        generate_suggestions "Overall Project" "$OVERALL_LINE_COVERAGE" "${THRESHOLDS[overall]}" >> "$suggestions_file"
    fi
    
    ((total_checks++))
    if ! check_threshold "$OVERALL_BRANCH_COVERAGE" "75" "Overall Branch Coverage"; then
        ((failed_checks++))
    fi
    
    echo ""
    echo "${BOLD}Component Coverage:${NC}"
    
    # Check component-specific coverage
    for component in "${!THRESHOLDS[@]}"; do
        if [[ "$component" == "overall" ]]; then
            continue
        fi
        
        # Try to find matching coverage data
        var_name="PKG_${component//./}_LINE_COVERAGE"
        coverage_value="${!var_name:-}"
        
        if [[ -n "$coverage_value" ]]; then
            ((total_checks++))
            if ! check_threshold "$coverage_value" "${THRESHOLDS[$component]}" "$component"; then
                ((failed_checks++))
                generate_suggestions "$component" "$coverage_value" "${THRESHOLDS[$component]}" >> "$suggestions_file"
            fi
        else
            log "${YELLOW}⚠️  $component: No coverage data found${NC}"
        fi
    done
    
    # Find and report low coverage classes
    low_coverage_classes=($(echo "$coverage_data" | grep "LOW_COVERAGE_CLASS=" | cut -d= -f2))
    if [ ${#low_coverage_classes[@]} -gt 0 ]; then
        echo ""
        log "${YELLOW}⚠️  Classes with very low coverage (<50%):${NC}"
        for class_info in "${low_coverage_classes[@]}"; do
            echo "  - $class_info"
        done
        
        echo "" >> "$suggestions_file"
        echo "## Classes Requiring Immediate Attention (<50% coverage)" >> "$suggestions_file"
        echo "" >> "$suggestions_file"
        for class_info in "${low_coverage_classes[@]}"; do
            echo "- \`$class_info\`" >> "$suggestions_file"
        done
    fi
    
    # Summary
    echo ""
    echo "=========================================="
    passed_checks=$((total_checks - failed_checks))
    pass_rate=$(echo "scale=1; $passed_checks * 100 / $total_checks" | bc -l)
    
    if [[ $failed_checks -eq 0 ]]; then
        log "${GREEN}${BOLD}🎉 All coverage thresholds met! ($passed_checks/$total_checks)${NC}"
        echo "✅ Overall: ${OVERALL_LINE_COVERAGE}% line, ${OVERALL_BRANCH_COVERAGE}% branch"
        exit_code=0
    else
        log "${RED}${BOLD}❌ Coverage thresholds not met ($passed_checks/$total_checks passed, ${pass_rate}%)${NC}"
        echo "📊 Overall: ${OVERALL_LINE_COVERAGE}% line, ${OVERALL_BRANCH_COVERAGE}% branch"
        echo ""
        echo "📋 Improvement suggestions saved to: $suggestions_file"
        exit_code=1
    fi
    
    # Generate summary report
    summary_file="$PROJECT_ROOT/coverage-reports/threshold-check-summary.json"
    cat > "$summary_file" << EOF
{
    "timestamp": "$(date -Iseconds)",
    "overall_line_coverage": $OVERALL_LINE_COVERAGE,
    "overall_branch_coverage": $OVERALL_BRANCH_COVERAGE,
    "total_checks": $total_checks,
    "passed_checks": $passed_checks,
    "failed_checks": $failed_checks,
    "pass_rate": $pass_rate,
    "status": "$(if [[ $failed_checks -eq 0 ]]; then echo "PASSED"; else echo "FAILED"; fi)",
    "thresholds_met": $(if [[ $failed_checks -eq 0 ]]; then echo "true"; else echo "false"; fi)
}
EOF
    
    echo ""
    echo "📊 Detailed results:"
    echo "  - HTML Report: $PROJECT_ROOT/coverage-reports/html/index.html"
    echo "  - Suggestions: $suggestions_file"
    echo "  - Summary JSON: $summary_file"
    
    return $exit_code
}

# Install bc if not available (for decimal arithmetic)
if ! command -v bc >/dev/null 2>&1; then
    log "${YELLOW}⚠️  Installing bc for decimal arithmetic...${NC}"
    if command -v apt >/dev/null 2>&1; then
        sudo apt update && sudo apt install -y bc
    elif command -v yum >/dev/null 2>&1; then
        sudo yum install -y bc
    elif command -v brew >/dev/null 2>&1; then
        brew install bc
    else
        log "${RED}❌ Cannot install bc. Please install it manually.${NC}"
        exit 1
    fi
fi

# Run main function
main "$@"