#!/bin/bash

# Comprehensive Coverage Runner for DotCompute
# This script runs all tests with coverage collection and generates comprehensive reports

set -euo pipefail

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
COVERAGE_OUTPUT_DIR="$PROJECT_ROOT/coverage-reports"
COVERAGE_MERGE_DIR="$PROJECT_ROOT/coverage-merge"
COVERAGE_THRESHOLD=85
CORE_THRESHOLD=90
BACKEND_THRESHOLD=85
ALGORITHM_THRESHOLD=85

# Clean previous results
echo -e "${BLUE}🧹 Cleaning previous coverage results...${NC}"
rm -rf "$COVERAGE_OUTPUT_DIR" "$COVERAGE_MERGE_DIR"
mkdir -p "$COVERAGE_OUTPUT_DIR" "$COVERAGE_MERGE_DIR"

# Function to log with timestamp
log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Function to run tests for a specific project with coverage
run_test_with_coverage() {
    local test_project="$1"
    local project_name=$(basename "$test_project" .csproj)
    
    log "${BLUE}📊 Running coverage for: $project_name${NC}"
    
    # Create unique output directory for this test run
    local coverage_dir="$COVERAGE_MERGE_DIR/$project_name"
    mkdir -p "$coverage_dir"
    
    # Run tests with coverage
    dotnet test "$test_project" \
        --configuration Release \
        --no-build \
        --verbosity normal \
        --collect:"XPlat Code Coverage" \
        --results-directory "$coverage_dir" \
        --settings "$PROJECT_ROOT/coverlet.runsettings" \
        --logger "console;verbosity=normal" \
        --logger "trx;LogFileName=${project_name}_results.trx" || {
        log "${YELLOW}⚠️  Some tests failed in $project_name, but continuing with coverage...${NC}"
    }
    
    # Find the coverage file
    local coverage_file=$(find "$coverage_dir" -name "coverage.cobertura.xml" | head -1)
    if [[ -f "$coverage_file" ]]; then
        cp "$coverage_file" "$COVERAGE_MERGE_DIR/${project_name}_coverage.cobertura.xml"
        log "${GREEN}✅ Coverage collected for $project_name${NC}"
    else
        log "${YELLOW}⚠️  No coverage file found for $project_name${NC}"
    fi
}

# Build all projects first
log "${BLUE}🏗️  Building all projects...${NC}"
dotnet build "$PROJECT_ROOT/DotCompute.sln" --configuration Release --verbosity minimal

# Find all test projects
log "${BLUE}🔍 Discovering test projects...${NC}"
test_projects=($(find "$PROJECT_ROOT" -name "*.Tests.csproj" -o -name "*Tests.csproj" | grep -v "/bin/" | grep -v "/obj/"))

if [ ${#test_projects[@]} -eq 0 ]; then
    log "${RED}❌ No test projects found!${NC}"
    exit 1
fi

log "${GREEN}Found ${#test_projects[@]} test projects:${NC}"
for project in "${test_projects[@]}"; do
    echo "  - $(basename "$project")"
done

# Run coverage for each test project
log "${BLUE}🚀 Starting coverage collection...${NC}"
for test_project in "${test_projects[@]}"; do
    run_test_with_coverage "$test_project"
done

# Check if we have any coverage files to merge
coverage_files=($(find "$COVERAGE_MERGE_DIR" -name "*_coverage.cobertura.xml"))
if [ ${#coverage_files[@]} -eq 0 ]; then
    log "${RED}❌ No coverage files found to merge!${NC}"
    exit 1
fi

log "${GREEN}📊 Found ${#coverage_files[@]} coverage files to merge${NC}"

# Generate merged coverage report
log "${BLUE}📈 Generating comprehensive coverage report...${NC}"

# Install ReportGenerator if not present
if ! command -v reportgenerator >/dev/null 2>&1; then
    log "${BLUE}📦 Installing ReportGenerator...${NC}"
    dotnet tool install -g dotnet-reportgenerator-globaltool
fi

# Generate HTML report with detailed analysis
reportgenerator \
    -reports:"$COVERAGE_MERGE_DIR/*_coverage.cobertura.xml" \
    -targetdir:"$COVERAGE_OUTPUT_DIR/html" \
    -reporttypes:"Html;Cobertura;JsonSummary;Badges;TextSummary" \
    -verbosity:Info \
    -title:"DotCompute Test Coverage Report" \
    -tag:"$(date '+%Y-%m-%d_%H-%M-%S')" \
    -historydir:"$COVERAGE_OUTPUT_DIR/history" \
    -sourcedirs:"$PROJECT_ROOT/src" \
    -classfilters:"-*.Tests*;-*.TestImplementations*;-*.SharedTestUtilities*;-*.Benchmarks*" \
    -filefilters:"-*.g.cs;-*.Designer.cs;-*.Generated.cs;-*AssemblyInfo.cs"

# Copy merged cobertura file to output
cp "$COVERAGE_OUTPUT_DIR/html/Cobertura.xml" "$COVERAGE_OUTPUT_DIR/merged_coverage.cobertura.xml"

# Parse coverage results
log "${BLUE}📊 Analyzing coverage results...${NC}"

# Extract overall coverage percentage
if [[ -f "$COVERAGE_OUTPUT_DIR/html/Summary.json" ]]; then
    overall_coverage=$(python3 -c "
import json
with open('$COVERAGE_OUTPUT_DIR/html/Summary.json', 'r') as f:
    data = json.load(f)
    print(f\"{data.get('coverage', {}).get('linecoverage', 0):.1f}\")
" 2>/dev/null || echo "0.0")
else
    overall_coverage="0.0"
fi

# Display results
log "${GREEN}📊 Coverage Analysis Complete!${NC}"
echo "=================================="
echo "Overall Coverage: ${overall_coverage}%"
echo "Target Coverage: ${COVERAGE_THRESHOLD}%"
echo "Core Target: ${CORE_THRESHOLD}%"
echo "Backend Target: ${BACKEND_THRESHOLD}%"
echo "Algorithm Target: ${ALGORITHM_THRESHOLD}%"
echo "=================================="

# Generate coverage badge
badge_color="red"
if (( $(echo "$overall_coverage >= $COVERAGE_THRESHOLD" | bc -l) )); then
    badge_color="brightgreen"
elif (( $(echo "$overall_coverage >= 75" | bc -l) )); then
    badge_color="yellow"
fi

# Create simple badge JSON
cat > "$COVERAGE_OUTPUT_DIR/coverage-badge.json" << EOF
{
  "schemaVersion": 1,
  "label": "coverage",
  "message": "${overall_coverage}%",
  "color": "$badge_color"
}
EOF

# Create coverage summary
cat > "$COVERAGE_OUTPUT_DIR/coverage-summary.md" << EOF
# DotCompute Test Coverage Report

**Generated:** $(date '+%Y-%m-%d %H:%M:%S')

## Overall Results
- **Line Coverage:** ${overall_coverage}%
- **Target Coverage:** ${COVERAGE_THRESHOLD}%
- **Status:** $(if (( $(echo "$overall_coverage >= $COVERAGE_THRESHOLD" | bc -l) )); then echo "✅ PASSED"; else echo "❌ FAILED"; fi)

## Coverage Targets
- Core Projects: ${CORE_THRESHOLD}% minimum
- Backend Projects: ${BACKEND_THRESHOLD}% minimum  
- Algorithm Projects: ${ALGORITHM_THRESHOLD}% minimum
- Overall Solution: ${COVERAGE_THRESHOLD}% target

## Report Files
- [HTML Report]($COVERAGE_OUTPUT_DIR/html/index.html)
- [Cobertura XML]($COVERAGE_OUTPUT_DIR/merged_coverage.cobertura.xml)
- [JSON Summary]($COVERAGE_OUTPUT_DIR/html/Summary.json)

## Next Steps
$(if (( $(echo "$overall_coverage < $COVERAGE_THRESHOLD" | bc -l) )); then 
echo "1. Review uncovered code in HTML report"
echo "2. Add missing unit tests"
echo "3. Focus on critical paths and edge cases"
echo "4. Re-run coverage to verify improvements"
else
echo "✅ Coverage targets met! Great work!"
fi)
EOF

# Check if coverage meets threshold
if (( $(echo "$overall_coverage >= $COVERAGE_THRESHOLD" | bc -l) )); then
    log "${GREEN}✅ Coverage threshold met: ${overall_coverage}% >= ${COVERAGE_THRESHOLD}%${NC}"
    echo "0" > "$COVERAGE_OUTPUT_DIR/exit_code"
    exit_code=0
else
    log "${RED}❌ Coverage below threshold: ${overall_coverage}% < ${COVERAGE_THRESHOLD}%${NC}"
    echo "1" > "$COVERAGE_OUTPUT_DIR/exit_code"
    exit_code=1
fi

# Display helpful information
echo ""
log "${BLUE}📊 Reports generated in: $COVERAGE_OUTPUT_DIR${NC}"
log "${BLUE}📊 Open HTML report: file://$COVERAGE_OUTPUT_DIR/html/index.html${NC}"
log "${BLUE}📊 Coverage summary: $COVERAGE_OUTPUT_DIR/coverage-summary.md${NC}"

exit $exit_code