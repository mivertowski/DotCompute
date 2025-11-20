#!/bin/bash
set -euo pipefail

# DotCompute Test Runner with automatic WSL2 CUDA configuration
# Usage: ./scripts/run-tests.sh [test-project] [dotnet-test-args...]

# Configure WSL2 CUDA path if needed
if grep -qi microsoft /proc/version 2>/dev/null; then
    if [ -d "/usr/lib/wsl/lib" ]; then
        export LD_LIBRARY_PATH="/usr/lib/wsl/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
        echo "✅ Configured WSL2 CUDA library path: $LD_LIBRARY_PATH"
    fi
fi

# Default to running all tests
TEST_PROJECT="${1:-DotCompute.sln}"
shift || true

echo "🧪 Running tests: $TEST_PROJECT"
echo "================================"

# Run tests with configured environment
dotnet test "$TEST_PROJECT" "$@"

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo ""
    echo "✅ All tests passed!"
else
    echo ""
    echo "❌ Tests failed with exit code $EXIT_CODE"
fi

exit $EXIT_CODE
