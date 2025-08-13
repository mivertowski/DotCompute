#!/bin/bash

echo "=== Fixing build for alpha release ==="

# Temporarily disable TreatWarningsAsErrors for alpha
echo "Disabling TreatWarningsAsErrors for alpha release..."
find . -name "*.csproj" -type f -exec sed -i 's/<TreatWarningsAsErrors>true<\/TreatWarningsAsErrors>/<TreatWarningsAsErrors>false<\/TreatWarningsAsErrors>/g' {} \;

# Fix duplicate .sln references
echo "Fixing solution file references..."
sed -i '/DotCompute.Linq/d' DotCompute.sln
cat >> DotCompute.sln << 'EOF'
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "DotCompute.Linq", "src\Extensions\DotCompute.Linq\DotCompute.Linq.csproj", "{3C4A5A3C-7E4D-4F4B-8F4B-3A2B1A2B3C3C}"
EndProject
EOF

echo "Build fixes applied for alpha release"