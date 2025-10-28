#!/bin/bash
# Fix CA5392: Use secure random number generator

set -e

echo "=========================================="
echo "Fixing CA5392: Secure Random"
echo "=========================================="

TARGET_DIR="src"
BACKUP_DIR="backups/random-$(date +%Y%m%d-%H%M%S)"

# Backup
mkdir -p "$BACKUP_DIR"
echo "Creating backup in: $BACKUP_DIR"
cp -r "$TARGET_DIR" "$BACKUP_DIR/"

echo "Replacing insecure Random usage..."

find "$TARGET_DIR" -name "*.cs" -type f | while read -r file; do
    # Check if file uses Random
    if grep -q "new Random()" "$file"; then
        echo "  Processing: $file"

        # Replace Random instantiation
        sed -i 's/new Random()/RandomNumberGenerator.Create()/g' "$file"

        # Replace common Random methods
        # random.Next(min, max) -> RandomNumberGenerator.GetInt32(min, max)
        perl -i -pe 's/(\w+)\.Next\(([^)]+)\)/RandomNumberGenerator.GetInt32($2)/g' "$file"

        # random.NextBytes(buffer) -> RandomNumberGenerator.Fill(buffer)
        perl -i -pe 's/(\w+)\.NextBytes\(([^)]+)\)/RandomNumberGenerator.Fill($2)/g' "$file"

        # Add using if needed
        if ! grep -q "using System.Security.Cryptography" "$file"; then
            sed -i '1i using System.Security.Cryptography;' "$file"
        fi

        echo "    ✓ Fixed"
    fi
done

echo ""
echo "=========================================="
echo "Fix Complete!"
echo "=========================================="
echo ""
echo "Note: Some Random usages may need manual review:"
echo "- NextDouble() has no direct replacement"
echo "- Complex random logic may need refactoring"
echo ""
echo "Next steps:"
echo "1. Build: dotnet build"
echo "2. Check: dotnet build 2>&1 | grep CA5392 | wc -l"
echo "3. Review: git diff $TARGET_DIR"
echo "4. Test: dotnet test"
