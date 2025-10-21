#!/bin/bash
# Logger Delegate Verification
# Specifically tests logger delegate conversions

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

LOG_FILE="docs/scripts/logger-verification-$(date +%Y%m%d-%H%M%S).txt"

echo "=== Logger Delegate Verification ===" | tee "$LOG_FILE"
echo "Started: $(date)" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Find files with logger delegates
echo "Searching for LoggerMessage delegates..." | tee -a "$LOG_FILE"

FILES_WITH_LOGGERS=$(git status --short | grep '\.cs$' | awk '{print $2}' | xargs grep -l "LoggerMessage" 2>/dev/null || true)

if [ -z "$FILES_WITH_LOGGERS" ]; then
    echo -e "${YELLOW}No files with LoggerMessage found in modified files${NC}" | tee -a "$LOG_FILE"
    exit 0
fi

echo "Files with logger delegates:" | tee -a "$LOG_FILE"
echo "$FILES_WITH_LOGGERS" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Verify logger delegate patterns
echo "Verifying logger delegate patterns..." | tee -a "$LOG_FILE"

ISSUES_FOUND=0

for file in $FILES_WITH_LOGGERS; do
    echo -e "${YELLOW}Checking: $file${NC}" | tee -a "$LOG_FILE"

    # Check for proper delegate definition pattern
    if grep -n "private static readonly Action<ILogger" "$file" >> "$LOG_FILE" 2>&1; then
        echo -e "${GREEN}✓ Found delegate definitions${NC}" | tee -a "$LOG_FILE"
    fi

    # Check for LoggerMessage.Define usage
    if grep -n "LoggerMessage.Define" "$file" >> "$LOG_FILE" 2>&1; then
        echo -e "${GREEN}✓ Found LoggerMessage.Define calls${NC}" | tee -a "$LOG_FILE"
    fi

    # Check for potential issues (inline logging instead of delegates)
    if grep -n "_logger\.Log[A-Z]" "$file" | grep -v "LoggerMessage" >> "$LOG_FILE" 2>&1; then
        echo -e "${RED}⚠ Found potential inline logging (should use delegate)${NC}" | tee -a "$LOG_FILE"
        ISSUES_FOUND=$((ISSUES_FOUND + 1))
    fi

    echo "" | tee -a "$LOG_FILE"
done

# Compile projects with logger changes
echo "Compiling projects with logger changes..." | tee -a "$LOG_FILE"

for file in $FILES_WITH_LOGGERS; do
    project=$(find "$(dirname "$file")" -maxdepth 3 -name "*.csproj" | head -1)
    if [ -n "$project" ]; then
        project_name=$(basename "$project" .csproj)
        echo -e "${YELLOW}Building: $project_name${NC}" | tee -a "$LOG_FILE"

        if dotnet build "$project" --configuration Release --nologo 2>&1 | tee -a "$LOG_FILE"; then
            echo -e "${GREEN}✓ Build successful${NC}" | tee -a "$LOG_FILE"
        else
            echo -e "${RED}✗ Build failed${NC}" | tee -a "$LOG_FILE"
            ISSUES_FOUND=$((ISSUES_FOUND + 1))
        fi
    fi
done

echo "" | tee -a "$LOG_FILE"
echo "=== Verification Summary ===" | tee -a "$LOG_FILE"

if [ $ISSUES_FOUND -eq 0 ]; then
    echo -e "${GREEN}✓ All logger delegates verified successfully${NC}" | tee -a "$LOG_FILE"
    exit 0
else
    echo -e "${RED}✗ Found $ISSUES_FOUND issues with logger delegates${NC}" | tee -a "$LOG_FILE"
    exit 1
fi
