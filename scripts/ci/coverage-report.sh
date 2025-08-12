#!/bin/bash
set -euo pipefail

# Coverage report generation script for DotCompute
# Usage: ./coverage-report.sh [output-format] [threshold]

OUTPUT_FORMAT="${1:-Html}"
THRESHOLD="${2:-85.0}"
REPORT_DIR="artifacts/coverage-reports"
RESULTS_DIR="artifacts/coverage"

echo "📊 DotCompute Coverage Report Generator"
echo "Output Format: $OUTPUT_FORMAT"
echo "Threshold: $THRESHOLD%"
echo "Results Directory: $RESULTS_DIR"
echo "Report Directory: $REPORT_DIR"
echo "================================"

# Check if ReportGenerator is available
if ! command -v reportgenerator &> /dev/null; then
    echo "📦 Installing ReportGenerator..."
    dotnet tool install --global dotnet-reportgenerator-globaltool
fi

# Create report directory
mkdir -p "$REPORT_DIR"

# Check if coverage files exist
if [ ! -d "$RESULTS_DIR" ] || [ -z "$(find "$RESULTS_DIR" -name "*.xml" -o -name "*.cobertura.xml" 2>/dev/null)" ]; then
    echo "❌ No coverage files found in $RESULTS_DIR"
    echo "Please run tests with coverage collection first:"
    echo "  dotnet test --collect:\"XPlat Code Coverage\""
    exit 1
fi

# Find coverage files
COVERAGE_FILES=$(find "$RESULTS_DIR" -name "*.xml" -o -name "*.cobertura.xml" | tr '\n' ';')
echo "📁 Found coverage files: $COVERAGE_FILES"

# Generate reports
echo "📊 Generating coverage reports..."

# Support multiple output formats
IFS=',' read -ra FORMATS <<< "$OUTPUT_FORMAT"
for format in "${FORMATS[@]}"; do
    format=$(echo "$format" | tr '[:lower:]' '[:upper:]')
    echo "  📄 Generating $format report..."
    
    reportgenerator \
        -reports:"$COVERAGE_FILES" \
        -targetdir:"$REPORT_DIR/$format" \
        -reporttypes:"$format" \
        -verbosity:Warning \
        -title:"DotCompute Coverage Report" \
        -tag:"$(git rev-parse --short HEAD 2>/dev/null || echo 'local')" \
        -historydir:"$REPORT_DIR/history" \
        -assemblyfilters:"+DotCompute.*;-*.Tests;-*.Benchmarks" \
        -classfilters:"+*;-*Test*;-*Mock*;-*Stub*" \
        -filefilters:"+*;-*Test*;-*AssemblyInfo.cs;-*GlobalSuppressions.cs"
done

# Always generate JSON summary for parsing
echo "📄 Generating JSON summary..."
reportgenerator \
    -reports:"$COVERAGE_FILES" \
    -targetdir:"$REPORT_DIR" \
    -reporttypes:"JsonSummary" \
    -verbosity:Warning

# Parse coverage percentage from JSON summary
if [ -f "$REPORT_DIR/Summary.json" ]; then
    COVERAGE=$(cat "$REPORT_DIR/Summary.json" | grep -o '"linecoverage":[0-9.]*' | cut -d':' -f2)
    BRANCH_COVERAGE=$(cat "$REPORT_DIR/Summary.json" | grep -o '"branchcoverage":[0-9.]*' | cut -d':' -f2)
    
    echo ""
    echo "📊 Coverage Summary:"
    echo "  Line Coverage: $COVERAGE%"
    echo "  Branch Coverage: $BRANCH_COVERAGE%"
    echo "  Threshold: $THRESHOLD%"
    echo ""
    
    # Check threshold
    if (( $(echo "$COVERAGE >= $THRESHOLD" | bc -l) )); then
        echo "✅ Coverage meets threshold ($COVERAGE% >= $THRESHOLD%)"
        THRESHOLD_MET=true
    else
        echo "❌ Coverage below threshold ($COVERAGE% < $THRESHOLD%)"
        THRESHOLD_MET=false
    fi
    
    # Create coverage badge data
    mkdir -p "$REPORT_DIR/badges"
    
    # Determine badge color based on coverage
    if (( $(echo "$COVERAGE >= 90" | bc -l) )); then
        COLOR="brightgreen"
    elif (( $(echo "$COVERAGE >= 80" | bc -l) )); then
        COLOR="green"
    elif (( $(echo "$COVERAGE >= 70" | bc -l) )); then
        COLOR="yellow"
    elif (( $(echo "$COVERAGE >= 60" | bc -l) )); then
        COLOR="orange"
    else
        COLOR="red"
    fi
    
    echo "coverage-${COVERAGE}%-${COLOR}" > "$REPORT_DIR/badges/coverage.txt"
    
    # Create coverage summary file
    cat > "$REPORT_DIR/coverage-summary.txt" << EOF
DotCompute Coverage Summary
Generated: $(date)
Commit: $(git rev-parse --short HEAD 2>/dev/null || echo 'local')

Line Coverage: $COVERAGE%
Branch Coverage: $BRANCH_COVERAGE%
Threshold: $THRESHOLD%
Status: $([ "$THRESHOLD_MET" = "true" ] && echo "PASS" || echo "FAIL")
EOF

else
    echo "❌ Failed to parse coverage summary"
    exit 1
fi

# List generated reports
echo ""
echo "📁 Generated reports:"
find "$REPORT_DIR" -type f -name "*.html" -o -name "*.xml" -o -name "*.json" | sort

# Display HTML report paths
if [ -f "$REPORT_DIR/HTML/index.html" ]; then
    echo ""
    echo "🌐 Open HTML report:"
    echo "  file://$(realpath "$REPORT_DIR/HTML/index.html")"
fi

if [ -f "$REPORT_DIR/HtmlInline/index.html" ]; then
    echo "  file://$(realpath "$REPORT_DIR/HtmlInline/index.html")"
fi

echo ""
if [ "$THRESHOLD_MET" = "true" ]; then
    echo "✅ Coverage report generation completed successfully!"
    exit 0
else
    echo "❌ Coverage report generation completed, but threshold not met!"
    exit 1
fi