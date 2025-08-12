#!/bin/bash
set -e

echo "Fixing DotCompute build errors and warnings..."

# Function to fix package references
fix_package_refs() {
    local file="$1"
    echo "Fixing package references in $file"
    
    # Standardize test package versions
    sed -i 's/Version="17\.8\.0"/Version="17.12.0"/g' "$file"
    sed -i 's/Version="17\.10\.0"/Version="17.12.0"/g' "$file"
    sed -i 's/Version="17\.14\.1"/Version="17.12.0"/g' "$file"
    sed -i 's/Version="2\.6\.0"/Version="2.9.2"/g' "$file"
    sed -i 's/Version="2\.6\.2"/Version="2.9.2"/g' "$file"
    sed -i 's/Version="2\.9\.3"/Version="2.9.2"/g' "$file"
    sed -i 's/Version="2\.5\.0"/Version="2.8.2"/g' "$file"
    sed -i 's/Version="2\.5\.3"/Version="2.8.2"/g' "$file"
    sed -i 's/Version="3\.1\.3"/Version="2.8.2"/g' "$file"
    sed -i 's/Version="6\.0\.0"/Version="6.0.4"/g' "$file"
    sed -i 's/Version="6\.12\.0"/Version="6.12.1"/g' "$file"
    sed -i 's/Version="8\.5\.0"/Version="6.12.1"/g' "$file"
}

# Function to fix project references
fix_project_refs() {
    local file="$1"
    echo "Fixing project references in $file"
    
    # Fix CPU backend test project references
    if [[ "$file" == *"plugins/backends/DotCompute.Backends.CPU/tests"* ]]; then
        sed -i 's|../../../src/DotCompute.Abstractions/|../../../../src/DotCompute.Abstractions/|g' "$file"
        sed -i 's|../../../src/DotCompute.Core/|../../../../src/DotCompute.Core/|g' "$file"
    fi
}

# Fix all test project files
find . -name "*.csproj" -path "*/tests/*" -o -name "*.Tests.csproj" | while read -r file; do
    fix_package_refs "$file"
    fix_project_refs "$file"
done

# Fix specific backend test projects
if [ -f "./plugins/backends/DotCompute.Backends.CPU/tests/DotCompute.Backends.CPU.Tests.csproj" ]; then
    fix_package_refs "./plugins/backends/DotCompute.Backends.CPU/tests/DotCompute.Backends.CPU.Tests.csproj"
    fix_project_refs "./plugins/backends/DotCompute.Backends.CPU/tests/DotCompute.Backends.CPU.Tests.csproj"
fi

echo "Build error fixes completed."