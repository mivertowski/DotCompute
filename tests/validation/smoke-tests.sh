#!/bin/bash

# DotCompute Smoke Tests
# Quick validation of critical functionality after fixes

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}=== DotCompute Smoke Tests ===${NC}"
echo "Running critical path validation..."
echo ""

passed=0
failed=0

# Function to run a smoke test
run_smoke_test() {
    local test_name="$1"
    local test_filter="$2"
    local test_project="$3"

    echo -n "Testing $test_name... "

    if dotnet test "$PROJECT_ROOT/$test_project" \
        --filter "$test_filter" \
        --configuration Release \
        --verbosity quiet \
        --no-build \
        > /dev/null 2>&1; then
        echo -e "${GREEN}✓ PASS${NC}"
        ((passed++))
        return 0
    else
        echo -e "${RED}✗ FAIL${NC}"
        ((failed++))
        return 1
    fi
}

# Critical Path 1: Core Kernel Execution
echo -e "${YELLOW}Critical Path 1: Core Kernel Execution${NC}"
run_smoke_test \
    "Kernel Definition" \
    "FullyQualifiedName~KernelDefinition" \
    "tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj"

run_smoke_test \
    "Kernel Compilation" \
    "FullyQualifiedName~KernelCompilation" \
    "tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj"

# Critical Path 2: CPU Backend
echo -e "\n${YELLOW}Critical Path 2: CPU Backend${NC}"
run_smoke_test \
    "CPU SIMD Operations" \
    "FullyQualifiedName~Simd" \
    "tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj"

run_smoke_test \
    "CPU Kernel Execution" \
    "Category=Smoke" \
    "tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj"

# Critical Path 3: Memory Management
echo -e "\n${YELLOW}Critical Path 3: Memory Management${NC}"
run_smoke_test \
    "Buffer Allocation" \
    "FullyQualifiedName~Buffer" \
    "tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj"

run_smoke_test \
    "Memory Pooling" \
    "FullyQualifiedName~Pool" \
    "tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj"

# Critical Path 4: Source Generators (if integration tests exist)
if [ -d "$PROJECT_ROOT/tests/Integration/DotCompute.Generators.Integration.Tests" ]; then
    echo -e "\n${YELLOW}Critical Path 4: Source Generators${NC}"
    run_smoke_test \
        "Kernel Attribute Generation" \
        "FullyQualifiedName~KernelAttribute" \
        "tests/Integration/DotCompute.Generators.Integration.Tests/DotCompute.Generators.Integration.Tests.csproj"
fi

# Critical Path 5: CUDA Backend (if hardware available)
if command -v nvidia-smi &> /dev/null; then
    echo -e "\n${YELLOW}Critical Path 5: CUDA Backend${NC}"
    run_smoke_test \
        "CUDA Initialization" \
        "FullyQualifiedName~CudaInit" \
        "tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj" || true

    run_smoke_test \
        "CUDA Kernel Launch" \
        "FullyQualifiedName~KernelLaunch" \
        "tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj" || true
fi

# Summary
echo -e "\n${BLUE}=== Smoke Test Summary ===${NC}"
echo "Passed: ${GREEN}$passed${NC}"
echo "Failed: ${RED}$failed${NC}"

if [ $failed -eq 0 ]; then
    echo -e "\n${GREEN}All critical paths validated successfully!${NC}"
    exit 0
else
    echo -e "\n${RED}Some critical paths failed validation!${NC}"
    exit 1
fi
