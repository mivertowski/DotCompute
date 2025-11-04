#!/bin/bash
# Test the KernelCache thread-safety fix by running the test 10 times

SUCCESS=0
FAILED=0

for i in {1..10}; do
    echo "=== Test run $i/10 ==="
    if dotnet test tests/Unit/DotCompute.Linq.Tests/DotCompute.Linq.Tests.csproj \
        --filter "FullyQualifiedName~KernelCacheTests.ConcurrentStoreAndGet_IsThreadSafe" \
        --nologo 2>&1 | grep -q "Passed!"; then
        SUCCESS=$((SUCCESS + 1))
        echo "✓ PASS"
    else
        echo "✗ FAIL"
        FAILED=1
        break
    fi
    echo ""
done

echo ""
echo "========================================="
if [ $FAILED -eq 0 ]; then
    echo "✅ SUCCESS: All 10 test runs passed!"
    echo "========================================="
    exit 0
else
    echo "❌ FAILED: Test failed after $SUCCESS successful runs"
    echo "========================================="
    exit 1
fi
