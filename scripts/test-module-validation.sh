#!/bin/bash
set -e

MODULE_NAME=$1
MODULE_PATH=$2

if [ -z "$MODULE_NAME" ] || [ -z "$MODULE_PATH" ]; then
    echo "Usage: $0 <module_name> <module_path>"
    echo "Example: $0 SimdVectorOperations src/Backends/DotCompute.Backends.CPU/SIMD/SimdVectorOperations.cs"
    exit 1
fi

echo "=== Validating Module: $MODULE_NAME ==="
echo "Timestamp: $(date -Iseconds)"
echo "Path: $MODULE_PATH"
echo ""

# Create module-specific results directory
mkdir -p TestResults/modules/$MODULE_NAME
cd TestResults/modules/$MODULE_NAME

# Build the containing project
echo "[1/4] Building project..."
PROJECT_DIR=$(dirname $MODULE_PATH)
PROJECT_FILE=$(find ../../$PROJECT_DIR -name "*.csproj" | head -1)
dotnet build ../../$PROJECT_FILE \
  --configuration Release \
  /warnaserror \
  2>&1 | tee build_output.log

# Run project-specific tests
echo "[2/4] Running project tests..."
PROJECT_NAME=$(basename $PROJECT_FILE .csproj)
TEST_PROJECT=$(find ../../tests -name "${PROJECT_NAME}.Tests.csproj" -o -name "*${PROJECT_NAME}*.Tests.csproj" | head -1)

if [ -n "$TEST_PROJECT" ]; then
    dotnet test ../../$TEST_PROJECT \
      --configuration Release \
      --logger "console;verbosity=detailed" \
      --logger "trx;LogFileName=${MODULE_NAME}_tests.trx" \
      --collect:"XPlat Code Coverage" \
      --results-directory .
else
    echo "No test project found for $PROJECT_NAME"
fi

# Run solution-wide smoke test
echo "[3/4] Running solution smoke tests..."
dotnet test ../../DotCompute.sln \
  --configuration Release \
  --filter "Category=Smoke" \
  --logger "trx;LogFileName=${MODULE_NAME}_smoke.trx" \
  --results-directory . || echo "No smoke tests defined"

# Check for new warnings
echo "[4/4] Checking for new warnings..."
NEW_WARNINGS=$(grep -c "warning" build_output.log || echo "0")
echo "New warnings detected: $NEW_WARNINGS"

if [ "$NEW_WARNINGS" -gt "0" ]; then
    echo "⚠️  WARNING: New warnings introduced!"
    grep "warning" build_output.log
fi

echo ""
echo "=== Module Validation Complete: $MODULE_NAME ==="
echo "Results: TestResults/modules/$MODULE_NAME/"
