#!/bin/bash

# Coverage Validation Script
# This script runs comprehensive coverage analysis for DotCompute

set -e

echo "🔍 Coverage Validator - Starting Coverage Analysis"
echo "=================================================="

# Create coverage directory
mkdir -p coverage
rm -rf coverage/*

# Define test projects
TEST_PROJECTS=(
    "tests/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj"
    "plugins/backends/DotCompute.Backends.CPU/tests/DotCompute.Backends.CPU.Tests.csproj"
    "plugins/backends/DotCompute.Backends.Metal/tests/DotCompute.Backends.Metal.Tests.csproj"
)

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "📊 Running Tests with Coverage Collection..."
echo "============================================="

# Run all tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" \
    --results-directory ./coverage \
    --configuration Release \
    --settings coverage.runsettings \
    --logger trx \
    --verbosity normal \
    --no-restore

echo ""
echo "📈 Generating Coverage Reports..."
echo "================================="

# Install report generator if not available
if ! command -v reportgenerator &> /dev/null; then
    echo "Installing ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
fi

# Generate comprehensive coverage report
reportgenerator \
    "-reports:coverage/*/coverage.cobertura.xml" \
    "-targetdir:coverage/report" \
    "-reporttypes:Html;JsonSummary;Badges;TextSummary;Cobertura" \
    "-assemblyfilters:+DotCompute.*;-*.Tests;-*.TestUtilities" \
    "-classfilters:-*Program*;-*AssemblyInfo*" \
    "-filefilters:-*.g.cs;-*.designer.cs;-*GlobalUsings.cs" \
    "-verbosity:Info"

echo ""
echo "📋 Coverage Summary"
echo "==================="

# Parse and display coverage summary
if [ -f "coverage/report/Summary.json" ]; then
    echo "Parsing coverage results..."
    
    # Extract coverage percentages using jq if available, otherwise use simple grep
    if command -v jq &> /dev/null; then
        LINE_COVERAGE=$(jq -r '.summary.linecoverage' coverage/report/Summary.json)
        BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage' coverage/report/Summary.json)
        METHOD_COVERAGE=$(jq -r '.summary.methodcoverage' coverage/report/Summary.json)
        
        echo -e "📊 Line Coverage:    ${GREEN}${LINE_COVERAGE}%${NC}"
        echo -e "📊 Branch Coverage:  ${GREEN}${BRANCH_COVERAGE}%${NC}"
        echo -e "📊 Method Coverage:  ${GREEN}${METHOD_COVERAGE}%${NC}"
        
        # Check if coverage meets targets
        LINE_TARGET=95
        BRANCH_TARGET=90
        METHOD_TARGET=95
        
        echo ""
        echo "🎯 Coverage Target Validation"
        echo "============================"
        
        if (( $(echo "$LINE_COVERAGE >= $LINE_TARGET" | bc -l) )); then
            echo -e "✅ Line Coverage: ${GREEN}PASS${NC} (${LINE_COVERAGE}% >= ${LINE_TARGET}%)"
        else
            echo -e "❌ Line Coverage: ${RED}FAIL${NC} (${LINE_COVERAGE}% < ${LINE_TARGET}%)"
        fi
        
        if (( $(echo "$BRANCH_COVERAGE >= $BRANCH_TARGET" | bc -l) )); then
            echo -e "✅ Branch Coverage: ${GREEN}PASS${NC} (${BRANCH_COVERAGE}% >= ${BRANCH_TARGET}%)"
        else
            echo -e "❌ Branch Coverage: ${RED}FAIL${NC} (${BRANCH_COVERAGE}% < ${BRANCH_TARGET}%)"
        fi
        
        if (( $(echo "$METHOD_COVERAGE >= $METHOD_TARGET" | bc -l) )); then
            echo -e "✅ Method Coverage: ${GREEN}PASS${NC} (${METHOD_COVERAGE}% >= ${METHOD_TARGET}%)"
        else
            echo -e "❌ Method Coverage: ${RED}FAIL${NC} (${METHOD_COVERAGE}% < ${METHOD_TARGET}%)"
        fi
        
    else
        echo "jq not available, displaying raw summary..."
        cat coverage/report/Summary.txt 2>/dev/null || echo "Summary not available"
    fi
else
    echo "⚠️  Coverage summary not found, checking for alternative formats..."
    ls -la coverage/report/
fi

echo ""
echo "📁 Coverage Report Location"
echo "=========================="
echo "HTML Report: coverage/report/index.html"
echo "JSON Summary: coverage/report/Summary.json"
echo "Cobertura XML: coverage/report/Cobertura.xml"

echo ""
echo "🔍 Coverage Analysis Complete"
echo "============================="