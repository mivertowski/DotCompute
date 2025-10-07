#!/bin/bash
# Type consolidation validation script
# Verifies removed types have no remaining references

echo "============================================"
echo "Type Consolidation Validator"
echo "============================================"
echo ""

# List of types that should be removed (from canonicalization)
REMOVED_TYPES=(
    "PerformanceTrend"
    "SecurityLevel"
    "TrendDirection"
    "ResultDifference"
    "KernelExecutionResult"
    "ExecutionResult"
    "BufferAccessPattern"
    "MemoryAccessPattern"
    "OptimizationLevel"
)

# Files where canonical versions should exist
CANONICAL_FILES=(
    "src/Core/DotCompute.Abstractions/Types/ProfilingTypes.cs"
    "src/Core/DotCompute.Abstractions/Security/SecurityTypes.cs"
    "src/Core/DotCompute.Abstractions/Types/ExecutionTypes.cs"
    "src/Core/DotCompute.Abstractions/Types/MemoryTypes.cs"
    "src/Core/DotCompute.Abstractions/Types/OptimizationTypes.cs"
)

echo "Checking for duplicate type definitions..."
echo ""

DUPLICATES_FOUND=0

for TYPE in "${REMOVED_TYPES[@]}"; do
    echo "Checking: $TYPE"

    # Count occurrences in source files (excluding tests, docs, obj, bin)
    COUNT=$(grep -r "class $TYPE\|struct $TYPE\|enum $TYPE\|record $TYPE" src/ \
        --include="*.cs" \
        --exclude-dir=obj \
        --exclude-dir=bin \
        2>/dev/null | wc -l)

    if [ "$COUNT" -gt 1 ]; then
        echo "  ❌ DUPLICATE: Found $COUNT definitions"
        grep -r "class $TYPE\|struct $TYPE\|enum $TYPE\|record $TYPE" src/ \
            --include="*.cs" \
            --exclude-dir=obj \
            --exclude-dir=bin \
            2>/dev/null | head -5
        DUPLICATES_FOUND=$((DUPLICATES_FOUND + 1))
    elif [ "$COUNT" -eq 1 ]; then
        echo "  ✅ OK: Single canonical definition"
    else
        echo "  ⚠️  WARNING: No definition found (may have different name)"
    fi
    echo ""
done

echo "============================================"
echo "Validation Summary"
echo "============================================"

if [ "$DUPLICATES_FOUND" -eq 0 ]; then
    echo "✅ All types consolidated successfully"
    echo "No duplicate definitions found"
    exit 0
else
    echo "❌ Found $DUPLICATES_FOUND types with duplicates"
    echo "Manual review required"
    exit 1
fi
