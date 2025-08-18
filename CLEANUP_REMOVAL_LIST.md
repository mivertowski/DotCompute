# DotCompute Repository Cleanup - Files for Removal

## Executive Summary
This document provides a comprehensive list of obsolete and unused files to be removed from the DotCompute repository to improve maintainability and reduce clutter.

## Categories of Files to Remove

### 1. Build Artifacts and Temporary Files

#### Build Directories (NOT tracked in Git)
- `artifacts/` (8,189 files) - Build output, not in Git
- `TestResults/` (21 files) - Test output, not in Git  
- `.hive-mind/` (40 files) - AI agent temporary files, not in Git

#### Log Files and Build Outputs
- `test_output.log` - Old test output logs
- `build_output.txt` - Build logs
- `build_output.log` - Duplicate build logs
- `buildlog.binlog` - Binary build logs
- `artifacts/bin/DotCompute.Performance.Tests/Debug/net9.0/BenchmarkDotNet.Artifacts/*.log` - 10+ benchmark logs

#### Location: `/src/Runtime/DotCompute.Runtime/Services/`
- `build_errors.log`
- `full_build.log`  
- `build_output.log`

### 2. Obsolete Code Files (Already Staged for Deletion)

These files are marked as deleted in git status and need to be committed:

#### Core Kernel Files (All in `/src/Core/DotCompute.Core/Kernels/`)
- `CUDAInterop.cs` - Moved to CUDA backend
- `CUDAKernelCompiler.cs` - Moved to CUDA backend
- `CUDAKernelExecutor.cs` - Moved to CUDA backend  
- `CUDAKernelGenerator.cs` - Moved to CUDA backend
- `DirectComputeInterop.cs` - DirectCompute backend removed
- `DirectComputeKernelCompiler.cs` - DirectCompute backend removed
- `DirectComputeKernelExecutor.cs` - DirectCompute backend removed
- `MetalKernelCompiler.cs` - Moved to Metal backend
- `MetalKernelExecutor.cs` - Moved to Metal backend
- `OpenCLInterop.cs` - OpenCL backend removed/moved
- `OpenCLKernelCompiler.cs` - OpenCL backend removed/moved
- `OpenCLKernelExecutor.cs` - OpenCL backend removed/moved

**Justification**: These files were part of a major refactoring to move backend-specific code from Core to respective backend projects. They contain obsolete APIs and duplicate functionality.

### 3. Test Files for Removed Components

#### DirectCompute Tests
- `tests/Hardware/DotCompute.Hardware.DirectCompute.Tests/DirectComputeHardwareTests.cs`
- `tests/Hardware/DotCompute.Hardware.Mock.Tests/DirectComputeSimulationTests.cs`

**Justification**: DirectCompute backend was removed as it's deprecated by Microsoft and superseded by Direct3D 12.

#### OpenCL Mock Tests  
- `tests/Hardware/DotCompute.Hardware.Mock.Tests/OpenCLHardwareTests.cs`
- `tests/Hardware/DotCompute.Hardware.Mock.Tests/OpenCLSimulationTests.cs`
- `tests/Hardware/DotCompute.Hardware.OpenCL.Tests/OpenCLHardwareTests.cs`

**Justification**: OpenCL support was reduced in favor of CUDA and Metal for better performance and maintainability.

### 4. Obsolete Documentation Files

#### Root Directory Cleanup (10+ files)
- `ATTENTION-POINTS-IMPLEMENTATION.md` - Temporary tracking document
- `BUILD_STATUS.md` - Temporary build status (not in git)
- `CI-CD-OPTIMIZATION-SUMMARY.md` - One-time implementation summary
- `COMPLETION_REPORT.md` - Project completion summary (not in git)
- `DEPLOYMENT-SUMMARY.md` - One-time deployment summary
- `DOTCOMPUTE-CHEATSHEET.md` - Outdated cheat sheet (references removed components)
- `FINAL_REPORT.md` - Project completion report (not in git)
- `PHASE4_COMPLETION_SUMMARY.md` - Phase-specific completion report
- `TEST_COVERAGE_PHASE4_REPORT.md` - Phase-specific test report

#### Outdated Documentation in `/docs/` (50+ files)
**Phase-specific reports** (should be in docs/phase-reports/):
- `docs/completion-reports/stub-replacement-completion-report.md`
- `docs/build-error-fixes-summary.md`
- `docs/ca1848-fix-summary.md`
- `docs/cuda-pinvoke-fixes-summary.md`
- `docs/improvements-summary.md`
- `docs/improvements-summary-final.md`
- `docs/pragma-warning-fixes-summary.md`
- `docs/solution-cleanup-summary.md`

**Obsolete Implementation References**:
- `docs/CUDA_Backend_Class_Designs.md` - Design docs for completed implementation
- `docs/CUDA_Backend_Implementation_Plan.md` - Implementation plan (completed)
- `docs/CUDA_Backend_Implementation_Summary.md` - Summary of completed work
- `docs/CUDA_PInvoke_Specifications.md` - Technical specs (now in code comments)

**Justification**: These are temporary tracking documents, phase-specific reports, or implementation planning documents that are no longer needed after completion.

### 5. Code Files with Obsolete References

#### Files referencing removed generators:
- `src/Extensions/DotCompute.Linq/Operators/DefaultKernelFactory.cs` (lines 194, 205)
  - References `CUDAKernelGenerator` and `OpenCLKernelGenerator` classes that no longer exist
  - **Action**: Update to use proper backend factory pattern or remove references

**Justification**: These files contain references to moved/removed classes and will cause compilation errors.

### 6. Empty or Minimal Test Files

After analysis, no significantly empty test files were found. All test files contain substantial implementations.

### 7. Temporary Script Files

#### Root Directory Scripts
- `build-core.sh` - Temporary build script
- `fix-project-references.sh` - One-time fix script  
- `run-coverage.sh` - Superseded by scripts/ directory
- `measure-coverage.ps1` - Superseded by scripts/ directory
- `validate-p2p.sh` - Validation script
- `test-execution-strategies.md` - Temporary strategy document
- `cuda_validation_test.cs` - Standalone test file
- `coverage-analysis-report.md` - Analysis report
- `coverage-thresholds.json` - Configuration (should be in ci/ directory)

#### Scripts Directory Cleanup (30+ files in `/scripts/`)
**One-time fix scripts**:
- All `fix-*.sh` and `fix-*.ps1` files (20+ files)
- All `fix-*.py` files (5+ files)  
- `BulkFixScript.ps1`
- `bulk_fix.sh`

**Justification**: These are one-time fix scripts that served their purpose during development and are no longer needed.

## Files to Keep (Important Exceptions)

### Essential Documentation
- `README.md` - Main project documentation
- `CHANGELOG.md` - Version history
- `LICENSE` - Legal requirement
- `CLAUDE.md` - Project instructions (per gitignore comment)

### Active Configuration
- `.gitignore` - Repository configuration
- `.editorconfig` - Editor configuration
- `Directory.Build.props/targets` - Build configuration
- `DotCompute.sln` - Solution file
- `GitVersion.yml` - Version configuration

### Current Implementation
- All files in `src/` that are not listed above for removal
- Active test files in `tests/` that are not listed above  
- Current documentation in `docs/` that provides ongoing value

## Removal Strategy

### Phase 1: Commit Deleted Files
```bash
git add -A  # Stage all deleted files
git commit -m "Remove obsolete kernel files from Core - moved to backend projects"
```

### Phase 2: Remove Build Artifacts (Safe - Not in Git)
```bash
rm -rf artifacts/
rm -rf TestResults/
rm -rf .hive-mind/
```

### Phase 3: Remove Obsolete Documentation
```bash
# Root directory cleanup
rm ATTENTION-POINTS-IMPLEMENTATION.md
rm CI-CD-OPTIMIZATION-SUMMARY.md
rm DEPLOYMENT-SUMMARY.md
rm DOTCOMPUTE-CHEATSHEET.md
rm PHASE4_COMPLETION_SUMMARY.md
rm TEST_COVERAGE_PHASE4_REPORT.md
```

### Phase 4: Remove Obsolete Test Files
```bash
rm tests/Hardware/DotCompute.Hardware.DirectCompute.Tests/DirectComputeHardwareTests.cs
rm tests/Hardware/DotCompute.Hardware.Mock.Tests/DirectComputeSimulationTests.cs
# (Additional OpenCL test removals as listed above)
```

### Phase 5: Clean Documentation Directory
```bash
# Remove phase-specific and temporary docs (50+ files)
# (Specific commands based on files listed above)
```

### Phase 6: Remove Temporary Scripts
```bash
# Remove one-time fix scripts and temporary files
# (Specific commands based on files listed above)
```

### Phase 7: Update References
- Fix `DefaultKernelFactory.cs` to remove references to deleted generator classes
- Update any remaining references to removed components

## Expected Impact

### Repository Size Reduction
- **Build artifacts**: ~8,000+ files removed (not in git, but consuming disk space)
- **Obsolete code**: 12 C# files removed from Core
- **Documentation**: ~60+ obsolete markdown files removed
- **Scripts**: ~30+ temporary script files removed

### Maintainability Improvements
- Reduced confusion from obsolete documentation
- Eliminated broken references to moved/removed code
- Cleaner repository structure focused on current implementation
- Reduced build and test times

### Risk Assessment
- **Low risk**: Most files are already deleted in git or are temporary/obsolete
- **Medium risk**: Documentation removal (mitigated by keeping essential docs)
- **High risk**: Code file modifications (will be tested before commit)

## Conclusion

This cleanup will significantly improve the DotCompute repository maintainability by removing:
- 8,000+ temporary build artifacts
- 12+ obsolete core kernel files  
- 60+ outdated documentation files
- 30+ temporary script files

The cleanup focuses on files that are either already staged for deletion, provide no ongoing value, or reference removed components.