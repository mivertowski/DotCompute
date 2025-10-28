#!/bin/bash
# Test Coverage Generation Script
# Generates code coverage reports using coverlet

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
COVERAGE_DIR="$PROJECT_ROOT/coverage"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Test Coverage Generation${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Create coverage directory
mkdir -p "$COVERAGE_DIR"

# Check if coverlet is available
if ! dotnet tool list -g | grep -q "coverlet.console"; then
    echo -e "${YELLOW}Installing coverlet.console...${NC}"
    dotnet tool install -g coverlet.console || true
fi

echo -e "${GREEN}Running tests with coverage collection...${NC}"
echo ""

# Run tests with coverage
COVERAGE_FILE="$COVERAGE_DIR/coverage_${TIMESTAMP}"

dotnet test "$PROJECT_ROOT/DotCompute.sln" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput="$COVERAGE_FILE" \
    /p:ExcludeByFile="**/*Designer.cs" \
    /p:ExcludeByAttribute="Obsolete,GeneratedCode,CompilerGenerated" \
    --verbosity normal

echo ""
echo -e "${GREEN}Coverage data collected!${NC}"

# Generate HTML report if reportgenerator is available
if command -v reportgenerator &> /dev/null; then
    echo ""
    echo -e "${BLUE}Generating HTML coverage report...${NC}"

    reportgenerator \
        "-reports:$COVERAGE_FILE.cobertura.xml" \
        "-targetdir:$COVERAGE_DIR/html_${TIMESTAMP}" \
        "-reporttypes:Html;Badges" \
        "-historydir:$COVERAGE_DIR/history"

    echo -e "${GREEN}HTML report generated!${NC}"
    echo -e "${BLUE}Open: $COVERAGE_DIR/html_${TIMESTAMP}/index.html${NC}"
else
    echo ""
    echo -e "${YELLOW}reportgenerator not found. Install with:${NC}"
    echo "dotnet tool install -g dotnet-reportgenerator-globaltool"
fi

# Extract coverage percentage from cobertura XML
if [ -f "$COVERAGE_FILE.cobertura.xml" ]; then
    echo ""
    echo -e "${BLUE}Coverage Summary:${NC}"

    LINE_RATE=$(grep -oP 'line-rate="\K[^"]+' "$COVERAGE_FILE.cobertura.xml" | head -1)
    BRANCH_RATE=$(grep -oP 'branch-rate="\K[^"]+' "$COVERAGE_FILE.cobertura.xml" | head -1)

    LINE_PERCENT=$(echo "$LINE_RATE * 100" | bc)
    BRANCH_PERCENT=$(echo "$BRANCH_RATE * 100" | bc)

    echo -e "  Line Coverage:   ${GREEN}${LINE_PERCENT}%${NC}"
    echo -e "  Branch Coverage: ${GREEN}${BRANCH_PERCENT}%${NC}"

    # Generate JSON summary
    SUMMARY_JSON="$COVERAGE_DIR/summary_${TIMESTAMP}.json"
    cat > "$SUMMARY_JSON" << EOF
{
  "timestamp": "$(date -Iseconds)",
  "coverage": {
    "line_rate": $LINE_RATE,
    "line_percent": $LINE_PERCENT,
    "branch_rate": $BRANCH_RATE,
    "branch_percent": $BRANCH_PERCENT
  },
  "files": {
    "cobertura": "$COVERAGE_FILE.cobertura.xml",
    "html_report": "$COVERAGE_DIR/html_${TIMESTAMP}/index.html"
  }
}
EOF

    echo ""
    echo -e "${GREEN}Coverage summary saved to: $SUMMARY_JSON${NC}"
fi

echo ""
echo -e "${GREEN}✅ Coverage generation complete!${NC}"
