# DotCompute Shared Test Utilities

This library consolidates all valuable test utilities that were preserved from the demolition of obsolete test projects.

## Preserved Components

### TestDataGenerator.cs
- **Source**: Originally from `DotCompute.Core.Tests/TestHelpers/TestDataGenerator.cs`
- **Purpose**: Comprehensive test data generation for arrays, matrices, and random data
- **Key Features**:
  - Deterministic random number generation (seed = 42)
  - Support for int, float, double, and generic arrays
  - Matrix multiplication test case generation
  - Sparse array generation
  - Edge case and standard size test cases

### MemoryTestUtilities.cs
- **Source**: Originally from `DotCompute.Core.Tests/Memory/MemoryTestUtilities.cs`
- **Purpose**: Advanced memory testing, monitoring, and performance analysis
- **Key Features**:
  - Memory monitoring with snapshots
  - Performance benchmarking for memory operations
  - Memory leak detection
  - Automated test reporting with JSON serialization
  - Performance regression detection
  - Historical report analysis

## Usage

Add a reference to this project in your test projects:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\tests\DotCompute.SharedTestUtilities\DotCompute.SharedTestUtilities.csproj" />
</ItemGroup>
```

## Example Usage

### Test Data Generation
```csharp
using DotCompute.SharedTestUtilities;

// Generate test arrays
var intArray = TestDataGenerator.GenerateIntArray(1000, 0, 100);
var floatArray = TestDataGenerator.GenerateFloatArray(1000, -1.0f, 1.0f);

// Generate matrix test cases
var (matrixA, matrixB, expected) = TestDataGenerator.GenerateMatrixMultiplicationTestCase(10, 10, 10);

// Generate sparse data
var (indices, values, length) = TestDataGenerator.GenerateSparseArray<float>(1000, 0.9, () => Random.Next());
```

### Memory Testing
```csharp
using DotCompute.SharedTestUtilities;

// Monitor memory usage
using var monitor = new MemoryTestUtilities.MemoryMonitor("MyTest");

// Perform operations
monitor.TakeSnapshot("After allocation");
// ... test operations ...
monitor.TakeSnapshot("After operations");

// Generate report
var report = monitor.GenerateReport();
await MemoryTestUtilities.TestReporter.SaveMemoryReport(report);
```

### Performance Benchmarking
```csharp
using DotCompute.SharedTestUtilities;

var benchmark = new MemoryTestUtilities.MemoryPerformanceBenchmark();
var result = await benchmark.BenchmarkAllocation(memoryManager, 1024 * 1024, 100);
var summary = benchmark.GenerateSummary();
```

## Migration Notes

The following obsolete test projects were demolished and their utilities consolidated here:

- **DotCompute.Abstractions.Tests** - Interface contract tests
- **DotCompute.Backends.CUDA.Tests** - CUDA backend tests  
- **DotCompute.Backends.Metal.Tests** - Metal backend tests
- **DotCompute.Core.Tests** - Core functionality tests (source of utilities)
- **DotCompute.Generators.Tests** - Code generator tests
- **DotCompute.Integration.Tests** - Integration test scenarios
- **DotCompute.Memory.Tests** - Memory management tests
- **DotCompute.Performance.Benchmarks** - Performance benchmarking
- **DotCompute.Plugins.Tests** - Plugin system tests
- **DotCompute.Runtime.Tests** - Runtime service tests
- **DotCompute.TestUtilities** - Original test utilities project

## Test Patterns Worth Preserving

The following test patterns were identified as valuable and should be considered for future test implementations:

1. **Memory Leak Detection**: Automated detection of memory leaks in tests
2. **Performance Regression Detection**: Comparing current vs baseline performance
3. **Deterministic Test Data**: Using fixed seeds for reproducible tests
4. **Matrix Operation Testing**: Comprehensive matrix multiplication verification
5. **Sparse Data Testing**: Testing with sparse arrays and data structures
6. **Memory Monitoring**: Real-time memory usage tracking during tests
7. **Benchmark Reporting**: JSON-based performance report generation
8. **Historical Analysis**: Comparing test results over time

## Future Considerations

- Consider adding GPU-specific test utilities if GPU backends are re-implemented
- Add integration test helpers for multi-backend scenarios
- Extend performance benchmarking to include GPU operations
- Add test utilities for kernel compilation and execution
- Consider adding test fixtures for complex integration scenarios

## License

This code is licensed under the MIT License, consistent with the DotCompute project.