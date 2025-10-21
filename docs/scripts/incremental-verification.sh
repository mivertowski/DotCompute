#!/bin/bash
# Incremental Verification Script for Hive Mind Cleanup
# Tests each module independently and tracks progress

set -e

# Color codes for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Log file
LOG_FILE="docs/scripts/verification-log-$(date +%Y%m%d-%H%M%S).txt"
mkdir -p docs/scripts

echo "=== DotCompute Incremental Verification ===" | tee "$LOG_FILE"
echo "Started: $(date)" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

# Function to test a project
test_project() {
    local project_path=$1
    local project_name=$(basename "$project_path" .csproj)

    echo -e "${YELLOW}Testing: $project_name${NC}" | tee -a "$LOG_FILE"

    TOTAL_TESTS=$((TOTAL_TESTS + 1))

    if dotnet test "$project_path" --configuration Release --verbosity minimal --nologo 2>&1 | tee -a "$LOG_FILE"; then
        echo -e "${GREEN}✓ PASSED: $project_name${NC}" | tee -a "$LOG_FILE"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        return 0
    else
        echo -e "${RED}✗ FAILED: $project_name${NC}" | tee -a "$LOG_FILE"
        FAILED_TESTS=$((FAILED_TESTS + 1))
        return 1
    fi
    echo "" | tee -a "$LOG_FILE"
}

# Function to build a project
build_project() {
    local project_path=$1
    local project_name=$(basename "$(dirname "$project_path")")

    echo -e "${YELLOW}Building: $project_name${NC}" | tee -a "$LOG_FILE"

    if dotnet build "$project_path" --configuration Release --nologo 2>&1 | tee -a "$LOG_FILE"; then
        echo -e "${GREEN}✓ BUILD PASSED: $project_name${NC}" | tee -a "$LOG_FILE"
        return 0
    else
        echo -e "${RED}✗ BUILD FAILED: $project_name${NC}" | tee -a "$LOG_FILE"
        return 1
    fi
    echo "" | tee -a "$LOG_FILE"
}

# Phase 1: Build Core Components
echo "=== PHASE 1: Building Core Components ===" | tee -a "$LOG_FILE"

build_project "src/Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj" || true
build_project "src/Core/DotCompute.Memory/DotCompute.Memory.csproj" || true
build_project "src/Core/DotCompute.Core/DotCompute.Core.csproj" || true

echo "" | tee -a "$LOG_FILE"

# Phase 2: Build Backend Components (Modified Files)
echo "=== PHASE 2: Building Backend Components ===" | tee -a "$LOG_FILE"

build_project "src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj" || true
build_project "src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj" || true
build_project "src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj" || true

echo "" | tee -a "$LOG_FILE"

# Phase 3: Build Extensions (Modified Files)
echo "=== PHASE 3: Building Extensions ===" | tee -a "$LOG_FILE"

build_project "src/Extensions/DotCompute.Algorithms/DotCompute.Algorithms.csproj" || true
build_project "src/Runtime/DotCompute.Plugins/DotCompute.Plugins.csproj" || true

echo "" | tee -a "$LOG_FILE"

# Phase 4: Run Unit Tests
echo "=== PHASE 4: Running Unit Tests ===" | tee -a "$LOG_FILE"

# CPU Backend Tests (high priority - many modified files)
test_project "tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj" || true

# Core Tests
test_project "tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj" || true
test_project "tests/Unit/DotCompute.Abstractions.Tests/DotCompute.Abstractions.Tests.csproj" || true
test_project "tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj" || true

# Plugin Tests
test_project "tests/Unit/DotCompute.Plugins.Tests/DotCompute.Plugins.Tests.csproj" || true

echo "" | tee -a "$LOG_FILE"

# Phase 5: Run Hardware Tests (if available)
echo "=== PHASE 5: Running Hardware Tests (CUDA) ===" | tee -a "$LOG_FILE"

if command -v nvidia-smi &> /dev/null; then
    echo "NVIDIA GPU detected, running CUDA tests..." | tee -a "$LOG_FILE"
    test_project "tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj" || true
else
    echo "No NVIDIA GPU detected, skipping CUDA tests" | tee -a "$LOG_FILE"
fi

echo "" | tee -a "$LOG_FILE"

# Phase 6: Run Integration Tests
echo "=== PHASE 6: Running Integration Tests ===" | tee -a "$LOG_FILE"

test_project "tests/Integration/DotCompute.Generators.Integration.Tests/DotCompute.Generators.Integration.Tests.csproj" || true

echo "" | tee -a "$LOG_FILE"

# Summary
echo "=== VERIFICATION SUMMARY ===" | tee -a "$LOG_FILE"
echo "Total test projects run: $TOTAL_TESTS" | tee -a "$LOG_FILE"
echo -e "${GREEN}Passed: $PASSED_TESTS${NC}" | tee -a "$LOG_FILE"
echo -e "${RED}Failed: $FAILED_TESTS${NC}" | tee -a "$LOG_FILE"
echo "Completed: $(date)" | tee -a "$LOG_FILE"
echo "Log saved to: $LOG_FILE" | tee -a "$LOG_FILE"

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}Some tests failed. Check log for details.${NC}"
    exit 1
fi
