#!/bin/bash

# Find all C# files and fix System namespace references
find /home/mivertowski/DotCompute/DotCompute/src -name "*.cs" -type f | while read file; do
    # Skip the System folder itself
    if [[ "$file" == *"/System/"* ]]; then
        continue
    fi
    
    # Create temp file
    temp_file="${file}.tmp"
    
    # Replace System namespace references with global:: prefix where needed
    sed -E '
        # Replace System.IO references
        s/([^a-zA-Z0-9_])System\.IO\./\1global::System.IO./g
        
        # Replace System.Runtime references
        s/([^a-zA-Z0-9_])System\.Runtime\./\1global::System.Runtime./g
        
        # Replace System.Numerics references
        s/([^a-zA-Z0-9_])System\.Numerics\./\1global::System.Numerics./g
        
        # Replace System.Security references
        s/([^a-zA-Z0-9_])System\.Security\./\1global::System.Security./g
    ' "$file" > "$temp_file"
    
    # Only replace if changes were made
    if ! cmp -s "$file" "$temp_file"; then
        mv "$temp_file" "$file"
        echo "Fixed: $file"
    else
        rm "$temp_file"
    fi
done

echo "System namespace fixes completed"