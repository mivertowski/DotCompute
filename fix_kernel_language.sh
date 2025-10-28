#!/bin/bash

# Quick fix script for KernelLanguage namespace issues

# Find all files with KernelLanguage references and fix namespace
find /home/mivertowski/DotCompute/DotCompute/src/Extensions/DotCompute.Linq -name "*.cs" -type f | xargs grep -l "KernelLanguage" | while read file; do
    echo "Fixing KernelLanguage in $file"

    # Add the correct using statement if not present
    if ! grep -q "using DotCompute.Abstractions.Kernels.Types;" "$file"; then
        # Insert after other using statements
        sed -i '/^using /a using DotCompute.Abstractions.Kernels.Types;' "$file"
    fi

    # Replace KernelLanguage references with fully qualified names where needed
    sed -i 's/\bKernelLanguage\b/DotCompute.Abstractions.Kernels.Types.KernelLanguage/g' "$file"

    # Fix some common patterns
    sed -i 's/DotCompute\.Abstractions\.Types\.KernelLanguage/DotCompute.Abstractions.Kernels.Types.KernelLanguage/g' "$file"
done

echo "Fixed KernelLanguage namespace issues in LINQ extension files"