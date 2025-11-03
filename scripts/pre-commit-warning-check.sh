#!/bin/bash
# Pre-commit hook to prevent warning introduction
#
# Installation:
#   cp scripts/pre-commit-warning-check.sh .git/hooks/pre-commit
#   chmod +x .git/hooks/pre-commit
#
# Description:
#   Validates that the build completes with zero warnings before allowing commit.
#   This helps maintain the zero-warning achievement.

set -e

echo "🔍 Checking for compiler warnings..."

# Run build and capture output
build_output=$(dotnet build DotCompute.sln --configuration Release --no-restore 2>&1) || {
    echo "❌ Build failed! Fix compilation errors before committing."
    exit 1
}

# Extract warning count
warning_count=$(echo "$build_output" | grep -o '[0-9]* Warning(s)' | grep -o '[0-9]*' | head -1)

if [ -z "$warning_count" ]; then
    # If grep didn't find "Warning(s)", assume 0
    warning_count=0
fi

if [ "$warning_count" -gt 0 ]; then
    echo "❌ Build has $warning_count warning(s)!"
    echo ""
    echo "The DotCompute project maintains ZERO warnings."
    echo "Please fix all warnings before committing."
    echo ""
    echo "To see warnings, run:"
    echo "  dotnet build DotCompute.sln --configuration Release"
    echo ""
    echo "To bypass this check (NOT recommended), use:"
    echo "  git commit --no-verify"
    exit 1
fi

echo "✅ Build succeeded with zero warnings!"
exit 0
