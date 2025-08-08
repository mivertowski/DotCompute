#!/bin/bash

# DotCompute Benchmark Baseline Creation Script
# This script creates performance baselines for all benchmark suites

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BASELINE_DIR="$PROJECT_ROOT/benchmark-baselines/$TIMESTAMP"

echo "================================================"
echo "  DotCompute Performance Benchmark Baseline"
echo "================================================"
echo ""
echo "Timestamp: $TIMESTAMP"
echo "Output Directory: $BASELINE_DIR"
echo ""

# Create baseline directory
mkdir -p "$BASELINE_DIR"

# Function to run benchmarks for a test project
run_benchmark() {
    local project_name=$1
    local project_path=$2
    local filter=$3
    
    echo "----------------------------------------"
    echo "Running benchmarks for: $project_name"
    echo "Filter: $filter"
    echo "----------------------------------------"
    
    if [ -f "$project_path" ]; then
        # Create project-specific output directory
        local output_dir="$BASELINE_DIR/$project_name"
        mkdir -p "$output_dir"
        
        # Run the benchmark
        dotnet test "$project_path" \
            --configuration Release \
            --filter "$filter" \
            --logger "trx;LogFileName=$output_dir/results.trx" \
            --results-directory "$output_dir" \
            -- RunConfiguration.TestSessionTimeout=1800000 \
            || echo "⚠️  Benchmark failed for $project_name"
        
        echo "✅ Completed: $project_name"
        echo ""
    else
        echo "⚠️  Project not found: $project_path"
        echo ""
    fi
}

# Navigate to project root
cd "$PROJECT_ROOT"

# Restore and build all projects
echo "🔧 Restoring dependencies..."
dotnet restore

echo "🏗️  Building projects..."
dotnet build --configuration Release --no-restore

# Run benchmarks for each test suite
echo ""
echo "🚀 Running Benchmark Suites"
echo "============================"
echo ""

# Memory benchmarks
run_benchmark \
    "Memory" \
    "tests/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj" \
    "Category=Benchmark"

# Core benchmarks
run_benchmark \
    "Core" \
    "tests/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj" \
    "Category=Benchmark"

# Abstractions benchmarks
run_benchmark \
    "Abstractions" \
    "tests/DotCompute.Abstractions.Tests/DotCompute.Abstractions.Tests.csproj" \
    "Category=Benchmark"

# Plugin benchmarks
run_benchmark \
    "Plugins" \
    "tests/DotCompute.Plugins.Tests/DotCompute.Plugins.Tests.csproj" \
    "Category=Benchmark"

# Integration benchmarks
run_benchmark \
    "Integration" \
    "tests/DotCompute.Integration.Tests/DotCompute.Integration.Tests.csproj" \
    "Category=Benchmark|Category=Performance"

# Generate summary report
echo ""
echo "📊 Generating Summary Report"
echo "============================"

cat > "$BASELINE_DIR/summary.md" << EOF
# DotCompute Benchmark Baseline

## Metadata
- **Date**: $(date)
- **Timestamp**: $TIMESTAMP
- **Git Branch**: $(git branch --show-current)
- **Git Commit**: $(git rev-parse HEAD)
- **Git Status**: $(git status --short | wc -l) uncommitted changes

## Environment
- **OS**: $(uname -s)
- **Processor**: $(uname -p)
- **CPU Cores**: $(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo "Unknown")
- **.NET Version**: $(dotnet --version)

## Benchmark Results

### Test Suites Executed
EOF

# List all executed benchmarks
for dir in "$BASELINE_DIR"/*/; do
    if [ -d "$dir" ]; then
        suite_name=$(basename "$dir")
        if [ -f "$dir/results.trx" ]; then
            echo "- ✅ $suite_name" >> "$BASELINE_DIR/summary.md"
        else
            echo "- ❌ $suite_name (failed or no results)" >> "$BASELINE_DIR/summary.md"
        fi
    fi
done

echo "" >> "$BASELINE_DIR/summary.md"
echo "## File Locations" >> "$BASELINE_DIR/summary.md"
echo "" >> "$BASELINE_DIR/summary.md"
echo "Benchmark results are stored in:" >> "$BASELINE_DIR/summary.md"
echo "\`\`\`" >> "$BASELINE_DIR/summary.md"
echo "$BASELINE_DIR" >> "$BASELINE_DIR/summary.md"
echo "\`\`\`" >> "$BASELINE_DIR/summary.md"

# Create latest symlink
ln -sfn "$BASELINE_DIR" "$PROJECT_ROOT/benchmark-baselines/latest"

echo ""
echo "✅ Benchmark baseline created successfully!"
echo ""
echo "Results saved to:"
echo "  $BASELINE_DIR"
echo ""
echo "Latest results available at:"
echo "  $PROJECT_ROOT/benchmark-baselines/latest"
echo ""
echo "Summary report:"
echo "  $BASELINE_DIR/summary.md"
echo ""

# Check if there are any .trx files
if find "$BASELINE_DIR" -name "*.trx" -type f | grep -q .; then
    echo "📈 Benchmark data collected successfully"
    exit 0
else
    echo "⚠️  Warning: No benchmark results were generated"
    echo "   This might indicate that no benchmarks are defined with [Benchmark] attribute"
    exit 1
fi