# Package Management Fixes Summary

## Issues Addressed

1. **Security Vulnerability**: Updated SixLabors.ImageSharp from 3.1.6 to 3.1.7
2. **Duplicate Package References**: Removed duplicate test package references from all test projects
3. **Microsoft.CodeAnalysis Version Conflicts**: Added explicit version overrides for transitive dependencies

## Changes Made

### 1. Updated ImageSharp Version (Security Fix)
- **File**: `Directory.Packages.props`
- **Change**: Updated SixLabors.ImageSharp from version 3.1.6 to 3.1.7
- **Reason**: Security vulnerability in version 3.1.6

### 2. Centralized Test Package Management
- **Created**: `tests/Directory.Build.targets`
- **Purpose**: Centralized all common test packages in one location
- **Packages included**:
  - Microsoft.NET.Test.Sdk
  - xunit
  - xunit.runner.visualstudio
  - coverlet.collector
  - FluentAssertions
  - NSubstitute
  - FluentAssertions.Analyzers

### 3. Removed Duplicate Package References
- **Modified**: All test project files (*.Tests.csproj)
- **Change**: Removed explicit references to common test packages
- **Projects updated**:
  - DotCompute.Backends.CUDA.Tests
  - DotCompute.Backends.Metal.Tests
  - DotCompute.Generators.Tests
  - DotCompute.Integration.Tests
  - DotCompute.Runtime.Tests
  - DotCompute.Abstractions.Tests
  - DotCompute.Plugins.Tests

### 4. Fixed Source Build Configuration Conflict
- **File**: `src/Directory.Build.props`
- **Change**: Removed duplicate test package definitions that were conflicting with the test directory configuration

### 5. Resolved CodeAnalysis Version Conflicts
- **File**: `Directory.Packages.props`
- **Added**: Explicit version overrides for Microsoft.CodeAnalysis dependencies
  - Microsoft.CodeAnalysis.Common: 4.9.2
  - Microsoft.CodeAnalysis.Workspaces.Common: 4.9.2
  - Microsoft.CodeAnalysis.CSharp.Workspaces: 4.9.2

## Benefits

1. **Security**: Resolved security vulnerability in ImageSharp
2. **Maintainability**: Centralized package management reduces duplication and makes updates easier
3. **Consistency**: All test projects now use the same versions of test packages
4. **Build Health**: Eliminated package restore warnings and version conflicts

## Verification

Run the following commands to verify the fixes:
```bash
# Restore packages
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

All duplicate package warnings should be resolved, and the ImageSharp security vulnerability is addressed.