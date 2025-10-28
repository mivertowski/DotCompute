#!/bin/bash
# Fix CS8632: Enable nullable reference types

set -e

echo "=========================================="
echo "Fixing CS8632: Nullable Reference Types"
echo "=========================================="

# Count files needing fix
TARGET_DIR="src/Extensions/DotCompute.Algorithms"
echo "Target directory: $TARGET_DIR"

FILES_NEEDING_FIX=$(grep -r "CS8632" /tmp/*.log 2>/dev/null | grep -o "$TARGET_DIR[^:]*" | sort -u | wc -l)
echo "Files needing fix: $FILES_NEEDING_FIX"

# Backup before changes
BACKUP_DIR="backups/nullable-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"
echo "Creating backup in: $BACKUP_DIR"
cp -r "$TARGET_DIR" "$BACKUP_DIR/"

# Add #nullable enable to files missing it
echo "Adding #nullable enable directives..."

find "$TARGET_DIR" -name "*.cs" -type f | while read -r file; do
    # Check if file already has #nullable enable
    if ! grep -q "^#nullable enable" "$file"; then
        # Add at the top after usings or namespace
        sed -i '1i#nullable enable\n' "$file"
        echo "  ✓ $file"
    fi
done

echo ""
echo "=========================================="
echo "Fix Complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. Build the project: dotnet build $TARGET_DIR"
echo "2. Check errors reduced: dotnet build 2>&1 | grep CS8632 | wc -l"
echo "3. Run tests: dotnet test"
echo ""
echo "If something went wrong, restore from: $BACKUP_DIR"
