# Build Error Fix Automation Scripts

This directory contains scripts to automatically fix common build errors in DotCompute.

## ⚠️ Important: Backup First!

All scripts create automatic backups, but it's recommended to commit your work first:

```bash
git add -A
git commit -m "checkpoint before automated fixes"
```

## 🚀 Quick Start

Run all automated fixes in sequence:

```bash
cd scripts/fix-automation

# Make scripts executable
chmod +x *.sh

# Run in order
./01-fix-nullable-contexts.sh
./02-fix-culture-formatting.sh
./03-fix-secure-random.sh

# Generate reports for manual fixes
./04-generate-struct-equality.sh
./05-add-exception-constructors.sh
```

## 📋 Script Overview

### 1. Fix Nullable Contexts (CS8632)
**Impact**: Fixes ~554 compilation warnings
**Time**: 2 minutes
**Safety**: High - adds directives only

```bash
./01-fix-nullable-contexts.sh
```

Adds `#nullable enable` to files in the Algorithms project.

### 2. Fix Culture Formatting (CA1305)
**Impact**: Fixes ~1,057 quality warnings
**Time**: 5 minutes
**Safety**: Medium - changes method calls

```bash
./02-fix-culture-formatting.sh
```

Adds `CultureInfo.InvariantCulture` to:
- `.ToString()` calls
- `string.Format()` calls
- `.Parse()` methods

### 3. Fix Secure Random (CA5392)
**Impact**: Fixes ~868 security warnings
**Time**: 3 minutes
**Safety**: Medium - changes API usage

```bash
./03-fix-secure-random.sh
```

Replaces `new Random()` with `RandomNumberGenerator`.

### 4. Generate Struct Equality Report (CA1815)
**Impact**: Identifies ~1,686 structs needing fixes
**Time**: 1 minute
**Safety**: High - report only, no changes

```bash
./04-generate-struct-equality.sh
```

Creates `docs/struct-equality-todo.md` with all structs needing equality.

### 5. Generate Exception Constructor Report (CA1032)
**Impact**: Identifies ~491 exceptions needing fixes
**Time**: 1 minute
**Safety**: High - report only, no changes

```bash
./05-add-exception-constructors.sh
```

Creates `docs/exception-constructors-todo.md` with exceptions needing standard constructors.

## 🔍 Verification

After running scripts, verify the fixes:

```bash
# Build and count remaining errors
dotnet build DotCompute.sln 2>&1 | tee build-after-fixes.log

# Count errors by type
grep -oP 'error (CS|CA|IDE)\d+' build-after-fixes.log | sort | uniq -c | sort -rn

# Compare before/after
echo "Before: $(cat /tmp/*.log 2>/dev/null | grep error | wc -l) errors"
echo "After:  $(cat build-after-fixes.log | grep error | wc -l) errors"
```

## 🔄 Rollback

If something goes wrong, restore from backups:

```bash
# List available backups
ls -la backups/

# Restore specific backup
cp -r backups/nullable-20250122-143022/DotCompute.Algorithms/* \
      src/Extensions/DotCompute.Algorithms/
```

Or use git:

```bash
git checkout -- src/
```

## 📊 Expected Results

| Script | Errors Fixed | Time | Risk |
|--------|--------------|------|------|
| 01-nullable | ~554 | 2 min | Low |
| 02-culture | ~1,057 | 5 min | Medium |
| 03-random | ~868 | 3 min | Medium |
| 04-struct-eq | 0 (report) | 1 min | None |
| 05-exception | 0 (report) | 1 min | None |
| **Total** | **~2,479** | **12 min** | - |

## 🐛 Troubleshooting

### Script fails with "Permission denied"
```bash
chmod +x *.sh
```

### Build still fails after fixes
1. Check the backup: `ls -la backups/`
2. Review changes: `git diff src/`
3. Look at specific errors: `dotnet build 2>&1 | grep error | head -20`

### Too many changes to review
```bash
# Review by file
git diff src/ --name-only

# Review specific patterns
git diff src/ | grep "CultureInfo"
git diff src/ | grep "RandomNumberGenerator"
```

## 🎯 Next Steps

After running automated scripts:

1. **Build and test**:
   ```bash
   dotnet build DotCompute.sln
   dotnet test DotCompute.sln
   ```

2. **Commit changes**:
   ```bash
   git add -A
   git commit -m "fix: apply automated fixes for CA1305, CA5392, CS8632

   - Added nullable contexts to Algorithms project
   - Applied culture-aware formatting (1,057 fixes)
   - Replaced insecure Random with RandomNumberGenerator (868 fixes)
   - Total automated fixes: ~2,479

   Generated reports for manual fixes:
   - docs/struct-equality-todo.md (1,686 structs)
   - docs/exception-constructors-todo.md (491 exceptions)"
   ```

3. **Review reports**:
   - `docs/struct-equality-todo.md`
   - `docs/exception-constructors-todo.md`

4. **Fix remaining CS errors** (see [quick-fix-guide.md](../../docs/quick-fix-guide.md))

## 📚 Additional Resources

- [Build Error Analysis](../../docs/build-error-analysis.md) - Complete error breakdown
- [Quick Fix Guide](../../docs/quick-fix-guide.md) - Manual fix instructions
- [Microsoft CA Rules](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/)

## 🤝 Contributing

To add new automated fixes:

1. Create `XX-fix-description.sh`
2. Follow the template from existing scripts
3. Always create backups
4. Test on a small subset first
5. Document in this README

## ⚙️ Script Template

```bash
#!/bin/bash
# Fix CAXXXX: Description

set -e

echo "=========================================="
echo "Fixing CAXXXX: Description"
echo "=========================================="

TARGET_DIR="src"
BACKUP_DIR="backups/fix-name-$(date +%Y%m%d-%H%M%S)"

# Backup
mkdir -p "$BACKUP_DIR"
echo "Creating backup in: $BACKUP_DIR"
cp -r "$TARGET_DIR" "$BACKUP_DIR/"

# Your fix logic here
echo "Applying fixes..."

# Verification
echo ""
echo "=========================================="
echo "Fix Complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. Build: dotnet build"
echo "2. Check: dotnet build 2>&1 | grep CAXXXX"
echo "3. Test: dotnet test"
```

---

*Last Updated: 2025-01-22*
