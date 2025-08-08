#!/bin/bash

# Fix incorrect project references in test projects
# The paths should be ../../../src instead of ../../src

echo "Fixing project references in test projects..."

# Find all test project files and fix the references
find tests -name "*.csproj" -type f | while read -r file; do
    echo "Processing: $file"
    
    # Fix Windows-style paths
    sed -i 's|Include="\\.\\.\\\\src\\\\|Include="\\.\\.\\.\\.\\.\\src\\|g' "$file"
    sed -i 's|Include="\.\.\\src\\|Include="..\\..\\..\\src\\|g' "$file"
    
    # Fix Unix-style paths
    sed -i 's|Include="../../src/|Include="../../../src/|g' "$file"
    
    # Fix mixed paths
    sed -i 's|Include="\.\./\.\./src/|Include="../../../src/|g' "$file"
    
    # Also fix plugin references
    sed -i 's|Include="\\.\\.\\\\plugins\\\\|Include="\\.\\.\\.\\.\\.\\plugins\\|g' "$file"
    sed -i 's|Include="\.\.\\plugins\\|Include="..\\..\\..\\plugins\\|g' "$file"
    sed -i 's|Include="../../plugins/|Include="../../../plugins/|g' "$file"
done

echo "Fixed project references in all test projects."