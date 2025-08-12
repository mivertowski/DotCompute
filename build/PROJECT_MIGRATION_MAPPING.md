# Project Migration Mapping

## Source Projects (8 projects)

| Current Location | New Location | Category | Dependencies |
|-----------------|-------------|----------|-------------|
| `src/DotCompute.Abstractions/` | `src/Core/DotCompute.Abstractions/` | Core | None |
| `src/DotCompute.Core/` | `src/Core/DotCompute.Core/` | Core | Abstractions |
| `src/DotCompute.Memory/` | `src/Core/DotCompute.Memory/` | Core | Abstractions |
| `src/DotCompute.Linq/` | `src/Extensions/DotCompute.Linq/` | Extension | Core, Memory |
| `src/DotCompute.Algorithms/` | `src/Extensions/DotCompute.Algorithms/` | Extension | Core, Memory |
| `src/DotCompute.Generators/` | `src/Tools/DotCompute.Generators/` | Tool | Abstractions |
| `src/DotCompute.Plugins/` | `src/Tools/DotCompute.Plugins/` | Tool | Abstractions |
| `src/DotCompute.Runtime/` | `src/Runtime/DotCompute.Runtime/` | Runtime | All above |

## Backend Projects (5 projects)

| Current Location | New Location | Category |
|-----------------|-------------|----------|
| `plugins/backends/DotCompute.Backends.CPU/` | `src/Backends/DotCompute.Backends.CPU/` | Backend |
| `plugins/backends/DotCompute.Backends.CUDA/` | `src/Backends/DotCompute.Backends.CUDA/` | Backend |
| `plugins/backends/DotCompute.Backends.Metal/` | `src/Backends/DotCompute.Backends.Metal/` | Backend |
| `plugins/backends/DotCompute.Backends.CPU/tests/` | `tests/Unit/DotCompute.Backends.CPU.Tests/` | Test |
| `plugins/backends/DotCompute.Backends.Metal/tests/` | `tests/Unit/DotCompute.Backends.Metal.Tests/` | Test |

## Test Projects (21 projects)

### Unit Tests
| Current Location | New Location |
|-----------------|-------------|
| `tests/Unit/DotCompute.Abstractions.Tests/` | `tests/Unit/DotCompute.Abstractions.Tests/` |
| `tests/Unit/DotCompute.Core.Tests/` | `tests/Unit/DotCompute.Core.Tests/` |
| `tests/Unit/DotCompute.Memory.Tests/` | `tests/Unit/DotCompute.Memory.Tests/` |
| `tests/Unit/DotCompute.Plugins.Tests/` | `tests/Unit/DotCompute.Plugins.Tests/` |
| `tests/Unit/DotCompute.Generators.Tests/` | `tests/Unit/DotCompute.Generators.Tests/` |
| `tests/Unit/DotCompute.BasicTests/` | `tests/Unit/DotCompute.BasicTests/` |
| `tests/Unit/DotCompute.Core.UnitTests/` | `tests/Unit/DotCompute.Core.UnitTests/` |
| `tests/Unit/DotCompute.Algorithms.Tests/` | `tests/Unit/DotCompute.Algorithms.Tests/` |
| `tests/DotCompute.Algorithms.Tests/` | **MERGE** → `tests/Unit/DotCompute.Algorithms.Tests/` |
| `tests/DotCompute.Memory.Tests/` | **MERGE** → `tests/Unit/DotCompute.Memory.Tests/` |
| `tests/Linq.Tests/` | `tests/Unit/DotCompute.Linq.Tests/` |

### Integration Tests
| Current Location | New Location |
|-----------------|-------------|
| `tests/Integration/DotCompute.Integration.Tests/` | `tests/Integration/DotCompute.Integration.Tests/` |

### Performance Tests
| Current Location | New Location |
|-----------------|-------------|
| `tests/Performance/DotCompute.Performance.Tests/` | `tests/Performance/DotCompute.Performance.Tests/` |
| `benchmarks/DotCompute.Benchmarks/` | `tests/Performance/DotCompute.Benchmarks/` |

### Hardware Tests
| Current Location | New Location |
|-----------------|-------------|
| `tests/Hardware/DotCompute.Hardware.Cuda.Tests/` | `tests/Hardware/DotCompute.Hardware.CUDA.Tests/` |
| `tests/Hardware/DotCompute.Hardware.OpenCL.Tests/` | `tests/Hardware/DotCompute.Hardware.OpenCL.Tests/` |
| `tests/Hardware/DotCompute.Hardware.DirectCompute.Tests/` | `tests/Hardware/DotCompute.Hardware.DirectCompute.Tests/` |
| `tests/Hardware/DotCompute.Hardware.Mock.Tests/` | `tests/Hardware/DotCompute.Hardware.Mock.Tests/` |
| `tests/Hardware/DotCompute.Hardware.RTX2000.Tests/` | `tests/Hardware/DotCompute.Hardware.RTX2000.Tests/` |

### Shared Test Utilities
| Current Location | New Location |
|-----------------|-------------|
| `tests/Shared/DotCompute.Tests.Common/` | `tests/Shared/DotCompute.Tests.Common/` |
| `tests/Shared/DotCompute.Tests.Mocks/` | `tests/Shared/DotCompute.Tests.Mocks/` |
| `tests/Shared/DotCompute.SharedTestUtilities/` | `tests/Shared/DotCompute.SharedTestUtilities/` |
| `tests/Shared/DotCompute.Tests.Implementations/` | `tests/Shared/DotCompute.Tests.Implementations/` |

## Sample Projects (4 projects)

| Current Location | New Location |
|-----------------|-------------|
| `samples/GettingStarted/` | `samples/GettingStarted/` |
| `samples/SimpleExample/` | `samples/SimpleExample/` |
| `samples/CpuKernelCompilationExample/` | `samples/CpuKernelCompilationExample/` |
| `samples/KernelExample/` | `samples/KernelExample/` |
| `examples/ExpressionToDynamicKernel/` | `samples/ExpressionToDynamicKernel/` |

## Tool Projects (3 projects)

| Current Location | New Location |
|-----------------|-------------|
| `tools/coverage-analyzer/` | `tools/coverage-analyzer/` |
| `tools/coverage-enhancer/` | `tools/coverage-enhancer/` |
| `tools/coverage-validator/` | `tools/coverage-validator/` |

## Special Cases and Merges

### Duplicate Test Projects
- `tests/Unit/DotCompute.Algorithms.Tests/` + `tests/DotCompute.Algorithms.Tests/` → Single project
- `tests/Unit/DotCompute.Memory.Tests/` + `tests/DotCompute.Memory.Tests/` → Single project

### Orphaned Projects
- `CudaTest/` → **DELETE** (appears to be temporary test project)

### Examples vs Samples
- Move all examples to samples for consistency
- Maintain clear naming convention

## Migration Dependencies

### Phase 1: Core Foundation
1. `DotCompute.Abstractions` (no dependencies)
2. `DotCompute.Core` (depends on Abstractions)
3. `DotCompute.Memory` (depends on Abstractions)

### Phase 2: Extensions
1. `DotCompute.Linq` (depends on Core, Memory)
2. `DotCompute.Algorithms` (depends on Core, Memory)

### Phase 3: Tools
1. `DotCompute.Generators` (depends on Abstractions)
2. `DotCompute.Plugins` (depends on Abstractions)

### Phase 4: Runtime
1. `DotCompute.Runtime` (depends on all above)

### Phase 5: Backends
1. Backend projects (plugin architecture - loose coupling)

### Phase 6: Tests and Samples
1. Test projects (depend on source projects)
2. Sample projects (depend on runtime + backends)

## Namespace Migration Strategy

Most projects will keep their current namespaces since the folder structure change doesn't require namespace changes. However, we should consider:

1. **Consistency Check**: Ensure namespaces align with new structure
2. **Backend Namespaces**: Keep `DotCompute.Backends.*` pattern
3. **Test Namespaces**: Keep `*.Tests` suffix pattern
4. **Tool Namespaces**: Consider if any adjustments needed

## File and Folder Considerations

### Files to Move with Projects
- All `.cs` files
- `*.csproj` files
- `README.md` files
- Any native dependencies (like Metal's `libDotComputeMetal.dylib`)

### Files to Update References
- Solution file (`.sln`)
- Directory.Build.props files
- Any documentation with path references
- CI/CD workflow files

### Build System Impact
- Update MSBuild project references
- Update test discovery patterns
- Update package output paths
- Update CI/CD build scripts