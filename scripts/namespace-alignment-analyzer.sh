#!/bin/bash
# Copyright (c) 2025 Michael Ivertowski
# Licensed under the MIT License. See LICENSE file in the project root for license information.

# DotCompute Namespace Alignment Analyzer (Linux/macOS version)
# This script analyzes and fixes namespace alignment with folder structure in DotCompute solution.

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_ROOT="$(dirname "$SCRIPT_DIR")"
ANALYZE_ONLY=false
FIX_ISSUES=false
DRY_RUN=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
WHITE='\033[1;37m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

print_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -a, --analyze     Only analyze and report misalignments without making changes"
    echo "  -f, --fix         Fix all identified namespace misalignments"
    echo "  -d, --dry-run     Show what changes would be made without actually making them"
    echo "  -h, --help        Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 --analyze"
    echo "  $0 --fix"
    echo "  $0 --dry-run"
}

print_header() {
    echo -e "${CYAN}DotCompute Namespace Alignment Analyzer${NC}"
    echo -e "${CYAN}=======================================${NC}"
    echo ""
}

get_expected_namespace() {
    local file_path="$1"
    local relative_path
    relative_path=$(realpath --relative-to="$SOLUTION_ROOT" "$file_path")
    
    # Define namespace mapping rules based on folder structure
    local base_namespace=""
    local remaining_path=""
    
    case "$relative_path" in
        src/DotCompute.Abstractions/*)
            base_namespace="DotCompute.Abstractions"
            remaining_path="${relative_path#src/DotCompute.Abstractions/}"
            ;;
        src/DotCompute.Algorithms/*)
            base_namespace="DotCompute.Algorithms"
            remaining_path="${relative_path#src/DotCompute.Algorithms/}"
            ;;
        src/DotCompute.Core/*)
            base_namespace="DotCompute.Core"
            remaining_path="${relative_path#src/DotCompute.Core/}"
            ;;
        src/DotCompute.Generators/*)
            base_namespace="DotCompute.Generators"
            remaining_path="${relative_path#src/DotCompute.Generators/}"
            ;;
        src/DotCompute.Linq/*)
            base_namespace="DotCompute.Linq"
            remaining_path="${relative_path#src/DotCompute.Linq/}"
            ;;
        src/DotCompute.Memory/*)
            base_namespace="DotCompute.Memory"
            remaining_path="${relative_path#src/DotCompute.Memory/}"
            ;;
        src/DotCompute.Plugins/*)
            base_namespace="DotCompute.Plugins"
            remaining_path="${relative_path#src/DotCompute.Plugins/}"
            ;;
        src/DotCompute.Runtime/*)
            base_namespace="DotCompute.Runtime"
            remaining_path="${relative_path#src/DotCompute.Runtime/}"
            ;;
        plugins/backends/DotCompute.Backends.CPU/*)
            base_namespace="DotCompute.Backends.CPU"
            remaining_path="${relative_path#plugins/backends/DotCompute.Backends.CPU/}"
            ;;
        plugins/backends/DotCompute.Backends.CUDA/*)
            base_namespace="DotCompute.Backends.CUDA"
            remaining_path="${relative_path#plugins/backends/DotCompute.Backends.CUDA/}"
            ;;
        plugins/backends/DotCompute.Backends.Metal/*)
            base_namespace="DotCompute.Backends.Metal"
            remaining_path="${relative_path#plugins/backends/DotCompute.Backends.Metal/}"
            ;;
        tests/*)
            base_namespace="DotCompute.Tests"
            remaining_path="${relative_path#tests/}"
            ;;
        benchmarks/*)
            base_namespace="DotCompute.Benchmarks"
            remaining_path="${relative_path#benchmarks/}"
            ;;
        samples/*)
            base_namespace="DotCompute.Samples"
            remaining_path="${relative_path#samples/}"
            ;;
        examples/*)
            base_namespace="DotCompute.Examples"
            remaining_path="${relative_path#examples/}"
            ;;
        tools/*)
            base_namespace="DotCompute.Tools"
            remaining_path="${relative_path#tools/}"
            ;;
        *)
            return 1
            ;;
    esac
    
    # Extract directory parts from remaining path
    local directory_parts=()
    IFS='/' read -ra PARTS <<< "$remaining_path"
    
    for part in "${PARTS[@]}"; do
        # Skip empty parts, file extensions, and common non-namespace directories
        if [[ -n "$part" && "$part" != *"."* && "$part" != "src" && "$part" != "tests" && "$part" != "bin" && "$part" != "obj" ]]; then
            directory_parts+=("$part")
        fi
    done
    
    # Build final namespace
    if [[ ${#directory_parts[@]} -gt 0 ]]; then
        local namespace_suffix
        namespace_suffix=$(IFS='.'; echo "${directory_parts[*]}")
        echo "${base_namespace}.${namespace_suffix}"
    else
        echo "$base_namespace"
    fi
}

get_namespace_from_file() {
    local file_path="$1"
    local line_number=0
    
    while IFS= read -r line; do
        ((line_number++))
        line=$(echo "$line" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
        
        if [[ $line =~ ^namespace[[:space:]]+([^;{]+) ]]; then
            local namespace="${BASH_REMATCH[1]}"
            namespace=$(echo "$namespace" | sed 's/;$//')
            echo "$namespace|$line_number"
            return 0
        fi
    done < "$file_path"
    
    return 1
}

find_namespace_issues() {
    local issues_file="/tmp/namespace_issues.txt"
    > "$issues_file"
    
    echo -e "${YELLOW}Scanning for C# files...${NC}"
    
    # Find all C# files
    local cs_files=()
    while IFS= read -r -d '' file; do
        cs_files+=("$file")
    done < <(find "$SOLUTION_ROOT" -name "*.cs" -type f \
        ! -path "*/bin/*" \
        ! -path "*/obj/*" \
        ! -path "*/.git/*" \
        ! -name "GlobalAssemblyInfo.cs" \
        ! -name "AssemblyInfo.cs" \
        -print0)
    
    echo -e "${GREEN}Found ${#cs_files[@]} C# files to analyze${NC}"
    
    local processed_count=0
    local issues_count=0
    
    for file in "${cs_files[@]}"; do
        ((processed_count++))
        if ((processed_count % 50 == 0)); then
            echo -e "${GRAY}Processed $processed_count / ${#cs_files[@]} files...${NC}"
        fi
        
        local namespace_info
        if namespace_info=$(get_namespace_from_file "$file"); then
            local current_namespace="${namespace_info%|*}"
            local line_number="${namespace_info#*|}"
            
            local expected_namespace
            if expected_namespace=$(get_expected_namespace "$file"); then
                if [[ "$current_namespace" != "$expected_namespace" ]]; then
                    local relative_path
                    relative_path=$(realpath --relative-to="$SOLUTION_ROOT" "$file")
                    echo "$file|$current_namespace|$expected_namespace|$line_number|$relative_path" >> "$issues_file"
                    ((issues_count++))
                fi
            fi
        fi
    done
    
    echo "$issues_count"
}

show_analysis_report() {
    local issues_file="/tmp/namespace_issues.txt"
    local issues_count="$1"
    
    echo ""
    echo -e "${MAGENTA}NAMESPACE ALIGNMENT ANALYSIS REPORT${NC}"
    echo -e "${MAGENTA}===================================${NC}"
    echo ""
    
    if [[ $issues_count -eq 0 ]]; then
        echo -e "${GREEN}✅ All namespaces are properly aligned with folder structure!${NC}"
        return
    fi
    
    echo -e "${RED}Found $issues_count namespace alignment issues:${NC}"
    echo ""
    
    # Group issues by expected namespace
    local current_expected=""
    while IFS='|' read -r file_path current_namespace expected_namespace line_number relative_path; do
        if [[ "$expected_namespace" != "$current_expected" ]]; then
            current_expected="$expected_namespace"
            echo -e "${CYAN}Expected Namespace: $expected_namespace${NC}"
            echo -e "${GRAY}$(printf '─%.0s' {1..50})${NC}"
        fi
        
        echo -e "  ${WHITE}📁 $relative_path${NC}"
        echo -e "    ${RED}Current:  $current_namespace${NC}"
        echo -e "    ${GREEN}Expected: $expected_namespace${NC}"
        echo -e "    ${GRAY}Line:     $line_number${NC}"
        echo ""
    done < <(sort -t'|' -k3 "$issues_file")
    
    # Summary statistics
    local unique_namespaces
    unique_namespaces=$(cut -d'|' -f2 "$issues_file" | sort -u | wc -l)
    
    echo -e "${YELLOW}SUMMARY${NC}"
    echo -e "${YELLOW}-------${NC}"
    echo -e "${WHITE}Files with issues: $issues_count${NC}"
    echo -e "${WHITE}Unique wrong namespaces: $unique_namespaces${NC}"
}

fix_namespace_issues() {
    local issues_file="/tmp/namespace_issues.txt"
    local issues_count="$1"
    local dry_run="$2"
    
    if [[ $issues_count -eq 0 ]]; then
        echo -e "${GREEN}No issues to fix!${NC}"
        return
    fi
    
    echo ""
    echo -e "${MAGENTA}FIXING NAMESPACE ISSUES${NC}"
    echo -e "${MAGENTA}=======================${NC}"
    echo ""
    
    if [[ "$dry_run" == "true" ]]; then
        echo -e "${YELLOW}DRY RUN MODE - No changes will be made${NC}"
        echo ""
    fi
    
    local fixed_count=0
    local failed_count=0
    
    while IFS='|' read -r file_path current_namespace expected_namespace line_number relative_path; do
        echo -e "${WHITE}Fixing: $relative_path${NC}"
        echo -e "  ${GRAY}$current_namespace → $expected_namespace${NC}"
        
        if [[ "$dry_run" != "true" ]]; then
            if sed -i "s/namespace[[:space:]]\+$(echo "$current_namespace" | sed 's/[[\.*^$()+?{|]/\\&/g')/namespace $expected_namespace/g" "$file_path"; then
                ((fixed_count++))
            else
                echo -e "${RED}Failed to fix $file_path${NC}"
                ((failed_count++))
            fi
        else
            ((fixed_count++))
        fi
    done < "$issues_file"
    
    echo ""
    echo -e "${YELLOW}RESULTS${NC}"
    echo -e "${YELLOW}-------${NC}"
    echo -e "${GREEN}Fixed: $fixed_count${NC}"
    if [[ $failed_count -gt 0 ]]; then
        echo -e "${RED}Failed: $failed_count${NC}"
    fi
}

update_using_statements() {
    local issues_file="/tmp/namespace_issues.txt"
    local issues_count="$1"
    local dry_run="$2"
    
    if [[ $issues_count -eq 0 ]]; then
        return
    fi
    
    echo ""
    echo -e "${MAGENTA}UPDATING USING STATEMENTS${NC}"
    echo -e "${MAGENTA}=========================${NC}"
    echo ""
    
    # Create temporary file for namespace mappings
    local mapping_file="/tmp/namespace_mappings.txt"
    > "$mapping_file"
    
    while IFS='|' read -r _ current_namespace expected_namespace _ _; do
        echo "$current_namespace|$expected_namespace" >> "$mapping_file"
    done < "$issues_file"
    
    # Remove duplicates
    sort -u "$mapping_file" -o "$mapping_file"
    
    # Find all C# files to update using statements
    local cs_files=()
    while IFS= read -r -d '' file; do
        cs_files+=("$file")
    done < <(find "$SOLUTION_ROOT" -name "*.cs" -type f \
        ! -path "*/bin/*" \
        ! -path "*/obj/*" \
        ! -path "*/.git/*" \
        -print0)
    
    local updated_files=0
    
    for file in "${cs_files[@]}"; do
        local original_content
        original_content=$(cat "$file")
        local new_content="$original_content"
        
        # Update using statements
        while IFS='|' read -r old_namespace new_namespace; do
            local escaped_old
            escaped_old=$(echo "$old_namespace" | sed 's/[[\.*^$()+?{|]/\\&/g')
            new_content=$(echo "$new_content" | sed "s/using[[:space:]]\+$escaped_old/using $new_namespace/g")
        done < "$mapping_file"
        
        # Only write if content changed
        if [[ "$new_content" != "$original_content" ]]; then
            local relative_path
            relative_path=$(realpath --relative-to="$SOLUTION_ROOT" "$file")
            echo -e "${GRAY}Updating using statements in: $relative_path${NC}"
            
            if [[ "$dry_run" != "true" ]]; then
                echo "$new_content" > "$file"
            fi
            ((updated_files++))
        fi
    done
    
    echo -e "${GREEN}Updated using statements in $updated_files files${NC}"
    
    # Cleanup
    rm -f "$mapping_file"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -a|--analyze)
            ANALYZE_ONLY=true
            shift
            ;;
        -f|--fix)
            FIX_ISSUES=true
            shift
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            print_usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            print_usage
            exit 1
            ;;
    esac
done

# Validate parameters
if [[ "$ANALYZE_ONLY" == "false" && "$FIX_ISSUES" == "false" && "$DRY_RUN" == "false" ]]; then
    echo -e "${RED}Please specify one of: --analyze, --fix, or --dry-run${NC}"
    print_usage
    exit 1
fi

# Main execution
print_header

echo -e "${YELLOW}Analyzing namespace alignment...${NC}"
issues_count=$(find_namespace_issues)

show_analysis_report "$issues_count"

if [[ "$FIX_ISSUES" == "true" || "$DRY_RUN" == "true" ]]; then
    fix_namespace_issues "$issues_count" "$DRY_RUN"
    update_using_statements "$issues_count" "$DRY_RUN"
    
    if [[ "$DRY_RUN" != "true" ]]; then
        echo ""
        echo -e "${GREEN}✅ Namespace alignment complete!${NC}"
        echo ""
        echo -e "${YELLOW}Next steps:${NC}"
        echo -e "${WHITE}1. Build the solution to verify no compilation errors${NC}"
        echo -e "${WHITE}2. Run tests to ensure functionality is preserved${NC}"
        echo -e "${WHITE}3. Commit the namespace alignment changes${NC}"
    fi
fi

# Cleanup
rm -f /tmp/namespace_issues.txt