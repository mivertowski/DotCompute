#!/bin/bash

# Temporarily disable documentation generation to get a clean build
echo "Disabling GenerateDocumentationFile temporarily..."

find . -name "*.csproj" -type f 2>/dev/null | grep -v "bin\|obj\|artifacts\|.git" | while read -r file; do
    if grep -q "<GenerateDocumentationFile>true</GenerateDocumentationFile>" "$file"; then
        sed -i '/<GenerateDocumentationFile>true<\/GenerateDocumentationFile>/d' "$file"
        echo "Disabled in: $file"
    fi
done

echo "Documentation generation disabled."