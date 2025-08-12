#!/bin/bash

# Generate Coverage Report for DotCompute
# This script generates comprehensive HTML and other format reports from coverage data

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
INPUT_DIR="${1:-$PROJECT_ROOT/coverage-merge}"
OUTPUT_DIR="${2:-$PROJECT_ROOT/coverage-reports}"
REPORT_TITLE="${3:-DotCompute Coverage Report}"

# Function to log with timestamp
log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Validate input directory
if [[ ! -d "$INPUT_DIR" ]]; then
    log "${RED}❌ Input directory does not exist: $INPUT_DIR${NC}"
    exit 1
fi

# Find coverage files
coverage_files=($(find "$INPUT_DIR" -name "*.cobertura.xml" -o -name "*coverage*.xml"))
if [ ${#coverage_files[@]} -eq 0 ]; then
    log "${RED}❌ No coverage files found in: $INPUT_DIR${NC}"
    exit 1
fi

log "${GREEN}📊 Found ${#coverage_files[@]} coverage files${NC}"
for file in "${coverage_files[@]}"; do
    echo "  - $(basename "$file")"
done

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Install ReportGenerator if not present
if ! command -v reportgenerator >/dev/null 2>&1; then
    log "${BLUE}📦 Installing ReportGenerator...${NC}"
    dotnet tool install -g dotnet-reportgenerator-globaltool || {
        log "${YELLOW}⚠️  Using local installation...${NC}"
        dotnet tool install dotnet-reportgenerator-globaltool --tool-path ./tools
        export PATH="$PATH:$PWD/tools"
    }
fi

log "${BLUE}📈 Generating comprehensive coverage report...${NC}"

# Generate multiple report formats
reportgenerator \
    -reports:"${coverage_files[*]// /;}" \
    -targetdir:"$OUTPUT_DIR/html" \
    -reporttypes:"Html;Html_Dark;Cobertura;JsonSummary;Badges;TextSummary;TeamCitySummary;lcov" \
    -verbosity:Info \
    -title:"$REPORT_TITLE" \
    -tag:"$(date '+%Y-%m-%d_%H-%M-%S')" \
    -historydir:"$OUTPUT_DIR/history" \
    -sourcedirs:"$PROJECT_ROOT/src;$PROJECT_ROOT/plugins" \
    -classfilters:"-*.Tests*;-*.TestImplementations*;-*.SharedTestUtilities*;-*.Benchmarks*;-*.Mock*" \
    -filefilters:"-*.g.cs;-*.Designer.cs;-*.Generated.cs;-*AssemblyInfo.cs;-GlobalUsings.cs" \
    -assemblyfilters:"+DotCompute.*;-*.Tests;-*.TestImplementations;-*.SharedTestUtilities;-*.Mock*;-*.Benchmarks" || {
    log "${RED}❌ ReportGenerator failed${NC}"
    exit 1
}

# Copy generated files to output root
cp "$OUTPUT_DIR/html/Cobertura.xml" "$OUTPUT_DIR/coverage.cobertura.xml" 2>/dev/null || true
cp "$OUTPUT_DIR/html/Summary.json" "$OUTPUT_DIR/coverage-summary.json" 2>/dev/null || true
cp "$OUTPUT_DIR/html/Summary.txt" "$OUTPUT_DIR/coverage-summary.txt" 2>/dev/null || true

# Parse coverage data and generate custom reports
log "${BLUE}📊 Generating custom analysis...${NC}"

# Create coverage analysis script
cat > "$OUTPUT_DIR/analyze_coverage.py" << 'EOF'
#!/usr/bin/env python3
import json
import xml.etree.ElementTree as ET
import sys
from pathlib import Path
import os

def analyze_cobertura_coverage(xml_file):
    """Analyze Cobertura XML coverage file"""
    try:
        tree = ET.parse(xml_file)
        root = tree.getroot()
        
        # Extract overall metrics
        line_rate = float(root.get('line-rate', 0)) * 100
        branch_rate = float(root.get('branch-rate', 0)) * 100
        
        # Analyze by package/namespace
        packages = {}
        for package in root.findall('.//package'):
            name = package.get('name', 'Unknown')
            pkg_line_rate = float(package.get('line-rate', 0)) * 100
            pkg_branch_rate = float(package.get('branch-rate', 0)) * 100
            
            packages[name] = {
                'line_coverage': pkg_line_rate,
                'branch_coverage': pkg_branch_rate
            }
        
        return {
            'overall_line_coverage': line_rate,
            'overall_branch_coverage': branch_rate,
            'packages': packages
        }
    except Exception as e:
        print(f"Error analyzing coverage: {e}")
        return None

def generate_detailed_report(analysis, output_file):
    """Generate detailed coverage report"""
    if not analysis:
        return
        
    with open(output_file, 'w') as f:
        f.write("# Detailed Coverage Analysis\n\n")
        f.write(f"**Overall Line Coverage:** {analysis['overall_line_coverage']:.1f}%\n")
        f.write(f"**Overall Branch Coverage:** {analysis['overall_branch_coverage']:.1f}%\n\n")
        
        f.write("## Coverage by Component\n\n")
        f.write("| Component | Line Coverage | Branch Coverage | Status |\n")
        f.write("|-----------|---------------|-----------------|--------|\n")
        
        for name, metrics in sorted(analysis['packages'].items()):
            line_cov = metrics['line_coverage']
            branch_cov = metrics['branch_coverage']
            
            status = "✅ Good" if line_cov >= 80 else "⚠️ Needs Work" if line_cov >= 60 else "❌ Poor"
            
            f.write(f"| {name} | {line_cov:.1f}% | {branch_cov:.1f}% | {status} |\n")
        
        f.write("\n## Recommendations\n\n")
        
        low_coverage = [(name, metrics) for name, metrics in analysis['packages'].items() 
                       if metrics['line_coverage'] < 80]
        
        if low_coverage:
            f.write("### Components needing attention:\n")
            for name, metrics in low_coverage:
                f.write(f"- **{name}**: {metrics['line_coverage']:.1f}% line coverage\n")
        else:
            f.write("✅ All components meet the 80% coverage threshold!\n")

if __name__ == "__main__":
    xml_file = sys.argv[1] if len(sys.argv) > 1 else "coverage.cobertura.xml"
    output_dir = sys.argv[2] if len(sys.argv) > 2 else "."
    
    if os.path.exists(xml_file):
        analysis = analyze_cobertura_coverage(xml_file)
        if analysis:
            generate_detailed_report(analysis, os.path.join(output_dir, "detailed-analysis.md"))
            
            # Generate JSON report
            with open(os.path.join(output_dir, "coverage-analysis.json"), 'w') as f:
                json.dump(analysis, f, indent=2)
            
            print(f"Line Coverage: {analysis['overall_line_coverage']:.1f}%")
            print(f"Branch Coverage: {analysis['overall_branch_coverage']:.1f}%")
    else:
        print(f"Coverage file not found: {xml_file}")
        sys.exit(1)
EOF

# Run coverage analysis
if [[ -f "$OUTPUT_DIR/coverage.cobertura.xml" ]]; then
    python3 "$OUTPUT_DIR/analyze_coverage.py" "$OUTPUT_DIR/coverage.cobertura.xml" "$OUTPUT_DIR"
fi

# Generate README for the coverage reports
cat > "$OUTPUT_DIR/README.md" << EOF
# DotCompute Coverage Reports

**Generated:** $(date '+%Y-%m-%d %H:%M:%S UTC%z')

## Available Reports

### Interactive Reports
- [HTML Report (Light Theme)](html/index.html) - Main interactive coverage report
- [HTML Report (Dark Theme)](html_dark/index.html) - Dark theme version

### Data Formats
- [Cobertura XML](coverage.cobertura.xml) - Standard coverage format
- [LCOV](html/lcov.info) - LCOV format for other tools
- [JSON Summary](coverage-summary.json) - Machine-readable summary

### Analysis Reports
- [Text Summary](coverage-summary.txt) - Quick text overview
- [Detailed Analysis](detailed-analysis.md) - Custom analysis with recommendations
- [Coverage Analysis JSON](coverage-analysis.json) - Detailed metrics in JSON

### Visual Elements
- [Coverage Badge](html/badge_linecoverage.svg) - Line coverage badge
- [Branch Coverage Badge](html/badge_branchcoverage.svg) - Branch coverage badge

## Usage

### Viewing Reports
1. Open \`html/index.html\` in a web browser for the full interactive report
2. Use the navigation to explore coverage by assembly, namespace, and class
3. Click on individual files to see line-by-line coverage

### Integration
- Use \`coverage.cobertura.xml\` for CI/CD integration
- Use \`lcov.info\` for LCOV-compatible tools
- Parse \`coverage-summary.json\` for automated analysis

### Badges
- Line Coverage: ![Coverage](html/badge_linecoverage.svg)
- Branch Coverage: ![Branch Coverage](html/badge_branchcoverage.svg)

## Understanding Coverage Metrics

- **Line Coverage**: Percentage of executable lines that were executed during tests
- **Branch Coverage**: Percentage of code branches (if/else, switch cases) that were executed
- **Method Coverage**: Percentage of methods that were called during tests
- **Class Coverage**: Percentage of classes that had at least one method executed

## Coverage Targets

- **Minimum Acceptable**: 70%
- **Good Coverage**: 80%
- **Excellent Coverage**: 90%+

## Historical Data

Coverage history is maintained in the \`history/\` directory to track trends over time.
EOF

# Create a simple index.html redirect for convenience
cat > "$OUTPUT_DIR/index.html" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>DotCompute Coverage Reports</title>
    <meta http-equiv="refresh" content="0; url=html/index.html">
    <style>
        body { font-family: Arial, sans-serif; text-align: center; margin-top: 50px; }
        .redirect-message { font-size: 18px; color: #666; }
    </style>
</head>
<body>
    <h1>DotCompute Coverage Reports</h1>
    <p class="redirect-message">Redirecting to coverage report...</p>
    <p><a href="html/index.html">Click here if not redirected automatically</a></p>
</body>
</html>
EOF

# Final summary
log "${GREEN}✅ Coverage report generation complete!${NC}"
echo ""
echo "📊 Reports generated in: $OUTPUT_DIR"
echo "🌐 Open main report: file://$OUTPUT_DIR/html/index.html"
echo "📄 View README: $OUTPUT_DIR/README.md"
echo ""

# Extract and display coverage summary
if [[ -f "$OUTPUT_DIR/coverage-summary.txt" ]]; then
    log "${BLUE}📊 Coverage Summary:${NC}"
    head -20 "$OUTPUT_DIR/coverage-summary.txt" | tail -10
fi