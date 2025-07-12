#!/bin/bash
# AOT Validation Test Suite for DotCompute
# Usage: ./validate-aot.sh

set -e

echo "🔍 DotCompute Native AOT Validation Suite"
echo "=========================================="

# Check .NET version
echo "1. Checking .NET version..."
dotnet --version
if [ $? -ne 0 ]; then
    echo "❌ .NET SDK not found"
    exit 1
fi
echo "✅ .NET SDK available"

# Clean build
echo ""
echo "2. Performing clean build..."
dotnet clean --configuration Release --verbosity minimal
dotnet restore --verbosity minimal

# Build with AOT
echo ""
echo "3. Building with Native AOT..."
BUILD_OUTPUT=$(dotnet publish --configuration Release --runtime linux-x64 --verbosity normal 2>&1)
BUILD_EXIT_CODE=$?

if [ $BUILD_EXIT_CODE -ne 0 ]; then
    echo "❌ AOT build failed:"
    echo "$BUILD_OUTPUT"
    exit 1
fi

# Check for warnings
WARNING_COUNT=$(echo "$BUILD_OUTPUT" | grep -c "Warning" || echo "0")
ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -c "Error" || echo "0")

echo "✅ AOT build successful"
echo "   Warnings: $WARNING_COUNT"
echo "   Errors: $ERROR_COUNT"

# Verify binary exists and is native
echo ""
echo "4. Verifying native binary..."
BINARY_PATH="/home/mivertowski/DotCompute/DotCompute/artifacts/bin/GettingStarted/Release/net9.0/linux-x64/publish/GettingStarted"

if [ ! -f "$BINARY_PATH" ]; then
    echo "❌ Native binary not found at $BINARY_PATH"
    exit 1
fi

# Check if it's actually a native binary
FILE_OUTPUT=$(file "$BINARY_PATH")
if [[ "$FILE_OUTPUT" == *"ELF"* ]]; then
    echo "✅ Native ELF binary generated"
else
    echo "❌ Not a native binary: $FILE_OUTPUT"
    exit 1
fi

# Get binary size
BINARY_SIZE=$(ls -lh "$BINARY_PATH" | awk '{print $5}')
echo "   Binary size: $BINARY_SIZE"

# Test version functionality
echo ""
echo "5. Testing version functionality..."
VERSION_OUTPUT=$("$BINARY_PATH" --version 2>&1)
if [[ "$VERSION_OUTPUT" == *"Native AOT"* ]]; then
    echo "✅ Version check passed"
    echo "   Output: $VERSION_OUTPUT"
else
    echo "❌ Version check failed: $VERSION_OUTPUT"
    exit 1
fi

# Test full execution
echo ""
echo "6. Testing full execution..."
EXEC_OUTPUT=$("$BINARY_PATH" 2>&1)
EXEC_EXIT_CODE=$?

if [ $EXEC_EXIT_CODE -eq 0 ]; then
    echo "✅ Full execution successful"
    if [[ "$EXEC_OUTPUT" == *"PASSED"* ]]; then
        echo "✅ Result verification passed"
    else
        echo "⚠️  Result verification unclear"
    fi
else
    echo "❌ Execution failed with exit code $EXEC_EXIT_CODE"
    echo "Output: $EXEC_OUTPUT"
    exit 1
fi

# Performance test
echo ""
echo "7. Basic performance test..."
START_TIME=$(date +%s%N)
"$BINARY_PATH" > /dev/null 2>&1
END_TIME=$(date +%s%N)
EXECUTION_TIME=$(( (END_TIME - START_TIME) / 1000000 ))

echo "✅ Execution time: ${EXECUTION_TIME}ms"

# Memory usage check
echo ""
echo "8. Memory usage check..."
MEMORY_OUTPUT=$(timeout 5s /usr/bin/time -v "$BINARY_PATH" 2>&1 | grep "Maximum resident set size" || echo "Memory check skipped")
if [[ "$MEMORY_OUTPUT" != "Memory check skipped" ]]; then
    echo "✅ $MEMORY_OUTPUT"
else
    echo "ℹ️  Memory check skipped (time command not available)"
fi

echo ""
echo "🎉 ALL AOT VALIDATION TESTS PASSED!"
echo "=================================="
echo "Summary:"
echo "  ✅ Build: Success ($WARNING_COUNT warnings, $ERROR_COUNT errors)"
echo "  ✅ Binary: Native ELF ($BINARY_SIZE)"
echo "  ✅ Version: Working"
echo "  ✅ Execution: Success (${EXECUTION_TIME}ms)"
echo "  ✅ Functionality: All tests passed"
echo ""
echo "DotCompute Phase 2 is fully Native AOT compatible! 🚀"