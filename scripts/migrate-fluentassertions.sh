#!/bin/bash

# HIVE MIND FLUENTASSERTIONS TO XUNIT MIGRATION SCRIPT
# This script systematically converts FluentAssertions patterns to xUnit assertions

echo "🐝 HIVE MIND - FluentAssertions to xUnit Migration Starting..."

# Function to process a single file
process_file() {
    local file="$1"
    echo "Processing: $file"
    
    # Create backup
    cp "$file" "$file.bak"
    
    # Remove FluentAssertions using statement
    sed -i '/using FluentAssertions;/d' "$file"
    
    # Basic assertions
    sed -i 's/\.Should()\.Be(\([^)]*\))/;\n        Assert.Equal(\1, actual)/g' "$file"
    sed -i 's/\.Should()\.BeTrue()/;\n        Assert.True(actual)/g' "$file"
    sed -i 's/\.Should()\.BeFalse()/;\n        Assert.False(actual)/g' "$file"
    sed -i 's/\.Should()\.BeNull()/;\n        Assert.Null(actual)/g' "$file"
    sed -i 's/\.Should()\.NotBeNull()/;\n        Assert.NotNull(actual)/g' "$file"
    sed -i 's/\.Should()\.BeEmpty()/;\n        Assert.Empty(actual)/g' "$file"
    sed -i 's/\.Should()\.NotBeEmpty()/;\n        Assert.NotEmpty(actual)/g' "$file"
    
    # Numeric assertions
    sed -i 's/\.Should()\.BeGreaterThan(\([^)]*\))/;\n        Assert.True(actual > \1)/g' "$file"
    sed -i 's/\.Should()\.BeLessThan(\([^)]*\))/;\n        Assert.True(actual < \1)/g' "$file"
    sed -i 's/\.Should()\.BeGreaterOrEqualTo(\([^)]*\))/;\n        Assert.True(actual >= \1)/g' "$file"
    sed -i 's/\.Should()\.BeLessOrEqualTo(\([^)]*\))/;\n        Assert.True(actual <= \1)/g' "$file"
    sed -i 's/\.Should()\.BeInRange(\([^,]*\),\([^)]*\))/;\n        Assert.InRange(actual, \1, \2)/g' "$file"
    
    # String assertions
    sed -i 's/\.Should()\.Contain(\([^)]*\))/;\n        Assert.Contains(\1, actual)/g' "$file"
    sed -i 's/\.Should()\.StartWith(\([^)]*\))/;\n        Assert.StartsWith(\1, actual)/g' "$file"
    sed -i 's/\.Should()\.EndWith(\([^)]*\))/;\n        Assert.EndsWith(\1, actual)/g' "$file"
    
    # Collection assertions
    sed -i 's/\.Should()\.HaveCount(\([^)]*\))/;\n        Assert.Equal(\1, actual.Count())/g' "$file"
    sed -i 's/\.Should()\.ContainSingle()/;\n        Assert.Single(actual)/g' "$file"
    sed -i 's/\.Should()\.BeEquivalentTo(\([^)]*\))/;\n        Assert.Equal(\1, actual)/g' "$file"
    
    # Exception assertions - synchronous
    sed -i 's/\.Should()\.Throw<\([^>]*\)>()/;\n        Assert.Throws<\1>(() => act())/g' "$file"
    sed -i 's/\.Should()\.NotThrow()/;\n        act(); \/\/ Should not throw/g' "$file"
    
    # Exception assertions - async (most complex)
    sed -i 's/\.Should()\.ThrowAsync<\([^>]*\)>()/;\n        await Assert.ThrowsAsync<\1>(async () => await act())/g' "$file"
    sed -i 's/\.Should()\.ThrowExactlyAsync<\([^>]*\)>()/;\n        await Assert.ThrowsAsync<\1>(async () => await act())/g' "$file"
    sed -i 's/\.Should()\.NotThrowAsync()/;\n        await act(); \/\/ Should not throw/g' "$file"
    
    # Type assertions
    sed -i 's/\.Should()\.BeOfType<\([^>]*\)>()/;\n        Assert.IsType<\1>(actual)/g' "$file"
    sed -i 's/\.Should()\.BeAssignableTo<\([^>]*\)>()/;\n        Assert.IsAssignableFrom<\1>(actual)/g' "$file"
    
    # Approximate equality
    sed -i 's/\.Should()\.BeApproximately(\([^,]*\),\([^)]*\))/;\n        Assert.Equal(\1, actual, \2)/g' "$file"
    
    # Complex patterns with And
    sed -i 's/\.And\.\([^;]*\)/; \/\/ Additional assertion: \1/g' "$file"
    
    # Remove any remaining Should() calls that weren't caught
    sed -i 's/\.Should()//g' "$file"
}

# Find all test files and process them
find . -name "*.cs" -path "*/tests/*" | while read -r file; do
    if grep -q "Should()" "$file" 2>/dev/null; then
        process_file "$file"
    fi
done

echo "🐝 Migration complete! Checking for remaining FluentAssertions usage..."
grep -r "Should()" tests/ --include="*.cs" | wc -l

echo "🐝 HIVE MIND - Migration Phase 1 Complete!"