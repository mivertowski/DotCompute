#!/bin/bash
# Fix CA1305: Specify IFormatProvider

set -e

echo "=========================================="
echo "Fixing CA1305: Culture-Aware Formatting"
echo "=========================================="

TARGET_DIR="src"
BACKUP_DIR="backups/culture-$(date +%Y%m%d-%H%M%S)"

# Backup
mkdir -p "$BACKUP_DIR"
echo "Creating backup in: $BACKUP_DIR"
cp -r "$TARGET_DIR" "$BACKUP_DIR/"

echo "Applying culture-aware formatting fixes..."

# Fix .ToString() -> .ToString(CultureInfo.InvariantCulture)
# But be careful not to replace overridden ToString() or ToString() with parameters
find "$TARGET_DIR" -name "*.cs" -type f | while read -r file; do
    # Replace .ToString() that's not already followed by ( or already has CultureInfo
    perl -i -pe 's/\.ToString\(\)(?!\s*\{|\s*;|\s*,|\s*\))/\.ToString(CultureInfo.InvariantCulture)/g unless /override.*ToString|\/\/.*ToString/' "$file"

    # Fix string.Format without culture
    perl -i -pe 's/string\.Format\s*\(\s*"/string.Format(CultureInfo.InvariantCulture, "/g unless /\/\//' "$file"

    # Fix Parse methods
    perl -i -pe 's/\.Parse\s*\(\s*([^,)]+)\s*\)/.Parse($1, CultureInfo.InvariantCulture)/g unless /\/\//' "$file"

    # Add using if needed
    if grep -q "CultureInfo" "$file"; then
        if ! grep -q "using System.Globalization" "$file"; then
            # Insert after other System usings
            sed -i '/^using System/a using System.Globalization;' "$file"
        fi
    fi
done

echo ""
echo "=========================================="
echo "Fix Complete!"
echo "=========================================="
echo ""
echo "Files modified in: $TARGET_DIR"
echo "Backup available in: $BACKUP_DIR"
echo ""
echo "Next steps:"
echo "1. Build: dotnet build"
echo "2. Check: dotnet build 2>&1 | grep CA1305 | wc -l"
echo "3. Review changes: git diff $TARGET_DIR"
echo "4. Test: dotnet test"
