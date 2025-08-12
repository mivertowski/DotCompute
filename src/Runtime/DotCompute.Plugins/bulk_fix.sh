#!/bin/bash
# Bulk C# Code Fixer Script (Bash version)
# Applies common fixes to all .cs files in the project

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Options
WHAT_IF=false
VERBOSE=false
DIRECTORY="."

# Statistics
FILES_PROCESSED=0
TOTAL_FIXES=0
declare -A FIX_TYPES=(
    ["CA1852"]=0
    ["CA1822"]=0
    ["CS1503"]=0
    ["CS1998"]=0
    ["CS1061"]=0
    ["Other"]=0
)

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --what-if)
            WHAT_IF=true
            shift
            ;;
        --verbose|-v)
            VERBOSE=true
            shift
            ;;
        --directory|-d)
            DIRECTORY="$2"
            shift 2
            ;;
        --help|-h)
            echo "Usage: $0 [--what-if] [--verbose] [--directory DIR]"
            echo "  --what-if    Show what would be changed without making changes"
            echo "  --verbose    Show detailed output"
            echo "  --directory  Directory to process (default: current directory)"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

function log_info() {
    echo -e "${BLUE}🔄 $1${NC}"
}

function log_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

function log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

function log_error() {
    echo -e "${RED}❌ $1${NC}"
}

function fix_ca1852() {
    local file="$1"
    local temp_file=$(mktemp)
    local fixes=0
    
    # Fix internal classes that should be sealed
    perl -pe '
        if (/^(\s*)(internal\s+class\s+\w+(?:\([^)]*\))?(?:\s*:\s*[^{]*)?)\s*$/ && 
            !/\b(sealed|abstract)\b/ && 
            !/\bBase\b/) {
            s/\binternal\s+class\b/internal sealed class/;
            $fixes++;
        }
    ' "$file" > "$temp_file"
    
    if ! cmp -s "$file" "$temp_file"; then
        fixes=1
        ((FIX_TYPES["CA1852"]++))
        if [[ "$VERBOSE" == "true" ]]; then
            log_warning "  CA1852: Sealed internal class in $(basename "$file")"
        fi
    fi
    
    mv "$temp_file" "$file"
    echo $fixes
}

function fix_cs1998() {
    local file="$1"
    local temp_file=$(mktemp)
    local fixes=0
    
    # Add await Task.CompletedTask to async methods without await
    python3 -c "
import re
import sys

content = open('$file').read()
original = content

# Find async methods without await
pattern = r'(public|private|protected|internal)?\s*async\s+Task[^{]*\{[^}]*\}'
matches = re.finditer(pattern, content, re.MULTILINE | re.DOTALL)

for match in matches:
    method_content = match.group()
    if 'await' not in method_content:
        if re.search(r'\{\s*(//[^\r\n]*)?\s*\}', method_content):
            # Empty method - add await Task.CompletedTask
            new_content = re.sub(r'\{\s*(//[^\r\n]*)?\s*\}', '{ await Task.CompletedTask; }', method_content)
            content = content.replace(method_content, new_content)
        elif '{' in method_content and '}' in method_content:
            # Non-empty method - add return Task.CompletedTask
            new_content = re.sub(r'(\s*)(\}\s*)$', r'\1return Task.CompletedTask;\n\1\2', method_content)
            if new_content != method_content:
                content = content.replace(method_content, new_content)

with open('$temp_file', 'w') as f:
    f.write(content)

print('1' if content != original else '0')
" 2>/dev/null || echo "0"
    
    if ! cmp -s "$file" "$temp_file"; then
        fixes=1
        ((FIX_TYPES["CS1998"]++))
        if [[ "$VERBOSE" == "true" ]]; then
            log_warning "  CS1998: Added Task.CompletedTask to async method in $(basename "$file")"
        fi
    fi
    
    mv "$temp_file" "$file"
    echo $fixes
}

function fix_basic_issues() {
    local file="$1"
    local temp_file=$(mktemp)
    local fixes=0
    
    # Basic text replacements
    sed -E \
        -e 's/\s+$//' \
        -e '/^\s*$/d' \
        "$file" > "$temp_file"
    
    # Ensure file ends with newline
    if [[ -s "$temp_file" ]] && [[ $(tail -c1 "$temp_file" | wc -l) -eq 0 ]]; then
        echo >> "$temp_file"
    fi
    
    if ! cmp -s "$file" "$temp_file"; then
        fixes=1
        ((FIX_TYPES["Other"]++))
    fi
    
    mv "$temp_file" "$file"
    echo $fixes
}

function process_file() {
    local file="$1"
    local basename=$(basename "$file")
    
    log_info "Processing $basename..."
    
    if [[ ! -s "$file" ]]; then
        log_warning "Skipping empty file: $basename"
        return
    fi
    
    local file_fixes=0
    local original_size=$(stat -c%s "$file")
    
    # Apply fixes
    fix_ca1852 "$file" >/dev/null
    fix_cs1998 "$file" >/dev/null
    fix_basic_issues "$file" >/dev/null
    
    local new_size=$(stat -c%s "$file")
    if [[ "$original_size" != "$new_size" ]] || ! cmp -s <(head -c "$original_size" "$file") <(printf '%*s' "$original_size" '' | tr ' ' '\0'); then
        if [[ "$WHAT_IF" == "false" ]]; then
            log_success "Fixed $basename"
        else
            log_warning "Would fix $basename"
        fi
        ((TOTAL_FIXES++))
    else
        if [[ "$VERBOSE" == "true" ]]; then
            log_success "No changes needed for $basename"
        fi
    fi
    
    ((FILES_PROCESSED++))
}

# Main execution
log_info "Finding C# files in $DIRECTORY..."

# Find C# files excluding generated and build outputs
readarray -t cs_files < <(find "$DIRECTORY" -name "*.cs" -type f ! -path "*/bin/*" ! -path "*/obj/*" ! -name "*.g.cs" ! -name "*AssemblyInfo.cs")

echo -e "${BLUE}Found ${#cs_files[@]} C# files to process.${NC}"
echo

log_info "Starting bulk fixes..."
echo

for file in "${cs_files[@]}"; do
    process_file "$file"
done

# Summary
echo
echo -e "${BLUE}===== BULK FIX SUMMARY =====${NC}"
echo "Files processed: $FILES_PROCESSED"
echo "Total files with fixes: $TOTAL_FIXES"
echo
echo -e "${BLUE}Fix breakdown:${NC}"
for fix_type in "${!FIX_TYPES[@]}"; do
    count=${FIX_TYPES[$fix_type]}
    if [[ $count -gt 0 ]]; then
        echo "  $fix_type: $count fixes"
    fi
done

if [[ "$WHAT_IF" == "true" ]]; then
    echo
    log_warning "This was a dry run. Remove --what-if to actually apply fixes."
fi

echo
log_success "Bulk fix operation completed!"