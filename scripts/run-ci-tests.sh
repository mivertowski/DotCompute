#!/bin/bash

# Script to run CI/CD tests (no hardware dependencies)
# This script excludes hardware-dependent tests and runs only mock/simulation tests

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Running CI/CD Tests (No Hardware Dependencies)${NC}"
echo "==============================================="

# Get the script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_SETTINGS="$PROJECT_ROOT/tests/ci-test.runsettings"

echo "Project Root: $PROJECT_ROOT"
echo "Test Settings: $TEST_SETTINGS"

# Check if test settings exist
if [ ! -f "$TEST_SETTINGS" ]; then
    echo -e "${RED}Error: CI test settings file not found at $TEST_SETTINGS${NC}"
    exit 1
fi

# Change to project root
cd "$PROJECT_ROOT"

echo -e "${YELLOW}Building solution...${NC}"
dotnet build --configuration Release --no-restore

if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed${NC}"
    exit 1
fi

echo -e "${YELLOW}Running CI tests...${NC}"
dotnet test --configuration Release \
    --settings "$TEST_SETTINGS" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=ci-test-results.trx" \
    --logger "html;LogFileName=ci-test-results.html" \
    --collect:"XPlat Code Coverage" \
    --results-directory "TestResults/CI" \
    --verbosity normal

TEST_EXIT_CODE=$?

echo ""
echo "==============================================="
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ CI Tests completed successfully${NC}"
    echo -e "Test results: $PROJECT_ROOT/TestResults/CI/"
else
    echo -e "${RED}✗ CI Tests failed with exit code $TEST_EXIT_CODE${NC}"
fi

echo -e "${YELLOW}Test Categories Included:${NC}"
echo "  ✓ Mock/Simulation tests"
echo "  ✓ Unit tests"
echo "  ✓ Non-hardware dependent tests"
echo ""
echo -e "${YELLOW}Test Categories Excluded:${NC}"
echo "  ✗ HardwareRequired"
echo "  ✗ CudaRequired"
echo "  ✗ OpenCLRequired" 
echo "  ✗ DirectComputeRequired"
echo "  ✗ RTX2000"

exit $TEST_EXIT_CODE