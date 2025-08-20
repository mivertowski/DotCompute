# DotCompute Solution Reorganization Guide

## Overview

This guide describes the reorganization of the DotCompute solution structure according to .NET best practices to improve maintainability, discoverability, and build performance.

## Current Issues

The existing solution structure has several issues:
- Projects scattered across various folders (`src/`, `plugins/backends/`, mixed locations)
- Inconsistent naming conventions
- Mixed test and source projects in some areas
- Complex relative path references
- Poor logical grouping of related projects

## New Structure

The reorganized solution follows .NET community best practices:

```
DotCompute/
├── src/
│   ├── Core/                           # Core abstractions and implementations
│   │   ├── DotCompute.Abstractions/    # Abstract interfaces and base types
│   │   ├── DotCompute.Core/            # Core compute engine implementation
│   │   └── DotCompute.Memory/          # Memory management implementations
│   ├── Backends/                       # Hardware-specific backend implementations
│   │   ├── DotCompute.Backends.CPU/    # CPU backend implementation
│   │   ├── DotCompute.Backends.CUDA/   # NVIDIA CUDA backend
│   │   └── DotCompute.Backends.Metal/  # Apple Metal backend
│   ├── Extensions/                     # High-level extensions and algorithms
│   │   ├── DotCompute.Linq/            # LINQ to GPU implementation
│   │   └── DotCompute.Algorithms/      # Mathematical algorithms library
│   └── Runtime/                        # Runtime services and infrastructure
│       ├── DotCompute.Runtime/         # Runtime and dependency injection
│       ├── DotCompute.Plugins/         # Plugin system and loading
│       └── DotCompute.Generators/      # Source generators and analyzers
├── tests/
│   ├── Unit/                          # Unit tests for all projects
│   │   ├── DotCompute.Core.Tests/
│   │   ├── DotCompute.Abstractions.Tests/
│   │   ├── DotCompute.Memory.Tests/
│   │   ├── DotCompute.Linq.Tests/
│   │   ├── DotCompute.Algorithms.Tests/
│   │   ├── DotCompute.Backends.CPU.Tests/
│   │   ├── DotCompute.Backends.CUDA.Tests/
│   │   ├── DotCompute.Backends.Metal.Tests/
│   │   ├── DotCompute.Runtime.Tests/
│   │   ├── DotCompute.Plugins.Tests/
│   │   └── DotCompute.Generators.Tests/
│   ├── Integration/                   # Integration tests
│   │   └── DotCompute.Integration.Tests/
│   ├── Performance/                   # Performance and hardware tests
│   │   ├── DotCompute.Performance.Tests/
│   │   ├── DotCompute.Hardware.Cuda.Tests/
│   │   ├── DotCompute.Hardware.OpenCL.Tests/
│   │   ├── DotCompute.Hardware.DirectCompute.Tests/
│   │   ├── DotCompute.Hardware.Mock.Tests/
│   │   └── DotCompute.Hardware.RTX2000.Tests/
│   └── Shared/                        # Shared test utilities
│       ├── DotCompute.Tests.Common/
│       ├── DotCompute.Tests.Mocks/
│       ├── DotCompute.Tests.Implementations/
│       └── DotCompute.SharedTestUtilities/
├── samples/                           # Example applications
├── docs/                              # Documentation
├── benchmarks/                        # Performance benchmarks
└── build/                             # Build artifacts
```

## Benefits of New Structure

### 1. Logical Grouping
- **Core**: Fundamental abstractions and implementations
- **Backends**: Hardware-specific implementations
- **Extensions**: High-level features and algorithms
- **Runtime**: Infrastructure and services

### 2. Simplified Dependencies
- Core projects have minimal dependencies
- Backends depend only on Core
- Extensions depend on Core and potentially Backends
- Runtime provides infrastructure for all

### 3. Clear Test Organization
- Unit tests mirror source structure
- Integration tests are separate
- Performance tests are grouped together
- Shared utilities are centralized

### 4. Better Discoverability
- Developers can easily find related projects
- Clear separation of concerns
- Consistent naming conventions

## Migration Process

### Prerequisites
- Clean working directory (or use `--force`)
- .NET SDK installed
- Git repository

### Running the Reorganization

#### Option 1: Bash Script (Recommended for Linux/macOS)
```bash
# Dry run to see what would change
./scripts/reorganize-solution.sh --dry-run

# Perform the reorganization
./scripts/reorganize-solution.sh

# With custom backup location
./scripts/reorganize-solution.sh --backup-path ./my-backup
```

#### Option 2: PowerShell Script (Windows/Cross-platform)
```powershell
# Dry run to see what would change
./scripts/reorganize-solution.ps1 -DryRun

# Perform the reorganization
./scripts/reorganize-solution.ps1

# With custom backup location
./scripts/reorganize-solution.ps1 -BackupPath "./my-backup"
```

### Script Features

1. **Backup Creation**: Automatic backup before any changes
2. **Dry Run Mode**: Preview changes without applying them
3. **Project Moving**: Safely move projects to new locations
4. **Reference Updates**: Update all project references automatically
5. **Solution File Updates**: Update solution file structure
6. **Build Verification**: Test build after reorganization
7. **Cleanup**: Remove empty directories

## Project Reference Updates

The reorganization automatically updates project references:

### Before
```xml
<!-- From a backend project -->
<ProjectReference Include="..\..\..\src\DotCompute.Abstractions\DotCompute.Abstractions.csproj" />

<!-- From a test project -->
<ProjectReference Include="..\..\src\DotCompute.Core\DotCompute.Core.csproj" />
```

### After
```xml
<!-- From a backend project -->
<ProjectReference Include="..\..\Core\DotCompute.Abstractions\DotCompute.Abstractions.csproj" />

<!-- From a test project -->
<ProjectReference Include="..\..\src\Core\DotCompute.Core\DotCompute.Core.csproj" />
```

## Dependency Flow

The new structure enforces a clean dependency flow:

```
┌─────────────────┐
│   Extensions    │ ← High-level features
│  (Linq, Algos) │
└─────────────────┘
         ↑
┌─────────────────┐
│    Backends     │ ← Hardware implementations
│ (CPU, CUDA, etc)│
└─────────────────┘
         ↑
┌─────────────────┐
│      Core       │ ← Foundation
│ (Abstractions)  │
└─────────────────┘
         ↑
┌─────────────────┐
│    Runtime      │ ← Infrastructure
│ (DI, Plugins)   │
└─────────────────┘
```

## Build Configuration Updates

### Directory.Build.props
The reorganization maintains existing `Directory.Build.props` files:
- `src/Directory.Build.props` - Common properties for source projects
- `tests/Directory.Build.props` - Common properties for test projects

### Global Configuration
- `Directory.Build.props` (root) - Global settings
- `Directory.Build.targets` (root) - Global targets
- `global.json` - SDK version

## CI/CD Updates Required

After reorganization, update:

1. **Build Scripts**: Update paths in build scripts
2. **Test Discovery**: Update test project paths
3. **Packaging**: Update NuGet package source paths
4. **Coverage**: Update code coverage configurations

### Example Updates
```yaml
# Before
- src/DotCompute.Core/**/*.cs
- plugins/backends/DotCompute.Backends.CPU/**/*.cs

# After
- src/Core/DotCompute.Core/**/*.cs
- src/Backends/DotCompute.Backends.CPU/**/*.cs
```

## Validation Steps

After reorganization:

1. **Build Verification**
   ```bash
   dotnet build
   ```

2. **Test Execution**
   ```bash
   dotnet test
   ```

3. **Package Restore**
   ```bash
   dotnet restore
   ```

4. **Reference Validation**
   ```bash
   # Check for any broken references
   find . -name "*.csproj" -exec grep -l "ProjectReference" {} \; | xargs grep "ProjectReference"
   ```

## Rollback Procedure

If issues occur:

1. **Stop any builds/tests**
2. **Restore from backup**:
   ```bash
   # The script creates timestamped backups
   cp -r backup/dotcompute_backup_YYYYMMDD_HHMMSS/* .
   ```
3. **Verify restoration**:
   ```bash
   dotnet build
   ```

## Post-Reorganization Tasks

1. **Update Documentation**
   - Update README files with new paths
   - Update architecture documentation
   - Update getting started guides

2. **IDE Configuration**
   - Update solution filters
   - Update project favorites
   - Update launch configurations

3. **Team Communication**
   - Notify team of structure changes
   - Update development guidelines
   - Update onboarding documentation

## Troubleshooting

### Common Issues

1. **Build Failures**
   - Check project references are updated correctly
   - Verify all projects moved successfully
   - Check for case sensitivity issues

2. **Missing Files**
   - Ensure all project files were moved
   - Check for hidden files (`.vs/`, `.vscode/`)
   - Verify backup integrity

3. **Reference Errors**
   - Run script again with updated mappings
   - Manually fix remaining references
   - Check for circular dependencies

### Manual Fixes

If automatic reference updates miss something:

```bash
# Find all project references
find . -name "*.csproj" -exec grep -l "ProjectReference" {} \;

# Find specific patterns
grep -r "\.\.\\\.\.\\\.\.\\src\\" . --include="*.csproj"
```

## Benefits Realized

After reorganization:

1. **Faster Navigation**: Developers can quickly locate related projects
2. **Cleaner Dependencies**: Clear dependency hierarchy
3. **Better IntelliSense**: IDEs can better understand project relationships
4. **Simplified Build**: Reduced complexity in build scripts
5. **Easier Onboarding**: New developers understand structure immediately
6. **Consistent Patterns**: Follows .NET community conventions

## Conclusion

The solution reorganization provides a solid foundation for:
- Future growth and feature additions
- Better team collaboration
- Improved build performance
- Easier maintenance and debugging
- Industry-standard project organization

The automated scripts ensure the migration is safe, reversible, and verifiable.