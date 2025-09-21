#!/bin/bash

# Fix the duplicated namespace issues created by the previous script

find /home/mivertowski/DotCompute/DotCompute/src/Extensions/DotCompute.Linq -name "*.cs" -type f | while read file; do
    echo "Cleaning duplicates in $file"

    # Remove duplicated DotCompute.Abstractions.Kernels.Types.DotCompute.Abstractions.Kernels.Types
    sed -i 's/DotCompute\.Abstractions\.Kernels\.Types\.DotCompute\.Abstractions\.Kernels\.Types\./DotCompute.Abstractions.Kernels.Types./g' "$file"

    # Remove duplicate using statements
    awk '!seen[$0]++' "$file" > "$file.tmp" && mv "$file.tmp" "$file"
done

echo "Cleaned up duplicate namespace issues"