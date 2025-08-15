# Test Execution Optimization Strategies

## Overview
This document outlines the test execution strategies to reduce CI/CD pipeline time from 30+ minutes to under 10 minutes for standard builds.

## Test Categories

### 1. Quick Tests (~2-3 minutes)
- **Categories**: Unit, Mock, CI
- **Purpose**: Fast feedback on basic functionality
- **When**: Every commit, PR validation
- **Command**: `dotnet test --filter "Category=Unit|Category=Mock|Category=CI"`

### 2. Integration Tests (~5-7 minutes)
- **Categories**: Integration (excluding hardware)
- **Purpose**: Validate component interactions
- **When**: Push to main/develop branches
- **Command**: `dotnet test --filter "Category=Integration&Category!=Hardware"`

### 3. Hardware Tests (~15-20 minutes)
- **Categories**: Hardware, CudaRequired, RTX2000, OpenCLRequired
- **Purpose**: Validate GPU/hardware functionality
- **When**: Nightly builds, release candidates, manual trigger
- **Command**: `dotnet test --filter "Category=Hardware|Category=CudaRequired"`

### 4. Performance Tests (~10 minutes)
- **Categories**: Performance, Benchmark, Stress
- **Purpose**: Performance regression detection
- **When**: Weekly, before releases
- **Command**: `dotnet test --filter "Category=Performance|Category=Benchmark"`

## Local Development

### Run all tests (default behavior):
```bash
dotnet test
```

### Run quick tests only:
```bash
dotnet test --filter "Category=Unit|Category=Mock"
```

### Run specific hardware tests:
```bash
dotnet test --filter "Category=CudaRequired"
```

### Skip slow tests:
```bash
dotnet test --filter "Category!=Stress&Category!=Performance"
```

## CI/CD Environment Variables

- `CI=true` - Activates CI test filtering
- `QUICK_TESTS=true` - Only run unit/mock tests
- `HARDWARE_TESTS=true` - Only run hardware tests
- `SKIP_SLOW_TESTS=true` - Skip stress/performance tests

## Test Parallelization

### Enable parallel execution:
```xml
<RunConfiguration>
  <MaxCpuCount>0</MaxCpuCount> <!-- Use all available cores -->
  <TestCaseOrder>FullyQualifiedName</TestCaseOrder>
  <DisableParallelization>false</DisableParallelization>
</RunConfiguration>
```

### In test projects:
```csharp
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
```

## Test Timeouts

Set appropriate timeouts to prevent hanging tests:

```csharp
[Fact(Timeout = 5000)] // 5 seconds
public async Task QuickTest() { }

[Fact(Timeout = 30000)] // 30 seconds
public async Task IntegrationTest() { }

[Fact(Skip = "Manual execution only")]
public async Task LongRunningHardwareTest() { }
```

## Optimization Techniques Applied

1. **Test Categorization**: All tests tagged with appropriate categories
2. **Parallel Execution**: Tests run in parallel where possible
3. **Build Caching**: NuGet and build outputs cached
4. **Selective Testing**: Only relevant tests run based on changes
5. **Hardware Isolation**: GPU tests separated to dedicated runners
6. **Timeout Protection**: Hanging tests killed after reasonable time
7. **Early Failure**: Quick tests fail fast before longer tests

## Expected Time Savings

### Before Optimization:
- All tests: 30+ minutes
- No parallelization
- No categorization
- Hardware tests on standard runners

### After Optimization:
- Quick feedback: 2-3 minutes
- Standard CI: 7-10 minutes  
- Full validation: 20-25 minutes (with hardware)
- Hardware tests: Only on dedicated GPU runners

## Monitoring Test Performance

Track test execution times:
```bash
dotnet test --logger "console;verbosity=detailed" --blame-hang-timeout 60s
```

Generate timing reports:
```bash
dotnet test --logger "trx" --results-directory ./TestResults
```

## Best Practices

1. **Tag new tests appropriately** with Category attributes
2. **Keep unit tests fast** (<100ms per test)
3. **Mock external dependencies** in unit tests
4. **Use TestContainers** for integration tests needing databases
5. **Parallelize independent tests** using xUnit collections
6. **Set reasonable timeouts** to prevent hanging
7. **Skip flaky tests in CI** but track for fixing
8. **Use data-driven tests** to reduce test code duplication