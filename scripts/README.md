# DotCompute Solution Reorganization Scripts

This directory contains automated scripts to reorganize the DotCompute solution structure according to .NET best practices.

## 📁 Files

| File | Purpose |
|------|---------|
| `reorganize-solution.sh` | Main reorganization script (Bash/Linux/macOS) |
| `reorganize-solution.ps1` | Main reorganization script (PowerShell/Windows) |
| `validate-reorganization.sh` | Pre-reorganization validation and analysis |
| `README.md` | This documentation file |

## 🚀 Quick Start

### 1. Validate Benefits
First, run the validation script to assess if reorganization would be beneficial:

```bash
./scripts/validate-reorganization.sh
```

### 2. Preview Changes
Run a dry-run to see what would change without making any modifications:

```bash
# Linux/macOS
./scripts/reorganize-solution.sh --dry-run --force

# Windows/PowerShell  
./scripts/reorganize-solution.ps1 -DryRun -Force
```

### 3. Execute Reorganization
If satisfied with the preview, run the actual reorganization:

```bash
# Linux/macOS
./scripts/reorganize-solution.sh --force

# Windows/PowerShell
./scripts/reorganize-solution.ps1 -Force
```

### 4. Verify Results
After reorganization, verify everything works:

```bash
dotnet build
dotnet test
```

## 📋 Script Options

### Bash Script (`reorganize-solution.sh`)
```bash
./reorganize-solution.sh [OPTIONS]

Options:
  --dry-run          Show what would be done without making changes
  --force           Continue even with uncommitted git changes
  --backup-path     Custom backup location (default: ./backup)
  -h, --help        Show help message
```

### PowerShell Script (`reorganize-solution.ps1`)
```powershell
./reorganize-solution.ps1 [PARAMETERS]

Parameters:
  -DryRun           Show what would be done without making changes
  -Force            Continue even with uncommitted git changes  
  -BackupPath       Custom backup location (default: ./backup)
```

## 🔄 What the Scripts Do

### 1. Safety Checks
- ✅ Verify solution file exists
- ✅ Check for uncommitted git changes
- ✅ Validate prerequisites (dotnet CLI, git)
- ✅ Check disk space for backup

### 2. Backup Creation
- 📦 Create timestamped backup of entire solution
- 🗂️ Exclude build artifacts and temporary files
- 🔒 Preserve original state for rollback

### 3. Structure Creation
Create new directory structure:
```
src/
├── Core/          # Core abstractions and implementations
├── Backends/      # Hardware-specific implementations  
├── Extensions/    # High-level features and algorithms
└── Runtime/       # Infrastructure and services
```

### 4. Project Movement
Move projects to their logical locations:
- Core projects → `src/Core/`
- Backend projects → `src/Backends/`
- Extension projects → `src/Extensions/`
- Runtime projects → `src/Runtime/`
- Test consolidation → `tests/Unit/`, `tests/Integration/`, etc.

### 5. Reference Updates
- 🔗 Update all `ProjectReference` paths automatically
- 📝 Maintain correct relative paths
- ✅ Preserve all dependencies

### 6. Solution Updates
- 📄 Update solution file with new project paths
- 📁 Update solution folder structure
- 🎯 Maintain build configurations

### 7. Verification
- 🏗️ Test build after reorganization
- ❌ Fail fast if build breaks
- 📊 Report success/failure

## 📊 Project Mapping

| Current Location | New Location | Category |
|------------------|--------------|----------|
| `src/DotCompute.Abstractions` | `src/Core/DotCompute.Abstractions` | Core |
| `src/DotCompute.Core` | `src/Core/DotCompute.Core` | Core |
| `src/DotCompute.Memory` | `src/Core/DotCompute.Memory` | Core |
| `plugins/backends/DotCompute.Backends.CPU` | `src/Backends/DotCompute.Backends.CPU` | Backend |
| `plugins/backends/DotCompute.Backends.CUDA` | `src/Backends/DotCompute.Backends.CUDA` | Backend |
| `plugins/backends/DotCompute.Backends.Metal` | `src/Backends/DotCompute.Backends.Metal` | Backend |
| `src/DotCompute.Linq` | `src/Extensions/DotCompute.Linq` | Extension |
| `src/DotCompute.Algorithms` | `src/Extensions/DotCompute.Algorithms` | Extension |
| `src/DotCompute.Runtime` | `src/Runtime/DotCompute.Runtime` | Runtime |
| `src/DotCompute.Plugins` | `src/Runtime/DotCompute.Plugins` | Runtime |
| `src/DotCompute.Generators` | `src/Runtime/DotCompute.Generators` | Runtime |

## 🛡️ Safety Features

### Automatic Backup
- Every run creates a timestamped backup in `backup/dotcompute_backup_YYYYMMDD_HHMMSS/`
- Backup excludes `bin/`, `obj/`, `.git/`, `TestResults/`, etc.
- Quick restoration possible if issues occur

### Dry Run Mode
- Preview all changes before applying them
- See exactly what files would be moved
- Validate reference updates
- No actual changes made

### Build Verification
- Automatic `dotnet build` after reorganization
- Fails if build breaks after changes
- Provides immediate feedback

### Rollback Support
```bash
# Restore from backup if needed
cp -r backup/dotcompute_backup_YYYYMMDD_HHMMSS/* .
dotnet build  # Verify restoration
```

## 🧪 Testing

### Validation Script
Run `validate-reorganization.sh` to:
- ✅ Analyze current structure complexity
- ✅ Assess expected benefits
- ✅ Check prerequisites
- ✅ Provide recommendations

### Manual Testing
After reorganization:
```bash
# Verify solution builds
dotnet build

# Verify all tests pass  
dotnet test

# Check for broken references
find . -name "*.csproj" -exec grep -l "ProjectReference" {} \;
```

## 🔧 Troubleshooting

### Common Issues

**Build Fails After Reorganization**
1. Check backup integrity: `ls backup/dotcompute_backup_*`
2. Restore from backup: `cp -r backup/dotcompute_backup_YYYYMMDD_HHMMSS/* .`
3. Report issue with build logs

**References Not Updated**
1. Check for missed patterns in project files
2. Run reference validation: `grep -r "ProjectReference" . --include="*.csproj"`
3. Manually fix remaining references

**Git Issues**
1. Use `--force` to bypass git checks
2. Commit or stash changes before reorganization
3. Review git status after reorganization

### Debug Mode
For detailed output, examine the scripts:
- Both scripts have verbose output for each step
- Dry-run mode shows all planned changes
- Build verification provides immediate feedback

## 📚 Additional Documentation

- [`SOLUTION_REORGANIZATION_GUIDE.md`](../docs/SOLUTION_REORGANIZATION_GUIDE.md) - Comprehensive guide
- [`REORGANIZATION_SUMMARY.md`](../docs/REORGANIZATION_SUMMARY.md) - Executive summary
- Solution README - Overall project documentation

## ✅ Success Criteria

After successful reorganization:
- ✅ All projects build successfully (`dotnet build`)
- ✅ All tests pass (`dotnet test`)
- ✅ Logical project organization matches new structure
- ✅ Project references use correct relative paths
- ✅ Solution file reflects new organization
- ✅ Empty directories are cleaned up

## 📞 Support

If you encounter issues:
1. Check the troubleshooting section above
2. Review backup and restoration procedures
3. Consult the comprehensive reorganization guide
4. Restore from backup if needed and report issues

The reorganization process is designed to be safe, reversible, and well-documented. Follow the dry-run → backup → execute → verify workflow for best results.