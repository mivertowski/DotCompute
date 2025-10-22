#!/bin/bash
set -e

echo "=== DotCompute Cleanup Baseline Capture ==="
echo "Timestamp: $(date -Iseconds)"
echo "Git Commit: $(git rev-parse HEAD)"
echo ""

# Create results directory
mkdir -p TestResults/baseline
cd TestResults/baseline

# Capture full solution build warnings
echo "[1/5] Capturing build warnings..."
dotnet build ../../DotCompute.sln \
  --configuration Release \
  --no-incremental \
  2>&1 | tee build_warnings_baseline.log

# Run all unit tests
echo "[2/5] Running unit tests..."
dotnet test ../../tests/Unit/ \
  --configuration Release \
  --logger "trx;LogFileName=unit_tests_baseline.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./unit

# Run integration tests
echo "[3/5] Running integration tests..."
dotnet test ../../tests/Integration/ \
  --configuration Release \
  --logger "trx;LogFileName=integration_tests_baseline.trx" \
  --results-directory ./integration

# Run hardware tests (if GPU available)
echo "[4/5] Running hardware tests..."
if command -v nvidia-smi &> /dev/null; then
    dotnet test ../../tests/Hardware/DotCompute.Hardware.Cuda.Tests/ \
      --configuration Release \
      --logger "trx;LogFileName=cuda_tests_baseline.trx" \
      --results-directory ./hardware
else
    echo "CUDA not available, skipping GPU tests"
fi

# Generate coverage report
echo "[5/5] Generating coverage summary..."
dotnet tool restore
dotnet reportgenerator \
  -reports:"**/*.cobertura.xml" \
  -targetdir:"./coverage" \
  -reporttypes:"TextSummary;Html" || echo "ReportGenerator not available"

echo ""
echo "=== Baseline Capture Complete ==="
echo "Results stored in: TestResults/baseline/"
echo ""
[ -f ./coverage/Summary.txt ] && cat ./coverage/Summary.txt || echo "Coverage summary not generated"
