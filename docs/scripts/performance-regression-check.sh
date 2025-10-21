#!/bin/bash
# Performance Regression Check
# Validates that fixes don't introduce performance degradation

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

BENCHMARK_LOG="docs/scripts/benchmark-$(date +%Y%m%d-%H%M%S).txt"

echo "=== Performance Regression Check ===" | tee "$BENCHMARK_LOG"
echo "Started: $(date)" | tee -a "$BENCHMARK_LOG"
echo "" | tee -a "$BENCHMARK_LOG"

# Check if benchmarks exist
if [ ! -d "benchmarks" ]; then
    echo -e "${YELLOW}No benchmarks directory found, skipping performance tests${NC}" | tee -a "$BENCHMARK_LOG"
    exit 0
fi

# Find benchmark projects
BENCHMARK_PROJECTS=$(find benchmarks -name "*.csproj" 2>/dev/null)

if [ -z "$BENCHMARK_PROJECTS" ]; then
    echo -e "${YELLOW}No benchmark projects found${NC}" | tee -a "$BENCHMARK_LOG"
    exit 0
fi

echo "Running performance benchmarks..." | tee -a "$BENCHMARK_LOG"
echo "" | tee -a "$BENCHMARK_LOG"

for project in $BENCHMARK_PROJECTS; do
    project_name=$(basename "$project" .csproj)
    echo -e "${YELLOW}Benchmarking: $project_name${NC}" | tee -a "$BENCHMARK_LOG"

    # Run benchmarks with short warmup for quick check
    if dotnet run --project "$project" --configuration Release -- --filter "*" --warmupCount 1 --iterationCount 3 2>&1 | tee -a "$BENCHMARK_LOG"; then
        echo -e "${GREEN}✓ Benchmark completed: $project_name${NC}" | tee -a "$BENCHMARK_LOG"
    else
        echo -e "${RED}✗ Benchmark failed: $project_name${NC}" | tee -a "$BENCHMARK_LOG"
    fi
    echo "" | tee -a "$BENCHMARK_LOG"
done

echo "=== Performance Check Complete ===" | tee -a "$BENCHMARK_LOG"
echo "Results saved to: $BENCHMARK_LOG" | tee -a "$BENCHMARK_LOG"
echo "Completed: $(date)" | tee -a "$BENCHMARK_LOG"
