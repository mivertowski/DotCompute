#!/bin/bash

# Script to run hardware-dependent tests
# This script runs CUDA, OpenCL, DirectCompute, and other hardware tests

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}Running Hardware-Dependent Tests${NC}"
echo "=================================="

# Get the script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_SETTINGS="$PROJECT_ROOT/tests/hardware-only-test.runsettings"

echo "Project Root: $PROJECT_ROOT"
echo "Test Settings: $TEST_SETTINGS"

# Check if test settings exist
if [ ! -f "$TEST_SETTINGS" ]; then
    echo -e "${RED}Error: Hardware test settings file not found at $TEST_SETTINGS${NC}"
    exit 1
fi

# Change to project root
cd "$PROJECT_ROOT"

# Hardware detection functions
check_cuda() {
    echo -e "${BLUE}Checking CUDA availability...${NC}"
    if command -v nvidia-smi &> /dev/null; then
        nvidia-smi -L 2>/dev/null && echo -e "${GREEN}✓ CUDA GPU detected${NC}" || echo -e "${YELLOW}⚠ nvidia-smi available but no GPUs detected${NC}"
        return 0
    else
        echo -e "${YELLOW}⚠ CUDA not detected (nvidia-smi not found)${NC}"
        return 1
    fi
}

check_opencl() {
    echo -e "${BLUE}Checking OpenCL availability...${NC}"
    if command -v clinfo &> /dev/null; then
        clinfo -l 2>/dev/null | grep -q "Platform" && echo -e "${GREEN}✓ OpenCL platforms detected${NC}" || echo -e "${YELLOW}⚠ clinfo available but no platforms detected${NC}"
        return 0
    else
        echo -e "${YELLOW}⚠ OpenCL not detected (clinfo not found)${NC}"
        return 1
    fi
}

check_directcompute() {
    echo -e "${BLUE}Checking DirectCompute availability...${NC}"
    if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
        echo -e "${GREEN}✓ Windows detected - DirectCompute may be available${NC}"
        return 0
    else
        echo -e "${YELLOW}⚠ Not Windows - DirectCompute not available${NC}"
        return 1
    fi
}

# Perform hardware detection
echo -e "${YELLOW}Detecting available hardware...${NC}"
check_cuda
CUDA_AVAILABLE=$?

check_opencl  
OPENCL_AVAILABLE=$?

check_directcompute
DIRECTCOMPUTE_AVAILABLE=$?

echo ""
echo -e "${YELLOW}Building solution...${NC}"
dotnet build --configuration Release --no-restore

if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed${NC}"
    exit 1
fi

# Run hardware tests with warnings about missing hardware
echo ""
echo -e "${YELLOW}Running hardware tests...${NC}"
if [ $CUDA_AVAILABLE -ne 0 ] && [ $OPENCL_AVAILABLE -ne 0 ] && [ $DIRECTCOMPUTE_AVAILABLE -ne 0 ]; then
    echo -e "${YELLOW}⚠ Warning: No hardware detected. Most hardware tests will be skipped.${NC}"
    echo -e "${YELLOW}  Consider running CI tests instead: ./scripts/run-ci-tests.sh${NC}"
fi

dotnet test --configuration Release \
    --settings "$TEST_SETTINGS" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=hardware-test-results.trx" \
    --logger "html;LogFileName=hardware-test-results.html" \
    --collect:"XPlat Code Coverage" \
    --results-directory "TestResults/Hardware" \
    --verbosity normal

TEST_EXIT_CODE=$?

echo ""
echo "=================================="
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ Hardware Tests completed${NC}"
    echo -e "Test results: $PROJECT_ROOT/TestResults/Hardware/"
else
    echo -e "${RED}✗ Hardware Tests failed with exit code $TEST_EXIT_CODE${NC}"
    echo -e "${YELLOW}Note: Failures may be due to missing hardware. Check test output for skipped tests.${NC}"
fi

echo ""
echo -e "${YELLOW}Test Categories Included:${NC}"
echo "  ✓ HardwareRequired"
echo "  ✓ CudaRequired (if CUDA available)"
echo "  ✓ OpenCLRequired (if OpenCL available)"
echo "  ✓ DirectComputeRequired (if Windows)"
echo "  ✓ RTX2000 (if specific GPU available)"

if [ $CUDA_AVAILABLE -ne 0 ]; then
    echo -e "${YELLOW}⚠ CUDA tests were likely skipped due to missing hardware${NC}"
fi
if [ $OPENCL_AVAILABLE -ne 0 ]; then
    echo -e "${YELLOW}⚠ OpenCL tests were likely skipped due to missing hardware${NC}"
fi
if [ $DIRECTCOMPUTE_AVAILABLE -ne 0 ]; then
    echo -e "${YELLOW}⚠ DirectCompute tests were likely skipped due to non-Windows platform${NC}"
fi

exit $TEST_EXIT_CODE