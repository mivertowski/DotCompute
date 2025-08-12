#!/bin/bash

# Script to run all tests (both CI and hardware-dependent)
# This is for comprehensive local development testing

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}Running All Tests (CI + Hardware)${NC}"
echo "================================="

# Get the script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_SETTINGS="$PROJECT_ROOT/tests/local-hardware-test.runsettings"

echo "Project Root: $PROJECT_ROOT"
echo "Test Settings: $TEST_SETTINGS"

# Check if test settings exist
if [ ! -f "$TEST_SETTINGS" ]; then
    echo -e "${RED}Error: Local test settings file not found at $TEST_SETTINGS${NC}"
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

echo ""
echo -e "${YELLOW}Running all tests...${NC}"
dotnet test --configuration Release \
    --settings "$TEST_SETTINGS" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=all-test-results.trx" \
    --logger "html;LogFileName=all-test-results.html" \
    --collect:"XPlat Code Coverage" \
    --results-directory "TestResults/All" \
    --verbosity normal

TEST_EXIT_CODE=$?

echo ""
echo "================================="
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ All Tests completed successfully${NC}"
    echo -e "Test results: $PROJECT_ROOT/TestResults/All/"
else
    echo -e "${RED}✗ Some tests failed with exit code $TEST_EXIT_CODE${NC}"
    echo -e "${YELLOW}Note: Hardware-dependent test failures may be due to missing hardware.${NC}"
fi

echo ""
echo -e "${YELLOW}Test Categories Included:${NC}"
echo "  ✓ Unit tests"
echo "  ✓ Integration tests"
echo "  ✓ Mock/Simulation tests" 
echo "  ✓ Hardware-dependent tests (if hardware available)"
echo "  ✓ Performance tests (if hardware available)"

echo ""
echo -e "${BLUE}For CI/CD environments, use: ./scripts/run-ci-tests.sh${NC}"
echo -e "${BLUE}For hardware-only testing, use: ./scripts/run-hardware-tests.sh${NC}"

exit $TEST_EXIT_CODE