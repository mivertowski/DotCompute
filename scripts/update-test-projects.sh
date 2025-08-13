#!/bin/bash

# Script to update all test project files with correct paths after reorganization

echo "Updating test project references..."

# Function to update project references in a file
update_project_refs() {
    local file=$1
    if [[ -f "$file" ]]; then
        # Update relative paths from ..\..\src to ..\..\..\src (one more level up)
        sed -i 's|Include="\\.\\.\\\\\\.\\.\\\\src\\\\|Include="..\\..\\..\\src\\|g' "$file"
        sed -i 's|Include="\\.\\.\\/\\.\\.\\/src\\/|Include="../../../src/|g' "$file"
        
        # Update references to shared test projects
        sed -i 's|DotCompute.SharedTestUtilities|DotCompute.Tests.Common|g' "$file"
        sed -i 's|DotCompute.TestDoubles|DotCompute.Tests.Mocks|g' "$file"
        sed -i 's|DotCompute.TestImplementations|DotCompute.Tests.Implementations|g' "$file"
        
        echo "Updated: $file"
    fi
}

# Update all Unit test projects
for proj in tests/Unit/*/*.csproj; do
    update_project_refs "$proj"
done

# Update Integration test projects
for proj in tests/Integration/*/*.csproj; do
    update_project_refs "$proj"
done

# Update Hardware test projects
for proj in tests/Hardware/*/*.csproj; do
    update_project_refs "$proj"
done

# Update shared projects that might reference each other
update_project_refs "tests/Shared/DotCompute.Tests.Implementations/DotCompute.Tests.Implementations.csproj"

echo "Project reference updates complete!"

# Now update namespaces in C# files
echo "Updating namespaces in source files..."

# Update namespaces in all C# files
find tests -name "*.cs" -type f | while read -r file; do
    # Update namespace declarations
    sed -i 's/namespace DotCompute\.SharedTestUtilities/namespace DotCompute.Tests.Common/g' "$file"
    sed -i 's/namespace DotCompute\.TestDoubles/namespace DotCompute.Tests.Mocks/g' "$file"
    sed -i 's/namespace DotCompute\.TestImplementations/namespace DotCompute.Tests.Implementations/g' "$file"
    sed -i 's/namespace DotCompute\.Hardware\.RealTests/namespace DotCompute.Hardware.Cuda.Tests/g' "$file"
    
    # Update using statements
    sed -i 's/using DotCompute\.SharedTestUtilities/using DotCompute.Tests.Common/g' "$file"
    sed -i 's/using DotCompute\.TestDoubles/using DotCompute.Tests.Mocks/g' "$file"
    sed -i 's/using DotCompute\.TestImplementations/using DotCompute.Tests.Implementations/g' "$file"
    sed -i 's/using DotCompute\.Hardware\.RealTests/using DotCompute.Hardware.Cuda.Tests/g' "$file"
done

echo "Namespace updates complete!"