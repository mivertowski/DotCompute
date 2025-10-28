# Implementation Action Plan - Duplicate Types Consolidation

## Immediate Actions (Phase 1 - Week 1)

### 1. ExecutionPriority Consolidation - Priority: LOW, Effort: 2 days

**Canonical Location**: `/src/Core/DotCompute.Abstractions/Compute/Options/ExecutionOptions.cs`

**Files to Remove**:
```
/src/Core/DotCompute.Abstractions/Pipelines/PipelineTypes.cs (ExecutionPriority enum)
/src/Core/DotCompute.Abstractions/Pipelines/Enums/ExecutionPriority.cs
/src/Core/DotCompute.Core/Compute/Enums/ExecutionPriority.cs
```

**Migration Steps**:
1. Add using statement: `using DotCompute.Abstractions.Compute.Options;`
2. Search and replace: `Pipelines.ExecutionPriority` → `ExecutionPriority`
3. Remove duplicate enum definitions
4. Update all references to use canonical definition

### 2. MemoryAccessPattern Cleanup - Priority: HIGH, Effort: 3 days

**Canonical Location**: `/src/Core/DotCompute.Abstractions/Types/MemoryAccessPattern.cs`

**Files to Remove (Already Marked Obsolete)**:
```
/src/Core/DotCompute.Core/Optimization/Enums/MemoryAccessPattern.cs
/src/Backends/DotCompute.Backends.CUDA/Types/MemoryAccessPattern.cs
/src/Backends/DotCompute.Backends.CUDA/Analysis/Types/AnalysisMemoryAccessPattern.cs
```

**Files to Keep**:
```
/src/Runtime/DotCompute.Generators/Kernel/Enums/MemoryAccessPattern.cs (netstandard2.0 compatibility)
/src/Backends/DotCompute.Backends.CUDA/Types/CudaMemoryAccessPattern.cs (hardware-specific)
```

**Migration Steps**:
1. Update all imports to use `DotCompute.Abstractions.Types.MemoryAccessPattern`
2. Remove obsolete alias files
3. Verify build compilation
4. Update documentation

## Complex Consolidation (Phase 2 - Weeks 2-3)

### 3. OptimizationLevel Consolidation - Priority: HIGH, Effort: 5 days

**Canonical Location**: `/src/Core/DotCompute.Abstractions/Types/OptimizationLevel.cs`

**Generator-Specific Keep**: `/src/Runtime/DotCompute.Generators/Configuration/Settings/Enums/OptimizationLevel.cs`

**Files to Update/Remove**:
```
❌ /src/Runtime/DotCompute.Generators/Configuration/GeneratorSettings.cs (remove enum)
❌ /src/Runtime/DotCompute.Generators/Kernel/Generation/KernelAttributeAnalyzer.cs (remove enum)
❌ /src/Runtime/DotCompute.Generators/Models/Kernel/KernelConfiguration.cs (remove enum)
❌ /src/Backends/DotCompute.Backends.CPU/CpuKernelOptimizer.cs (remove enum)
❌ /src/Core/DotCompute.Abstractions/Pipelines/Models/PipelineOptimizationSettings.cs (remove enum)
```

**Migration Strategy**:
1. Create type alias in generator: `using OptimizationLevel = DotCompute.Abstractions.Types.OptimizationLevel;`
2. Update all non-generator files to use canonical
3. Remove duplicate enum definitions
4. Extensive testing across all backends

### 4. SecurityLevel Consolidation - Priority: MEDIUM, Effort: 3 days

**Target Location**: `/src/Core/DotCompute.Abstractions/Security/SecurityLevel.cs` (create new)

**Files to Consolidate**:
```
🔄 /src/Extensions/DotCompute.Algorithms/Types/Security/SecurityLevel.cs
🔄 /src/Extensions/DotCompute.Algorithms/Security/SecurityPolicy.cs
```

**Files to Keep (Different Purpose)**:
```
✅ /src/Core/DotCompute.Core/Security/CryptographicValidator.cs (crypto-specific)
✅ /src/Core/DotCompute.Core/Security/SecurityLogger.cs (logging severity)
```

## Critical Restructuring (Phase 3 - Weeks 4-6)

### 5. PerformanceMetrics Hierarchy - Priority: CRITICAL, Effort: 8 days

**New Architecture**:
```
/src/Core/DotCompute.Abstractions/Performance/
├── IPerformanceMetrics.cs (interface)
├── BasePerformanceMetrics.cs (base implementation)
├── ExecutionMetrics.cs (execution-specific)
└── DebugMetrics.cs (debug-specific)

/src/Runtime/DotCompute.Runtime/Services/Performance/Metrics/
└── AggregatedPerformanceMetrics.cs (comprehensive)

/src/Backends/DotCompute.Backends.CUDA/Performance/
└── CudaPerformanceMetrics.cs (hardware-specific)

/src/Backends/DotCompute.Backends.Metal/Performance/
└── MetalPerformanceMetrics.cs (hardware-specific)
```

**Files to Refactor**:
```
🔄 /src/Core/DotCompute.Abstractions/Debugging/IKernelDebugService.cs
🔄 /src/Backends/DotCompute.Backends.CPU/CpuKernelCache.cs
🔄 /src/Backends/DotCompute.Backends.CPU/CpuKernelOptimizer.cs
🔄 /src/Extensions/DotCompute.Algorithms/LinearAlgebra/Components/GpuOptimizationStrategies.cs
```

**Migration Strategy**:
1. Create interface and base class
2. Update each implementation to inherit/implement
3. Gradual migration of usage sites
4. Comprehensive testing

## Large File Splitting (Phase 4 - Ongoing)

### Priority 1: Critical Files (1500+ lines) - 2-3 weeks

#### AlgorithmPluginManager.cs (2219 lines) - Week 1
```
Split into:
📁 /src/Extensions/DotCompute.Algorithms/Management/
├── AlgorithmPluginManager.cs (core orchestration, ~400 lines)
├── AlgorithmPluginRegistry.cs (registration logic, ~500 lines)
├── AlgorithmPluginValidator.cs (validation logic, ~400 lines)
├── AlgorithmPluginLoader.cs (loading logic, ~500 lines)
└── AlgorithmPluginCache.cs (caching logic, ~300 lines)
```

#### PluginRecoveryManager.cs (1536 lines) - Week 2
```
Split into:
📁 /src/Runtime/DotCompute.Plugins/Recovery/
├── PluginRecoveryManager.cs (orchestration, ~300 lines)
├── RecoveryStrategy.cs (strategy pattern, ~400 lines)
├── RecoveryStateManager.cs (state management, ~300 lines)
├── RecoveryEventHandler.cs (event handling, ~300 lines)
└── RecoveryMetrics.cs (metrics collection, ~200 lines)
```

#### CryptographicSecurity.cs (1358 lines) - Week 3
```
Split into:
📁 /src/Core/DotCompute.Core/Security/Cryptographic/
├── CryptographicSecurity.cs (main API, ~300 lines)
├── CryptographicValidator.cs (validation, ~400 lines)
├── CryptographicProvider.cs (provider logic, ~300 lines)
├── CryptographicOperations.cs (operations, ~300 lines)
└── CryptographicUtilities.cs (utilities, ~200 lines)
```

### Priority 2: High-Impact Files (1200-1500 lines) - 3-4 weeks

#### Memory Management Files
```
UnifiedBuffer.cs (1209 lines) → 4 files
OptimizedUnifiedBuffer.cs (1148 lines) → 4 files
```

#### CUDA Runtime Files
```
CudaRuntimeExtended.cs (1188 lines) → 4 files
CudaRuntime.cs (1182 lines) → 4 files
```

#### Compilation Files
```
CpuKernelCompiler.cs (1177 lines) → 4 files
```

### Priority 3: Medium Files (1000-1200 lines) - 4-5 weeks

Focus on execution and coordination:
```
ExecutionPlanExecutor.cs (1152 lines)
WorkStealingCoordinator.cs (1145 lines)
```

## Implementation Timeline

### Week 1: Quick Wins
- [ ] ExecutionPriority consolidation
- [ ] MemoryAccessPattern cleanup
- [ ] Basic testing and validation

### Week 2-3: Complex Consolidation
- [ ] OptimizationLevel consolidation
- [ ] SecurityLevel consolidation
- [ ] Comprehensive testing

### Week 4-6: Performance Metrics Restructuring
- [ ] Design new hierarchy
- [ ] Implement base classes
- [ ] Migrate implementations
- [ ] Performance testing

### Week 7-10: Priority 1 File Splitting
- [ ] AlgorithmPluginManager split
- [ ] PluginRecoveryManager split
- [ ] CryptographicSecurity split
- [ ] Integration testing

### Week 11-16: Priority 2 File Splitting
- [ ] Memory management files
- [ ] CUDA runtime files
- [ ] Compilation files
- [ ] Performance validation

## Risk Mitigation

### Build Verification
```bash
# After each consolidation
dotnet build DotCompute.sln --configuration Release
dotnet test DotCompute.sln --configuration Release
```

### Rollback Strategy
```bash
# Create checkpoint branches
git checkout -b consolidation-checkpoint-1
git checkout -b consolidation-checkpoint-2
# etc.
```

### Testing Strategy
1. **Unit Tests**: Verify all existing unit tests pass
2. **Integration Tests**: Run full integration test suite
3. **Hardware Tests**: Validate CUDA/Metal functionality
4. **Performance Tests**: Ensure no performance regressions

## Success Criteria

### Quantitative
- [ ] Reduce duplicate types from 30 to <10
- [ ] No files over 1500 lines
- [ ] <20 files over 1000 lines
- [ ] Build time improvement 10-15%
- [ ] Maintain >80% test coverage

### Qualitative
- [ ] Clear type organization
- [ ] Improved maintainability
- [ ] Better developer experience
- [ ] Consistent architecture

## Post-Implementation

### Code Quality Gates
1. **Analyzer Rules**: Prevent future duplicates
2. **Review Process**: Type definition reviews
3. **Documentation**: Updated architecture docs
4. **Templates**: Standard file organization

### Monitoring
1. **Build Performance**: Track compilation times
2. **Code Metrics**: Monitor file sizes and complexity
3. **Developer Feedback**: Survey team satisfaction
4. **Maintenance Burden**: Track time to implement changes

---

**Next Action**: Begin Phase 1 with ExecutionPriority consolidation
**Est. Total Effort**: 12-16 weeks (with dedicated developer time)
**Risk Level**: Medium (with proper checkpoints and testing)