#!/bin/bash

# Fix CopyFromHostAsync to CopyFromAsync
echo "Fixing CopyFromHostAsync to CopyFromAsync..."
find src -type f -name "*.cs" | while read file; do
    if grep -q "CopyFromHostAsync" "$file"; then
        sed -i 's/\.CopyFromHostAsync/\.CopyFromAsync/g' "$file"
        echo "  Fixed: $file"
    fi
done

# Fix CopyToHostAsync to CopyToAsync
echo "Fixing CopyToHostAsync to CopyToAsync..."
find src -type f -name "*.cs" | while read file; do
    if grep -q "CopyToHostAsync" "$file"; then
        sed -i 's/\.CopyToHostAsync/\.CopyToAsync/g' "$file"
        echo "  Fixed: $file"
    fi
done

echo "Method name fixes complete!"