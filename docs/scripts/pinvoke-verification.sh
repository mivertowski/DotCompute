#!/bin/bash
# P/Invoke Migration Verification
# Tests P/Invoke LibraryImport conversions

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

LOG_FILE="docs/scripts/pinvoke-verification-$(date +%Y%m%d-%H%M%S).txt"

echo "=== P/Invoke Migration Verification ===" | tee "$LOG_FILE"
echo "Started: $(date)" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Find files with LibraryImport
echo "Searching for LibraryImport declarations..." | tee -a "$LOG_FILE"

FILES_WITH_PINVOKE=$(git status --short | grep '\.cs$' | awk '{print $2}' | xargs grep -l "LibraryImport\|DllImport" 2>/dev/null || true)

if [ -z "$FILES_WITH_PINVOKE" ]; then
    echo -e "${YELLOW}No files with P/Invoke found in modified files${NC}" | tee -a "$LOG_FILE"
    exit 0
fi

echo "Files with P/Invoke:" | tee -a "$LOG_FILE"
echo "$FILES_WITH_PINVOKE" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Check for proper patterns
echo "Verifying P/Invoke patterns..." | tee -a "$LOG_FILE"

ISSUES_FOUND=0

for file in $FILES_WITH_PINVOKE; do
    echo -e "${YELLOW}Checking: $file${NC}" | tee -a "$LOG_FILE"

    # Check for LibraryImport usage (modern approach)
    LIBRARY_IMPORT_COUNT=$(grep -c "\[LibraryImport" "$file" 2>/dev/null || echo 0)
    DLL_IMPORT_COUNT=$(grep -c "\[DllImport" "$file" 2>/dev/null || echo 0)

    echo "  LibraryImport: $LIBRARY_IMPORT_COUNT" | tee -a "$LOG_FILE"
    echo "  DllImport: $DLL_IMPORT_COUNT" | tee -a "$LOG_FILE"

    # Check for proper partial method declarations
    if [ $LIBRARY_IMPORT_COUNT -gt 0 ]; then
        if grep -n "partial.*LibraryImport" "$file" >> "$LOG_FILE" 2>&1; then
            echo -e "${GREEN}✓ Found partial methods with LibraryImport${NC}" | tee -a "$LOG_FILE"
        else
            echo -e "${RED}⚠ LibraryImport without partial methods${NC}" | tee -a "$LOG_FILE"
            ISSUES_FOUND=$((ISSUES_FOUND + 1))
        fi
    fi

    # Warn about remaining DllImport
    if [ $DLL_IMPORT_COUNT -gt 0 ]; then
        echo -e "${YELLOW}⚠ File still contains DllImport (consider migration)${NC}" | tee -a "$LOG_FILE"
    fi

    echo "" | tee -a "$LOG_FILE"
done

# Build check for P/Invoke projects
echo "Building projects with P/Invoke..." | tee -a "$LOG_FILE"

# CUDA backend (major P/Invoke usage)
if echo "$FILES_WITH_PINVOKE" | grep -q "CUDA"; then
    echo -e "${YELLOW}Building CUDA backend...${NC}" | tee -a "$LOG_FILE"
    if dotnet build src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj --configuration Release --nologo 2>&1 | tee -a "$LOG_FILE"; then
        echo -e "${GREEN}✓ CUDA backend build successful${NC}" | tee -a "$LOG_FILE"
    else
        echo -e "${RED}✗ CUDA backend build failed${NC}" | tee -a "$LOG_FILE"
        ISSUES_FOUND=$((ISSUES_FOUND + 1))
    fi
fi

# Metal backend (P/Invoke usage)
if echo "$FILES_WITH_PINVOKE" | grep -q "Metal"; then
    echo -e "${YELLOW}Building Metal backend...${NC}" | tee -a "$LOG_FILE"
    if dotnet build src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj --configuration Release --nologo 2>&1 | tee -a "$LOG_FILE"; then
        echo -e "${GREEN}✓ Metal backend build successful${NC}" | tee -a "$LOG_FILE"
    else
        echo -e "${RED}✗ Metal backend build failed${NC}" | tee -a "$LOG_FILE"
        ISSUES_FOUND=$((ISSUES_FOUND + 1))
    fi
fi

echo "" | tee -a "$LOG_FILE"
echo "=== Verification Summary ===" | tee -a "$LOG_FILE"

if [ $ISSUES_FOUND -eq 0 ]; then
    echo -e "${GREEN}✓ All P/Invoke declarations verified successfully${NC}" | tee -a "$LOG_FILE"
    exit 0
else
    echo -e "${RED}✗ Found $ISSUES_FOUND issues with P/Invoke declarations${NC}" | tee -a "$LOG_FILE"
    exit 1
fi
