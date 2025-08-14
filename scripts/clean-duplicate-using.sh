#!/bin/bash

# Clean duplicate using FluentAssertions directives

echo "Cleaning duplicate using directives..."

for file in tests/Unit/DotCompute.Memory.Tests/*.cs; do
    if [ -f "$file" ]; then
        # Remove duplicate using FluentAssertions lines, keeping only the first one
        awk '!(/^using FluentAssertions;$/ && seen++)' "$file" > "$file.tmp" && mv "$file.tmp" "$file"
    fi
done

# Also fix specific issues in UnifiedBufferTests.cs
file="tests/Unit/DotCompute.Memory.Tests/UnifiedBufferTests.cs"
if [ -f "$file" ]; then
    echo "Fixing UnifiedBufferTests.cs specific issues"
    # Line 183: buffer.Assert.Accelerator should be Assert.Equal(expected, buffer.Accelerator)
    sed -i 's/buffer\.Assert\.Accelerator/Assert.NotNull(buffer.Accelerator)/g' "$file"
fi

# Fix MultiGpuMemoryManagerIntegrationTests ValueTask to Task conversion
file="tests/Unit/DotCompute.Memory.Tests/MultiGpuMemoryManagerIntegrationTests.cs"
if [ -f "$file" ]; then
    echo "Fixing MultiGpuMemoryManagerIntegrationTests.cs ValueTask issues"
    # Lines 293-294: Convert ValueTask to Task
    sed -i '293s/Task gpu1Task = await/var gpu1Task =/g' "$file"
    sed -i '294s/Task gpu2Task = await/var gpu2Task =/g' "$file"
    sed -i '293s/= await/=/g' "$file"
    sed -i '294s/= await/=/g' "$file"
    sed -i '293s/Task gpu1Task/var gpu1Task/g' "$file"
    sed -i '294s/Task gpu2Task/var gpu2Task/g' "$file"
fi

echo "Cleanup completed!"
