# DotCompute Compilation Error Fix Action Plan

## SYSTEMATIC APPROACH TO RESOLVE 714 ERRORS

**Priority**: CRITICAL
**Estimated Time**: 2-4 hours
**Approach**: Dependency-ordered fixes to prevent cascading failures

---

## PHASE 1: IMMEDIATE CLEANUP (Priority 1)
*Remove duplicate type definitions causing conflicts*

### Step 1.1: Delete Duplicate ComputeBackendType
```bash
# DELETE this file completely - it's a duplicate of the Abstractions version
rm src/Core/DotCompute.Core/Compute/Enums/ComputeBackendType.cs
```

### Step 1.2: Check for Other Duplicates
```bash
# Verify no other enum duplicates exist
find src/Core/DotCompute.Core -name "*.cs" -path "*/Enums/*" | xargs grep -l "enum.*Type"
```

### Step 1.3: Remove Duplicate ValidationWarning (if exists)
```bash
# Check for ValidationWarning conflicts
grep -r "class ValidationWarning\|enum ValidationWarning" src/Core/DotCompute.Core
# Delete any duplicates found in Core (keep only Abstractions version)
```

**Expected Result**: ~50-100 errors resolved

---

## PHASE 2: FIX NAMESPACE IMPORTS (Priority 2)
*Update all using statements to reference Abstractions*

### Step 2.1: Fix Core Compute Files
**Files to Update**:
- `src/Core/DotCompute.Core/Compute/DefaultComputeEngine.cs`
- `src/Core/DotCompute.Core/Compute/*.cs`

**Changes Required**:
```csharp
// REMOVE:
using DotCompute.Core.Compute.Enums;

// ADD:
using DotCompute.Abstractions.Compute.Enums;
```

### Step 2.2: Fix Execution Files
**Files to Update**:
- `src/Core/DotCompute.Core/Execution/ParallelExecutionStrategy.cs`
- `src/Core/DotCompute.Core/Execution/PerformanceMonitor.cs`
- `src/Core/DotCompute.Core/Execution/WorkStealingCoordinator.cs`

**Changes Required**:
```csharp
// ADD missing imports:
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Models.Device;
using DotCompute.Abstractions.Interfaces.Recovery;
```

### Step 2.3: Fix Pipeline Files
**Files to Update**:
- `src/Core/DotCompute.Core/Pipelines/*.cs`
- `src/Core/DotCompute.Core/Pipelines/Examples/*.cs`
- `src/Core/DotCompute.Core/Pipelines/Exceptions/*.cs`

**Changes Required**:
```csharp
// ADD missing imports:
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Pipelines;
```

### Step 2.4: Fix Recovery Files
**Files to Update**:
- `src/Core/DotCompute.Core/Recovery/*.cs`

**Changes Required**:
```csharp
// ADD missing imports:
using DotCompute.Abstractions.Interfaces.Recovery;
```

**Expected Result**: ~400-500 errors resolved

---

## PHASE 3: INTERFACE IMPLEMENTATION FIXES (Priority 3)
*Ensure interface implementations use correct types*

### Step 3.1: Fix DefaultComputeEngine Interface
**File**: `src/Core/DotCompute.Core/Compute/DefaultComputeEngine.cs`

**Required Changes**:
1. Ensure `AvailableBackends` returns `DotCompute.Abstractions.Compute.Enums.ComputeBackendType[]`
2. Ensure `DefaultBackend` returns `DotCompute.Abstractions.Compute.Enums.ComputeBackendType`
3. Update `ExecuteAsync` method signature to use correct ComputeBackendType

### Step 3.2: Fix Type Conflicts in Core Files
**Pattern to Fix**:
```csharp
// WRONG:
ComputeDeviceType.GPU

// CORRECT (choose appropriate based on context):
ComputeBackendType.CUDA  // For backend selection
// OR
ComputeDeviceType.GPU    // For device characteristics
```

**Expected Result**: ~100-150 errors resolved

---

## PHASE 4: VERIFICATION AND VALIDATION (Priority 4)
*Ensure all types exist and are properly referenced*

### Step 4.1: Verify Missing Types
Check that these types exist in Abstractions:
- `KernelArgument` → Should be `KernelArguments` or similar
- `KernelChainExecutionResult`
- `KernelStepMetrics`
- `KernelChainMemoryMetrics`
- `PipelineExecutionResult`

### Step 4.2: Run Incremental Builds
```bash
# After each phase, check error count
dotnet build DotCompute.sln --configuration Release 2>&1 | grep "error CS" | wc -l
```

### Step 4.3: Address Remaining Missing Types
If any types are genuinely missing, create them in appropriate Abstractions namespaces.

**Expected Result**: 0 errors remaining

---

## AUTOMATED FIX COMMANDS

### Batch Using Statement Replacement
```bash
# Replace Core.Compute.Enums with Abstractions.Compute.Enums
find src/Core/DotCompute.Core -name "*.cs" -exec sed -i 's/using DotCompute\.Core\.Compute\.Enums;/using DotCompute.Abstractions.Compute.Enums;/g' {} \;

# Add missing Abstractions imports to files that need them
grep -r "IKernelManager\|RecoveryResult\|PipelineExecutionContext" src/Core/DotCompute.Core --include="*.cs" -l | \
xargs -I {} sed -i '1a using DotCompute.Abstractions.Interfaces.Kernels;\nusing DotCompute.Abstractions.Interfaces.Recovery;\nusing DotCompute.Abstractions.Interfaces.Pipelines;' {}
```

---

## SUCCESS CRITERIA

### Phase 1 Success:
- Duplicate ComputeBackendType deleted
- Error count reduced by 50-100

### Phase 2 Success:
- All Core files import from Abstractions
- Error count reduced by 400-500

### Phase 3 Success:
- Interface implementations compile correctly
- Error count reduced by 100-150

### Phase 4 Success:
- **ZERO compilation errors**
- Full solution builds successfully
- All unit tests pass (if applicable)

---

## ROLLBACK PLAN

If any phase causes new errors:
1. **Git checkout** to revert changes
2. **Analyze** the specific files causing new errors
3. **Fix individually** rather than batch operations
4. **Test incrementally** before proceeding

---

## FILES REQUIRING MODIFICATION (Summary)

### DELETE:
- `src/Core/DotCompute.Core/Compute/Enums/ComputeBackendType.cs`

### MODIFY (Using statements):
- `src/Core/DotCompute.Core/Compute/DefaultComputeEngine.cs`
- `src/Core/DotCompute.Core/Execution/ParallelExecutionStrategy.cs`
- `src/Core/DotCompute.Core/Execution/PerformanceMonitor.cs`
- `src/Core/DotCompute.Core/Execution/WorkStealingCoordinator.cs`
- `src/Core/DotCompute.Core/Pipelines/*.cs` (all files)
- `src/Core/DotCompute.Core/Recovery/*.cs` (all files)

### CREATE (if missing):
- Potentially some pipeline model types in Abstractions

**Total Estimated Files**: 50-100 files
**Total Estimated Changes**: 200-300 import statements + 1-5 deletions