#!/bin/bash

# DotCompute Error Tracking System
# Tracks CA1848 errors and monitors fix progress

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TRACKING_DIR="$SCRIPT_DIR/tracking"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BASELINE_FILE="$TRACKING_DIR/error_baseline.json"
CURRENT_FILE="$TRACKING_DIR/error_current_$TIMESTAMP.json"
PROGRESS_FILE="$TRACKING_DIR/error_progress.json"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

mkdir -p "$TRACKING_DIR"

echo -e "${BLUE}=== DotCompute Error Tracking System ===${NC}"
echo "Analyzing codebase for CA1848 violations..."
echo ""

# Function to count errors by type
count_errors() {
    local output_file="$1"

    # Build to get compiler diagnostics
    local build_log=$(mktemp)
    dotnet build "$PROJECT_ROOT/DotCompute.sln" \
        --no-restore \
        --configuration Release \
        --verbosity detailed \
        > "$build_log" 2>&1 || true

    # Parse errors
    local ca1848_count=$(grep -c "CA1848" "$build_log" || echo "0")
    local total_errors=$(grep -c " error " "$build_log" || echo "0")
    local total_warnings=$(grep -c " warning " "$build_log" || echo "0")

    # Extract CA1848 error details
    local ca1848_errors='[]'
    while IFS= read -r line; do
        if [[ $line == *"CA1848"* ]]; then
            # Parse file, line, message
            local file=$(echo "$line" | sed -n 's/.*\(\S\+\.cs\)(.*/\1/p')
            local line_num=$(echo "$line" | sed -n 's/.*\.cs(\([0-9]\+\).*/\1/p')
            local message=$(echo "$line" | sed -n 's/.*CA1848: \(.*\)/\1/p')

            ca1848_errors=$(echo "$ca1848_errors" | jq \
                --arg file "$file" \
                --arg line "$line_num" \
                --arg msg "$message" \
                '. + [{
                    "file": $file,
                    "line": $line,
                    "message": $msg,
                    "timestamp": now
                }]')
        fi
    done < "$build_log"

    # Group errors by file
    local errors_by_file=$(echo "$ca1848_errors" | jq 'group_by(.file) |
        map({
            file: .[0].file,
            count: length,
            errors: .
        })')

    # Create summary
    local summary=$(jq -n \
        --argjson ca1848 "$ca1848_count" \
        --argjson total_err "$total_errors" \
        --argjson total_warn "$total_warnings" \
        --argjson errors "$ca1848_errors" \
        --argjson by_file "$errors_by_file" \
        '{
            "timestamp": now,
            "summary": {
                "ca1848_violations": $ca1848,
                "total_errors": $total_err,
                "total_warnings": $total_warn
            },
            "ca1848_errors": $errors,
            "errors_by_file": $by_file
        }')

    echo "$summary" > "$output_file"
    rm -f "$build_log"

    echo "$ca1848_count"
}

# Function to create baseline if it doesn't exist
create_baseline() {
    if [ ! -f "$BASELINE_FILE" ]; then
        echo -e "${YELLOW}Creating baseline error count...${NC}"
        count_errors "$BASELINE_FILE" > /dev/null
        echo -e "${GREEN}Baseline created: $BASELINE_FILE${NC}"
    fi
}

# Function to compare with baseline
compare_with_baseline() {
    local current_count=$1
    local baseline_count=$(jq -r '.summary.ca1848_violations' "$BASELINE_FILE")
    local reduction=$((baseline_count - current_count))
    local percentage=0

    if [ $baseline_count -gt 0 ]; then
        percentage=$((reduction * 100 / baseline_count))
    fi

    echo -e "\n${BLUE}=== Progress Report ===${NC}"
    echo "Baseline errors: $baseline_count"
    echo "Current errors: $current_count"

    if [ $reduction -gt 0 ]; then
        echo -e "Reduction: ${GREEN}$reduction ($percentage%)${NC}"
    elif [ $reduction -lt 0 ]; then
        echo -e "Increase: ${RED}$((reduction * -1)) ($((percentage * -1))%)${NC}"
    else
        echo "No change"
    fi

    # Update progress tracking
    local progress_entry=$(jq -n \
        --arg timestamp "$TIMESTAMP" \
        --argjson baseline "$baseline_count" \
        --argjson current "$current_count" \
        --argjson reduction "$reduction" \
        --argjson percentage "$percentage" \
        '{
            timestamp: $timestamp,
            baseline: $baseline,
            current: $current,
            reduction: $reduction,
            percentage: $percentage
        }')

    # Append to progress file
    if [ -f "$PROGRESS_FILE" ]; then
        local progress=$(cat "$PROGRESS_FILE")
        echo "$progress" | jq --argjson entry "$progress_entry" '. + [$entry]' > "$PROGRESS_FILE"
    else
        echo "[$progress_entry]" > "$PROGRESS_FILE"
    fi
}

# Function to show errors by category
show_error_categories() {
    local error_file="$1"

    echo -e "\n${BLUE}=== Errors by File ===${NC}"
    jq -r '.errors_by_file[] |
        "\(.file): \(.count) errors"' "$error_file" | \
        sort -t: -k2 -rn | head -20
}

# Function to generate fix recommendations
generate_recommendations() {
    local error_file="$1"

    echo -e "\n${BLUE}=== Fix Recommendations ===${NC}"

    # Get top files with most errors
    local top_files=$(jq -r '.errors_by_file |
        sort_by(.count) |
        reverse |
        .[0:5] |
        .[] | .file' "$error_file")

    echo "Priority files to fix (most errors):"
    echo "$top_files" | while read -r file; do
        local count=$(jq -r --arg file "$file" \
            '.errors_by_file[] | select(.file == $file) | .count' \
            "$error_file")
        echo "  - $file ($count errors)"
    done

    echo ""
    echo "Fix strategy:"
    echo "  1. Start with files having most errors (highest impact)"
    echo "  2. Use automated fix script: ./fix-ca1848-auto.sh"
    echo "  3. Validate each fix: ./build-validator.sh"
    echo "  4. Run tests: dotnet test"
}

# Main execution
create_baseline

echo "Counting current errors..."
current_count=$(count_errors "$CURRENT_FILE")

echo -e "\n${BLUE}=== Current Error Count ===${NC}"
echo "CA1848 violations: $current_count"

# Show summary from current file
jq -r '.summary |
    "Total errors: \(.total_errors)\nTotal warnings: \(.total_warnings)"' \
    "$CURRENT_FILE"

compare_with_baseline "$current_count"
show_error_categories "$CURRENT_FILE"
generate_recommendations "$CURRENT_FILE"

# Show progress trend
if [ -f "$PROGRESS_FILE" ]; then
    local entries=$(jq 'length' "$PROGRESS_FILE")
    if [ $entries -gt 1 ]; then
        echo -e "\n${BLUE}=== Progress Trend ===${NC}"
        jq -r '.[] |
            "\(.timestamp): \(.current) errors (\(.reduction) reduction)"' \
            "$PROGRESS_FILE" | tail -10
    fi
fi

echo -e "\nDetailed results saved to: $CURRENT_FILE"

# Exit with success if no errors, failure otherwise
[ $current_count -eq 0 ]
