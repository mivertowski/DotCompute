# Build Fix Priority List

## Immediate Fixes (Unblock Most Errors)

### 1. CUDA Backend Foundation (76 errors)
**Files to fix:**
- `plugins/backends/DotCompute.Backends.CUDA/CudaContext.cs`
- `plugins/backends/DotCompute.Backends.CUDA/Memory/CudaMemoryBuffer.cs`
- `plugins/backends/DotCompute.Backends.CUDA/Memory/CudaMemoryManager.cs`

**Issues:**
- Missing using directives for Abstractions
- IMemoryBuffer interface not fully implemented
- CudaContext missing required members

**Fix approach:**
1. Add missing using statements
2. Implement all IMemoryBuffer members
3. Complete CudaContext implementation

### 2. Plugin System Interfaces (168 errors)
**Files to fix:**
- `tests/DotCompute.Plugins.Tests/AdvancedPluginTests.cs`
- `tests/DotCompute.Plugins.Tests/PluginManagerTests.cs`
- `tests/DotCompute.Plugins.Tests/PipelineIntegrationTests.cs`

**Issues:**
- IBackendPlugin interface changes not reflected in tests
- Missing abstract member implementations
- Namespace DotCompute.Plugins.Abstractions doesn't exist

**Fix approach:**
1. Update test plugin implementations to match current interfaces
2. Remove references to non-existent Abstractions namespace
3. Implement all required interface members

### 3. Generator Tests Naming (112 errors)
**Files to fix:**
- `tests/DotCompute.Generators.Tests/AdvancedGeneratorTests.cs`
- `tests/DotCompute.Generators.Tests/VectorOperationsGeneratorTests.cs`

**Issues:**
- CA1707: Underscores in identifiers
- Invalid method calls to non-existent members

**Fix approach:**
1. Remove underscores from test method names
2. Update method calls to match actual API

## Secondary Fixes

### 4. Integration Test Fixtures (40 errors)
**Files to fix:**
- `tests/DotCompute.Integration.Tests/Fixtures/SimpleIntegrationTestFixture.cs`
- `tests/DotCompute.Integration.Tests/Fixtures/IntegrationTestFixture.cs`

**Issues:**
- Missing type definitions
- Interface implementation issues

### 5. Metal Backend (18 errors)
**Files to fix:**
- `plugins/backends/DotCompute.Backends.Metal/src/MetalBackend.cs`
- `plugins/backends/DotCompute.Backends.Metal/src/Registration/MetalBackendPlugin.cs`

**Issues:**
- Incomplete implementation
- Missing required members

### 6. CPU Backend AOT (12 errors)
**Files to fix:**
- `plugins/backends/DotCompute.Backends.CPU/src/Kernels/AotCpuKernelCompiler.cs`

**Issues:**
- AOT compilation configuration issues

## Quick Wins

### Naming Violations (CA1707)
- Simple find/replace to remove underscores
- No functional changes required

### Accessibility Issues (CA1515)
- Add `internal` modifier to public types in test assemblies
- Simple modifier changes

### Missing Using Statements
- Add required using directives
- No logic changes needed

## Dependency Resolution Order

1. **Fix CUDA Native Types** → Unblocks CUDA backend
2. **Fix Plugin Interfaces** → Unblocks plugin tests
3. **Fix Test Naming** → Removes 112 errors quickly
4. **Fix Integration Tests** → Enables end-to-end testing
5. **Complete Backends** → Full functionality

## Estimated Impact

- Fixing CUDA types: Resolves ~100 errors
- Fixing plugin interfaces: Resolves ~150 errors
- Fixing naming violations: Resolves 112 errors
- **Total quick impact: ~362/418 errors (86.6%)**

## Next Steps for Other Agents

1. **CUDA Specialist**: Implement missing CUDA types and memory management
2. **Plugin Specialist**: Update all plugin test implementations
3. **Test Specialist**: Fix naming violations and test infrastructure
4. **Integration Specialist**: Update integration test fixtures