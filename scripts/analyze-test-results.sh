#!/bin/bash
# Test Results Analysis Script
# Analyzes test output and generates detailed failure reports

set -e

if [ $# -lt 1 ]; then
    echo "Usage: $0 <test-result-file>"
    exit 1
fi

RESULT_FILE=$1
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ANALYSIS_DIR="$PROJECT_ROOT/test-results/analysis"

mkdir -p "$ANALYSIS_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Test Results Analysis${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Extract summary statistics
TOTAL_TESTS=$(grep -c "  Passed" "$RESULT_FILE" || echo 0)
PASSED_TESTS=$(grep "  Passed" "$RESULT_FILE" | wc -l || echo 0)
FAILED_TESTS=$(grep "  Failed" "$RESULT_FILE" | wc -l || echo 0)
SKIPPED_TESTS=$(grep "  Skipped" "$RESULT_FILE" | wc -l || echo 0)

echo -e "${GREEN}Total Tests:   $TOTAL_TESTS${NC}"
echo -e "${GREEN}Passed:        $PASSED_TESTS${NC}"
echo -e "${RED}Failed:        $FAILED_TESTS${NC}"
echo -e "${YELLOW}Skipped:       $SKIPPED_TESTS${NC}"
echo ""

# Extract failed test details
if [ $FAILED_TESTS -gt 0 ]; then
    echo -e "${RED}Failed Tests:${NC}"
    echo ""

    FAILED_LIST="$ANALYSIS_DIR/failed-tests.txt"
    grep -A 10 "  Failed" "$RESULT_FILE" > "$FAILED_LIST" || true

    # Extract unique failure categories
    echo -e "${YELLOW}Failure Categories:${NC}"

    if grep -q "Assert.Equal" "$RESULT_FILE"; then
        ASSERT_FAILS=$(grep -c "Assert.Equal" "$RESULT_FILE" || echo 0)
        echo -e "  - Assertion failures: ${RED}$ASSERT_FAILS${NC}"
    fi

    if grep -q "NullReferenceException" "$RESULT_FILE"; then
        NULL_FAILS=$(grep -c "NullReferenceException" "$RESULT_FILE" || echo 0)
        echo -e "  - Null reference: ${RED}$NULL_FAILS${NC}"
    fi

    if grep -q "TimeoutException" "$RESULT_FILE"; then
        TIMEOUT_FAILS=$(grep -c "TimeoutException" "$RESULT_FILE" || echo 0)
        echo -e "  - Timeouts: ${RED}$TIMEOUT_FAILS${NC}"
    fi

    if grep -q "NotImplementedException" "$RESULT_FILE"; then
        NOT_IMPL=$(grep -c "NotImplementedException" "$RESULT_FILE" || echo 0)
        echo -e "  - Not implemented: ${YELLOW}$NOT_IMPL${NC}"
    fi

    if grep -q "CudaException\|CUDA error" "$RESULT_FILE"; then
        CUDA_FAILS=$(grep -c "CudaException\|CUDA error" "$RESULT_FILE" || echo 0)
        echo -e "  - CUDA errors: ${RED}$CUDA_FAILS${NC}"
    fi

    echo ""
    echo -e "${BLUE}Detailed failure list saved to: $FAILED_LIST${NC}"
fi

# Extract timing information
if grep -q "Total time:" "$RESULT_FILE"; then
    TOTAL_TIME=$(grep "Total time:" "$RESULT_FILE" | awk '{print $3}')
    echo ""
    echo -e "${BLUE}Execution Time: $TOTAL_TIME${NC}"
fi

# Extract slow tests (>1 second)
echo ""
echo -e "${YELLOW}Slow Tests (>1s):${NC}"
grep -E "\[[0-9]+\.[0-9]+ s\]" "$RESULT_FILE" | grep -v "\[0\." | head -10 || echo "  None found"

# Generate JSON summary
TIMESTAMP=$(date -Iseconds)
SUMMARY_JSON="$ANALYSIS_DIR/summary_$(date +%Y%m%d_%H%M%S).json"

cat > "$SUMMARY_JSON" << EOF
{
  "timestamp": "$TIMESTAMP",
  "source_file": "$RESULT_FILE",
  "statistics": {
    "total": $TOTAL_TESTS,
    "passed": $PASSED_TESTS,
    "failed": $FAILED_TESTS,
    "skipped": $SKIPPED_TESTS,
    "pass_rate": $(echo "scale=2; ($PASSED_TESTS * 100) / $TOTAL_TESTS" | bc 2>/dev/null || echo "0")
  },
  "failure_categories": {
    "assertion_failures": $(grep -c "Assert\." "$RESULT_FILE" || echo 0),
    "null_reference": $(grep -c "NullReferenceException" "$RESULT_FILE" || echo 0),
    "timeouts": $(grep -c "TimeoutException" "$RESULT_FILE" || echo 0),
    "not_implemented": $(grep -c "NotImplementedException" "$RESULT_FILE" || echo 0),
    "cuda_errors": $(grep -c "CudaException\|CUDA error" "$RESULT_FILE" || echo 0)
  }
}
EOF

echo ""
echo -e "${GREEN}Analysis complete!${NC}"
echo -e "${BLUE}Summary JSON: $SUMMARY_JSON${NC}"

# Return exit code based on failures
if [ $FAILED_TESTS -gt 0 ]; then
    exit 1
else
    exit 0
fi
