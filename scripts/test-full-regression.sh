#!/bin/bash
set -e

echo "=== DotCompute Full Regression Suite ==="
echo "Timestamp: $(date -Iseconds)"
echo "Git Branch: $(git branch --show-current)"
echo "Git Commit: $(git rev-parse HEAD)"
echo ""

# Create regression results directory
mkdir -p TestResults/regression
cd TestResults/regression

# Step 1: Clean build
echo "[1/7] Clean build..."
dotnet clean ../../DotCompute.sln
dotnet build ../../DotCompute.sln \
  --configuration Release \
  /p:TreatWarningsAsErrors=true \
  2>&1 | tee regression_build.log

# Step 2: Unit tests
echo "[2/7] Unit tests..."
dotnet test ../../tests/Unit/ \
  --configuration Release \
  --logger "trx;LogFileName=regression_unit.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./unit

# Step 3: Integration tests
echo "[3/7] Integration tests..."
dotnet test ../../tests/Integration/ \
  --configuration Release \
  --logger "trx;LogFileName=regression_integration.trx" \
  --results-directory ./integration

# Step 4: Hardware tests
echo "[4/7] Hardware tests (CUDA)..."
if command -v nvidia-smi &> /dev/null; then
    nvidia-smi --query-gpu=name,driver_version,memory.total --format=csv
    dotnet test ../../tests/Hardware/DotCompute.Hardware.Cuda.Tests/ \
      --configuration Release \
      --logger "trx;LogFileName=regression_cuda.trx" \
      --results-directory ./hardware
else
    echo "CUDA not available, skipping GPU tests"
fi

# Step 5: Generator tests
echo "[5/7] Generator integration tests..."
dotnet test ../../tests/Integration/DotCompute.Generators.Integration.Tests/ \
  --configuration Release \
  --logger "trx;LogFileName=regression_generators.trx" \
  --results-directory ./generators

# Step 6: Performance benchmarks
echo "[6/7] Performance benchmarks..."
if [ -d "../../benchmarks" ]; then
    dotnet run --project ../../benchmarks/DotCompute.Benchmarks.csproj \
      --configuration Release \
      --filter "*VectorAdd*" \
      2>&1 | tee regression_benchmarks.log
else
    echo "Benchmarks not found, skipping"
fi

# Step 7: Coverage report
echo "[7/7] Generating coverage report..."
dotnet tool restore
dotnet reportgenerator \
  -reports:"**/*.cobertura.xml" \
  -targetdir:"./coverage" \
  -reporttypes:"TextSummary;HtmlInline_AzurePipelines;Cobertura" || echo "ReportGenerator not available"

echo ""
echo "=== Full Regression Suite Complete ==="
echo ""
echo "Summary:"
grep -E "Total tests|Passed|Failed|Skipped" regression_*.trx 2>/dev/null || echo "Test results in .trx files"
echo ""
echo "Coverage:"
[ -f ./coverage/Summary.txt ] && cat ./coverage/Summary.txt || echo "Coverage report not generated"
echo ""
echo "Results: TestResults/regression/"
