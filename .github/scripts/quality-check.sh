#!/bin/bash
set -euo pipefail

# Quality check script for DotCompute
# Usage: ./quality-check.sh [fix]

FIX_MODE="${1:-false}"
EXIT_CODE=0

echo "🔍 DotCompute Quality Check"
echo "Fix Mode: $FIX_MODE"
echo "================================"

# Function to run check and optionally fix
run_check() {
    local name="$1"
    local check_cmd="$2"
    local fix_cmd="${3:-}"
    
    echo "🔍 $name..."
    
    if eval "$check_cmd"; then
        echo "✅ $name passed"
    else
        echo "❌ $name failed"
        EXIT_CODE=1
        
        if [ "$FIX_MODE" = "fix" ] && [ -n "$fix_cmd" ]; then
            echo "🔧 Attempting to fix $name..."
            if eval "$fix_cmd"; then
                echo "✅ $name fixed"
            else
                echo "❌ Failed to fix $name"
            fi
        fi
    fi
    echo ""
}

# Restore dependencies first
echo "📦 Restoring dependencies..."
dotnet restore --verbosity quiet
echo ""

# Code formatting
run_check "Code Formatting" \
    "dotnet format --verify-no-changes --verbosity quiet" \
    "dotnet format --verbosity quiet"

# Build
run_check "Build" \
    "dotnet build --configuration Release --no-restore --verbosity quiet"

# Tests
run_check "Unit Tests" \
    "dotnet test --configuration Release --no-build --verbosity quiet --logger 'console;verbosity=quiet'"

# Security vulnerabilities
run_check "Security Vulnerabilities" \
    "dotnet list package --vulnerable --include-transitive | grep -q 'has no vulnerable packages' || exit 1"

# Deprecated packages
echo "🔍 Checking for deprecated packages..."
DEPRECATED_OUTPUT=$(dotnet list package --deprecated 2>/dev/null || echo "")
if echo "$DEPRECATED_OUTPUT" | grep -q "deprecated"; then
    echo "⚠️  Found deprecated packages:"
    echo "$DEPRECATED_OUTPUT"
    echo ""
else
    echo "✅ No deprecated packages found"
    echo ""
fi

# Code coverage (if available)
if [ -f "scripts/ci/coverage.sh" ]; then
    run_check "Code Coverage" \
        "bash scripts/ci/coverage.sh check"
fi

# NuGet package validation
echo "🔍 Validating NuGet packages..."
if [ -d "artifacts/packages" ]; then
    PACKAGE_COUNT=$(find artifacts/packages -name "*.nupkg" ! -name "*.symbols.nupkg" | wc -l)
    if [ "$PACKAGE_COUNT" -gt 0 ]; then
        echo "✅ Found $PACKAGE_COUNT NuGet packages"
        
        # Validate package metadata
        for package in artifacts/packages/*.nupkg; do
            if [[ $package != *".symbols.nupkg" ]]; then
                echo "  📦 $(basename "$package")"
                # Additional package validation could go here
            fi
        done
    else
        echo "❌ No NuGet packages found"
        EXIT_CODE=1
    fi
else
    echo "⚠️  artifacts/packages directory not found"
fi
echo ""

# Summary
echo "================================"
if [ $EXIT_CODE -eq 0 ]; then
    echo "✅ All quality checks passed!"
else
    echo "❌ Some quality checks failed!"
    
    if [ "$FIX_MODE" != "fix" ]; then
        echo ""
        echo "💡 To attempt automatic fixes, run:"
        echo "   $0 fix"
    fi
fi

exit $EXIT_CODE