# DotCompute Test Structure

## Overview

The DotCompute test suite follows a professional, organized structure with clear separation between different types of tests. This ensures maintainability, scalability, and clear understanding of test dependencies.

## Directory Structure

```
tests/
├── Unit/                              # Hardware-independent unit tests
│   ├── DotCompute.Abstractions.Tests/
│   ├── DotCompute.Algorithms.Tests/
│   ├── DotCompute.BasicTests/
│   ├── DotCompute.Core.Tests/
│   ├── DotCompute.Core.UnitTests/
│   ├── DotCompute.Generators.Tests/
│   ├── DotCompute.Memory.Tests/
│   └── DotCompute.Plugins.Tests/
│
├── Integration/                       # Integration tests (hardware-independent)
│   └── DotCompute.Integration.Tests/
│
├── Hardware/                          # Hardware-dependent tests
│   ├── DotCompute.Hardware.Cuda.Tests/
│   ├── DotCompute.Hardware.OpenCL.Tests/
│   ├── DotCompute.Hardware.DirectCompute.Tests/
│   └── DotCompute.Hardware.Mock.Tests/
│
└── Shared/                           # Shared test infrastructure
    ├── DotCompute.Tests.Common/     # Common test utilities
    ├── DotCompute.Tests.Mocks/      # Mock implementations
    └── DotCompute.Tests.Implementations/ # Test implementations

benchmarks/                           # Performance benchmarks (separate from tests)
└── DotCompute.Benchmarks/
```

## Test Categories

### Unit Tests (`tests/Unit/`)
**Purpose**: Test individual components in isolation without external dependencies.

- **DotCompute.Abstractions.Tests**: Core abstraction tests
- **DotCompute.Algorithms.Tests**: Algorithm implementation tests
- **DotCompute.BasicTests**: Basic functionality tests
- **DotCompute.Core.Tests**: Core functionality tests
- **DotCompute.Core.UnitTests**: Additional core unit tests
- **DotCompute.Generators.Tests**: Source generator tests
- **DotCompute.Memory.Tests**: Memory management tests
- **DotCompute.Plugins.Tests**: Plugin system tests

**Characteristics**:
- No hardware dependencies
- Fast execution (<100ms per test)
- Use mocks for external dependencies
- Run on all platforms

### Integration Tests (`tests/Integration/`)
**Purpose**: Test component interactions and complete workflows.

- **DotCompute.Integration.Tests**: End-to-end integration scenarios

**Characteristics**:
- May use real implementations
- Test complete workflows
- Hardware-independent
- Longer execution time acceptable

### Hardware Tests (`tests/Hardware/`)
**Purpose**: Test GPU and accelerator functionality with real hardware.

- **DotCompute.Hardware.Cuda.Tests**: NVIDIA CUDA GPU tests
- **DotCompute.Hardware.OpenCL.Tests**: OpenCL device tests
- **DotCompute.Hardware.DirectCompute.Tests**: DirectX Compute Shader tests
- **DotCompute.Hardware.Mock.Tests**: Mock hardware tests for CI/CD

**Characteristics**:
- Require specific hardware/drivers
- Use `[SkippableFact]` for conditional execution
- Platform-specific (e.g., DirectCompute on Windows)
- May require environment setup (e.g., `LD_LIBRARY_PATH`)

### Shared Infrastructure (`tests/Shared/`)
**Purpose**: Provide common utilities and mock implementations for all tests.

- **DotCompute.Tests.Common**: Shared utilities, helpers, fixtures
- **DotCompute.Tests.Mocks**: Mock implementations of interfaces
- **DotCompute.Tests.Implementations**: Test-specific implementations

**Characteristics**:
- Not test projects themselves
- Referenced by other test projects
- Contain reusable code
- Maintain consistency across tests

## Naming Conventions

### Project Names
- Unit tests: `DotCompute.[Component].Tests`
- Hardware tests: `DotCompute.Hardware.[Technology].Tests`
- Shared: `DotCompute.Tests.[Purpose]`

### Namespaces
- Unit tests: `DotCompute.[Component].Tests`
- Hardware tests: `DotCompute.Hardware.[Technology].Tests`
- Shared: `DotCompute.Tests.[Purpose]`

### Test Classes
- Suffix with `Tests`: `AcceleratorManagerTests`
- Group related tests: `MemoryAllocationTests`
- Hardware-specific: `CudaHardwareTests`, `OpenCLHardwareTests`

### Test Methods
- Pattern: `Should_[ExpectedBehavior]_When_[Condition]`
- Examples:
  - `Should_AllocateMemory_When_ValidSizeProvided`
  - `Should_ThrowException_When_InvalidKernelSource`
  - `Should_DetectCudaDevices_When_DriverInstalled`

## Running Tests

### All Tests
```bash
dotnet test
```

### By Category
```bash
# Unit tests only
dotnet test tests/Unit/**/*.csproj

# Integration tests only
dotnet test tests/Integration/**/*.csproj

# Hardware tests (requires hardware)
dotnet test tests/Hardware/**/*.csproj
```

### By Test Trait
```bash
# Skip GPU tests
dotnet test --filter "Category!=RequiresGPU"

# Only CUDA tests
dotnet test --filter "Category=CUDA"

# Only OpenCL tests
dotnet test --filter "Category=OpenCL"
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

## CI/CD Integration

### GitHub Actions Configuration
```yaml
- name: Run Unit Tests
  run: dotnet test tests/Unit/**/*.csproj

- name: Run Integration Tests
  run: dotnet test tests/Integration/**/*.csproj

- name: Run Hardware Mock Tests
  run: dotnet test tests/Hardware/DotCompute.Hardware.Mock.Tests
```

### Hardware Test Execution
Hardware tests should be run conditionally:
```yaml
- name: Run CUDA Tests
  if: runner.gpu == 'nvidia'
  run: |
    export LD_LIBRARY_PATH=/usr/lib/wsl/lib:$LD_LIBRARY_PATH
    dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests
```

## Best Practices

### 1. Test Organization
- Keep unit tests close to the code they test
- Separate hardware-dependent tests
- Share common utilities through Shared projects

### 2. Mock Usage
- Use `DotCompute.Tests.Mocks` for consistent mocking
- Create specific mocks for different scenarios
- Avoid over-mocking in integration tests

### 3. Hardware Tests
- Always use `[SkippableFact]` for hardware tests
- Check hardware availability before testing
- Provide clear skip reasons
- Document required environment setup

### 4. Performance
- Unit tests should be fast (<100ms)
- Integration tests can be slower (<1s)
- Hardware tests depend on hardware capabilities
- Use benchmarks project for performance testing

## Adding New Tests

### Adding a Unit Test
1. Create test class in appropriate Unit project
2. Use namespace matching project name
3. Reference `DotCompute.Tests.Common` if needed
4. Follow naming conventions

### Adding a Hardware Test
1. Determine hardware requirement (CUDA, OpenCL, DirectCompute)
2. Add to appropriate Hardware project
3. Use `[SkippableFact]` with hardware detection
4. Document environment requirements

### Adding Shared Utilities
1. Add to `DotCompute.Tests.Common`
2. Keep utilities generic and reusable
3. Document usage patterns
4. Update dependent projects if needed

## Migration from Old Structure

### Old Structure → New Structure
- `tests/DotCompute.SharedTestUtilities` → `tests/Shared/DotCompute.Tests.Common`
- `tests/DotCompute.TestDoubles` → `tests/Shared/DotCompute.Tests.Mocks`
- `tests/DotCompute.Hardware.RealTests` → `tests/Hardware/DotCompute.Hardware.Cuda.Tests`
- `tests/DotCompute.[Component].Tests` → `tests/Unit/DotCompute.[Component].Tests`

### Namespace Updates
- `DotCompute.SharedTestUtilities` → `DotCompute.Tests.Common`
- `DotCompute.TestDoubles` → `DotCompute.Tests.Mocks`
- `DotCompute.Hardware.RealTests` → `DotCompute.Hardware.Cuda.Tests`

## Conclusion

The reorganized test structure provides:
- **Clear separation** between test types
- **Professional naming** conventions
- **Scalable organization** for future growth
- **Easy CI/CD integration**
- **Consistent patterns** across the codebase

This structure makes it easy to:
- Identify test dependencies
- Run specific test categories
- Add new tests in the right location
- Maintain and update tests over time