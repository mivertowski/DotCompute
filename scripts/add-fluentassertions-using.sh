#!/bin/bash

# Add FluentAssertions using directive to test files that need it

echo "Adding FluentAssertions using directives where needed..."

# Find all test files
find /home/mivertowski/DotCompute/DotCompute/tests -name "*.cs" -type f | while read -r file; do
    # Check if file contains .Should() but doesn't have using FluentAssertions
    if grep -q "\.Should()" "$file" && ! grep -q "using FluentAssertions;" "$file"; then
        echo "Adding FluentAssertions to: $file"
        
        # Create temp file
        temp_file="${file}.temp"
        
        # Find the last using statement and add FluentAssertions after it
        # If no using statements, add after namespace declaration
        if grep -q "^using " "$file"; then
            # Add after the last using statement
            awk '/^using / {lastusing=NR} 
                 {lines[NR]=$0} 
                 END {
                    for(i=1;i<=NR;i++) {
                        print lines[i]
                        if(i==lastusing) print "using FluentAssertions;"
                    }
                 }' "$file" > "$temp_file"
        else
            # No using statements found, add at the beginning
            echo "using FluentAssertions;" > "$temp_file"
            cat "$file" >> "$temp_file"
        fi
        
        mv "$temp_file" "$file"
    fi
done

echo "FluentAssertions using directives added where needed!"